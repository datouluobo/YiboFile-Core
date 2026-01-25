using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YiboFile.Models;
using YiboFile.Services.Features;
using YiboFile.Services.Favorite;

namespace YiboFile.Services.UI
{
    /// <summary>
    /// 负责构建和更新文件右键菜单的动态内容
    /// </summary>
    public static class ContextMenuBuilder
    {
        public static void UpdateFavoritesSubMenu(MenuItem favoriteMenuItem, List<FileSystemItem> selectedItems, Action refreshCallback)
        {
            if (favoriteMenuItem == null) return;

            bool hasSelection = selectedItems != null && selectedItems.Count > 0;
            favoriteMenuItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;

            if (!hasSelection) return;

            favoriteMenuItem.Items.Clear();

            try
            {
                var favoriteService = App.ServiceProvider?.GetService(typeof(FavoriteService)) as FavoriteService;
                var groups = favoriteService?.GetAllGroups();

                if (groups != null && groups.Count > 0)
                {
                    foreach (var group in groups)
                    {
                        var groupMenuItem = new MenuItem
                        {
                            Header = group.Name,
                            Tag = group.Id
                        };
                        groupMenuItem.Click += (sender, args) =>
                        {
                            var mi = (MenuItem)sender;
                            var groupId = (int)mi.Tag;
                            favoriteService?.AddFavorite(selectedItems, groupId);
                        };
                        favoriteMenuItem.Items.Add(groupMenuItem);
                    }

                    favoriteMenuItem.Items.Add(new Separator());
                }

                var createGroupItem = new MenuItem { Header = "+ 新建分组..." };
                createGroupItem.Click += (sender, args) =>
                {
                    var inputName = YiboFile.DialogService.ShowInput("请输入新分组名称：", "新分组", "新建分组", owner: Window.GetWindow(favoriteMenuItem));
                    if (inputName != null)
                    {
                        var name = inputName.Trim();
                        if (!string.IsNullOrEmpty(name))
                        {
                            int newGroupId = favoriteService != null ? favoriteService.CreateGroup(name) : -1;
                            if (newGroupId != -1)
                            {
                                favoriteService?.AddFavorite(selectedItems, newGroupId);
                            }
                        }
                    }
                };
                favoriteMenuItem.Items.Add(createGroupItem);
            }
            catch
            {
                favoriteMenuItem.Items.Add(new MenuItem { Header = "加载失败", IsEnabled = false });
            }
        }

        public static void UpdateLibrarySubMenu(MenuItem libraryMenuItem, List<FileSystemItem> selectedItems, Action refreshCallback)
        {
            if (libraryMenuItem == null) return;

            // 仅对文件夹显示
            bool hasFolderSelection = selectedItems != null && selectedItems.Any(item => item.IsDirectory);
            libraryMenuItem.Visibility = hasFolderSelection ? Visibility.Visible : Visibility.Collapsed;

            if (!hasFolderSelection) return;

            libraryMenuItem.Items.Clear();

            try
            {
                var libService = App.ServiceProvider?.GetService(typeof(LibraryService)) as LibraryService;
                var allLibraries = libService?.LoadLibraries();
                var selectedFolders = selectedItems.Where(item => item.IsDirectory).ToList();

                if (allLibraries != null && allLibraries.Count > 0)
                {
                    foreach (var library in allLibraries)
                    {
                        var item = new MenuItem
                        {
                            IsCheckable = true,
                            StaysOpenOnClick = true,
                            Tag = library.Id,
                            Header = library.Name
                        };

                        // 判断是否勾选
                        bool allInLibrary = selectedFolders.All(folder =>
                            library.Paths != null && library.Paths.Contains(folder.Path));
                        item.IsChecked = allInLibrary;

                        item.Click += (sender, args) =>
                        {
                            var mi = (MenuItem)sender;
                            var libId = (int)mi.Tag;
                            bool shouldAdd = mi.IsChecked;

                            if (libService != null)
                            {
                                foreach (var folder in selectedFolders)
                                {
                                    if (!string.IsNullOrEmpty(folder.Path))
                                    {
                                        if (shouldAdd)
                                            libService.AddLibraryPath(libId, folder.Path);
                                        else
                                            libService.RemoveLibraryPath(libId, folder.Path);
                                    }
                                }
                            }
                        };
                        libraryMenuItem.Items.Add(item);
                    }
                }

                if (allLibraries != null && allLibraries.Count > 0)
                {
                    libraryMenuItem.Items.Add(new Separator());
                }

                var newLibraryItem = new MenuItem { Header = "新建库..." };
                newLibraryItem.Click += (sender, args) =>
                {
                    var dialog = new YiboFile.Controls.Dialogs.InputDialog("新建库", "请输入库名称:") { Owner = Window.GetWindow(libraryMenuItem) };
                    if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                    {
                        if (libService != null)
                        {
                            int newLibId = libService.AddLibrary(dialog.InputText);
                            if (newLibId > 0 || newLibId < 0)
                            {
                                int targetId = Math.Abs(newLibId);
                                foreach (var folder in selectedFolders)
                                {
                                    if (!string.IsNullOrEmpty(folder.Path))
                                        libService.AddLibraryPath(targetId, folder.Path);
                                }
                                libService.LoadLibraries();
                            }
                        }
                    }
                };
                libraryMenuItem.Items.Add(newLibraryItem);
            }
            catch
            {
                libraryMenuItem.Items.Add(new MenuItem { Header = "加载失败", IsEnabled = false });
            }
        }

        public static void UpdateTagSubMenu(MenuItem tagMenuItem, List<FileSystemItem> selectedItems, Action refreshCallback)
        {
            if (tagMenuItem == null) return;
            if (!App.IsTagTrainAvailable)
            {
                tagMenuItem.Visibility = Visibility.Collapsed;
                return;
            }

            bool hasSelection = selectedItems != null && selectedItems.Count > 0;
            tagMenuItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;

            if (!hasSelection) return;

            tagMenuItem.Items.Clear();

            try
            {
                var tagService = App.ServiceProvider?.GetService(typeof(ITagService)) as ITagService;
                var allTags = tagService?.GetAllTags()?.ToList();

                if (allTags != null && allTags.Count > 0)
                {
                    foreach (var tag in allTags)
                    {
                        var tagItem = new MenuItem
                        {
                            IsCheckable = true,
                            StaysOpenOnClick = true,
                            Tag = tag.Id
                        };

                        // Check status
                        bool allHaveTag = selectedItems.All(item => item.TagList != null && item.TagList.Any(t => t.Id == tag.Id));
                        tagItem.IsChecked = allHaveTag;

                        // Header with color
                        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                        var colorRect = new System.Windows.Shapes.Rectangle
                        {
                            Width = 12,
                            Height = 12,
                            RadiusX = 2,
                            RadiusY = 2,
                            Margin = new Thickness(0, 0, 8, 0),
                            Fill = new SolidColorBrush(ParseColor(tag.Color ?? "#808080"))
                        };
                        headerPanel.Children.Add(colorRect);
                        headerPanel.Children.Add(new TextBlock { Text = tag.Name, VerticalAlignment = VerticalAlignment.Center });
                        tagItem.Header = headerPanel;

                        tagItem.Click += (sender, args) =>
                        {
                            var mi = (MenuItem)sender;
                            var tagId = (int)mi.Tag;
                            bool shouldAdd = mi.IsChecked;

                            if (tagService != null)
                            {
                                foreach (var item in selectedItems)
                                {
                                    if (!string.IsNullOrEmpty(item.Path))
                                    {
                                        if (shouldAdd)
                                            tagService.AddTagToFile(item.Path, tagId);
                                        else
                                            tagService.RemoveTagFromFile(item.Path, tagId);
                                    }
                                }
                                refreshCallback?.Invoke();
                            }
                        };
                        tagMenuItem.Items.Add(tagItem);
                    }
                }
                else
                {
                    tagMenuItem.Items.Add(new MenuItem { Header = "暂无标签", IsEnabled = false });
                }
            }
            catch
            {
                tagMenuItem.Items.Add(new MenuItem { Header = "加载失败", IsEnabled = false });
            }
        }

        private static Color ParseColor(string colorString)
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(colorString);
            }
            catch
            {
                return Colors.Gray;
            }
        }
    }
}
