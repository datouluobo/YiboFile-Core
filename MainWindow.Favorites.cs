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
            if (NavigationPanelControl == null) return;
            _favoriteService.LoadFavorites(NavigationPanelControl.FolderFavoritesListBoxControl, NavigationPanelControl.FileFavoritesListBoxControl);
        }

        private void AddFavorite_Click(object sender, RoutedEventArgs e)
        {
            // 根据当前焦点判断使用哪个列表
            var activeBrowser = (_isSecondPaneFocused && SecondFileBrowser != null) ? SecondFileBrowser : FileBrowser;

            // 获取选中的文件或文件夹
            var selectedItems = activeBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();

            // 使用 Service 添加收藏
            _favoriteService.AddFavorite(selectedItems);
        }

        #endregion
    }
}

