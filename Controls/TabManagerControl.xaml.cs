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
        /// 附加属性：用于监听标签激活状态并触发 BringIntoView
        /// </summary>
        public static readonly DependencyProperty IsActiveNotifierProperty =
            DependencyProperty.RegisterAttached("IsActiveNotifier", typeof(bool), typeof(TabManagerControl),
                new PropertyMetadata(false, OnIsActiveNotifierChanged));

        public static bool GetIsActiveNotifier(DependencyObject obj) => (bool)obj.GetValue(IsActiveNotifierProperty);
        public static void SetIsActiveNotifier(DependencyObject obj, bool value) => obj.SetValue(IsActiveNotifierProperty, value);

        private static void OnIsActiveNotifierChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && (bool)e.NewValue)
            {
                // 延迟到 Loaded 优先级，确保布局完成后再滚动
                element.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Loaded,
                    new Action(() => element.BringIntoView()));
            }
        }

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

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(System.Collections.IEnumerable), typeof(TabManagerControl));

        public System.Collections.IEnumerable ItemsSource
        {
            get => (System.Collections.IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
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
