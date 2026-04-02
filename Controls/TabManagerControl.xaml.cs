using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YiboFile.Controls
{
    /// <summary>
    /// TabManagerControl.xaml 的交互逻辑
    /// 标签页管理控件的 UI 容器，支持溢出导航箭头和渐变遮罩
    /// 业务逻辑已移至 TabService
    /// </summary>
    public partial class TabManagerControl : UserControl
    {
        /// <summary>
        /// 是否显示溢出导航箭头（可由外部配置绑定）
        /// </summary>
        private bool _showOverflowArrows = true;
        
        /// <summary>
        /// 是否显示溢出渐变遮罩（可由外部配置绑定）
        /// </summary>
        private bool _showOverflowGradient = true;

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

        /// <summary>
        /// 绑定的标签服务实例
        /// </summary>
        public Services.Tabs.TabService Service { get; set; }

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(System.Collections.IEnumerable), typeof(TabManagerControl));

        public System.Collections.IEnumerable ItemsSource
        {
            get => (System.Collections.IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        #endregion

        #region 溢出配置

        /// <summary>
        /// 更新溢出 UI 配置（由 TabService 在配置变更时调用）
        /// </summary>
        public void UpdateOverflowSettings(bool showArrows, bool showGradient)
        {
            _showOverflowArrows = showArrows;
            _showOverflowGradient = showGradient;
            UpdateOverflowUI();
        }

        #endregion

        #region Event Handlers

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
            // 尺寸变化后更新溢出 UI
            UpdateOverflowUI();
        }

        private void TabScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateOverflowUI();
        }

        private void ScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            // 向左滚动一个标签的距离（约 120px）
            double step = 120;
            TabScrollViewer.ScrollToHorizontalOffset(
                Math.Max(0, TabScrollViewer.HorizontalOffset - step));
        }

        private void ScrollRight_Click(object sender, RoutedEventArgs e)
        {
            // 向右滚动一个标签的距离
            double step = 120;
            TabScrollViewer.ScrollToHorizontalOffset(
                Math.Min(TabScrollViewer.ScrollableWidth, TabScrollViewer.HorizontalOffset + step));
        }

        #endregion

        #region Context Menu Handlers

        private void TabContextMenu_Opening(object sender, ContextMenuEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.ContextMenu != null && fe.DataContext is Services.Tabs.PathTab tab)
            {
                var loc = App.ServiceProvider.GetService(typeof(Services.Localization.ILocalizationService)) as Services.Localization.ILocalizationService;
                foreach (var item in fe.ContextMenu.Items)
                {
                    if (item is MenuItem menuItem && menuItem.Name == "PinMenuItem")
                    {
                        menuItem.Header = tab.IsPinned 
                            ? loc?.Get("TabContent.Context.Unpin") 
                            : loc?.Get("TabContent.Context.Pin");
                        break;
                    }
                }
            }
        }

        private void DuplicateTab_Click(object sender, RoutedEventArgs e)
        {
            if (GetTab(sender) is var tab && tab != null) Service?.CreateDuplicateTab(tab);
        }

        private void TogglePinTab_Click(object sender, RoutedEventArgs e)
        {
            if (GetTab(sender) is var tab && tab != null) Service?.TogglePinTab(tab);
        }

        private void RenameTab_Click(object sender, RoutedEventArgs e)
        {
            if (GetTab(sender) is var tab && tab != null) Service?.RenameDisplayTitle(tab);
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (GetTab(sender) is var tab && !string.IsNullOrEmpty(tab.Path))
            {
                YiboFile.Helpers.ClipboardHelper.SetTextAsync(tab.Path);
            }
        }

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (GetTab(sender) is var tab && tab != null) Service?.OpenTabInExplorer(tab);
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (GetTab(sender) is var tab && tab != null) Service?.CloseTab(tab);
        }

        private void CloseOtherTabs_Click(object sender, RoutedEventArgs e)
        {
            if (GetTab(sender) is var tab && tab != null) Service?.CloseOtherTabs(tab);
        }

        private void CloseLeftTabs_Click(object sender, RoutedEventArgs e)
        {
            if (GetTab(sender) is var tab && tab != null) Service?.CloseTabsToLeft(tab);
        }

        private void CloseRightTabs_Click(object sender, RoutedEventArgs e)
        {
            if (GetTab(sender) is var tab && tab != null) Service?.CloseTabsToRight(tab);
        }

        private Services.Tabs.PathTab GetTab(object sender)
        {
            if (sender is MenuItem mi && mi.Parent is ContextMenu cm)
            {
                return (cm.PlacementTarget as FrameworkElement)?.DataContext as Services.Tabs.PathTab;
            }
            return (sender as FrameworkElement)?.DataContext as Services.Tabs.PathTab;
        }

        #endregion

        #region 溢出 UI 状态管理

        /// <summary>
        /// 根据 ScrollViewer 的滚动状态更新箭头和渐变遮罩的可见性
        /// </summary>
        private void UpdateOverflowUI()
        {
            if (TabScrollViewer == null) return;

            bool hasOverflow = TabScrollViewer.ScrollableWidth > 0;
            bool canScrollLeft = TabScrollViewer.HorizontalOffset > 1; // > 1 避免浮点精度
            bool canScrollRight = TabScrollViewer.HorizontalOffset < TabScrollViewer.ScrollableWidth - 1;

            // 导航箭头
            if (_showOverflowArrows && hasOverflow)
            {
                ScrollLeftButton.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
                ScrollRightButton.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                ScrollLeftButton.Visibility = Visibility.Collapsed;
                ScrollRightButton.Visibility = Visibility.Collapsed;
            }

            // 渐变遮罩
            if (_showOverflowGradient && hasOverflow)
            {
                GradientLeft.Visibility = canScrollLeft ? Visibility.Visible : Visibility.Collapsed;
                GradientRight.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                GradientLeft.Visibility = Visibility.Collapsed;
                GradientRight.Visibility = Visibility.Collapsed;
            }
        }

        #endregion


    }
}
