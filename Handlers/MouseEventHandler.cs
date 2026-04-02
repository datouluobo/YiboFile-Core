using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.Services.Navigation;
using YiboFile.Models.Navigation;
using System.Windows.Media;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 鼠标事件处理器（轻量级 UI Handler - 合理保留）
    /// 处理窗口级别的鼠标交互：标题栏拖拽、最大化、侧边栏列表点击
    /// 设计说明：职责单一，无业务逻辑，导航委托给 NavigationCoordinator，符合 MVVM
    /// </summary>
    public class MouseEventHandler
    {
        private readonly Action _windowMaximizeClick;
        private readonly Action _windowDragMove;

        private readonly Func<ListBox> _getQuickAccessListBox;
        private readonly INavigationCoordinator _navigationCoordinator;
        private readonly Action<Favorite> _handleFavoriteNavigation;
        private readonly Action<string> _handleQuickAccessNavigation;
        private readonly Func<YiboFile.Services.Navigation.PaneId> _getActivePaneId;

        public MouseEventHandler(
            Action windowMaximizeClick,
            Action windowDragMove,
            Func<ListBox> getQuickAccessListBox,
            INavigationCoordinator navigationCoordinator,
            Action<Favorite> handleFavoriteNavigation,
            Action<string> handleQuickAccessNavigation,
            Func<YiboFile.Services.Navigation.PaneId> getActivePaneId)
        {
            _windowMaximizeClick = windowMaximizeClick ?? throw new ArgumentNullException(nameof(windowMaximizeClick));
            _windowDragMove = windowDragMove ?? throw new ArgumentNullException(nameof(windowDragMove));
            _getQuickAccessListBox = getQuickAccessListBox ?? throw new ArgumentNullException(nameof(getQuickAccessListBox));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _handleFavoriteNavigation = handleFavoriteNavigation ?? throw new ArgumentNullException(nameof(handleFavoriteNavigation));
            _handleQuickAccessNavigation = handleQuickAccessNavigation ?? throw new ArgumentNullException(nameof(handleQuickAccessNavigation));
            _getActivePaneId = getActivePaneId ?? throw new ArgumentNullException(nameof(getActivePaneId));
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
            if (clickType == YiboFile.Models.Navigation.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var path = ExtractPathFromListBoxItem(listBox, e.GetPosition(listBox));
            if (!string.IsNullOrEmpty(path))
            {
                e.Handled = true;
                _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.QuickAccess, clickType, pane: _getActivePaneId());
            }
        }

        /// <summary>
        /// 库列表鼠标点击处理
        /// </summary>
        public void LibrariesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == YiboFile.Models.Navigation.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext is YiboFile.Library library)
                {
                    e.Handled = true;
                    _navigationCoordinator.HandleLibraryNavigation(library, clickType, _getActivePaneId());
                    return;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
        }

        /// <summary>
        /// 收藏列表点击处理
        /// </summary>
        public void HandleFavoriteListBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == YiboFile.Models.Navigation.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var favorite = ExtractFavoriteFromListBoxItem(listBox, e.GetPosition(listBox));
            if (favorite != null)
            {
                e.Handled = true;
                _navigationCoordinator.HandleFavoriteNavigation(favorite, clickType, _getActivePaneId());
            }
        }

        public void HandleGlobalMouseDown(object sender, MouseButtonEventArgs e, Controls.FileBrowserControl secondFileBrowser)
        {
            // Apply the same global mouse down logic for the Secondary File Browser
            // If the Secondary Address Bar is in edit mode and the click is outside it, close edit mode.
            if (secondFileBrowser != null && secondFileBrowser.AddressBarControl != null &&
                secondFileBrowser.AddressBarControl.IsEditMode)
            {
                var source = e.OriginalSource as DependencyObject;
                bool isAddressBar = false;

                // Check if the click target is within the AddressBarControl
                var current = source;
                while (current != null)
                {
                    if (current == secondFileBrowser.AddressBarControl)
                    {
                        isAddressBar = true;
                        break;
                    }
                    if (current is System.Windows.Media.Media3D.Visual3D)
                    {
                        current = VisualTreeHelper.GetParent(current);
                    }
                    else if (current is FrameworkContentElement fce)
                    {
                        current = fce.Parent;
                    }
                    else
                    {
                        current = VisualTreeHelper.GetParent(current);
                    }
                }

                if (!isAddressBar)
                {
                    // If clicked outside, exit edit mode
                    secondFileBrowser.AddressBarControl.SwitchToBreadcrumbMode();
                }
            }
        }

        private string ExtractPathFromListBoxItem(ListBox listBox, System.Windows.Point position)
        {
            var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(listBox, position);
            if (hitResult == null) return null;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext != null)
                {
                    var pathProperty = item.DataContext.GetType().GetProperty("Path");
                    if (pathProperty != null)
                    {
                        return pathProperty.GetValue(item.DataContext) as string;
                    }
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private YiboFile.Favorite ExtractFavoriteFromListBoxItem(ListBox listBox, System.Windows.Point position)
        {
            var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(listBox, position);
            if (hitResult == null) return null;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext != null)
                {
                    var favoriteProperty = item.DataContext.GetType().GetProperty("Favorite");
                    if (favoriteProperty != null)
                    {
                        return favoriteProperty.GetValue(item.DataContext) as YiboFile.Favorite;
                    }
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}












