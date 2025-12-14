using System.Windows;
using System.Windows.Controls;

namespace OoiMRR.Controls
{
    /// <summary>
    /// 窗口控制按钮控件
    /// 包含设置、关于、最小化、最大化、关闭按钮
    /// </summary>
    public partial class WindowControlButtonsControl : UserControl
    {
        // 事件定义
        public event RoutedEventHandler SettingsClick;
        public event RoutedEventHandler AboutClick;
        public event RoutedEventHandler MinimizeClick;
        public event RoutedEventHandler MaxRestoreClick;
        public event RoutedEventHandler CloseClick;

        public WindowControlButtonsControl()
        {
            InitializeComponent();
            InitializeEvents();
        }

        private void InitializeEvents()
        {
            if (SettingsButton != null)
            {
                SettingsButton.Click += (s, e) => SettingsClick?.Invoke(s, e);
            }

            if (AboutButton != null)
            {
                AboutButton.Click += (s, e) => AboutClick?.Invoke(s, e);
            }

            if (MinimizeButton != null)
            {
                MinimizeButton.Click += (s, e) => MinimizeClick?.Invoke(s, e);
            }

            if (MaxRestoreButton != null)
            {
                MaxRestoreButton.Click += (s, e) => MaxRestoreClick?.Invoke(s, e);
            }

            if (CloseButton != null)
            {
                CloseButton.Click += (s, e) => CloseClick?.Invoke(s, e);
            }
        }

        /// <summary>
        /// 更新最大化/还原按钮的图标和提示
        /// </summary>
        public void UpdateMaxRestoreButton(bool isMaximized)
        {
            if (MaxRestoreButton != null)
            {
                // Segoe MDL2 Assets: Maximize E922, Restore E923
                MaxRestoreButton.Content = isMaximized ? "\uE923" : "\uE922";
                MaxRestoreButton.ToolTip = isMaximized ? "还原" : "最大化";
            }
        }
    }
}


























