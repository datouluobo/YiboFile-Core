using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Services.Core;
using YiboFile.Controls;
using YiboFile;
using YiboFile.Services.Data.Repositories;
using YiboFile.Helpers;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Services.Favorite
{
    /// <summary>
    /// 收藏管理服务
    /// 负责收藏项的加载、添加、删除和拖拽排序
    /// </summary>
    public class FavoriteService
    {


        #region 私有字段
        private readonly IFavoriteRepository _favoriteRepository;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;
        private readonly IMessageBus _messageBus;
        private readonly Services.Navigation.INavigationCoordinator _navigationCoordinator;
        private YiboFile.Favorite _draggedFavorite = null;
        private System.Windows.Point _dragStartPoint;
        private bool _isDraggingFavorite = false;
        private bool _suppressFavoriteSelectionNavigation = false;
        private ListBoxItem _lastDragOverItem;
        private bool _lastInsertBefore;
        private Thickness? _originalPadding;
        private Thickness? _originalBorderThickness;

        #endregion

        #region 构造函数

        public FavoriteService(
            IFavoriteRepository favoriteRepository, 
            IMessageBus messageBus = null, 
            System.Windows.Threading.Dispatcher dispatcher = null,
            Services.Navigation.INavigationCoordinator navigationCoordinator = null)
        {
            _favoriteRepository = favoriteRepository ?? throw new ArgumentNullException(nameof(favoriteRepository));
            _messageBus = messageBus ?? App.ServiceProvider?.GetService<IMessageBus>();
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
            _navigationCoordinator = navigationCoordinator ?? App.ServiceProvider?.GetService<Services.Navigation.INavigationCoordinator>();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 收藏项显示数据
        /// </summary>
        public class FavoriteItem
        {
            public YiboFile.Favorite Favorite { get; set; }
            public string IconKey { get; set; }
            public string DisplayName { get; set; }
            public string Path { get; set; }
        }

        /// <summary>
        /// 收藏分组显示项
        /// </summary>
        public class FavoriteGroupItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public List<FavoriteItem> Items { get; set; }
        }

        /// <summary>
        /// 加载收藏列表 (按分组加载)
        /// </summary>
        /// <summary>
        /// 获取所有收藏分组数据 (MVVM)
        /// </summary>
        public List<FavoriteGroupItem> GetFavoriteGroups()
        {
            try
            {
                var locService = App.ServiceProvider?.GetService<YiboFile.Services.Localization.ILocalizationService>();

                var allFavorites = _favoriteRepository.GetAllFavorites();
                var groups = _favoriteRepository.GetAllGroups();

                // 分组同名项
                var nameGroups = allFavorites.GroupBy(f =>
                {
                    string name = f.DisplayName ?? Path.GetFileName(f.Path);
                    if (string.IsNullOrEmpty(name)) name = f.Path;
                    return name;
                }).ToList();

                var displayGroups = groups.Select(group =>
                {
                    var groupFavorites = allFavorites.Where(f => f.GroupId == group.Id).OrderBy(f => f.SortOrder).ToList();

                    var items = groupFavorites.Select(favorite =>
                    {
                        string iconKey = favorite.IsDirectory ? "Icon_Folder" : "Icon_Document";
                        string displayName = favorite.DisplayName ?? Path.GetFileName(favorite.Path);
                        if (string.IsNullOrEmpty(displayName))
                            displayName = favorite.Path;

                        // 同名项区分逻辑
                        var sameNameGroup = nameGroups.FirstOrDefault(g => (favorite.DisplayName ?? Path.GetFileName(favorite.Path)) == g.Key);
                        if (sameNameGroup != null && sameNameGroup.Count() > 1)
                        {
                            var parentDir = Path.GetDirectoryName(favorite.Path);
                            if (!string.IsNullOrEmpty(parentDir))
                            {
                                var parentName = Path.GetFileName(parentDir);
                                if (!string.IsNullOrEmpty(parentName))
                                    displayName = $"{displayName} ({parentName})";
                            }
                        }

                        return new FavoriteItem
                        {
                            Favorite = favorite,
                            IconKey = iconKey,
                            DisplayName = displayName,
                            Path = favorite.Path
                        };
                    }).ToList();

                    var translatedGroupName = group.Name;
                    if (group.Id == 1 && translatedGroupName == "文件夹")
                        translatedGroupName = locService?["Favorites.Folder"] ?? "文件夹";
                    else if (group.Id == 2 && translatedGroupName == "文件")
                        translatedGroupName = locService?["Favorites.File"] ?? "文件";

                    return new FavoriteGroupItem
                    {
                        Id = group.Id,
                        Name = translatedGroupName,
                        Items = items
                    };
                }).ToList();

                return displayGroups;
            }
            catch (Exception)
            {
                return new List<FavoriteGroupItem>();
            }
        }

        /// <summary>
        /// 加载收藏列表 (按分组加载)
        /// 已废弃: 请使用 GetFavoriteGroups 并通过 MVVM 绑定
        /// </summary>
        [Obsolete("Use GetFavoriteGroups() and bind to ViewModel instead.")]
        public void LoadFavorites(ItemsControl groupsControl)
        {
            if (groupsControl == null) return;

            _dispatcher.Invoke(() =>
            {
                groupsControl.ItemsSource = GetFavoriteGroups();
            });
        }

        /// <summary>
        /// 加载旧版收藏列表 (兼容逻辑，逐步淘汰)
        /// </summary>
        public void LoadFavorites(ListBox folderFavoritesListBox, ListBox fileFavoritesListBox)
        {
            _dispatcher.Invoke(() =>
            {
                try
                {
                    var favorites = _favoriteRepository.GetAllFavorites();

                    // 分组同名项
                    var nameGroups = favorites.GroupBy(f =>
                    {
                        string name = f.DisplayName ?? Path.GetFileName(f.Path);
                        if (string.IsNullOrEmpty(name)) name = f.Path;
                        return name;
                    }).ToList();

                    // 创建显示项列表
                    var allDisplayItems = favorites.Select(favorite =>
                    {
                        string iconKey = favorite.IsDirectory ? "Icon_Folder" : "Icon_Document";
                        string displayName = favorite.DisplayName ?? Path.GetFileName(favorite.Path);
                        if (string.IsNullOrEmpty(displayName))
                        {
                            displayName = favorite.Path;
                        }

                        // 如果存在同名项，添加路径标识
                        var sameNameGroup = nameGroups.FirstOrDefault(g =>
                        {
                            string name = favorite.DisplayName ?? Path.GetFileName(favorite.Path);
                            if (string.IsNullOrEmpty(name)) name = favorite.Path;
                            return g.Key == name;
                        });

                        if (sameNameGroup != null && sameNameGroup.Count() > 1)
                        {
                            // 添加父文件夹名称作为区分
                            var parentDir = Path.GetDirectoryName(favorite.Path);
                            if (!string.IsNullOrEmpty(parentDir))
                            {
                                var parentName = Path.GetFileName(parentDir);
                                if (!string.IsNullOrEmpty(parentName))
                                {
                                    displayName = $"{displayName} ({parentName})";
                                }
                            }
                        }

                        return new FavoriteItem
                        {
                            Favorite = favorite,
                            IconKey = iconKey,
                            DisplayName = displayName,
                            Path = favorite.Path
                        };
                    }).ToList();

                    // 分离文件夹和文件
                    var folderItems = allDisplayItems.Where(i => i.Favorite.IsDirectory).OrderBy(i => i.Favorite.SortOrder).ToList();
                    var fileItems = allDisplayItems.Where(i => !i.Favorite.IsDirectory).OrderBy(i => i.Favorite.SortOrder).ToList();

                    // 绑定文件夹列表
                    if (folderFavoritesListBox != null)
                    {
                        folderFavoritesListBox.ItemsSource = folderItems;
                        folderFavoritesListBox.DisplayMemberPath = null;
                        ConfigureListBoxEvents(folderFavoritesListBox);
                    }

                    // 绑定文件列表
                    if (fileFavoritesListBox != null)
                    {
                        fileFavoritesListBox.ItemsSource = fileItems;
                        fileFavoritesListBox.DisplayMemberPath = null;
                        ConfigureListBoxEvents(fileFavoritesListBox);
                    }

                    // FavoritesLoaded?.Invoke(this, EventArgs.Empty);  // Removed to avoid infinite loop when MainWindow reloads on this event
                }
                catch
                {
                    if (folderFavoritesListBox != null) folderFavoritesListBox.ItemsSource = null;
                    if (fileFavoritesListBox != null) fileFavoritesListBox.ItemsSource = null;
                }
            });
        }

        public void ConfigureListBoxEvents(ListBox listBox)
        {
            // 设置选择事件（单击进入）
            listBox.SelectionChanged -= FavoritesListBox_SelectionChanged;
            listBox.SelectionChanged += FavoritesListBox_SelectionChanged;

            // 设置右键菜单
            listBox.ContextMenu = CreateFavoritesContextMenu(listBox);
            listBox.PreviewMouseRightButtonDown -= FavoritesListBox_PreviewMouseRightButtonDown;
            listBox.PreviewMouseRightButtonDown += FavoritesListBox_PreviewMouseRightButtonDown;

            // 设置鼠标中键事件 - 已在 MainWindow.Initialization 中处理，这里移除或保留作为备用？
            // 原逻辑包含在此类中，保留以维持功能完整性
            
            // 初始化拖拽排序
            InitializeFavoritesDragDrop(listBox);
        }

        public void ConfigureGroupHeaderEvents(Grid grid)
        {
            if (grid == null) return;
            grid.AllowDrop = true;
            grid.DragOver += FavoritesGroupHeader_DragOver;
            grid.DragLeave += FavoritesGroupHeader_DragLeave;
            grid.Drop += FavoritesGroupHeader_Drop;
        }

        /// <summary>
        /// 添加收藏
        /// </summary>
        public void AddFavorite(List<FileSystemItem> selectedItems, int groupId = 1)
        {
            var locService = App.ServiceProvider?.GetService<YiboFile.Services.Localization.ILocalizationService>();
            if (selectedItems == null || selectedItems.Count == 0)
            {
                YiboFile.DialogService.Info(locService?["Favorites.SelectItemsFirst"] ?? "请先选择要收藏的文件或文件夹");
                return;
            }

            int successCount = 0;

            foreach (var item in selectedItems)
            {
                try
                {
                    // 注意：现在支持移动分组，所以不检查是否已收藏，而是直接 AddFavorite (INSERT OR REPLACE)
                    string displayName = item.Name;
                    _favoriteRepository.AddFavorite(item.Path, item.IsDirectory, displayName, groupId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    YiboFile.DialogService.Error($"{locService?["Favorites.AddFailed"] ?? "收藏失败"}: {item.Name} - {ex.Message}");
                }
            }

            // 触发重新加载通知
            _messageBus?.Publish(new FavoritesUpdatedMessage());

            if (successCount > 0)
            {
                string format = locService?["Favorites.AddSuccessFormat"] ?? "成功添加 {0} 个项目到收藏";
                NotificationService.Show(string.Format(format, successCount), NotificationType.Success);
            }
        }

        /// <summary>
        /// 移除收藏
        /// </summary>
        public void RemoveFavorite(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            _favoriteRepository.RemoveFavorite(path);
            _messageBus?.Publish(new FavoritesUpdatedMessage());
        }

        #region 分组管理方法

        public List<FavoriteGroup> GetAllGroups() => _favoriteRepository.GetAllGroups();

        public int CreateGroup(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            int newId = _favoriteRepository.CreateGroup(name);
            _messageBus?.Publish(new FavoritesUpdatedMessage());
            return newId;
        }

        public void RenameGroup(int id, string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            _favoriteRepository.RenameGroup(id, name);
            _messageBus?.Publish(new FavoritesUpdatedMessage());
        }

        public void DeleteGroup(int id)
        {
            var locService = App.ServiceProvider?.GetService<YiboFile.Services.Localization.ILocalizationService>();
            if (id == 1)
            {
                YiboFile.DialogService.Info(locService?["Favorites.DefaultGroupCannotDelete"] ?? "默认分组不能删除");
                return;
            }
            _favoriteRepository.DeleteGroup(id);
            _messageBus?.Publish(new FavoritesUpdatedMessage());
        }

        #endregion

        #endregion

        #region 事件处理

        private void FavoritesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem == null) return;
            if (_isDraggingFavorite) return; // 如果正在拖拽，不处理单击
            if (_suppressFavoriteSelectionNavigation) return; // 右键上下文菜单打开时不导航

            // 使用反射获取Favorite对象
            var selectedItem = listBox.SelectedItem;
            var favoriteProperty = selectedItem.GetType().GetProperty("Favorite");
            if (favoriteProperty == null) return;

            var favorite = favoriteProperty.GetValue(selectedItem) as YiboFile.Favorite;
            if (favorite == null) return;

            if (favorite.IsDirectory && Directory.Exists(favorite.Path))
            {
                // Fix BUG-019: Disable broadcast to prevent double navigation (handled by NavigationPanelControl)
                // _messageBus?.Publish(new NavigateToPathMessage(favorite.Path));
            }
            else if (!favorite.IsDirectory && File.Exists(favorite.Path))
            {
                // _messageBus?.Publish(new OpenFileRequestMessage(favorite.Path)); 
                // Keep file opening if not handled elsewhere? 
                // NavigationCoordinator handles file via OpenFileRequestMessage too.
                // But FavoriteListBox_PreviewMouseDown handles files? No, it only handles directories?
                // Let's check PreviewMouseDown. It handles Directory.Exists.
                // It DOES NOT handle files. So KEEP this for files?
                // But wait, the user complaint is "Clicking favorite links causes simultaneous navigation".
                // Usually implies folders.
                // If I disable folder broadcast, folders are fixed.
                // If I keep file broadcast, files work.
                _messageBus?.Publish(new OpenFileRequestMessage(favorite.Path));
            }
            else
            {
                var locService = App.ServiceProvider?.GetService<YiboFile.Services.Localization.ILocalizationService>();
                string notExistStr = locService?["Favorites.PathNotExist"] ?? "路径不存在";
                string askRemoveStr = locService?["Favorites.AskRemove"] ?? "是否从收藏中移除？";
                if (YiboFile.DialogService.Ask($"{notExistStr}: {favorite.Path}\n\n{askRemoveStr}", "提示"))
                {
                    _favoriteRepository.RemoveFavorite(favorite.Path);
                    _messageBus?.Publish(new FavoritesUpdatedMessage());
                    NotificationService.Show(locService?["Favorites.RemovedInvalid"] ?? "已移除无效收藏", NotificationType.Success);
                }
            }

            // 清除选择，避免残留选中状态
            listBox.SelectedItem = null;
        }

        private void FavoritesListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = true;
            var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (item != null)
            {
                item.IsSelected = true;
            }
        }

        private ContextMenu CreateFavoritesContextMenu(ListBox listBox)
        {
            var menu = new ContextMenu();
            menu.Closed += (s, e) =>
            {
                _suppressFavoriteSelectionNavigation = false;
                if (listBox != null)
                    listBox.SelectedItem = null;
            };

            var locService = App.ServiceProvider?.GetService<YiboFile.Services.Localization.ILocalizationService>();
            var navigationCoordinator = _navigationCoordinator;
            
            // 辅助方法：获取当前选中的 Favorite
            YiboFile.Favorite GetSelectedFavorite()
            {
                if (listBox.SelectedItem == null) return null;
                var selectedItem = listBox.SelectedItem;
                var favoriteProperty = selectedItem.GetType().GetProperty("Favorite");
                return favoriteProperty?.GetValue(selectedItem) as YiboFile.Favorite;
            }

            // 辅助方法：创建带图标的菜单项
            MenuItem CreateIconicMenuItem(string headerKey, string defaultHeader, string icon, bool isBold = false)
            {
                var headerText = (headerKey != null ? locService?[headerKey] : null) ?? defaultHeader;
                var item = new MenuItem 
                { 
                    Header = headerText,
                    FontWeight = isBold ? FontWeights.Bold : FontWeights.Normal,
                    Icon = new TextBlock { Text = icon, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 }
                };
                return item;
            }

            // --- 组1: 打开与定位 ---
            
            // 1. 打开
            var openItem = CreateIconicMenuItem("Dialog.Open", "打开", "📂", true);
            openItem.Click += (s, e) =>
            {
                var favorite = GetSelectedFavorite();
                if (favorite != null)
                    navigationCoordinator?.HandleFavoriteNavigation(favorite, YiboFile.Models.Navigation.ClickType.LeftClick);
            };
            menu.Items.Add(openItem);

            // 2. 在新标签页打开
            var openNewTabItem = CreateIconicMenuItem("Favorites.OpenInNewTab", "在新标签页打开", "📑");
            openNewTabItem.Click += (s, e) =>
            {
                var favorite = GetSelectedFavorite();
                if (favorite != null)
                    navigationCoordinator?.HandleFavoriteNavigation(favorite, YiboFile.Models.Navigation.ClickType.MiddleClick);
            };
            menu.Items.Add(openNewTabItem);

            // 3. 打开文件位置
            var openLocationItem = CreateIconicMenuItem("Favorites.OpenLocation", "打开文件位置", "📍");
            openLocationItem.Click += (s, e) =>
            {
                var favorite = GetSelectedFavorite();
                if (favorite != null)
                {
                    string parentPath = Path.GetDirectoryName(favorite.Path);
                    if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
                    {
                        navigationCoordinator?.HandlePathNavigation(parentPath, YiboFile.Models.Navigation.NavigationSource.Favorite, YiboFile.Models.Navigation.ClickType.LeftClick, pathToSelect: favorite.Path);
                    }
                    else
                    {
                        navigationCoordinator?.HandleFavoriteNavigation(favorite, YiboFile.Models.Navigation.ClickType.LeftClick);
                    }
                }
            };
            menu.Items.Add(openLocationItem);

            menu.Items.Add(new Separator());

            // --- 组2: 实用工具 ---

            // 4. 重命名别名
            var renameItem = CreateIconicMenuItem("Favorites.Rename", "重命名别名", "✏️");
            renameItem.Click += (s, e) =>
            {
                var favorite = GetSelectedFavorite();
                if (favorite != null)
                {
                    var currentName = !string.IsNullOrEmpty(favorite.DisplayName) ? favorite.DisplayName : Path.GetFileName(favorite.Path);
                    var newName = DialogService.ShowInput(locService?["Favorites.Rename"] ?? "输入新的收藏别名", currentName);
                    if (newName != null)
                    {
                        _favoriteRepository.UpdateFavoriteDisplayName(favorite.Path, newName);
                        _messageBus?.Publish(new FavoritesUpdatedMessage());
                    }
                }
            };
            menu.Items.Add(renameItem);

            // 5. 复制路径
            var copyPathItem = CreateIconicMenuItem("Favorites.CopyPath", "复制完整路径", "📋");
            copyPathItem.Click += (s, e) =>
            {
                var favorite = GetSelectedFavorite();
                if (favorite != null)
                {
                    // 使用异步复制，防止剪贴板操作阻塞 UI (修复卡顿问题)
                    ClipboardHelper.SetTextAsync(favorite.Path, (success) => 
                    {
                        if (success)
                        {
                            NotificationService.Show(locService?["TabContent.Context.CopyPath"] ?? "路径已复制", NotificationType.Success);
                        }
                        else
                        {
                            NotificationService.Show("无法访问剪贴板，请重试", NotificationType.Error);
                        }
                    });
                }
            };
            menu.Items.Add(copyPathItem);

            // 6. 在外部资源管理器打开
            var externalItem = CreateIconicMenuItem("Favorites.OpenExplorer", "在资源管理器中打开", "🖥️");
            externalItem.Click += (s, e) =>
            {
                var favorite = GetSelectedFavorite();
                if (favorite != null && (Directory.Exists(favorite.Path) || File.Exists(favorite.Path)))
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{favorite.Path}\"");
                    }
                    catch { /* 处理异常 */ }
                }
            };
            menu.Items.Add(externalItem);

            menu.Items.Add(new Separator());

            // --- 组3: 管理 ---
            
            // 7. 属性
            var propertiesItem = CreateIconicMenuItem("Favorites.Properties", "属性", "ℹ️");
            propertiesItem.Click += (s, e) =>
            {
                var favorite = GetSelectedFavorite();
                if (favorite != null)
                {
                    var fileItem = new FileSystemItem 
                    { 
                        Path = favorite.Path, 
                        IsDirectory = favorite.IsDirectory, 
                        Name = favorite.DisplayName ?? Path.GetFileName(favorite.Path) 
                    };
                    _messageBus?.Publish(new ShowPropertiesRequestMessage(fileItem, favorite.Path));
                }
            };
            menu.Items.Add(propertiesItem);

            // 8. 删除收藏
            var removeItem = CreateIconicMenuItem("Favorites.RemoveFavorite", "删除收藏", "🗑️");
            removeItem.Click += (s, e) =>
            {
                var favorite = GetSelectedFavorite();
                if (favorite != null)
                {
                    _favoriteRepository.RemoveFavorite(favorite.Path);
                    _messageBus?.Publish(new FavoritesUpdatedMessage());
                    NotificationService.Show(locService?["Favorites.CancelFavorite"] ?? "已取消收藏", NotificationType.Success);
                }
            };
            menu.Items.Add(removeItem);

            return menu;
        }

        #endregion

        #region 拖拽排序

        private void InitializeFavoritesDragDrop(ListBox listBox)
        {
            if (listBox == null) return;

            // 启用拖拽排序
            listBox.PreviewMouseLeftButtonDown += FavoritesListBox_PreviewMouseLeftButtonDown;
            listBox.PreviewMouseLeftButtonUp += FavoritesListBox_PreviewMouseLeftButtonUp;
            listBox.Drop += FavoritesListBox_Drop;
            listBox.DragOver += FavoritesListBox_DragOver;
            listBox.DragLeave += FavoritesListBox_DragLeave;
            listBox.AllowDrop = true;
            listBox.PreviewMouseMove += FavoritesListBox_PreviewMouseMove;
        }

        private void FavoritesGroupHeader_DragOver(object sender, DragEventArgs e)
        {
            bool isInternal = e.Data.GetDataPresent("Favorite");
            bool isFileDrop = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (!isInternal && !isFileDrop)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            if (isInternal) e.Effects = DragDropEffects.Move;
            else e.Effects = DragDropEffects.Link;

            e.Handled = true;

            if (sender is Grid grid)
            {
                grid.Background = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
            }
        }

        private void FavoritesGroupHeader_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Grid grid)
            {
                grid.Background = Brushes.Transparent;
            }
        }

        private void FavoritesGroupHeader_Drop(object sender, DragEventArgs e)
        {
            if (sender is Grid grid)
            {
                grid.Background = Brushes.Transparent;
            }

            var targetGroup = (sender as FrameworkElement)?.DataContext as FavoriteGroupItem;
            if (targetGroup == null) return;

            // 处理内部移动或外部拖入
            if (e.Data.GetDataPresent("Favorite"))
            {
                var dragged = e.Data.GetData("Favorite") as YiboFile.Favorite;
                if (dragged != null && dragged.GroupId != targetGroup.Id)
                {
                    _favoriteRepository.AddFavorite(dragged.Path, dragged.IsDirectory, dragged.DisplayName, targetGroup.Id);
                    _messageBus?.Publish(new FavoritesUpdatedMessage());
                }
                e.Handled = true;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null && paths.Length > 0)
                {
                    var itemsToAdd = paths.Select(p => new FileSystemItem
                    {
                        Path = p,
                        IsDirectory = Directory.Exists(p),
                        Name = Path.GetFileName(p)
                    }).ToList();

                    AddFavorite(itemsToAdd, targetGroup.Id);
                    e.Handled = true;
                }
            }
        }

        private void FavoritesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedFavorite = null;
            _isDraggingFavorite = false;
        }

        private void FavoritesListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(null);
                var diff = _dragStartPoint - currentPoint;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var listBox = sender as ListBox;
                    if (listBox != null)
                    {
                        if (_draggedFavorite == null)
                        {
                            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                            if (listBoxItem != null)
                            {
                                var item = listBoxItem.DataContext;
                                var favoriteProperty = item.GetType().GetProperty("Favorite");
                                if (favoriteProperty != null)
                                {
                                    _draggedFavorite = favoriteProperty.GetValue(item) as YiboFile.Favorite;
                                }
                            }
                        }

                        if (_draggedFavorite != null)
                        {
                            _isDraggingFavorite = true;
                            var dataObject = new DataObject("Favorite", _draggedFavorite);
                            DragDrop.DoDragDrop(listBox, dataObject, DragDropEffects.Move);
                        }
                    }
                }
            }
        }

        private void FavoritesListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingFavorite = false;
            _draggedFavorite = null;
        }

        private void FavoritesListBox_DragOver(object sender, DragEventArgs e)
        {
            // 检查数据格式
            bool isInternal = e.Data.GetDataPresent("Favorite");
            bool isFileDrop = e.Data.GetDataPresent(DataFormats.FileDrop);

            if (!isInternal && !isFileDrop)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            if (isInternal)
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.Link;
            }

            e.Handled = true;

            var listBox = sender as ListBox;
            if (listBox == null) return;

            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            bool insertBefore = true;
            if (targetItem != null)
            {
                Point p = e.GetPosition(targetItem);
                insertBefore = p.Y < targetItem.ActualHeight / 2;
            }

            // 仅在目标项或位置变化时更新，避免频繁触发布局更新
            if (targetItem == _lastDragOverItem && insertBefore == _lastInsertBefore) return;

            // 清除旧的视觉状态
            ClearLastDragIndicator();

            _lastDragOverItem = targetItem;
            _lastInsertBefore = insertBefore;

            // 设置新的视觉状态（带动态补偿，防止高度抖动）
            if (targetItem != null)
            {
                // 保存原始属性
                _originalPadding = targetItem.Padding;
                _originalBorderThickness = targetItem.BorderThickness;

                var highlightBrush = Application.Current.TryFindResource("AccentDefaultBrush") as Brush ?? Brushes.RoyalBlue;
                targetItem.BorderBrush = highlightBrush;

                var oldP = _originalPadding.Value;
                var oldT = _originalBorderThickness.Value;

                // 计算原始边框对总高度的贡献
                double originalVerticalBorderHeight = oldT.Top + oldT.Bottom;
                // 我们想要一个 2px 的指示线
                double targetLineThickness = 2.0;
                
                // 计算指示线出现后相对于原高度的垂直增量
                double heightDelta = targetLineThickness - originalVerticalBorderHeight;

                if (insertBefore)
                {
                    // 仅设置顶部边框，其余设为 0 以显示为“线形”
                    targetItem.BorderThickness = new Thickness(0, targetLineThickness, 0, 0);
                    // 补偿 Padding 以抵消高度增量
                    targetItem.Padding = new Thickness(oldP.Left, Math.Max(0, oldP.Top - heightDelta), oldP.Right, oldP.Bottom);
                }
                else
                {
                    // 仅设置底部边框
                    targetItem.BorderThickness = new Thickness(0, 0, 0, targetLineThickness);
                    // 补偿 Padding 以抵消高度增量
                    targetItem.Padding = new Thickness(oldP.Left, oldP.Top, oldP.Right, Math.Max(0, oldP.Bottom - heightDelta));
                }
            }
        }

        private void ClearLastDragIndicator()
        {
            if (_lastDragOverItem != null && _originalPadding.HasValue && _originalBorderThickness.HasValue)
            {
                _lastDragOverItem.BorderThickness = _originalBorderThickness.Value;
                _lastDragOverItem.Padding = _originalPadding.Value;
                _lastDragOverItem.BorderBrush = Brushes.Transparent;
            }
            _lastDragOverItem = null;
            _originalPadding = null;
            _originalBorderThickness = null;
        }

        private void FavoritesListBox_DragLeave(object sender, DragEventArgs e)
        {
            ClearLastDragIndicator();
            _isDraggingFavorite = false;
        }

        private void FavoritesListBox_Drop(object sender, DragEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            // 获取目标分组信息
            var targetGroup = listBox.DataContext as FavoriteGroupItem;
            if (targetGroup == null) return;

            ClearLastDragIndicator();

            // 情况1：内部排序/移动 (数据格式为 "Favorite")
            if (e.Data.GetDataPresent("Favorite"))
            {
                var draggedFavorite = e.Data.GetData("Favorite") as YiboFile.Favorite;
                if (draggedFavorite == null) return;

                // 目标项和插入位置计算
                var targetItemInside = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                bool insertBefore = true;
                if (targetItemInside != null)
                {
                    Point p = e.GetPosition(targetItemInside);
                    insertBefore = p.Y < targetItemInside.ActualHeight / 2;
                }

                // 情况1.1：跨分组拖拽
                if (draggedFavorite.GroupId != targetGroup.Id)
                {
                    _favoriteRepository.AddFavorite(draggedFavorite.Path, draggedFavorite.IsDirectory, draggedFavorite.DisplayName, targetGroup.Id);
                    
                    // 如果拖到了具体某一项上，需要重新排序以到达正确位置
                    if (targetItemInside != null)
                    {
                        var allFavs = _favoriteRepository.GetAllFavorites();
                        var targetData = targetItemInside.DataContext;
                        var targetFavorite = targetData?.GetType().GetProperty("Favorite")?.GetValue(targetData) as YiboFile.Favorite;
                        if (targetFavorite != null)
                        {
                            var groupFavs = allFavs.Where(f => f.GroupId == targetGroup.Id).OrderBy(f => f.SortOrder).ToList();
                            var newlyAdded = groupFavs.FirstOrDefault(f => f.Path == draggedFavorite.Path && f.Id != draggedFavorite.Id);
                            if (newlyAdded != null)
                            {
                                int dragIdx = groupFavs.IndexOf(newlyAdded);
                                int targetIdx = groupFavs.IndexOf(targetFavorite);
                                groupFavs.RemoveAt(dragIdx);
                                if (!insertBefore) targetIdx++;
                                if (targetIdx > groupFavs.Count) targetIdx = groupFavs.Count;
                                groupFavs.Insert(targetIdx, newlyAdded);
                                for (int i = 0; i < groupFavs.Count; i++) _favoriteRepository.UpdateSortOrder(groupFavs[i].Id, i);
                            }
                        }
                    }
                    
                    _messageBus?.Publish(new FavoritesUpdatedMessage());
                    _draggedFavorite = null;
                    _isDraggingFavorite = false;
                    e.Handled = true;
                    return;
                }

                // 情况1.2：组内排序
                if (targetItemInside == null || targetItemInside.DataContext == null) return;

                var currentTargetData = targetItemInside.DataContext;
                var favoriteProp = currentTargetData.GetType().GetProperty("Favorite");
                if (favoriteProp == null) return;

                var targetFavoriteObj = favoriteProp.GetValue(currentTargetData) as YiboFile.Favorite;
                if (targetFavoriteObj == null || targetFavoriteObj.Id == draggedFavorite.Id) return;

                // 更新排序顺序
                var allFavorites = _favoriteRepository.GetAllFavorites();
                var groupFavorites = allFavorites.Where(f => f.GroupId == targetGroup.Id).OrderBy(f => f.SortOrder).ToList();

                var draggedIndex = groupFavorites.FindIndex(f => f.Id == draggedFavorite.Id);
                var targetIndex = groupFavorites.FindIndex(f => f.Id == targetFavoriteObj.Id);

                if (draggedIndex >= 0 && targetIndex >= 0 && draggedIndex != targetIndex)
                {
                    groupFavorites.RemoveAt(draggedIndex);
                    // 重新计算 targetIndex
                    targetIndex = groupFavorites.FindIndex(f => f.Id == targetFavoriteObj.Id);
                    if (!insertBefore) targetIndex++;
                    if (targetIndex > groupFavorites.Count) targetIndex = groupFavorites.Count;
                    
                    groupFavorites.Insert(targetIndex, draggedFavorite);

                    for (int i = 0; i < groupFavorites.Count; i++)
                    {
                        _favoriteRepository.UpdateSortOrder(groupFavorites[i].Id, i);
                    }
                    _messageBus?.Publish(new FavoritesUpdatedMessage());
                }

                _draggedFavorite = null;
                _isDraggingFavorite = false;
                e.Handled = true;
            }
            // 情况2：外部文件拖入 (数据格式为 FileDrop)
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null && paths.Length > 0)
                {
                    var itemsToAdd = paths.Select(p => new FileSystemItem
                    {
                        Path = p,
                        IsDirectory = Directory.Exists(p),
                        Name = Path.GetFileName(p)
                    }).ToList();

                    // 先添加收藏
                    AddFavorite(itemsToAdd, targetGroup.Id);

                    // 如果拖到了具体某一项上，则执行插入排序
                    var targetItemInside = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                    if (targetItemInside != null)
                    {
                        Point p = e.GetPosition(targetItemInside);
                        bool insertBefore = p.Y < targetItemInside.ActualHeight / 2;

                        var targetData = targetItemInside.DataContext;
                        var targetFavorite = targetData?.GetType().GetProperty("Favorite")?.GetValue(targetData) as YiboFile.Favorite;

                        if (targetFavorite != null)
                        {
                            var allFavs = _favoriteRepository.GetAllFavorites();
                            var groupFavs = allFavs.Where(f => f.GroupId == targetGroup.Id).OrderBy(f => f.SortOrder).ToList();
                            
                            // 获取刚刚添加的几项（按路径匹配）
                            var addedPaths = itemsToAdd.Select(i => i.Path).ToHashSet();
                            var addedFavs = groupFavs.Where(f => addedPaths.Contains(f.Path)).ToList();

                            if (addedFavs.Any())
                            {
                                int targetIndex = groupFavs.IndexOf(targetFavorite);
                                if (!insertBefore) targetIndex++;

                                // 从原位置移除
                                foreach (var fav in addedFavs) groupFavs.Remove(fav);

                                // 插入到目标位置
                                if (targetIndex > groupFavs.Count) targetIndex = groupFavs.Count;
                                groupFavs.InsertRange(targetIndex, addedFavs);

                                // 更新所有项的排序
                                for (int i = 0; i < groupFavs.Count; i++)
                                {
                                    _favoriteRepository.UpdateSortOrder(groupFavs[i].Id, i);
                                }
                                _messageBus?.Publish(new FavoritesUpdatedMessage());
                            }
                        }
                    }
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region 辅助方法

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        #endregion
    }
}


