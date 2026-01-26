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
using YiboFile.Dialogs;

namespace YiboFile
{
    public partial class MainWindow
    {
        #region 收藏功能

        private void OnFavoriteListBoxLoaded(NavigationPanelControl sender, ListBox listBox)
        {
            // 当 DataTemplate 中的 ListBox 加载时，配置其事件（点击导航、右键菜单、拖拽等）
            _favoriteService.ConfigureListBoxEvents(listBox);
        }

        private void OnFavoriteListBoxPreviewMouseDown(NavigationPanelControl sender, ListBox listBox, MouseButtonEventArgs e)
        {
            // 处理鼠标中键等特殊点击
            // 注意：FavoriteService 中已有处理逻辑，这里主要作为桥接
        }

        private void ManageFavorites_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现分组管理窗口或弹窗
            DialogService.Info("分组管理功能即将推出！可通过文件夹右键菜单直接管理收藏。", owner: this);
        }

        private void AddFavorite_Click(object sender, RoutedEventArgs e)
        {
            // 根据当前焦点判断使用哪个列表
            var activeBrowser = (_isSecondPaneFocused && SecondFileBrowser != null) ? SecondFileBrowser : FileBrowser;

            // 获取选中的文件或文件夹
            var selectedItems = activeBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();

            // 使用 Module 添加到默认分组 (1)
            _viewModel?.Favorites?.AddFavorite(selectedItems, 1);
        }

        private void OnRenameFavoriteGroupRequested(object groupItem)
        {
            if (groupItem is FavoriteService.FavoriteGroupItem group)
            {
                var inputName = DialogService.ShowInput("请输入新的分组名称：", group.Name, "重命名分组", owner: this);
                if (inputName != null)
                {
                    var newName = inputName?.Trim();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        _viewModel?.Favorites?.RenameGroup(group.Id, newName);
                    }
                }
            }
        }

        private void OnDeleteFavoriteGroupRequested(object groupItem)
        {
            if (groupItem is FavoriteService.FavoriteGroupItem group)
            {
                if (group.Id == 1)
                {
                    DialogService.Info("默认分组不能删除", owner: this);
                    return;
                }

                if (DialogService.Ask($"确定要删除分组 \"{group.Name}\" 吗？\n其中的内容将被移动到默认分组中。", "确认删除", this))
                {
                    _viewModel?.Favorites?.DeleteGroup(group.Id);
                }
            }
        }

        #endregion
    }
}

