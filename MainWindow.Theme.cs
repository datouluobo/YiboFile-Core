using System;
using System.Windows;

namespace YiboFile
{
    public partial class MainWindow
    {
        /// <summary>
        /// 初始化主题切换事件
        /// </summary>
        private void InitializeThemeEvents()
        {
            // 订阅主题切换事件,刷新导航面板图标
            Services.Theming.ThemeManager.ThemeChanged += (s, e) =>
            {
                RefreshNavigationIcons();

                // 修复：切换主题时，如果有副列表，强制刷新布局以防止地址栏错位
                if (IsDualListMode && SecondFileBrowserContainer != null)
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        SecondFileBrowserContainer.InvalidateVisual();
                        SecondFileBrowserContainer.UpdateLayout();
                    }), System.Windows.Threading.DispatcherPriority.Render);
                }
            };
        }

        /// <summary>
        /// 刷新导航面板的图标(用于主题切换)
        /// </summary>
        private void RefreshNavigationIcons()
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 重新加载快速访问、驱动器和收藏列表以刷新图标
                    if (QuickAccessListBox != null)
                        _quickAccessService?.LoadQuickAccess(QuickAccessListBox);
                    if (DrivesTreeView != null)
                        _quickAccessService?.LoadDriveTree(DrivesTreeView, _fileListService.FormatFileSize);
                    ViewModel?.Favorites?.LoadFavorites();
                }
                catch (Exception)
                { }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}

