using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        private const int BatchSize = 20; // 每批加载的文件数量（减少到20，降低内存压力）
        private const int ScrollLoadBatchSize = 15; // 滚动时每批加载的数量（减少到15）
        private const int BatchDelayMs = 50; // 批处理之间的延迟（增加到50ms，给UI更多响应时间）
        private const int InitialWaitMs = 100; // 初始等待时间（增加到100ms，确保UI先响应）
        private const int ScrollDelayMs = 100; // 滚动加载延迟（增加到100ms，减少频繁触发）
        private const int MaxConcurrentLoads = 4; // 最大并发加载数（减少到4，降低内存和CPU压力）
        private ScrollViewer _scrollViewer;
        private bool _isLoading = false;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task> _loadingTasks = new System.Collections.Concurrent.ConcurrentDictionary<string, Task>();
        private readonly SemaphoreSlim _concurrencySemaphore = new SemaphoreSlim(MaxConcurrentLoads, MaxConcurrentLoads);

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
                var cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
                Task.Delay(ScrollDelayMs, cancellationToken).ContinueWith(_ =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            LoadVisibleThumbnails();
                            _isLoading = false;
                        }));
                    }
                    else
                    {
                        _isLoading = false;
                    }
                }, cancellationToken);
            }
        }
        
        /// <summary>
        /// 加载可见区域的缩略图
        /// </summary>
        private void LoadVisibleThumbnails()
        {
            if (_listView == null || _scrollViewer == null)
                return;

            var cancellationToken = _cancellationTokenSource?.Token ?? CancellationToken.None;
            if (cancellationToken.IsCancellationRequested)
                return;
                
            try
            {
                var itemsSource = _listView.ItemsSource;
                if (itemsSource == null || cancellationToken.IsCancellationRequested)
                    return;
                    
                var containerGenerator = _listView.ItemContainerGenerator;
                int loadedCount = 0;
                int index = 0;
                
                foreach (var item in itemsSource)
                {
                    if (cancellationToken.IsCancellationRequested || loadedCount >= ScrollLoadBatchSize)
                        break;
                        
                    var container = containerGenerator.ContainerFromIndex(index) as ListViewItem;
                    if (container != null)
                    {
                        // 检查项目是否在可见区域内
                        var containerBounds = container.TransformToAncestor(_scrollViewer).TransformBounds(
                            new Rect(0, 0, container.RenderSize.Width, container.RenderSize.Height));
                        var scrollViewerBounds = new Rect(0, 0, _scrollViewer.ViewportWidth, _scrollViewer.ViewportHeight);
                        
                        // 如果项目在可见区域内或接近可见区域（减少预加载范围到400px）
                        if (containerBounds.IntersectsWith(scrollViewerBounds) || 
                            (containerBounds.Top < scrollViewerBounds.Bottom + 400 && containerBounds.Bottom > scrollViewerBounds.Top - 200))
                        {
                            var image = FindVisualChild<Image>(container);
                            if (image != null && image.Source is RenderTargetBitmap)
                            {
                                var pathProperty = item.GetType().GetProperty("Path");
                                if (pathProperty != null)
                                {
                                    var path = pathProperty.GetValue(item) as string;
                                    if (!string.IsNullOrEmpty(path) && !cancellationToken.IsCancellationRequested)
                                    {
                                        LoadThumbnailForItemAsync(path, image, (int)_thumbnailSize, cancellationToken);
                                        loadedCount++;
                                    }
                                }
                            }
                        }
                    }
                    index++;
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
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

            // 取消之前的加载任务
            CancelLoading();

            // 创建新的取消令牌
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            Task.Run(async () =>
            {
                try
                {
                    var itemsSource = _listView.ItemsSource;
                    if (itemsSource == null || cancellationToken.IsCancellationRequested)
                        return;

                    // 等待一段时间，确保UI先响应
                    await Task.Delay(InitialWaitMs, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    await Application.Current?.Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            // 只加载可见区域的缩略图
                            LoadVisibleThumbnailsOnly(cancellationToken);
                        }
                        catch { }
                    }, DispatcherPriority.Background);
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，忽略
                }
                catch { }
            }, cancellationToken);
        }

        /// <summary>
        /// 只加载可见区域的缩略图
        /// </summary>
        private void LoadVisibleThumbnailsOnly(CancellationToken cancellationToken)
        {
            if (_listView == null || _scrollViewer == null || cancellationToken.IsCancellationRequested)
                return;

            try
            {
                var itemsSource = _listView.ItemsSource;
                if (itemsSource == null || cancellationToken.IsCancellationRequested)
                    return;

                var containerGenerator = _listView.ItemContainerGenerator;
                int index = 0;
                int loadedCount = 0;

                foreach (var item in itemsSource)
                {
                    if (cancellationToken.IsCancellationRequested || loadedCount >= BatchSize)
                        break;

                    var container = containerGenerator.ContainerFromIndex(index) as ListViewItem;
                    if (container != null)
                    {
                        // 检查项目是否在可见区域内
                        var containerBounds = container.TransformToAncestor(_scrollViewer).TransformBounds(
                            new Rect(0, 0, container.RenderSize.Width, container.RenderSize.Height));
                        var scrollViewerBounds = new Rect(0, 0, _scrollViewer.ViewportWidth, _scrollViewer.ViewportHeight);

                        // 只加载可见区域和下方400px的内容（减少预加载范围）
                        if (containerBounds.IntersectsWith(scrollViewerBounds) ||
                            (containerBounds.Top < scrollViewerBounds.Bottom + 400 && containerBounds.Bottom > scrollViewerBounds.Top - 200))
                        {
                            var image = FindVisualChild<Image>(container);
                            if (image != null && image.Source is RenderTargetBitmap)
                            {
                                var pathProperty = item.GetType().GetProperty("Path");
                                if (pathProperty != null)
                                {
                                    var path = pathProperty.GetValue(item) as string;
                                    if (!string.IsNullOrEmpty(path) && !cancellationToken.IsCancellationRequested)
                                    {
                                        LoadThumbnailForItemAsync(path, image, (int)_thumbnailSize, cancellationToken);
                                        loadedCount++;
                                    }
                                }
                            }
                        }
                    }
                    index++;
                }

                // 如果还有更多可见项目，延迟加载下一批
                if (loadedCount >= BatchSize && !cancellationToken.IsCancellationRequested)
                {
                    Task.Delay(BatchDelayMs, cancellationToken).ContinueWith(_ =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                            {
                                LoadVisibleThumbnailsOnly(cancellationToken);
                            }));
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch { }
        }

        /// <summary>
        /// 取消所有正在进行的加载任务
        /// </summary>
        private void CancelLoading()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 为指定项异步加载缩略图（支持并发控制，避免过多并发任务）
        /// </summary>
        private void LoadThumbnailForItemAsync(string path, Image image, int targetSize, CancellationToken cancellationToken = default)
        {
            // 检查是否已经在加载中，避免重复加载
            if (_loadingTasks.ContainsKey(path) || cancellationToken.IsCancellationRequested)
                return;

            var loadTask = Task.Run(async () =>
            {
                // 使用信号量控制并发数
                await _concurrencySemaphore.WaitAsync(cancellationToken);
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    // 使用ThumbnailConverter的同步加载方法
                    var converter = new ThumbnailConverter();
                    var thumbnail = converter.LoadThumbnailSync(path, targetSize);

                    if (thumbnail != null && !cancellationToken.IsCancellationRequested)
                    {
                        await Application.Current?.Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                if (image != null && image.Source is RenderTargetBitmap && !cancellationToken.IsCancellationRequested)
                                {
                                    image.Source = thumbnail;
                                }
                            }
                            catch { }
                        }, DispatcherPriority.Background);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，忽略
                }
                catch { }
                finally
                {
                    _concurrencySemaphore.Release();
                    // 加载完成后移除任务记录
                    _loadingTasks.TryRemove(path, out _);
                }
            }, cancellationToken);

            // 记录加载任务
            _loadingTasks[path] = loadTask;
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
            // 取消所有正在进行的加载任务
            CancelLoading();
            
            ThumbnailConverter.ClearPriorityLoadPaths();
            // 清除加载任务记录
            _loadingTasks.Clear();
        }
    }
}


