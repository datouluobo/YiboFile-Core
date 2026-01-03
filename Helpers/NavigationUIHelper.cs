using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OoiMRR.Services.Navigation;

namespace OoiMRR.Helpers
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
            return _mainWindow.DrivesListBox?.Items;
        }

        public IEnumerable GetQuickAccessListItems()
        {
            return _mainWindow.QuickAccessListBox?.Items;
        }

        public IEnumerable GetFavoritesListItems()
        {
            return _mainWindow.FavoritesListBox?.Items;
        }

        public IEnumerable GetLibrariesListItems()
        {
            return _mainWindow.LibrariesListBox?.Items;
        }

        public void SetItemHighlight(string listType, object item, bool highlight)
        {
            ListBox listBox = null;
            switch (listType)
            {
                case "Drive":
                    listBox = _mainWindow.DrivesListBox;
                    break;
                case "QuickAccess":
                    listBox = _mainWindow.QuickAccessListBox;
                    break;
                case "Favorites":
                    listBox = _mainWindow.FavoritesListBox;
                    break;
                case "Library":
                    listBox = _mainWindow.LibrariesListBox;
                    break;
            }

            if (listBox == null || item == null) return;

            try
            {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    if (highlight)
                    {
                        // 检查是否已经高亮，避免重复设置
                        if (container.Tag as string == "Match")
                            return;

                        // 只设置 Tag，让样式触发器处理视觉效果
                        container.Tag = "Match";
                    }
                    else
                    {
                        // 清除匹配高亮
                        var tag = container.Tag as string;
                        if (tag == "Match")
                        {
                            container.Tag = null;
                        }
                    }
                }
                else
                {
                    // 如果容器还未生成，延迟执行（使用低优先级避免阻塞UI）
                    _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var retryContainer = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                        if (retryContainer != null)
                        {
                            if (highlight)
                            {
                                if (retryContainer.Tag as string == "Match")
                                    return;

                                retryContainer.Tag = "Match";
                            }
                            else
                            {
                                var tag = retryContainer.Tag as string;
                                if (tag == "Match")
                                {
                                    retryContainer.Tag = null;
                                }
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception)
            {
            }
        }

        public void ClearListBoxHighlights(string listType)
        {
            ListBox listBox = null;
            switch (listType)
            {
                case "Drive":
                    listBox = _mainWindow.DrivesListBox;
                    break;
                case "QuickAccess":
                    listBox = _mainWindow.QuickAccessListBox;
                    break;
                case "Favorites":
                    listBox = _mainWindow.FavoritesListBox;
                    break;
                case "Library":
                    listBox = _mainWindow.LibrariesListBox;
                    break;
            }

            if (listBox == null || listBox.Items == null) return;

            foreach (var item in listBox.Items)
            {
                SetItemHighlight(listType, item, false);
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
                    if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Collapsed;
                    break;
                case "Library":
                    if (_mainWindow.NavPathContent != null) _mainWindow.NavPathContent.Visibility = Visibility.Collapsed;
                    if (_mainWindow.NavLibraryContent != null) _mainWindow.NavLibraryContent.Visibility = Visibility.Visible;
                    if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Collapsed;
                    break;
                case "Tag":
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
