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

namespace YiboFile.Services.Favorite
{
    /// <summary>
    /// 收藏管理服务
    /// 负责收藏项的加载、添加、删除和拖拽排序
    /// </summary>
    public class FavoriteService
    {
        #region 事件定义

        /// <summary>
        /// 路径导航请求事件（文件夹）
        /// </summary>
        public event EventHandler<string> NavigateRequested;

        /// <summary>
        /// 文件打开请求事件（文件）
        /// </summary>
        public event EventHandler<string> FileOpenRequested;

        /// <summary>
        /// 新标签页创建请求事件
        /// </summary>
        public event EventHandler<string> CreateTabRequested;

        /// <summary>
        /// 收藏列表已加载事件
        /// </summary>
        public event EventHandler FavoritesLoaded;

        #endregion

        #region 私有字段

        private readonly System.Windows.Threading.Dispatcher _dispatcher;
        private YiboFile.Favorite _draggedFavorite = null;
        private System.Windows.Point _dragStartPoint;
        private bool _isDraggingFavorite = false;
        private bool _suppressFavoriteSelectionNavigation = false;

        #endregion

        #region 构造函数

        public FavoriteService(System.Windows.Threading.Dispatcher dispatcher = null)
        {
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
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
        /// 加载收藏列表 (分离文件夹和文件)
        /// </summary>
        public void LoadFavorites(ListBox folderFavoritesListBox, ListBox fileFavoritesListBox)
        {
            _dispatcher.Invoke(() =>
            {
                try
                {
                    var favorites = DatabaseManager.GetAllFavorites();

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

        private void ConfigureListBoxEvents(ListBox listBox)
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
            listBox.PreviewMouseDown -= FavoritesListBox_PreviewMouseDown;
            listBox.PreviewMouseDown += FavoritesListBox_PreviewMouseDown;

            // 初始化拖拽排序
            InitializeFavoritesDragDrop(listBox);
        }

        /// <summary>
        /// 添加收藏
        /// </summary>
        public void AddFavorite(List<FileSystemItem> selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
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

            // 触发重新加载事件
            FavoritesLoaded?.Invoke(this, EventArgs.Empty);

            if (successCount > 0)
                NotificationService.Show($"成功添加 {successCount} 个项目到收藏", NotificationType.Success);
        }

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
                NavigateRequested?.Invoke(this, favorite.Path);
            }
            else if (!favorite.IsDirectory && File.Exists(favorite.Path))
            {
                FileOpenRequested?.Invoke(this, favorite.Path);
            }
            else
            {
                if (MessageBox.Show($"路径不存在: {favorite.Path}\n\n是否从收藏中移除？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    DatabaseManager.RemoveFavorite(favorite.Path);
                    FavoritesLoaded?.Invoke(this, EventArgs.Empty);
                    NotificationService.Show("已移除无效收藏", NotificationType.Success);
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

        private void FavoritesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 处理鼠标中键点击打开新标签页
            if (e.ChangedButton == MouseButton.Middle)
            {
                var listBox = sender as ListBox;
                if (listBox == null) return;

                // 获取点击位置对应的项目
                var hitResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
                if (hitResult == null) return;

                // 向上查找 ListBoxItem
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listBox)
                {
                    if (current is ListBoxItem item && item.DataContext != null)
                    {
                        var favoriteProperty = item.DataContext.GetType().GetProperty("Favorite");
                        if (favoriteProperty != null)
                        {
                            var favorite = favoriteProperty.GetValue(item.DataContext) as YiboFile.Favorite;
                            if (favorite != null && favorite.IsDirectory)
                            {
                                try
                                {
                                    if (Directory.Exists(favorite.Path))
                                    {
                                        CreateTabRequested?.Invoke(this, favorite.Path);
                                        e.Handled = true;
                                        return;
                                    }
                                    else
                                    {
                                        MessageBox.Show($"路径不存在: {favorite.Path}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                        e.Handled = true;
                                        return;
                                    }
                                }
                                catch (UnauthorizedAccessException ex)
                                {
                                    MessageBox.Show($"无法访问路径: {favorite.Path}\n\n{ex.Message}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    e.Handled = true;
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"无法打开路径: {favorite.Path}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    e.Handled = true;
                                    return;
                                }
                            }
                        }
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
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

            var removeItem = new MenuItem { Header = "删除收藏" };
            removeItem.Click += (s, e) =>
            {
                if (listBox.SelectedItem != null)
                {
                    var selectedItem = listBox.SelectedItem;
                    var favoriteProperty = selectedItem.GetType().GetProperty("Favorite");
                    if (favoriteProperty != null)
                    {
                        var favorite = favoriteProperty.GetValue(selectedItem) as YiboFile.Favorite;
                        if (favorite != null)
                        {
                            // Capture references needed for reload
                            // 由于 LoadFavorites 现在需要两个 ListBox，我们需要知道上下文
                            // 或者通过事件通知上层重新加载
                            // 暂时简单地调用 LoadFavorites 需要保存 listbox 引用吗？
                            // 更好的方式是触发事件通知 MainWindow 更新

                            DatabaseManager.RemoveFavorite(favorite.Path);

                            // 触发重新加载 - 下游 MainWindow 监听此事件并调用 LoadFavorites
                            FavoritesLoaded?.Invoke(this, EventArgs.Empty);
                            NotificationService.Show("已取消收藏", NotificationType.Success);
                        }
                    }
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
            if (!e.Data.GetDataPresent("Favorite"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            var draggedItem = e.Data.GetData("Favorite") as YiboFile.Favorite;
            if (draggedItem == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            // 提供拖拽视觉效果
            var listBox = sender as ListBox;
            if (listBox != null)
            {
                var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetItem != null)
                {
                    // 高亮目标项
                    foreach (ListBoxItem item in FindVisualChildren<ListBoxItem>(listBox))
                    {
                        if (item == targetItem)
                        {
                            item.Background = new SolidColorBrush(Color.FromArgb(100, 33, 150, 243));
                        }
                        else if (item.Background is SolidColorBrush brush && brush.Color.A == 100)
                        {
                            item.Background = Brushes.Transparent;
                        }
                    }
                }
            }
        }

        private void FavoritesListBox_DragLeave(object sender, DragEventArgs e)
        {
            // 清除所有高亮
            var listBox = sender as ListBox;
            if (listBox != null)
            {
                foreach (ListBoxItem item in FindVisualChildren<ListBoxItem>(listBox))
                {
                    item.Background = Brushes.Transparent;
                }
            }
            _isDraggingFavorite = false;
        }

        private void FavoritesListBox_Drop(object sender, DragEventArgs e)
        {
            // 检查数据格式
            if (!e.Data.GetDataPresent("Favorite"))
            {
                return;
            }

            var draggedFavorite = e.Data.GetData("Favorite") as YiboFile.Favorite;
            if (draggedFavorite == null) return;

            var listBox = sender as ListBox;
            if (listBox == null)
            {
                return;
            }

            // 清除所有高亮
            foreach (ListBoxItem item in FindVisualChildren<ListBoxItem>(listBox))
            {
                item.Background = Brushes.Transparent;
            }

            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (targetItem == null || targetItem.DataContext == null)
            {
                return;
            }

            var targetData = targetItem.DataContext;
            var favoriteProperty = targetData.GetType().GetProperty("Favorite");
            if (favoriteProperty == null)
            {
                return;
            }

            var targetFavorite = favoriteProperty.GetValue(targetData) as YiboFile.Favorite;
            if (targetFavorite == null || targetFavorite.Id == draggedFavorite.Id)
            {
                return;
            }

            // 更新排序顺序并重新加载
            var favorites = DatabaseManager.GetAllFavorites().ToList();
            var draggedIndex = favorites.FindIndex(f => f.Id == draggedFavorite.Id);
            var targetIndex = favorites.FindIndex(f => f.Id == targetFavorite.Id);

            if (draggedIndex >= 0 && targetIndex >= 0 && draggedIndex != targetIndex)
            {
                // 重新排序：移除拖拽项，插入到目标位置
                var newOrder = new List<YiboFile.Favorite>();
                for (int i = 0; i < favorites.Count; i++)
                {
                    if (i == draggedIndex) continue; // 跳过被拖拽的项
                    if (i == targetIndex)
                    {
                        // 在目标位置插入
                        if (draggedIndex < targetIndex)
                        {
                            // 向下拖拽：先插入目标项，再插入被拖拽项
                            newOrder.Add(favorites[targetIndex]);
                            newOrder.Add(draggedFavorite);
                        }
                        else
                        {
                            // 向上拖拽：先插入被拖拽项，再插入目标项
                            newOrder.Add(draggedFavorite);
                            newOrder.Add(favorites[targetIndex]);
                        }
                    }
                    else
                    {
                        newOrder.Add(favorites[i]);
                    }
                }

                // 更新数据库中的SortOrder
                // 判断是文件夹还是文件，仅更新该组的顺序
                // 注意：由于我们分开了两个列表，这里的 newOrder 仅包含当前组（全是文件夹或全是文件）
                // 我们需要重新分配这些项的 SortOrder
                // 为了避免冲突，可以简单地重新分配所有 Favorites 的 SortOrder，或者只更新变动的。
                // 简单策略：获取该 item 类型（List 中第一个即可），找出所有该类型的 items，按新顺序更新

                int counter = 0;
                foreach (var item in newOrder)
                {
                    DatabaseManager.UpdateFavoriteSortOrder(item.Id, counter++);
                }

                // 触发 LoadFavorites
                FavoritesLoaded?.Invoke(this, EventArgs.Empty);

                // 重新加载显示
                FavoritesLoaded?.Invoke(this, EventArgs.Empty);
            }

            _draggedFavorite = null;
            _isDraggingFavorite = false;
            e.Handled = true;
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


