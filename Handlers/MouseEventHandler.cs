using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.Services.Navigation;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 鼠标事件处理器
    /// 处理非文件列表的鼠标事件，包括标题栏、收藏列表等
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





        public void QuickAccessListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == NavigationCoordinator.ClickType.LeftClick) return; // 左键由SelectionChanged处理

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












