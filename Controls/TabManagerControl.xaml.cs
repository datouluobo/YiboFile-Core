using System;
using System.Windows;
using System.Windows.Controls;
using OoiMRR;

namespace OoiMRR.Controls
{
    /// <summary>
    /// TabManagerControl.xaml 的交互逻辑
    /// 标签页管理控件的 UI 容器
    /// 业务逻辑已移至 TabService
    /// </summary>
    public partial class TabManagerControl : UserControl
    {
        private Window _parentWindow;

        /// <summary>
        /// 文件拖放事件
        /// </summary>
        public event Action<string[], string, bool> FileDropped;

        public TabManagerControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置父窗口（用于对话框等）
        /// </summary>
        public void SetParentWindow(Window window)
        {
            _parentWindow = window;
        }

        /// <summary>
        /// 标签页面板（XAML引用）
        /// </summary>
        public StackPanel TabsPanelControl => TabsPanel;

        /// <summary>
        /// 标签页边框容器（XAML引用）
        /// </summary>
        public Border TabsBorderControl => TabsBorder;
    }
}
