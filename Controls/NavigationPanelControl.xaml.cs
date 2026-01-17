using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
// using TagTrain.UI; // Phase 2

namespace YiboFile.Controls
{
    /// <summary>
    /// 导航面板控件
    /// 包含路径导航、库导航、标签导航三个面板
    /// </summary>
    public partial class NavigationPanelControl : UserControl
    {
        // 事件定义
        public event MouseButtonEventHandler DrivesListBoxPreviewMouseDown;
        public event SelectionChangedEventHandler DrivesListBoxSelectionChanged;
        public event MouseButtonEventHandler QuickAccessListBoxPreviewMouseDown;
        public event MouseButtonEventHandler FavoritesListBoxPreviewMouseDown;
        public event SelectionChangedEventHandler LibrariesListBoxSelectionChanged;
        public event ContextMenuEventHandler LibrariesListBoxContextMenuOpening;
        public event MouseButtonEventHandler LibrariesListBoxPreviewMouseDown;
        public event RoutedEventHandler AddFavoriteClick;
        public event RoutedEventHandler LibraryManageClick;
        // public event Action<string, bool> TagBrowsePanelTagClicked;
        // public event Action TagBrowsePanelCategoryManagementRequested;
        // public event Action<string, bool> TagEditPanelTagClicked; // Phase 2
        // public event Action TagEditPanelCategoryManagementRequested; // Phase 2
        public event RoutedEventHandler LibraryContextMenuClick;

        public event RoutedEventHandler DrivesTreeViewItemClick;

        public NavigationPanelControl()
        {
            InitializeComponent();
            this.Loaded += NavigationPanelControl_Loaded;
        }

        private void NavigationPanelControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeEvents();
        }

        // Handler for TreeViewItem PreviewMouseLeftButtonDown (defined in Style EventSetter)
        private void DrivesTreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only trigger if it's the TreeViewItem itself or its content (not the Expander)

            // Check if we clicked the toggle button or child container (ItemsPresenter)
            if (e.OriginalSource is DependencyObject source)
            {
                var parent = source;
                while (parent != null && parent != sender)
                {
                    if (parent is System.Windows.Controls.Primitives.ToggleButton)
                    {
                        return; // Let ToggleButton handle it
                    }
                    if (parent is ItemsPresenter)
                    {
                        return; // Let child handle it
                    }
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            }

            // Handle Double Click
            if (e.ClickCount > 1)
            {
                // Double click behavior:
                // We let the standard TreeView behavior (Expand/Collapse) happen.
                // We do NOT navigate again (navigation happened on first click).
                return;
            }

            // Handle Single Click - Navigate Immediately
            // This ensures maximum responsiveness.
            DrivesTreeViewItemClick?.Invoke(sender, e);
        }

        // Handler for TreeViewItem.Expanded event (for accordion behavior)
        private void DrivesTreeView_Expanded(object sender, RoutedEventArgs e)
        {
            // Accordion Logic: When a root item is expanded, collapse others
            // This method will contain the logic to collapse other TreeViewItems
            // when one is expanded.
            if (e.OriginalSource is TreeViewItem expandedItem)
            {
                // Ensure the event is handled only once per expansion
                e.Handled = true;

                // Get the parent TreeView
                var treeView = FindAncestor<TreeView>(expandedItem);
                if (treeView != null)
                {
                    // Use ItemContainerGenerator to check if the container belongs to the root ItemsSource
                    if (treeView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                    {
                        var index = treeView.ItemContainerGenerator.IndexFromContainer(expandedItem);
                        if (index != -1)
                        {
                            // It is a root item! Collapse others.
                            foreach (var item in treeView.Items)
                            {
                                var container = treeView.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                                if (container != null && container != expandedItem && container.IsExpanded)
                                {
                                    container.IsExpanded = false;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!e.Handled && sender is DependencyObject source)
            {
                var scrollViewer = FindAncestor<ScrollViewer>(source);
                if (scrollViewer != null)
                {
                    // Scroll incrementally based on Delta
                    // Standard wheel delta is 120. 
                    // 48 pixels is roughly 3 lines of text (16px fontsize).
                    var scrollAmount = e.Delta / 120.0 * 48.0;
                    scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - scrollAmount);
                    e.Handled = true;
                }
            }
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T typed)
                {
                    return typed;
                }
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void InitializeEvents()
        {
            // 路径导航事件
            var drivesTreeView = DrivesTreeViewControl;
            if (drivesTreeView != null)
            {
                drivesTreeView.PreviewMouseDown += (s, e) => DrivesListBoxPreviewMouseDown?.Invoke(s, e);
                drivesTreeView.SelectedItemChanged += (s, e) => DrivesListBoxSelectionChanged?.Invoke(s, null);
                drivesTreeView.PreviewMouseWheel += OnPreviewMouseWheel;

                // Accordion Logic: When a root item is expanded, collapse others
                drivesTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(DrivesTreeView_Expanded));
            }

            var quickAccessListBox = QuickAccessListBoxControl;
            if (quickAccessListBox != null)
            {
                quickAccessListBox.PreviewMouseDown += (s, e) => QuickAccessListBoxPreviewMouseDown?.Invoke(s, e);
                quickAccessListBox.PreviewMouseWheel += OnPreviewMouseWheel;
            }

            var favoritesListBox = FavoritesListBoxControl;
            if (favoritesListBox != null)
            {
                favoritesListBox.PreviewMouseDown += (s, e) => FavoritesListBoxPreviewMouseDown?.Invoke(s, e);
                favoritesListBox.PreviewMouseWheel += OnPreviewMouseWheel;
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


            var libraryManageBtn = FindName("LibraryManageBtn") as Button;
            if (libraryManageBtn != null)
            {
                libraryManageBtn.Click += (s, e) => LibraryManageClick?.Invoke(s, e);
            }



            // TagPanel 事件
            // Tag panel initialization - Phase 2 restored
            var tagBrowsePanel = TagBrowsePanelControl;
            if (tagBrowsePanel != null)
            {
                // Proxy TagClicked event
                // tagBrowsePanel.TagClicked += (id, name) => TagBrowsePanelTagClicked?.Invoke(id, name);
                // tagBrowsePanel.ManagementRequested += (s, e) => TagBrowsePanelCategoryManagementRequested?.Invoke();
            }

            // Tag edit panel initialization removed - Phase 2
            // var tagEditPanel = TagEditPanelControl;
            // if (tagEditPanel != null) {...}
        }

        // 公共属性访问器（通过FindName获取，避免命名冲突）
        public TreeView DrivesTreeViewControl => FindName("DrivesTreeView") as TreeView;
        // Obsolete: public ListBox DrivesListBoxControl => FindName("DrivesListBox") as ListBox;
        public ListBox QuickAccessListBoxControl => FindName("QuickAccessListBox") as ListBox;
        public ListBox FavoritesListBoxControl => FindName("FavoritesListBox") as ListBox;
        public ListBox LibrariesListBoxControl => FindName("LibrariesListBox") as ListBox;
        public ContextMenu LibraryContextMenuControl => FindName("LibraryContextMenu") as ContextMenu;
        public Grid NavPathContentControl => FindName("NavPathContent") as Grid;
        public Grid NavLibraryContentControl => FindName("NavLibraryContent") as Grid;
        public StackPanel LibraryBottomButtonsControl => FindName("LibraryBottomButtons") as StackPanel;

        // Tag Panel
        public TagBrowsePanel TagBrowsePanelControl => FindName("TagBrowsePanelElement") as TagBrowsePanel;
        public Grid NavTagContentControl => FindName("NavTagContent") as Grid;


    }
}

