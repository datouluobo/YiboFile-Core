using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using OoiMRR.Controls;
using OoiMRR.Services;
using OoiMRR.Services.Navigation;
using OoiMRR.Services.Search;
using OoiMRR.Services.Tabs;
using System.Windows.Media;

namespace OoiMRR.Handlers
{
    /// <summary>
    /// FileBrowser 控件事件处理器
    /// 处理 FileBrowser 控件的所有事件，包括路径变化、面包屑点击、搜索、过滤等
    /// </summary>
    public class FileBrowserEventHandler
    {
        private readonly FileBrowserControl _fileBrowser;
        private readonly NavigationCoordinator _navigationCoordinator;
        private readonly TabService _tabService;
        private readonly SearchService _searchService;
        private readonly SearchCacheService _searchCacheService;
        private readonly Action<string> _navigateToPath;
        private readonly Action<string> _performSearch;
        private readonly Action<string> _switchNavigationMode;
        private readonly Action _loadCurrentDirectory;
        private readonly Action _clearFilter;
        private readonly Action _hideEmptyStateMessage;
        private readonly Action<GridViewColumnHeader> _gridViewColumnHeaderClick;
        private readonly Action<SizeChangedEventArgs> _listViewSizeChanged;
        private readonly Action<DragDeltaEventArgs> _gridSplitterDragDelta;
        private readonly Func<string> _getCurrentPath;
        private readonly Func<AppConfig> _getConfig;
        private readonly Func<Tag> _getCurrentTagFilter;
        private readonly Action<Tag> _setCurrentTagFilter;
        private readonly Func<List<FileSystemItem>> _getCurrentFiles;
        private readonly Action<List<FileSystemItem>> _setCurrentFiles;
        private readonly Func<SearchOptions> _getSearchOptions;
        private readonly Action<SelectionChangedEventArgs> _filesListViewSelectionChanged;
        private readonly Action<MouseButtonEventArgs> _filesListViewMouseDoubleClick;
        private readonly Action<MouseButtonEventArgs> _filesListViewPreviewMouseDoubleClick;
        private readonly Action<KeyEventArgs> _filesListViewPreviewKeyDown;
        private readonly Action<MouseButtonEventArgs> _filesListViewPreviewMouseLeftButtonDown;
        private readonly Action<MouseButtonEventArgs> _filesListViewMouseLeftButtonUp;
        private readonly Action<MouseButtonEventArgs> _filesListViewPreviewMouseDown;
        private readonly Action<MouseButtonEventArgs> _filesListViewPreviewMouseDoubleClickForBlank;
        private readonly Action<MouseEventArgs> _filesListViewPreviewMouseMove;
        private readonly Func<ColumnDefinition> _getColLeft;

        public FileBrowserEventHandler(
            FileBrowserControl fileBrowser,
            NavigationCoordinator navigationCoordinator,
            TabService tabService,
            SearchService searchService,
            SearchCacheService searchCacheService,
            Action<string> navigateToPath,
            Action<string> performSearch,
            Action<string> switchNavigationMode,
            Action loadCurrentDirectory,
            Action clearFilter,
            Action hideEmptyStateMessage,
            Action<GridViewColumnHeader> gridViewColumnHeaderClick,
            Action<SizeChangedEventArgs> listViewSizeChanged,
            Action<DragDeltaEventArgs> gridSplitterDragDelta,
            Func<string> getCurrentPath,
            Func<AppConfig> getConfig,
            Func<Tag> getCurrentTagFilter,
            Action<Tag> setCurrentTagFilter,
            Func<List<FileSystemItem>> getCurrentFiles,
            Action<List<FileSystemItem>> setCurrentFiles,
            Func<SearchOptions> getSearchOptions,
            Action<SelectionChangedEventArgs> filesListViewSelectionChanged,
            Action<MouseButtonEventArgs> filesListViewMouseDoubleClick,
            Action<MouseButtonEventArgs> filesListViewPreviewMouseDoubleClick,
            Action<KeyEventArgs> filesListViewPreviewKeyDown,
            Action<MouseButtonEventArgs> filesListViewPreviewMouseLeftButtonDown,
            Action<MouseButtonEventArgs> filesListViewMouseLeftButtonUp,
            Action<MouseButtonEventArgs> filesListViewPreviewMouseDown,
            Action<MouseButtonEventArgs> filesListViewPreviewMouseDoubleClickForBlank,
            Action<MouseEventArgs> filesListViewPreviewMouseMove,
            Func<ColumnDefinition> getColLeft,
            Action<RenameEventArgs> commitRename)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _searchCacheService = searchCacheService ?? throw new ArgumentNullException(nameof(searchCacheService));
            _navigateToPath = navigateToPath ?? throw new ArgumentNullException(nameof(navigateToPath));
            _performSearch = performSearch ?? throw new ArgumentNullException(nameof(performSearch));
            _switchNavigationMode = switchNavigationMode ?? throw new ArgumentNullException(nameof(switchNavigationMode));
            _loadCurrentDirectory = loadCurrentDirectory ?? throw new ArgumentNullException(nameof(loadCurrentDirectory));
            _clearFilter = clearFilter ?? throw new ArgumentNullException(nameof(clearFilter));
            _hideEmptyStateMessage = hideEmptyStateMessage ?? throw new ArgumentNullException(nameof(hideEmptyStateMessage));
            _gridViewColumnHeaderClick = gridViewColumnHeaderClick ?? throw new ArgumentNullException(nameof(gridViewColumnHeaderClick));
            _listViewSizeChanged = listViewSizeChanged ?? throw new ArgumentNullException(nameof(listViewSizeChanged));
            _gridSplitterDragDelta = gridSplitterDragDelta ?? throw new ArgumentNullException(nameof(gridSplitterDragDelta));
            _getCurrentPath = getCurrentPath ?? throw new ArgumentNullException(nameof(getCurrentPath));
            _getConfig = getConfig ?? throw new ArgumentNullException(nameof(getConfig));
            _getCurrentTagFilter = getCurrentTagFilter ?? throw new ArgumentNullException(nameof(getCurrentTagFilter));
            _setCurrentTagFilter = setCurrentTagFilter ?? throw new ArgumentNullException(nameof(setCurrentTagFilter));
            _getCurrentFiles = getCurrentFiles ?? throw new ArgumentNullException(nameof(getCurrentFiles));
            _setCurrentFiles = setCurrentFiles ?? throw new ArgumentNullException(nameof(setCurrentFiles));
            _getSearchOptions = getSearchOptions ?? throw new ArgumentNullException(nameof(getSearchOptions));
            _filesListViewSelectionChanged = filesListViewSelectionChanged ?? throw new ArgumentNullException(nameof(filesListViewSelectionChanged));
            _filesListViewMouseDoubleClick = filesListViewMouseDoubleClick ?? throw new ArgumentNullException(nameof(filesListViewMouseDoubleClick));
            _filesListViewPreviewMouseDoubleClick = filesListViewPreviewMouseDoubleClick ?? throw new ArgumentNullException(nameof(filesListViewPreviewMouseDoubleClick));
            _filesListViewPreviewKeyDown = filesListViewPreviewKeyDown ?? throw new ArgumentNullException(nameof(filesListViewPreviewKeyDown));
            _filesListViewPreviewMouseLeftButtonDown = filesListViewPreviewMouseLeftButtonDown ?? throw new ArgumentNullException(nameof(filesListViewPreviewMouseLeftButtonDown));
            _filesListViewMouseLeftButtonUp = filesListViewMouseLeftButtonUp ?? throw new ArgumentNullException(nameof(filesListViewMouseLeftButtonUp));
            _filesListViewPreviewMouseDown = filesListViewPreviewMouseDown ?? throw new ArgumentNullException(nameof(filesListViewPreviewMouseDown));
            _filesListViewPreviewMouseDoubleClickForBlank = filesListViewPreviewMouseDoubleClickForBlank ?? throw new ArgumentNullException(nameof(filesListViewPreviewMouseDoubleClickForBlank));
            _filesListViewPreviewMouseMove = filesListViewPreviewMouseMove ?? throw new ArgumentNullException(nameof(filesListViewPreviewMouseMove));
            _getColLeft = getColLeft ?? throw new ArgumentNullException(nameof(getColLeft));
            _commitRename = commitRename ?? throw new ArgumentNullException(nameof(commitRename));
        }

        private readonly Action<RenameEventArgs> _commitRename;
        private bool _isFilterMultiSelect = false;

        /// <summary>
        /// 初始化事件绑定
        /// </summary>
        public void Initialize()
        {
            if (_fileBrowser == null) return;

            _fileBrowser.PathChanged += FileBrowser_PathChanged;
            _fileBrowser.BreadcrumbMiddleClicked += FileBrowser_BreadcrumbMiddleClicked;
            _fileBrowser.BreadcrumbClicked += FileBrowser_BreadcrumbClicked;

            _fileBrowser.FilterClicked += FileBrowser_FilterClicked;
            _fileBrowser.LoadMoreClicked += FileBrowser_LoadMoreClicked;
            _fileBrowser.GridViewColumnHeaderClick += FileBrowser_GridViewColumnHeaderClick;
            _fileBrowser.FilesSizeChanged += FileBrowser_FilesSizeChanged;
            _fileBrowser.Loaded += FileBrowser_Loaded;
            _fileBrowser.FilesSelectionChanged += FileBrowser_FilesSelectionChanged;
            _fileBrowser.FilesMouseDoubleClick += FileBrowser_FilesMouseDoubleClick;
            _fileBrowser.FilesPreviewMouseDoubleClick += FileBrowser_FilesPreviewMouseDoubleClick;
            _fileBrowser.FilesPreviewKeyDown += FileBrowser_FilesPreviewKeyDown;
            _fileBrowser.FilesPreviewMouseLeftButtonDown += FileBrowser_FilesPreviewMouseLeftButtonDown;
            _fileBrowser.FilesMouseLeftButtonUp += FileBrowser_FilesMouseLeftButtonUp;
            _fileBrowser.FilesPreviewMouseDown += FileBrowser_FilesPreviewMouseDown;
            _fileBrowser.FilesPreviewMouseDoubleClickForBlank += FileBrowser_FilesPreviewMouseDoubleClickForBlank;
            _fileBrowser.FilesPreviewMouseMove += FileBrowser_FilesPreviewMouseMove;
            _fileBrowser.CommitRename += (s, e) => _commitRename(e);

        }

        public void FileBrowser_PathChanged(object sender, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            // 检查是否为有效路径
            bool isPath = Directory.Exists(path) || File.Exists(path);
            if (isPath)
            {
                // 使用统一导航协调器处理路径导航（左键点击）
                _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.AddressBar, NavigationCoordinator.ClickType.LeftClick);
                return;
            }

            // 非有效路径：按搜索关键词处理（支持回车触发搜索）

            // 非有效路径：按搜索关键词处理（支持回车触发搜索）

            // 使用统一的规范化方法剥离前缀"搜索:"
            var normalizedKeyword = SearchService.NormalizeKeyword(path);
            if (!string.IsNullOrEmpty(normalizedKeyword))
            {
                _performSearch(normalizedKeyword);
            }
            else
            {
            }
        }

        public void FileBrowser_BreadcrumbMiddleClicked(object sender, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            // 处理tag://路径，返回到标签浏览模式
            if (path == "tag://")
            {
                var config = _getConfig();
                // 切换到标签模式（如果当前不在标签模式）
                if (config.LastNavigationMode != "Tag")
                {
                    _switchNavigationMode("Tag");
                }
                else
                {
                    // 已经在标签模式，清除当前选中的标签，显示所有标签
                    _setCurrentTagFilter(null);
                    if (_fileBrowser != null)
                    {
                        _fileBrowser.FilesItemsSource = null;
                        _fileBrowser.AddressText = "";
                        _fileBrowser.IsAddressReadOnly = true;
                        _fileBrowser.SetTagBreadcrumb("标签");
                    }
                    _hideEmptyStateMessage();
                }
                return;
            }

            // 使用统一导航协调器处理面包屑中键点击（中键打开新标签页）
            _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.Breadcrumb, NavigationCoordinator.ClickType.MiddleClick);
        }

        public void FileBrowser_BreadcrumbClicked(object sender, string path)
        {
            // 处理tag://路径，返回到标签浏览模式
            if (path == "tag://")
            {
                var config = _getConfig();
                // 切换到标签模式（如果当前不在标签模式）
                if (config.LastNavigationMode != "Tag")
                {
                    _switchNavigationMode("Tag");
                }
                else
                {
                    // 已经在标签模式，清除当前选中的标签，显示所有标签
                    _setCurrentTagFilter(null);
                    if (_fileBrowser != null)
                    {
                        _fileBrowser.FilesItemsSource = null;
                        _fileBrowser.AddressText = "";
                        _fileBrowser.IsAddressReadOnly = true;
                        _fileBrowser.SetTagBreadcrumb("标签");
                    }
                    _hideEmptyStateMessage();
                }
                return;
            }

            // 使用统一导航协调器处理面包屑左键点击
            _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.Breadcrumb, NavigationCoordinator.ClickType.LeftClick);
        }



        private void RefreshSearchIfActive()
        {
            var currentPath = _getCurrentPath();
            if (!string.IsNullOrEmpty(currentPath) && currentPath.StartsWith("search://"))
            {
                var keyword = currentPath.Substring("search://".Length);
                if (!string.IsNullOrEmpty(keyword))
                {
                    _performSearch(keyword);
                }
            }
        }

        /// <summary>
        /// 应用全局过滤器到当前文件列表（对路径/库/搜索模式均生效）
        /// </summary>
        private void ApplyGlobalFilter()
        {
            try
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_fileBrowser?.FilesItemsSource);
                if (view == null) return;

                var searchOptions = _getSearchOptions();
                var currentPath = _getCurrentPath();

                // 如果是搜索模式，刷新搜索结果（搜索服务会应用过滤）
                if (!string.IsNullOrEmpty(currentPath) && currentPath.StartsWith("search://"))
                {
                    RefreshSearchIfActive();
                    return;
                }

                // 对路径/库模式使用 CollectionView.Filter
                view.Filter = obj =>
                {
                    if (obj is not FileSystemItem item) return true;

                    // 类型过滤
                    if (searchOptions.Type != FileTypeFilter.All)
                    {
                        if (!MatchesTypeFilter(item, searchOptions.Type)) return false;
                    }

                    // 日期过滤
                    if (searchOptions.DateRange != DateRangeFilter.All)
                    {
                        if (!MatchesDateFilter(item, searchOptions.DateRange)) return false;
                    }

                    // 大小过滤
                    if (searchOptions.SizeRange != SizeRangeFilter.All)
                    {
                        if (!MatchesSizeFilter(item, searchOptions.SizeRange)) return false;
                    }

                    return true;
                };

                view.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApplyGlobalFilter] Error: {ex.Message}");
            }
        }

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff", ".ico", ".svg" };

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".ts" };

        private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md", ".rtf" };

        private bool MatchesTypeFilter(FileSystemItem item, FileTypeFilter filter)
        {
            switch (filter)
            {
                case FileTypeFilter.Images:
                    return !item.IsDirectory && ImageExtensions.Contains(System.IO.Path.GetExtension(item.Path));
                case FileTypeFilter.Videos:
                    return !item.IsDirectory && VideoExtensions.Contains(System.IO.Path.GetExtension(item.Path));
                case FileTypeFilter.Documents:
                    return !item.IsDirectory && DocumentExtensions.Contains(System.IO.Path.GetExtension(item.Path));
                case FileTypeFilter.Folders:
                    return item.IsDirectory;
                default:
                    return true;
            }
        }

        private bool MatchesDateFilter(FileSystemItem item, DateRangeFilter filter)
        {
            var modTime = item.ModifiedDateTime;
            if (modTime == default) return true;

            var now = DateTime.Now;
            return filter switch
            {
                DateRangeFilter.Today => modTime.Date == now.Date,
                DateRangeFilter.ThisWeek => modTime >= now.Date.AddDays(-(int)now.DayOfWeek),
                DateRangeFilter.ThisMonth => modTime >= new DateTime(now.Year, now.Month, 1),
                DateRangeFilter.ThisYear => modTime >= new DateTime(now.Year, 1, 1),
                _ => true
            };
        }

        private bool MatchesSizeFilter(FileSystemItem item, SizeRangeFilter filter)
        {
            if (item.IsDirectory) return true; // 文件夹不按大小过滤
            var size = item.SizeBytes >= 0 ? item.SizeBytes : 0;

            const long KB = 1024;
            const long MB = 1024 * KB;

            return filter switch
            {
                SizeRangeFilter.Tiny => size < 100 * KB,
                SizeRangeFilter.Small => size >= 100 * KB && size < MB,
                SizeRangeFilter.Medium => size >= MB && size < 10 * MB,
                SizeRangeFilter.Large => size >= 10 * MB && size < 100 * MB,
                SizeRangeFilter.Huge => size >= 100 * MB,
                _ => true
            };
        }

        public void FileBrowser_FilterClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var cm = new ContextMenu();
                var searchOptions = _getSearchOptions();

                // Multi-select Toggle
                var multiSelect = new MenuItem { Header = "多选模式 (保持菜单开启)", IsCheckable = true, IsChecked = _isFilterMultiSelect };
                multiSelect.Click += (s, ev) =>
                {
                    _isFilterMultiSelect = !_isFilterMultiSelect;
                    multiSelect.IsChecked = _isFilterMultiSelect;
                    foreach (var item in cm.Items)
                    {
                        if (item is MenuItem mi && mi != multiSelect) mi.StaysOpenOnClick = _isFilterMultiSelect;
                    }
                };
                multiSelect.StaysOpenOnClick = true;
                cm.Items.Add(multiSelect);
                cm.Items.Add(new Separator());

                // Helper to add items
                void AddItem(string header, bool isChecked, Action onClick, object tag = null)
                {
                    var mi = new MenuItem { Header = header, IsCheckable = true, IsChecked = isChecked, Tag = tag };
                    mi.StaysOpenOnClick = _isFilterMultiSelect;
                    mi.Click += (s, ev) =>
                    {
                        onClick();
                        if (!_isFilterMultiSelect)
                        {
                            ApplyGlobalFilter();
                        }
                        else
                        {
                            ApplyGlobalFilter();
                            // Update visual state for radio behavior
                            if (tag != null) // Group logic implies by Tag type
                            {
                                var type = tag.GetType();
                                foreach (var item in cm.Items)
                                {
                                    if (item is MenuItem otherMi && otherMi.Tag != null && otherMi.Tag.GetType() == type)
                                    {
                                        otherMi.IsChecked = (otherMi == mi);
                                    }
                                }
                            }
                            // Special handling for Scope (no Enum Tag used below, explicit string check or separate Tag)
                            if (header == "按文件名" || header == "按备注")
                            {
                                foreach (var item in cm.Items)
                                {
                                    if (item is MenuItem otherMi && (otherMi.Header.ToString() == "按文件名" || otherMi.Header.ToString() == "按备注"))
                                    {
                                        otherMi.IsChecked = (otherMi == mi);
                                    }
                                }
                            }
                        }
                    };
                    cm.Items.Add(mi);
                }

                // Search Scope
                cm.Items.Add(new MenuItem { Header = "搜索范围", IsEnabled = false });
                AddItem("按文件名", searchOptions.Mode == SearchMode.FileName, () =>
                {
                    searchOptions.Mode = SearchMode.FileName;
                    searchOptions.SearchNames = true;
                    searchOptions.SearchNotes = false;
                });
                AddItem("按备注", searchOptions.Mode == SearchMode.Notes, () =>
                {
                    searchOptions.Mode = SearchMode.Notes;
                    searchOptions.SearchNames = false;
                    searchOptions.SearchNotes = true;
                });
                cm.Items.Add(new Separator());

                // Date Range
                cm.Items.Add(new MenuItem { Header = "时间范围", IsEnabled = false });
                AddItem("全部时间", searchOptions.DateRange == DateRangeFilter.All, () => searchOptions.DateRange = DateRangeFilter.All, DateRangeFilter.All);
                AddItem("今天", searchOptions.DateRange == DateRangeFilter.Today, () => searchOptions.DateRange = DateRangeFilter.Today, DateRangeFilter.Today);
                AddItem("本周", searchOptions.DateRange == DateRangeFilter.ThisWeek, () => searchOptions.DateRange = DateRangeFilter.ThisWeek, DateRangeFilter.ThisWeek);
                AddItem("本月", searchOptions.DateRange == DateRangeFilter.ThisMonth, () => searchOptions.DateRange = DateRangeFilter.ThisMonth, DateRangeFilter.ThisMonth);
                AddItem("今年", searchOptions.DateRange == DateRangeFilter.ThisYear, () => searchOptions.DateRange = DateRangeFilter.ThisYear, DateRangeFilter.ThisYear);
                cm.Items.Add(new Separator());

                // File Type
                cm.Items.Add(new MenuItem { Header = "文件类型", IsEnabled = false });
                AddItem("全部类型", searchOptions.Type == FileTypeFilter.All, () => searchOptions.Type = FileTypeFilter.All, FileTypeFilter.All);
                AddItem("图片", searchOptions.Type == FileTypeFilter.Images, () => searchOptions.Type = FileTypeFilter.Images, FileTypeFilter.Images);
                AddItem("视频", searchOptions.Type == FileTypeFilter.Videos, () => searchOptions.Type = FileTypeFilter.Videos, FileTypeFilter.Videos);
                AddItem("文档", searchOptions.Type == FileTypeFilter.Documents, () => searchOptions.Type = FileTypeFilter.Documents, FileTypeFilter.Documents);
                AddItem("文件夹", searchOptions.Type == FileTypeFilter.Folders, () => searchOptions.Type = FileTypeFilter.Folders, FileTypeFilter.Folders);
                cm.Items.Add(new Separator());

                // Size Range
                cm.Items.Add(new MenuItem { Header = "文件大小", IsEnabled = false });
                AddItem("全部大小", searchOptions.SizeRange == SizeRangeFilter.All, () => searchOptions.SizeRange = SizeRangeFilter.All, SizeRangeFilter.All);
                AddItem("微型 (<100KB)", searchOptions.SizeRange == SizeRangeFilter.Tiny, () => searchOptions.SizeRange = SizeRangeFilter.Tiny, SizeRangeFilter.Tiny);
                AddItem("小 (100KB-1MB)", searchOptions.SizeRange == SizeRangeFilter.Small, () => searchOptions.SizeRange = SizeRangeFilter.Small, SizeRangeFilter.Small);
                AddItem("中 (1MB-10MB)", searchOptions.SizeRange == SizeRangeFilter.Medium, () => searchOptions.SizeRange = SizeRangeFilter.Medium, SizeRangeFilter.Medium);
                AddItem("大 (10MB-100MB)", searchOptions.SizeRange == SizeRangeFilter.Large, () => searchOptions.SizeRange = SizeRangeFilter.Large, SizeRangeFilter.Large);
                AddItem("巨大 (>100MB)", searchOptions.SizeRange == SizeRangeFilter.Huge, () => searchOptions.SizeRange = SizeRangeFilter.Huge, SizeRangeFilter.Huge);
                cm.Items.Add(new Separator());

                // Path Range (仅搜索时有效)
                cm.Items.Add(new MenuItem { Header = "搜索位置 (仅搜索)", IsEnabled = false });
                AddItem("当前磁盘", searchOptions.PathRange == PathRangeFilter.CurrentDrive, () => searchOptions.PathRange = PathRangeFilter.CurrentDrive, PathRangeFilter.CurrentDrive);
                AddItem("全部磁盘", searchOptions.PathRange == PathRangeFilter.AllDrives, () => searchOptions.PathRange = PathRangeFilter.AllDrives, PathRangeFilter.AllDrives);

                cm.PlacementTarget = sender as UIElement;
                cm.IsOpen = true;
            }
            catch { }
        }

        public void FileBrowser_LoadMoreClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var activeTab = _tabService.ActiveTab;
                if (activeTab == null || activeTab.Path == null || !activeTab.Path.StartsWith("search://"))
                    return;

                var keyword = activeTab.Path.Substring("search://".Length);
                if (string.IsNullOrEmpty(keyword)) return;

                // 从缓存获取当前偏移量
                var cacheKey = $"search://{keyword}";
                var cache = _searchCacheService.GetCache(cacheKey);
                if (cache == null || !cache.HasMore) return;

                var currentPath = _getCurrentPath();
                var searchOptions = _getSearchOptions();
                var moreResult = _searchService.LoadMore(keyword, cache.Offset, searchOptions, currentPath);
                if (moreResult != null && moreResult.Items != null && moreResult.Items.Count > 0)
                {
                    var currentFiles = _getCurrentFiles();
                    currentFiles.AddRange(moreResult.Items);
                    _setCurrentFiles(currentFiles);

                    if (_fileBrowser != null)
                    {
                        _fileBrowser.FilesItemsSource = null;
                        _fileBrowser.FilesItemsSource = currentFiles;
                        _fileBrowser.LoadMoreVisible = moreResult.HasMore;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public void FileBrowser_GridViewColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            var header = sender as GridViewColumnHeader;
            if (header != null)
            {
                _gridViewColumnHeaderClick(header);
            }
        }

        public void FileBrowser_FilesSizeChanged(object sender, SizeChangedEventArgs e)
        {
            _listViewSizeChanged(e);
        }

        private void GridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            try
            {
                var colLeft = _getColLeft();
                if (colLeft != null)
                {
                    var w = colLeft.Width.Value;
                    var newW = w + e.HorizontalChange;
                    if (newW < 0) newW = 0;
                    var min = Math.Max(0, colLeft.MinWidth);
                    if (newW < min) newW = min;
                    colLeft.Width = new GridLength(newW);
                }
            }
            catch { }
        }

        public void FileBrowser_GridSplitterDragDelta(object sender, DragDeltaEventArgs e)
        {
            GridSplitter_DragDelta(sender, e);
        }

        public void FileBrowser_FilesPreviewMouseDoubleClickForBlank(object sender, MouseButtonEventArgs e)
        {
            _filesListViewPreviewMouseDoubleClickForBlank(e);
        }

        public void FileBrowser_FilesPreviewMouseMove(object sender, MouseEventArgs e)
        {
            _filesListViewPreviewMouseMove(e);
        }

        // 文件浏览控件的事件转发
        public void FileBrowser_FilesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 控制备注区可编辑状态
            var selectedCount = _fileBrowser?.FilesSelectedItems?.Count ?? 0;
            var mainWindow = Application.Current.MainWindow;

            if (mainWindow != null)
            {
                var mainWindowType = mainWindow.GetType();
                var rightPanelField = mainWindowType.GetField("RightPanel",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (rightPanelField != null)
                {
                    var rightPanel = rightPanelField.GetValue(mainWindow);
                    if (rightPanel != null)
                    {
                        var rightPanelType = rightPanel.GetType();
                        var notesTextBoxField = rightPanelType.GetField("NotesTextBox",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                        if (notesTextBoxField != null)
                        {
                            var notesTextBox = notesTextBoxField.GetValue(rightPanel) as System.Windows.Controls.TextBox;
                            if (notesTextBox != null)
                            {
                                notesTextBox.IsEnabled = (selectedCount == 1);
                            }
                        }
                    }
                }
            }

            _filesListViewSelectionChanged(e);
        }

        public void FileBrowser_FilesMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _filesListViewMouseDoubleClick(e);
        }

        public void FileBrowser_FilesPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _filesListViewPreviewMouseDoubleClick(e);
        }

        public void FileBrowser_FilesPreviewKeyDown(object sender, KeyEventArgs e)
        {
            _filesListViewPreviewKeyDown(e);
        }

        public void FileBrowser_FilesPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _filesListViewPreviewMouseLeftButtonDown(e);
        }

        public void FileBrowser_FilesMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _filesListViewMouseLeftButtonUp(e);
        }

        public void FileBrowser_FilesPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果点击了文件列表且地址栏处于编辑模式，退出编辑模式
            if (_fileBrowser?.AddressBar != null && _fileBrowser.AddressBar.IsAddressTextBoxFocused)
            {
                _fileBrowser.AddressBar.SwitchToBreadcrumbMode();
            }

            _filesListViewPreviewMouseDown(e);
        }

        public void FileBrowser_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化完成，无需额外处理
        }
        public void HandleGlobalMouseDown(MouseButtonEventArgs e)
        {
            // 如果地址栏处于编辑模式
            if (_fileBrowser?.AddressBar != null && _fileBrowser.AddressBar.IsEditMode)
            {
                // 检查点击目标是否在地址栏内
                var source = e.OriginalSource as DependencyObject;
                bool isAddressBar = false;

                // 向上查找看是否是 AddressBarControl 的子元素
                var current = source;
                while (current != null)
                {
                    if (current == _fileBrowser.AddressBar)
                    {
                        isAddressBar = true;
                        break;
                    }
                    if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                    {
                        current = VisualTreeHelper.GetParent(current);
                    }
                    else if (current is FrameworkContentElement fce)
                    {
                        current = fce.Parent;
                    }
                    else
                    {
                        current = null;
                    }
                }

                // 如果点击在地址栏外部，退出编辑模式
                if (!isAddressBar)
                {
                    // 使用 _getCurrentPath 获取当前正在显示的实际路径，确保地址栏显示正确
                    if (_getCurrentPath != null)
                    {
                        var currentPath = _getCurrentPath();
                        // 仅当路径不为空时重置，避免异常清空
                        if (!string.IsNullOrEmpty(currentPath))
                        {
                            _fileBrowser.AddressBar.AddressText = currentPath;
                        }
                    }
                    _fileBrowser.AddressBar.SwitchToBreadcrumbMode();
                }
            }
        }
    }
}

