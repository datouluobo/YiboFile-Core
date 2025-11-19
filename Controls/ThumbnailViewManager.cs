using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OoiMRR.Controls.Converters;

namespace OoiMRR.Controls
{
    /// <summary>
    /// 缩略图视图管理器
    /// 负责管理缩略图视图的加载逻辑，包括优先加载、异步加载等
    /// </summary>
    public class ThumbnailViewManager
    {
        private readonly ListView _listView;
        private readonly double _thumbnailSize;
        private const int BatchSize = 50; // 每批加载的文件数量（从20增加到50，加速加载）
        private const int ScrollLoadBatchSize = 30; // 滚动时每批加载的数量
        private const int BatchDelayMs = 10; // 批处理之间的延迟（从50ms减少到10ms）
        private const int InitialWaitMs = 50; // 初始等待时间（从200ms减少到50ms，更快开始加载第二页）
        private const int ScrollDelayMs = 30; // 滚动加载延迟（从100ms减少到30ms，更快响应）
        private const int MaxConcurrentLoads = 8; // 最大并发加载数（并行加载加速）
        private ScrollViewer _scrollViewer;
        private bool _isLoading = false;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _loadingTasks = new System.Collections.Concurrent.ConcurrentDictionary<string, Task>();

        public ThumbnailViewManager(ListView listView, double thumbnailSize)
        {
            _listView = listView ?? throw new ArgumentNullException(nameof(listView));
            _thumbnailSize = thumbnailSize;
            
            // 监听ListView的Loaded事件，查找ScrollViewer并监听滚动
            _listView.Loaded += ListView_LoadedForScroll;
        }
        
        private void ListView_LoadedForScroll(object sender, RoutedEventArgs e)
        {
            _listView.Loaded -= ListView_LoadedForScroll;
            
            // 查找ScrollViewer
            _scrollViewer = FindVisualChild<ScrollViewer>(_listView);
            if (_scrollViewer != null)
            {
                // 监听滚动事件
                _scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
            }
        }
        
        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // 当滚动时，检查可见区域的项目并加载缩略图
            if (!_isLoading)
            {
                _isLoading = true;
                Task.Delay(ScrollDelayMs).ContinueWith(_ =>
                {
                    Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        LoadVisibleThumbnails();
                        _isLoading = false;
                    }));
                });
            }
        }
        
        /// <summary>
        /// 加载可见区域的缩略图
        /// </summary>
        private void LoadVisibleThumbnails()
        {
            if (_listView == null || _scrollViewer == null)
                return;
                
            try
            {
                var itemsSource = _listView.ItemsSource;
                if (itemsSource == null)
                    return;
                    
                var containerGenerator = _listView.ItemContainerGenerator;
                int loadedCount = 0;
                int index = 0;
                
                foreach (var item in itemsSource)
                {
                    if (loadedCount >= ScrollLoadBatchSize)
                        break;
                        
                    var container = containerGenerator.ContainerFromIndex(index) as ListViewItem;
                    if (container != null)
                    {
                        // 检查项目是否在可见区域内
                        var containerBounds = container.TransformToAncestor(_scrollViewer).TransformBounds(
                            new Rect(0, 0, container.RenderSize.Width, container.RenderSize.Height));
                        var scrollViewerBounds = new Rect(0, 0, _scrollViewer.ViewportWidth, _scrollViewer.ViewportHeight);
                        
                        // 如果项目在可见区域内或接近可见区域（提前加载下方800px的内容，增加预加载范围）
                        if (containerBounds.IntersectsWith(scrollViewerBounds) || 
                            containerBounds.Top < scrollViewerBounds.Bottom + 800) // 从500px增加到800px，提前加载更多
                        {
                            var image = FindVisualChild<Image>(container);
                            if (image != null && image.Source is RenderTargetBitmap)
                            {
                                var pathProperty = item.GetType().GetProperty("Path");
                                if (pathProperty != null)
                                {
                                    var path = pathProperty.GetValue(item) as string;
                                    if (!string.IsNullOrEmpty(path))
                                    {
                                        LoadThumbnailForItemAsync(path, image, (int)_thumbnailSize);
                                        loadedCount++;
                                    }
                                }
                            }
                        }
                    }
                    index++;
                }
            }
            catch { }
        }

        /// <summary>
        /// 计算第一页能显示的文件数量并设置优先加载
        /// </summary>
        public void CalculateAndSetPriorityLoad()
        {
            if (_listView == null)
                return;

            try
            {
                // 获取ListView的实际可用宽度和高度
                var actualWidth = _listView.ActualWidth;
                var actualHeight = _listView.ActualHeight;

                // 如果尺寸还未确定，等待布局完成后再计算
                if (actualWidth <= 0 || actualHeight <= 0)
                {
                    _listView.Loaded += ListView_LoadedForPriority;
                    _listView.LayoutUpdated += ListView_LayoutUpdatedForPriority;
                    return;
                }

                // 计算每行能显示多少个文件
                var itemWidth = _thumbnailSize + 24; // 每个项目的宽度（缩略图 + 边距）
                var itemsPerRow = Math.Max(1, (int)Math.Floor(actualWidth / itemWidth));

                // 计算每列的高度（缩略图高度 + 文本高度 + 边距）
                var itemHeight = _thumbnailSize + 80 + 12; // 缩略图 + 文本（最多4行） + 边距
                var rowsPerPage = Math.Max(1, (int)Math.Floor(actualHeight / itemHeight));

                // 第一页的文件数量（多加载一些，确保滚动时也有内容）
                var firstPageCount = itemsPerRow * rowsPerPage * 2; // 加载2页的内容

                // 获取文件列表
                var itemsSource = _listView.ItemsSource;
                if (itemsSource == null)
                {
                    ThumbnailConverter.ClearPriorityLoadPaths();
                    return;
                }

                // 提取文件路径
                var filePaths = new List<string>();
                int count = 0;
                foreach (var item in itemsSource)
                {
                    if (count >= firstPageCount)
                        break;

                    // 假设item有Path属性（根据实际的数据模型调整）
                    var pathProperty = item.GetType().GetProperty("Path");
                    if (pathProperty != null)
                    {
                        var path = pathProperty.GetValue(item) as string;
                        if (!string.IsNullOrEmpty(path))
                        {
                            filePaths.Add(path);
                            count++;
                        }
                    }
                }

                // 设置优先加载路径
                ThumbnailConverter.SetPriorityLoadPaths(filePaths);
            }
            catch
            {
                // 如果计算失败，清除优先加载列表
                ThumbnailConverter.ClearPriorityLoadPaths();
            }
        }

        private void ListView_LoadedForPriority(object sender, RoutedEventArgs e)
        {
            _listView.Loaded -= ListView_LoadedForPriority;
            CalculateAndSetPriorityLoad();
        }

        private void ListView_LayoutUpdatedForPriority(object sender, EventArgs e)
        {
            _listView.LayoutUpdated -= ListView_LayoutUpdatedForPriority;
            CalculateAndSetPriorityLoad();
        }

        /// <summary>
        /// 异步加载非优先文件的缩略图（分批加载，避免一次性加载太多）
        /// </summary>
        public void LoadThumbnailsAsync()
        {
            if (_listView == null)
                return;

            Task.Run(() =>
            {
                try
                {
                    var itemsSource = _listView.ItemsSource;
                    if (itemsSource == null)
                        return;

                    // 减少等待时间，更快开始加载第二页（从200ms减少到50ms）
                    System.Threading.Thread.Sleep(InitialWaitMs);

                    Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                    {
                        try
                        {
                            // 分批加载，每次加载更多（BatchSize已增加到50）
                            var containerGenerator = _listView.ItemContainerGenerator;
                            int index = 0;
                            int loadedCount = 0;

                            foreach (var item in itemsSource)
                            {
                                if (loadedCount >= BatchSize)
                                {
                                    // 减少延迟，更快加载下一批（从50ms减少到10ms）
                                    Task.Delay(BatchDelayMs).ContinueWith(_ =>
                                    {
                                        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                                        {
                                            LoadThumbnailsBatch(itemsSource, containerGenerator, index, BatchSize);
                                        }));
                                    });
                                    break;
                                }

                                var container = containerGenerator.ContainerFromIndex(index) as ListViewItem;
                                if (container != null)
                                {
                                    // 查找Image控件
                                    var image = FindVisualChild<Image>(container);
                                    if (image != null && image.Source != null)
                                    {
                                        // 检查是否是占位符（通过检查是否是RenderTargetBitmap来判断）
                                        if (image.Source is RenderTargetBitmap)
                                        {
                                            // 获取文件路径
                                            var pathProperty = item.GetType().GetProperty("Path");
                                            if (pathProperty != null)
                                            {
                                                var path = pathProperty.GetValue(item) as string;
                                                if (!string.IsNullOrEmpty(path))
                                                {
                                                    // 异步加载实际缩略图（并行加载）
                                                    LoadThumbnailForItemAsync(path, image, (int)_thumbnailSize);
                                                    loadedCount++;
                                                }
                                            }
                                        }
                                    }
                                }
                                index++;
                            }
                        }
                        catch { }
                    }));
                }
                catch { }
            });
        }

        /// <summary>
        /// 分批加载缩略图
        /// </summary>
        private void LoadThumbnailsBatch(IEnumerable itemsSource, ItemContainerGenerator containerGenerator, int startIndex, int batchSize)
        {
            try
            {
                int index = startIndex;
                int loadedCount = 0;

                foreach (var item in itemsSource)
                {
                    if (index < startIndex)
                    {
                        index++;
                        continue;
                    }

                    if (loadedCount >= batchSize)
                        break;

                    var container = containerGenerator.ContainerFromIndex(index) as ListViewItem;
                    if (container != null)
                    {
                        var image = FindVisualChild<Image>(container);
                        if (image != null && image.Source is RenderTargetBitmap)
                        {
                            var pathProperty = item.GetType().GetProperty("Path");
                            if (pathProperty != null)
                            {
                                var path = pathProperty.GetValue(item) as string;
                                if (!string.IsNullOrEmpty(path))
                                {
                                    LoadThumbnailForItemAsync(path, image, (int)_thumbnailSize);
                                    loadedCount++;
                                }
                            }
                        }
                    }
                    index++;
                }

                // 如果还有更多项目，继续加载下一批（减少延迟，更快加载）
                if (loadedCount >= batchSize)
                {
                    Task.Delay(BatchDelayMs).ContinueWith(_ =>
                    {
                        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            LoadThumbnailsBatch(itemsSource, containerGenerator, index, batchSize);
                        }));
                    });
                }
            }
            catch { }
        }

        /// <summary>
        /// 为指定项异步加载缩略图（支持并发控制，避免过多并发任务）
        /// </summary>
        private void LoadThumbnailForItemAsync(string path, Image image, int targetSize)
        {
            // 检查是否已经在加载中，避免重复加载
            if (_loadingTasks.ContainsKey(path))
                return;

            var loadTask = Task.Run(() =>
            {
                try
                {
                    // 使用ThumbnailConverter的同步加载方法
                    var converter = new ThumbnailConverter();
                    var thumbnail = converter.LoadThumbnailSync(path, targetSize);

                    if (thumbnail != null)
                    {
                        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            try
                            {
                                if (image != null && image.Source is RenderTargetBitmap)
                                {
                                    image.Source = thumbnail;
                                }
                            }
                            catch { }
                            finally
                            {
                                // 加载完成后移除任务记录
                                _loadingTasks.TryRemove(path, out _);
                            }
                        }));
                    }
                    else
                    {
                        _loadingTasks.TryRemove(path, out _);
                    }
                }
                catch
                {
                    _loadingTasks.TryRemove(path, out _);
                }
            });

            // 记录加载任务
            _loadingTasks[path] = loadTask;

            // 如果并发任务过多，等待一些任务完成
            if (_loadingTasks.Count > MaxConcurrentLoads)
            {
                // 等待最早的任务完成，避免过多并发
                Task.Run(async () =>
                {
                    await Task.Delay(10);
                });
            }
        }

        /// <summary>
        /// 查找视觉树中的子元素
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        /// <summary>
        /// 清除优先加载列表
        /// </summary>
        public void ClearPriorityLoad()
        {
            ThumbnailConverter.ClearPriorityLoadPaths();
            // 清除加载任务记录
            _loadingTasks.Clear();
        }
    }
}


