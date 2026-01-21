using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using YiboFile.Controls;
using YiboFile.Services;
using YiboFile.Services.Navigation;
using YiboFile.Services.Search;
using YiboFile.Services.Core;
using YiboFile.Services.Tabs;
using System.Windows.Media;

namespace YiboFile.Handlers
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
            Func<object> getCurrentTagFilter,
            Action<object> setCurrentTagFilter,
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
            _fileBrowser.FilesPreviewMouseDoubleClickForBlank += FileBrowser_FilesPreviewMouseDoubleClickForBlank;

            _fileBrowser.TagClicked += (s, tag) =>
            {
                if (tag != null && !string.IsNullOrEmpty(tag.Name))
                {
                    _navigationCoordinator.HandlePathNavigation(
                        $"tag://{tag.Name}",
                        NavigationCoordinator.NavigationSource.AddressBar,
                        NavigationCoordinator.ClickType.LeftClick
                    );
                }
            };

            _fileBrowser.CommitRename += (s, e) => _commitRename(e);

        }

        public void FileBrowser_PathChanged(object sender, string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            // 检查是否为有效路径
            bool isPath = Directory.Exists(path) || File.Exists(path) || ProtocolManager.Parse(path).Type == ProtocolType.Archive || ProtocolManager.Parse(path).Type == ProtocolType.Tag;
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

            if (path == "tag://")
            {
                return;
            }

            // 使用统一导航协调器处理面包屑中键点击（中键打开新标签页）
            _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.Breadcrumb, NavigationCoordinator.ClickType.MiddleClick);
        }

        public void FileBrowser_BreadcrumbClicked(object sender, string path)
        {
            if (path == "tag://")
            {
                return;
            }

            // 使用统一导航协调器处理面包屑左键点击
            _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.Breadcrumb, NavigationCoordinator.ClickType.LeftClick);
        }



        private void RefreshSearchIfActive()
        {
            var currentPath = _getCurrentPath();
            if (!string.IsNullOrEmpty(currentPath))
            {
                var protocolInfo = ProtocolManager.Parse(currentPath);
                if (protocolInfo.Type == ProtocolType.Search)
                {
                    if (!string.IsNullOrEmpty(protocolInfo.TargetPath))
                    {
                        _performSearch(protocolInfo.TargetPath);
                    }
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
                var protocolInfo = ProtocolManager.Parse(currentPath);

                // 如果是搜索模式，刷新搜索结果（搜索服务会应用过滤）
                if (protocolInfo.Type == ProtocolType.Search)
                {
                    RefreshSearchIfActive();
                    return;
                }

                // 对路径/库模式使用 CollectionView.Filter
                view.Filter = obj =>
                {
                    if (obj is not FileSystemItem item) return true;

                    // Scope Filter (SearchMode) for Normal View
                    switch (searchOptions.Mode)
                    {
                        case SearchMode.Folder:
                            if (!item.IsDirectory) return false;
                            break;
                        case SearchMode.FileName:
                            if (item.IsDirectory) return false;
                            break;
                        case SearchMode.Notes:
                            if (string.IsNullOrEmpty(item.Notes)) return false;
                            break;
                    }

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

                    // 图片尺寸过滤
                    if (searchOptions.ImageSize != ImageDimensionFilter.All)
                    {
                        if (!MatchesImageSizeFilter(item, searchOptions.ImageSize)) return false;
                    }

                    // 时长过滤
                    if (searchOptions.Duration != AudioDurationFilter.All)
                    {
                        if (!MatchesDurationFilter(item, searchOptions.Duration)) return false;
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

        public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff", ".ico", ".svg" };

        public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".flv", ".webm", ".m4v", ".ts" };

        public static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md", ".rtf" };

        public static bool MatchesTypeFilter(FileSystemItem item, FileTypeFilter filter)
        {
            switch (filter)
            {
                case FileTypeFilter.Images:
                    return !item.IsDirectory && YiboFile.Services.Search.SearchFilterService.ImageExtensions.Contains(System.IO.Path.GetExtension(item.Path));
                case FileTypeFilter.Videos:
                    return !item.IsDirectory && YiboFile.Services.Search.SearchFilterService.VideoExtensions.Contains(System.IO.Path.GetExtension(item.Path));
                case FileTypeFilter.Audio:
                    return !item.IsDirectory && YiboFile.Services.Search.SearchFilterService.AudioExtensions.Contains(System.IO.Path.GetExtension(item.Path));
                case FileTypeFilter.Documents:
                    return !item.IsDirectory && YiboFile.Services.Search.SearchFilterService.DocumentExtensions.Contains(System.IO.Path.GetExtension(item.Path));
                case FileTypeFilter.Folders:
                    return item.IsDirectory;
                default:
                    return true;
            }
        }

        public static bool MatchesDateFilter(FileSystemItem item, DateRangeFilter filter)
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

        public static bool MatchesSizeFilter(FileSystemItem item, SizeRangeFilter filter)
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

        public static bool MatchesImageSizeFilter(FileSystemItem item, ImageDimensionFilter filter)
        {
            if (item.IsDirectory) return false;
            int maxDim = Math.Max(item.PixelWidth, item.PixelHeight); // 0 if N/A

            return filter switch
            {
                ImageDimensionFilter.Small => maxDim < 800,
                ImageDimensionFilter.Medium => maxDim >= 800 && maxDim < 1920,
                ImageDimensionFilter.Large => maxDim >= 1920 && maxDim < 3840,
                ImageDimensionFilter.Huge => maxDim >= 3840,
                _ => true
            };
        }

        public static bool MatchesDurationFilter(FileSystemItem item, AudioDurationFilter filter)
        {
            if (item.IsDirectory) return false;
            long duration = item.DurationMs; // 0 if N/A
            const long Minute = 60 * 1000;

            return filter switch
            {
                AudioDurationFilter.Short => duration < Minute,
                AudioDurationFilter.Medium => duration >= Minute && duration < 5 * Minute,
                AudioDurationFilter.Long => duration >= 5 * Minute && duration < 20 * Minute,
                AudioDurationFilter.VeryLong => duration >= 20 * Minute,
                _ => true
            };
        }

        public void FileBrowser_FilterClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var searchOptions = _getSearchOptions();
                if (searchOptions == null) return;

                // Show the modern Filter Panel instead of ContextMenu
                _fileBrowser.ToggleFilterPanel(searchOptions, (s, ev) =>
                {
                    // Filter Changed Handler
                    ApplyGlobalFilter();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FilterClicked] Error: {ex.Message}");
            }
        }

        public void FileBrowser_LoadMoreClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var activeTab = _tabService.ActiveTab;
                if (activeTab == null || activeTab.Path == null) return;

                var protocolInfo = ProtocolManager.Parse(activeTab.Path);
                if (protocolInfo.Type != ProtocolType.Search) return;

                var keyword = protocolInfo.TargetPath;
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


