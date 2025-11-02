using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
            
            // 订阅预览区中键事件
            if (PreviewGrid != null)
            {
                PreviewGrid.MouseDown += PreviewGrid_MouseDown;
            }
        }
        
        private void PreviewGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 检测中键点击
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                PreviewMiddleClickRequested?.Invoke(sender, e);
                e.Handled = true;
            }
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

