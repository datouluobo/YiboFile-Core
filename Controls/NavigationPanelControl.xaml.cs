using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.Services.Config;
using YiboFile.Models;
using YiboFile.ViewModels.Messaging.Messages;
// using TagTrain.UI; // Phase 2

namespace YiboFile.Controls
{
    /// <summary>
    /// 导航面板控件
    /// 包含路径导航、库导航、标签导航三个面板
    /// </summary>
    public partial class NavigationPanelControl : UserControl
    {
        // Dependency Properties
        public static readonly DependencyProperty CurrentPathProperty =
            DependencyProperty.Register("CurrentPath", typeof(string), typeof(NavigationPanelControl), new PropertyMetadata(null));

        public string CurrentPath
        {
            get => (string)GetValue(CurrentPathProperty);
            set => SetValue(CurrentPathProperty, value);
        }

        // 事件定义
        public event MouseButtonEventHandler DrivesListBoxPreviewMouseDown;
        public event SelectionChangedEventHandler DrivesListBoxSelectionChanged;
        public event MouseButtonEventHandler QuickAccessListBoxPreviewMouseDown;
        public event SelectionChangedEventHandler QuickAccessListBoxSelectionChanged;
        public event MouseButtonEventHandler FolderFavoritesListBoxPreviewMouseDown;
        public event MouseButtonEventHandler FileFavoritesListBoxPreviewMouseDown;
        public event SelectionChangedEventHandler LibrariesListBoxSelectionChanged;
        public event ContextMenuEventHandler LibrariesListBoxContextMenuOpening;
        public event MouseButtonEventHandler LibrariesListBoxPreviewMouseDown;
        public event RoutedEventHandler AddFolderFavoriteClick;
        public event RoutedEventHandler AddFileFavoriteClick;
        public event RoutedEventHandler LibraryManageClick;
        public event RoutedEventHandler PathManageClick;

        public event Action<NavigationPanelControl, ListBox> FavoriteListBoxLoaded;
        public event Action<NavigationPanelControl, Grid> FavoriteGroupHeaderLoaded;
        public event Action<NavigationPanelControl, ListBox, MouseButtonEventArgs> FavoriteListBoxPreviewMouseDown;
        public event Action<NavigationPanelControl, ListBox, SelectionChangedEventArgs> FavoriteListBoxSelectionChanged;
        public event Action<object> RenameFavoriteGroupRequested;
        public event Action<object> DeleteFavoriteGroupRequested;
        public event RoutedEventHandler LibraryContextMenuClick;

        public event RoutedEventHandler DrivesTreeViewItemClick;

        // Dependency Properties for Commands
        public static readonly DependencyProperty NavigateCommandProperty =
            DependencyProperty.Register("NavigateCommand", typeof(ICommand), typeof(NavigationPanelControl), new PropertyMetadata(null));

        public ICommand NavigateCommand
        {
            get => (ICommand)GetValue(NavigateCommandProperty);
            set => SetValue(NavigateCommandProperty, value);
        }

        public static readonly DependencyProperty NavigateLibraryCommandProperty =
            DependencyProperty.Register("NavigateLibraryCommand", typeof(ICommand), typeof(NavigationPanelControl), new PropertyMetadata(null));

        public ICommand NavigateLibraryCommand
        {
            get => (ICommand)GetValue(NavigateLibraryCommandProperty);
            set => SetValue(NavigateLibraryCommandProperty, value);
        }

        public static readonly DependencyProperty OpenInNewTabCommandProperty =
           DependencyProperty.Register("OpenInNewTabCommand", typeof(ICommand), typeof(NavigationPanelControl), new PropertyMetadata(null));

        public ICommand OpenInNewTabCommand
        {
            get => (ICommand)GetValue(OpenInNewTabCommandProperty);
            set => SetValue(OpenInNewTabCommandProperty, value);
        }



        public NavigationPanelControl()
        {
            InitializeComponent();
            this.Loaded += NavigationPanelControl_Loaded;
        }

        private void NavigationPanelControl_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeEvents();
            UpdateSectionOrder();

            var messageBus = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<ViewModels.Messaging.IMessageBus>(App.ServiceProvider);
            messageBus?.Subscribe<ConfigurationSettingChangedMessage>(msg => OnSettingChanged(msg.SettingName));

            var taskQueueService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<YiboFile.Services.FileOperations.TaskQueue.TaskQueueService>(App.ServiceProvider);
            if (taskQueueService != null)
            {
                NavTaskQueue.SetService(taskQueueService);
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(AppConfig.NavigationSectionsOrder) || propertyName == "All")
            {
                Dispatcher.Invoke(() => UpdateSectionOrder());
            }
        }

        private void UpdateSectionOrder()
        {
            var order = ConfigurationService.Instance.Get(c => c.NavigationSectionsOrder);
            if (order == null || order.Count == 0) return;

            var drivesExpander = FindName("DrivesExpander") as UIElement;
            var quickAccessExpander = FindName("QuickAccessExpander") as UIElement;
            var favoritesGroups = FavoritesGroupsControl as UIElement;
            var navSectionsPanel = NavSectionsPanel;

            if (navSectionsPanel == null) return;

            var fixedControls = new Dictionary<string, UIElement>
            {
                { "Drives", drivesExpander },
                { "QuickAccess", quickAccessExpander }
            };

            navSectionsPanel.Children.Clear();

            foreach (var key in order)
            {
                if (fixedControls.TryGetValue(key, out var control) && control != null)
                {
                    navSectionsPanel.Children.Add(control);
                    fixedControls.Remove(key);
                }
                else if (key == "Favorites" || key == "FolderFavorites" || key == "FileFavorites" || key.StartsWith("FavoriteGroup_"))
                {
                    if (favoritesGroups != null && !navSectionsPanel.Children.Contains(favoritesGroups))
                    {
                        navSectionsPanel.Children.Add(favoritesGroups);
                    }
                }
            }

            foreach (var ctrl in fixedControls.Values)
            {
                if (ctrl != null && !navSectionsPanel.Children.Contains(ctrl))
                    navSectionsPanel.Children.Add(ctrl);
            }
            if (favoritesGroups != null && !navSectionsPanel.Children.Contains(favoritesGroups))
                navSectionsPanel.Children.Add(favoritesGroups);

            LoadExpanderStates();
        }

        private void LoadExpanderStates()
        {
            var states = ConfigurationService.Instance.Config.SidebarExpanderStates;
            if (states == null) return;

            void Restore(string name)
            {
                if (FindName(name) is Expander exp && states.TryGetValue(name, out bool expanded))
                {
                    exp.IsExpanded = expanded;
                }
            }

            Restore("QuickAccessExpander");
            Restore("DrivesExpander");
            // Add listeners to auto-save
            AttachSaveListener("QuickAccessExpander");
            AttachSaveListener("DrivesExpander");
        }

        private void AttachSaveListener(string name)
        {
            if (FindName(name) is Expander exp)
            {
                // Remove first to avoid double subscription
                exp.Expanded -= Expander_StateChanged;
                exp.Collapsed -= Expander_StateChanged;
                exp.Expanded += Expander_StateChanged;
                exp.Collapsed += Expander_StateChanged;
            }
        }

        private void Expander_StateChanged(object sender, RoutedEventArgs e)
        {
            if (sender is Expander exp && !string.IsNullOrEmpty(exp.Name))
            {
                var config = ConfigurationService.Instance.Config;
                if (config.SidebarExpanderStates == null) config.SidebarExpanderStates = new Dictionary<string, bool>();

                config.SidebarExpanderStates[exp.Name] = exp.IsExpanded;
                // Delay save handled by ConfigurationService
                ConfigurationService.Instance.Set(c => c.SidebarExpanderStates, config.SidebarExpanderStates);
            }
        }

        // Public Property for XAML access
        // public StackPanel NavSectionsPanelControl => FindName("NavSectionsPanel") as StackPanel;

        // Handler for TreeViewItem PreviewMouseDown (defined in Style EventSetter)
        private void DrivesTreeViewItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
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

            // Command Support
            if (sender is TreeViewItem tvi &&
                tvi.DataContext is YiboFile.Services.Navigation.NavigationItem item &&
                !string.IsNullOrEmpty(item.Path))
            {
                if (e.ChangedButton == MouseButton.Left && NavigateCommand != null)
                {
                    if (NavigateCommand.CanExecute(item.Path))
                        NavigateCommand.Execute(item.Path);
                }
                else if (e.ChangedButton == MouseButton.Middle && OpenInNewTabCommand != null)
                {
                    if (OpenInNewTabCommand.CanExecute(item.Path))
                        OpenInNewTabCommand.Execute(item.Path);
                    e.Handled = true;
                }
            }

            if (!e.Handled && e.ChangedButton == MouseButton.Left)
            {
                // Handle Single Click - Navigate Immediately
                // This ensures maximum responsiveness.
                DrivesTreeViewItemClick?.Invoke(sender, e);
            }
        }

        // Handler for TreeViewItem.Expanded event (for accordion behavior)
        private void DrivesTreeView_Expanded(object sender, RoutedEventArgs e)
        {
            // Accordion Logic: When a root item is expanded, collapse others
            // This method will contain the logic to collapse other TreeViewItems
            // when one is expanded.
            if (e.OriginalSource is TreeViewItem expandedItem)
            {
                // [Optimization] We can optionally handle it here for root items
                // But do NOT set e.Handled = true for sub-items or even root items unless absolutely necessary
                // as it can break some WPF internal TreeView behaviors.

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
                drivesTreeView.SelectedItemChanged += (s, e) =>
                {
                    if (NavigateCommand != null && s is TreeView tv && tv.SelectedItem is YiboFile.Services.Navigation.NavigationItem item && !string.IsNullOrEmpty(item.Path))
                    {
                        if (NavigateCommand.CanExecute(item.Path)) NavigateCommand.Execute(item.Path);
                    }
                    DrivesListBoxSelectionChanged?.Invoke(s, null);
                };
                drivesTreeView.PreviewMouseWheel += OnPreviewMouseWheel;

                // Accordion Logic: When a root item is expanded, collapse others
                drivesTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(DrivesTreeView_Expanded));
            }

            var quickAccessListBox = QuickAccessListBoxControl;
            if (quickAccessListBox != null)
            {
                quickAccessListBox.PreviewMouseDown += QuickAccessListBox_PreviewMouseDown;

                quickAccessListBox.SelectionChanged += (s, e) =>
                {
                    // SelectionChanged 仍然保留作为兜底，但主要由 PreviewMouseDown 处理多点击
                    QuickAccessListBoxSelectionChanged?.Invoke(s, e);
                };
                quickAccessListBox.PreviewMouseWheel += OnPreviewMouseWheel;
            }

            var folderFavoritesListBox = FolderFavoritesListBoxControl;
            if (folderFavoritesListBox != null)
            {
                folderFavoritesListBox.PreviewMouseDown += (s, e) => FolderFavoritesListBoxPreviewMouseDown?.Invoke(s, e);
                folderFavoritesListBox.PreviewMouseWheel += OnPreviewMouseWheel;
            }

            var fileFavoritesListBox = FileFavoritesListBoxControl;
            if (fileFavoritesListBox != null)
            {
                fileFavoritesListBox.PreviewMouseDown += (s, e) => FileFavoritesListBoxPreviewMouseDown?.Invoke(s, e);
                fileFavoritesListBox.PreviewMouseWheel += OnPreviewMouseWheel;
            }

            // 库导航事件
            var librariesListBox = LibrariesListBoxControl;
            if (librariesListBox != null)
            {
                librariesListBox.PreviewMouseDown += (s, e) =>
                {
                    if (e.Handled) return;

                    if (e.ChangedButton == MouseButton.Middle && OpenInNewTabCommand != null)
                    {
                        DependencyObject current = e.OriginalSource as DependencyObject;
                        while (current != null && !(current is ListBoxItem) && current != librariesListBox)
                        {
                            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                        }

                        if (current is ListBoxItem listboxItem && listboxItem.DataContext is YiboFile.Library lib)
                        {
                            OpenInNewTabCommand.Execute($"lib://{lib.Name}");
                            e.Handled = true;
                        }
                        return;
                    }

                    // 左键点击支持
                    if (e.ChangedButton == MouseButton.Left && NavigateCommand != null)
                    {
                        var element = e.OriginalSource as FrameworkElement;
                        var item = element?.DataContext as YiboFile.Library;
                        DependencyObject current = e.OriginalSource as DependencyObject;
                        while (current != null && current != librariesListBox)
                        {
                            if (current is ListBoxItem)
                            {
                                if (item != null)
                                {
                                    var path = $"lib://{item.Name}";
                                    if (NavigateCommand.CanExecute(path))
                                    {
                                        NavigateCommand.Execute(path);
                                        e.Handled = true;
                                    }
                                }
                                break;
                            }
                            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                        }
                    }

                    if (!e.Handled) LibrariesListBoxPreviewMouseDown?.Invoke(s, e);
                };

                librariesListBox.SelectionChanged += (s, e) =>
                {
                    // 已在 PreviewMouseDown 中处理多点击，此处仅作为兜底和事件透传
                    if (NavigateLibraryCommand != null && s is ListBox lb && lb.SelectedItem is YiboFile.Library lib)
                    {
                        // 这是一个备用方案
                    }
                    LibrariesListBoxSelectionChanged?.Invoke(s, e);
                };
                librariesListBox.ContextMenuOpening += (s, e) => LibrariesListBoxContextMenuOpening?.Invoke(s, e);
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
            var addFolderFavoriteButton = FindName("AddFolderFavoriteButton") as Button;
            if (addFolderFavoriteButton != null)
            {
                addFolderFavoriteButton.Click += (s, e) => AddFolderFavoriteClick?.Invoke(s, e);
            }

            var addFileFavoriteButton = FindName("AddFileFavoriteButton") as Button;
            if (addFileFavoriteButton != null)
            {
                addFileFavoriteButton.Click += (s, e) => AddFileFavoriteClick?.Invoke(s, e);
            }


            var libraryManageBtn = FindName("LibraryManageBtn") as Button;
            if (libraryManageBtn != null)
            {
                libraryManageBtn.Click += (s, e) => LibraryManageClick?.Invoke(s, e);
            }

            var pathManageBtn = FindName("PathManageBtn") as Button;
            if (pathManageBtn != null)
            {
                pathManageBtn.Click += (s, e) => PathManageClick?.Invoke(s, e);
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
        public ListBox FolderFavoritesListBoxControl => FindName("FolderFavoritesListBox") as ListBox;
        public ListBox FileFavoritesListBoxControl => FindName("FileFavoritesListBox") as ListBox;
        public ListBox LibrariesListBoxControl => FindName("LibrariesListBox") as ListBox;
        public ContextMenu LibraryContextMenuControl => FindName("LibraryContextMenu") as ContextMenu;
        public Grid NavPathContentControl => FindName("NavPathContent") as Grid;
        public Grid NavLibraryContentControl => FindName("NavLibraryContent") as Grid;

        public ItemsControl FavoritesGroupsControl => FindName("FavoritesGroupsItemsControl") as ItemsControl;

        // Tag Panel
        public TagBrowsePanel TagBrowsePanelControl => FindName("TagBrowsePanelElement") as TagBrowsePanel;
        public Grid NavTagContentControl => FindName("NavTagContent") as Grid;

        private void FavoriteListBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                FavoriteListBoxLoaded?.Invoke(this, listBox);
                // Fix BUG-019: Remove SelectionChanged to prevent duplicate navigation
                // listBox.SelectionChanged += FavoritesListBox_SelectionChanged;
                listBox.PreviewMouseDown += FavoritesListBox_PreviewMouseDown;
            }
        }

        private void FavoriteGroupHeader_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Grid grid)
            {
                FavoriteGroupHeaderLoaded?.Invoke(this, grid);
            }
        }

        private string GetPathFromDataContext(object dataContext)
        {
            if (dataContext == null) return null;

            // Try casting to NavigationItem first
            if (dataContext is YiboFile.Services.Navigation.NavigationItem navItem)
                return navItem.Path;

            // Reflection fallback for FavoriteItem or other types
            try
            {
                var type = dataContext.GetType();
                var pathProp = type.GetProperty("Path");
                if (pathProp != null)
                    return pathProp.GetValue(dataContext) as string;
            }
            catch { }

            return null;
        }

        private void FavoritesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.Handled) return;

            var element = e.OriginalSource as FrameworkElement;
            var dataContext = element?.DataContext;

            // Traverse up to find ListBoxItem if needed (for accurate DataContext)
            DependencyObject current = element;
            while (current != null && !(current is ListBoxItem) && current != sender as DependencyObject)
            {
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            if (current is ListBoxItem itemContainer)
                dataContext = itemContainer.DataContext;

            var path = GetPathFromDataContext(dataContext);

            if (!string.IsNullOrEmpty(path))
            {
                // Check if it's a file (not directory)
                bool isDirectory = true;
                if (dataContext != null)
                {
                    var favProp = dataContext.GetType().GetProperty("Favorite");
                    if (favProp != null)
                    {
                        var fav = favProp.GetValue(dataContext);
                        var isDirProp = fav?.GetType().GetProperty("IsDirectory");
                        if (isDirProp != null)
                        {
                            isDirectory = (bool)isDirProp.GetValue(fav);
                        }
                    }
                }

                if (e.ChangedButton == MouseButton.Left)
                {
                    if (isDirectory && NavigateCommand != null)
                    {
                        NavigateCommand.Execute(path);
                    }
                    else if (!isDirectory)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                        }
                        catch { }
                    }
                    e.Handled = true; // Prevent bubbling and double handling
                }
                else if (e.ChangedButton == MouseButton.Middle && OpenInNewTabCommand != null && isDirectory)
                {
                    OpenInNewTabCommand.Execute(path);
                    e.Handled = true;
                }
            }

            // Trigger global event if needed (keeping compatibility)
            if (!e.Handled) FavoriteListBoxPreviewMouseDown?.Invoke(this, sender as ListBox, e);
        }

        // Quick Access logic updated to prevent simultaneous navigation
        private void QuickAccessListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationPanelControl] QuickAccessListBox_PreviewMouseDown fired. Sender: {sender}, Source: {e.Source}, OriginalSource: {e.OriginalSource}");

            // ... (rest of logic)
            // Traverse to item
            var element = e.OriginalSource as FrameworkElement;
            DependencyObject current = element;
            while (current != null && !(current is ListBoxItem) && current != sender as DependencyObject)
            {
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }

            if (current is ListBoxItem itemContainer)
            {
                var path = GetPathFromDataContext(itemContainer.DataContext);
                if (!string.IsNullOrEmpty(path))
                {
                    if (e.ChangedButton == MouseButton.Left && NavigateCommand != null)
                    {
                        NavigateCommand.Execute(path);
                        e.Handled = true; // Prevent SelectionChanged and double nav
                    }
                    else if (e.ChangedButton == MouseButton.Middle && OpenInNewTabCommand != null)
                    {
                        OpenInNewTabCommand.Execute(path);
                        e.Handled = true;
                    }
                }
            }

            if (!e.Handled) QuickAccessListBoxPreviewMouseDown?.Invoke(sender, e);
        }

        private void FavoritesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox)
            {
                // Command Support - Removed to prevent double navigation with PreviewMouseDown
                // if (NavigateCommand != null && listBox.SelectedItem is YiboFile.Services.Navigation.NavigationItem item && !string.IsNullOrEmpty(item.Path))
                // {
                //     if (NavigateCommand.CanExecute(item.Path))
                //         NavigateCommand.Execute(item.Path);
                // }

                FavoriteListBoxSelectionChanged?.Invoke(this, listBox, e);
            }
        }
        private void RenameFavoriteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                RenameFavoriteGroupRequested?.Invoke(mi.Tag);
            }
        }

        private void DeleteFavoriteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi)
            {
                DeleteFavoriteGroupRequested?.Invoke(mi.Tag);
            }
        }
    }
}

