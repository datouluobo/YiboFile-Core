using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Services.Favorite;
using YiboFile.Services.Core;
using YiboFile.Models;
using YiboFile.Services.Navigation;
using YiboFile.Services.FileList;
using YiboFile.Models.UI;
using YiboFile.Services.Config;
using YiboFile.Controls;

namespace YiboFile
{
    public partial class MainWindow
    {
        #region 收藏功能

        internal void LoadFavorites()
        {
            if (FavoritesListBox == null) return;
            _favoriteService.LoadFavorites(FavoritesListBox);
        }

        private void FavoritesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FavoritesListBox.SelectedItem == null) return;

            // 清除其他导航区域的选择
            ClearOtherNavigationSelections("Favorites");


            if (_suppressFavoriteSelectionNavigation) return; // 右键上下文菜单打开时不导航

            // 使用反射获取Favorite对象
            var selectedItem = FavoritesListBox.SelectedItem;
            var favoriteProperty = selectedItem.GetType().GetProperty("Favorite");
            if (favoriteProperty == null) return;

            var favorite = favoriteProperty.GetValue(selectedItem) as Favorite;
            if (favorite == null) return;

            _navigationService.LastLeftNavSource = "Favorites";
            _navigationCoordinator.HandleFavoriteNavigation(favorite, NavigationCoordinator.ClickType.LeftClick);

            // 清除选择，避免残留选中状态
            FavoritesListBox.SelectedItem = null;
        }

        private bool _suppressFavoriteSelectionNavigation = false;

        private void FavoritesListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = true;
            var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (item != null)
            {
                item.IsSelected = true;
            }
        }

        private void FavoritesListBox_ContextMenuClosed(object sender, RoutedEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = false;
            // 关闭菜单后清除选择，避免残留状态
            if (FavoritesListBox != null)
                FavoritesListBox.SelectedItem = null;
        }

        private void FavoritesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 已改为单击进入，此方法保留但不再使用
        }



        private void FavoritesContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = false;
            if (FavoritesListBox != null)
                FavoritesListBox.SelectedItem = null;
        }



        private void AddFavorite_Click(object sender, RoutedEventArgs e)
        {
            // 根据当前焦点判断使用哪个列表
            var activeBrowser = (_isSecondPaneFocused && SecondFileBrowser != null) ? SecondFileBrowser : FileBrowser;

            // 获取选中的文件或文件夹
            var selectedItems = activeBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要收藏的文件或文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int successCount = 0;
            int skipCount = 0;

            foreach (var item in selectedItems)
            {
                try
                {
                    // 检查是否已收藏
                    if (DatabaseManager.IsFavorite(item.Path))
                    {
                        skipCount++;
                        continue;
                    }

                    string displayName = item.Name;
                    DatabaseManager.AddFavorite(item.Path, item.IsDirectory, displayName);
                    successCount++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"收藏失败: {item.Name} - {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // 刷新收藏列表
            LoadFavorites();

            // 显示通知
            if (successCount > 0)
                NotificationService.Show($"成功添加 {successCount} 个项目到收藏", NotificationType.Success);
            else if (skipCount > 0)
                NotificationService.Show($"{skipCount} 个项目已存在于收藏中", NotificationType.Info);
        }

        #endregion
    }
}

