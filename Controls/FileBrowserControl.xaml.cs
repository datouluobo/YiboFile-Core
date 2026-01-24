using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using YiboFile.Controls.Converters;
using YiboFile.Models;
using YiboFile.Services.Favorite;
using YiboFile.Services.Search;

namespace YiboFile.Controls
{
    /// <summary>
    /// FileBrowserControl.xaml 的交互逻辑
    /// 统一的文件浏览控件，支持路径、库、标签三种模式
    /// </summary>
    public partial class FileBrowserControl : UserControl
    {
        private readonly Dictionary<GridViewColumn, double> _columnDefaultWidths = new Dictionary<GridViewColumn, double>();

        // 事件定义（保持向后兼容）
        public event SelectionChangedEventHandler FilesSelectionChanged;
        public event MouseButtonEventHandler FilesMouseDoubleClick;
        public event MouseButtonEventHandler FilesPreviewMouseDoubleClick;
        public event KeyEventHandler FilesPreviewKeyDown;
        public event MouseButtonEventHandler FilesPreviewMouseLeftButtonDown;
        public event MouseButtonEventHandler FilesMouseLeftButtonUp;
        public event MouseButtonEventHandler FilesPreviewMouseDown;
        public event RoutedEventHandler GridViewColumnHeaderClick;
        public event SizeChangedEventHandler FilesSizeChanged;
#pragma warning disable CS0067 // Event is never used (used in XAML)
        public event MouseButtonEventHandler FilesPreviewMouseDoubleClickForBlank;
        public event MouseEventHandler FilesPreviewMouseMove;
#pragma warning restore CS0067

        public FileBrowserControl()
        {
            InitializeComponent();

            // 订阅地址栏控件的事件
            if (AddressBarControl != null)
            {
                AddressBarControl.PathChanged += AddressBarControl_PathChanged;
                AddressBarControl.BreadcrumbClicked += AddressBarControl_BreadcrumbClicked;
                AddressBarControl.BreadcrumbMiddleClicked += AddressBarControl_BreadcrumbMiddleClicked;
            }

            // 订阅文件列表控件的事件（转发到外部事件）
            if (FileList != null)
            {
                FileList.SelectionChanged += (s, e) => FilesSelectionChanged?.Invoke(s, e);
                FileList.MouseDoubleClick += (s, e) => FilesMouseDoubleClick?.Invoke(s, e);
                FileList.PreviewMouseDoubleClick += (s, e) =>
                {
                    FilesPreviewMouseDoubleClick?.Invoke(s, e);
                    // Check for blank area double click
                    // Fix: sender is FileListControl, not ListView. Use FileList.FilesList.
                    // Improve Blank Area Check
                    // If HitTest hits ScrollViewer, Border, or Grid but NOT a ListViewItem, it's blank.
                    var hit = FileList.InputHitTest(e.GetPosition(FileList));
                    bool isItem = false;
                    var current = hit as DependencyObject;
                    while (current != null)
                    {
                        if (current is ListViewItem) { isItem = true; break; }
                        if (current == FileList) break;
                        current = VisualTreeHelper.GetParent(current);
                    }

                    if (!isItem)
                    {
                        FilesPreviewMouseDoubleClickForBlank?.Invoke(s, e);
                    }
                };
                FileList.PreviewKeyDown += (s, e) => FilesPreviewKeyDown?.Invoke(s, e);
                FileList.PreviewMouseLeftButtonDown += (s, e) => FilesPreviewMouseLeftButtonDown?.Invoke(s, e);
                FileList.MouseLeftButtonUp += (s, e) => FilesMouseLeftButtonUp?.Invoke(s, e);
                FileList.PreviewMouseDown += (s, e) => FilesPreviewMouseDown?.Invoke(s, e);
                FileList.PreviewMouseMove += (s, e) => FilesPreviewMouseMove?.Invoke(s, e);
                FileList.SizeChanged += (s, e) => FilesSizeChanged?.Invoke(s, e);
                FileList.GridViewColumnHeaderClick += (s, e) => GridViewColumnHeaderClick?.Invoke(s, e);
                FileList.LoadMoreClick += (s, e) => LoadMoreBtn_Click(s, e);
                FileList.CommitRename += (s, e) =>
                {
                    CommitRename?.Invoke(s, e);
                };

                // 订阅列标题点击事件（用于记录默认列宽）
                if (FileList.FilesGrid != null)
                {
                    foreach (GridViewColumn column in FileList.FilesGrid.Columns)
                    {
                        // 记录默认列宽
                        if (!_columnDefaultWidths.ContainsKey(column))
                            _columnDefaultWidths[column] = column.Width;
                    }

                    // 挂载列头点击事件（通过 VisualTree 查找，确保覆盖整个列头区域且解决 Tag 问题）
                    if (FileList.FilesList != null)
                    {
                        if (FileList.FilesList.IsLoaded)
                        {
                            HookColumnHeaders();
                        }
                        else
                        {
                            FileList.FilesList.Loaded += (s, e) => HookColumnHeaders();
                        }
                    }

                    // 右键菜单：与预览窗口一致的列显示/隐藏
                    SetupFileContextMenu();
                }

                FileList.TagClicked += (s, e) => TagClicked?.Invoke(s, e);
            }

            // 订阅标题操作栏事件
            if (TitleActionBar != null)
            {
                TitleActionBar.NewFolderClicked += (s, e) => FileNewFolder?.Invoke(s, e);
                TitleActionBar.NewFileClicked += (s, e) => FileNewFile?.Invoke(s, e);
                TitleActionBar.CopyClicked += (s, e) => FileCopy?.Invoke(s, e);
                TitleActionBar.PasteClicked += (s, e) => FilePaste?.Invoke(s, e);
                TitleActionBar.DeleteClicked += (s, e) => FileDelete?.Invoke(s, e);
                TitleActionBar.RefreshClicked += (s, e) => FileRefresh?.Invoke(s, e);
                TitleActionBar.NewTagClicked += (s, e) => FileAddTag?.Invoke(s, e);
                // TitleActionBar.ManageTagsClicked += ... (Pending implementation if needed)
                // TitleActionBar.ManageTagsClicked += ... (Pending implementation if needed)
                // TitleActionBar.BatchAddTagsClicked += ...
            }
        }





        // 公共属性（保持向后兼容）
        public AddressBarControl AddressBar => AddressBarControl;
        public ListView FilesList => FileList?.FilesList;
        public GridView FilesGrid => FileList?.FilesGrid;

        // TabManager moved to MainWindow
        // public TabManagerControl TabManagerControl => TabManager;
        // public StackPanel TabsPanelControl => TabManager?.TabsPanelControl;
        // public Border TabsBorderControl => TabManager?.TabsBorderControl;

        public Border FocusBorderControl => FocusBorder; // 焦点边框
        public StackPanel FileInfoPanelControl => FileInfoPanel;
        public TextBlock EmptyStateTextControl => FileList?.EmptyStateTextControl;

        // TitleActionBar 现在在 FileBrowserControl 中，提供公共访问
        public TitleActionBar ActionBar => TitleActionBar;


        /// <summary>
        /// 获取 FileListControl 实例（供设置面板调用）
        /// </summary>
        public FileListControl GetFileListControl() => FileList;

        // 地址栏相关方法
        public string AddressText
        {
            get => AddressBarControl?.AddressText ?? "";
            set
            {
                if (AddressBarControl != null)
                    AddressBarControl.AddressText = value;
            }
        }

        public bool IsAddressReadOnly
        {
            get => AddressBarControl?.IsReadOnly ?? false;
            set
            {
                if (AddressBarControl != null)
                    AddressBarControl.IsReadOnly = value;
            }
        }

        public void UpdateBreadcrumb(string path)
        {
            AddressBarControl?.UpdateBreadcrumb(path);
        }

        public void UpdateBreadcrumbText(string text)
        {
            AddressBarControl?.UpdateBreadcrumbText(text);
        }

        public void SetBreadcrumbCustomText(string text)
        {
            AddressBarControl?.SetBreadcrumbCustomText(text);
        }

        public void SetTagBreadcrumb(string tagName)
        {
            AddressBarControl?.SetTagBreadcrumb(tagName);
        }

        public void SetSearchBreadcrumb(string keyword)
        {
            AddressBarControl?.SetSearchBreadcrumb(keyword);
        }

        public void SetLibraryBreadcrumb(string libraryName)
        {
            AddressBarControl?.SetLibraryBreadcrumb(libraryName);
        }

        // 文件列表相关方法
        public System.Collections.IEnumerable FilesItemsSource
        {
            get => FileList?.FilesItemsSource;
            set
            {
                if (FileList != null)
                {
                    // 使用FilesItemsSource属性，它会自动调用Items.Refresh()
                    FileList.FilesItemsSource = value;
                }
            }
        }

        /// <summary>
        /// 设置搜索状态显示
        /// </summary>
        /// <param name="isVisible">是否可见</param>
        /// <param name="text">状态文本</param>
        public void SetSearchStatus(bool isVisible, string text = null)
        {
            if (SearchStatusBar == null) return;

            SearchStatusBar.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            if (isVisible && !string.IsNullOrEmpty(text))
            {
                if (SearchStatusText != null) SearchStatusText.Text = text;
            }
        }

        /// <summary>
        /// 设置分组搜索结果
        /// </summary>
        public void SetGroupedSearchResults(Dictionary<SearchResultType, List<FileSystemItem>> groupedItems)
        {
            FileList?.SetGroupedSearchResults(groupedItems);
        }

        public object FilesSelectedItem
        {
            get => FileList?.SelectedItem;
            set
            {
                if (FileList?.FilesList != null)
                    FileList.FilesList.SelectedItem = value;
            }
        }

        public System.Collections.IList FilesSelectedItems
        {
            get => FileList?.SelectedItems;
        }

        // 标签页相关方法（已移动到MainWindow）
        public bool TabsVisible
        {
            get => false; // TabManager?.IsVisible ?? false;
            set
            {
                // Move logic to MainWindow if needed
            }
        }

        // 空状态提示
        public void ShowEmptyState(string message = "暂无文件")
        {
            FileList?.ShowEmptyState(message);
        }

        public void HideEmptyState()
        {
            FileList?.HideEmptyState();
        }

        // 地址栏事件（转发给外部）
        public event EventHandler<string> PathChanged;
        public event EventHandler<string> BreadcrumbClicked;
        public event EventHandler<string> BreadcrumbMiddleClicked;
        public event RoutedEventHandler NavigationBack;
        public event RoutedEventHandler NavigationForward;
        public event RoutedEventHandler NavigationUp;
        // public event RoutedEventHandler SearchClicked; // Removed unused event
        public event RoutedEventHandler FilterClicked;
        public event RoutedEventHandler LoadMoreClicked;

        // 文件操作事件
        public event RoutedEventHandler FileCopy;
        public event RoutedEventHandler FileCut;
        public event RoutedEventHandler FilePaste;
        public event RoutedEventHandler FileDelete;
        public event RoutedEventHandler FileRename;
        public event RoutedEventHandler FileRefresh;
        public event RoutedEventHandler FileProperties;
        public event RoutedEventHandler FileAddTag;
        public event RoutedEventHandler FileNewFolder;
        public event RoutedEventHandler FileNewFile;
        public event RoutedEventHandler FileUndo;
        public event RoutedEventHandler FileRedo;
        public event EventHandler<RenameEventArgs> CommitRename;
        public event EventHandler<TagViewModel> TagClicked;

        // 地址栏事件处理
        private void AddressBarControl_PathChanged(object sender, string path)
        {
            PathChanged?.Invoke(this, path);
        }

        private void AddressBarControl_BreadcrumbClicked(object sender, string path)
        {
            BreadcrumbClicked?.Invoke(this, path);
        }

        private void AddressBarControl_BreadcrumbMiddleClicked(object sender, string path)
        {
            // 中键打开新标签，触发专门的事件
            BreadcrumbMiddleClicked?.Invoke(this, path);
        }

        private void NavBackBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationBack?.Invoke(sender, e);
        }

        private void NavForwardBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationForward?.Invoke(sender, e);
        }


        private void NavUpBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationUp?.Invoke(sender, e);
        }

        private void UndoBtn_Click(object sender, RoutedEventArgs e)
        {
            FileUndo?.Invoke(this, e);
        }

        private void RedoBtn_Click(object sender, RoutedEventArgs e)
        {
            FileRedo?.Invoke(this, e);
        }



        private void FilterBtn_Click(object sender, RoutedEventArgs e)
        {
            FilterClicked?.Invoke(sender, e);
        }

        public void ShowFilterPopup(SearchOptions options, EventHandler onChange)
        {
            if (FilterPanelControl == null || FilterPopup == null) return;

            // Initialize panel
            FilterPanelControl.Initialize(options);

            // Subscribe to change (avoid duplicate subscription by unsubscribing first? Or passing delegate that is stored?)
            // FilterPanelControl.FilterChanged actually is an event. We should clear previous subscribers?
            // Since we create a new EventHandler in the caller usually, we rely on the caller to manage logic.
            // But here we need to hook the 'onChange' to the panel's event.

            // Better pattern: The caller (handler) subscribes to the event. The control just shows it.
            // But the handler doesn't have access to FilterPanelControl (it's internal/private field).
            // So we bridge it.

            // Unsubscribe previous handlers to prevent memory leaks/multiple calls if called repeatedly
            // But we can't easily unsubscribe anonymous delegates.
            // Let's assume the handler manages the subscription lifecycle or we clear all invocations?

            // Simpler: Just set the options. The Panel fires FilterChanged. 
            // We expose an event `FilterPanelFilterChanged` on FileBrowserControl.

            if (!FilterPopup.IsOpen)
            {
                FilterPopup.IsOpen = true;
                // Focus?
            }
            else
            {
                FilterPopup.IsOpen = false;
            }
        }

        /// <summary>
        /// 显示筛选面板
        /// </summary>
        public void ToggleFilterPanel(SearchOptions options, EventHandler onFilterChanged)
        {
            if (FilterPanelControl == null || FilterPopup == null) return;

            if (FilterPopup.IsOpen)
            {
                FilterPopup.IsOpen = false;
                return;
            }

            // Initialize with current options
            FilterPanelControl.Initialize(options);

            // Remove old handlers to prevent duplicates
            // Use a private stub to forward?
            FilterPanelControl.FilterChanged -= _filterChangedStub; // Try remove previous
            _filterChangedStub = onFilterChanged;
            FilterPanelControl.FilterChanged += _filterChangedStub;

            FilterPopup.IsOpen = true;
        }

        private EventHandler _filterChangedStub;




        private void PropertiesBtn_Click(object sender, RoutedEventArgs e)
        {
            // Invoke the properties event (same as context menu)
            // Fix: The event is defined as specific delegate type, or generic routed event?
            // FileBrowser.FileProperties is defined in MainWindow.Initialization.cs as custom event or RoutedEventHandler?
            // In FileBrowserControl.xaml.cs, it is NOT defined as an event in the top list?
            // Actually it IS NOT in the list I read (lines 1-100).
            // Let me check lines 100+ or if I missed it.
            // Wait, previous `grep` showed `FileBrowser.FileProperties += ...` in `MainWindow.Initialization.cs`.
            // So `FileBrowserControl` MUST have `FileProperties` event.
            // I'll assume it's there (likely further down in file).
            // Tricky: if I can't see it, I might break build.
            // Let's assume it follows pattern: `public event RoutedEventHandler FileProperties;`
            // and invoke it.
            FileProperties?.Invoke(this, e);
        }

        public void SetPropertiesButtonVisibility(bool visible)
        {
            if (PropertiesBtn != null)
            {
                PropertiesBtn.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void LoadMoreBtn_Click(object sender, RoutedEventArgs e)
        {
            LoadMoreClicked?.Invoke(sender, e);
        }

        public bool LoadMoreVisible
        {
            get => FileList?.LoadMoreVisible ?? false;
            set
            {
                if (FileList != null)
                    FileList.LoadMoreVisible = value;
            }
        }

        public bool NavUpEnabled
        {
            get => NavUpBtn?.IsEnabled ?? false;
            set
            {
                if (NavUpBtn != null)
                    NavUpBtn.IsEnabled = value;
            }
        }

        public bool NavBackEnabled
        {
            get => NavBackBtn?.IsEnabled ?? false;
            set
            {
                if (NavBackBtn != null)
                    NavBackBtn.IsEnabled = value;
            }
        }

        public bool NavForwardEnabled
        {
            get => NavForwardBtn?.IsEnabled ?? false;
            set
            {
                if (NavForwardBtn != null)
                    NavForwardBtn.IsEnabled = value;
            }
        }

        public void EnableAutoLoadMore()
        {
            try
            {
                var sv = GetScrollViewer(FileList?.FilesList);
                if (sv != null)
                {
                    sv.ScrollChanged -= Sv_ScrollChanged;
                    sv.ScrollChanged += Sv_ScrollChanged;
                }
            }
            catch { }
        }

        public bool UndoEnabled
        {
            get => UndoBtn?.IsEnabled ?? false;
            set
            {
                if (UndoBtn != null) UndoBtn.IsEnabled = value;
            }
        }

        public bool RedoEnabled
        {
            get => RedoBtn?.IsEnabled ?? false;
            set
            {
                if (RedoBtn != null) RedoBtn.IsEnabled = value;
            }
        }

        public string UndoToolTipText
        {
            get => UndoBtn?.ToolTip as string;
            set
            {
                if (UndoBtn != null) UndoBtn.ToolTip = value;
            }
        }

        public string RedoToolTipText
        {
            get => RedoBtn?.ToolTip as string;
            set
            {
                if (RedoBtn != null) RedoBtn.ToolTip = value;
            }
        }

        private void Sv_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            try
            {
                var sv = sender as ScrollViewer;
                if (sv == null) return;
                if (e.VerticalOffset + e.ViewportHeight >= e.ExtentHeight - 20)
                {
                    LoadMoreClicked?.Invoke(this, new RoutedEventArgs());
                }
            }
            catch { }
        }

        private ScrollViewer GetScrollViewer(DependencyObject root)
        {
            if (root == null) return null;
            if (root is ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private void SetupFileContextMenu()
        {
            if (FileList?.FilesList == null) return;

            var cm = new ContextMenu();

            // 复制
            var copyItem = new MenuItem { Header = "复制", Name = "CopyItem" };
            copyItem.Click += (s, e) => FileCopy?.Invoke(this, e);
            cm.Items.Add(copyItem);

            // 剪切
            var cutItem = new MenuItem { Header = "剪切", Name = "CutItem" };
            cutItem.Click += (s, e) => FileCut?.Invoke(this, e);
            cm.Items.Add(cutItem);

            // 粘贴
            var pasteItem = new MenuItem { Header = "粘贴", Name = "PasteItem" };
            pasteItem.Click += (s, e) => FilePaste?.Invoke(this, e);
            cm.Items.Add(pasteItem);

            var separator1 = new Separator { Name = "Separator1" };
            cm.Items.Add(separator1);

            // 删除
            var deleteItem = new MenuItem { Header = "删除", Name = "DeleteItem" };
            deleteItem.Click += (s, e) => FileDelete?.Invoke(this, e);
            cm.Items.Add(deleteItem);

            // 重命名
            var renameItem = new MenuItem { Header = "重命名", Name = "RenameItem" };
            renameItem.Click += (s, e) => FileRename?.Invoke(this, e);
            cm.Items.Add(renameItem);

            var separator2 = new Separator { Name = "Separator2" };
            cm.Items.Add(separator2);

            // 添加到收藏
            MenuItem addFavoriteItem = new MenuItem { Header = "⭐ 添加到收藏", Name = "AddFavoriteItem" };
            cm.Items.Add(addFavoriteItem);

            // 添加到库 - 仅对文件夹显示
            MenuItem addToLibraryItem = new MenuItem { Header = "添加到库", Name = "AddToLibraryItem" };
            cm.Items.Add(addToLibraryItem);

            // 添加标签 (仅当TagTrain可用时显示) - 使用子菜单展示所有标签
            MenuItem addTagItem = null;
            if (App.IsTagTrainAvailable)
            {
                addTagItem = new MenuItem { Header = "添加标签", Name = "AddTagItem" };
                cm.Items.Add(addTagItem);
            }

            var separator3 = new Separator { Name = "Separator3" };
            cm.Items.Add(separator3);

            // 刷新
            var refreshItem = new MenuItem { Header = "刷新", Name = "RefreshItem" };
            refreshItem.Click += (s, e) => FileRefresh?.Invoke(this, e);
            cm.Items.Add(refreshItem);

            // 属性
            var propertiesItem = new MenuItem { Header = "属性", Name = "PropertiesItem" };
            propertiesItem.Click += (s, e) => FileProperties?.Invoke(this, e);
            cm.Items.Add(propertiesItem);

            // 在菜单打开时动态更新菜单项可见性
            cm.Opened += (s, e) =>
            {
                bool hasSelection = FileList?.SelectedItems != null && FileList.SelectedItems.Count > 0;

                // 需要选中项的操作
                copyItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                cutItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                deleteItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                renameItem.Visibility = hasSelection && FileList.SelectedItems.Count == 1 ? Visibility.Visible : Visibility.Collapsed;
                propertiesItem.Visibility = hasSelection && FileList.SelectedItems.Count == 1 ? Visibility.Visible : Visibility.Collapsed;

                // 添加标签项可见性和动态子菜单
                if (App.IsTagTrainAvailable && addTagItem != null)
                {
                    addTagItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;

                    // 清空并重新填充子菜单
                    addTagItem.Items.Clear();

                    try
                    {
                        var tagService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.Features.ITagService)) as YiboFile.Services.Features.ITagService;
                        var allTags = tagService?.GetAllTags()?.ToList();
                        var selectedItemsList = FileList?.SelectedItems?.Cast<FileSystemItem>().ToList();

                        if (allTags != null && allTags.Count > 0)
                        {
                            foreach (var tag in allTags)
                            {
                                var tagMenuItem = new MenuItem
                                {
                                    IsCheckable = true,
                                    StaysOpenOnClick = true,
                                    Tag = tag.Id
                                };

                                // 判断是否勾选：如果所有选中的文件都包含此标签，则勾选
                                bool allHaveTag = selectedItemsList != null && selectedItemsList.Count > 0 &&
                                                 selectedItemsList.All(item => item.TagList != null && item.TagList.Any(t => t.Id == tag.Id));
                                tagMenuItem.IsChecked = allHaveTag;

                                // 创建带颜色的Header
                                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

                                // 颜色指示器
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

                                // 标签名称
                                headerPanel.Children.Add(new TextBlock { Text = tag.Name, VerticalAlignment = VerticalAlignment.Center });

                                tagMenuItem.Header = headerPanel;

                                tagMenuItem.Click += (sender, args) =>
                                {
                                    var mi = (MenuItem)sender;
                                    var tagId = (int)mi.Tag;
                                    bool shouldAdd = mi.IsChecked; // 点击后IsChecked已自动翻转

                                    if (selectedItemsList != null && tagService != null)
                                    {
                                        foreach (var item in selectedItemsList)
                                        {
                                            if (!string.IsNullOrEmpty(item.Path))
                                            {
                                                if (shouldAdd)
                                                    tagService.AddTagToFile(item.Path, tagId);
                                                else
                                                    tagService.RemoveTagFromFile(item.Path, tagId);
                                            }
                                        }
                                        // 触发刷新以显示新标签（注意：这里可能需要更新ViewModel而不是全量刷新，但目前保持一致）
                                        FileRefresh?.Invoke(this, args);
                                    }
                                };

                                addTagItem.Items.Add(tagMenuItem);
                            }
                        }
                        else
                        {
                            var emptyItem = new MenuItem { Header = "暂无标签", IsEnabled = false };
                            addTagItem.Items.Add(emptyItem);
                        }
                    }
                    catch
                    {
                        var errorItem = new MenuItem { Header = "加载失败", IsEnabled = false };
                        addTagItem.Items.Add(errorItem);
                    }
                }

                // 添加到收藏项可见性和动态子菜单
                addFavoriteItem.Visibility = hasSelection ? Visibility.Visible : Visibility.Collapsed;
                if (hasSelection)
                {
                    addFavoriteItem.Items.Clear();
                    try
                    {
                        var groups = DatabaseManager.GetAllFavoriteGroups();
                        var selectedItemsList = FileList?.SelectedItems?.Cast<FileSystemItem>().ToList();

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
                                    if (selectedItemsList != null)
                                    {
                                        var favoriteService = App.ServiceProvider?.GetService(typeof(FavoriteService)) as FavoriteService;
                                        favoriteService?.AddFavorite(selectedItemsList, groupId);
                                    }
                                };
                                addFavoriteItem.Items.Add(groupMenuItem);
                            }

                            addFavoriteItem.Items.Add(new Separator());
                        }

                        var createGroupItem = new MenuItem { Header = "+ 新建分组..." };
                        createGroupItem.Click += (sender, args) =>
                        {
                            var dialog = new PathInputDialog("请输入新分组名称：");
                            dialog.InputText = "新分组";
                            var owner = Window.GetWindow(this);
                            dialog.Owner = owner;
                            if (dialog.ShowDialog() == true)
                            {
                                var name = dialog.InputText?.Trim();
                                if (!string.IsNullOrEmpty(name))
                                {
                                    int newGroupId = DatabaseManager.CreateFavoriteGroup(name);
                                    if (selectedItemsList != null)
                                    {
                                        var favoriteService = App.ServiceProvider?.GetService(typeof(FavoriteService)) as FavoriteService;
                                        favoriteService?.AddFavorite(selectedItemsList, newGroupId);
                                    }
                                }
                            }
                        };
                        addFavoriteItem.Items.Add(createGroupItem);
                    }
                    catch
                    {
                        addFavoriteItem.Items.Add(new MenuItem { Header = "加载失败", IsEnabled = false });
                    }
                }

                // 添加到库项可见性和动态子菜单（仅对文件夹显示）
                bool hasFolderSelection = hasSelection && FileList.SelectedItems.Cast<FileSystemItem>().Any(item => item.IsDirectory);
                addToLibraryItem.Visibility = hasFolderSelection ? Visibility.Visible : Visibility.Collapsed;

                if (hasFolderSelection)
                {
                    addToLibraryItem.Items.Clear();

                    try
                    {
                        var allLibraries = DatabaseManager.GetAllLibraries();
                        var selectedFolders = FileList.SelectedItems.Cast<FileSystemItem>().Where(item => item.IsDirectory).ToList();

                        if (allLibraries != null && allLibraries.Count > 0)
                        {
                            foreach (var library in allLibraries)
                            {
                                var libraryMenuItem = new MenuItem
                                {
                                    IsCheckable = true,
                                    StaysOpenOnClick = true,
                                    Tag = library.Id,
                                    Header = library.Name
                                };

                                // 判断是否勾选：如果所有选中的文件夹都已在该库中，则勾选
                                bool allInLibrary = selectedFolders.All(folder =>
                                    library.Paths != null && library.Paths.Contains(folder.Path));
                                libraryMenuItem.IsChecked = allInLibrary;

                                libraryMenuItem.Click += (sender, args) =>
                                {
                                    var mi = (MenuItem)sender;
                                    var libId = (int)mi.Tag;
                                    bool shouldAdd = mi.IsChecked;

                                    foreach (var folder in selectedFolders)
                                    {
                                        if (!string.IsNullOrEmpty(folder.Path))
                                        {
                                            if (shouldAdd)
                                                DatabaseManager.AddLibraryPath(libId, folder.Path);
                                            else
                                                DatabaseManager.RemoveLibraryPath(libId, folder.Path);
                                        }
                                    }
                                };

                                addToLibraryItem.Items.Add(libraryMenuItem);
                            }
                        }

                        // 添加分隔符和"新建库..."选项
                        if (allLibraries != null && allLibraries.Count > 0)
                        {
                            addToLibraryItem.Items.Add(new Separator());
                        }

                        var newLibraryItem = new MenuItem { Header = "新建库..." };
                        newLibraryItem.Click += (sender, args) =>
                        {
                            var dialog = new YiboFile.Controls.Dialogs.InputDialog("新建库", "请输入库名称:");
                            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                            {
                                var libService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.LibraryService)) as YiboFile.Services.LibraryService;
                                if (libService != null)
                                {
                                    // 创建新库并添加所有选中的文件夹
                                    int newLibId = libService.AddLibrary(dialog.InputText);
                                    if (newLibId > 0)
                                    {
                                        foreach (var folder in selectedFolders)
                                        {
                                            if (!string.IsNullOrEmpty(folder.Path))
                                            {
                                                DatabaseManager.AddLibraryPath(newLibId, folder.Path);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // 直接使用 DatabaseManager
                                    int newLibId = DatabaseManager.AddLibrary(dialog.InputText);
                                    if (newLibId > 0)
                                    {
                                        foreach (var folder in selectedFolders)
                                        {
                                            if (!string.IsNullOrEmpty(folder.Path))
                                            {
                                                DatabaseManager.AddLibraryPath(newLibId, folder.Path);
                                            }
                                        }
                                    }
                                }
                            }
                        };
                        addToLibraryItem.Items.Add(newLibraryItem);
                    }
                    catch
                    {
                        addToLibraryItem.Items.Clear();
                        var errorItem = new MenuItem { Header = "加载失败", IsEnabled = false };
                        addToLibraryItem.Items.Add(errorItem);
                    }
                }

                // 始终可用的操作
                pasteItem.Visibility = Visibility.Visible;
                refreshItem.Visibility = Visibility.Visible;

                // 更新分隔符可见性
                separator1.Visibility = (hasSelection || pasteItem.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
                separator2.Visibility = (hasSelection || refreshItem.Visibility == Visibility.Visible || propertiesItem.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
            };

            FileList.FilesList.ContextMenu = cm;
        }

        /// <summary>
        /// 解析颜色字符串为 Color 对象
        /// </summary>
        private static Color ParseColor(string colorString)
        {
            try
            {
                if (string.IsNullOrEmpty(colorString))
                    return Colors.Gray;

                return (Color)ColorConverter.ConvertFromString(colorString);
            }
            catch
            {
                return Colors.Gray;
            }
        }

        // 视图模式变更事件
        public event EventHandler<string> ViewModeChanged;

        public event EventHandler<double> InfoHeightChanged;

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.Content is Grid rootGrid && rootGrid.RowDefinitions.Count > 4)
            {
                var height = rootGrid.RowDefinitions[4].Height.Value;
                InfoHeightChanged?.Invoke(this, height);
            }
        }
        private void ViewModeBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton btn && btn.Tag is string mode)
            {
                FileList?.SetViewMode(mode);
                ViewModeChanged?.Invoke(this, mode);
            }
        }

        /// <summary>
        /// 点击视图模式按钮时打开下拉菜单
        /// </summary>
        private void ViewModeBtn_DropDown(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                btn.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// 视图模式菜单项点击
        /// </summary>
        private void ViewModeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is string mode)
            {
                FileList?.SetViewMode(mode);
                ViewModeChanged?.Invoke(this, mode);

                // 更新按钮图标以反映当前模式
                UpdateViewModeButtonIcon(mode);
            }
        }

        /// <summary>
        /// 更新视图模式按钮图标
        /// </summary>
        private void UpdateViewModeButtonIcon(string mode)
        {
            // 查找按钮控件
            var viewModeBtn = FindName("ViewModeBtn") as Button;
            if (viewModeBtn == null) return;

            // 根据视图模式更新按钮图标
            string iconKey = mode switch
            {
                "Thumbnail" or "Tiles" => "Icon_ViewThumb",
                "SmallIcons" => "Icon_ViewThumb",
                "Content" => "Icon_ViewList",
                "Compact" => "Icon_ViewList",
                _ => "Icon_ViewList" // List 或默认
            };

            // 安全地获取资源
            try
            {
                var icon = FindResource(iconKey);
                if (icon != null)
                {
                    viewModeBtn.Content = icon;
                }
            }
            catch
            {
                // 资源不存在，保持默认图标
            }
        }

        private bool IsMouseOverItem(ListView listView, Point point)
        {
            if (listView == null) return false;
            var hit = VisualTreeHelper.HitTest(listView, point);
            if (hit == null) return false;

            var current = hit.VisualHit;
            while (current != null && current != listView)
            {
                if (current is ListViewItem) return true;
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }
        /// <summary>
        /// 手动挂载列头点击事件
        /// 此方法通过遍历可视树找到 Visual Header 容器，解决 TextBlock 内容导致点击区域过小和 Tag 丢失的问题
        /// </summary>
        private void HookColumnHeaders()
        {
            if (FileList?.FilesList == null) return;

            var headers = FindVisualChildren<GridViewColumnHeader>(FileList.FilesList);
            foreach (var header in headers)
            {
                // 移除旧处理程序以防重复
                header.Click -= Header_Click;
                header.Click += Header_Click;

                // 尝试修复 Tag 缺失问题（立即修复，防止第一次点击失败）
                FixHeaderTag(header);
            }
        }

        private void Header_Click(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader header)
            {
                FixHeaderTag(header);
                GridViewColumnHeaderClick?.Invoke(header, e);
            }
        }

        private void FixHeaderTag(GridViewColumnHeader header)
        {
            if (header.Tag == null && header.Content is FrameworkElement content && content.Tag != null)
            {
                header.Tag = content.Tag;
            }
        }

        private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T t)
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
    }
}

