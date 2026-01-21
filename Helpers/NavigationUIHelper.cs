using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YiboFile.Services.Navigation;

namespace YiboFile.Helpers
{
    /// <summary>
    /// 导航 UI 辅助实现类
    /// </summary>
    internal class NavigationUIHelper : INavigationUIHelper
    {
        private readonly MainWindow _mainWindow;

        public NavigationUIHelper(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public System.Windows.Threading.Dispatcher Dispatcher => _mainWindow.Dispatcher;

        public IEnumerable GetDrivesListItems()
        {
            return _mainWindow.DrivesTreeView?.ItemsSource;
        }

        public IEnumerable GetQuickAccessListItems()
        {
            return _mainWindow.QuickAccessListBox?.Items;
        }

        public IEnumerable GetFavoritesListItems()
        {
            var folderItems = _mainWindow.NavigationPanelControl.FolderFavoritesListBoxControl?.Items;
            var fileItems = _mainWindow.NavigationPanelControl.FileFavoritesListBoxControl?.Items;

            if (folderItems == null && fileItems == null) return null;

            var list = new System.Collections.Generic.List<object>();
            if (folderItems != null) foreach (var item in folderItems) list.Add(item);
            if (fileItems != null) foreach (var item in fileItems) list.Add(item);
            return list;
        }

        public IEnumerable GetLibrariesListItems()
        {
            return _mainWindow.LibrariesListBox?.Items;
        }

        public void SetItemHighlight(string listType, object item, bool highlight)
        {
            // Special handling for Drive TreeView
            if (listType == "Drive")
            {
                var treeView = _mainWindow.DrivesTreeView;
                if (treeView == null || item == null) return;

                // Simple handling for root items for now. 
                // Recursive finding of TreeViewItems is complex and might be better handled by a TreeView-specific helper later.
                var container = treeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container != null)
                {
                    UpdateContainerTag(container, highlight);
                }
                return;
            }

            ListBox listBox = null;
            switch (listType)
            {
                case "QuickAccess":
                    listBox = _mainWindow.QuickAccessListBox;
                    break;
                case "Favorites":
                case "FolderFavorites":
                    // 先尝试文件夹收藏
                    listBox = _mainWindow.NavigationPanelControl.FolderFavoritesListBoxControl;
                    HighlightItemInListBox(listBox, item, highlight);

                    // 再尝试文件收藏 (如果传入的是Favorites泛型)
                    listBox = _mainWindow.NavigationPanelControl.FileFavoritesListBoxControl;
                    HighlightItemInListBox(listBox, item, highlight);
                    return; // 已处理
                case "FileFavorites":
                    listBox = _mainWindow.NavigationPanelControl.FileFavoritesListBoxControl;
                    break;
                case "Library":
                    listBox = _mainWindow.LibrariesListBox;
                    break;
            }

            if (listBox != null)
            {
                HighlightItemInListBox(listBox, item, highlight);
            }
        }

        private void HighlightItemInListBox(ListBox listBox, object item, bool highlight)
        {

            try
            {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    UpdateContainerTag(container, highlight);
                }
                else
                {
                    // 如果容器还未生成，延迟执行（使用低优先级避免阻塞UI）
                    _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var retryContainer = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                        if (retryContainer != null)
                        {
                            UpdateContainerTag(retryContainer, highlight);
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception)
            {
            }
        }

        private void UpdateContainerTag(FrameworkElement container, bool highlight)
        {
            if (highlight)
            {
                if (container.Tag as string == "Match") return;
                container.Tag = "Match";
            }
            else
            {
                if (container.Tag as string == "Match") container.Tag = null;
            }
        }

        public void ClearListBoxHighlights(string listType)
        {
            if (listType == "Drive")
            {
                // For TreeView, we need to iterate differently. 
                // Assuming flat search highlighting for now, or just clearing root interactions.
                // This is a placeholder as full recursive clearing would be needed.
                var treeView = _mainWindow.DrivesTreeView;
                if (treeView?.ItemsSource == null) return;
                foreach (var item in treeView.ItemsSource)
                {
                    SetItemHighlight(listType, item, false);
                }
                return;
            }


            switch (listType)
            {
                case "QuickAccess":
                    ClearListBox(_mainWindow.QuickAccessListBox);
                    break;
                case "Favorites":
                    ClearListBox(_mainWindow.NavigationPanelControl.FolderFavoritesListBoxControl);
                    ClearListBox(_mainWindow.NavigationPanelControl.FileFavoritesListBoxControl);
                    break;
                case "Library":
                    ClearListBox(_mainWindow.LibrariesListBox);
                    break;
            }
        }

        private void ClearListBox(ListBox listBox)
        {
            if (listBox == null || listBox.Items == null) return;

            foreach (var item in listBox.Items)
            {
                SetItemHighlight("", item, false); // listType is ignored inside for loop logic above? No, wait.
                                                   // The original logic called SetItemHighlight(listType, item, false). 
                                                   // But SetItemHighlight uses listType to find the listbox again.
                                                   // We should refactor to pass the container or just reset the tag directly here or call UpdateContainerTag.

                try
                {
                    var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                    if (container != null) UpdateContainerTag(container, false);
                }
                catch { }
            }
        }

        public void SetNavigationContentVisibility(string mode)
        {
            // 隐藏所有导航内容（添加空值检查）
            if (_mainWindow.NavPathContent != null) _mainWindow.NavPathContent.Visibility = Visibility.Collapsed;
            if (_mainWindow.NavLibraryContent != null) _mainWindow.NavLibraryContent.Visibility = Visibility.Collapsed;
            if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Collapsed;

            // 根据模式显示对应内容
            switch (mode)
            {
                case "Path":
                    if (_mainWindow.NavPathContent != null) _mainWindow.NavPathContent.Visibility = Visibility.Visible;
                    if (_mainWindow.NavLibraryContent != null) _mainWindow.NavLibraryContent.Visibility = Visibility.Collapsed;
                    // if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Collapsed; // Phase 2
                    break;
                case "Library":
                    if (_mainWindow.NavPathContent != null) _mainWindow.NavPathContent.Visibility = Visibility.Collapsed;
                    if (_mainWindow.NavLibraryContent != null) _mainWindow.NavLibraryContent.Visibility = Visibility.Visible;
                    // if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Collapsed; // Phase 2
                    break;
                case "Tag":
                    // Phase 2 - Tag 导航模式恢复
                    if (_mainWindow.NavPathContent != null) _mainWindow.NavPathContent.Visibility = Visibility.Collapsed;
                    if (_mainWindow.NavLibraryContent != null) _mainWindow.NavLibraryContent.Visibility = Visibility.Collapsed;
                    if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Visible;
                    break;
            }
        }

        public void UpdateActionButtons(string mode)
        {
            // 使用 ConfigService 更新操作按钮
            _mainWindow._configService?.UpdateActionButtons(mode);
        }

        public SolidColorBrush GetResourceBrush(string resourceKey)
        {
            return _mainWindow.FindResource(resourceKey) as SolidColorBrush;
        }

        public void SetLibrarySelectedItem(object library)
        {
            if (_mainWindow.LibrariesListBox != null)
            {
                _mainWindow.LibrariesListBox.SelectedItem = library;
            }
        }
    }
}

