using System;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile
{
    /// <summary>
    /// MainWindow 快速访问功能
    /// </summary>
    public partial class MainWindow
    {
        #region 快速访问

        internal void LoadQuickAccess()
        {
            if (QuickAccessListBox == null) return;
            _quickAccessService.LoadQuickAccess(QuickAccessListBox);
        }

        private void QuickAccessListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInternalUpdate) return;

            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                // Try to get Path property from the bound item
                var item = listBox.SelectedItem;
                var pathProperty = item.GetType().GetProperty("Path");
                if (pathProperty != null)
                {
                    var path = pathProperty.GetValue(item) as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Navigate
                        _navigationCoordinator.HandlePathNavigation(path, YiboFile.Services.Navigation.NavigationSource.QuickAccess, YiboFile.Services.Navigation.ClickType.LeftClick);
                    }
                }
            }

            // 清除其他导航区域的选择（无论当前选择是否为null）
            ClearOtherNavigationSelections("QuickAccess");
        }

        #endregion
    }
}

