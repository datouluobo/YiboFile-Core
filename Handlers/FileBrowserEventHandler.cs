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
            Func<ColumnDefinition> getColLeft)
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
        }

        /// <summary>
        /// 初始化事件绑定
        /// </summary>
        public void Initialize()
        {
            if (_fileBrowser == null) return;

            _fileBrowser.PathChanged += FileBrowser_PathChanged;
            _fileBrowser.BreadcrumbMiddleClicked += FileBrowser_BreadcrumbMiddleClicked;
            _fileBrowser.BreadcrumbClicked += FileBrowser_BreadcrumbClicked;
            _fileBrowser.SearchClicked += FileBrowser_SearchClicked;
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

        public void FileBrowser_SearchClicked(object sender, RoutedEventArgs e)
        {
            // 从列2地址栏读取搜索关键词
            var searchText = _fileBrowser?.AddressText?.Trim() ?? "";
            // 使用统一的规范化方法剥离前缀"搜索:"避免污染关键词（多次前缀）
            var normalizedKeyword = SearchService.NormalizeKeyword(searchText);

            if (string.IsNullOrEmpty(normalizedKeyword))
            {
                MessageBox.Show("请在地址栏输入搜索关键词", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 检查是否为有效路径（使用规范化后的关键词检查）
            if (Directory.Exists(normalizedKeyword) || File.Exists(normalizedKeyword))
            {
                // 如果是有效路径，导航到该路径
                _navigateToPath(normalizedKeyword);
                return;
            }

            // 非路径，执行全盘搜索（文件名+备注），使用规范化关键词
            _performSearch(normalizedKeyword);
        }

        public void FileBrowser_FilterClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var cm = new ContextMenu();
                var searchOptions = _getSearchOptions();

                void AddType(string text, FileTypeFilter type)
                {
                    var mi = new MenuItem { Header = text, IsCheckable = true, IsChecked = searchOptions.Type == type };
                    mi.Click += (s, ev) => { searchOptions.Type = type; };
                    cm.Items.Add(mi);
                }

                AddType("全部", FileTypeFilter.All);
                AddType("图片", FileTypeFilter.Images);
                AddType("视频", FileTypeFilter.Videos);
                AddType("文档", FileTypeFilter.Documents);
                AddType("文件夹", FileTypeFilter.Folders);

                var rangeCurrent = new MenuItem { Header = "当前磁盘", IsCheckable = true, IsChecked = searchOptions.PathRange == PathRangeFilter.CurrentDrive };
                rangeCurrent.Click += (s, ev) => { searchOptions.PathRange = PathRangeFilter.CurrentDrive; };
                cm.Items.Add(rangeCurrent);

                var rangeAll = new MenuItem { Header = "全部磁盘", IsCheckable = true, IsChecked = searchOptions.PathRange == PathRangeFilter.AllDrives };
                rangeAll.Click += (s, ev) => { searchOptions.PathRange = PathRangeFilter.AllDrives; };
                cm.Items.Add(rangeAll);

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
            catch (Exception ex)
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
                    current = VisualTreeHelper.GetParent(current);
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

