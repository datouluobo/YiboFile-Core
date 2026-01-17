using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace YiboFile
{
    /// <summary>
    /// RightPanelControl.xaml 的交互逻辑
    /// </summary>
    public partial class RightPanelControl : UserControl
    {
        // PreviewGrid 和 NotesTextBox 由 XAML 自动生成

        public event TextChangedEventHandler NotesTextChanged;
        public event RoutedEventHandler NotesAutoSaved;
        public event RoutedEventHandler WindowMinimize;
        public event RoutedEventHandler WindowMaximize;
        public event RoutedEventHandler WindowClose;
        public event MouseButtonEventHandler TitleBarMouseDown;
        public event EventHandler<string> PreviewOpenFileRequested;  // 预览区打开文件请求
        public event MouseButtonEventHandler PreviewMiddleClickRequested;  // 中键打开文件请求
        public event EventHandler NavigatePrevImageRequested;  // 上一张图片请求
        public event EventHandler NavigateNextImageRequested;  // 下一张图片请求
        public event EventHandler<int> NavigateToImageIndexRequested;  // 跳转到指定图片索引请求

        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        private bool _hasPendingChanges = false;

        public RightPanelControl()
        {
            InitializeComponent();

            // 设置自动保存定时器
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500); // 停止输入0.5秒后自动保存，提升保存速度
            _autoSaveTimer.Tick += AutoSaveTimer_Tick;

            // 订阅文本变化事件
            if (NotesTextBox != null)
            {
                NotesTextBox.TextChanged += NotesTextBox_TextChanged;
            }

            // 订阅预览区中键事件（使用Border容器）
            if (ImagePreviewDisplay != null)
            {
                var parentBorder = ImagePreviewDisplay.Parent as FrameworkElement;
                if (parentBorder != null)
                {
                    parentBorder.MouseDown += PreviewArea_MouseDown;
                }
            }
        }

        private void PreviewArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 检测中键点击
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                PreviewMiddleClickRequested?.Invoke(sender, e);
                e.Handled = true;
            }
        }

        // 显示图片预览（使用TagTrain样式）
        private System.Threading.CancellationTokenSource _previewLoadCancellation;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapImage> _previewCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BitmapImage>();
        private const int MaxPreviewCacheSize = 10;
        private const int MaxPreviewWidth = 1920; // 限制预览图片最大宽度，减少内存占用

        public void DisplayImagePreview(string imagePath)
        {
            if (ImagePreviewDisplay == null || ImagePreviewBorder == null || DefaultPreviewText == null)
                return;

            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    ClearImagePreview();
                    if (DefaultPreviewText != null)
                    {
                        DefaultPreviewText.Visibility = Visibility.Visible;
                    }
                    return;
                }

                // 取消之前的加载任务
                if (_previewLoadCancellation != null)
                {
                    _previewLoadCancellation.Cancel();
                    _previewLoadCancellation.Dispose();
                }
                _previewLoadCancellation = new System.Threading.CancellationTokenSource();
                var cancellationToken = _previewLoadCancellation.Token;

                // 清理PreviewGrid中可能残留的其它预览元素，避免遮挡图片
                if (PreviewGrid != null)
                {
                    for (int i = PreviewGrid.Children.Count - 1; i >= 0; i--)
                    {
                        var child = PreviewGrid.Children[i];
                        // 保留DefaultPreviewText和ImagePreviewBorder，清除其他元素
                        if (!ReferenceEquals(child, DefaultPreviewText) && !ReferenceEquals(child, ImagePreviewBorder))
                        {
                            PreviewGrid.Children.RemoveAt(i);
                        }
                    }
                    // 确保图片预览层在最上方
                    Panel.SetZIndex(ImagePreviewBorder, 1);
                }

                // 先清除旧的图片源，确保能正确加载新图片
                if (ImagePreviewDisplay.Source != null)
                {
                    var oldBitmap = ImagePreviewDisplay.Source as BitmapImage;
                    if (oldBitmap != null)
                    {
                        ImagePreviewDisplay.Source = null;
                        oldBitmap = null;
                    }
                }

                // 显示加载提示
                DefaultPreviewText.Text = "加载中...";
                DefaultPreviewText.Visibility = Visibility.Visible;
                ImagePreviewBorder.Visibility = Visibility.Visible;

                // 确保使用绝对路径
                if (!Path.IsPathRooted(imagePath))
                {
                    imagePath = Path.GetFullPath(imagePath);
                }

                // 检查缓存
                var fileInfo = new FileInfo(imagePath);
                var cacheKey = $"{imagePath}_{fileInfo.LastWriteTime.Ticks}";

                if (_previewCache.TryGetValue(cacheKey, out var cachedBitmap))
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        ImagePreviewDisplay.Source = cachedBitmap;
                        DefaultPreviewText.Visibility = Visibility.Collapsed;
                        if (NoImagePreviewText != null)
                        {
                            NoImagePreviewText.Visibility = Visibility.Collapsed;
                        }
                    }
                    return;
                }

                // 异步加载图片
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        BitmapImage bitmap = null;

                        // 优先尝试使用UriSource（性能更好），如果失败则使用StreamSource
                        try
                        {
                            bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            // 限制预览图片大小，减少内存占用
                            bitmap.DecodePixelWidth = MaxPreviewWidth;
                            bitmap.EndInit();
                            bitmap.Freeze();
                        }
                        catch
                        {
                            // 如果UriSource失败（可能包含特殊字符），使用StreamSource
                            try
                            {
                                bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.StreamSource = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                // 限制预览图片大小，减少内存占用
                                bitmap.DecodePixelWidth = MaxPreviewWidth;
                                bitmap.EndInit();
                                bitmap.Freeze();
                            }
                            catch
                            {
                                bitmap = null;
                            }
                        }

                        if (bitmap != null && !cancellationToken.IsCancellationRequested)
                        {
                            // 添加到缓存
                            if (_previewCache.Count >= MaxPreviewCacheSize)
                            {
                                // 移除最早的缓存项
                                var firstKey = _previewCache.Keys.FirstOrDefault();
                                if (firstKey != null)
                                {
                                    _previewCache.TryRemove(firstKey, out _);
                                }
                            }
                            _previewCache[cacheKey] = bitmap;

                            // 在UI线程更新
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                if (!cancellationToken.IsCancellationRequested && ImagePreviewDisplay != null)
                                {
                                    ImagePreviewDisplay.Source = bitmap;
                                    DefaultPreviewText.Visibility = Visibility.Collapsed;
                                    if (NoImagePreviewText != null)
                                    {
                                        NoImagePreviewText.Visibility = Visibility.Collapsed;
                                    }
                                }
                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                        else if (!cancellationToken.IsCancellationRequested)
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                if (DefaultPreviewText != null)
                                {
                                    DefaultPreviewText.Text = "图片加载失败";
                                    DefaultPreviewText.Visibility = Visibility.Visible;
                                }
                                if (ImagePreviewBorder != null)
                                {
                                    ImagePreviewBorder.Visibility = Visibility.Collapsed;
                                }
                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                    catch (System.OperationCanceledException)
                    {
                        // 正常取消，忽略
                    }
                    catch
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                ClearImagePreview();
                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                }, cancellationToken);
            }
            catch (Exception)
            {
                ClearImagePreview();
            }
        }

        // 清除图片预览，恢复原有预览状态
        public void ClearImagePreview()
        {
            if (ImagePreviewBorder != null)
            {
                ImagePreviewBorder.Visibility = Visibility.Collapsed;
                Panel.SetZIndex(ImagePreviewBorder, 0);
            }

            if (ImagePreviewDisplay != null)
            {
                // 清除图片源，释放资源
                var oldBitmap = ImagePreviewDisplay.Source as BitmapImage;
                ImagePreviewDisplay.Source = null;
                oldBitmap = null;
            }

            if (NoImagePreviewText != null)
            {
                NoImagePreviewText.Visibility = Visibility.Collapsed;
            }

            // 注意：不清除DefaultPreviewText的Visibility，让调用者决定是否显示
        }

        // 处理预览区打开文件请求的公共方法
        public void RequestPreviewOpenFile(string filePath)
        {
            PreviewOpenFileRequested?.Invoke(this, filePath);
        }

        public void SetMaximizedVisual(bool isMax)
        {
            // 按钮已移到主窗口，此方法已废弃但保留以避免破坏接口
            // 实际的按钮更新在 MainWindow.UpdateWindowStateUI() 中处理
        }

        public void ForceSaveNotes()
        {
            if (_hasPendingChanges)
            {
                NotesAutoSaved?.Invoke(this, new RoutedEventArgs());
                _hasPendingChanges = false;
            }
        }

        private void AutoSaveTimer_Tick(object sender, EventArgs e)
        {
            _autoSaveTimer.Stop();
            if (_hasPendingChanges)
            {
                try
                {
                    NotesAutoSaved?.Invoke(this, new RoutedEventArgs());
                    _hasPendingChanges = false;
                }
                catch
                {
                    // 忽略保存错误，继续工作
                }
            }
        }

        private void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _hasPendingChanges = true;

            // 重新启动定时器
            if (_autoSaveTimer != null)
            {
                _autoSaveTimer.Stop();
                _autoSaveTimer.Start();
            }

            // 触发事件
            NotesTextChanged?.Invoke(sender, e);
        }

        private void WindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowMinimize?.Invoke(sender, e);
        }

        private void WindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowMaximize?.Invoke(sender, e);
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            WindowClose?.Invoke(sender, e);
        }

        private void TitleBarArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TitleBarMouseDown?.Invoke(sender, e);
        }

        // 显示/隐藏图片导航控件
        public void ShowImageNavigation(bool show)
        {
            if (PrevImageBtn != null) PrevImageBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (NextImageBtn != null) NextImageBtn.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (ImageIndexText != null) ImageIndexText.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        // 更新图片索引显示
        public void UpdateImageIndex(int currentIndex, int totalCount, string imagePath = null)
        {
            if (ImageIndexText == null) return;

            if (totalCount > 0 && currentIndex >= 0)
            {
                var displayText = imagePath != null
                    ? $"第 {currentIndex + 1}/{totalCount} 张: {Path.GetFileName(imagePath)}"
                    : $"第 {currentIndex + 1}/{totalCount} 张";
                ImageIndexText.Text = displayText;
            }
            else
            {
                ImageIndexText.Text = "";
            }
        }

        // 更新导航按钮状态
        public void UpdateNavigationButtons(bool hasPrev, bool hasNext)
        {
            if (PrevImageBtn != null) PrevImageBtn.IsEnabled = hasPrev;
            if (NextImageBtn != null) NextImageBtn.IsEnabled = hasNext;
        }

        // 导航按钮事件处理
        private void PrevImageBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigatePrevImageRequested?.Invoke(this, EventArgs.Empty);
        }

        private void NextImageBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigateNextImageRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ImageIndexText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 触发跳转事件，由MainWindow处理跳转逻辑（-1表示需要弹出对话框）
            NavigateToImageIndexRequested?.Invoke(this, -1);
        }


        public event EventHandler<double> NotesHeightChanged;

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.Content is Grid rootGrid && rootGrid.RowDefinitions.Count > 4)
            {
                var height = rootGrid.RowDefinitions[4].Height.Value;
                NotesHeightChanged?.Invoke(this, height);
            }
        }
    }
}


