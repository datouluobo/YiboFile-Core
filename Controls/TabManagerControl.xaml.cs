using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YiboFile.Controls
{
    /// <summary>
    /// TabManagerControl.xaml 的交互逻辑
    /// 标签页管理控件的 UI 容器
    /// 业务逻辑已移至 TabService
    /// </summary>
    public partial class TabManagerControl : UserControl
    {
        public TabManagerControl()
        {
            InitializeComponent();
            TabScrollViewer.PreviewMouseWheel += TabScrollViewer_PreviewMouseWheel;
        }

        #region Dependency Properties

        /// <summary>
        /// 新建标签页命令
        /// </summary>
        public static readonly DependencyProperty NewTabCommandProperty =
            DependencyProperty.Register(nameof(NewTabCommand), typeof(ICommand), typeof(TabManagerControl));

        public ICommand NewTabCommand
        {
            get => (ICommand)GetValue(NewTabCommandProperty);
            set => SetValue(NewTabCommandProperty, value);
        }

        /// <summary>
        /// 更新标签页宽度命令
        /// </summary>
        public static readonly DependencyProperty UpdateTabWidthsCommandProperty =
            DependencyProperty.Register(nameof(UpdateTabWidthsCommand), typeof(ICommand), typeof(TabManagerControl));

        public ICommand UpdateTabWidthsCommand
        {
            get => (ICommand)GetValue(UpdateTabWidthsCommandProperty);
            set => SetValue(UpdateTabWidthsCommandProperty, value);
        }

        #endregion

        #region Event Handlers

        public event EventHandler CloseOverlayRequested;

        public void RaiseCloseOverlayRequested()
        {
            CloseOverlayRequested?.Invoke(this, EventArgs.Empty);
        }

        private void TabScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta != 0)
            {
                TabScrollViewer.ScrollToHorizontalOffset(TabScrollViewer.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private void TabsBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (UpdateTabWidthsCommand != null && UpdateTabWidthsCommand.CanExecute(e.NewSize.Width))
            {
                UpdateTabWidthsCommand.Execute(e.NewSize.Width);
            }
        }

        #endregion

        #region Obsolete/Compatibility (To be removed if possible)

        public StackPanel TabsPanelControl => null;
        public Border TabsBorderControl => TabsBorder;

        #endregion
    }
}
