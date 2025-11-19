using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace OoiMRR
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
                
                // 隐藏默认预览文本
                DefaultPreviewText.Visibility = Visibility.Collapsed;
                
                // 显示图片预览边框
                ImagePreviewBorder.Visibility = Visibility.Visible;
                
                // 加载图片
                if (!File.Exists(imagePath))
                {
                    DefaultPreviewText.Text = $"图片文件不存在: {imagePath}";
                    DefaultPreviewText.Visibility = Visibility.Visible;
                    ImagePreviewBorder.Visibility = Visibility.Collapsed;
                    return;
                }
                
                // 确保使用绝对路径
                if (!Path.IsPathRooted(imagePath))
                {
                    imagePath = Path.GetFullPath(imagePath);
                }
                
                BitmapImage bitmap;
                
                // 优先尝试使用UriSource（性能更好），如果失败则使用StreamSource
                try
                {
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    // 不设置DecodePixelWidth，让图片保持原始尺寸，由Stretch属性控制显示
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
                catch
                {
                    // 如果UriSource失败（可能包含特殊字符），使用StreamSource
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    // 不设置DecodePixelWidth，让图片保持原始尺寸，由Stretch属性控制显示
                    bitmap.EndInit();
                    bitmap.Freeze();
                }
                
                ImagePreviewDisplay.Source = bitmap;
                if (NoImagePreviewText != null)
                {
                    NoImagePreviewText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载图片预览失败: {ex.Message}");
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
    }
}

