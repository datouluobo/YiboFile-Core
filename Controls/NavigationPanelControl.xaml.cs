using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TagTrain.UI;

namespace OoiMRR.Controls
{
    /// <summary>
    /// 导航面板控件
    /// 包含路径导航、库导航、标签导航三个面板
    /// </summary>
    public partial class NavigationPanelControl : UserControl
    {
        // 事件定义
        public event MouseButtonEventHandler DrivesListBoxPreviewMouseDown;
        public event MouseButtonEventHandler QuickAccessListBoxPreviewMouseDown;
        public event MouseButtonEventHandler FavoritesListBoxPreviewMouseDown;
        public event SelectionChangedEventHandler LibrariesListBoxSelectionChanged;
        public event ContextMenuEventHandler LibrariesListBoxContextMenuOpening;
        public event MouseButtonEventHandler LibrariesListBoxPreviewMouseDown;
        public event RoutedEventHandler AddFavoriteClick;
        public event RoutedEventHandler AddTagToFileClick;
        public event RoutedEventHandler LibraryManageClick;
        public event RoutedEventHandler LibraryRefreshClick;
        public event RoutedEventHandler TagClickModeClick;
        public event RoutedEventHandler TagCategoryManageClick;
        public event Action<string, bool> TagBrowsePanelTagClicked;
        public event Action TagBrowsePanelCategoryManagementRequested;
        public event Action<string, bool> TagEditPanelTagClicked;
        public event Action TagEditPanelCategoryManagementRequested;
        public event RoutedEventHandler LibraryContextMenuClick;

        public NavigationPanelControl()
        {
            InitializeComponent();
            this.Loaded += NavigationPanelControl_Loaded;
        }

        private void NavigationPanelControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeEvents();
        }

        private void InitializeEvents()
        {
            // 路径导航事件
            var drivesListBox = DrivesListBoxControl;
            if (drivesListBox != null)
            {
                drivesListBox.PreviewMouseDown += (s, e) => DrivesListBoxPreviewMouseDown?.Invoke(s, e);
            }

            var quickAccessListBox = QuickAccessListBoxControl;
            if (quickAccessListBox != null)
            {
                quickAccessListBox.PreviewMouseDown += (s, e) => QuickAccessListBoxPreviewMouseDown?.Invoke(s, e);
            }

            var favoritesListBox = FavoritesListBoxControl;
            if (favoritesListBox != null)
            {
                favoritesListBox.PreviewMouseDown += (s, e) => FavoritesListBoxPreviewMouseDown?.Invoke(s, e);
            }

            // 库导航事件
            var librariesListBox = LibrariesListBoxControl;
            if (librariesListBox != null)
            {
                librariesListBox.SelectionChanged += (s, e) => LibrariesListBoxSelectionChanged?.Invoke(s, e);
                librariesListBox.ContextMenuOpening += (s, e) => LibrariesListBoxContextMenuOpening?.Invoke(s, e);
                librariesListBox.PreviewMouseDown += (s, e) => LibrariesListBoxPreviewMouseDown?.Invoke(s, e);
            }

            // 库上下文菜单事件
            var libraryContextMenu = LibraryContextMenuControl;
            if (libraryContextMenu != null)
            {
                foreach (var item in libraryContextMenu.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        menuItem.Click += (s, e) => LibraryContextMenuClick?.Invoke(s, e);
                    }
                }
            }

            // 底部按钮事件
            var addFavoriteButton = FindName("AddFavoriteButton") as Button;
            if (addFavoriteButton != null)
            {
                addFavoriteButton.Click += (s, e) => AddFavoriteClick?.Invoke(s, e);
            }

            var addTagToFileButton = FindName("AddTagToFileButton") as Button;
            if (addTagToFileButton != null)
            {
                addTagToFileButton.Click += (s, e) => AddTagToFileClick?.Invoke(s, e);
            }

            var libraryManageBtn = FindName("LibraryManageBtn") as Button;
            if (libraryManageBtn != null)
            {
                libraryManageBtn.Click += (s, e) => LibraryManageClick?.Invoke(s, e);
            }

            var libraryRefreshBtn = FindName("LibraryRefreshBtn") as Button;
            if (libraryRefreshBtn != null)
            {
                libraryRefreshBtn.Click += (s, e) => LibraryRefreshClick?.Invoke(s, e);
            }

            var tagClickModeBtn = FindName("TagClickModeBtn") as Button;
            if (tagClickModeBtn != null)
            {
                tagClickModeBtn.Click += (s, e) => TagClickModeClick?.Invoke(s, e);
            }

            var tagCategoryManageBtn = FindName("TagCategoryManageBtn") as Button;
            if (tagCategoryManageBtn != null)
            {
                tagCategoryManageBtn.Click += (s, e) => TagCategoryManageClick?.Invoke(s, e);
            }

            // TagPanel 事件
            var tagBrowsePanel = TagBrowsePanelControl;
            if (tagBrowsePanel != null)
            {
                tagBrowsePanel.TagClicked += (tagName, forceNewTab) => TagBrowsePanelTagClicked?.Invoke(tagName, forceNewTab);
                tagBrowsePanel.CategoryManagementRequested += () => TagBrowsePanelCategoryManagementRequested?.Invoke();
            }

            var tagEditPanel = TagEditPanelControl;
            if (tagEditPanel != null)
            {
                tagEditPanel.TagClicked += (tagName, forceNewTab) => TagEditPanelTagClicked?.Invoke(tagName, forceNewTab);
                tagEditPanel.CategoryManagementRequested += () => TagEditPanelCategoryManagementRequested?.Invoke();
            }
        }

        // 公共属性访问器（通过FindName获取，避免命名冲突）
        public WrapPanel BreadcrumbPanelControl => FindName("BreadcrumbPanel") as WrapPanel;
        public ListBox DrivesListBoxControl => FindName("DrivesListBox") as ListBox;
        public ListBox QuickAccessListBoxControl => FindName("QuickAccessListBox") as ListBox;
        public ListBox FavoritesListBoxControl => FindName("FavoritesListBox") as ListBox;
        public ListBox LibrariesListBoxControl => FindName("LibrariesListBox") as ListBox;
        public ContextMenu LibraryContextMenuControl => FindName("LibraryContextMenu") as ContextMenu;
        public Grid NavPathContentControl => FindName("NavPathContent") as Grid;
        public Grid NavLibraryContentControl => FindName("NavLibraryContent") as Grid;
        public Grid NavTagContentControl => FindName("NavTagContent") as Grid;
        public TagPanel TagBrowsePanelControl => FindName("TagBrowsePanel") as TagPanel;
        public TagPanel TagEditPanelControl => FindName("TagEditPanel") as TagPanel;
        public StackPanel LibraryBottomButtonsControl => FindName("LibraryBottomButtons") as StackPanel;
        public StackPanel TagBottomButtonsControl => FindName("TagBottomButtons") as StackPanel;
    }
}

