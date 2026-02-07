using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.Services.Navigation;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 鼠标事件处理器（轻量级 UI Handler - 合理保留）
    /// 处理窗口级别的鼠标交互：标题栏拖拽、最大化、快速访问列表点击
    /// 设计说明：职责单一，无业务逻辑，导航委托给 NavigationCoordinator，符合 MVVM
    /// </summary>
    public class MouseEventHandler
    {
        private readonly Action _windowMaximizeClick;
        private readonly Action _windowDragMove;

        private readonly Func<ListBox> _getQuickAccessListBox;
        private readonly NavigationCoordinator _navigationCoordinator;
        private readonly Action<Favorite> _handleFavoriteNavigation;
        private readonly Action<string> _handleQuickAccessNavigation;

        public MouseEventHandler(
            Action windowMaximizeClick,
            Action windowDragMove,
            Func<ListBox> getQuickAccessListBox,
            NavigationCoordinator navigationCoordinator,
            Action<Favorite> handleFavoriteNavigation,
            Action<string> handleQuickAccessNavigation)
        {
            _windowMaximizeClick = windowMaximizeClick ?? throw new ArgumentNullException(nameof(windowMaximizeClick));
            _windowDragMove = windowDragMove ?? throw new ArgumentNullException(nameof(windowDragMove));
            _getQuickAccessListBox = getQuickAccessListBox ?? throw new ArgumentNullException(nameof(getQuickAccessListBox));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _handleFavoriteNavigation = handleFavoriteNavigation ?? throw new ArgumentNullException(nameof(handleFavoriteNavigation));
            _handleQuickAccessNavigation = handleQuickAccessNavigation ?? throw new ArgumentNullException(nameof(handleQuickAccessNavigation));
        }

        /// <summary>
        /// 标题栏鼠标事件：双击最大化/还原，单击拖动窗口
        /// 注：此为 WPF 标准窗口行为，不需要迁移到 ViewModel
        /// </summary>
        public void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    // 双击切换最大化/还原
                    _windowMaximizeClick();
                }
                else if (e.ClickCount == 1)
                {
                    // 单击拖拽窗口
                    _windowDragMove();
                }
            }
        }

        /// <summary>
        /// 快速访问列表鼠标事件：处理中键/右键导航
        /// 注：导航逻辑已委托给 NavigationCoordinator，符合 MVVM
        /// </summary>
        public void QuickAccessListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext is string path)
                {
                    e.Handled = true;
                    _handleQuickAccessNavigation(path);
                    return;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
        }
    }
}












