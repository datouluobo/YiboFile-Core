using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.ComponentModel;
using OoiMRR.Services;
using OoiMRR.Services.FileOperations;
using OoiMRR.Services.FileList;
using OoiMRR.Services.Search;
using DragDropManager = OoiMRR.Services.DragDropManager;
using System.Threading;

namespace OoiMRR
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        private List<string> _navigationHistory = new List<string>();
        private int _currentHistoryIndex = -1;
        private List<FileSystemItem> _currentFiles = new List<FileSystemItem>();
        private AppConfig _config = new AppConfig();
        private bool _isApplyingConfig = false;
        private FileSystemWatcher _fileWatcher;
        private System.Windows.Threading.DispatcherTimer _refreshDebounceTimer;
        private DragDropManager _dragDropManager;
        private LibraryService _libraryService;
        private OoiMRR.Services.QuickAccess.QuickAccessService _quickAccessService;
        private OoiMRR.Services.Favorite.FavoriteService _favoriteService;
        private OoiMRR.Services.Tag.TagService _tagService;
        private FileListService _fileListService;
        private OoiMRR.Services.Search.SearchService _searchService;
        private OoiMRR.Services.Search.SearchCacheService _searchCacheService;
        private OoiMRR.Services.Search.SearchOptions _searchOptions = new OoiMRR.Services.Search.SearchOptions();
        private string _lastSortColumn = "Name";
        private bool _sortAscending = true;
        private System.Windows.Point _mouseDownPoint;
        private bool _isMouseDownOnListView = false;
        private bool _isMouseDownOnColumnHeader = false;
        private Library _currentLibrary = null;
        private Tag _currentTagFilter = null;
        private bool _isUpdatingTagSelection = false;
        private string _lastLeftNavSource = null;
        
        // 加载锁定，防止重复加载导致卡死
        private bool _isLoadingFiles = false;
        private System.Threading.SemaphoreSlim _loadFilesSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        
        // 性能优化：限制并发文件夹大小计算任务（减少到1个，避免CPU占用过高）
        private System.Threading.SemaphoreSlim _folderSizeCalculationSemaphore = new System.Threading.SemaphoreSlim(1, 1); // 最多1个并发任务
        private System.Threading.CancellationTokenSource _folderSizeCalculationCancellation = new System.Threading.CancellationTokenSource();
        
        // 剩余文件夹大小计算队列（用于闲置时计算）
        private Queue<string> _pendingFolderSizeCalculations = new Queue<string>();
        private System.Windows.Threading.DispatcherTimer _idleFolderSizeCalculationTimer;
        
        // 定时器管理
        private System.Windows.Threading.DispatcherTimer _periodicTimer;
        private System.Windows.Threading.DispatcherTimer _layoutCheckTimer;
        private System.Windows.Threading.DispatcherTimer _saveTimer;
        private System.Windows.Threading.DispatcherTimer _columnWidthSaveTimer;
        private bool _isSplitterDragging = false; // 标记是否正在拖拽分割器
        
        // 标签页管理（统一处理库和路径）
        private enum TabType
        {
            Path,    // 路径标签页
            Library, // 库标签页
            Tag      // 标签标签页（仅当 TagTrain 可用时）
        }
        
        // 统一的标签显示排序：当前过滤标签优先，其余按名称升序
        private List<string> OrderTagNames(List<int> tagIds)
        {
            try
            {
                var pairs = tagIds
                    .Select(id => new { Id = id, Name = OoiMRRIntegration.GetTagName(id) })
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .ToList();
                
                var comparer = StringComparer.CurrentCultureIgnoreCase;
                int currentId = _currentTagFilter?.Id ?? int.MinValue;
                
                var ordered = pairs
                    .OrderBy(p => p.Id == currentId ? 0 : 1)   // 当前筛选的标签放最前
                    .ThenBy(p => p.Name, comparer)
                    .Select(p => p.Name)
                    .ToList();
                
                return ordered;
            }
            catch
            {
                // 回退到按名称排序
                return tagIds
                    .Select(id => OoiMRRIntegration.GetTagName(id))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
        }
        
        // TagTrain 训练状态
        private CancellationTokenSource _tagTrainTrainingCancellation = null;
        private bool _tagTrainIsTraining = false;
        
        // 标签点击模式
        private OoiMRR.Services.Tag.TagClickMode _tagClickMode = OoiMRR.Services.Tag.TagClickMode.Browse;
        
        private class PathTab
        {
            public TabType Type { get; set; } = TabType.Path;
            public string Path { get; set; }  // 路径标签页使用路径，库/标签页使用名称或标识
            public string Title { get; set; }
            public Button TabButton { get; set; }
            public FrameworkElement CloseButton { get; set; }
            public Library Library { get; set; }  // 库标签页时使用
            public int TagId { get; set; }       // 标签页时使用
            public string TagName { get; set; }  // 标签页时使用
            public bool IsPinned { get; set; }
            public StackPanel TabContainer { get; set; }
            public TextBlock TitleTextBlock { get; set; }
            public string OverrideTitle { get; set; }
        }
        private List<PathTab> _pathTabs = new List<PathTab>();
        private PathTab _activeTab = null;
        
        // 可拖动按钮管理
        private class DraggableButton
        {
            public Button Button { get; set; }
            public string ActionName { get; set; }
            public RoutedEventHandler ClickHandler { get; set; }
        }
        
        // 用于保存按钮和分隔符的混合列表
        private class ActionItem
        {
            public DraggableButton Button { get; set; }
            public Separator Separator { get; set; }
            public bool IsSeparator => Separator != null;
        }
        
        private List<DraggableButton> _currentActionButtons = new List<DraggableButton>();
        private List<ActionItem> _actionItems = new List<ActionItem>(); // 保存按钮和分隔符的完整顺序
        private DraggableButton _draggingButton = null;
        private System.Windows.Point _buttonDragStartPoint;
        private bool _isDragging = false;
        private PathTab _draggingTab = null;
        private System.Windows.Point _tabDragStartPoint;

        public MainWindow()
        {
            InitializeComponent();
            
            // 为库列表添加鼠标事件处理，检测鼠标中键和Ctrl键
            if (LibrariesListBox != null)
            {
                LibrariesListBox.PreviewMouseDown += LibrariesListBox_PreviewMouseDown;
            }
            
            // 订阅文件浏览控件的事件
            if (FileBrowser != null)
            {
                // 地址栏事件已在 XAML 中绑定
                // 文件列表事件已在 XAML 中绑定
                // 导航按钮事件
                FileBrowser.NavigationBack += NavigateBack_Click;
                FileBrowser.NavigationForward += NavigateForward_Click;
                FileBrowser.NavigationUp += NavigateUp_Click;
                FileBrowser.EnableAutoLoadMore();
                
                // 文件操作事件
                FileBrowser.FileCopy += Copy_Click;
                FileBrowser.FileCut += Cut_Click;
                FileBrowser.FilePaste += Paste_Click;
                FileBrowser.FileDelete += Delete_Click;
                FileBrowser.FileRename += Rename_Click;
                FileBrowser.FileRefresh += Refresh_Click;
                FileBrowser.FileProperties += ShowProperties_Click;
                
                // 搜索相关事件
                FileBrowser.SearchClicked += FileBrowser_SearchClicked;
                FileBrowser.FilterClicked += FileBrowser_FilterClicked;
                FileBrowser.LoadMoreClicked += FileBrowser_LoadMoreClicked;
            }
            InitializeApplication();
            this.Activated += (s, e) =>
            {
                if (_activeTab != null && _activeTab.Path != null && _activeTab.Path.StartsWith("search://"))
                {
                    CheckAndRefreshSearchTab(_activeTab.Path);
                }
            };
        }

        private void InitializeApplication()
        {
            // 初始化 FileListService
            _fileListService = new FileListService();
            
            // 初始化 LibraryService
            _libraryService = new LibraryService(this.Dispatcher);
            _libraryService.LibrariesLoaded += LibraryService_LibrariesLoaded;
            _libraryService.LibraryFilesLoaded += LibraryService_LibraryFilesLoaded;
            _libraryService.LibraryHighlightRequested += LibraryService_LibraryHighlightRequested;
            
            // 初始化 QuickAccessService
            _quickAccessService = new OoiMRR.Services.QuickAccess.QuickAccessService(this.Dispatcher);
            _quickAccessService.NavigateRequested += QuickAccessService_NavigateRequested;
            _quickAccessService.CreateTabRequested += QuickAccessService_CreateTabRequested;
            
            // 初始化 FavoriteService
            _favoriteService = new OoiMRR.Services.Favorite.FavoriteService(this.Dispatcher);
            _favoriteService.NavigateRequested += FavoriteService_NavigateRequested;
            _favoriteService.FileOpenRequested += FavoriteService_FileOpenRequested;
            _favoriteService.CreateTabRequested += FavoriteService_CreateTabRequested;
            _favoriteService.FavoritesLoaded += FavoriteService_FavoritesLoaded;
            
            // 初始化 TagService
            _tagService = new OoiMRR.Services.Tag.TagService(this.Dispatcher);
            _tagService.TagFilterRequested += TagService_TagFilterRequested;
            _tagService.TagTabRequested += TagService_TagTabRequested;
            _tagService.TagsLoaded += TagService_TagsLoaded;
            
            // 初始化 SearchService
            var filterService = new SearchFilterService();
            _searchCacheService = new SearchCacheService();
            var resultBuilder = new SearchResultBuilder(
                formatFileSize: FormatFileSize,
                getFileTagIds: path => App.IsTagTrainAvailable ? OoiMRRIntegration.GetFileTagIds(path) : null,
                getTagName: tagId => App.IsTagTrainAvailable ? OoiMRRIntegration.GetTagName(tagId) : null,
                getFileNotes: path => DatabaseManager.GetFileNotes(path)
            );
            _searchService = new SearchService(filterService, _searchCacheService, resultBuilder);
            
            // 加载配置
            _config = ConfigManager.Load();
            
            // 先应用路径配置（在Loaded之前）
            if (!string.IsNullOrEmpty(_config.LastPath) && Directory.Exists(_config.LastPath))
            {
                _currentPath = _config.LastPath;
            }
            
            // 窗口加载完成后异步加载初始数据，避免阻塞UI
            this.Loaded += (s, e) => 
            {
                ApplyConfig(_config);
                
                // 如果 TagTrain 不可用，隐藏标签按钮
                if (!App.IsTagTrainAvailable && NavTagBtn != null)
                {
                    NavTagBtn.Visibility = Visibility.Collapsed;
                }
                
                // 直接在主线程加载UI数据，避免嵌套Dispatcher调用
                _libraryService.LoadLibraries(); // 先加载库列表，确保后续恢复选中时库列表已准备好
                if (App.IsTagTrainAvailable)
                {
                    _tagService.LoadTags(TagBrowsePanel, TagEditPanel);
                }
                _quickAccessService.LoadQuickAccess(QuickAccessListBox);
                _quickAccessService.LoadDrives(DrivesListBox, (bytes) => _fileListService.FormatFileSize(bytes));
                _favoriteService.LoadFavorites(FavoritesListBox);
                
                // 初始化拖拽管理器
                InitializeDragDrop();
                
                // 延迟加载文件列表，确保窗口完全显示后再加载
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 初始化导航，恢复最后使用的模式（这会触发文件列表加载）
                    string lastMode = !string.IsNullOrEmpty(_config.LastNavigationMode) ? _config.LastNavigationMode : "Path";
                    SwitchNavigationMode(lastMode);
                }), System.Windows.Threading.DispatcherPriority.Background);
                
                // 程序启动后，异步清理不存在的文件夹大小缓存（防止数据库无限增长）
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CleanupFolderSizeCacheOnStartup();
                }), System.Windows.Threading.DispatcherPriority.Background);
                
                // 确保库列表拖拽功能已初始化（延迟到控件完全加载后）
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    InitializeLibraryDragDrop();
                    InitializeTabsDragDrop();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                
                // 连接右侧面板事件（按钮已移到主窗口，不再需要按钮事件）
                if (RightPanel != null)
                {
                    RightPanel.NotesTextChanged += NotesTextBox_TextChanged;
                    RightPanel.NotesAutoSaved += NotesAutoSaved_Handler;
                    RightPanel.PreviewMiddleClickRequested += RightPanel_PreviewMiddleClickRequested;
                    RightPanel.PreviewOpenFileRequested += RightPanel_PreviewOpenFileRequested;
                    // 按钮已移到主窗口，不再从右侧面板获取事件
                }
                
                // 标题栏拖拽功能已在XAML中绑定到TitleBar_MouseDown，无需额外设置
                // TopDragArea 和 RightPanel 的拖拽功能已移除，统一使用顶部标题栏
                
                // 设置窗口最小宽度=三列MinWidth之和+两个分割器宽度
                this.MinWidth = ColLeft.MinWidth + ColCenter.MinWidth + ColRight.MinWidth + 12;

                // 设置ListView列的最小宽度和压缩顺序
                if (FileBrowser != null && FileBrowser.FilesList != null)
                {
                    FileBrowser.FilesList.SizeChanged += ListView_SizeChanged;
                    
                    // 捕获列头分隔线的双击（即使事件已被子元素标记为Handled）
                    FileBrowser.FilesList.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
                        new MouseButtonEventHandler(FilesList_HeaderThumbDoubleClick), true);
                    
                    // 添加双击空白区域的事件处理
                    FileBrowser.FilesList.PreviewMouseDoubleClick += FilesListView_PreviewMouseDoubleClickForBlank;
                    
                    // 延迟加载列宽度，确保GridView已初始化
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        LoadColumnWidths();
                        ApplyVisibleColumnsForCurrentMode();
                        EnsureHeaderContextMenuHook();
                        HookHeaderThumbs();
                        // 设置定时保存列宽度（延迟保存，避免频繁写入）
                        _columnWidthSaveTimer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(1)
                        };
                        _columnWidthSaveTimer.Tick += (s, e) =>
                        {
                            SaveColumnWidths();
                            _columnWidthSaveTimer.Stop();
                        };
                        
                        // 监听列宽度变化
                        if (FileBrowser.FilesGrid != null)
                        {
                            foreach (var column in FileBrowser.FilesGrid.Columns)
                            {
                                // 使用依赖属性监听宽度变化
                                var dpd = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));
                                if (dpd != null)
                                {
                                    dpd.AddValueChanged(column, (s, e) =>
                                    {
                                        // 若该列在当前模式不可见，则强制保持为0，阻止任何拖动或布局将其拉出
                                        if (s is GridViewColumn changedCol)
                                        {
                                            var header = changedCol.Header as GridViewColumnHeader;
                                            var tag = header?.Tag?.ToString();
                                            if (!string.IsNullOrEmpty(tag))
                                            {
                                                var visibleCsvLocal = GetVisibleColumnsForCurrentMode() ?? "";
                                                var set = new HashSet<string>(visibleCsvLocal.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                                                if (!set.Contains(tag))
                                                {
                                                    if (changedCol.Width != 0)
                                                    {
                                                        changedCol.Width = 0;
                                                        return;
                                                    }
                                                }
                                            }
                                        }
                                        
                                        if (_columnWidthSaveTimer != null)
                                        {
                                            _columnWidthSaveTimer.Stop();
                                            _columnWidthSaveTimer.Start();
                                        }
                                    });
                                }
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }

                // 强制初始化列2和列3为固定宽度（不使用Star模式）
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ForceColumnWidthsToFixed();
                    // 更新列2操作按钮位置，使其居中对齐列2区域
                    UpdateActionButtonsPosition();
                    // 更新分隔符位置，使其与列1和列2之间的分割器对齐
                    UpdateSeparatorPosition();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                
                // 监听列宽度变化，更新按钮位置和分隔符位置
                this.SizeChanged += (s, e) =>
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateActionButtonsPosition();
                        UpdateSeparatorPosition();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                };
                
                // 监听GridSplitter拖拽，更新按钮位置和分隔符位置
                var splitters1 = RootGrid.Children.OfType<GridSplitter>().ToList();
                foreach (var splitter in splitters1)
                {
                    splitter.DragCompleted += (s, e) =>
                    {
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateActionButtonsPosition();
                            UpdateSeparatorPosition();
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    };
                }

                // 监听GridSplitter拖拽事件，实时限制最小宽度
                var splitters = FindVisualChildren<GridSplitter>(this).ToList();
                for (int i = 0; i < splitters.Count; i++)
                {
                    var splitter = splitters[i];
                    int splitterColumn = Grid.GetColumn(splitter);
                    
                    // 判断是哪个分割线：Column=1是列1和列2之间，Column=3是列2和列3之间
                    bool isRightSplitter = (splitterColumn == 3); // 列2和列3之间的分割线
                    
                    // 在拖拽开始时，记录当前宽度
                    double? savedCenterWidth = null;
                    double? savedRightWidth = null;
                    
                    splitter.DragStarted += (s2, e2) =>
                    {
                        // 设置拖拽状态标记，防止UpdateLayout影响DataGrid列宽
                        _isSplitterDragging = true;
                        
                        // 保存当前宽度
                        savedCenterWidth = ColCenter.ActualWidth;
                        savedRightWidth = ColRight.ActualWidth;
                        
                        Debug.WriteLine($"[DragStarted] Splitter Column={splitterColumn}, IsRightSplitter={isRightSplitter}");
                        Debug.WriteLine($"[DragStarted] 分割器拖拽开始，设置_isSplitterDragging=true");
                        Debug.WriteLine($"[DragStarted] ColCenter: IsStar={ColCenter.Width.IsStar}, ActualWidth={ColCenter.ActualWidth}, MinWidth={ColCenter.MinWidth}, Width.Value={ColCenter.Width.Value}");
                        Debug.WriteLine($"[DragStarted] ColRight: IsStar={ColRight.Width.IsStar}, ActualWidth={ColRight.ActualWidth}, MinWidth={ColRight.MinWidth}, Width.Value={ColRight.Width.Value}");
                        
                        // 强制将列2设置为固定宽度（不使用Star模式）
                        if (ColCenter.Width.IsStar && savedCenterWidth.HasValue)
                        {
                            double newWidth = Math.Max(ColCenter.MinWidth, savedCenterWidth.Value);
                            ColCenter.Width = new GridLength(newWidth);
                            Debug.WriteLine($"[DragStarted] ColCenter: 从Star模式改为固定宽度 {newWidth}");
                        }
                        
                        // 确保列3使用Star模式
                        if (!ColRight.Width.IsStar && savedRightWidth.HasValue)
                        {
                            // 如果列3不是Star模式，改为Star模式以允许调整
                            ColRight.Width = new GridLength(1, GridUnitType.Star);
                            Debug.WriteLine($"[DragStarted] ColRight: 改为Star模式，允许调整宽度");
                        }
                    };
                    
                    // 在拖拽过程中实时检查并限制最小宽度
                    splitter.DragDelta += (s2, e2) =>
                    {
                        // 设置拖拽状态标记
                        _isSplitterDragging = true;
                        
                        // 延迟检查，在GridSplitter改变列宽之后立即修复
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            double centerActual = ColCenter.ActualWidth;
                            double rightActual = ColRight.ActualWidth;
                            double minCenter = ColCenter.MinWidth;
                            double minRight = ColRight.MinWidth;
                            
                            Debug.WriteLine($"[DragDelta] HorizontalChange={e2.HorizontalChange}, IsRightSplitter={isRightSplitter}");
                            Debug.WriteLine($"[DragDelta] ColCenter: IsStar={ColCenter.Width.IsStar}, ActualWidth={centerActual}, MinWidth={minCenter}, Width.Value={ColCenter.Width.Value}");
                            Debug.WriteLine($"[DragDelta] ColRight: IsStar={ColRight.Width.IsStar}, ActualWidth={rightActual}, MinWidth={minRight}, Width.Value={ColRight.Width.Value}");
                            
                            bool needFix = false;
                            
                            // 检查并修复列2（中间列）
                            if (centerActual < minCenter)
                            {
                                // 如果列2小于最小宽度，设置为最小宽度
                                ColCenter.Width = new GridLength(minCenter);
                                needFix = true;
                                Debug.WriteLine($"[DragDelta] 修复列2: {centerActual} < {minCenter}, 设置为 {minCenter}");
                            }
                            else if (ColCenter.Width.IsStar)
                            {
                                // 如果列2是Star模式，改为固定宽度
                                double newWidth = Math.Max(minCenter, centerActual);
                                ColCenter.Width = new GridLength(newWidth);
                                needFix = true;
                                Debug.WriteLine($"[DragDelta] 列2从Star模式改为固定宽度: {newWidth}");
                            }
                            
                            // 只检查列3最小宽度，保持Star模式
                            if (rightActual < minRight)
                            {
                                // 如果列3小于最小宽度，设置为最小宽度（但保持Star模式）
                                // 需要改为固定宽度才能设置最小宽度
                                ColRight.Width = new GridLength(minRight);
                                needFix = true;
                                Debug.WriteLine($"[DragDelta] 修复列3最小宽度: {rightActual} < {minRight}, 设置为 {minRight}");
                            }
                            else if (!ColRight.Width.IsStar)
                            {
                                // 如果列3不是Star模式，改为Star模式以允许调整
                                ColRight.Width = new GridLength(1, GridUnitType.Star);
                                needFix = true;
                                Debug.WriteLine($"[DragDelta] 列3改为Star模式，允许调整宽度");
                            }
                            
                            // 拖拽过程中不调用UpdateLayout，避免影响DataGrid列宽
                            // Grid的列宽调整会自然生效，不需要强制更新布局
                            if (needFix)
                            {
                                Debug.WriteLine($"[DragDelta] 跳过UpdateLayout以避免重置DataGrid列宽");
                            }
                        }), System.Windows.Threading.DispatcherPriority.Input);
                    };
                    
                    // 拖拽结束后保存配置
                    splitter.DragCompleted += (s2, e2) => 
                    {
                        Debug.WriteLine($"[DragCompleted] 分割器拖拽结束，准备清理");
                        
                        // 延迟一点再清除标记，确保所有拖拽相关操作完成
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            System.Threading.Thread.Sleep(100);
                            
                            // 确保列2和列3最小宽度
                            double centerActual = ColCenter.ActualWidth;
                            double rightActual = ColRight.ActualWidth;
                            double minCenter = ColCenter.MinWidth;
                            double minRight = ColRight.MinWidth;
                            
                            Debug.WriteLine($"[DragCompleted] IsRightSplitter={isRightSplitter}");
                            Debug.WriteLine($"[DragCompleted] ColCenter: IsStar={ColCenter.Width.IsStar}, ActualWidth={centerActual}, MinWidth={minCenter}, Width.Value={ColCenter.Width.Value}");
                            Debug.WriteLine($"[DragCompleted] ColRight: IsStar={ColRight.Width.IsStar}, ActualWidth={rightActual}, MinWidth={minRight}, Width.Value={ColRight.Width.Value}");
                            
                            bool needFix = false;
                            if (centerActual < minCenter)
                            {
                                ColCenter.Width = new GridLength(minCenter);
                                needFix = true;
                                Debug.WriteLine($"[DragCompleted] 修复列2: {centerActual} < {minCenter}, 设置为 {minCenter}");
                            }
                            
                            // 只检查列3最小宽度，保持Star模式
                            if (rightActual < minRight)
                            {
                                ColRight.Width = new GridLength(minRight);
                                needFix = true;
                                Debug.WriteLine($"[DragCompleted] 修复列3最小宽度: {rightActual} < {minRight}, 设置为 {minRight}");
                            }
                            else if (!ColRight.Width.IsStar)
                            {
                                // 如果列3不是Star模式，改为Star模式使用剩余空间
                                ColRight.Width = new GridLength(1, GridUnitType.Star);
                                needFix = true;
                                Debug.WriteLine($"[DragCompleted] 列3改为Star模式，使用剩余空间");
                            }
                            
                            // 清除拖拽标记后再调用ForceColumnWidthsToFixed，允许UpdateLayout
                            _isSplitterDragging = false;
                            Debug.WriteLine($"[DragCompleted] 清除拖拽标记，调用ForceColumnWidthsToFixed");
                            
                            // 强制确保列2不是Star模式（现在可以安全调用UpdateLayout了）
                            ForceColumnWidthsToFixed();
                            
                            if (needFix)
                            {
                                Debug.WriteLine($"[DragCompleted] 调用AdjustColumnWidths重新分配");
                                AdjustColumnWidths();
                            }
                            
                            SaveCurrentConfig();
                            
                            Debug.WriteLine($"[DragCompleted] 分割器拖拽处理完成");
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    };
                }
            };

            // 根据窗口大小动态约束列宽（优先保持列3，再列2，最后列1）
            this.SizeChanged += (s, e) =>
            {
                // 防止小于三列最小宽度总和
                double minTotal = ColLeft.MinWidth + ColCenter.MinWidth + ColRight.MinWidth + 12;
                if (this.ActualWidth < minTotal)
                {
                    // 不要强制设置窗口宽度，否则可能影响窗口边缘调整
                    // 只在极端情况下设置MinWidth
                    if (this.WindowState == WindowState.Normal && this.Width < minTotal)
                    {
                        // 使用MinWidth而不是Width，避免干扰窗口边缘调整
                        this.MinWidth = minTotal;
                    }
                    return;
                }

                // 等待布局完成后再调整
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    AdjustColumnWidths();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
            
            // 禁用LayoutUpdated监听，避免性能问题
            // 列宽度调整已在SizeChanged和GridSplitter事件中处理，无需持续监听
            // this.LayoutUpdated += (s, e) => { ... };
            UpdateWindowStateUI();

            // 延迟保存配置，避免频繁保存
            // 使用成员变量而不是局部变量
            _saveTimer = null;
            Action delayedSave = () =>
            {
                if (_saveTimer != null)
                {
                    _saveTimer.Stop();
                }
                _saveTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _saveTimer.Tick += (s2, e2) =>
                {
                    _saveTimer.Stop();
                    SaveCurrentConfig();
                };
                _saveTimer.Start();
            };
            
            // 窗口大小改变时延迟保存配置
            this.SizeChanged += (s, e) => delayedSave();
            
            // 禁用RootGrid.LayoutUpdated监听，避免性能问题
            // 配置保存已在SizeChanged和GridSplitter事件中处理
            // if (this.RootGrid != null)
            // {
            //     this.RootGrid.LayoutUpdated += (s, e) => delayedSave();
            // }
            
            // 关闭时立即保存配置
                this.Closing += (s, e) => 
                {
                // 保存当前路径
                _config.LastPath = _currentPath;
                
                // 强制保存备注
                if (RightPanel != null)
                {
                    RightPanel.ForceSaveNotes();
                }
                
                // 停止所有定时器
                if (_saveTimer != null)
                {
                    _saveTimer.Stop();
                    _saveTimer = null;
                }
                if (_columnWidthSaveTimer != null)
                {
                    _columnWidthSaveTimer.Stop();
                    _columnWidthSaveTimer = null;
                }
                if (_periodicTimer != null)
                {
                    _periodicTimer.Stop();
                    _periodicTimer = null;
                }
                if (_layoutCheckTimer != null)
                {
                    _layoutCheckTimer.Stop();
                    _layoutCheckTimer = null;
                }
                
                // 停止文件监视器
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Dispose();
                    _fileWatcher = null;
                }
                
                // 停止刷新定时器
                if (_refreshDebounceTimer != null)
                {
                    _refreshDebounceTimer.Stop();
                    _refreshDebounceTimer = null;
                }
                
                // 取消文件夹大小计算任务（不等待，直接取消）
                if (_folderSizeCalculationCancellation != null)
                {
                    _folderSizeCalculationCancellation.Cancel();
                    _folderSizeCalculationCancellation.Dispose();
                    _folderSizeCalculationCancellation = null;
                }
                
                // 释放信号量（不等待，直接释放）
                if (_folderSizeCalculationSemaphore != null)
                {
                    _folderSizeCalculationSemaphore.Dispose();
                    _folderSizeCalculationSemaphore = null;
                }
                
                // 保存配置（不等待布局更新，避免阻塞）
                try
                {
                    SaveCurrentConfig();
                }
                catch { }
            };

            // 定期保存配置（作为备份）
            _periodicTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _periodicTimer.Tick += (s, e) => SaveCurrentConfig();
            _periodicTimer.Start();
        }

        private void ApplyConfig(AppConfig cfg)
        {
            try
            {
                _isApplyingConfig = true;

                if (cfg.IsMaximized)
                {
                    // 使用伪最大化而不是WindowState.Maximized，避免系统边距
                    _restoreBounds = new Rect(0, 0, cfg.WindowWidth, cfg.WindowHeight);
                    var wa = GetCurrentMonitorWorkAreaDIPs();
                    this.WindowState = WindowState.Normal;
                    this.Left = wa.Left;
                    this.Top = wa.Top;
                    this.Width = wa.Width;
                    this.Height = wa.Height;
                    _isPseudoMaximized = true;
                    this.ResizeMode = ResizeMode.NoResize;
                    
                    // 等待窗口加载完成后再移除边框
                    this.Loaded += (s, e) =>
                    {
                        if (_isPseudoMaximized)
                        {
                            var hwnd = new WindowInteropHelper(this).Handle;
                            var margins = new NativeMethods.MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
                            NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
                        }
                    };
                }
                else
                {
                    this.Width = cfg.WindowWidth;
                    this.Height = cfg.WindowHeight;
                    if (!double.IsNaN(cfg.WindowTop)) this.Top = cfg.WindowTop;
                    if (!double.IsNaN(cfg.WindowLeft)) this.Left = cfg.WindowLeft;
                    _isPseudoMaximized = false;
                    this.ResizeMode = ResizeMode.CanResize;
                }

                // 应用主Grid列宽
                if (this.RootGrid != null)
                {
                    // 列 0,2 分别对应 左/中；右侧自适应
                    // 确保设置的宽度不小于最小宽度
                    var leftWidth = Math.Max(ColLeft.MinWidth, cfg.LeftPanelWidth);
                    var centerWidth = Math.Max(ColCenter.MinWidth, cfg.MiddlePanelWidth);
                    
                    this.RootGrid.ColumnDefinitions[0].Width = new GridLength(leftWidth);
                    this.RootGrid.ColumnDefinitions[2].Width = new GridLength(centerWidth);
                    // 右侧(列4)使用*自适应，不设置固定宽度
                    
                    // 确保窗口最小宽度正确设置
                    var minTotalWidth = ColLeft.MinWidth + ColCenter.MinWidth + ColRight.MinWidth + 12;
                    if (this.MinWidth < minTotalWidth)
                    {
                        this.MinWidth = minTotalWidth;
                    }
                }
                
                // 重要：应用完配置后，立即调整列宽以确保MinWidth生效
                this.UpdateLayout();
                AdjustColumnWidths();
                // 确保列3的最小宽度约束生效
                EnsureColumnMinWidths();
            }
            catch { }
            finally
            {
                _isApplyingConfig = false;
            }
        }

        private void SaveCurrentConfig()
        {
            // 如果正在应用配置，不保存
            if (_isApplyingConfig) return;
            
            try
            {
                // 保存当前路径
                _config.LastPath = _currentPath;
                _config.IsMaximized = _isPseudoMaximized; // 使用_isPseudoMaximized而不是WindowState
                if (!_config.IsMaximized)
                {
                    _config.WindowWidth = this.Width;
                    _config.WindowHeight = this.Height;
                    _config.WindowTop = this.Top;
                    _config.WindowLeft = this.Left;
                }

                if (this.RootGrid != null && this.RootGrid.IsLoaded)
                {
                    // 强制更新布局
                    this.RootGrid.UpdateLayout();
                    
                    // 获取实际尺寸
                    var leftWidth = this.RootGrid.ColumnDefinitions[0].ActualWidth;
                    var middleWidth = this.RootGrid.ColumnDefinitions[2].ActualWidth;
                    var rightWidth = this.RootGrid.ColumnDefinitions[4].ActualWidth;
                    
                    // 只有当实际尺寸大于0时才保存
                    if (leftWidth > 0) _config.LeftPanelWidth = leftWidth;
                    if (middleWidth > 0) _config.MiddlePanelWidth = middleWidth;
                    // 右侧为自适应宽度，不保存
                }
                else if (this.RootGrid != null)
                {
                    // 如果Grid未加载，尝试使用Width值
                    var leftWidth = this.RootGrid.ColumnDefinitions[0].Width;
                    var middleWidth = this.RootGrid.ColumnDefinitions[2].Width;
                    var rightWidth = this.RootGrid.ColumnDefinitions[4].Width;
                    
                    if (leftWidth.IsAbsolute) _config.LeftPanelWidth = leftWidth.Value;
                    if (middleWidth.IsAbsolute) _config.MiddlePanelWidth = middleWidth.Value;
                    // 右侧为自适应宽度，不保存
                }

                ConfigManager.Save(_config);
            }
            catch { }
        }
        #region 导航功能
        private void UpdateActionButtons(string mode)
        {
            try
            {
                if (ActionButtonsContainer == null)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateActionButtons: ActionButtonsContainer is null");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"UpdateActionButtons: mode={mode}");
                
                // 清空现有按钮
                if (ActionButtonsContainer != null)
                {
                    ActionButtonsContainer.Children.Clear();
                }
                _currentActionButtons.Clear();
                _actionItems.Clear();
                
                // 根据模式获取对应的按钮列表
                StackPanel sourcePanel = null;
                switch (mode)
                {
                    case "Path":
                        sourcePanel = this.FindName("PathActionButtons") as StackPanel;
                        break;
                    case "Library":
                        sourcePanel = this.FindName("LibraryActionButtons") as StackPanel;
                        break;
                    case "Tag":
                        sourcePanel = this.FindName("TagActionButtons") as StackPanel;
                        break;
                }
                
                if (sourcePanel == null)
                {
                    System.Diagnostics.Debug.WriteLine($"UpdateActionButtons: sourcePanel is null for mode={mode}");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"UpdateActionButtons: sourcePanel found, Children.Count={sourcePanel.Children.Count}");
                
                // 从源面板中提取按钮和分隔符，保持原始顺序
                foreach (var child in sourcePanel.Children)
                {
                    if (child is Button btn)
                    {
                        // 创建可拖动按钮包装
                        var newBtn = new Button
                        {
                            Content = btn.Content,
                            Style = btn.Style,
                            Margin = new Thickness(0, 0, 4, 0)
                        };
                        
                        // 复制事件处理程序 - 通过按钮内容匹配对应的方法
                        RoutedEventHandler handler = GetClickHandlerByButtonName(btn);
                        if (handler != null)
                        {
                            newBtn.Click += handler;
                        }
                        
                        var draggableBtn = new DraggableButton
                        {
                            Button = newBtn,
                            ActionName = btn.Content?.ToString() ?? "",
                            ClickHandler = handler
                        };
                        
                        // 添加拖动支持（Ctrl+拖动）
                        draggableBtn.Button.PreviewMouseLeftButtonDown += (s, e) =>
                        {
                            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                            {
                                _draggingButton = draggableBtn;
                                _buttonDragStartPoint = e.GetPosition(ActionButtonsContainer);
                                _isDragging = false;
                                draggableBtn.Button.CaptureMouse();
                                e.Handled = false;
                            }
                        };
                        
                        draggableBtn.Button.PreviewMouseMove += (s, e) =>
                        {
                            if (_draggingButton == draggableBtn && draggableBtn.Button.IsMouseCaptured)
                            {
                                var currentPoint = e.GetPosition(ActionButtonsContainer);
                                var delta = currentPoint - _buttonDragStartPoint;
                                
                                if (!_isDragging && (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5))
                                {
                                    _isDragging = true;
                                    draggableBtn.Button.Cursor = Cursors.Hand;
                                }
                                
                                if (_isDragging)
                                {
                                    // 计算应该插入的位置（考虑分隔符）
                                    var insertIndex = GetInsertIndexWithSeparators(currentPoint, draggableBtn);
                                    if (insertIndex >= 0)
                                    {
                                        var currentItemIndex = _actionItems.FindIndex(item => item.Button == draggableBtn);
                                        if (currentItemIndex != insertIndex && insertIndex <= _actionItems.Count)
                                        {
                                            var item = _actionItems[currentItemIndex];
                                            _actionItems.RemoveAt(currentItemIndex);
                                            _actionItems.Insert(insertIndex, item);
                                            RefreshActionButtons();
                                            _buttonDragStartPoint = currentPoint;
                                        }
                                    }
                                }
                            }
                        };
                        
                        draggableBtn.Button.PreviewMouseLeftButtonUp += (s, e) =>
                        {
                            if (_draggingButton == draggableBtn)
                            {
                                if (draggableBtn.Button.IsMouseCaptured)
                                {
                                    draggableBtn.Button.ReleaseMouseCapture();
                                }
                                _draggingButton = null;
                                _isDragging = false;
                                draggableBtn.Button.Cursor = Cursors.Arrow;
                            }
                        };
                        
                        _currentActionButtons.Add(draggableBtn);
                        _actionItems.Add(new ActionItem { Button = draggableBtn });
                    }
                    else if (child is Separator sep)
                    {
                        // 创建新的分隔符实例（不能重用原分隔符）
                        // 增加组间间距，让分组更明显
                        var newSeparator = new Separator { Margin = new Thickness(16, 0, 16, 0) };
                        _actionItems.Add(new ActionItem { Separator = newSeparator });
                    }
                }
                
                RefreshActionButtons();
                System.Diagnostics.Debug.WriteLine($"UpdateActionButtons: completed, _actionItems.Count={_actionItems.Count}, ActionButtonsContainer.Children.Count={(ActionButtonsContainer?.Children.Count ?? 0)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateActionButtons error: {ex.Message}\n{ex.StackTrace}");
                // 出错时至少清空容器
                try
                {
                    if (ActionButtonsContainer != null)
                    {
                        ActionButtonsContainer.Children.Clear();
                    }
                }
                catch { }
            }
        }
        
        private int GetInsertIndexWithSeparators(System.Windows.Point point, DraggableButton draggingBtn)
        {
            // 计算插入位置，考虑按钮和分隔符
            if (ActionButtonsContainer == null) return -1;
            
            // 现在ActionButtonsContainer就是StackPanel，直接使用Children
            var container = ActionButtonsContainer;
            
            // 计算每个项目的位置（按钮或分隔符）
            double accumulatedX = 0;
            int itemIndex = 0;
            
            foreach (var element in container.Children.OfType<FrameworkElement>())
            {
                var width = element.ActualWidth > 0 ? element.ActualWidth : (element is Separator ? 16 : 80); // 分隔符默认宽度16
                    
                if (point.X < accumulatedX + width / 2)
                {
                    return itemIndex;
                }
                
                accumulatedX += width;
                itemIndex++;
            }
            
            return itemIndex; // 插入到最后
        }
        
        private RoutedEventHandler GetClickHandlerByButtonName(Button btn)
        {
            // 根据按钮内容匹配对应的事件处理程序
            string content = btn.Content?.ToString() ?? "";
            if (content.Contains("新建文件夹")) return NewFolder_Click;
            if (content.Contains("新建文件")) return NewFile_Click;
            if (content.Contains("新建库")) return AddLibrary_Click;
            if (content.Contains("添加到库")) return AddFileToLibrary_Click;
            if (content.Contains("管理库")) return ManageLibraries_Click;
            if (content.Contains("复制")) return Copy_Click;
            if (content.Contains("粘贴")) return Paste_Click;
            if (content.Contains("删除")) return Delete_Click;
            if (content.Contains("添加收藏")) return AddFavorite_Click;
            if (content.Contains("添加标签")) return AddTagToFile_Click;
            if (content.Contains("刷新")) return Refresh_Click;
            if (content.Contains("新建标签")) return NewTag_Click;
            if (content.Contains("编辑标签")) return ManageTags_Click;
            if (content.Contains("批量添加标签")) return BatchAddTags_Click;
            if (content.Contains("标签统计")) return TagStatistics_Click;
            return null;
        }
        
        private void RefreshActionButtons()
        {
            if (ActionButtonsContainer == null)
            {
                System.Diagnostics.Debug.WriteLine("RefreshActionButtons: ActionButtonsContainer is null");
                return;
            }
            
            try
            {
                // 清空两个容器（现在都是StackPanel）
                if (ActionButtonsContainer != null)
                {
                    ActionButtonsContainer.Children.Clear();
                }
                
                // 使用保存的按钮和分隔符顺序（支持拖动后的顺序）
                foreach (var item in _actionItems)
                {
                    if (item == null) continue;
                    
                    UIElement elementToAdd = null;
                    
                    if (item.IsSeparator)
                    {
                        if (item.Separator != null)
                        {
                            elementToAdd = item.Separator;
                        }
                    }
                    else
                    {
                        if (item.Button != null && item.Button.Button != null)
                        {
                            elementToAdd = item.Button.Button;
                        }
                    }
                    
                    // 只添加到标题栏的容器（列2的按钮已移除）
                    if (elementToAdd != null && ActionButtonsContainer != null)
                    {
                        ActionButtonsContainer.Children.Add(elementToAdd);
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"RefreshActionButtons: Completed. ActionButtonsContainer.Children.Count={(ActionButtonsContainer?.Children.Count ?? 0)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshActionButtons error: {ex.Message}\n{ex.StackTrace}");
                // 如果出错，至少清空容器，避免显示错误状态
                if (ActionButtonsContainer != null)
                {
                    ActionButtonsContainer.Children.Clear();
                }
            }
        }
        
        private StackPanel GetSourcePanelForCurrentMode()
        {
            string mode = _config?.LastNavigationMode ?? "Path";
            switch (mode)
            {
                case "Path":
                    return this.FindName("PathActionButtons") as StackPanel;
                case "Library":
                    return this.FindName("LibraryActionButtons") as StackPanel;
                case "Tag":
                    return this.FindName("TagActionButtons") as StackPanel;
                default:
                    return this.FindName("PathActionButtons") as StackPanel;
            }
        }
        
        private void SwitchNavigationMode(string mode)
        {
            // 隐藏所有导航内容（添加空值检查）
            if (NavPathContent != null) NavPathContent.Visibility = Visibility.Collapsed;
            if (NavLibraryContent != null) NavLibraryContent.Visibility = Visibility.Collapsed;
            if (NavTagContent != null) NavTagContent.Visibility = Visibility.Collapsed;
            
            // 切换到非库模式时清空当前库
            if (mode != "Library")
            {
                _currentLibrary = null;
            }
            
            // 根据模式显示对应内容和按钮
            switch (mode)
            {
                case "Path":
                    if (NavPathContent != null) NavPathContent.Visibility = Visibility.Visible;
                    if (NavLibraryContent != null) NavLibraryContent.Visibility = Visibility.Collapsed;
                    if (NavTagContent != null) NavTagContent.Visibility = Visibility.Collapsed;
                    UpdateActionButtons("Path");
                    
                    // 隐藏标签页面底部按钮
                    if (TagBottomButtons != null)
                    {
                        TagBottomButtons.Visibility = Visibility.Collapsed;
                    }
                    
                    // 延迟刷新列2按钮，确保容器已完全初始化
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        RefreshActionButtons();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                    if (FileBrowser != null) FileBrowser.TabsVisible = true;
                    // 切换到路径模式时，清除库的高亮
                    if (LibrariesListBox != null && LibrariesListBox.Items != null)
                    {
                        foreach (var item in LibrariesListBox.Items)
                        {
                            SetItemHighlight(LibrariesListBox, item, false);
                        }
                    }
                    // 从库切换到路径时，查找或创建标签页
                    // 延迟执行，确保控件已初始化
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (FileBrowser == null || FileBrowser.TabsPanelControl == null) return; // 确保控件已初始化
                        
                        if (string.IsNullOrEmpty(_currentPath))
                        {
                            // 查找第一个使用路径的标签页
                            PathTab matchingTab = _pathTabs.FirstOrDefault();
                            if (matchingTab != null && Directory.Exists(matchingTab.Path))
                            {
                                _currentPath = matchingTab.Path;
                                SwitchToTab(matchingTab);
                            }
                            else
                            {
                                // 如果没有标签页，创建新标签页，默认路径为桌面
                                _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                                CreateTab(_currentPath);
                            }
                        }
                        else
                        {
                            // 如果已有路径，查找或创建对应的标签页
                            PathTab existingTab = _pathTabs.FirstOrDefault(t => t.Path == _currentPath);
                            if (existingTab != null)
                            {
                                SwitchToTab(existingTab);
                            }
                            else
                            {
                                CreateTab(_currentPath);
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                    break;
                case "Library":
                    if (NavPathContent != null) NavPathContent.Visibility = Visibility.Collapsed;
                    if (NavLibraryContent != null) NavLibraryContent.Visibility = Visibility.Visible;
                    if (NavTagContent != null) NavTagContent.Visibility = Visibility.Collapsed;
                    UpdateActionButtons("Library");
                    
                    // 隐藏标签页面底部按钮
                    if (TagBottomButtons != null)
                    {
                        TagBottomButtons.Visibility = Visibility.Collapsed;
                    }
                    // 延迟刷新列2按钮，确保容器已完全初始化
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        RefreshActionButtons();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                    // 库模式下也显示标签页
                    if (FileBrowser != null) FileBrowser.TabsVisible = true;
                    // 切换到库模式时，恢复最后选中的库
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_currentLibrary == null && _config.LastLibraryId > 0)
                        {
                            var lastLibrary = DatabaseManager.GetLibrary(_config.LastLibraryId);
                            if (lastLibrary != null)
                            {
                                _currentLibrary = lastLibrary;
                                // 使用辅助方法确保选中状态正确显示
                                EnsureSelectedItemVisible(LibrariesListBox, lastLibrary);
                                // 高亮当前库（作为匹配当前库）
                                _libraryService.HighlightLibrary(lastLibrary);
                                // 确保文件列表被加载
                                _libraryService.LoadLibraryFiles(lastLibrary, 
                                    (path) => DatabaseManager.GetFolderSize(path),
                                    (bytes) => FormatFileSize(bytes));
                            }
                        }
                        else if (_currentLibrary != null)
                        {
                            // 如果已有当前库，高亮它
                            _libraryService.HighlightLibrary(_currentLibrary);
                        }
                        InitializeLibraryDragDrop();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                    break;
                case "Tag":
                    // 只有在 TagTrain 可用时才显示标签页面
                    if (App.IsTagTrainAvailable)
                    {
                        if (NavPathContent != null) NavPathContent.Visibility = Visibility.Collapsed;
                        if (NavLibraryContent != null) NavLibraryContent.Visibility = Visibility.Collapsed;
                        if (NavTagContent != null) NavTagContent.Visibility = Visibility.Visible;
                        UpdateActionButtons("Tag");
                        
                        // 显示标签页面底部按钮
                        if (TagBottomButtons != null)
                        {
                            TagBottomButtons.Visibility = Visibility.Visible;
                        }
                        
                        // 默认使用浏览模式
                        _tagClickMode = OoiMRR.Services.Tag.TagClickMode.Browse;
                        if (TagClickModeBtn != null)
                        {
                            TagClickModeBtn.Content = "👁";
                            TagClickModeBtn.ToolTip = "切换到编辑模式：显示完整TagTrain训练面板";
                        }
                        
                        // 延迟刷新列2按钮，确保容器已完全初始化
                        // 先确保UI元素已加载，再初始化
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            System.Diagnostics.Debug.WriteLine("SwitchNavigationMode(Tag): 开始初始化TagTrain面板");
                            RefreshActionButtons();
                            
                            // 确保NavTagContent已可见
                            if (NavTagContent != null && NavTagContent.Visibility != Visibility.Visible)
                            {
                                NavTagContent.Visibility = Visibility.Visible;
                            }
                            
                            // 切换到浏览模式
                            SwitchTagMode();
                            
                            // 延迟初始化，确保所有UI元素都已渲染
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                // TagTrain面板已在TagService.LoadTags中初始化
                                System.Diagnostics.Debug.WriteLine("SwitchNavigationMode(Tag): TagTrain面板初始化完成");
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                        if (FileBrowser != null) FileBrowser.TabsVisible = true; // 标签模式也显示标签页
                        
                        // 初始化地址栏（标签浏览模式）- 显示tag按钮
                        if (FileBrowser != null)
                        {
                            FileBrowser.AddressText = "";
                            FileBrowser.IsAddressReadOnly = true;
                            FileBrowser.SetTagBreadcrumb("标签");
                        }
                    }
                    else
                    {
                        // TagTrain 不可用，切换到路径模式
                        SwitchNavigationMode("Path");
                    }
                    break;
            }
            
            // 保存当前模式
            _config.LastNavigationMode = mode;
            ConfigManager.Save(_config);
            
            // 应用可见列设置并确保右键菜单绑定
            ApplyVisibleColumnsForCurrentMode();
            EnsureHeaderContextMenuHook();
            
            // 更新文件列表
            RefreshFileList();
        }
        
        private void NavPathBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchNavigationMode("Path");
        }
        
        private void NavLibraryBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchNavigationMode("Library");
        }
        
        private void NavTagBtn_Click(object sender, RoutedEventArgs e)
        {
            // 只有在 TagTrain 可用时才切换到标签模式
            if (App.IsTagTrainAvailable)
            {
                SwitchNavigationMode("Tag");
            }
            else
            {
                MessageBox.Show("TagTrain 不可用，无法使用标签功能。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        
        private void RefreshFileList()
        {
            // 根据当前导航模式刷新文件列表
            if (NavTagContent != null && NavTagContent.Visibility == Visibility.Visible)
            {
                // 标签模式：使用TagTrain面板，如果有选中的标签，显示该标签的文件；否则清空文件列表
                if (_currentTagFilter != null)
                {
                    FilterByTag(_currentTagFilter);
                }
                else
                {
                    // 没有选中标签，清空文件列表
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = null;
                    }
                    HideEmptyStateMessage();
                }
            }
            else if (_currentLibrary != null)
            {
                // 库模式：刷新库文件
                _libraryService.LoadLibraryFiles(_currentLibrary,
                    (path) => DatabaseManager.GetFolderSize(path),
                    (bytes) => FormatFileSize(bytes));
            }
            else if (!string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath))
            {
                // 路径模式：加载目录
                LoadCurrentDirectory();
            }
            else
            {
                // 如果是库模式但没有当前库，尝试恢复最后选中的库
                if (NavLibraryContent != null && NavLibraryContent.Visibility == Visibility.Visible)
                {
                    if (_config.LastLibraryId > 0)
                    {
                        var lastLibrary = DatabaseManager.GetLibrary(_config.LastLibraryId);
                        if (lastLibrary != null)
                        {
                            _currentLibrary = lastLibrary;
                            // 使用辅助方法确保选中状态正确显示
                            EnsureSelectedItemVisible(LibrariesListBox, lastLibrary);
                            _libraryService.LoadLibraryFiles(lastLibrary,
                                (path) => DatabaseManager.GetFolderSize(path),
                                (bytes) => FormatFileSize(bytes));
                            return;
                        }
                    }
                }
                
                // 其他模式：清空列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                HideEmptyStateMessage();
            }
        }
        
        #endregion

        #region 辅助方法：确保选中状态正确显示
        
        /// <summary>
        /// 确保 ListBox 的选中项正确显示（强制刷新视觉状态）
        /// </summary>
        private void EnsureSelectedItemVisible(ListBox listBox, object selectedItem)
        {
            if (listBox == null || selectedItem == null) return;
            
            try
            {
                // 先更新布局，确保容器已生成
                listBox.UpdateLayout();
                
                // 设置选中项
                listBox.SelectedItem = selectedItem;
                
                // 等待容器生成
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 获取容器并强制刷新视觉状态
                        var container = listBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
                        if (container != null)
                        {
                            // 检查是否是匹配高亮（优先级最高，不应清除）
                            var tag = container.Tag as string;
                            bool isMatch = (tag == "Match");
                            
                            // 只清除拖拽高亮（半透明），保留匹配高亮（黄色）
                            if (!isMatch)
                            {
                                var localBg = container.Background as SolidColorBrush;
                                // 只清除拖拽高亮（半透明背景）
                                if (localBg != null && localBg.Color.A < 255)
                                {
                                    container.ClearValue(ListBoxItem.BackgroundProperty);
                                    container.ClearValue(ListBoxItem.ForegroundProperty);
                                    container.ClearValue(ListBoxItem.BorderBrushProperty);
                                }
                            }
                            
                            // 强制刷新视觉状态
                            container.InvalidateVisual();
                            container.UpdateLayout();
                            
                            // 滚动到选中项
                            container.BringIntoView();
                        }
                        else
                        {
                            // 如果容器还未生成，稍后重试
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var retryContainer = listBox.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListBoxItem;
                                if (retryContainer != null)
                                {
                                    // 检查是否是匹配高亮
                                    var tag = retryContainer.Tag as string;
                                    bool isMatch = (tag == "Match");
                                    
                                    // 如果不是匹配，清除拖拽高亮
                                    if (!isMatch)
                                    {
                                        var localBg = retryContainer.Background as SolidColorBrush;
                                        if (localBg != null && localBg.Color.A < 255)
                                        {
                                            retryContainer.ClearValue(ListBoxItem.BackgroundProperty);
                                            retryContainer.ClearValue(ListBoxItem.ForegroundProperty);
                                            retryContainer.ClearValue(ListBoxItem.BorderBrushProperty);
                                        }
                                    }
                                    retryContainer.InvalidateVisual();
                                    retryContainer.UpdateLayout();
                                    retryContainer.BringIntoView();
                                }
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"刷新选中项视觉状态失败: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"确保选中项可见失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 确保 ListView 的选中项正确显示（强制刷新视觉状态）
        /// </summary>
        private void EnsureSelectedItemVisible(ListView listView, object selectedItem)
        {
            if (listView == null || selectedItem == null) return;
            
            try
            {
                // 先更新布局，确保容器已生成
                listView.UpdateLayout();
                
                // 设置选中项
                listView.SelectedItem = selectedItem;
                
                // 等待容器生成
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 获取容器并强制刷新视觉状态
                        var container = listView.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListViewItem;
                        if (container != null)
                        {
                            // 强制刷新视觉状态
                            container.InvalidateVisual();
                            container.UpdateLayout();
                            
                            // 滚动到选中项
                            container.BringIntoView();
                        }
                        else
                        {
                            // 如果容器还未生成，稍后重试
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                var retryContainer = listView.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListViewItem;
                                if (retryContainer != null)
                                {
                                    retryContainer.InvalidateVisual();
                                    retryContainer.UpdateLayout();
                                    retryContainer.BringIntoView();
                                }
                            }), System.Windows.Threading.DispatcherPriority.Loaded);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"刷新选中项视觉状态失败: {ex.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"确保选中项可见失败: {ex.Message}");
            }
        }
        
        #endregion

        #region 导航功能

        private void LoadCurrentDirectory()
        {
            try
            {
                // 路径页使用文件浏览控件
                if (FileBrowser != null)
                {
                    FileBrowser.AddressText = _currentPath;
                    FileBrowser.IsAddressReadOnly = false;
                    FileBrowser.UpdateBreadcrumb(_currentPath);
                }
                LoadFiles();
                SetupFileWatcher(_currentPath);
                
                // 高亮匹配当前路径的列表项
                HighlightMatchingItems(_currentPath);
                
                // 隐藏空状态提示
                HideEmptyStateMessage();
            }
            catch (UnauthorizedAccessException ex)
            {
                // 友好的错误消息
                string errorMessage = $"无法访问路径: {_currentPath}\n\n";
                if (ex.Message.Contains("Access to the path") && ex.Message.Contains("is denied"))
                {
                    errorMessage += "访问被拒绝。请检查文件夹权限或尝试以管理员身份运行程序。";
                }
                else
                {
                    errorMessage += ex.Message;
                }
                
                MessageBox.Show(errorMessage, "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                // 清空文件列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                ShowEmptyStateMessage($"无法访问此路径：\n{_currentPath}");
            }
            catch (DirectoryNotFoundException ex)
            {
                MessageBox.Show($"路径不存在: {_currentPath}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                // 清空文件列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                ShowEmptyStateMessage($"路径不存在：\n{_currentPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法加载目录: {_currentPath}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                // 清空文件列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                ShowEmptyStateMessage($"加载失败：\n{ex.Message}");
            }
        }
        // HighlightMatchingLibrary() 已迁移到 LibraryService
        
        /// <summary>
        /// 高亮匹配当前路径的列表项（驱动器、快速访问、收藏）
        /// </summary>
        private void HighlightMatchingItems(string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath)) return;
            
            string normalizedPath = currentPath.TrimEnd('\\', '/');
            if (string.IsNullOrEmpty(normalizedPath)) normalizedPath = currentPath;
            
            // 先清除所有高亮，确保不会出现多个列表同时高亮同一路径
            ClearItemHighlights();
            
            object driveMatch = null;
            object quickMatch = null;
            object favoriteMatch = null;
            
            // 查找驱动器匹配项
            if (DrivesListBox != null && DrivesListBox.Items != null)
            {
                foreach (var item in DrivesListBox.Items)
                {
                    var pathProperty = item.GetType().GetProperty("Path");
                    if (pathProperty != null)
                    {
                        var drivePath = pathProperty.GetValue(item) as string;
                        if (!string.IsNullOrEmpty(drivePath))
                        {
                            string normalizedDrive = drivePath.TrimEnd('\\', '/');
                            if (normalizedPath.Equals(normalizedDrive, StringComparison.OrdinalIgnoreCase))
                            {
                                driveMatch = item;
                                break;
                            }
                        }
                    }
                }
            }
            
            // 查找快速访问匹配项
            if (QuickAccessListBox != null && QuickAccessListBox.Items != null)
            {
                foreach (var item in QuickAccessListBox.Items)
                {
                    var pathProperty = item.GetType().GetProperty("Path");
                    if (pathProperty != null)
                    {
                        var accessPath = pathProperty.GetValue(item) as string;
                        if (!string.IsNullOrEmpty(accessPath))
                        {
                            string normalizedAccess = accessPath.TrimEnd('\\', '/');
                            if (normalizedPath.Equals(normalizedAccess, StringComparison.OrdinalIgnoreCase))
                            {
                                quickMatch = item;
                                break;
                            }
                        }
                    }
                }
            }
            
            // 查找收藏匹配项
            if (FavoritesListBox != null && FavoritesListBox.Items != null)
            {
                foreach (var item in FavoritesListBox.Items)
                {
                    var pathProp = item?.GetType().GetProperty("Path");
                    var pathVal = pathProp != null ? (pathProp.GetValue(item) as string) : null;
                    if (!string.IsNullOrEmpty(pathVal))
                    {
                        if (normalizedPath.Equals(pathVal.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                        {
                            favoriteMatch = item;
                            break;
                        }
                    }
                    else if (item is Favorite favorite && !string.IsNullOrEmpty(favorite.Path))
                    {
                        if (normalizedPath.Equals(favorite.Path.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                        {
                            favoriteMatch = item;
                            break;
                        }
                    }
                }
            }
            
            // 决定高亮的唯一来源
            string source = _lastLeftNavSource;
            if (source == "Drive" && driveMatch != null)
            {
                SetItemHighlight(DrivesListBox, driveMatch, true);
                return;
            }
            if (source == "QuickAccess" && quickMatch != null)
            {
                SetItemHighlight(QuickAccessListBox, quickMatch, true);
                return;
            }
            if (source == "Favorites" && favoriteMatch != null)
            {
                SetItemHighlight(FavoritesListBox, favoriteMatch, true);
                return;
            }
            
            // 默认优先级：快速访问 > 收藏 > 驱动器
            if (quickMatch != null)
            {
                SetItemHighlight(QuickAccessListBox, quickMatch, true);
            }
            else if (favoriteMatch != null)
            {
                SetItemHighlight(FavoritesListBox, favoriteMatch, true);
            }
            else if (driveMatch != null)
            {
                SetItemHighlight(DrivesListBox, driveMatch, true);
            }
        }
        
        /// <summary>
        /// 清除所有列表项的高亮状态
        /// </summary>
        private void ClearItemHighlights()
        {
            ClearListBoxHighlights(DrivesListBox);
            ClearListBoxHighlights(QuickAccessListBox);
            ClearListBoxHighlights(FavoritesListBox);
        }
        
        /// <summary>
        /// 清除指定列表的所有高亮
        /// </summary>
        private void ClearListBoxHighlights(ListBox listBox)
        {
            if (listBox == null || listBox.Items == null) return;
            
            foreach (var item in listBox.Items)
            {
                SetItemHighlight(listBox, item, false);
            }
        }
        
        /// <summary>
        /// 设置列表项的高亮状态（匹配高亮 - 优先级最高）
        /// 无论是否选中，匹配当前路径/库的项都显示为黄色，让用户知道当前位置
        /// 这是统一的高亮函数，用于路径匹配和库匹配
        /// </summary>
        private void SetItemHighlight(ListBox listBox, object item, bool highlight)
        {
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
                        
                        // 匹配当前路径/库：无论是否选中，都显示黄色（优先级最高）
                        var yellowBg = this.FindResource("HighlightBrush") as SolidColorBrush;
                        var blackFg = this.FindResource("HighlightForegroundBrush") as SolidColorBrush;
                        var orangeBorder = this.FindResource("HighlightBorderBrush") as SolidColorBrush;
                        
                        container.ClearValue(ListBoxItem.BackgroundProperty);
                        container.ClearValue(ListBoxItem.ForegroundProperty);
                        container.ClearValue(ListBoxItem.BorderBrushProperty);
                        container.SetValue(ListBoxItem.BackgroundProperty, yellowBg);
                        container.SetValue(ListBoxItem.ForegroundProperty, blackFg);
                        container.SetValue(ListBoxItem.BorderBrushProperty, orangeBorder);
                        container.Tag = "Match";
                    }
                    else
                    {
                        // 清除匹配高亮
                        var tag = container.Tag as string;
                        if (tag == "Match")
                        {
                            container.Tag = null;
                            container.ClearValue(ListBoxItem.BackgroundProperty);
                            container.ClearValue(ListBoxItem.ForegroundProperty);
                            container.ClearValue(ListBoxItem.BorderBrushProperty);
                        }
                    }
                }
                else
                {
                    // 如果容器还未生成，延迟执行（使用低优先级避免阻塞UI）
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var retryContainer = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                        if (retryContainer != null)
                        {
                            if (highlight)
                            {
                                if (retryContainer.Tag as string == "Match")
                                    return;
                                
                                var yellowBg = this.FindResource("HighlightBrush") as SolidColorBrush;
                                var blackFg = this.FindResource("HighlightForegroundBrush") as SolidColorBrush;
                                var orangeBorder = this.FindResource("HighlightBorderBrush") as SolidColorBrush;
                                
                                retryContainer.ClearValue(ListBoxItem.BackgroundProperty);
                                retryContainer.ClearValue(ListBoxItem.ForegroundProperty);
                                retryContainer.ClearValue(ListBoxItem.BorderBrushProperty);
                                retryContainer.SetValue(ListBoxItem.BackgroundProperty, yellowBg);
                                retryContainer.SetValue(ListBoxItem.ForegroundProperty, blackFg);
                                retryContainer.SetValue(ListBoxItem.BorderBrushProperty, orangeBorder);
                                retryContainer.Tag = "Match";
                            }
                            else
                            {
                                var tag = retryContainer.Tag as string;
                                if (tag == "Match")
                                {
                                    retryContainer.Tag = null;
                                    retryContainer.ClearValue(ListBoxItem.BackgroundProperty);
                                    retryContainer.ClearValue(ListBoxItem.ForegroundProperty);
                                    retryContainer.ClearValue(ListBoxItem.BorderBrushProperty);
                                }
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置列表项高亮失败: {ex.Message}");
            }
        }

        private void SetupFileWatcher(string path)
        {
            // 停止并释放旧的监视器
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            // 初始化防抖定时器（只初始化一次）
            if (_refreshDebounceTimer == null)
            {
                _refreshDebounceTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(3000) // 增加到3秒防抖延迟，大幅减少CPU占用和文件系统监控频率
                };
                _refreshDebounceTimer.Tick += (s, e) =>
                {
                    _refreshDebounceTimer.Stop();
                    try
                    {
                        // 检查是否正在加载，避免重复加载
                        if (!_isLoadingFiles)
                        {
                            System.Diagnostics.Debug.WriteLine("自动刷新文件列表...");
                            LoadFiles();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"自动刷新失败: {ex.Message}");
                    }
                };
            }

            try
            {
                // 创建新的文件系统监视器
                // 只监控文件名和目录名变化，减少事件触发频率
                _fileWatcher = new FileSystemWatcher
                {
                    Path = path,
                    NotifyFilter = NotifyFilters.FileName | 
                                   NotifyFilters.DirectoryName,  // 移除LastWrite和Size，减少事件频率
                    Filter = "*.*",
                    IncludeSubdirectories = false,
                    InternalBufferSize = 8192  // 设置缓冲区大小，减少事件丢失
                };

                // 文件创建事件
                _fileWatcher.Created += OnFileSystemChanged;
                // 文件删除事件
                _fileWatcher.Deleted += OnFileSystemChanged;
                // 文件重命名事件
                _fileWatcher.Renamed += OnFileSystemChanged;
                // 文件修改事件
                _fileWatcher.Changed += OnFileSystemChanged;

                // 启用监视
                _fileWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"无法设置文件监视器: {ex.Message}");
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 使用防抖机制，避免频繁刷新
            // 如果正在加载文件，跳过本次事件，避免循环触发
            if (_isLoadingFiles)
            {
                return;
            }
            
            // 移除Debug输出，避免性能损耗
            // System.Diagnostics.Debug.WriteLine($"文件系统变化: {e.ChangeType} - {e.Name}");
            
            // 使用最低优先级，避免影响其他操作
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 再次检查是否正在加载
                if (!_isLoadingFiles && _refreshDebounceTimer != null)
                {
                    _refreshDebounceTimer.Stop();
                    _refreshDebounceTimer.Start();
                }
            }), System.Windows.Threading.DispatcherPriority.SystemIdle);
        }

        private void UpdateAddressBar(string text)
        {
            if (FileBrowser != null)
            {
                FileBrowser.AddressText = text;
            }
        }

        private void UpdateBreadcrumb(string text)
        {
            if (FileBrowser != null)
            {
                FileBrowser.UpdateBreadcrumbText(text);
            }
        }

        #region 标签页管理

        /// <summary>
        /// 创建新标签页
        /// </summary>
        private void CreateTab(string path, bool forceNewTab = false)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    MessageBox.Show($"路径不存在: {path}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"无法访问路径: {path}\n\n{ex.Message}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法访问路径: {path}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            if (FileBrowser == null || FileBrowser.TabsPanelControl == null) return; // 确保控件已初始化

            // 如果强制新标签页，直接创建新标签页
            if (forceNewTab)
            {
                var tab = new PathTab
                {
                    Type = TabType.Path,
                    Path = path,
                    Title = GetPathDisplayTitle(path)
                };
                CreateTabInternal(tab);
                return;
            }

            // 检查是否已存在该路径的标签页
            var existingTab = _pathTabs.FirstOrDefault(t => t.Type == TabType.Path && t.Path == path);
            if (existingTab != null)
            {
                SwitchToTab(existingTab);
                return;
            }

            var newTab = new PathTab
            {
                Type = TabType.Path,
                Path = path,
                Title = GetPathDisplayTitle(path)
            };
            
            CreateTabInternal(newTab);
        }
        
        /// <summary>
        /// 在标签页中打开库
        /// </summary>
        private void OpenLibraryInTab(Library library, bool forceNewTab = false)
        {
            if (library == null) return;
            
            // 如果强制新标签页，直接创建新标签页
            if (forceNewTab)
            {
                var tab = new PathTab
                {
                    Type = TabType.Library,
                    Path = library.Name,  // 库标签页使用库名称作为Path标识
                    Title = library.Name,
                    Library = library
                };
                CreateTabInternal(tab);
                return;
            }
            
            // 检查是否已存在该库的标签页
            var existingTab = _pathTabs.FirstOrDefault(t => t.Type == TabType.Library && t.Library != null && t.Library.Id == library.Id);
            if (existingTab != null)
            {
                SwitchToTab(existingTab);
                return;
            }
            
            var newTab = new PathTab
            {
                Type = TabType.Library,
                Path = library.Name,  // 库标签页使用库名称作为Path标识
                Title = library.Name,
                Library = library
            };
            
            CreateTabInternal(newTab);
        }
        
        private void OpenTagInTab(Tag tag, bool forceNewTab = false)
        {
            if (tag == null || string.IsNullOrWhiteSpace(tag.Name)) return;

            // 如果强制新标签页，直接创建新标签页
            if (forceNewTab)
            {
                var tab = new PathTab
                {
                    Type = TabType.Tag,
                    Path = $"tag://{tag.Id}",
                    Title = tag.Name,
                    TagId = tag.Id,
                    TagName = tag.Name
                };
                CreateTabInternal(tab);
                return;
            }

            // 1. 查找是否已存在该标签的标签页（按标签ID）
            var existingTab = _pathTabs.FirstOrDefault(t => t.Type == TabType.Tag && t.TagId == tag.Id);
            if (existingTab != null)
            {
                SwitchToTab(existingTab);
                return;
            }

            // 2. 如果没有同名标签页，但当前标签页是tag页，用当前页打开
            if (_activeTab != null && _activeTab.Type == TabType.Tag)
            {
                // 更新当前标签页的tag信息
                _activeTab.TagId = tag.Id;
                _activeTab.TagName = tag.Name;
                _activeTab.Path = $"tag://{tag.Id}";
                _activeTab.Title = tag.Name;
                
                // 更新标签页标题显示
                if (_activeTab.TitleTextBlock != null)
                {
                    _activeTab.TitleTextBlock.Text = tag.Name;
                }
                if (_activeTab.TabButton != null)
                {
                    _activeTab.TabButton.ToolTip = tag.Name;
                }
                
                // 切换到该标签
                SwitchToTab(_activeTab);
                return;
            }

            // 3. 如果都没有，打开一个新标签页
            var newTab = new PathTab
            {
                Type = TabType.Tag,
                Path = $"tag://{tag.Id}",
                Title = tag.Name,
                TagId = tag.Id,
                TagName = tag.Name
            };

            CreateTabInternal(newTab);
        }
        
        /// <summary>
        /// 创建标签页的内部实现（统一处理库和路径）
        /// </summary>
        private void CreateTabInternal(PathTab tab)
        {
            if (FileBrowser == null || FileBrowser.TabsPanelControl == null) return;

            // 创建标签按钮容器
            var tabContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 2, 0)
            };

            // 创建标签文本（靠左，支持省略号）
            var titleText = new TextBlock
            {
                Text = tab.Title,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // 创建关闭按钮文本（居中显示，与标题重叠）
            var closeButtonText = new TextBlock
            {
                Text = "×",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Tag = tab,
                Opacity = 0.0,  // 初始不可见
                Cursor = Cursors.Hand
            };
            
            // 关闭按钮的点击区域（使用Border作为点击目标，固定在右侧）
            var closeButton = new Border
            {
                Width = 24,
                Height = 24,
                Background = System.Windows.Media.Brushes.Transparent,
                Tag = tab,
                Cursor = Cursors.Hand,
                Child = closeButtonText,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(4, 0, 0, 0)
            };
            
            closeButton.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (s is Border border && border.Tag is PathTab tabToClose)
                {
                    CloseTab(tabToClose);
                }
            };
            
            closeButton.MouseEnter += (s, e) =>
            {
                if (s is Border border && border.Child is TextBlock textBlock)
                {
                    textBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDC, 0x35, 0x45)); // 悬停时变红色
                }
            };
            
            closeButton.MouseLeave += (s, e) =>
            {
                if (s is Border border && border.Child is TextBlock textBlock)
                {
                    // 根据标签状态设置颜色
                    var tabToCheck = border.Tag as PathTab;
                    if (tabToCheck != null && tabToCheck == _activeTab)
                    {
                        textBlock.Foreground = System.Windows.Media.Brushes.White; // 活动标签使用白色
                    }
                    else
                    {
                        textBlock.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6C, 0x75, 0x7D)); // 非活动标签使用灰色
                    }
                }
            };

            // 使用Grid作为按钮内容，左侧文本，右侧关闭按钮
            var buttonContent = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(titleText, 0);
            Grid.SetColumn(closeButton, 1);
            buttonContent.Children.Add(titleText);
            buttonContent.Children.Add(closeButton);

            // 创建标签按钮
            var button = new Button
            {
                Content = buttonContent,
                Style = (Style)FindResource("TabButtonStyle"),
                Tag = tab,
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0)
            };
            button.Click += TabButton_Click;

            // 启动拖拽：记录按下位置并在移动超过阈值时开始拖拽
            button.PreviewMouseLeftButtonDown += (s, e) =>
            {
                _tabDragStartPoint = e.GetPosition(null);
                _draggingTab = tab;
            };
            button.MouseMove += (s, e) =>
            {
                if (_draggingTab == tab && e.LeftButton == MouseButtonState.Pressed)
                {
                    var pos = e.GetPosition(null);
                    if (Math.Abs(pos.X - _tabDragStartPoint.X) > 4 || Math.Abs(pos.Y - _tabDragStartPoint.Y) > 4)
                    {
                        var data = new DataObject();
                        data.SetData("OoiMRR_TabKey", GetTabKey(tab));
                        data.SetData("OoiMRR_TabPinned", tab.IsPinned);
                        DragDrop.DoDragDrop(button, data, DragDropEffects.Move);
                        _draggingTab = null;
                    }
                }
            };
            button.PreviewMouseLeftButtonUp += (s, e) => { _draggingTab = null; };

            var cm = new ContextMenu();
            var pinItem = new MenuItem { Header = "固定此标签页" };
            pinItem.Click += (s, e) => TogglePinTab(tab);
            var renameItem = new MenuItem { Header = "重命名显示标题" };
            renameItem.Click += (s, e) => RenameDisplayTitle(tab);
            cm.Items.Add(pinItem);
            cm.Items.Add(renameItem);
            cm.Opened += (s, e) => { pinItem.Header = tab.IsPinned ? "取消固定此标签页" : "固定此标签页"; };
            button.ContextMenu = cm;
            
            // 添加中键点击关闭标签页
            button.PreviewMouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    if (s is Button btn && btn.Tag is PathTab tabToClose)
                    {
                        CloseTab(tabToClose);
                        e.Handled = true;
                    }
                }
            };

            // 设置关闭按钮的初始颜色（根据标签状态）
            if (tab == _activeTab)
            {
                closeButtonText.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                closeButtonText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6C, 0x75, 0x7D));
            }
            
            // 创建动画对象用于平滑过渡
            var fadeInAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            
            var fadeOutAnimation = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut }
            };
            
            // 鼠标悬停时显示关闭按钮，标题不隐藏
            button.MouseEnter += (s, e) => 
            {
                closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };
            
            button.MouseLeave += (s, e) => 
            {
                // 检查鼠标是否还在按钮区域内
                var btn = button;
                var mousePos = Mouse.GetPosition(btn);
                if (mousePos.X < 0 || mousePos.Y < 0 || mousePos.X > btn.ActualWidth || mousePos.Y > btn.ActualHeight)
                {
                    closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                }
            };

            
            // 关闭按钮区域也要支持鼠标悬停
            closeButton.MouseEnter += (s, e) => 
            {
                closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };
            
            closeButton.MouseLeave += (s, e) =>
            {
                var btn = button;
                var mousePos = Mouse.GetPosition(btn);
                if (mousePos.X < 0 || mousePos.Y < 0 || mousePos.X > btn.ActualWidth || mousePos.Y > btn.ActualHeight)
                {
                    closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                }
            };


            tabContainer.Children.Add(button);
            
            // 保存关闭按钮引用以便后续访问
            tab.CloseButton = closeButton;
            tab.TitleTextBlock = titleText;
            tab.TabContainer = tabContainer;

            tab.TabButton = button;
            _pathTabs.Add(tab);

            // 添加到面板
            if (FileBrowser != null && FileBrowser.TabsPanelControl != null)
            {
                FileBrowser.TabsPanelControl.Children.Add(tabContainer);
            }

            ApplyTabOverrides(tab);
            ApplyPinVisual(tab);
            ReorderTabs();

            // 切换到新标签页
            SwitchToTab(tab);
        }

        private void InitializeTabsDragDrop()
        {
            try
            {
                if (FileBrowser?.TabsPanelControl == null) return;
                var panel = FileBrowser.TabsPanelControl;
                panel.AllowDrop = true;
                panel.DragOver -= TabsPanel_DragOver;
                panel.Drop -= TabsPanel_Drop;
                panel.DragOver += TabsPanel_DragOver;
                panel.Drop += TabsPanel_Drop;
            }
            catch { }
        }

        private void TabsPanel_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("OoiMRR_TabKey"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void TabsPanel_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent("OoiMRR_TabKey")) return;
                var key = e.Data.GetData("OoiMRR_TabKey") as string;
                if (string.IsNullOrEmpty(key) || FileBrowser?.TabsPanelControl == null) return;
                var tab = _pathTabs.FirstOrDefault(t => GetTabKey(t) == key);
                if (tab == null) return;

                var panel = FileBrowser.TabsPanelControl;
                var mousePos = e.GetPosition(panel);
                var children = panel.Children.OfType<StackPanel>().ToList();
                int targetIndex = 0;
                for (int i = 0; i < children.Count; i++)
                {
                    var child = children[i] as FrameworkElement;
                    if (child == null) continue;
                    var pos = child.TransformToAncestor(panel).Transform(new System.Windows.Point(0, 0));
                    double mid = pos.X + child.ActualWidth / 2;
                    if (mousePos.X > mid) targetIndex = i + 1;
                }

                int pinnedCount = _pathTabs.Count(t => t.IsPinned);
                if (tab.IsPinned) targetIndex = Math.Min(targetIndex, pinnedCount);
                else targetIndex = Math.Max(targetIndex, pinnedCount);

                int currentIndex = children.IndexOf(tab.TabContainer);
                if (currentIndex == targetIndex) return;

                var pinned = _pathTabs.Where(t => t.IsPinned).ToList();
                var unpinned = _pathTabs.Where(t => !t.IsPinned).ToList();

                if (tab.IsPinned)
                {
                    pinned.Remove(tab);
                    pinned.Insert(targetIndex, tab);
                    _config.PinnedTabs = pinned.Select(t => GetTabKey(t)).ToList();
                    ConfigManager.Save(_config);
                    _pathTabs = pinned.Concat(unpinned).ToList();
                }
                else
                {
                    int unTarget = Math.Max(0, targetIndex - pinnedCount);
                    int unCurrent = unpinned.IndexOf(tab);
                    if (unCurrent == -1) return;
                    unpinned.Remove(tab);
                    if (unTarget > unpinned.Count) unTarget = unpinned.Count;
                    unpinned.Insert(unTarget, tab);
                    _pathTabs = pinned.Concat(unpinned).ToList();
                }

                ReorderTabs();
                UpdateTabStyles();
            }
            catch { }
        }

        private string GetTabKey(PathTab tab)
        {
            if (tab.Type == TabType.Path) return "path:" + (tab.Path ?? string.Empty);
            if (tab.Type == TabType.Library) return "library:" + (tab.Library?.Id.ToString() ?? "");
            if (tab.Type == TabType.Tag) return "tag:" + tab.TagId.ToString();
            return "unknown:" + (tab.Title ?? "");
        }

        private void ApplyTabOverrides(PathTab tab)
        {
            var key = GetTabKey(tab);
            if (_config.TabTitleOverrides != null && _config.TabTitleOverrides.TryGetValue(key, out var overrideTitle) && !string.IsNullOrWhiteSpace(overrideTitle))
            {
                tab.OverrideTitle = overrideTitle;
                if (tab.TitleTextBlock != null) tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
                if (tab.TabButton != null) tab.TabButton.ToolTip = tab.Title;
            }
            if (_config.PinnedTabs != null && _config.PinnedTabs.Contains(key))
            {
                tab.IsPinned = true;
            }
        }

        private string GetEffectiveTitle(PathTab tab)
        {
            return string.IsNullOrWhiteSpace(tab.OverrideTitle) ? tab.Title : tab.OverrideTitle;
        }

        private void TogglePinTab(PathTab tab)
        {
            if (tab == null) return;
            tab.IsPinned = !tab.IsPinned;
            var key = GetTabKey(tab);
            if (_config.PinnedTabs == null) _config.PinnedTabs = new List<string>();
            if (tab.IsPinned)
            {
                if (!_config.PinnedTabs.Contains(key)) _config.PinnedTabs.Insert(0, key);
            }
            else
            {
                _config.PinnedTabs.Remove(key);
            }
            ConfigManager.Save(_config);
            ApplyPinVisual(tab);
            ReorderTabs();
        }

        private void ApplyPinVisual(PathTab tab)
        {
            if (tab == null || tab.TabButton == null || tab.TitleTextBlock == null) return;
            if (tab.IsPinned)
            {
                tab.TabButton.Width = _config.PinnedTabWidth > 0 ? _config.PinnedTabWidth : 90;
                tab.TitleTextBlock.Text = "📌 " + GetEffectiveTitle(tab);
                tab.TabButton.ToolTip = GetEffectiveTitle(tab);
            }
            else
            {
                tab.TabButton.Width = double.NaN;
                tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
                tab.TabButton.ToolTip = null;
            }
        }

        private void ReorderTabs()
        {
            if (FileBrowser == null || FileBrowser.TabsPanelControl == null) return;
            var pinned = _pathTabs.Where(t => t.IsPinned).ToList();
            var unpinned = _pathTabs.Where(t => !t.IsPinned).ToList();
            var ordered = new List<PathTab>();
            if (_config.PinnedTabs != null && _config.PinnedTabs.Count > 0)
            {
                foreach (var k in _config.PinnedTabs)
                {
                    var found = pinned.FirstOrDefault(t => GetTabKey(t) == k);
                    if (found != null) ordered.Add(found);
                }
                foreach (var t in pinned)
                {
                    if (!ordered.Contains(t)) ordered.Add(t);
                }
            }
            else
            {
                ordered.AddRange(pinned);
            }
            ordered.AddRange(unpinned);
            FileBrowser.TabsPanelControl.Children.Clear();
            foreach (var t in ordered)
            {
                if (t.TabContainer != null) FileBrowser.TabsPanelControl.Children.Add(t.TabContainer);
            }
            FileBrowser.TabsPanelControl.UpdateLayout();
            if (FileBrowser.TabsBorderControl != null) FileBrowser.TabsBorderControl.UpdateLayout();
        }

        private void RenameDisplayTitle(PathTab tab)
        {
            try
            {
                var dlg = new PathInputDialog("请输入新的显示标题：");
                dlg.Owner = this;
                dlg.InputText = GetEffectiveTitle(tab);
                if (dlg.ShowDialog() == true)
                {
                    var newTitle = dlg.InputText?.Trim() ?? string.Empty;
                    var key = GetTabKey(tab);
                    if (string.IsNullOrWhiteSpace(newTitle))
                    {
                        tab.OverrideTitle = null;
                        if (_config.TabTitleOverrides != null) _config.TabTitleOverrides.Remove(key);
                    }
                    else
                    {
                        tab.OverrideTitle = newTitle;
                        if (_config.TabTitleOverrides == null) _config.TabTitleOverrides = new Dictionary<string, string>();
                        _config.TabTitleOverrides[key] = newTitle;
                    }
                    ConfigManager.Save(_config);
                    ApplyPinVisual(tab);
                    if (tab.TitleTextBlock != null) tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
                }
            }
            catch { }
        }

        /// <summary>
        /// 切换到指定标签页（统一处理库和路径）
        /// </summary>
        private void SwitchToTab(PathTab tab)
        {
            if (tab == null) return;

            _activeTab = tab;
            UpdateTabStyles();

            // 根据标签页类型加载内容
            if (tab.Type == TabType.Library)
            {
                // 库类型标签页
                if (tab.Library != null)
                {
                    _currentLibrary = tab.Library;
                    _currentPath = null;  // 库模式下不使用单一路径
                    _config.LastLibraryId = tab.Library.Id;
                    ConfigManager.Save(_config);
                    if (FileBrowser != null) FileBrowser.NavUpEnabled = false;
                    _libraryService.LoadLibraryFiles(tab.Library,
                        (path) => DatabaseManager.GetFolderSize(path),
                        (bytes) => FormatFileSize(bytes));
                }
            }
            else if (tab.Type == TabType.Tag)
            {
                _currentLibrary = null;
                _currentPath = null;
                _currentTagFilter = new Tag { Id = tab.TagId, Name = tab.TagName };
                if (FileBrowser != null)
                {
                    FileBrowser.AddressText = "";
                    FileBrowser.IsAddressReadOnly = true;
                    FileBrowser.SetTagBreadcrumb(tab.TagName);
                    FileBrowser.NavUpEnabled = false;
                }
                FilterByTag(_currentTagFilter);
            }
            else
            {
                // 路径类型标签页
                _currentLibrary = null;  // 路径模式下清除库引用
                
                // 检查是否是搜索标签页（search://协议）
                if (tab.Path != null && tab.Path.StartsWith("search://"))
                {
                    // 搜索标签页：同步地址栏为规范化关键词并更新面包屑
                    var keyword = tab.Path.Substring("search://".Length);
                    _currentPath = null;
                    if (FileBrowser != null)
                    {
                        FileBrowser.AddressText = keyword;
                        FileBrowser.IsAddressReadOnly = false;
                        FileBrowser.SetSearchBreadcrumb(keyword);
                        FileBrowser.NavUpEnabled = false;
                    }
                    CheckAndRefreshSearchTab(tab.Path);
                    return;
                }
                
                try
                {
                    // 验证路径是否仍然有效
                    if (!Directory.Exists(tab.Path))
                    {
                        MessageBox.Show($"路径不存在: {tab.Path}\n\n标签页将被关闭。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        CloseTab(tab);
                        return;
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show($"无法访问路径: {tab.Path}\n\n{ex.Message}\n\n标签页将被关闭。", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CloseTab(tab);
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法访问路径: {tab.Path}\n\n{ex.Message}\n\n标签页将被关闭。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    CloseTab(tab);
                    return;
                }

                _currentPath = tab.Path;
                
                // 正常加载路径
                try
                {
                    NavigateToPathInternal(tab.Path);
                    if (FileBrowser != null) FileBrowser.NavUpEnabled = true;
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show($"无法加载路径: {tab.Path}\n\n{ex.Message}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法加载路径: {tab.Path}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RunVisualTests_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var outDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VisualTests");
                System.IO.Directory.CreateDirectory(outDir);
                CaptureElement(NavPathContent, System.IO.Path.Combine(outDir, "NavPath.png"));
                CaptureElement(NavLibraryContent, System.IO.Path.Combine(outDir, "NavLibrary.png"));
                CaptureElement(NavTagContent, System.IO.Path.Combine(outDir, "NavTag.png"));
                if (FileBrowser?.TabsPanelControl != null)
                {
                    CaptureElement(FileBrowser.TabsPanelControl, System.IO.Path.Combine(outDir, "Tabs.png"));
                }
                var baselineDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VisualBaseline");
                double diffTotal = 0; int diffCount = 0;
                foreach (var name in new[] { "NavPath.png", "NavLibrary.png", "NavTag.png", "Tabs.png" })
                {
                    var current = System.IO.Path.Combine(outDir, name);
                    var baseline = System.IO.Path.Combine(baselineDir, name);
                    if (System.IO.File.Exists(baseline) && System.IO.File.Exists(current))
                    {
                        var pct = CompareImages(baseline, current);
                        diffTotal += pct; diffCount++;
                    }
                }
                if (diffCount > 0)
                {
                    var avg = diffTotal / diffCount;
                    MessageBox.Show($"视觉测试完成，平均差异 {avg:F2}%。快照位于 VisualTests。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("视觉测试快照已生成于 VisualTests 目录。未找到基准图像。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "视觉测试失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CaptureElement(FrameworkElement element, string path)
        {
            if (element == null) return;
            var vis = element.Visibility;
            if (vis != Visibility.Visible)
            {
                element.Visibility = Visibility.Visible;
                element.UpdateLayout();
            }
            element.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            element.Arrange(new System.Windows.Rect(new System.Windows.Point(0, 0), new System.Windows.Size(element.ActualWidth > 0 ? element.ActualWidth : element.DesiredSize.Width,
                                                               element.ActualHeight > 0 ? element.ActualHeight : element.DesiredSize.Height)));
            element.UpdateLayout();
            var width = (int)Math.Max(1, element.RenderSize.Width);
            var height = (int)Math.Max(1, element.RenderSize.Height);
            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = System.IO.File.Create(path))
            {
                encoder.Save(fs);
            }
        }

        private double CompareImages(string baselinePath, string currentPath)
        {
            BitmapImage Load(string p)
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.UriSource = new Uri(p);
                img.EndInit();
                return img;
            }
            var a = Load(baselinePath);
            var b = Load(currentPath);
            if (a.PixelWidth != b.PixelWidth || a.PixelHeight != b.PixelHeight)
            {
                return 100.0; // 尺寸不同，视为完全不同
            }
            var fmt = PixelFormats.Pbgra32;
            var fa = new FormatConvertedBitmap(a, fmt, null, 0);
            var fb = new FormatConvertedBitmap(b, fmt, null, 0);
            int stride = fa.PixelWidth * (fmt.BitsPerPixel / 8);
            byte[] pa = new byte[fa.PixelHeight * stride];
            byte[] pb = new byte[fb.PixelHeight * stride];
            fa.CopyPixels(pa, stride, 0);
            fb.CopyPixels(pb, stride, 0);
            long diff = 0; long total = pa.Length;
            for (int i = 0; i < pa.Length; i++)
            {
                diff += Math.Abs(pa[i] - pb[i]);
            }
            // 将字节总差异归一化为百分比（最大差异 255 每字节）
            double pct = (diff / (double)(total * 255)) * 100.0;
            return Math.Min(100.0, Math.Max(0.0, pct));
        }


        /// <summary>
        /// 更新所有标签页样式（高亮当前标签）
        /// </summary>
        private void UpdateTabStyles()
        {
            foreach (var tab in _pathTabs)
            {
                if (tab.TabButton != null)
                {
                    if (tab == _activeTab)
                    {
                        tab.TabButton.Style = (Style)FindResource("ActiveTabButtonStyle");
                    }
                    else
                    {
                        tab.TabButton.Style = (Style)FindResource("TabButtonStyle");
                    }
                    
                    // 更新关闭按钮的颜色（根据标签状态）
                    if (tab.CloseButton is Border border && border.Child is TextBlock closeButtonText)
                    {
                        if (tab == _activeTab)
                        {
                            closeButtonText.Foreground = System.Windows.Media.Brushes.White; // 活动标签使用白色
                        }
                        else
                        {
                            closeButtonText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6C, 0x75, 0x7D)); // 非活动标签使用灰色
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 在库模式下设置标签页（为库的每个路径创建标签页）
        /// </summary>
        private void SetupLibraryTabs(Library library)
        {
            if (library == null || library.Paths == null || library.Paths.Count == 0) return;
            if (FileBrowser == null || FileBrowser.TabsPanelControl == null) return;

            // 获取库中所有有效的路径
            var validPaths = library.Paths.Where(p => Directory.Exists(p)).ToList();
            
            if (validPaths.Count == 0) return;

            // 只移除不属于当前库的标签页，保留属于当前库的标签页
            var tabsToRemove = _pathTabs.Where(tab => !validPaths.Contains(tab.Path)).ToList();
            foreach (var tab in tabsToRemove)
            {
                CloseTab(tab);
            }

            // 为每个有效路径创建标签页（如果不存在）
            foreach (var path in validPaths)
            {
                try
                {
                    // 检查是否已存在该路径的标签页
                    var existingTab = _pathTabs.FirstOrDefault(t => t.Path == path);
                    if (existingTab == null)
                    {
                        CreateTab(path);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"创建库路径标签页失败 {path}: {ex.Message}");
                }
            }

            // 如果没有活动标签页，或者当前活动标签页不属于当前库，激活第一个标签页
            if (_activeTab == null || !validPaths.Contains(_activeTab.Path))
            {
                var firstTab = _pathTabs.FirstOrDefault(t => validPaths.Contains(t.Path));
                if (firstTab != null)
                {
                    SwitchToTab(firstTab);
                }
                else if (_pathTabs.Count > 0)
                {
                    SwitchToTab(_pathTabs.First());
                }
            }
        }

        /// <summary>
        /// 清空库模式下的标签页
        /// </summary>
        private void ClearTabsInLibraryMode()
        {
            if (FileBrowser == null || FileBrowser.TabsPanelControl == null) return;

            // 清空所有标签页
            var tabsToRemove = _pathTabs.ToList();
            foreach (var tab in tabsToRemove)
            {
                CloseTab(tab);
            }

            // 如果没有标签页了，至少创建一个默认标签页（桌面）
            if (_pathTabs.Count == 0)
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktopPath))
                {
                    CreateTab(desktopPath);
                }
            }
        }

        /// <summary>
        /// 关闭标签页
        /// </summary>
        private void CloseTab(PathTab tab)
        {
            if (tab == null || tab.TabButton == null) return;
            // 在库模式下，如果关闭的是最后一个标签页，不阻止关闭（会重新加载库）
            // 在路径模式下，至少保留一个标签页
            if (_currentLibrary == null && _pathTabs.Count <= 1) return;
            if (FileBrowser == null || FileBrowser.TabsPanelControl == null) return; // 确保控件已初始化

            // 从列表中移除
            _pathTabs.Remove(tab);

            // 从面板中移除（找到包含按钮的容器）
            var container = tab.TabButton.Parent as StackPanel;
            if (container != null && FileBrowser != null && FileBrowser.TabsPanelControl != null)
            {
                // 先移除所有子元素，确保完全清理
                container.Children.Clear();
                FileBrowser.TabsPanelControl.Children.Remove(container);
                
                // 强制更新布局，避免残留占位符
                FileBrowser.TabsPanelControl.UpdateLayout();
                if (FileBrowser.TabsBorderControl != null)
                {
                    FileBrowser.TabsBorderControl.UpdateLayout();
                }
            }
            
            // 清理资源
            tab.TabButton = null;
            tab.CloseButton = null;

            // 如果关闭的是当前标签页，切换到其他标签页
            if (tab == _activeTab)
            {
                if (_pathTabs.Count > 0)
                {
                    SwitchToTab(_pathTabs.First());
                }
                else
                {
                    // 如果没有标签页了，创建默认标签页（桌面）
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    if (Directory.Exists(desktopPath))
                    {
                        CreateTab(desktopPath);
                    }
                }
            }
            else
            {
                UpdateTabStyles();
            }
        }

        /// <summary>
        /// 获取路径的显示标题（处理驱动器根目录）
        /// </summary>
        private string GetPathDisplayTitle(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            
            // 规范化路径（移除末尾的反斜杠，但保留驱动器根目录的形式）
            string normalizedPath = path.TrimEnd('\\');
            if (string.IsNullOrEmpty(normalizedPath)) normalizedPath = path;
            
            // 检查是否是驱动器根目录（如 C:\ 或 F:\）
            string rootPath = Path.GetPathRoot(path);
            if (rootPath == path || rootPath.TrimEnd('\\') == normalizedPath)
            {
                // 是驱动器根目录，尝试获取卷标
                try
                {
                    var driveInfo = new DriveInfo(rootPath);
                    if (driveInfo.IsReady && !string.IsNullOrEmpty(driveInfo.VolumeLabel))
                    {
                        return $"{driveInfo.Name.TrimEnd('\\')} ({driveInfo.VolumeLabel})";
                    }
                    else
                    {
                        return driveInfo.Name.TrimEnd('\\');
                    }
                }
                catch
                {
                    // 如果获取失败，返回路径本身（去掉末尾反斜杠）
                    return rootPath.TrimEnd('\\');
                }
            }
            
            // 普通路径，使用文件名
            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
            {
                // 如果 GetFileName 返回空，可能路径本身有问题，返回路径
                return path;
            }
            return fileName;
        }

        /// <summary>
        /// 更新标签页标题
        /// </summary>
        private void UpdateTabTitle(PathTab tab, string path)
        {
            if (tab == null) return;
            tab.Title = GetPathDisplayTitle(path);
            if (tab.TitleTextBlock != null)
            {
                tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
            }
        }

        private void TabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PathTab tab)
            {
                SwitchToTab(tab);
            }
        }

        // 默认名称搜索（顶层文件/目录）
        #endregion

        #region 搜索功能

        private async void FileBrowser_SearchClicked(object sender, RoutedEventArgs e)
        {
            // 从列2地址栏读取搜索关键词
            var searchText = FileBrowser?.AddressText?.Trim() ?? "";
            var normalizedKeyword = SearchService.NormalizeKeyword(searchText);
            
            if (string.IsNullOrEmpty(normalizedKeyword))
            {
                MessageBox.Show("请在地址栏输入搜索关键词", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 检查是否为有效路径
            if (Directory.Exists(normalizedKeyword) || File.Exists(normalizedKeyword))
            {
                // 如果是有效路径，导航到该路径
                NavigateToPath(normalizedKeyword);
                return;
            }
            
            // 非路径，执行全盘搜索（文件名+备注）
            await PerformSearchAsync(normalizedKeyword, true, true);
        }

        private void FileBrowser_FilterClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                var cm = new ContextMenu();
                void AddType(string text, FileTypeFilter type)
                {
                    var mi = new MenuItem { Header = text, IsCheckable = true, IsChecked = _searchOptions.Type == type };
                    mi.Click += (s, ev) => { _searchOptions.Type = type; };
                    cm.Items.Add(mi);
                }
                AddType("全部", FileTypeFilter.All);
                AddType("图片", FileTypeFilter.Images);
                AddType("视频", FileTypeFilter.Videos);
                AddType("文档", FileTypeFilter.Documents);
                AddType("文件夹", FileTypeFilter.Folders);
                var rangeCurrent = new MenuItem { Header = "当前磁盘", IsCheckable = true, IsChecked = _searchOptions.PathRange == PathRangeFilter.CurrentDrive };
                rangeCurrent.Click += (s, ev) => { _searchOptions.PathRange = PathRangeFilter.CurrentDrive; };
                cm.Items.Add(rangeCurrent);
                var rangeAll = new MenuItem { Header = "全部磁盘", IsCheckable = true, IsChecked = _searchOptions.PathRange == PathRangeFilter.AllDrives };
                rangeAll.Click += (s, ev) => { _searchOptions.PathRange = PathRangeFilter.AllDrives; };
                cm.Items.Add(rangeAll);
                cm.IsOpen = true;
            }
            catch { }
        }

        private void FileBrowser_LoadMoreClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_activeTab == null || !_activeTab.Path.StartsWith("search://")) return;
                
                var keyword = _activeTab.Path.Substring("search://".Length);
                var result = _searchService.LoadMore(keyword, _currentFiles.Count, _searchOptions, _currentPath);
                
                if (result.Items != null && result.Items.Count > 0)
                {
                    _currentFiles.AddRange(result.Items);
                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = null;
                        FileBrowser.FilesItemsSource = _currentFiles;
                        FileBrowser.LoadMoreVisible = result.HasMore;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载更多搜索结果失败: {ex.Message}");
            }
        }

        private async Task PerformSearchAsync(string keyword, bool searchNames, bool searchNotes)
        {
            if (string.IsNullOrEmpty(keyword) || _searchService == null)
            {
                return;
            }

            try
            {
                FileBrowser?.ShowEmptyState("正在搜索...");
                
                var result = await _searchService.PerformSearchAsync(
                    keyword: keyword,
                    searchOptions: _searchOptions,
                    currentPath: _currentPath,
                    searchNames: searchNames,
                    searchNotes: searchNotes,
                    getNotesFromDb: DatabaseManager.SearchFilesByNotes,
                    progressCallback: (pageResult) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (pageResult.Items != null && pageResult.Items.Count > 0)
                            {
                                if (_currentFiles == null || _currentFiles.Count == 0)
                                {
                                    _currentFiles = new List<FileSystemItem>(pageResult.Items);
                                }
                                else
                                {
                                    _currentFiles.AddRange(pageResult.Items);
                                }
                                
                                if (FileBrowser != null)
                                {
                                    FileBrowser.FilesItemsSource = null;
                                    FileBrowser.FilesItemsSource = _currentFiles;
                                    FileBrowser.LoadMoreVisible = pageResult.HasMore;
                                }
                            }
                        }));
                    }
                );

                // 显示搜索结果
                _currentFiles = result.Items ?? new List<FileSystemItem>();
                
                // 在列2打开新标签页显示搜索结果（即使结果为空也要创建标签页）
                if (FileBrowser != null) FileBrowser.TabsVisible = true;
                string searchTabTitle = $"搜索: {keyword}";
                string searchTabPath = $"search://{keyword}";
                
                // 检查是否已存在相同的搜索标签页
                var existingTab = _pathTabs.FirstOrDefault(t => t.Path == searchTabPath);
                if (existingTab != null)
                {
                    // 切换到现有标签页
                    SwitchToTab(existingTab);
                    if (FileBrowser != null)
                    {
                        // 使用分组显示（如果有分组数据）
                        if (result.GroupedItems != null && result.GroupedItems.Count > 0)
                        {
                            FileBrowser.SetGroupedSearchResults(result.GroupedItems);
                        }
                        else
                        {
                            FileBrowser.FilesItemsSource = _currentFiles;
                        }
                        FileBrowser.SetSearchBreadcrumb(keyword);
                        FileBrowser.AddressText = keyword;
                        FileBrowser.LoadMoreVisible = result.HasMore;
                        FileBrowser.HideEmptyState();
                    }
                }
                else
                {
                    // 创建新标签页
                    var searchTab = new PathTab
                    {
                        Type = TabType.Path,
                        Path = searchTabPath,
                        Title = searchTabTitle
                    };
                    
                    CreateTabInternal(searchTab);
                    _activeTab = searchTab;
                    
                    // 显示搜索结果
                    if (FileBrowser != null)
                    {
                        // 使用分组显示（如果有分组数据）
                        if (result.GroupedItems != null && result.GroupedItems.Count > 0)
                        {
                            FileBrowser.SetGroupedSearchResults(result.GroupedItems);
                        }
                        else
                        {
                            FileBrowser.FilesItemsSource = _currentFiles;
                        }
                        FileBrowser.SetSearchBreadcrumb(keyword);
                        FileBrowser.AddressText = keyword;
                        FileBrowser.NavUpEnabled = false;
                        FileBrowser.LoadMoreVisible = result.HasMore;
                        FileBrowser.HideEmptyState();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"搜索时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                FileBrowser?.HideEmptyState();
            }
        }

        private void CheckAndRefreshSearchTab(string searchTabPath)
        {
            if (string.IsNullOrEmpty(searchTabPath) || !searchTabPath.StartsWith("search://"))
                return;

            try
            {
                var keyword = searchTabPath.Substring("search://".Length);
                
                // 先检查缓存
                var cache = _searchCacheService?.GetCache(searchTabPath);
                if (cache != null)
                {
                    // 缓存有效，直接使用
                    _currentFiles = new List<FileSystemItem>(cache.Items);
                    if (FileBrowser != null)
                    {
                        // 注意：缓存可能没有分组信息，使用普通显示
                        FileBrowser.FilesItemsSource = null;
                        FileBrowser.FilesItemsSource = _currentFiles;
                        FileBrowser.LoadMoreVisible = cache.HasMore;
                    }
                    return;
                }
                
                // 缓存过期或不存在，刷新
                FileBrowser?.ShowEmptyState("正在刷新...");
                var result = _searchService.RefreshSearch(keyword, _searchOptions, _currentPath);
                
                if (result.Items != null)
                {
                    _currentFiles = result.Items;
                    if (FileBrowser != null)
                    {
                        // 使用分组显示（如果有分组数据）
                        if (result.GroupedItems != null && result.GroupedItems.Count > 0)
                        {
                            FileBrowser.SetGroupedSearchResults(result.GroupedItems);
                        }
                        else
                        {
                            FileBrowser.FilesItemsSource = null;
                            FileBrowser.FilesItemsSource = _currentFiles;
                        }
                        FileBrowser.LoadMoreVisible = result.HasMore;
                        FileBrowser.HideEmptyState();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查搜索缓存失败: {ex.Message}");
                FileBrowser?.HideEmptyState();
            }
        }

        #endregion

        private void NavigateToPath(string path)
        {
            if (Directory.Exists(path))
            {
                // 更新或创建标签页
                if (_activeTab != null && _activeTab.Type == TabType.Path && _activeTab.Path == path)
                {
                    // 已经是当前标签页的路径，直接导航
                    NavigateToPathInternal(path);
                }
                else
                {
                    // 查找是否已有该路径的标签页
                    var existingTab = _pathTabs.FirstOrDefault(t => t.Type == TabType.Path && t.Path == path);
                    if (existingTab != null)
                    {
                        SwitchToTab(existingTab);
                    }
                    else
                    {
                        // 更新当前标签页路径或创建新标签页
                        if (_activeTab != null && _activeTab.Type == TabType.Path)
                        {
                            // 如果当前标签页是路径类型，更新它
                            _activeTab.Path = path;
                            UpdateTabTitle(_activeTab, path);
                            NavigateToPathInternal(path);
                        }
                        else
                        {
                            // 创建新标签页
                            CreateTab(path);
                        }
                    }
                }
            }
        }

        private void NavigateToPathInternal(string path)
        {
            if (!Directory.Exists(path)) return;

            // 如果进入的是文件夹，计算并更新其大小缓存
            // 检查数据库中是否有缓存，如果没有或已过期，重新计算
            var cachedSize = DatabaseManager.GetFolderSize(path);
            if (!cachedSize.HasValue)
            {
                // 没有缓存，异步计算并更新
                CalculateAndUpdateFolderSize(path);
            }
            else
            {
                // 有缓存，但需要验证是否仍然有效（通过最后修改时间检查）
                // GetFolderSize 已经做了这个检查，所以这里只需要计算实际大小并比较
                // 异步计算并更新（如果大小有变化）
                CalculateAndUpdateFolderSizeIfChanged(path, cachedSize.Value);
            }

            // 只有在当前路径不为空时才添加到历史记录
            if (!string.IsNullOrEmpty(_currentPath) && _currentPath != path)
            {
                AddToHistory(_currentPath);
            }
            _currentPath = path;
            // 清除任何过滤状态
            ClearFilter();
            LoadCurrentDirectory();
            if (FileBrowser != null) FileBrowser.NavUpEnabled = true;
            
            // 第一次打开文件夹时，异步计算所有子文件夹的大小
            // 延迟执行，避免阻塞文件列表加载
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                CalculateAllSubfolderSizesOnFirstOpen(path);
            }), System.Windows.Threading.DispatcherPriority.Background);
            
            // 保存当前路径到配置
            _config.LastPath = _currentPath;
            ConfigManager.Save(_config);
        }
        
        /// <summary>
        /// 第一次打开文件夹时，计算所有子文件夹的大小（性能优化版本）
        /// </summary>
        private void CalculateAllSubfolderSizesOnFirstOpen(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;
            
            // 异步检查是否需要计算
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // 获取所有子文件夹
                    string[] subfolders;
                    try
                    {
                        subfolders = Directory.GetDirectories(folderPath);
                    }
                    catch
                    {
                        return; // 无法访问，跳过
                    }
                    
                    if (subfolders.Length == 0)
                        return; // 没有子文件夹，不需要计算
                    
                    // 检查有多少子文件夹已有缓存
                    var cachedCount = 0;
                    foreach (var subfolder in subfolders)
                    {
                        var cachedSize = DatabaseManager.GetFolderSize(subfolder);
                        if (cachedSize.HasValue)
                        {
                            cachedCount++;
                        }
                    }
                    
                    // 如果缓存率低于50%，认为是第一次打开，计算所有子文件夹大小
                    var cacheRate = (double)cachedCount / subfolders.Length;
                    if (cacheRate < 0.5)
                    {
                        // 异步计算所有子文件夹大小（分批处理，控制性能）
                        CalculateSubfolderSizesBatch(subfolders);
                    }
                }
                catch { }
            });
        }
        
        /// <summary>
        /// 分批计算子文件夹大小（性能优化：限制并发、延迟处理）
        /// </summary>
        private void CalculateSubfolderSizesBatch(string[] folderPaths)
        {
            if (folderPaths == null || folderPaths.Length == 0)
                return;
            
            var cancellationToken = _folderSizeCalculationCancellation.Token;
            
            // 分批处理，每批最多10个文件夹
            int batchSize = 10;
            int delayBetweenBatches = 2000; // 每批之间延迟2秒
            
            for (int i = 0; i < folderPaths.Length; i += batchSize)
            {
                var batch = folderPaths.Skip(i).Take(batchSize).ToArray();
                var batchIndex = i / batchSize;
                var delay = batchIndex * delayBetweenBatches;
                
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // 延迟启动，避免同时启动太多任务
                        if (delay > 0)
                        {
                            await System.Threading.Tasks.Task.Delay(delay, cancellationToken);
                        }
                        
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        // 处理当前批次
                        foreach (var folderPath in batch)
                        {
                            if (cancellationToken.IsCancellationRequested) return;
                            
                            // 检查是否已有缓存
                            var cachedSize = DatabaseManager.GetFolderSize(folderPath);
                            if (cachedSize.HasValue)
                            {
                                continue; // 已有缓存，跳过
                            }
                            
                            // 尝试获取信号量（非阻塞，如果无法获取则跳过）
                            if (!await _folderSizeCalculationSemaphore.WaitAsync(100, cancellationToken))
                            {
                                // 无法获取，延迟后重试或跳过
                                continue;
                            }
                            
                            try
                            {
                                if (cancellationToken.IsCancellationRequested) return;
                                
                                // 计算文件夹大小
                                var size = CalculateDirectorySize(folderPath, cancellationToken);
                                if (cancellationToken.IsCancellationRequested) return;
                                
                                // 更新数据库缓存
                                DatabaseManager.SetFolderSize(folderPath, size);
                                
                                // 更新UI（使用低优先级，避免影响用户操作）
                                _ = Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (!cancellationToken.IsCancellationRequested)
                                    {
                                        var item = _currentFiles.FirstOrDefault(f => f.Path == folderPath);
                                        if (item != null && (item.Size == "计算中..." || string.IsNullOrEmpty(item.Size)))
                                        {
                                            item.Size = FormatFileSize(size);
                                            var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                                            collectionView?.Refresh();
                                        }
                                    }
                                }), System.Windows.Threading.DispatcherPriority.SystemIdle);
                            }
                            catch (OperationCanceledException) { }
                            catch { }
                            finally
                            {
                                _folderSizeCalculationSemaphore.Release();
                            }
                            
                            // 每个文件夹之间延迟100ms，避免CPU占用过高
                            await System.Threading.Tasks.Task.Delay(100, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                }, cancellationToken);
            }
        }
        
        /// <summary>
        /// 程序启动时清理不存在的文件夹大小缓存
        /// </summary>
        private void CleanupFolderSizeCacheOnStartup()
        {
            // 异步执行，不阻塞UI
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // 获取缓存总数
                    int totalCount = DatabaseManager.GetFolderSizeCacheCount();
                    if (totalCount == 0)
                        return; // 没有缓存，不需要清理
                    
                    // 如果缓存数量较少，清理所有；如果较多，只清理一部分（避免启动时耗时过长）
                    int maxProcessed = totalCount > 5000 ? 1000 : 0; // 超过5000条时，只清理1000条
                    int cleanedCount = DatabaseManager.CleanupNonExistentFolderSizes(batchSize: 100, maxProcessed: maxProcessed);
                    
                    if (cleanedCount > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"启动时清理了 {cleanedCount} 条不存在的文件夹大小缓存");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"清理文件夹大小缓存失败: {ex.Message}");
                }
            });
        }
        
        /// <summary>
        /// 计算并更新文件夹大小（进入文件夹时调用）
        /// </summary>
        private void CalculateAndUpdateFolderSize(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;
            
            var cancellationToken = _folderSizeCalculationCancellation.Token;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    await _folderSizeCalculationSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        var size = CalculateDirectorySize(folderPath, cancellationToken);
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        // 更新数据库缓存
                        DatabaseManager.SetFolderSize(folderPath, size);
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                    finally
                    {
                        _folderSizeCalculationSemaphore.Release();
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            }, cancellationToken);
        }
        
        /// <summary>
        /// 如果文件夹大小有变化，则计算并更新（进入文件夹时调用，已有缓存）
        /// </summary>
        private void CalculateAndUpdateFolderSizeIfChanged(string folderPath, long cachedSize)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;
            
            var cancellationToken = _folderSizeCalculationCancellation.Token;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    await _folderSizeCalculationSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        var size = CalculateDirectorySize(folderPath, cancellationToken);
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        // 如果大小有变化，更新数据库缓存
                        if (size != cachedSize)
                        {
                            DatabaseManager.SetFolderSize(folderPath, size);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                    finally
                    {
                        _folderSizeCalculationSemaphore.Release();
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            }, cancellationToken);
        }

        private void AddToHistory(string path)
        {
            if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count - 1)
            {
                _navigationHistory.RemoveRange(_currentHistoryIndex + 1, _navigationHistory.Count - _currentHistoryIndex - 1);
            }
            
            _navigationHistory.Add(path);
            _currentHistoryIndex = _navigationHistory.Count - 1;
        }
        private void LoadFiles()
        {
            // 使用信号量防止重复加载
            if (!_loadFilesSemaphore.Wait(0))
            {
                System.Diagnostics.Debug.WriteLine("LoadFiles: 已有加载任务在进行，跳过此次调用");
                return;
            }
            
            try
            {
                // 设置加载标志
                _isLoadingFiles = true;
                
                _currentFiles.Clear();
                
                // 先检查目录是否存在和可访问
                if (!Directory.Exists(_currentPath))
                {
                    throw new DirectoryNotFoundException($"路径不存在: {_currentPath}");
                }

                // 异步加载文件列表，避免阻塞UI线程
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        // 使用 FileListService 加载文件列表
                        var allFiles = _fileListService.LoadFileSystemItems(
                            _currentPath,
                            (path) => DatabaseManager.GetFolderSize(path),
                            (bytes) => _fileListService.FormatFileSize(bytes));

                        // 在UI线程更新文件列表
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                _currentFiles.Clear();
                                _currentFiles.AddRange(allFiles);
                                
                                // 应用排序
                                SortFiles();

                                if (FileBrowser != null)
                                    FileBrowser.FilesItemsSource = _currentFiles;

                                // 取消之前的文件夹大小计算任务
                                _folderSizeCalculationCancellation.Cancel();
                                _folderSizeCalculationCancellation = new System.Threading.CancellationTokenSource();
                                var cancellationToken = _folderSizeCalculationCancellation.Token;
                                
                                // 清空待计算的文件夹队列（文件列表已改变）
                                lock (_pendingFolderSizeCalculations)
                                {
                                    _pendingFolderSizeCalculations.Clear();
                                }
                                
                                // 停止闲置计算定时器
                                if (_idleFolderSizeCalculationTimer != null)
                                {
                                    _idleFolderSizeCalculationTimer.Stop();
                                }

                                // 异步加载标签和备注（延迟加载，避免阻塞UI）
                                System.Threading.Tasks.Task.Run(() =>
                                {
                                    try
                                    {
                                        // 批量加载标签和备注（限制并发，减少到2个避免CPU占用过高）
                                        var semaphore = new System.Threading.SemaphoreSlim(2, 2); // 最多2个并发查询
                                        var tasks = _currentFiles.Select(async item =>
                                        {
                                            if (cancellationToken.IsCancellationRequested) return;
                                            
                                            await semaphore.WaitAsync(cancellationToken);
                                            try
                                            {
                                                if (cancellationToken.IsCancellationRequested) return;
                                                
                                                // 从 TagTrain 获取文件的标签
                                                if (App.IsTagTrainAvailable)
                                                {
                                                    var fileTagIds = OoiMRRIntegration.GetFileTagIds(item.Path);
                                                    if (fileTagIds != null && fileTagIds.Count > 0)
                                                    {
                                                        var fileTagNames = OrderTagNames(fileTagIds);
                                                        item.Tags = string.Join(", ", fileTagNames);
                                                    }
                                                    else
                                                    {
                                                        item.Tags = "";
                                                    }
                                                }
                                                else
                                                {
                                                    item.Tags = "";
                                                }
                                                
                                                var notes = DatabaseManager.GetFileNotes(item.Path);
                                                if (!string.IsNullOrEmpty(notes))
                                                {
                                                    var firstLine = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                                                    item.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                                                }
                                                else
                                                {
                                                    item.Notes = "";
                                                }
                                            }
                                            finally
                                            {
                                                semaphore.Release();
                                            }
                                        }).ToList();
                                        
                                        try
                                        {
                                            System.Threading.Tasks.Task.WaitAll(tasks.ToArray(), cancellationToken);
                                        }
                                        catch (OperationCanceledException) { }
                                        
                                        // 批量更新UI（减少刷新次数）
                                        Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            if (!cancellationToken.IsCancellationRequested)
                                            {
                                                var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                                                collectionView?.Refresh();
                                            }
                                        }), System.Windows.Threading.DispatcherPriority.Background);
                                    }
                                    catch (OperationCanceledException) { }
                                    catch { }
                                }, cancellationToken);

                                // 异步计算文件夹大小（严格限制数量和延迟，避免资源消耗过大）
                                // 只计算前5个文件夹的大小，大幅减少资源消耗
                                var directories = allFiles.Where(f => f.IsDirectory).ToList();
                                int maxCalculations = Math.Min(5, directories.Count);
                                int delayIndex = 0;
                                
                                for (int i = 0; i < maxCalculations; i++)
                                {
                                    var dir = directories[i];
                                    var path = dir.Path;
                                    var currentDelay = delayIndex * 1000; // 每个任务延迟1秒，避免同时启动
                                    delayIndex++;
                                    
                                    System.Threading.Tasks.Task.Run(async () =>
                                    {
                                        try
                                        {
                                            // 延迟启动，避免同时启动太多任务
                                            if (currentDelay > 0)
                                            {
                                                await System.Threading.Tasks.Task.Delay(currentDelay, cancellationToken);
                                            }
                                            
                                            if (cancellationToken.IsCancellationRequested) return;
                                            
                                            await _folderSizeCalculationSemaphore.WaitAsync(cancellationToken);
                                            try
                                            {
                                                if (cancellationToken.IsCancellationRequested) return;
                                                
                                                var size = CalculateDirectorySize(path, cancellationToken);
                                                if (cancellationToken.IsCancellationRequested) return;
                                                
                                                // 更新数据库缓存
                                                DatabaseManager.SetFolderSize(path, size);
                                                
                                                // 使用低优先级批量更新，减少UI刷新频率
                                                _ = Dispatcher.BeginInvoke(new Action(() =>
                                                {
                                                    if (!cancellationToken.IsCancellationRequested)
                                                    {
                                                        var item = _currentFiles.FirstOrDefault(f => f.Path == path);
                                                        if (item != null)
                                                        {
                                                            item.Size = FormatFileSize(size);
                                                            var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                                                            collectionView?.Refresh();
                                                        }
                                                    }
                                                }), System.Windows.Threading.DispatcherPriority.SystemIdle);
                                            }
                                            catch (OperationCanceledException) { }
                                            catch { }
                                            finally
                                            {
                                                _folderSizeCalculationSemaphore.Release();
                                            }
                                        }
                                        catch (OperationCanceledException) { }
                                        catch { }
                                    }, cancellationToken);
                                }
                            }
                            finally
                            {
                                // 重置加载标志并释放信号量
                                _isLoadingFiles = false;
                                _loadFilesSemaphore.Release();
                            }
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        // 在UI线程显示错误
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _isLoadingFiles = false;
                            _loadFilesSemaphore.Release();
                            throw ex;
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        // 在UI线程显示错误
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _isLoadingFiles = false;
                            _loadFilesSemaphore.Release();
                            throw ex;
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                });
            }
            catch (UnauthorizedAccessException)
            {
                // 确保释放锁
                _isLoadingFiles = false;
                _loadFilesSemaphore.Release();
                // 重新抛出，让 LoadCurrentDirectory 处理
                throw;
            }
            catch (Exception)
            {
                // 确保释放锁
                _isLoadingFiles = false;
                _loadFilesSemaphore.Release();
                // 重新抛出，让 LoadCurrentDirectory 处理
                throw;
            }
        }

        private long CalculateDirectorySize(string directory, System.Threading.CancellationToken cancellationToken = default)
        {
            long size = 0;
            try
            {
                var dirInfo = new DirectoryInfo(directory);
                if (!dirInfo.Exists) return size;
                
                var startTime = System.Diagnostics.Stopwatch.StartNew();
                int maxTimeMs = 10000; // 增加到10秒超时（因为要递归计算）
                int maxDepth = 20; // 限制递归深度，避免过深
                int maxFilesPerLevel = 5000; // 每层最多计算5000个文件
                
                // 使用递归方法计算，包含所有子文件夹的大小
                size = CalculateDirectorySizeRecursiveOptimized(dirInfo, 0, maxDepth, maxFilesPerLevel, startTime, maxTimeMs, cancellationToken);
            }
            catch { }
            return size;
        }
        
        /// <summary>
        /// 递归计算文件夹大小（优化版本，包含所有子文件夹）
        /// </summary>
        private long CalculateDirectorySizeRecursiveOptimized(
            DirectoryInfo dirInfo, 
            int currentDepth, 
            int maxDepth, 
            int maxFilesPerLevel,
            System.Diagnostics.Stopwatch startTime,
            int maxTimeMs,
            System.Threading.CancellationToken cancellationToken)
        {
            long size = 0;
            
            if (currentDepth >= maxDepth || cancellationToken.IsCancellationRequested) 
                return size;
            
            // 超时检查
            if (startTime.ElapsedMilliseconds > maxTimeMs)
                return size;
            
            try
            {
                // 先尝试从数据库读取子文件夹的缓存大小（如果存在）
                // 这样可以避免重复计算已缓存的子文件夹
                var subDirs = dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly);
                var subDirsToCalculate = new List<DirectoryInfo>();
                long cachedSubDirSize = 0;
                
                foreach (var subDir in subDirs)
                {
                    if (cancellationToken.IsCancellationRequested) return size;
                    if (startTime.ElapsedMilliseconds > maxTimeMs) return size;
                    
                    // 尝试从数据库读取缓存
                    var cachedSize = DatabaseManager.GetFolderSize(subDir.FullName);
                    if (cachedSize.HasValue)
                    {
                        cachedSubDirSize += cachedSize.Value;
                    }
                    else
                    {
                        subDirsToCalculate.Add(subDir);
                    }
                }
                
                size += cachedSubDirSize;
                
                // 计算当前目录的直接文件
                int fileCount = 0;
                try
                {
                    var files = dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested) return size;
                        if (startTime.ElapsedMilliseconds > maxTimeMs) return size;
                        if (fileCount >= maxFilesPerLevel) break; // 超过限制，停止计算
                        
                        // 每处理100个文件检查一次取消并让出CPU
                        fileCount++;
                        if (fileCount % 100 == 0)
                        {
                            System.Threading.Thread.Sleep(20);
                            if (cancellationToken.IsCancellationRequested) return size;
                            if (startTime.ElapsedMilliseconds > maxTimeMs) return size;
                        }
                        
                        try
                        {
                            size += file.Length;
                        }
                        catch { }
                    }
                }
                catch { }
                
                // 递归计算子目录（只计算没有缓存的）
                foreach (var subDir in subDirsToCalculate)
                {
                    if (cancellationToken.IsCancellationRequested) return size;
                    if (startTime.ElapsedMilliseconds > maxTimeMs) return size;
                    
                    try
                    {
                        long subDirSize = CalculateDirectorySizeRecursiveOptimized(
                            subDir, 
                            currentDepth + 1, 
                            maxDepth, 
                            maxFilesPerLevel,
                            startTime,
                            maxTimeMs,
                            cancellationToken);
                        size += subDirSize;
                        
                        // 将子文件夹的大小缓存到数据库（异步，不阻塞）
                        if (subDirSize > 0)
                        {
                            System.Threading.Tasks.Task.Run(() =>
                            {
                                try
                                {
                                    DatabaseManager.SetFolderSize(subDir.FullName, subDirSize);
                                }
                                catch { }
                            });
                        }
                    }
                    catch { }
                    
                    // 每个子文件夹之间延迟，避免CPU占用过高
                    if (currentDepth < 3) // 只在浅层延迟，深层不延迟以加快速度
                    {
                        System.Threading.Thread.Sleep(10);
                    }
                }
            }
            catch { }
            
            return size;
        }
        
        private long CalculateDirectorySizeRecursive(DirectoryInfo dirInfo, int currentDepth, int maxDepth, System.Threading.CancellationToken cancellationToken)
        {
            long size = 0;
            if (currentDepth >= maxDepth || cancellationToken.IsCancellationRequested) return size;
            
            try
            {
                // 计算当前目录的直接文件
                int fileCount = 0;
                foreach (var file in dirInfo.GetFiles("*", SearchOption.TopDirectoryOnly))
                {
                    if (cancellationToken.IsCancellationRequested) return size;
                    
                    // 每处理20个文件检查一次取消，并让出CPU时间片（增加频率减少CPU占用）
                    fileCount++;
                    if (fileCount % 20 == 0)
                    {
                        System.Threading.Thread.Sleep(10); // 增加到10ms，让出更多CPU时间片
                        if (cancellationToken.IsCancellationRequested) return size;
                    }
                    
                    try
                    {
                        size += file.Length;
                    }
                    catch { }
                }
                
                // 递归计算子目录（限制深度）
                foreach (var subDir in dirInfo.GetDirectories("*", SearchOption.TopDirectoryOnly))
                {
                    if (cancellationToken.IsCancellationRequested) return size;
                    try
                    {
                        size += CalculateDirectorySizeRecursive(subDir, currentDepth + 1, maxDepth, cancellationToken);
                    }
                    catch { }
                }
            }
            catch { }
            
            return size;
        }

        private string FormatFileSize(long bytes)
        {
            return _fileListService?.FormatFileSize(bytes) ?? "0 B";
        }

        #endregion

        #region 拖拽功能

        private void InitializeDragDrop()
        {
            _dragDropManager = new DragDropManager();
            
            // 订阅事件
            _dragDropManager.DragDropCompleted += DragDropManager_DragDropCompleted;
            _dragDropManager.DragDropStarted += DragDropManager_DragDropStarted;
            _dragDropManager.DragDropCancelled += DragDropManager_DragDropCancelled;
            
            // 初始化文件列表拖拽
            if (FileBrowser?.FilesList != null)
                _dragDropManager.InitializeFileListDragDrop(FileBrowser.FilesList);
            
            // 初始化库列表拖放功能
            InitializeLibraryDragDrop();
        }
        
        private void InitializeLibraryDragDrop()
        {
            if (LibrariesListBox == null) return;
            
            // 移除旧的事件处理器（避免重复绑定）
            LibrariesListBox.DragEnter -= LibrariesListBox_DragEnter;
            LibrariesListBox.DragOver -= LibrariesListBox_DragOver;
            LibrariesListBox.DragLeave -= LibrariesListBox_DragLeave;
            LibrariesListBox.Drop -= LibrariesListBox_Drop;
            
            LibrariesListBox.AllowDrop = true;
            LibrariesListBox.DragEnter += LibrariesListBox_DragEnter;
            LibrariesListBox.DragOver += LibrariesListBox_DragOver;
            LibrariesListBox.DragLeave += LibrariesListBox_DragLeave;
            LibrariesListBox.Drop += LibrariesListBox_Drop;
            
            // 同时为 ScrollViewer 设置拖放（因为 ScrollViewer 可能拦截事件）
            var scrollViewer = VisualTreeHelper.GetParent(LibrariesListBox) as ScrollViewer;
            if (scrollViewer != null)
            {
                scrollViewer.AllowDrop = true;
                scrollViewer.DragEnter -= LibrariesListBox_DragEnter;
                scrollViewer.DragOver -= LibrariesListBox_DragOver;
                scrollViewer.DragLeave -= LibrariesListBox_DragLeave;
                scrollViewer.Drop -= LibrariesListBox_Drop;
                
                scrollViewer.DragEnter += LibrariesListBox_DragEnter;
                scrollViewer.DragOver += LibrariesListBox_DragOver;
                scrollViewer.DragLeave += LibrariesListBox_DragLeave;
                scrollViewer.Drop += LibrariesListBox_Drop;
                
                System.Diagnostics.Debug.WriteLine("[库拖拽] ScrollViewer 拖放已设置");
            }
            
            // 为整个 NavLibraryContent Grid 设置拖放（处理拖拽到空白区域）
            if (NavLibraryContent != null)
            {
                NavLibraryContent.AllowDrop = true;
                NavLibraryContent.DragEnter -= LibrariesListBox_DragEnter;
                NavLibraryContent.DragOver -= LibrariesListBox_DragOver;
                NavLibraryContent.DragLeave -= LibrariesListBox_DragLeave;
                NavLibraryContent.Drop -= LibrariesListBox_Drop;
                
                NavLibraryContent.DragEnter += LibrariesListBox_DragEnter;
                NavLibraryContent.DragOver += LibrariesListBox_DragOver;
                NavLibraryContent.DragLeave += LibrariesListBox_DragLeave;
                NavLibraryContent.Drop += LibrariesListBox_Drop;
                
                System.Diagnostics.Debug.WriteLine("[库拖拽] NavLibraryContent Grid 拖放已设置");
            }
            
            System.Diagnostics.Debug.WriteLine("[库拖拽] 初始化完成");
        }
        
        private void LibrariesListBox_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[库拖拽] DragEnter 触发 - sender: {sender.GetType().Name}");
                
                // 检查库导航内容是否可见
                if (NavLibraryContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("[库拖拽] NavLibraryContent 为 null");
                    e.Effects = DragDropEffects.None;
                    return;
                }
                
                if (NavLibraryContent.Visibility != Visibility.Visible)
                {
                    System.Diagnostics.Debug.WriteLine("[库拖拽] NavLibraryContent 不可见");
                    e.Effects = DragDropEffects.None;
                    return;
                }
                
                // 检查数据格式 - 尝试多种格式
                bool hasFileDrop = e.Data.GetDataPresent(DataFormats.FileDrop);
                System.Diagnostics.Debug.WriteLine($"[库拖拽] DataFormats.FileDrop 存在: {hasFileDrop}");
                
                if (!hasFileDrop)
                {
                    // 列出所有可用的数据格式
                    var formats = e.Data.GetFormats();
                    System.Diagnostics.Debug.WriteLine($"[库拖拽] 可用格式: {string.Join(", ", formats)}");
                    e.Effects = DragDropEffects.None;
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine("[库拖拽] 数据格式正确，允许拖放");
                e.Effects = DragDropEffects.Link; // 库操作使用 Link 效果
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[库拖拽] DragEnter 异常: {ex.Message}");
                e.Effects = DragDropEffects.None;
            }
        }
        
        private void LibrariesListBox_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                // 检查库导航内容是否可见
                if (NavLibraryContent == null || NavLibraryContent.Visibility != Visibility.Visible)
                {
                    e.Effects = DragDropEffects.None;
                    return;
                }
                
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.None;
                    return;
                }
                
                // 获取实际的 ListBox（可能是从 ScrollViewer 或 Grid 触发的）
                ListBox listBox = sender as ListBox;
                if (listBox == null)
                {
                    // 如果是 ScrollViewer 或 Grid 触发，找到 LibrariesListBox
                    if (sender is ScrollViewer || sender is Grid)
                    {
                        listBox = LibrariesListBox;
                    }
                }
                
                // 高亮显示鼠标下的库项
                if (listBox != null)
                {
                    // 清除拖拽高亮（不包括路径匹配高亮和选中样式）
                    foreach (var listItem in listBox.Items)
                    {
                        var container = listBox.ItemContainerGenerator.ContainerFromItem(listItem) as ListBoxItem;
                        if (container != null)
                        {
                        var tag = container.Tag as string;
                        // 不清除匹配高亮（黄色）
                        if (tag != "Match")
                            {
                                var bg = container.Background as SolidColorBrush;
                                // 只清除拖拽高亮（半透明背景）
                                if (bg != null && bg.Color.A < 255)
                                {
                                    container.ClearValue(ListBoxItem.BackgroundProperty);
                                    container.ClearValue(ListBoxItem.ForegroundProperty);
                                    container.ClearValue(ListBoxItem.BorderBrushProperty);
                                }
                            }
                        }
                    }
                    
                    var point = e.GetPosition(listBox);
                    var element = listBox.InputHitTest(point) as DependencyObject;
                    
                    // 查找 ListBoxItem
                    while (element != null && !(element is ListBoxItem))
                    {
                        element = VisualTreeHelper.GetParent(element);
                    }
                    
                    if (element is ListBoxItem listBoxItem && listBoxItem.Content is Library library)
                    {
                        // 拖拽高亮：只在非匹配时设置
                        var tag = listBoxItem.Tag as string;
                        if (tag != "Match")
                        {
                            listBoxItem.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 33, 150, 243));
                        }
                    }
                }
                
                e.Effects = DragDropEffects.Link;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[库拖拽] DragOver 异常: {ex.Message}");
                e.Effects = DragDropEffects.None;
            }
        }
        
        private void LibrariesListBox_DragLeave(object sender, DragEventArgs e)
        {
            // 获取实际的 ListBox（可能是从 ScrollViewer 或 Grid 触发的）
            ListBox listBox = sender as ListBox;
            if (listBox == null)
            {
                // 如果是 ScrollViewer 或 Grid 触发，使用 LibrariesListBox
                if (sender is ScrollViewer || sender is Grid)
                {
                    listBox = LibrariesListBox;
                }
            }
            
            // 清除拖拽高亮（不包括路径匹配高亮和选中样式）
            if (listBox != null)
            {
                foreach (var item in listBox.Items)
                {
                    var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                    if (container != null)
                    {
                        var tag = container.Tag as string;
                        // 不清除匹配高亮（黄色）
                        if (tag != "Match")
                        {
                            var bg = container.Background as SolidColorBrush;
                            // 只清除拖拽高亮（半透明背景）
                            if (bg != null && bg.Color.A < 255)
                            {
                                container.ClearValue(ListBoxItem.BackgroundProperty);
                                container.ClearValue(ListBoxItem.ForegroundProperty);
                                container.ClearValue(ListBoxItem.BorderBrushProperty);
                            }
                        }
                    }
                }
            }
        }
        private void LibrariesListBox_Drop(object sender, DragEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[库拖拽] Drop 触发，sender: {sender.GetType().Name}");
                
                // 检查库导航内容是否可见
                if (NavLibraryContent == null)
                {
                    System.Diagnostics.Debug.WriteLine("[库拖拽] Drop - NavLibraryContent 为 null");
                    MessageBox.Show("库导航内容未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                if (NavLibraryContent.Visibility != Visibility.Visible)
                {
                    System.Diagnostics.Debug.WriteLine("[库拖拽] Drop - 库导航内容不可见");
                    MessageBox.Show("请先切换到库模式", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                if (LibrariesListBox == null)
                {
                    System.Diagnostics.Debug.WriteLine("[库拖拽] Drop - LibrariesListBox 为 null");
                    MessageBox.Show("库列表未初始化", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // 清除高亮
                LibrariesListBox_DragLeave(sender, e);
                
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var formats = e.Data.GetFormats();
                    System.Diagnostics.Debug.WriteLine($"[库拖拽] Drop - 数据格式不正确，可用格式: {string.Join(", ", formats)}");
                    MessageBox.Show("无法识别拖拽的数据格式", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[库拖拽] Drop - 文件列表为空或null");
                    MessageBox.Show("没有可添加的文件或文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"[库拖拽] Drop - 接收到 {files.Length} 个文件:");
                foreach (var file in files)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {file}");
                }
                
                // 获取实际的 ListBox（可能是从 ScrollViewer 或 Grid 触发的）
                ListBox listBox = sender as ListBox;
                if (listBox == null)
                {
                    // 如果是 ScrollViewer 或 Grid 触发，使用 LibrariesListBox
                    if (sender is ScrollViewer || sender is Grid)
                    {
                        listBox = LibrariesListBox;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[库拖拽] Drop - 无法识别的 sender 类型: {sender.GetType().Name}");
                        MessageBox.Show("无法识别拖拽目标", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                
                if (listBox == null)
                {
                    System.Diagnostics.Debug.WriteLine("[库拖拽] Drop - listBox 为 null");
                    MessageBox.Show("库列表未找到", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var point = e.GetPosition(listBox);
                var element = listBox.InputHitTest(point) as DependencyObject;
                
                // 查找 ListBoxItem
                while (element != null && !(element is ListBoxItem))
                {
                    element = VisualTreeHelper.GetParent(element);
                }
                
                Library targetLibrary = null;
                
                // 只有当明确拖拽到 ListBoxItem 上时，才使用该库
                // 如果拖拽到空白区域（element 不是 ListBoxItem），应该创建新库
                if (element is ListBoxItem item && item.Content is Library library)
                {
                    targetLibrary = library;
                    System.Diagnostics.Debug.WriteLine($"[库拖拽] Drop - 目标库: {targetLibrary.Name}");
                }
                else
                {
                    // 拖拽到空白区域，不检查 SelectedItem，直接创建新库
                    System.Diagnostics.Debug.WriteLine("[库拖拽] Drop - 拖拽到空白区域，将创建新库");
                }
                
                // 如果目标库为空（拖拽到空白区域），自动创建新库
                if (targetLibrary == null)
                {
                // 使用第一个文件/文件夹的名称作为库名
                if (files == null || files.Length == 0)
                {
                    return;
                }
                
                string firstPath = files[0];
                string libraryName = System.IO.Path.GetFileName(firstPath);
                
                // 如果名称为空（可能是根目录），使用路径的最后一部分
                if (string.IsNullOrEmpty(libraryName))
                {
                    libraryName = firstPath.TrimEnd('\\', '/');
                    if (libraryName.Length > 1)
                    {
                        libraryName = System.IO.Path.GetFileName(libraryName);
                    }
                    if (string.IsNullOrEmpty(libraryName))
                    {
                        libraryName = "新建库";
                    }
                }
                
                // 检查库名是否已存在，如果存在则添加序号
                string baseLibraryName = libraryName;
                int counter = 1;
                while (true)
                {
                    var existingLibraries = DatabaseManager.GetAllLibraries();
                    if (!existingLibraries.Any(l => l.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase)))
                    {
                        break;
                    }
                    libraryName = $"{baseLibraryName} ({counter})";
                    counter++;
                }
                
                try
                {
                    // 创建新库
                    var libraryId = DatabaseManager.AddLibrary(libraryName);
                    if (libraryId > 0)
                    {
                        // 获取新创建的库
                        targetLibrary = DatabaseManager.GetLibrary(libraryId);
                        if (targetLibrary == null)
                        {
                            MessageBox.Show("创建库失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                        
                        // 刷新库列表
                        _libraryService.LoadLibraries();
                        
                        // 选中新创建的库
                        EnsureSelectedItemVisible(LibrariesListBox, targetLibrary);
                    }
                    else if (libraryId < 0)
                    {
                        // 库已存在（虽然理论上不应该发生，因为我们已经检查过）
                        _libraryService.LoadLibraries();
                        var existingLibrary = DatabaseManager.GetAllLibraries()
                            .FirstOrDefault(l => l.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase));
                        if (existingLibrary != null)
                        {
                            targetLibrary = existingLibrary;
                            EnsureSelectedItemVisible(LibrariesListBox, targetLibrary);
                        }
                    }
                    else
                    {
                        MessageBox.Show("创建库失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建库失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            
                // 创建拖拽数据
                var dragData = new DragDropManager.DragDropData
                {
                    SourcePaths = files.ToList(),
                    TargetPath = targetLibrary.Id.ToString(), // 使用库ID作为目标标识
                    TargetType = DragDropManager.DropTargetType.Library,
                    Operation = DragDropManager.DragDropOperation.AddToLibrary,
                    TargetControl = sender as FrameworkElement
                };
                
                if (dragData.TargetControl == null)
                {
                    dragData.TargetControl = LibrariesListBox;
                }
                
                // 临时存储目标库，以便 ExecuteAddToLibrary 使用
                dragData.TargetControl.Tag = targetLibrary;
                
                // 确保拖拽动画被关闭（因为直接调用 Drop 事件，不经过 DoDragDrop，动画不会自动关闭）
                // 在调用 DragDropCompleted 之前关闭，避免动画在 MessageBox 显示时还在
                _dragDropManager.ForceCloseDragVisual();
                
                // 直接调用拖拽完成处理方法
                DragDropManager_DragDropCompleted(_dragDropManager, dragData);
                
                e.Handled = true;
                System.Diagnostics.Debug.WriteLine("[库拖拽] Drop - 处理完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[库拖拽] Drop 异常: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"拖拽操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DragDropManager_DragDropStarted(object sender, DragDropManager.DragDropData e)
        {
            // 拖拽开始
            System.Diagnostics.Debug.WriteLine($"开始拖拽 {e.SourcePaths.Count} 个项目");
        }

        private void DragDropManager_DragDropCompleted(object sender, DragDropManager.DragDropData e)
        {
            try
            {
                // 特殊处理：添加到库操作
                if (e.Operation == DragDropManager.DragDropOperation.AddToLibrary)
                {
                    // 从 TargetControl.Tag 获取目标库
                    Library targetLibrary = null;
                    if (e.TargetControl?.Tag is Library library)
                    {
                        targetLibrary = library;
                    }
                    else if (e.TargetPath != null && int.TryParse(e.TargetPath, out int libraryId))
                    {
                        // 从库ID获取库
                        targetLibrary = DatabaseManager.GetLibrary(libraryId);
                    }
                    
                    if (targetLibrary == null)
                    {
                        MessageBox.Show("无法确定目标库", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    
                    // 添加文件/文件夹路径到库
                    int successCount = 0;
                    int failCount = 0;
                    var failedItems = new List<string>();
                    
                    foreach (var sourcePath in e.SourcePaths)
                    {
                        try
                        {
                            // 检查路径是否存在
                            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                            {
                                failCount++;
                                failedItems.Add($"{System.IO.Path.GetFileName(sourcePath)} (路径不存在)");
                                continue;
                            }
                            
                            // 对于文件夹，添加文件夹路径
                            // 对于文件，添加文件路径（库可以包含文件路径）
                            string pathToAdd = sourcePath;
                            
                            // 检查路径是否已存在
                            var existingPaths = DatabaseManager.GetLibraryPaths(targetLibrary.Id);
                            if (!existingPaths.Any(p => p.Path.Equals(pathToAdd, StringComparison.OrdinalIgnoreCase)))
                            {
                                DatabaseManager.AddLibraryPath(targetLibrary.Id, pathToAdd);
                                successCount++;
                            }
                            else
                            {
                                // 路径已存在，跳过但不算失败
                                failCount++;
                                failedItems.Add($"{System.IO.Path.GetFileName(sourcePath)} (已存在于库中)");
                            }
                        }
                        catch (Exception ex)
                        {
                            failCount++;
                            failedItems.Add($"{System.IO.Path.GetFileName(sourcePath)} ({ex.Message})");
                        }
                    }
                    
                    // 不显示成功提示（减少提示框）
                    // 如果有失败项，才显示错误提示
                    if (failCount > 0 && successCount == 0)
                    {
                        var message = $"添加失败:\n{string.Join("\n", failedItems)}";
                        MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    
                    // 如果当前在库模式且是当前库，刷新显示
                    if (_currentLibrary != null && _currentLibrary.Id == targetLibrary.Id)
                    {
                        _libraryService.LoadLibraryFiles(_currentLibrary,
                            (path) => DatabaseManager.GetFolderSize(path),
                            (bytes) => FormatFileSize(bytes));
                    }
                    
                    // 刷新库列表
                    _libraryService.LoadLibraries();
                    
                    return;
                }
                
                // 执行其他拖拽操作
                bool success = _dragDropManager.ExecuteDragDropOperation(e);
                
                if (success)
                {
                    // 操作成功，刷新界面
                    LoadFiles();
                    
                    // 不再显示成功消息，因为 ExecuteDragDropOperation 内部已经处理了
                    System.Diagnostics.Debug.WriteLine($"拖拽操作完成: {e.SourcePaths.Count} 个项目");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"拖拽操作失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DragDropManager_DragDropCancelled(object sender, EventArgs e)
        {
            // 拖拽取消，确保恢复选中状态和背景
            System.Diagnostics.Debug.WriteLine("拖拽已取消，恢复选中状态");
            
            // 使用 Dispatcher 延迟执行，确保在拖拽操作完全结束后再恢复
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (FileBrowser?.FilesList != null && _dragDropManager != null)
                {
                    // 刷新文件列表，确保选中状态和背景正确
                    var selectedPaths = new List<string>();
                    if (FileBrowser.FilesSelectedItems != null)
                    {
                        foreach (var item in FileBrowser.FilesSelectedItems)
                        {
                            if (item is FileSystemItem fileItem)
                            {
                                selectedPaths.Add(fileItem.Path);
                            }
                        }
                    }
                    
                    // 清除选中并重新设置，触发背景更新
                    if (FileBrowser.FilesSelectedItems != null)
                    {
                        FileBrowser.FilesSelectedItems.Clear();
                        if (FileBrowser.FilesList.Items != null)
                        {
                            foreach (var item in FileBrowser.FilesList.Items)
                            {
                                if (item is FileSystemItem fileItem && selectedPaths.Contains(fileItem.Path))
                                {
                                    FileBrowser.FilesSelectedItems.Add(item);
                                }
                            }
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private string GetOperationText(DragDropManager.DragDropOperation operation)
        {
            return operation switch
            {
                DragDropManager.DragDropOperation.Move => "移动",
                DragDropManager.DragDropOperation.Copy => "复制",
                DragDropManager.DragDropOperation.CreateLink => "创建链接",
                DragDropManager.DragDropOperation.AddToQuickAccess => "添加到快速访问",
                DragDropManager.DragDropOperation.AddToLibrary => "添加到库",
                DragDropManager.DragDropOperation.AddTag => "添加标签",
                _ => "操作"
            };
        }

        #endregion

        #region 事件处理

        private void NavigateBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHistoryIndex > 0)
            {
                _currentHistoryIndex--;
                _currentPath = _navigationHistory[_currentHistoryIndex];
                ClearFilter();
                LoadCurrentDirectory();
            }
        }

        private void NavigateForward_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHistoryIndex < _navigationHistory.Count - 1)
            {
                _currentHistoryIndex++;
                _currentPath = _navigationHistory[_currentHistoryIndex];
                ClearFilter();
                LoadCurrentDirectory();
            }
        }

        private void NavigateUp_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPath))
            {
                return;
            }

            try
            {
                if (File.Exists(_currentPath))
                {
                    var dir = Path.GetDirectoryName(_currentPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        NavigateToPath(dir);
                    }
                    return;
                }

                var parent = Directory.GetParent(_currentPath);
                if (parent != null)
                {
                    NavigateToPath(parent.FullName);
                }
            }
            catch (ArgumentNullException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        private async void FileBrowser_PathChanged(object sender, string path)
        {
            if (Directory.Exists(path) || File.Exists(path))
            {
                NavigateToPath(path);
                return;
            }
            // 非有效路径：按搜索关键词处理（支持回车触发搜索）
            var keyword = path?.Trim() ?? "";
            if (!string.IsNullOrEmpty(keyword))
            {
                // 规范化前缀"搜索:"
                while (keyword.StartsWith("搜索:"))
                {
                    keyword = keyword.Substring("搜索:".Length).Trim();
                }
                await PerformSearchAsync(keyword, true, true);
            }
        }

        private void FileBrowser_BreadcrumbClicked(object sender, string path)
        {
            // 处理tag://路径，返回到标签浏览模式
            if (path == "tag://")
            {
                // 切换到标签模式（如果当前不在标签模式）
                if (_config.LastNavigationMode != "Tag")
                {
                    SwitchNavigationMode("Tag");
                }
                else
                {
                    // 已经在标签模式，清除当前选中的标签，显示所有标签
                    _currentTagFilter = null;
                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = null;
                        FileBrowser.AddressText = "";
                        FileBrowser.IsAddressReadOnly = true;
                        FileBrowser.SetTagBreadcrumb("标签");
                    }
                    HideEmptyStateMessage();
                }
                return;
            }
            
            NavigateToPath(path);
        }
        
        // 文件浏览控件的事件转发
        private void FileBrowser_FilesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilesListView_SelectionChanged(sender, e);
        }

        private void FileBrowser_FilesMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FilesListView_MouseDoubleClick(sender, e);
        }

        private void FileBrowser_FilesPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FilesListView_PreviewMouseDoubleClick(sender, e);
        }

        private void FileBrowser_FilesPreviewKeyDown(object sender, KeyEventArgs e)
        {
            FilesListView_PreviewKeyDown(sender, e);
        }

        private void FileBrowser_FilesPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            FilesListView_PreviewMouseLeftButtonDown(sender, e);
        }

        private void FileBrowser_FilesMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            FilesListView_MouseLeftButtonUp(sender, e);
        }

        private void FileBrowser_FilesPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            FilesListView_PreviewMouseDown(sender, e);
        }

        private void FileBrowser_GridViewColumnHeaderClick(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader_Click(sender, e);
        }

        private void FileBrowser_FilesSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ListView_SizeChanged(sender, e);
        }

        private void GridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            try
            {
                if (ColLeft != null)
                {
                    var w = ColLeft.Width.Value;
                    var newW = w + e.HorizontalChange;
                    if (newW < 0) newW = 0;
                    var min = Math.Max(0, ColLeft.MinWidth);
                    if (newW < min) newW = min;
                    ColLeft.Width = new GridLength(newW);
                }
            }
            catch { }
        }

        private void FileBrowser_FilesPreviewMouseDoubleClickForBlank(object sender, MouseButtonEventArgs e)
        {
            FilesListView_PreviewMouseDoubleClickForBlank(sender, e);
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ClearFilter();
            
            // 根据当前导航模式执行不同的刷新操作
            if (NavLibraryContent != null && NavLibraryContent.Visibility == Visibility.Visible)
            {
                // 库模式：刷新库列表
                _libraryService.LoadLibraries();
            }
            else if (NavTagContent != null && NavTagContent.Visibility == Visibility.Visible)
            {
                // 标签模式：刷新文件列表
                RefreshFileList();
            }
            else
            {
                // 路径模式：刷新当前目录
                LoadCurrentDirectory();
            }
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            ClearFilter();
            LoadCurrentDirectory();
        }

        private void ClearFilter()
        {
            // 清除过滤状态，恢复正常的文件浏览
            _currentTagFilter = null;
            // TagsListBox已移除，标签选择现在由TagTrain面板处理
            
            _currentFiles.Clear();
            if (FileBrowser != null)
                FileBrowser.FilesItemsSource = null;
            HideEmptyStateMessage();
        }

        private void FilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
            {
                ShowFileInfo(selectedItem);
                LoadFilePreview(selectedItem);
                LoadFileNotes(selectedItem);
                
                // 标签页：对图片执行AI预测并渲染到预测面板
                try
                {
                    if (NavTagContent != null && NavTagContent.Visibility == Visibility.Visible)
                    {
                        var ext = System.IO.Path.GetExtension(selectedItem.Path)?.ToLowerInvariant();
                        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
                        if (!selectedItem.IsDirectory && !string.IsNullOrEmpty(ext) && imageExtensions.Contains(ext))
                        {
                            // 预测占位
                            if (TagEditPanel != null)
                            {
                                TagEditPanel.PredictionPanel.Children.Clear();
                                TagEditPanel.NoPredictionText.Visibility = Visibility.Visible;
                                TagEditPanel.NoPredictionText.Text = "预测中...";
                            }
                            
                            Task.Run(() =>
                            {
                                var preds = OoiMRRIntegration.PredictTagsForImage(selectedItem.Path) ?? new List<TagTrain.Services.TagPredictionResult>();
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    RenderPredictionResults(preds);
                                }), System.Windows.Threading.DispatcherPriority.Background);
                            });
                        }
                        else
                        {
                            RenderPredictionResults(new List<TagTrain.Services.TagPredictionResult>());
                        }
                    }
                }
                catch { }
                
                // 如果选中的是文件夹且大小未计算，立即计算
                if (selectedItem.IsDirectory)
                {
                    // 检查大小是否已计算（Size为空、"-"、"计算中..."或null表示未计算）
                    if (string.IsNullOrEmpty(selectedItem.Size) || 
                        selectedItem.Size == "-" || 
                        selectedItem.Size == "计算中...")
                    {
                        // 立即计算该文件夹的大小
                        CalculateFolderSizeImmediately(selectedItem.Path);
                    }
                }
            }
            else
            {
                // 没有选择文件时，清除预览区和文件信息
                ClearPreviewAndInfo();
            }
        }
        
        private void ClearPreviewAndInfo()
        {
            // 清除图片预览
            if (RightPanel != null)
            {
                RightPanel.ClearImagePreview();
            }
            
            // 清空预测面板
            try
            {
                RenderPredictionResults(new List<TagTrain.Services.TagPredictionResult>());
            }
            catch { }
            
            // 清除其他预览：不要清空 Children，避免移除默认预览结构（DefaultPreviewText、ImagePreviewBorder）
            if (RightPanel?.PreviewGrid != null)
            {
                // 清除所有预览元素（保留 DefaultPreviewText 和 ImagePreviewBorder）
                for (int i = RightPanel.PreviewGrid.Children.Count - 1; i >= 0; i--)
                {
                    var child = RightPanel.PreviewGrid.Children[i];
                    // 保留 DefaultPreviewText 和 ImagePreviewBorder，清除其他元素（包括 SVG、PSD 等预览）
                    if (child != RightPanel.DefaultPreviewText && child != RightPanel.ImagePreviewBorder)
                    {
                        RightPanel.PreviewGrid.Children.RemoveAt(i);
                    }
                }
                
                // 显示默认提示文本
                var defaultText = RightPanel.PreviewGrid.Children.OfType<TextBlock>()
                    .FirstOrDefault(tb => tb.Name == "DefaultPreviewText");
                if (defaultText != null)
                {
                    defaultText.Visibility = Visibility.Visible;
                }
                
                // 同时确保图片预览边框处于隐藏状态
                var imageBorder = RightPanel.PreviewGrid.Children.OfType<Border>()
                    .FirstOrDefault(b => b.Name == "ImagePreviewBorder");
                if (imageBorder != null)
                {
                    imageBorder.Visibility = Visibility.Collapsed;
                }
            }
            
            // 清除文件信息
            if (FileBrowser?.FileInfoPanelControl != null)
            {
                FileBrowser.FileInfoPanelControl.Children.Clear();
            }
            
            // 清除备注
            if (RightPanel?.NotesTextBox != null)
            {
                RightPanel.NotesTextBox.Text = "";
            }
        }
        
        // 渲染 AI 预测结果到标签页的预测面板
        private void RenderPredictionResults(List<TagTrain.Services.TagPredictionResult> preds)
        {
            try
            {
                if (TagEditPanel == null) return;
                
                TagEditPanel.PredictionPanel.Children.Clear();
                if (preds == null || preds.Count == 0)
                {
                    TagEditPanel.NoPredictionText.Text = "暂无预测结果";
                    TagEditPanel.NoPredictionText.Visibility = Visibility.Visible;
                    return;
                }
                
                TagEditPanel.NoPredictionText.Visibility = Visibility.Collapsed;
                
                foreach (var p in preds.OrderByDescending(x => x.Confidence).Take(5))
                {
                    var name = OoiMRRIntegration.GetTagName(p.TagId) ?? p.TagId.ToString();
                    var border = new Border
                    {
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 245, 253)),
                        BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(144, 202, 249)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(4, 4, 4, 4)
                    };
                    var sp = new StackPanel { Orientation = Orientation.Horizontal };
                    sp.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,6,0) });
                    sp.Children.Add(new TextBlock { Text = $"{p.Confidence:P1}", Foreground = new SolidColorBrush(Colors.Gray) });
                    border.Child = sp;
                    TagEditPanel?.PredictionPanel?.Children.Add(border);
                }
            }
            catch { }
        }
        
        private void ShowEmptyLibraryMessage(string libraryName)
        {
            if (FileBrowser != null)
            {
                FileBrowser.ShowEmptyState($"库 \"{libraryName}\" 没有添加任何位置。\n\n请在管理库中添加位置。");
            }
        }
        
        private void HideEmptyStateMessage()
        {
            if (FileBrowser != null)
            {
                FileBrowser.HideEmptyState();
            }
        }

        private void ShowEmptyStateMessage(string message)
        {
            if (FileBrowser != null)
            {
                FileBrowser.ShowEmptyState(message);
            }
        }

        private void FilesListView_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 如果双击发生在列头或分隔线，拦截，不进行文件/文件夹打开
            var src = e.OriginalSource as DependencyObject;
            if (src != null)
            {
                if (FindAncestor<GridViewColumnHeader>(src) != null ||
                    FindAncestor<System.Windows.Controls.Primitives.Thumb>(src) != null)
                {
                    e.Handled = true;
                    return;
                }
            }
            // Preview 事件优先处理
            HandleDoubleClick(e);
        }
        
        private void FilesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 如果双击发生在列头或分隔线，拦截
            var src = e.OriginalSource as DependencyObject;
            if (src != null)
            {
                if (FindAncestor<GridViewColumnHeader>(src) != null ||
                    FindAncestor<System.Windows.Controls.Primitives.Thumb>(src) != null)
                {
                    e.Handled = true;
                    return;
                }
            }
            // 备用处理（如果 Preview 事件没有被处理）
            HandleDoubleClick(e);
        }
        
        private void HandleDoubleClick(MouseButtonEventArgs e)
        {
            // 检测鼠标中键或Ctrl键，强制打开新标签页
            bool forceNewTab = (e.ChangedButton == MouseButton.Middle) || 
                              ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);
            
            // 获取双击位置对应的项目
            if (FileBrowser?.FilesList == null) return;
            var hitResult = VisualTreeHelper.HitTest(FileBrowser.FilesList, e.GetPosition(FileBrowser.FilesList));
            if (hitResult == null) return;
            
            // 向上查找 ListViewItem
            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != FileBrowser.FilesList)
            {
                if (current is System.Windows.Controls.ListViewItem item)
                {
                    if (item.Content is FileSystemItem selectedItem)
                    {
                        if (selectedItem.IsDirectory)
                        {
                            // 检查路径是否存在
                            if (!Directory.Exists(selectedItem.Path))
                            {
                                MessageBox.Show($"文件夹路径不存在: {selectedItem.Path}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            
                            // 如果是库模式，切换到路径模式并导航
                            if (_currentLibrary != null)
                            {
                                // 切换到路径模式
                                _currentLibrary = null;
                                SwitchNavigationMode("Path");
                            }
                            
                            // 立即导航，不等待任何异步操作
                            CreateTab(selectedItem.Path, forceNewTab);
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            // 打开文件
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = selectedItem.Path,
                                    UseShellExecute = true
                                });
                                e.Handled = true;
                                return;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
            
            // 备用：使用选中项
            if (FileBrowser?.FilesSelectedItem is FileSystemItem backupItem)
            {
                System.Diagnostics.Debug.WriteLine($"[双击备用] 使用选中项: {backupItem.Name}");
                if (backupItem.IsDirectory)
                {
                    if (Directory.Exists(backupItem.Path))
                    {
                        if (_currentLibrary != null)
                        {
                            _currentLibrary = null;
                            SwitchNavigationMode("Path");
                        }
                        NavigateToPath(backupItem.Path);
                        e.Handled = true;
                    }
                }
                else
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = backupItem.Path,
                            UseShellExecute = true
                        });
                        e.Handled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        
        private void FilesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 记录鼠标按下位置，用于区分点击和拖动
            var listView = sender as ListView;
            if (listView == null)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 支持双击列头分隔线（Thumb）自动适配列宽
            if (e.ClickCount == 2)
            {
                var src = e.OriginalSource as DependencyObject;
                if (src != null)
                {
                    var header = FindAncestor<GridViewColumnHeader>(src);
                    var thumb = FindAncestor<System.Windows.Controls.Primitives.Thumb>(src);
                    if (header != null && thumb != null && header.Column != null)
                    {
                        AutoSizeGridViewColumn(header.Column);
                        e.Handled = true;
                        return;
                    }
                }
            }

            System.Windows.Point hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);
            
            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;
                int depth = 0;
                while (current != null && current != listView && depth < 10)
                {
                    string typeName = current.GetType().Name;
                    
                    // 检查是否是列头相关元素
                    if (current is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }
                    
                    // 检查是否是 Thumb（调整大小的拖拽句柄）
                    if (current.GetType().Name.Contains("Thumb") || current.GetType().Name == "Thumb")
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }
                    
                    // 检查父元素是否是 GridViewColumnHeader
                    var parent = VisualTreeHelper.GetParent(current);
                    if (parent is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }
                    
                    current = parent;
                    depth++;
                }
                
                // 额外检查：如果点击位置在列头行的 Y 坐标范围内，也不处理
                if (listView.View is GridView gridView && gridView.Columns.Count > 0)
                {
                    // 获取列头行的高度
                    if (hitPoint.Y < 30) // 列头通常高度约为 25-30 像素
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        // 不设置 e.Handled，让列头的排序和调整宽度功能正常工作
                        return;
                    }
                }
            }

            // 不是在列头区域，记录按下位置
            _mouseDownPoint = e.GetPosition(listView);
            _isMouseDownOnListView = true;
            _isMouseDownOnColumnHeader = false; // 清除列头标志
            
            // 检查是否点击在空白区域（不是 ListViewItem）
            bool isListViewItem = false;
            
            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;
                
                // 向上查找，检查是否点击在 ListViewItem 上
                while (current != null && current != listView)
                {
                    if (current is System.Windows.Controls.ListViewItem)
                    {
                        isListViewItem = true;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
            
            // 如果点击在空白区域（hitResult 为 null 或不是 ListViewItem），清除选择
            if (!isListViewItem && e.ChangedButton == MouseButton.Left)
            {
                listView.SelectedItem = null;
                listView.SelectedItems.Clear();
                // SelectionChanged 事件会自动触发，调用 ClearPreviewAndInfo()
            }
        }
        
        private void FilesListView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 处理鼠标中键点击打开新标签页
            if (e.ChangedButton == MouseButton.Middle)
            {
                var listView = sender as ListView;
                if (listView == null) return;

                // 获取点击位置对应的项目
                var hitResult = VisualTreeHelper.HitTest(listView, e.GetPosition(listView));
                if (hitResult == null) return;

                // 向上查找 ListViewItem
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listView)
                {
                    if (current is System.Windows.Controls.ListViewItem item)
                    {
                        if (item.Content is FileSystemItem selectedItem)
                        {
                            if (selectedItem.IsDirectory)
                            {
                                // 创建新标签页并导航到该路径（添加错误处理）
                                try
                                {
                                    if (Directory.Exists(selectedItem.Path))
                                    {
                                        CreateTab(selectedItem.Path);
                                        e.Handled = true;
                                        return;
                                    }
                                    else
                                    {
                                        MessageBox.Show($"路径不存在: {selectedItem.Path}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                        e.Handled = true;
                                        return;
                                    }
                                }
                                catch (UnauthorizedAccessException ex)
                                {
                                    MessageBox.Show($"无法访问路径: {selectedItem.Path}\n\n{ex.Message}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    e.Handled = true;
                                    return;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"无法打开路径: {selectedItem.Path}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
        // DrivesListBox_PreviewMouseDown, QuickAccessListBox_PreviewMouseDown, FavoritesListBox_PreviewMouseDown
        // 已迁移到对应的服务中处理，这里保留空方法以兼容 XAML 绑定
        private void DrivesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 事件处理已迁移到 QuickAccessService，此方法保留以兼容 XAML
        }

        private void QuickAccessListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 事件处理已迁移到 QuickAccessService，此方法保留以兼容 XAML
        }

        private void FavoritesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 事件处理已迁移到 FavoriteService，此方法保留以兼容 XAML
        }

        private void FilesListView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 如果按下时在列头区域，无论抬起位置在哪里，都不处理清除选中
            if (_isMouseDownOnColumnHeader)
            {
                _isMouseDownOnColumnHeader = false;
                _isMouseDownOnListView = false;
                return;
            }
            
            // 只有在按下和抬起都在 ListView 上，且没有明显移动时，才处理点击空白区域
            if (!_isMouseDownOnListView)
                return;

            var listView = sender as ListView;
            if (listView == null)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 首先检查事件的原始源，看是否是列头相关元素
            var originalSource = e.OriginalSource as DependencyObject;
            DependencyObject checkSource = originalSource;
            while (checkSource != null)
            {
                if (checkSource is GridViewColumnHeader)
                {
                    _isMouseDownOnListView = false;
                    return;
                }
                if (checkSource.GetType().Name.Contains("Thumb") || checkSource.GetType().Name == "Thumb")
                {
                    _isMouseDownOnListView = false;
                    return;
                }
                checkSource = VisualTreeHelper.GetParent(checkSource);
            }

            System.Windows.Point mouseUpPoint = e.GetPosition(listView);
            
            // 计算鼠标移动距离
            double distance = Math.Sqrt(Math.Pow(mouseUpPoint.X - _mouseDownPoint.X, 2) + 
                                      Math.Pow(mouseUpPoint.Y - _mouseDownPoint.Y, 2));
            
            // 如果移动距离超过阈值，说明是拖动而不是点击，不处理
            if (distance > SystemParameters.MinimumHorizontalDragDistance)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 额外检查：如果抬起位置在列头区域，也不处理
            if (mouseUpPoint.Y < 30)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 检测点击位置是否是空白区域
            System.Windows.Point hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);
            
            if (hitResult != null)
            {
                // 向上查找，看是否点击在 ListViewItem 上
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listView)
                {
                    if (current is ListViewItem)
                    {
                        // 点击在 ListViewItem 上，不处理（让默认行为处理）
                        _isMouseDownOnListView = false;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                // 如果到达 ListView 都没有找到 ListViewItem，说明点击的是空白区域
                // 再次检查是否点击在列头相关区域
                current = hitResult.VisualHit;
                while (current != null)
                {
                    if (current is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                // 点击的是空白区域，清除文件列表选中（但保留库的选择状态）
                if (listView.SelectedItems.Count > 0)
                {
                    listView.SelectedItems.Clear();
                }
                
                // 在库模式下，确保库的选择状态保持正确显示
                if (_currentLibrary != null && LibrariesListBox != null)
                {
                    // 延迟执行，确保UI更新完成
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 确保库列表中的选择状态正确
                        if (LibrariesListBox.SelectedItem != _currentLibrary)
                        {
                            LibrariesListBox.SelectedItem = _currentLibrary;
                        }
                        // 确保库的高亮显示正确
                        _libraryService.HighlightLibrary(_currentLibrary);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            
            _isMouseDownOnListView = false;
            _isMouseDownOnColumnHeader = false; // 清除列头标志
        }
        
        private void RightPanel_PreviewMiddleClickRequested(object sender, MouseButtonEventArgs e)
        {
            // 预览区中键打开文件
            if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedItem.Path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+W 或 Ctrl+F4: 关闭当前标签页
            if ((e.Key == Key.W || e.Key == Key.F4) && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_activeTab != null && _pathTabs.Count > 1)
                {
                    CloseTab(_activeTab);
                    e.Handled = true;
                    return;
                }
            }
            
            // Ctrl+T: 新建标签页（打开桌面）
            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                CreateTab(desktopPath);
                e.Handled = true;
                return;
            }
            
            // Ctrl+Tab: 切换到下一个标签页
            if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.Control && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                if (_pathTabs.Count > 1)
                {
                    var currentIndex = _pathTabs.IndexOf(_activeTab);
                    var nextIndex = (currentIndex + 1) % _pathTabs.Count;
                    SwitchToTab(_pathTabs[nextIndex]);
                    e.Handled = true;
                    return;
                }
            }
            
            // Ctrl+Shift+Tab: 切换到上一个标签页
            if (e.Key == Key.Tab && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                if (_pathTabs.Count > 1)
                {
                    var currentIndex = _pathTabs.IndexOf(_activeTab);
                    var prevIndex = (currentIndex - 1 + _pathTabs.Count) % _pathTabs.Count;
                    SwitchToTab(_pathTabs[prevIndex]);
                    e.Handled = true;
                    return;
                }
            }
            
            // Ctrl+N: 新建文件夹
            if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Control)
            {
                NewFolder_Click(null, null);
                e.Handled = true;
                return;
            }
            
            // Ctrl+Shift+N: 新建文件夹（Windows标准）
            if (e.Key == Key.N && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                NewFolder_Click(null, null);
                e.Handled = true;
                return;
            }
            
            // F5: 刷新
            if (e.Key == Key.F5)
            {
                Refresh_Click(null, null);
                e.Handled = true;
                return;
            }
            
            // 检查地址栏是否处于编辑模式或有焦点
            // 如果地址栏正在编辑，让地址栏处理这些快捷键，不在这里处理
            bool isAddressBarEditing = FileBrowser?.AddressBar != null && 
                                       (FileBrowser.AddressBar.IsEditMode || FileBrowser.AddressBar.IsAddressTextBoxFocused);
            
            // 如果地址栏正在编辑，检查焦点元素是否是 TextBox
            if (isAddressBarEditing)
            {
                var focusedElement = Keyboard.FocusedElement;
                if (focusedElement is System.Windows.Controls.TextBox)
                {
                    // 地址栏的 TextBox 有焦点，不处理这些快捷键，让地址栏处理
                    return;
                }
            }
            
            // Ctrl+A: 全选（在文件列表中，但不在地址栏中）
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (!isAddressBarEditing && FileBrowser?.FilesList != null && FileBrowser.FilesList.Items.Count > 0)
                {
                    if (FileBrowser?.FilesList != null)
                        FileBrowser.FilesList.SelectAll();
                    e.Handled = true;
                    return;
                }
            }
            
            // Ctrl+C: 复制（如果文件列表有焦点，但不在地址栏中）
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (!isAddressBarEditing && FileBrowser?.FilesList != null && FileBrowser.FilesList.IsFocused)
                {
                    Copy_Click(null, null);
                    e.Handled = true;
                    return;
                }
            }
            
            // Ctrl+V: 粘贴（如果文件列表有焦点，但不在地址栏中）
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (!isAddressBarEditing && FileBrowser?.FilesList != null && FileBrowser.FilesList.IsFocused)
                {
                    Paste_Click(null, null);
                    e.Handled = true;
                    return;
                }
            }
            
            // Ctrl+X: 剪切（如果文件列表有焦点，但不在地址栏中）
            if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (!isAddressBarEditing && FileBrowser?.FilesList != null && FileBrowser.FilesList.IsFocused)
                {
                    Cut_Click(null, null);
                    e.Handled = true;
                    return;
                }
            }
            
            // Delete: 删除（如果文件列表有焦点，且不在文本框中）
            if (e.Key == Key.Delete)
            {
                var focusedElement = Keyboard.FocusedElement;
                if (focusedElement is TextBox || focusedElement is TextBlock)
                {
                    // 在文本框中，不处理
                    return;
                }
                if (FileBrowser?.FilesSelectedItems != null && FileBrowser.FilesSelectedItems.Count > 0)
                {
                    Delete_Click(null, null);
                    e.Handled = true;
                    return;
                }
            }
            
            // F2: 重命名（如果文件列表有焦点）
            if (e.Key == Key.F2)
            {
                var focusedElement = Keyboard.FocusedElement;
                if (focusedElement is TextBox)
                {
                    // 在文本框中，不处理
                    return;
                }
                if (FileBrowser?.FilesSelectedItem != null)
                {
                    Rename_Click(null, null);
                    e.Handled = true;
                    return;
                }
            }
            
            // 空格键触发 QuickLook 预览
            if (e.Key == Key.Space)
            {
                // 检查是否有选中的文件
                if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
                {
                    // 检查 QuickLook 是否安装
                    if (OoiMRR.Previews.PreviewHelper.IsQuickLookInstalled())
                    {
                        try
                        {
                            var quickLookPath = OoiMRR.Previews.PreviewHelper.GetQuickLookPath();
                            if (!string.IsNullOrEmpty(quickLookPath))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = quickLookPath,
                                    Arguments = $@"""{selectedItem.Path}""",
                                    UseShellExecute = false
                                });
                                e.Handled = true; // 标记事件已处理
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法启动 QuickLook: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            // 空格键触发 QuickLook 预览
            if (e.Key == Key.Space)
            {
                // 检查是否有选中的文件
                if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem && !selectedItem.IsDirectory)
                {
                    // 检查 QuickLook 是否安装
                    if (OoiMRR.Previews.PreviewHelper.IsQuickLookInstalled())
                    {
                        try
                        {
                            var quickLookPath = OoiMRR.Previews.PreviewHelper.GetQuickLookPath();
                            if (!string.IsNullOrEmpty(quickLookPath))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = quickLookPath,
                                    Arguments = $@"""{selectedItem.Path}""",
                                    UseShellExecute = false
                                });
                                e.Handled = true; // 标记事件已处理
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法启动 QuickLook: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void RightPanel_PreviewOpenFileRequested(object sender, string filePath)
        {
            // 预览区打开文件请求 - 在当前预览区显示文件内容
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                LoadFilePreview(new FileSystemItem
                {
                    Path = filePath,
                    Name = System.IO.Path.GetFileName(filePath),
                    IsDirectory = false
                });
            }
        }

        #endregion

        #region 文件信息显示

        private void ShowFileInfo(FileSystemItem item)
        {
            // 恢复列2的详细信息显示
            if (FileBrowser?.FileInfoPanelControl == null) return;
            
            FileBrowser.FileInfoPanelControl.Children.Clear();

            if (item.IsDirectory)
            {
                // 文件夹详细信息
                try
                {
                    var files = Directory.GetFiles(item.Path);
                    var directories = Directory.GetDirectories(item.Path);
                    long totalSize = files.Sum(f => new FileInfo(f).Length);

                    var infoItems = new[]
                    {
                        ("名称", item.Name),
                        ("路径", item.Path),
                        ("类型", "文件夹"),
                        ("文件数", files.Length.ToString()),
                        ("文件夹数", directories.Length.ToString()),
                        ("总大小", FormatFileSize(totalSize)),
                        ("修改日期", item.ModifiedDate),
                        ("标签", item.Tags)
                    };

                    foreach (var (label, value) in infoItems)
                    {
                        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                        panel.Children.Add(new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.Bold, Width = 80 });
                        panel.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
                        if (FileBrowser?.FileInfoPanelControl != null)
                            FileBrowser.FileInfoPanelControl.Children.Add(panel);
                    }
                }
                catch (Exception ex)
                {
                    var errorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                    errorPanel.Children.Add(new TextBlock { Text = "错误: ", FontWeight = FontWeights.Bold, Width = 80 });
                    errorPanel.Children.Add(new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Red });
                    if (FileBrowser?.FileInfoPanelControl != null)
                        FileBrowser.FileInfoPanelControl.Children.Add(errorPanel);
                }
            }
            else
            {
                // 文件详细信息
                var infoItems = new List<(string label, string value)>
                {
                    ("名称", item.Name),
                    ("路径", item.Path),
                    ("类型", item.Type),
                    ("大小", item.Size),
                    ("修改日期", item.ModifiedDate),
                    ("标签", item.Tags)
                };

                // 如果是图片文件，添加尺寸信息
                var fileExtension = System.IO.Path.GetExtension(item.Path)?.ToLowerInvariant();
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif", ".svg", ".psd", ".ico" };
                if (!string.IsNullOrEmpty(fileExtension) && imageExtensions.Contains(fileExtension))
                {
                    try
                    {
                        string imageSize = GetImageDimensions(item.Path);
                        if (!string.IsNullOrEmpty(imageSize))
                        {
                            infoItems.Insert(4, ("尺寸", imageSize)); // 在"大小"之后插入
                        }
                    }
                    catch
                    {
                        // 获取尺寸失败，忽略
                    }
                }

                foreach (var (label, value) in infoItems)
                {
                    var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                    panel.Children.Add(new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.Bold, Width = 80 });
                    panel.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
                    if (FileBrowser?.FileInfoPanelControl != null)
                        FileBrowser.FileInfoPanelControl.Children.Add(panel);
                }
            }
        }

        /// <summary>
        /// 获取图片的尺寸信息
        /// </summary>
        private string GetImageDimensions(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                    return null;

                var extension = System.IO.Path.GetExtension(imagePath)?.ToLowerInvariant();
                
                // 优先使用 Magick.NET（支持更多格式，包括 SVG 和 PSD）
                try
                {
                    using (var image = new ImageMagick.MagickImage(imagePath))
                    {
                        return $"{image.Width} × {image.Height} 像素";
                    }
                }
                catch
                {
                    // 如果 Magick.NET 失败，尝试使用 System.Drawing.Image（仅支持常见格式）
                    try
                    {
                        using (var image = System.Drawing.Image.FromFile(imagePath))
                        {
                            return $"{image.Width} × {image.Height} 像素";
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private void CleanupPreviousPreview()
        {
            if (RightPanel?.PreviewGrid == null) return;
            
            // 查找并停止所有MediaElement（视频预览）
            var mediaElements = FindVisualChildren<System.Windows.Controls.MediaElement>(RightPanel.PreviewGrid).ToList();
            foreach (var mediaElement in mediaElements)
            {
                try
                {
                    mediaElement.Stop();
                    mediaElement.Source = null;
                    mediaElement.Close();
                }
                catch
                {
                    // 忽略清理错误
                }
            }
            
            // 查找并停止所有DispatcherTimer（视频预览的定时器）
            // 注意：DispatcherTimer无法直接从UI元素中查找，需要在VideoPreview中管理
            // 这里只清理MediaElement即可
        }

        private void LoadFilePreview(FileSystemItem item)
        {
            if (RightPanel?.PreviewGrid == null) return;
            
            // 先清理之前的预览资源（特别是视频的MediaElement）
            CleanupPreviousPreview();
            
            // 检查是否是图片文件，如果是则使用TagTrain样式的图片预览
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
            var fileExtension = Path.GetExtension(item.Path)?.ToLowerInvariant();
            
            if (!item.IsDirectory && !string.IsNullOrEmpty(fileExtension) && imageExtensions.Contains(fileExtension))
            {
                // 先清除图片预览状态，确保干净的状态
                RightPanel.ClearImagePreview();
                
                // 使用TagTrain样式的图片预览
                // DisplayImagePreview 内部会清理其他预览元素，保留 ImagePreviewBorder 和 DefaultPreviewText
                RightPanel.DisplayImagePreview(item.Path);
                // 注意：图片预览时可以集成TagTrain的训练标记功能（后续实现）
                return;
            }
            
            // 对于非图片文件，先清除图片预览，然后使用原有的预览方式（通过PreviewGrid）
            RightPanel.ClearImagePreview();
            
            // 确保ImagePreviewBorder不会遮挡预览内容
            if (RightPanel.ImagePreviewBorder != null)
            {
                RightPanel.ImagePreviewBorder.Visibility = Visibility.Collapsed;
                Panel.SetZIndex(RightPanel.ImagePreviewBorder, 0);
            }
            
            // 清理PreviewGrid中的其他预览元素（保留DefaultPreviewText和ImagePreviewBorder）
            if (RightPanel.PreviewGrid != null)
            {
                for (int i = RightPanel.PreviewGrid.Children.Count - 1; i >= 0; i--)
                {
                    var child = RightPanel.PreviewGrid.Children[i];
                    // 保留DefaultPreviewText和ImagePreviewBorder，清除其他元素
                    if (child != RightPanel.DefaultPreviewText && child != RightPanel.ImagePreviewBorder)
                    {
                        RightPanel.PreviewGrid.Children.RemoveAt(i);
                    }
                }
            }

            try
            {
                // 设置刷新回调
                OoiMRR.Previews.PreviewFactory.OnFileListRefreshRequested = () =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        LoadCurrentDirectory();
                    });
                };
                
                // 设置在新标签页中打开文件夹的回调
                OoiMRR.Previews.PreviewFactory.OnOpenFolderInNewTab = (folderPath) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        CreateTab(folderPath);
                    });
                };

                // PreviewFactory 会自动处理文件夹和文件
                var previewElement = OoiMRR.Previews.PreviewFactory.CreatePreview(item.Path);
                if (previewElement != null)
                {
                    // 确保预览元素在ImagePreviewBorder之上
                    Panel.SetZIndex(previewElement, 1);
                    RightPanel.PreviewGrid.Children.Add(previewElement);
                    
                    // 隐藏默认预览文本
                    var defaultText = RightPanel.PreviewGrid.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => tb.Name == "DefaultPreviewText");
                    if (defaultText != null)
                    {
                        defaultText.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // 如果预览元素为null，显示默认提示
                    var defaultText = new TextBlock
                    {
                        Text = "无法创建预览",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        FontSize = 14
                    };
                    RightPanel.PreviewGrid.Children.Add(defaultText);
                }
                
                // 延迟绑定按钮事件，确保UI元素已完全加载
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 为预览元素中的按钮绑定事件
                    AttachPreviewButtonEvents(previewElement, item.Path);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                RightPanel.PreviewGrid.Children.Add(new TextBlock 
                { 
                    Text = $"预览失败: {ex.Message}", 
                    HorizontalAlignment = HorizontalAlignment.Center, 
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = System.Windows.Media.Brushes.Red
                });
            }
        }
        
        private void AttachPreviewButtonEvents(UIElement element, string filePath)
        {
            // 递归查找所有按钮并绑定事件
            if (element == null) return;
            
            var allElements = FindVisualChildren<Button>(element).ToList();
            
            foreach (var button in allElements)
            {
                if (button.Tag is string tagValue)
                {
                    // 检查是否是"打开文件夹"按钮
                    if (tagValue.StartsWith("OpenFolder:"))
                    {
                        string folderPath = tagValue.Length > "OpenFolder:".Length 
                            ? tagValue.Substring("OpenFolder:".Length) 
                            : "";
                        
                        // 清除可能存在的旧事件处理程序
                        button.Click -= Button_OpenFolderClick;
                        
                        // 创建新的事件处理程序
                        RoutedEventHandler handler = (s, e) =>
                        {
                            e.Handled = true; // 标记事件已处理
                            
                            try
                            {
                                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Opening folder in new tab: {folderPath}");
                                    // 在新标签页中打开文件夹
                                    CreateTab(folderPath);
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Folder path does not exist: {folderPath}");
                                    MessageBox.Show($"文件夹路径不存在: {folderPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error opening folder: {ex.Message}");
                                MessageBox.Show($"无法打开文件夹: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        };
                        
                        button.Click += handler;
                        continue;
                    }
                    
                    // 原有的预览区打开按钮逻辑
                    if (tagValue == filePath)
                    {
                        string content = button.Content?.ToString() ?? "";
                        if (content.Contains("预览区打开"))
                        {
                            button.Click -= PreviewButton_Click;
                            button.Click += PreviewButton_Click;
                        }
                    }
                }
            }
        }
        
        private void Button_OpenFolderClick(object sender, RoutedEventArgs e)
        {
            // 这个方法不会被使用，只是为了能够清除事件
        }
        
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            // 预览区打开按钮点击 - 在预览区中重新加载文件
            var button = sender as Button;
            var filePath = button?.Tag as string;
            if (!string.IsNullOrEmpty(filePath))
            {
                RightPanel_PreviewOpenFileRequested(sender, filePath);
            }
        }

        #endregion

        #region 备注功能

        private void LoadFileNotes(FileSystemItem item)
        {
            if (RightPanel?.NotesTextBox == null) return;
            
            if (item != null)
            {
                var notes = DatabaseManager.GetFileNotes(item.Path);
                RightPanel.NotesTextBox.Text = notes;
            }
            else
            {
                RightPanel.NotesTextBox.Text = "";
            }
        }

        private void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 备注文本变化时，实时更新列表中显示的备注
            if (RightPanel?.NotesTextBox == null) return;
            if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
            {
                var notesText = RightPanel.NotesTextBox.Text;
                // 更新备注的第一行显示
                if (!string.IsNullOrEmpty(notesText))
                {
                    var firstLine = notesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                    selectedItem.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                }
                else
                {
                    selectedItem.Notes = "";
                }
                
                // 刷新显示
                if (FileBrowser?.FilesList != null)
                    FileBrowser.FilesList.Items.Refresh();
            }
        }
        private async void NotesAutoSaved_Handler(object sender, RoutedEventArgs e)
        {
            if (RightPanel?.NotesTextBox == null) return;
            
            try
            {
                if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
                {
                    // 异步保存，提升性能
                    await DatabaseManager.SetFileNotesAsync(selectedItem.Path, RightPanel.NotesTextBox.Text);
                    
                    // 确保备注显示已更新
                    var notesText = RightPanel.NotesTextBox.Text;
                    if (!string.IsNullOrEmpty(notesText))
                    {
                        var firstLine = notesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                        selectedItem.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                    }
                    else
                    {
                        selectedItem.Notes = "";
                    }
                    
                    // 刷新显示
                    if (FileBrowser?.FilesList != null)
                    FileBrowser.FilesList.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存备注失败: {ex.Message}");
            }
        }

        #endregion

        #region 库功能

        #region LibraryService 事件处理器

        /// <summary>
        /// 库列表已加载事件处理器
        /// </summary>
        private void LibraryService_LibrariesLoaded(object sender, List<Library> libraries)
        {
            if (LibrariesListBox == null) return;
            
            var currentSelected = LibrariesListBox.SelectedItem; // 保存当前选中项
            LibrariesListBox.ItemsSource = null; // 先清空以强制刷新
            LibrariesListBox.ItemsSource = libraries;
            LibrariesListBox.Items.Refresh(); // 强制刷新显示
            
            // 如果有之前选中的库，恢复选中状态并确保视觉状态正确
            if (currentSelected != null)
            {
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    EnsureSelectedItemVisible(LibrariesListBox, currentSelected);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 库文件已加载事件处理器
        /// </summary>
        private void LibraryService_LibraryFilesLoaded(object sender, LibraryFilesLoadedEventArgs e)
        {
            if (e.Library == null) return;
            
            _currentFiles.Clear();
            _currentFiles.AddRange(e.Files ?? new List<FileSystemItem>());
            
            System.Diagnostics.Debug.WriteLine($"[加载库文件] 合并后共有 {_currentFiles.Count} 项");

            // 应用排序
            SortFiles();

            System.Diagnostics.Debug.WriteLine($"[加载库文件] 设置 ItemsSource，文件数量: {_currentFiles.Count}");
            
            // 确保UI控件存在
            if (FileBrowser != null)
            {
                FileBrowser.FilesItemsSource = null; // 先清空
                FileBrowser.FilesItemsSource = _currentFiles; // 再设置
                FileBrowser.FilesList?.Items.Refresh(); // 强制刷新
            }

            // 高亮当前库（通过事件处理器处理）
            _libraryService.HighlightLibrary(e.Library);

            // 如果文件列表为空，显示空状态提示；否则隐藏
            if (e.IsEmpty || _currentFiles.Count == 0)
            {
                if (e.IsEmpty)
                {
                    ShowEmptyLibraryMessage(e.Library.Name);
                    ClearPreviewAndInfo();
                    ClearItemHighlights();
                    ClearTabsInLibraryMode();
                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = null;
                        FileBrowser.AddressText = e.Library.Name + " (无位置)";
                    }
                }
                else
                {
                    ShowEmptyStateMessage($"库 \"{e.Library.Name}\" 中没有文件或文件夹");
                }
            }
            else
            {
                HideEmptyStateMessage();
            }

            // 更新地址栏显示库名称
            if (FileBrowser != null)
            {
                FileBrowser.AddressText = e.Library.Name;
                FileBrowser.IsAddressReadOnly = true;
                FileBrowser.SetLibraryBreadcrumb(e.Library.Name);
                FileBrowser.NavUpEnabled = false;
            }
            
            System.Diagnostics.Debug.WriteLine($"[加载库文件] 完成，ItemsSource 已设置");
            
            // 更新当前库引用
            _currentLibrary = e.Library;
            _currentPath = null; // 标记当前在库模式下
        }

        /// <summary>
        /// 库需要高亮事件处理器
        /// </summary>
        private void LibraryService_LibraryHighlightRequested(object sender, Library library)
        {
            // 高亮库列表中的匹配项
            if (LibrariesListBox == null || library == null) return;
            
            try
            {
                foreach (var item in LibrariesListBox.Items)
                {
                    if (item is Library lib && lib.Id == library.Id)
                    {
                        LibrariesListBox.SelectedItem = item;
                        LibrariesListBox.ScrollIntoView(item);
                        break;
                    }
                }
            }
            catch { }
        }
        
        #region QuickAccessService 事件处理
        
        private void QuickAccessService_NavigateRequested(object sender, string path)
        {
            _lastLeftNavSource = "QuickAccess";
            NavigateToPath(path);
        }
        
        private void QuickAccessService_CreateTabRequested(object sender, string path)
        {
            _lastLeftNavSource = "QuickAccess";
            CreateTab(path);
        }
        
        #endregion
        
        #region FavoriteService 事件处理
        
        private void FavoriteService_NavigateRequested(object sender, string path)
        {
            _lastLeftNavSource = "Favorites";
            NavigateToPath(path);
        }
        
        private void FavoriteService_FileOpenRequested(object sender, string path)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void FavoriteService_CreateTabRequested(object sender, string path)
        {
            _lastLeftNavSource = "Favorites";
            CreateTab(path);
        }
        
        private bool _isHandlingFavoritesLoaded = false;
        private void FavoriteService_FavoritesLoaded(object sender, EventArgs e)
        {
            // 避免事件-加载的循环触发导致堆栈溢出
            if (_isHandlingFavoritesLoaded) return;
            try
            {
                _isHandlingFavoritesLoaded = true;
                _favoriteService?.LoadFavorites(FavoritesListBox);
            }
            finally
            {
                _isHandlingFavoritesLoaded = false;
            }
        }
        
        #endregion
        
        #region TagService 事件处理
        
        private void TagService_TagFilterRequested(object sender, OoiMRR.Services.Tag.TagFilterEventArgs e)
        {
            UpdateTagFilesUI(e.Tag, e.Files);
        }
        
        private void TagService_TagTabRequested(object sender, OoiMRR.Tag tag)
        {
            OpenTagInTab(tag, false);
        }
        
        private void TagService_TagsLoaded(object sender, EventArgs e)
        {
            // 标签列表已重新加载，可以在这里更新UI
            if (TagBrowsePanel != null)
            {
                TagBrowsePanel.LoadExistingTags();
            }
        }
        
        #endregion

        // LoadLibraries() 已迁移到 LibraryService

        // AddLibrary_Click() 已迁移到 LibraryService

        // ManageLibraries_Click() 已迁移到 LibraryService

        // LibrariesListBox_ContextMenuOpening() - 保留，用于UI状态管理
        private void LibrariesListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // 根据是否有选中项来启用/禁用菜单项
            bool hasSelection = LibrariesListBox.SelectedItem != null;
            
            if (LibraryContextMenu != null)
            {
                LibraryRenameMenuItem.IsEnabled = hasSelection;
                LibraryDeleteMenuItem.IsEnabled = hasSelection;
                LibraryManageMenuItem.IsEnabled = hasSelection;
            }
        }

        // LibraryRename_Click() - 重新实现，调用 LibraryService
        private void LibraryRename_Click(object sender, RoutedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                var dialog = new PathInputDialog("请输入新的库名称:");
                dialog.InputText = selectedLibrary.Name;
                if (dialog.ShowDialog() == true)
                {
                    var newName = dialog.InputText.Trim();
                    if (_libraryService.UpdateLibraryName(selectedLibrary.Id, newName))
                    {
                        // 如果当前库被重命名，更新当前库引用并恢复选中状态
                        if (_currentLibrary != null && _currentLibrary.Id == selectedLibrary.Id)
                        {
                            var updatedLibrary = _libraryService.GetLibrary(selectedLibrary.Id);
                            if (updatedLibrary != null)
                            {
                                _currentLibrary = updatedLibrary;
                                // 确保重命名后的库仍然被选中并正确显示
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    EnsureSelectedItemVisible(LibrariesListBox, updatedLibrary);
                                    _libraryService.LoadLibraryFiles(updatedLibrary,
                                        (path) => DatabaseManager.GetFolderSize(path),
                                        (bytes) => FormatFileSize(bytes));
                                }), System.Windows.Threading.DispatcherPriority.Loaded);
                            }
                        }
                    }
                }
            }
        }

        // LibraryDelete_Click() - 重新实现，调用 LibraryService
        private void LibraryDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                if (!ConfirmDialog.Show(
                    $"确定要删除库 \"{selectedLibrary.Name}\" 吗？\n这将删除库及其所有位置，但不会删除实际文件。",
                    "确认删除",
                    ConfirmDialog.DialogType.Question,
                    this))
                {
                    return;
                }

                if (_libraryService.DeleteLibrary(selectedLibrary.Id, selectedLibrary.Name))
                {
                    // 如果删除的是当前库，清空显示
                    if (_currentLibrary != null && _currentLibrary.Id == selectedLibrary.Id)
                    {
                        _currentLibrary = null;
                        _currentFiles.Clear();
                        if (FileBrowser != null)
                        {
                            FileBrowser.FilesItemsSource = null;
                            FileBrowser.AddressText = "";
                        }
                    }
                }
            }
        }

        // LibraryManage_Click() - 重新实现，调用 LibraryService
        private void LibraryManage_Click(object sender, RoutedEventArgs e)
        {
            ManageLibraries_Click(sender, e);
        }

        // LibraryManageBottomBtn_Click() - 重新实现，调用 LibraryService
        private void LibraryManageBottomBtn_Click(object sender, RoutedEventArgs e)
        {
            ManageLibraries_Click(sender, e);
        }

        // AddLibrary_Click() - 重新实现，调用 LibraryService
        private void AddLibrary_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new LibraryDialog();
            if (dialog.ShowDialog() == true)
            {
                var libraryId = _libraryService.AddLibrary(dialog.LibraryName, dialog.LibraryPath);
                if (libraryId > 0)
                {
                    // 创建后自动打开库标签页并高亮
                    var newLibrary = _libraryService.GetLibrary(libraryId);
                    if (newLibrary != null)
                    {
                        OpenLibraryInTab(newLibrary);
                        _libraryService.HighlightLibrary(newLibrary);
                    }
                }
            }
        }

        // ManageLibraries_Click() - 重新实现，调用 LibraryService
        private void ManageLibraries_Click(object sender, RoutedEventArgs e)
        {
            var manageWindow = new LibraryManagementWindow();
            manageWindow.ShowDialog();
            _libraryService.LoadLibraries();
            
            // 如果当前库被删除或修改，刷新显示
            if (_currentLibrary != null)
            {
                var updatedLibrary = _libraryService.GetLibrary(_currentLibrary.Id);
                if (updatedLibrary != null)
                {
                    _currentLibrary = updatedLibrary;
                    _libraryService.LoadLibraryFiles(_currentLibrary,
                        (path) => DatabaseManager.GetFolderSize(path),
                        (bytes) => FormatFileSize(bytes));
                }
                else
                {
                    _currentLibrary = null;
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                        FileBrowser.FilesItemsSource = null;
                }
            }
        }

        // LibrariesListBox_PreviewMouseDown() 和 LibrariesListBox_SelectionChanged() - 保留，用于UI事件处理
        private bool _libraryClickForceNewTab = false;

        private void LibrariesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 检测鼠标中键或Ctrl键，强制打开新标签页
            _libraryClickForceNewTab = (e.ChangedButton == MouseButton.Middle) || 
                                       ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);
        }

        private void LibrariesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[库选择] SelectionChanged 事件触发");
            
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                System.Diagnostics.Debug.WriteLine($"[库选择] 选中的库: {selectedLibrary.Name}, Id: {selectedLibrary.Id}");
                
                // 重新从数据库加载库信息，确保路径信息是最新的
                var updatedLibrary = DatabaseManager.GetLibrary(selectedLibrary.Id);
                if (updatedLibrary != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[库选择] 库路径数量: {updatedLibrary.Paths?.Count ?? 0}");
                    if (updatedLibrary.Paths != null && updatedLibrary.Paths.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[库选择] 路径列表: {string.Join(", ", updatedLibrary.Paths)}");
                    }
                    
                    // 在标签页中打开库（统一标签页系统）
                    OpenLibraryInTab(updatedLibrary, _libraryClickForceNewTab);
                    _libraryClickForceNewTab = false; // 重置标志
                    
                    // 高亮当前选中的库（作为匹配当前库）- 在加载文件后执行，确保库列表已更新
                    _libraryService.HighlightLibrary(updatedLibrary);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[库选择] 从数据库加载库失败");
                    _currentLibrary = null;
                    _config.LastLibraryId = 0;
                    ConfigManager.Save(_config);
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                    if (FileBrowser != null)
                        FileBrowser.AddressText = "";
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[库选择] 未选中库");
                _currentLibrary = null;
                _config.LastLibraryId = 0;
                ConfigManager.Save(_config);
                _currentFiles.Clear();
                if (FileBrowser != null)
                {
                    FileBrowser.FilesItemsSource = null;
                    FileBrowser.AddressText = "";
                }
                
                // 清除所有库的高亮
                if (LibrariesListBox != null && LibrariesListBox.Items != null)
                {
                    foreach (var item in LibrariesListBox.Items)
                    {
                        SetItemHighlight(LibrariesListBox, item, false);
                    }
                }
            }
        }

        // LoadLibraryFiles() 已迁移到 LibraryService

        // ShowMergedLibraryFiles() 已迁移到 LibraryService

        /// <summary>
        /// 启动闲置时文件夹大小计算定时器
        /// </summary>
        private void StartIdleFolderSizeCalculation()
        {
            // 如果定时器已存在且正在运行，不需要重新创建
            if (_idleFolderSizeCalculationTimer != null && _idleFolderSizeCalculationTimer.IsEnabled)
                return;
            
            // 创建或重置定时器
            if (_idleFolderSizeCalculationTimer == null)
            {
                _idleFolderSizeCalculationTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3) // 每3秒检查一次，在CPU闲置时计算
                };
                _idleFolderSizeCalculationTimer.Tick += IdleFolderSizeCalculationTimer_Tick;
            }
            
            // 启动定时器
            _idleFolderSizeCalculationTimer.Start();
        }
        
        /// <summary>
        /// 闲置时文件夹大小计算定时器回调
        /// </summary>
        private void IdleFolderSizeCalculationTimer_Tick(object sender, EventArgs e)
        {
            // 如果正在加载文件或没有待计算的文件夹，停止定时器
            if (_isLoadingFiles)
            {
                return;
            }
            
            string pathToCalculate = null;
            lock (_pendingFolderSizeCalculations)
            {
                if (_pendingFolderSizeCalculations.Count == 0)
                {
                    // 没有待计算的文件夹，停止定时器
                    if (_idleFolderSizeCalculationTimer != null)
                    {
                        _idleFolderSizeCalculationTimer.Stop();
                    }
                    return;
                }
                
                // 取出一个待计算的文件夹
                pathToCalculate = _pendingFolderSizeCalculations.Dequeue();
            }
            
            if (string.IsNullOrEmpty(pathToCalculate))
                return;
            
            // 使用SystemIdle优先级，在CPU闲置时计算
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 检查文件夹是否仍在当前文件列表中
                var item = _currentFiles.FirstOrDefault(f => f.Path == pathToCalculate && f.IsDirectory);
                if (item == null)
                {
                    // 文件夹不在当前列表中，跳过
                    return;
                }
                
                // 如果已经计算过大小，跳过
                if (!string.IsNullOrEmpty(item.Size) && item.Size != "-")
                {
                    return;
                }
                
                // 异步计算文件夹大小
                var cancellationToken = _folderSizeCalculationCancellation.Token;
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        // 尝试获取信号量（非阻塞，如果正在计算其他文件夹则跳过）
                        if (!await _folderSizeCalculationSemaphore.WaitAsync(100, cancellationToken))
                        {
                            // 无法获取信号量，将路径重新加入队列
                            lock (_pendingFolderSizeCalculations)
                            {
                                _pendingFolderSizeCalculations.Enqueue(pathToCalculate);
                            }
                            return;
                        }
                        
                        try
                        {
                            if (cancellationToken.IsCancellationRequested) return;
                            
                            var size = CalculateDirectorySize(pathToCalculate, cancellationToken);
                            if (cancellationToken.IsCancellationRequested) return;
                            
                            // 更新数据库缓存
                            DatabaseManager.SetFolderSize(pathToCalculate, size);
                            
                            // 使用SystemIdle优先级更新UI，避免影响用户操作
                            _ = Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (!cancellationToken.IsCancellationRequested)
                                {
                                    var updatedItem = _currentFiles.FirstOrDefault(f => f.Path == pathToCalculate);
                                    if (updatedItem != null)
                                    {
                                        updatedItem.Size = FormatFileSize(size);
                                        var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                                        collectionView?.Refresh();
                                    }
                                }
                            }), System.Windows.Threading.DispatcherPriority.SystemIdle);
                        }
                        catch (OperationCanceledException) { }
                        catch { }
                        finally
                        {
                            _folderSizeCalculationSemaphore.Release();
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                }, cancellationToken);
            }), System.Windows.Threading.DispatcherPriority.SystemIdle);
        }
        /// <summary>
        /// 立即计算指定文件夹的大小（用户选中时触发）
        /// </summary>
        private void CalculateFolderSizeImmediately(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;
            
            // 先更新UI显示"计算中..."，给用户即时反馈
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var item = _currentFiles.FirstOrDefault(f => f.Path == folderPath);
                if (item != null && (string.IsNullOrEmpty(item.Size) || item.Size == "-" || item.Size == "计算中..."))
                {
                    item.Size = "计算中...";
                    var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                    collectionView?.Refresh();
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
            
            // 从队列中移除该文件夹（如果存在），避免重复计算
            lock (_pendingFolderSizeCalculations)
            {
                var queueList = _pendingFolderSizeCalculations.ToList();
                _pendingFolderSizeCalculations.Clear();
                foreach (var path in queueList)
                {
                    if (path != folderPath)
                    {
                        _pendingFolderSizeCalculations.Enqueue(path);
                    }
                }
            }
            
            // 立即启动计算任务
            var cancellationToken = _folderSizeCalculationCancellation.Token;
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    
                    // 获取信号量（优先计算，但也要等待当前任务完成）
                    await _folderSizeCalculationSemaphore.WaitAsync(cancellationToken);
                    try
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        var size = CalculateDirectorySize(folderPath, cancellationToken);
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        // 更新数据库缓存
                        DatabaseManager.SetFolderSize(folderPath, size);
                        
                        // 使用正常优先级更新UI（用户主动选中，应该及时反馈）
                        _ = Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                var item = _currentFiles.FirstOrDefault(f => f.Path == folderPath);
                                if (item != null)
                                {
                                    item.Size = FormatFileSize(size);
                                    var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                                    collectionView?.Refresh();
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                    finally
                    {
                        _folderSizeCalculationSemaphore.Release();
                    }
                }
                catch (OperationCanceledException) { }
                catch { }
            }, cancellationToken);
        }

        #endregion

        #endregion

        #region 标签功能

        // LoadTags 和 InitializeTagTrainPanel 已迁移到 TagService
        // TagBrowsePanel_TagClicked 和 TagEditPanel_TagClicked 已迁移到 TagService
        
        // 更新TagTrain模型状态
        private void UpdateTagTrainModelStatus()
        {
            // 模型状态现在由TagPanel内部管理，此方法已废弃
            // TagEditPanel会自动更新模型状态
                return;
            }
            
        // 加载TagTrain已有标签列表
        private void LoadTagTrainExistingTags()
        {
            // 标签加载现在由TagPanel内部管理，直接调用TagPanel的方法
            TagEditPanel?.LoadExistingTags();
        }

        
        // 创建浏览模式的标签边框（与TagTrain样式一致）
        private Border CreateBrowseModeTagBorder(OoiMRR.Services.TagInfo tagInfo, int count)
                {
                    var tagName = tagInfo.Name ?? $"标签{tagInfo.Id}";
                    
            // 使用TagTrain的统一颜色样式
                    var border = new Border
                    {
                BorderBrush = System.Windows.Media.Brushes.LightBlue,
                        BorderThickness = new Thickness(1),
                Background = System.Windows.Media.Brushes.AliceBlue,
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 3, 6, 3),
                        Margin = new Thickness(0, 0, 8, 5),
                        Cursor = Cursors.Hand,
                        Tag = tagInfo.Id,
                Focusable = false,
                IsHitTestVisible = true
                    };
                    
            // 添加鼠标悬停效果（与TagTrain一致）
                    border.MouseEnter += (s, e) =>
                    {
                border.Background = System.Windows.Media.Brushes.LightSkyBlue;
                border.BorderBrush = System.Windows.Media.Brushes.DodgerBlue;
            };
            border.MouseLeave += (s, e) =>
            {
                // 高亮当前选中的标签
                        if (_currentTagFilter != null && _currentTagFilter.Id == tagInfo.Id)
                        {
                            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
                            border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
                        }
                        else
                        {
                    border.Background = System.Windows.Media.Brushes.AliceBlue;
                    border.BorderBrush = System.Windows.Media.Brushes.LightBlue;
                        }
                    };
                    
            // 高亮当前选中的标签
                        if (_currentTagFilter != null && _currentTagFilter.Id == tagInfo.Id)
                        {
                            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
                            border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
                        }
            
            border.MouseLeftButtonDown += (s, e) =>
            {
                var tag = new Tag { Id = tagInfo.Id, Name = tagName };
                OpenTagInTab(tag);
            };
            
            // 右键菜单：修改、分配到分组或删除（与TagTrain一致）
            border.ContextMenu = new ContextMenu();
            
            // 修改标签名称
            var editMenuItem = new MenuItem
            {
                Header = "✏️ 修改标签名称",
                Tag = new { TagId = tagInfo.Id, TagName = tagName }
            };
            editMenuItem.Click += (s, e) =>
            {
                _tagService.EditTagName(tagInfo.Id, tagName, this, TagBrowsePanel, TagEditPanel, _tagClickMode);
            };
            
            // 创建"分配到分组"子菜单
            var assignToCategoryMenuItem = new MenuItem
            {
                Header = "📁 分配到分组"
            };
            
            // 获取所有分组和当前标签的分组
            try
            {
                var categories = TagTrain.Services.DataManager.GetAllCategories();
                var currentCategories = TagTrain.Services.DataManager.GetTagCategories(tagInfo.Id);
                
                if (categories.Count > 0)
                {
                    foreach (var category in categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
                    {
                        var categoryMenuItem = new MenuItem
                        {
                            Header = category.Name,
                            Tag = new { TagId = tagInfo.Id, CategoryId = category.Id, TagName = tagName },
                            IsCheckable = true,
                            IsChecked = currentCategories.Contains(category.Id)
                        };
                        
                        categoryMenuItem.Click += (s, e) =>
                        {
                            var menuItem = s as MenuItem;
                            if (menuItem?.Tag != null)
                            {
                                var tagType = menuItem.Tag.GetType();
                                var tagIdProp = tagType.GetProperty("TagId");
                                var categoryIdProp = tagType.GetProperty("CategoryId");
                                
                                if (tagIdProp != null && categoryIdProp != null)
                                {
                                    var tagId = (int)tagIdProp.GetValue(menuItem.Tag);
                                    var categoryId = (int)categoryIdProp.GetValue(menuItem.Tag);
                                    
                                    try
                                    {
                                        if (menuItem.IsChecked)
                                        {
                                            TagTrain.Services.DataManager.AssignTagToCategory(tagId, categoryId);
                                    }
                                    else
                                    {
                                            TagTrain.Services.DataManager.RemoveTagFromCategory(tagId, categoryId);
                                        }
                                        // 刷新标签列表以反映分组变化
                                        TagBrowsePanel?.LoadExistingTags();
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show($"分组操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                        menuItem.IsChecked = !menuItem.IsChecked;
                                    }
                                }
                            }
                            e.Handled = true;
                        };
                        
                        assignToCategoryMenuItem.Items.Add(categoryMenuItem);
                    }
                    
                    assignToCategoryMenuItem.Items.Add(new Separator());
                    var manageCategoryMenuItem = new MenuItem
                    {
                        Header = "管理分组..."
                    };
                    manageCategoryMenuItem.Click += (s, e) =>
                    {
                        OpenCategoryManagement();
                        e.Handled = true;
                    };
                    assignToCategoryMenuItem.Items.Add(manageCategoryMenuItem);
                                }
                                else
                                {
                    var noCategoryMenuItem = new MenuItem
                    {
                        Header = "（暂无分组，点击创建）",
                        IsEnabled = true
                    };
                    noCategoryMenuItem.Click += (s, e) =>
                    {
                        OpenCategoryManagement();
                        e.Handled = true;
                    };
                    assignToCategoryMenuItem.Items.Add(noCategoryMenuItem);
                }
            }
            catch (Exception ex)
            {
                var errorMenuItem = new MenuItem
                {
                    Header = $"加载失败: {ex.Message}",
                    IsEnabled = false
                };
                assignToCategoryMenuItem.Items.Add(errorMenuItem);
            }
            
            // 删除标签
                    var deleteMenuItem = new MenuItem
                    {
                        Header = "🗑️ 删除标签",
                Tag = new { TagId = tagInfo.Id, TagName = tagName }
                    };
                    deleteMenuItem.Click += (s, e) =>
                    {
                        _tagService.DeleteTagById(tagInfo.Id, tagName, TagBrowsePanel, TagEditPanel, _tagClickMode);
                    };
            
            border.ContextMenu.Items.Add(editMenuItem);
            border.ContextMenu.Items.Add(new Separator());
            border.ContextMenu.Items.Add(assignToCategoryMenuItem);
            border.ContextMenu.Items.Add(new Separator());
                    border.ContextMenu.Items.Add(deleteMenuItem);
                    
                    var stackPanel = new StackPanel 
                    { 
                        Orientation = Orientation.Horizontal,
                        IsHitTestVisible = false
                    };
                    
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = tagName,
                        FontWeight = FontWeights.Bold,
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
                    });
                    
                    if (count > 0)
                    {
                        stackPanel.Children.Add(new TextBlock
                        {
                            Text = $"({count})",
                            Foreground = System.Windows.Media.Brushes.DarkGray,
                            FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center
                        });
                    }
                    
                    border.Child = stackPanel;
            return border;
        }
        
        // 标签浏览排序切换
        private void TagBrowseSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tagClickMode == OoiMRR.Services.Tag.TagClickMode.Browse)
            {
                TagBrowsePanel?.LoadExistingTags();
            }
        }

        // 计算希望的标签项宽度（根据设置的每行数量和容器宽度）
        private double GetDesiredTagItemWidth()
        {
            try
            {
                int perRow = TagTrain.Services.SettingsManager.GetTagsPerRow();
                if (perRow <= 0) perRow = 5;
                
                // 标签布局现在由TagPanel内部管理，此方法已废弃
                // 返回默认宽度
                double containerWidth = 300;
                
                double gap = 8; // 与子项右侧 Margin 对齐（每个项都有右间距）
                double totalGap = gap * perRow;
                
                // 预留1px与右侧分割器的安全距离，避免在特殊宽度时换行
                double safe = 1;
                
                // 使用向下取整避免浮点误差导致的溢出换行
                double width = Math.Floor((containerWidth - totalGap - safe) / perRow);
                return Math.Max(120, width);
            }
            catch
            {
                return 150;
            }
        }

        // 根据容器宽度变化刷新每个标签项的宽度
        private void UpdateTagTrainExistingTagsLayout()
        {
            // 标签布局现在由TagPanel内部管理，此方法已废弃
            return;
        }

        // 面板尺寸变化时自适应（已废弃，由TagPanel内部管理）
        private void TagTrainExistingTagsPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 已废弃
            return;
        }

        /// <summary>
        /// 根据标签名称生成颜色（确保相同名称总是生成相同颜色）
        /// </summary>
        private string GenerateTagColor(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return "#FF0000";

            // 使用标签名称的哈希值生成颜色
            int hash = tagName.GetHashCode();
            // 确保哈希值为正数
            if (hash < 0) hash = -hash;
            
            // 生成一个颜色（使用 HSL 色相值）
            int hue = hash % 360;
            // 转换为 RGB（简化版本，使用固定饱和度和亮度）
            var color = HslToRgb(hue, 0.7, 0.5);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// HSL 转 RGB
        /// </summary>
        private System.Drawing.Color HslToRgb(int h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c / 2;

            double r = 0, g = 0, b = 0;
            if (h >= 0 && h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h >= 60 && h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h >= 120 && h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h >= 180 && h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h >= 240 && h < 300)
            {
                r = x; g = 0; b = c;
            }
            else if (h >= 300 && h < 360)
            {
                r = c; g = 0; b = x;
            }

            return System.Drawing.Color.FromArgb(
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255));
        }

        private void UpdateTagFilesUI(Tag tag, List<FileSystemItem> tagFiles)
        {
            try
            {
                // 更新中间文件列表（如果当前在标签模式）
                if (NavTagContent != null && NavTagContent.Visibility == Visibility.Visible)
                {
                    _currentFiles = tagFiles;
                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = null;
                        FileBrowser.FilesItemsSource = _currentFiles;
                    }
                    
                    // 更新地址栏为标签模式（明显的 tag 徽标）
                    if (FileBrowser != null)
                    {
                        FileBrowser.AddressText = "";
                        FileBrowser.IsAddressReadOnly = true;
                        FileBrowser.SetTagBreadcrumb(tag.Name);
                    }
                    
                    // 如果没有文件，显示空状态提示
                    if (tagFiles.Count == 0)
                    {
                        ShowEmptyStateMessage("该标签下没有文件。\n\n提示：只有图片文件（jpg, png, bmp, gif, webp）可以添加标签。");
                    }
                    else
                    {
                        HideEmptyStateMessage();
                    }
                    // 左侧标签列表统一橙色高亮当前标签
                    HighlightActiveTagChip(tag.Id);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTagFilesUI: 发生错误: {ex.Message}");
            }
        }

        private void HighlightActiveTagChip(int tagId)
        {
            try
            {
                // 标签高亮现在由TagPanel内部管理
                if (TagEditPanel?.ExistingTagsPanel == null) return;
                foreach (var child in TagEditPanel.ExistingTagsPanel.Children)
                {
                    if (child is Border border)
                    {
                        bool isMatch = border.Tag is int bid && bid == tagId;
                        if (isMatch)
                        {
                            // 使用统一的选中高亮颜色（与浏览模式一致）
                            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
                            border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
                        }
                        else
                        {
                            // 使用统一的默认颜色（与TagTrain一致）
                            border.Background = System.Windows.Media.Brushes.AliceBlue;
                            border.BorderBrush = System.Windows.Media.Brushes.LightBlue;
                        }
                        if (border.Child is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is TextBlock tb)
                        {
                            if (isMatch)
                            {
                                tb.FontWeight = FontWeights.SemiBold;
                                var fg = this.FindResource("HighlightForegroundBrush") as SolidColorBrush;
                                tb.Foreground = fg ?? System.Windows.Media.Brushes.Black;
                            }
                            else
                            {
                                tb.FontWeight = FontWeights.Bold;
                                tb.Foreground = System.Windows.Media.Brushes.Black;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void FilterByTag(Tag tag)
        {
            _currentTagFilter = tag;
            _tagService.FilterByTag(tag, FormatFileSize);
        }

        private void NewTag_Click(object sender, RoutedEventArgs e)
        {
            _tagService.NewTag(this);
        }

        private void ManageTags_Click(object sender, RoutedEventArgs e)
        {
            _tagService.ManageTags(this);
        }

        private void AddTagToFile_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            _tagService.AddTagToFile(selectedItems, this, () =>
            {
                if (_currentTagFilter != null)
                    _tagService.FilterByTag(_currentTagFilter, FormatFileSize);
                else
                    LoadFiles();
            });
        }

        // OpenTagDialogForSelectedItems 和 GetSharedTagIds 已迁移到 TagService
        // 使用 _tagService.AddTagToFile 代替

        private void TagsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TagsListBox已移除，此方法保留用于兼容性
            // 标签选择现在由TagTrain面板处理，后续可以从TagTrain面板获取选中的标签
            if (_isUpdatingTagSelection)
                return;

            // 如果_currentTagFilter有值，使用它来过滤文件
            if (_currentTagFilter != null)
            {
                FilterByTag(_currentTagFilter);
            }
            else
            {
                // 没有选中标签，清空过滤
                if (_currentLibrary != null)
                {
                    _libraryService.LoadLibraryFiles(_currentLibrary,
                        (path) => DatabaseManager.GetFolderSize(path),
                        (bytes) => FormatFileSize(bytes));
                }
                else if (!string.IsNullOrEmpty(_currentPath))
                {
                    LoadCurrentDirectory();
                }
            }
        }

        #endregion

        #region 标签页面路径浏览功能


        private void UpdateTagPageFilesUI(string path, List<FileSystemItem> files)
        {
            try
            {
                // 更新中间文件列表
                _currentFiles = files;
                if (FileBrowser != null)
                {
                    FileBrowser.FilesItemsSource = null;
                    FileBrowser.FilesItemsSource = _currentFiles;
                }

                // 更新地址栏和面包屑（复用路径页的逻辑）
                if (FileBrowser != null)
                {
            if (FileBrowser != null)
            {
                FileBrowser.AddressText = path;
                FileBrowser.IsAddressReadOnly = false;
                FileBrowser.UpdateBreadcrumb(path);
            }
                }

                // 隐藏空状态提示
                HideEmptyStateMessage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTagPageFilesUI: 发生错误: {ex.Message}");
            }
        }


        #endregion

        #region 搜索功能



        #endregion

        #region 快速访问

        // LoadQuickAccess 和 QuickAccessListBox_SelectionChanged 已迁移到 QuickAccessService
        [Obsolete("Use QuickAccessService.LoadQuickAccess instead")]
        private void LoadQuickAccess()
        {
            _quickAccessService?.LoadQuickAccess(QuickAccessListBox);
        }

        #endregion

        #region 驱动器功能

        // LoadDrives 和 DrivesListBox_SelectionChanged 已迁移到 QuickAccessService
        [Obsolete("Use QuickAccessService.LoadDrives instead")]
        private void LoadDrives()
        {
            _quickAccessService?.LoadDrives(DrivesListBox, FormatFileSize);
        }

        #endregion

        #region 文件操作辅助方法

        /// <summary>
        /// 获取当前文件操作上下文
        /// </summary>
        private IFileOperationContext GetCurrentFileOperationContext()
        {
            var refreshCallback = new Action(() => 
            {
                if (_currentLibrary != null)
                    _libraryService.LoadLibraryFiles(_currentLibrary,
                        (path) => DatabaseManager.GetFolderSize(path),
                        (bytes) => FormatFileSize(bytes));
                else if (_currentTagFilter != null)
                    FilterByTag(_currentTagFilter);
                else
                    LoadCurrentDirectory();
            });

            if (_currentLibrary != null)
            {
                return new LibraryOperationContext(_currentLibrary, FileBrowser, this, refreshCallback);
            }
            else if (_currentTagFilter != null)
            {
                return new TagOperationContext(_currentTagFilter, FileBrowser, this, refreshCallback);
            }
            else
            {
                return new PathOperationContext(_currentPath, FileBrowser, this, refreshCallback);
            }
        }

        #endregion

        #region 菜单事件

        private void NewFolder_Click(object sender, RoutedEventArgs e)
        {
            var context = GetCurrentFileOperationContext();
            var newFolderOperation = new NewFolderOperation(context, this);

            newFolderOperation.Execute();
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            var context = GetCurrentFileOperationContext();
            var placementTarget = sender as UIElement;
            var newFileOperation = new NewFileOperation(context, this, placementTarget);

            newFileOperation.Execute();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            FileBrowser?.FilesList?.SelectAll();
        }

        private void ViewLargeIcons_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现大图标视图
            MessageBox.Show("大图标视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewSmallIcons_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现小图标视图
            MessageBox.Show("小图标视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewList_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现列表视图
            MessageBox.Show("列表视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现详细信息视图
            MessageBox.Show("详细信息视图功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("设置已包含导入/导出配置功能，请使用工具菜单中的相应项。", "设置", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.Import(ofd.FileName);
                    _config = ConfigManager.Load();
                    ApplyConfig(_config);
                    MessageBox.Show("配置已导入并应用。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportConfig_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                FileName = "config.json",
                Filter = "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    SaveCurrentConfig();
                    ConfigManager.Export(sfd.FileName);
                    MessageBox.Show("配置已导出。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EditNotes_Click(object sender, RoutedEventArgs e)
        {
            if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
            {
                RightPanel?.NotesTextBox?.Focus();
            }
            else
            {
                MessageBox.Show("请先选择一个文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("OoiMRR - 文件资源管理器\n版本 1.0\n\n一个功能强大的Windows文件管理工具", 
                "关于", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 新增按钮事件处理

        private void BatchAddTags_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            _tagService.BatchAddTags(selectedItems, this, () =>
            {
                if (_currentTagFilter != null)
                    _tagService.FilterByTag(_currentTagFilter, FormatFileSize);
                else
                    LoadFiles();
            });
        }

        private void TagStatistics_Click(object sender, RoutedEventArgs e)
        {
            _tagService.TagStatistics();
        }

        private void ImportLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导入库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导出库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void AddFileToLibrary_Click(object sender, RoutedEventArgs e)
        {
            // 获取选中的文件或文件夹
            var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要添加到库的文件或文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 确定要添加到的库
            Library targetLibrary = null;
            
            // 如果当前在库模式且选中了库，使用当前库
            if (_currentLibrary != null)
            {
                targetLibrary = _currentLibrary;
            }
            else
            {
                // 让用户选择库
                var libraries = DatabaseManager.GetAllLibraries();
                if (libraries.Count == 0)
                {
                    MessageBox.Show("当前没有可用的库，请先创建一个库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 创建库选择对话框
                var dialog = new Window
                {
                    Title = "选择库",
                    Width = 400,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                var listBox = new ListBox
                {
                    DisplayMemberPath = "Name",
                    Margin = new Thickness(10),
                    ItemsSource = libraries
                };

                var okButton = new Button
                {
                    Content = "确定",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Width = 80,
                    Height = 30,
                    IsCancel = true
                };

                okButton.Click += (s, args) =>
                {
                    if (listBox.SelectedItem is Library selectedLib)
                    {
                        targetLibrary = selectedLib;
                        dialog.DialogResult = true;
                        dialog.Close();
                    }
                    else
                    {
                        MessageBox.Show("请选择一个库", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };

                cancelButton.Click += (s, args) =>
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                };

                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical
                };

                var label = new Label
                {
                    Content = "请选择要添加到的库:",
                    Margin = new Thickness(10, 10, 10, 5)
                };

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(10)
                };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                stackPanel.Children.Add(label);
                stackPanel.Children.Add(listBox);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;

                if (dialog.ShowDialog() != true)
                {
                    return; // 用户取消
                }
            }

            if (targetLibrary == null)
            {
                return;
            }

            // 添加选中的文件/文件夹路径到库
            int successCount = 0;
            int failCount = 0;
            var failedItems = new List<string>();

            foreach (var item in selectedItems)
            {
                try
                {
                    // 对于文件夹，添加文件夹路径
                    // 对于文件，添加文件所在文件夹路径（或直接添加文件路径？）
                    // 根据 Windows 库的行为，应该是添加文件夹路径
                    string pathToAdd = item.IsDirectory ? item.Path : System.IO.Path.GetDirectoryName(item.Path);
                    
                    // 检查路径是否已存在
                    var existingPaths = DatabaseManager.GetLibraryPaths(targetLibrary.Id);
                    if (!existingPaths.Any(p => p.Path.Equals(pathToAdd, StringComparison.OrdinalIgnoreCase)))
                    {
                        DatabaseManager.AddLibraryPath(targetLibrary.Id, pathToAdd);
                        successCount++;
                    }
                    else
                    {
                        // 路径已存在，跳过但不算失败
                        failCount++;
                        failedItems.Add($"{item.Name} (已存在于库中)");
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    failedItems.Add($"{item.Name} ({ex.Message})");
                }
            }

            // 不显示成功提示（减少提示框）
            // 如果有失败项，才显示错误提示
            if (failCount > 0 && successCount == 0)
            {
                var message = $"添加失败:\n{string.Join("\n", failedItems)}";
                MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 如果当前在库模式且是当前库，刷新显示
            if (_currentLibrary != null && _currentLibrary.Id == targetLibrary.Id)
            {
                _libraryService.LoadLibraryFiles(_currentLibrary,
                    (path) => DatabaseManager.GetFolderSize(path),
                    (bytes) => FormatFileSize(bytes));
            }
        }


        private void WindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private bool _isPseudoMaximized = false;
        private Rect _restoreBounds;

        private void WindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            // 使用系统最大化并限制到工作区，确保铺满且不遮挡任务栏
            if (_isPseudoMaximized)
            {
                // 还原到最后一次记录值
                this.WindowState = WindowState.Normal;
                this.Left = _restoreBounds.Left;
                this.Top = _restoreBounds.Top;
                this.Width = _restoreBounds.Width;
                this.Height = _restoreBounds.Height;
                _isPseudoMaximized = false;
                this.ResizeMode = ResizeMode.CanResize;
                
                // 恢复窗口边框
                var hwnd = new WindowInteropHelper(this).Handle;
                var margins = new NativeMethods.MARGINS();
                NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
            }
            else
            {
                // 记录还原尺寸
                _restoreBounds = new Rect(this.Left, this.Top, this.Width, this.Height);
                var wa = GetCurrentMonitorWorkAreaDIPs();
                // 最大化时，使用工作区尺寸，不遮挡任务栏
                this.WindowState = WindowState.Normal;
                this.Left = wa.Left;
                this.Top = wa.Top;
                this.Width = wa.Width;
                this.Height = wa.Height;
                _isPseudoMaximized = true;
                this.ResizeMode = ResizeMode.NoResize;
                
                // 移除窗口边框，将客户区扩展到整个窗口
                var hwnd = new WindowInteropHelper(this).Handle;
                var margins = new NativeMethods.MARGINS { cxLeftWidth = 0, cxRightWidth = 0, cyTopHeight = 0, cyBottomHeight = 0 };
                NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
            }
            UpdateWindowStateUI();
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustListViewColumnWidths();
        }

        private void AdjustListViewColumnWidths()
        {
            if (FileBrowser == null || FileBrowser.FilesList == null) return;

            // 如果正在拖拽分割器，跳过列宽调整，避免重置用户调整的列宽
            if (_isSplitterDragging) return;

            var gridView = FileBrowser.FilesGrid;
            if (gridView == null || gridView.Columns.Count < 5) return;

            // 若有隐藏列，需尊重"可见列"配置：隐藏列保持宽度0，不参与自适应
            var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
            var visible = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            bool showName = visible.Contains("Name");
            bool showSize = visible.Contains("Size");
            bool showType = visible.Contains("Type");
            bool showModified = visible.Contains("ModifiedDate");
            bool showTags = visible.Contains("Tags");

            // 获取可用宽度（减去名称、修改日期、标签列的宽度和边距）
            double availableWidth = FileBrowser.FilesList.ActualWidth - 50; // 减去一些边距和滚动条

            // 名称列固定宽度
            double nameColWidth = showName ? 200 : 0;
            // 修改日期列固定宽度
            double modifiedDateColWidth = showModified ? 150 : 0;
            // 标签列固定宽度
            double tagsColWidth = showTags ? 150 : 0;

            // 计算剩余可用宽度
            double remainingWidth = availableWidth - nameColWidth - modifiedDateColWidth - tagsColWidth;

            // 设置最小宽度
            double minSizeColWidth = 80;
            double minTypeColWidth = 80;

            // 获取当前大小列和类型列的实际宽度，而不是使用硬编码的默认值
            double sizeColWidth = showSize ? (gridView.Columns.Count >= 2 && gridView.Columns[1].ActualWidth > 0 ? gridView.Columns[1].ActualWidth : 100) : 0;
            double typeColWidth = showType ? (gridView.Columns.Count >= 3 && gridView.Columns[2].ActualWidth > 0 ? gridView.Columns[2].ActualWidth : 100) : 0;

            if (remainingWidth < sizeColWidth + typeColWidth && showSize && showType)
            {
                // 空间不足，需要压缩
                if (remainingWidth >= minSizeColWidth + minTypeColWidth)
                {
                    // 可以容纳最小宽度，先压缩类型列
                    double minTotal = minSizeColWidth + minTypeColWidth;
                    double extraWidth = remainingWidth - minTotal;
                    
                    // 先压缩类型列
                    double typeShrink = Math.Max(0, typeColWidth - minTypeColWidth);
                    double typeCanShrink = Math.Min(typeShrink, extraWidth);
                    typeColWidth -= typeCanShrink;
                    
                    // 如果还有空间，给大小列
                    if (typeCanShrink < extraWidth)
                    {
                        sizeColWidth = minSizeColWidth + (extraWidth - typeCanShrink);
                    }
                    else
                    {
                        sizeColWidth = minSizeColWidth;
                    }
                }
                else
                {
                    // 空间不足，都设置为最小宽度
                    sizeColWidth = minSizeColWidth;
                    typeColWidth = minTypeColWidth;
                }
            }

            // 按列Tag应用，隐藏列保持0
            // 索引与Tag对应：0-Name,1-Size,2-Type,3-ModifiedDate,4-CreatedTime,5-Tags,6-Notes
            if (gridView.Columns.Count >= 1) gridView.Columns[0].Width = nameColWidth;           // Name
            if (gridView.Columns.Count >= 2) gridView.Columns[1].Width = sizeColWidth;           // Size
            if (gridView.Columns.Count >= 3) gridView.Columns[2].Width = typeColWidth;           // Type
            if (gridView.Columns.Count >= 4) gridView.Columns[3].Width = modifiedDateColWidth;   // ModifiedDate
            // 不调整 CreatedTime（[4]），由用户控制；若隐藏则保持0
            if (gridView.Columns.Count >= 6) gridView.Columns[5].Width = tagsColWidth;           // Tags
        }
        private void AdjustColumnWidths()
        {
            // 获取可用总宽度（去掉两个垂直分割器）
            if (RootGrid == null) return;
            double total = RootGrid.ActualWidth - 12; // 两个6像素分割器

            // 当前实际宽度
            // 列1和列2使用固定宽度值（如果已设置），否则使用ActualWidth
            // 列3始终使用固定宽度360
            double left = ColLeft.Width.IsAbsolute ? ColLeft.Width.Value : ColLeft.ActualWidth;
            double center = ColCenter.Width.IsAbsolute ? ColCenter.Width.Value : ColCenter.ActualWidth;
            double right = ColRight.Width.IsAbsolute ? ColRight.Width.Value : ColRight.ActualWidth; // 列3使用实际宽度

            double minLeft = ColLeft.MinWidth;
            double minCenter = ColCenter.MinWidth;
            double minRight = ColRight.MinWidth;

            double sum = left + center + right;
            
            // 确保窗口最小宽度不小于三列MinWidth总和
            var minTotalWidth = minLeft + minCenter + minRight + 12;
            if (this.MinWidth < minTotalWidth)
            {
                this.MinWidth = minTotalWidth;
            }
            
            // 如果总宽度小于最小宽度总和，强制设置窗口宽度
            if (total < minTotalWidth)
            {
                // 按比例分配最小宽度，但列3优先保持最小宽度
                double scale = total / minTotalWidth;
                left = minLeft * scale;
                center = minCenter * scale;
                
                // 如果还有空间不够，继续压缩列2和列1
                double needed = left + center + minRight;
                if (needed > total)
                {
                    double shortage = needed - total;
                    double canShrinkCenter = center - minCenter;
                    if (canShrinkCenter >= shortage)
                    {
                        center -= shortage;
                    }
                    else
                    {
                        double remaining = shortage - canShrinkCenter;
                        center = minCenter;
                        left = Math.Max(minLeft, left - remaining);
                    }
                }
                
                // 应用像素宽度，确保不低于最小宽度
                ColLeft.Width = new GridLength(Math.Max(minLeft, left));
                ColCenter.Width = new GridLength(Math.Max(minCenter, center));
                // 列3始终使用Star模式，确保顶到窗口右边缘
                ColRight.Width = new GridLength(1, GridUnitType.Star);
                return;
            }
            else if (total < sum)
            {
                // 需要压缩
                double shortage = sum - total;

                // 先从列2收缩
                    double canShrinkCenter = Math.Max(0, center - minCenter);
                    double shrinkCenter = Math.Min(canShrinkCenter, shortage);
                    center -= shrinkCenter;
                    shortage -= shrinkCenter;

                // 然后列1（保持固定宽度，只在必要时压缩）
                if (shortage > 0)
                {
                    double canShrinkLeft = Math.Max(0, left - minLeft);
                    double shrinkLeft = Math.Min(canShrinkLeft, shortage);
                    left -= shrinkLeft;
                    shortage -= shrinkLeft;
                    // 更新后保存列1宽度，确保后续使用固定值
                    if (left > minLeft)
                    {
                        // 可以在这里保存列1的固定宽度
                    }
                }

                // 应用像素宽度，确保不低于最小宽度
                ColLeft.Width = new GridLength(Math.Max(minLeft, left));
                ColCenter.Width = new GridLength(Math.Max(minCenter, center));
                // 列3始终使用Star模式，确保顶到窗口右边缘
                ColRight.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // 宽度足够，不需要压缩
                // 压缩顺序：列3 -> 列2 -> 列1
                // 确保所有列都使用固定像素宽度（不使用Star），确保MinWidth生效
                
                // 确保列1（左侧列）保持固定宽度，不随窗口变化
                // 优先使用Width.Value（如果已设置固定值），否则使用ActualWidth，但不小于最小宽度
                double leftWidth;
                if (ColLeft.Width.IsAbsolute && ColLeft.Width.Value > 0)
                {
                    // 如果已有固定宽度值，使用该值（但不小于最小宽度）
                    leftWidth = Math.Max(minLeft, ColLeft.Width.Value);
                }
                else
                {
                    // 否则使用实际宽度，但不小于最小宽度
                    leftWidth = Math.Max(minLeft, ColLeft.ActualWidth > 0 ? ColLeft.ActualWidth : minLeft);
                }
                
                // 确保列2（中间列）保持固定宽度，不随窗口变化
                // 如果列2当前是Star模式，使用ActualWidth；否则使用当前计算值，但不小于最小宽度
                double centerWidth;
                if (ColCenter.Width.IsStar)
                {
                    // 如果是Star模式，使用实际宽度，但不小于最小宽度
                    centerWidth = Math.Max(minCenter, ColCenter.ActualWidth > 0 ? ColCenter.ActualWidth : minCenter);
                }
                else
                {
                    // 如果不是Star模式，使用当前宽度，但不小于最小宽度
                    centerWidth = Math.Max(minCenter, center > 0 ? center : minCenter);
                }
                
                // 列1和列2使用固定像素宽度，列3使用Star模式自动填充剩余空间
                ColLeft.Width = new GridLength(leftWidth);
                ColCenter.Width = new GridLength(centerWidth);
                ColRight.Width = new GridLength(1, GridUnitType.Star);
            }
        }
        private void ForceColumnWidthsToFixed(bool skipUpdateLayout = false)
        {
            // 强制将列2和列3设置为固定宽度（不使用Star模式），确保MinWidth生效
            if (RootGrid == null || ColCenter == null || ColRight == null) return;
            
            double centerActual = ColCenter.ActualWidth;
            double rightActual = ColRight.ActualWidth;
            double minCenter = ColCenter.MinWidth;
            double minRight = ColRight.MinWidth;
            
            Debug.WriteLine($"[ForceColumnWidthsToFixed] 开始检查 (skipUpdateLayout={skipUpdateLayout}, _isSplitterDragging={_isSplitterDragging})");
            Debug.WriteLine($"[ForceColumnWidthsToFixed] ColCenter: IsStar={ColCenter.Width.IsStar}, ActualWidth={centerActual}, MinWidth={minCenter}, Width.Value={ColCenter.Width.Value}");
            Debug.WriteLine($"[ForceColumnWidthsToFixed] ColRight: IsStar={ColRight.Width.IsStar}, ActualWidth={rightActual}, MinWidth={minRight}, Width.Value={ColRight.Width.Value}");
            
            bool needFix = false;
            
            // 检查列2：如果是Star模式，或者宽度小于最小宽度，强制改为固定宽度
            if (ColCenter.Width.IsStar || (centerActual > 0 && centerActual < minCenter))
            {
                double newCenterWidth = Math.Max(minCenter, centerActual > 0 ? centerActual : minCenter);
                ColCenter.Width = new GridLength(newCenterWidth);
                needFix = true;
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 修复列2: IsStar={ColCenter.Width.IsStar}, 实际宽度={centerActual}, 设置为 {newCenterWidth}");
            }
            else if (!ColCenter.Width.IsStar && ColCenter.Width.Value < minCenter)
            {
                ColCenter.Width = new GridLength(minCenter);
                needFix = true;
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 修复列2宽度值: {ColCenter.Width.Value} < {minCenter}, 设置为 {minCenter}");
            }
            
            // 检查列3：如果宽度小于最小宽度，修复它；否则保持Star模式或当前宽度
            // 列3应该使用剩余空间（Star模式）
            if (ColRight.Width.IsStar)
            {
                // Star模式是好的，让AdjustColumnWidths来处理
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 列3是Star模式，这是正常的");
            }
            else if (rightActual < minRight)
            {
                // 如果列3宽度小于最小宽度，改为Star模式让它使用剩余空间
                ColRight.Width = new GridLength(1, GridUnitType.Star);
                needFix = true;
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 列3宽度小于最小宽度，改为Star模式使用剩余空间");
            }
            else if (!ColRight.Width.IsStar)
            {
                // 如果列3不是Star模式，改为Star模式让它使用剩余空间
                ColRight.Width = new GridLength(1, GridUnitType.Star);
                needFix = true;
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 列3不是Star模式，改为Star模式使用剩余空间");
            }
            
            // 如果修复了列宽，触发布局更新（但在拖拽期间跳过，避免影响DataGrid列宽）
            if (needFix && !skipUpdateLayout && !_isSplitterDragging)
            {
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 触发布局更新");
                this.UpdateLayout();
            }
            else if (needFix)
            {
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 跳过UpdateLayout (skipUpdateLayout={skipUpdateLayout}, _isSplitterDragging={_isSplitterDragging})");
            }
            else
            {
                Debug.WriteLine($"[ForceColumnWidthsToFixed] 无需修复");
            }
        }

        private void EnsureColumnMinWidths()
        {
            // 强制检查并应用所有列的最小宽度约束
            if (RootGrid == null) return;
            
            // 先强制设置为固定宽度（不使用Star模式）
            ForceColumnWidthsToFixed();
            
            // 获取当前实际宽度
            double leftActual = ColLeft.ActualWidth;
            double centerActual = ColCenter.ActualWidth;
            double rightActual = ColRight.ActualWidth;
            
            double minLeft = ColLeft.MinWidth;
            double minCenter = ColCenter.MinWidth;
            double minRight = ColRight.MinWidth;
            
            bool needAdjust = false;
            
            // 检查列2（中间列）是否小于最小宽度
            if (centerActual < minCenter)
            {
                ColCenter.Width = new GridLength(minCenter);
                needAdjust = true;
            }
            
            // 检查列3（右侧面板）是否小于最小宽度
            if (rightActual < minRight)
            {
                // 计算可用空间
                double totalWidth = RootGrid.ActualWidth - 12; // 减去两个分割器宽度
                double availableWidth = totalWidth - minLeft - (centerActual >= minCenter ? centerActual : minCenter);
                
                // 确保右侧面板至少达到最小宽度
                if (availableWidth >= minRight)
                {
                    ColRight.Width = new GridLength(minRight);
                    needAdjust = true;
                }
                else
                {
                    // 空间不足，需要重新分配
                    AdjustColumnWidths();
                    return;
                }
            }
            
            // 检查列1（左侧列）
            if (leftActual < minLeft)
            {
                ColLeft.Width = new GridLength(minLeft);
                needAdjust = true;
            }
            
            // 如果需要调整，触发布局更新
            if (needAdjust)
            {
                this.UpdateLayout();
            }
        }

        private void UpdateWindowStateUI()
        {
            bool isMax = _isPseudoMaximized;

            // 更新主窗口右上角按钮图标
            if (TitleBarMaxRestoreButton != null)
            {
                // Segoe MDL2 Assets: Maximize E922, Restore E923
                TitleBarMaxRestoreButton.Content = isMax ? "\uE923" : "\uE922";
                TitleBarMaxRestoreButton.ToolTip = isMax ? "还原" : "最大化";
            }
        }

        private Rect GetCurrentMonitorWorkAreaDIPs()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            var mi = new NativeMethods.MONITORINFO();
            mi.cbSize = Marshal.SizeOf(mi);
            if (NativeMethods.GetMonitorInfo(monitor, ref mi))
            {
                // 使用WPF提供的从设备像素到DIPs的转换，避免缩放误差
                var source = HwndSource.FromHwnd(hwnd);
                var m = source?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
                // 使用rcWork以排除任务栏区域
                var tl = m.Transform(new System.Windows.Point(mi.rcWork.Left, mi.rcWork.Top));
                var br = m.Transform(new System.Windows.Point(mi.rcWork.Right, mi.rcWork.Bottom));
                return new Rect(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
            }
            // 回退到工作区尺寸
            var wa = SystemParameters.WorkArea;
            return new Rect(wa.Left, wa.Top, wa.Width, wa.Height);
        }

        internal static class NativeMethods
        {
            public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
            public const int SWP_NOSIZE = 0x0001;
            public const int SWP_NOMOVE = 0x0002;
            public const int SWP_NOZORDER = 0x0004;
            public const int SWP_FRAMECHANGED = 0x0020;

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

            [DllImport("user32.dll")]
            public static extern int GetSystemMetrics(int nIndex);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            [DllImport("dwmapi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

            [StructLayout(LayoutKind.Sequential)]
            internal struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            internal struct MONITORINFO
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public int dwFlags;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct MARGINS
            {
                public int cxLeftWidth;
                public int cxRightWidth;
                public int cyTopHeight;
                public int cyBottomHeight;
            }
        }
        private void UpdateActionButtonsPosition()
        {
            // 更新列2操作按钮位置，使其在标题栏中居中对齐列2区域
            if (ActionButtonsGrid == null || ColLeft == null || ColCenter == null || RootGrid == null) return;
            
            try
            {
                // 计算列2在RootGrid中的起始位置（列1宽度 + 分割器宽度）
                double col2Start = ColLeft.ActualWidth + 6;
                // 计算列2的中心位置
                double col2Center = col2Start + ColCenter.ActualWidth / 2;
                // 计算ActionButtonsGrid的宽度（测量实际宽度）
                ActionButtonsGrid.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double buttonsWidth = ActionButtonsGrid.DesiredSize.Width;
                if (buttonsWidth == 0) buttonsWidth = 400; // 如果尚未测量，使用估算值
                
                // 设置Margin，使按钮中心对齐列2中心
                // 标题栏Grid的Column 1是*，从列1按钮结束后开始，所以需要减去列1按钮区域
                double leftMargin = col2Center - buttonsWidth / 2 - ColLeft.ActualWidth - 16; // 16是列1按钮的左右Margin
                // 限制Margin范围，确保按钮不超出可见区域
                double maxMargin = RootGrid.ActualWidth - buttonsWidth - 102 - ColLeft.ActualWidth - 16; // 102是右上角按钮宽度
                leftMargin = Math.Max(8, Math.Min(leftMargin, maxMargin));
                
                ActionButtonsGrid.Margin = new Thickness(leftMargin, 0, 0, 0);
                ActionButtonsGrid.HorizontalAlignment = HorizontalAlignment.Left;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateActionButtonsPosition error: {ex.Message}");
            }
        }
        
        private void UpdateSeparatorPosition()
        {
            // 更新分隔符位置，使其与列1和列2之间的分割器对齐
            if (TitleBarSeparator == null || ColLeft == null) return;
            
            try
            {
                // 获取包含分隔符的StackPanel
                var stackPanel = TitleBarSeparator.Parent as StackPanel;
                if (stackPanel == null) return;
                
                // 计算导航按钮的总宽度
                double navButtonsWidth = 0;
                foreach (var child in stackPanel.Children)
                {
                    if (child == TitleBarSeparator) break; // 遇到分隔符就停止
                    if (child is FrameworkElement fe)
                    {
                        // 使用ActualWidth，如果为0则使用DesiredSize
                        double width = fe.ActualWidth;
                        if (width <= 0)
                        {
                            fe.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                            width = fe.DesiredSize.Width;
                        }
                        navButtonsWidth += width;
                    }
                }
                
                // 计算分隔符应该的左边距
                // 目标：分隔符右边缘 = ColLeft右边缘
                // 分隔符右边缘 = StackPanel左边距 + 导航按钮宽度 + 分隔符左边距 + 分隔符宽度
                // 所以：分隔符左边距 = ColLeft宽度 - StackPanel左边距 - 导航按钮宽度 - 分隔符宽度
                
                double stackPanelLeftMargin = 8; // StackPanel的Margin="8,0"
                
                // 测量分隔符宽度
                TitleBarSeparator.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double separatorWidth = TitleBarSeparator.ActualWidth > 0 ? TitleBarSeparator.ActualWidth : TitleBarSeparator.DesiredSize.Width;
                if (separatorWidth <= 0) separatorWidth = 1; // 默认宽度
                
                double targetSeparatorLeftMargin = ColLeft.ActualWidth - stackPanelLeftMargin - navButtonsWidth - separatorWidth;
                
                // 确保左边距不为负，最小值为0
                targetSeparatorLeftMargin = Math.Max(0, targetSeparatorLeftMargin);
                
                TitleBarSeparator.Margin = new Thickness(targetSeparatorLeftMargin, 0, 0, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSeparatorPosition error: {ex.Message}");
            }
        }

        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    // 双击切换最大化/还原
                    WindowMaximize_Click(sender, e);
                }
                else if (e.ClickCount == 1)
                {
                    // 单击拖拽窗口
                    this.DragMove();
                }
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        #endregion

        #region 收藏功能

        // 收藏功能已迁移到 FavoriteService
        // 所有收藏相关的逻辑（加载、拖拽、选择等）都在 FavoriteService 中处理

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

        private void AddFavorite_Click(object sender, RoutedEventArgs e)
        {
            // 获取选中的文件或文件夹
            var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            _favoriteService.AddFavorite(selectedItems);
        }
        private void LoadColumnWidths()
        {
            if (FileBrowser?.FilesGrid == null || _config == null) return;
            
            try
            {
                var columns = FileBrowser.FilesGrid.Columns;
                if (columns.Count >= 7)
                {
                    // 创建列名到列的映射
                    var columnMap = new Dictionary<string, GridViewColumn>
                    {
                        { "Name", columns[0] },
                        { "Size", columns[1] },
                        { "Type", columns[2] },
                        { "ModifiedDate", columns[3] },
                        { "CreatedTime", columns[4] },
                        { "Tags", columns[5] },
                        { "Notes", columns[6] }
                    };
                    
                    // 加载保存的列顺序
                    if (!string.IsNullOrEmpty(_config.ColumnOrder))
                    {
                        var savedOrder = _config.ColumnOrder.Split(',');
                        var newColumns = new List<GridViewColumn>();
                        
                        foreach (var colName in savedOrder)
                        {
                            var trimmedName = colName.Trim();
                            if (columnMap.ContainsKey(trimmedName))
                            {
                                newColumns.Add(columnMap[trimmedName]);
                            }
                        }
                        
                        // 添加未在顺序中的列（向后兼容）
                        foreach (var kvp in columnMap)
                        {
                            if (!savedOrder.Any(s => s.Trim() == kvp.Key))
                            {
                                newColumns.Add(kvp.Value);
                            }
                        }
                        
                        // 重新排序列
                        if (newColumns.Count == columns.Count)
                        {
                            FileBrowser.FilesGrid.Columns.Clear();
                            foreach (var col in newColumns)
                            {
                                FileBrowser.FilesGrid.Columns.Add(col);
                            }
                        }
                    }
                    
                    // 加载列宽度并结合“可见列”配置（隐藏列保持宽度为0）
                    var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
                    var visibleSet = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                    foreach (var column in FileBrowser.FilesGrid.Columns)
                    {
                        var header = column.Header as GridViewColumnHeader;
                        if (header?.Tag != null)
                        {
                            var tag = header.Tag.ToString();
                            bool shouldShow = visibleSet.Contains(tag);
                            if (!shouldShow)
                            {
                                column.Width = 0; // 隐藏
                                continue;
                            }
                            
                            double width = tag switch
                            {
                                "Name" => _config.ColNameWidth,
                                "Size" => _config.ColSizeWidth,
                                "Type" => _config.ColTypeWidth,
                                "ModifiedDate" => _config.ColModifiedDateWidth,
                                "CreatedTime" => _config.ColCreatedTimeWidth,
                                "Tags" => _config.ColTagsWidth,
                                "Notes" => _config.ColNotesWidth,
                                _ => 0
                            };
                            if (width > 0) column.Width = width;
                        }
                    }
                }
            }
            catch { }
        }
        
        private void SaveColumnWidths()
        {
            if (FileBrowser?.FilesGrid == null || _config == null) return;
            
            try
            {
                var columns = FileBrowser.FilesGrid.Columns;
                if (columns.Count >= 7)
                {
                    // 保存列顺序
                    var columnOrder = new List<string>();
                    foreach (var column in columns)
                    {
                        var header = column.Header as GridViewColumnHeader;
                        if (header?.Tag != null)
                        {
                            columnOrder.Add(header.Tag.ToString());
                        }
                    }
                    _config.ColumnOrder = string.Join(",", columnOrder);
                    
                    // 保存列宽度（按Tag保存）
                    foreach (var column in columns)
                    {
                        var header = column.Header as GridViewColumnHeader;
                        if (header?.Tag != null)
                        {
                            var tag = header.Tag.ToString();
                            var width = column.ActualWidth;
                            
                            // 如果列被隐藏（宽度=0），不要覆盖之前保存的宽度
                            if (width <= 0) continue;
                            
                            switch (tag)
                            {
                                case "Name":
                                    _config.ColNameWidth = width;
                                    break;
                                case "Size":
                                    _config.ColSizeWidth = width;
                                    break;
                                case "Type":
                                    _config.ColTypeWidth = width;
                                    break;
                                case "ModifiedDate":
                                    _config.ColModifiedDateWidth = width;
                                    break;
                                case "CreatedTime":
                                    _config.ColCreatedTimeWidth = width;
                                    break;
                                case "Tags":
                                    _config.ColTagsWidth = width;
                                    break;
                                case "Notes":
                                    _config.ColNotesWidth = width;
                                    break;
                            }
                        }
                    }
                    
                    ConfigManager.Save(_config);
                }
            }
            catch { }
        }
        
        private void FilesListView_PreviewMouseDoubleClickForBlank(object sender, MouseButtonEventArgs e)
        {
            // 1) 优先判断列头分隔条（Thumb）双击：自动适配列宽
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null) return;
            var thumbAncestor = FindAncestor<System.Windows.Controls.Primitives.Thumb>(originalSource);
            if (thumbAncestor != null)
            {
                var thumbHeader = FindAncestor<GridViewColumnHeader>(originalSource);
                if (thumbHeader?.Column != null)
                {
                    AutoSizeGridViewColumn(thumbHeader.Column);
                    e.Handled = true;
                    return;
                }
            }
            
            // 2) 列头本体双击：不做“返回上一级”
            var header = FindAncestor<GridViewColumnHeader>(originalSource);
            if (header != null)
            {
                e.Handled = true;
                return;
            }
            
            // 2) 若点击在列表项上，也不做“返回上一级”
            var listViewItem = FindAncestor<ListViewItem>(originalSource);
            if (listViewItem != null) return;
            
            var hitResult = VisualTreeHelper.HitTest(FileBrowser.FilesList, e.GetPosition(FileBrowser.FilesList));
            if (hitResult != null && FindAncestor<ListViewItem>(hitResult.VisualHit) != null)
                return;
            
            // 3) 真正的空白区域双击：返回上一级
            if (!string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath))
            {
                var parentPath = Directory.GetParent(_currentPath);
                if (parentPath != null)
                {
                    NavigateToPath(parentPath.FullName);
                    e.Handled = true;
                }
            }
        }

        // 根据内容自动调整列宽（用于双击列分隔条）
        private void AutoSizeGridViewColumn(GridViewColumn column)
        {
            if (FileBrowser?.FilesList == null || column == null) return;
            
            // 若该列在当前模式被设置为隐藏，禁止在双击时把它显示出来
            var headerForTag = column.Header as GridViewColumnHeader;
            var tagName = headerForTag?.Tag?.ToString();
            if (!string.IsNullOrEmpty(tagName))
            {
                var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
                var visibleSet = new HashSet<string>(
                    visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.OrdinalIgnoreCase);
                if (!visibleSet.Contains(tagName))
                {
                    // 保持隐藏状态，直接返回
                    return;
                }
            }
            
            double padding = 24; // 预留左右内边距和排序箭头空间
            double maxWidth = 0;
            
            // 列头文本宽度
            var header = column.Header as GridViewColumnHeader;
            var headerText = header?.Content?.ToString() ?? "";
            maxWidth = Math.Max(maxWidth, MeasureTextWidth(headerText) + padding);
            
            // 各行文本宽度
            foreach (var item in FileBrowser.FilesList.Items)
            {
                string cellText = GetCellTextForColumn(item, column, header);
                maxWidth = Math.Max(maxWidth, MeasureTextWidth(cellText) + padding);
            }
            
            // 最小宽度保护
            if (maxWidth < 50) maxWidth = 50;
            column.Width = Math.Ceiling(maxWidth);
        }

        private string GetCellTextForColumn(object item, GridViewColumn column, GridViewColumnHeader header)
        {
            if (item == null) return "";
            
            // 优先使用 DisplayMemberBinding
            if (column.DisplayMemberBinding is System.Windows.Data.Binding binding && binding.Path != null)
            {
                var prop = item.GetType().GetProperty(binding.Path.Path);
                var val = prop?.GetValue(item);
                return val?.ToString() ?? "";
            }
            
            // 退化：使用列头 Tag 作为属性名尝试
            var propName = header?.Tag?.ToString();
            if (!string.IsNullOrEmpty(propName))
            {
                var prop2 = item.GetType().GetProperty(propName);
                var val2 = prop2?.GetValue(item);
                if (val2 != null) return val2.ToString();
            }
            
            return "";
        }

        private double MeasureTextWidth(string text)
        {
            var tb = new TextBlock
            {
                Text = text ?? "",
                FontSize = FileBrowser?.FilesList?.FontSize ?? 12,
                FontFamily = FileBrowser?.FilesList?.FontFamily
            };
            tb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            return tb.DesiredSize.Width;
        }

        // 右键列头 -> 列显示设置
        private void EnsureHeaderContextMenuHook()
        {
            if (FileBrowser?.FilesList == null) return;
            FileBrowser.FilesList.PreviewMouseRightButtonUp -= FilesList_PreviewMouseRightButtonUp_HeaderMenu;
            FileBrowser.FilesList.PreviewMouseRightButtonUp += FilesList_PreviewMouseRightButtonUp_HeaderMenu;
        }

        private void FilesList_PreviewMouseRightButtonUp_HeaderMenu(object sender, MouseButtonEventArgs e)
        {
            var src = e.OriginalSource as DependencyObject;
            if (src == null) return;
            
            // 检查是否点击在列头上
            var header = FindAncestor<GridViewColumnHeader>(src);
            if (header != null)
            {
                // 在列头上右键，显示列选择菜单（弹出选项菜单，与列3一致）
                e.Handled = true;
                
                // 创建弹出菜单
                var cm = new ContextMenu();
                var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
                var visibleSet = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                
                if (FileBrowser?.FilesGrid != null)
                {
                    foreach (var column in FileBrowser.FilesGrid.Columns)
                    {
                        var colHeader = column.Header as GridViewColumnHeader;
                        var tag = colHeader?.Tag?.ToString();
                        if (string.IsNullOrEmpty(tag)) continue;
                        
                        string title = colHeader?.Content?.ToString() ?? tag;
                        bool isVisible = visibleSet.Contains(tag);
                        
                        var mi = new MenuItem
                        {
                            Header = $"列: {title}",
                            IsCheckable = true,
                            IsChecked = isVisible
                        };
                        
                        mi.Checked += (s, ev) =>
                        {
                            // 显示列
                            if (column.Width <= 1)
                            {
                                double w = tag switch
                                {
                                    "Name" => _config.ColNameWidth,
                                    "Size" => _config.ColSizeWidth,
                                    "Type" => _config.ColTypeWidth,
                                    "ModifiedDate" => _config.ColModifiedDateWidth,
                                    "CreatedTime" => _config.ColCreatedTimeWidth,
                                    "Tags" => _config.ColTagsWidth,
                                    "Notes" => _config.ColNotesWidth,
                                    _ => column.ActualWidth > 0 ? column.ActualWidth : 100
                                };
                                column.Width = Math.Max(40, w);
                            }
                            // 更新配置
                            var currentVisible = GetVisibleColumnsForCurrentMode() ?? "";
                            var currentSet = new HashSet<string>(currentVisible.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                            currentSet.Add(tag);
                            SetVisibleColumnsForCurrentMode(string.Join(",", currentSet));
                            ConfigManager.Save(_config);
                        };
                        
                        mi.Unchecked += (s, ev) =>
                        {
                            // 隐藏列
                            column.Width = 0;
                            // 更新配置
                            var currentVisible = GetVisibleColumnsForCurrentMode() ?? "";
                            var currentSet = new HashSet<string>(currentVisible.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                            currentSet.Remove(tag);
                            SetVisibleColumnsForCurrentMode(string.Join(",", currentSet));
                            ConfigManager.Save(_config);
                        };
                        
                        cm.Items.Add(mi);
                    }
                }
                
                // 将菜单附加到列头元素上并显示
                cm.PlacementTarget = header;
                cm.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                cm.IsOpen = true;
                return;
            }
            
            // 不在列头上，不处理，让文件列表的右键菜单显示
            // 不设置 e.Handled，让事件继续传播到文件列表的 ContextMenu
        }

        private string GetCurrentModeKey()
        {
            return _config?.LastNavigationMode ?? "Path";
        }

        private string GetVisibleColumnsForCurrentMode()
        {
            var key = GetCurrentModeKey();
            return key switch
            {
                "Library" => _config.VisibleColumns_Library,
                "Tag" => _config.VisibleColumns_Tag,
                _ => _config.VisibleColumns_Path
            };
        }

        private void SetVisibleColumnsForCurrentMode(string csv)
        {
            var key = GetCurrentModeKey();
            switch (key)
            {
                case "Library":
                    _config.VisibleColumns_Library = csv;
                    break;
                case "Tag":
                    _config.VisibleColumns_Tag = csv;
                    break;
                default:
                    _config.VisibleColumns_Path = csv;
                    break;
            }
        }

        private void ApplyVisibleColumnsForCurrentMode()
        {
            if (FileBrowser?.FilesGrid == null) return;
            var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
            var set = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            foreach (var column in FileBrowser.FilesGrid.Columns)
            {
                var header = column.Header as GridViewColumnHeader;
                var tag = header?.Tag?.ToString();
                if (string.IsNullOrEmpty(tag)) continue;
                bool shouldShow = set.Contains(tag);
                if (shouldShow)
                {
                    // 恢复保存的宽度
                    double w = tag switch
                    {
                        "Name" => _config.ColNameWidth,
                        "Size" => _config.ColSizeWidth,
                        "Type" => _config.ColTypeWidth,
                        "ModifiedDate" => _config.ColModifiedDateWidth,
                        "CreatedTime" => _config.ColCreatedTimeWidth,
                        "Tags" => _config.ColTagsWidth,
                        "Notes" => _config.ColNotesWidth,
                        _ => column.ActualWidth > 0 ? column.ActualWidth : 100
                    };
                    column.Width = Math.Max(40, w);
                }
                else
                {
                    // 折叠
                    column.Width = 0;
                }
            }
            HookHeaderThumbs();
        }

        // 绑定列头分隔线双击
        private void HookHeaderThumbs()
        {
            if (FileBrowser?.FilesGrid == null) return;
            foreach (var column in FileBrowser.FilesGrid.Columns)
            {
                if (column.Header is GridViewColumnHeader header)
                {
                    header.PreviewMouseLeftButtonDown -= Header_PreviewMouseLeftButtonDown_ForThumb;
                    header.PreviewMouseLeftButtonDown += Header_PreviewMouseLeftButtonDown_ForThumb;
                    
                    // 确保模板应用后再挂载Thumb事件
                    header.Loaded -= Header_Loaded_AttachThumb;
                    header.Loaded += Header_Loaded_AttachThumb;
                }
            }
        }

        private void Header_PreviewMouseLeftButtonDown_ForThumb(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (sender is GridViewColumnHeader header)
            {
                // Header 级别也拦截（无论Thumb是否已处理）
                if (header.Column != null)
                {
                    AutoSizeGridViewColumn(header.Column);
                    e.Handled = true;
                }
            }
        }

        // 使用 AddHandler 捕获的分隔线双击
        private void FilesList_HeaderThumbDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            var src = e.OriginalSource as DependencyObject;
            if (src == null) return;
            var header = FindAncestor<GridViewColumnHeader>(src);
            var thumb = FindAncestor<System.Windows.Controls.Primitives.Thumb>(src);
            if (header != null && thumb != null && header.Column != null)
            {
                AutoSizeGridViewColumn(header.Column);
                e.Handled = true;
            }
        }

        private void Header_Loaded_AttachThumb(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader header)
            {
                var thumb = FindHeaderThumb(header);
                if (thumb != null)
                {
                    thumb.DragStarted -= HeaderThumb_DragStarted;
                    thumb.DragDelta -= HeaderThumb_DragDelta;
                    thumb.DragStarted += HeaderThumb_DragStarted;
                    thumb.DragDelta += HeaderThumb_DragDelta;
                }
            }
        }

        private void HeaderThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            var header = FindAncestor<GridViewColumnHeader>(sender as DependencyObject);
            if (header?.Column == null) return;
            var tag = header.Tag?.ToString();
            if (!IsColumnVisible(tag))
            {
                // 阻止隐藏列被拖动展开
                header.Column.Width = 0;
                e.Handled = true;
            }
        }

        private void HeaderThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var header = FindAncestor<GridViewColumnHeader>(sender as DependencyObject);
            if (header?.Column == null) return;
            var tag = header.Tag?.ToString();
            if (!IsColumnVisible(tag))
            {
                // 该列是隐藏列：把拖动量转嫁到左侧最近的可见列，避免用户感觉被阻塞
                header.Column.Width = 0; // 自身保持隐藏

                // 在列集合中找到当前列索引
                var gridView = FileBrowser?.FilesGrid;
                if (gridView != null)
                {
                    int idx = gridView.Columns.IndexOf(header.Column);
                    // 向左寻找最近的可见列
                    for (int i = idx - 1; i >= 0; i--)
                    {
                        var leftCol = gridView.Columns[i];
                        var leftHeader = leftCol.Header as GridViewColumnHeader;
                        var leftTag = leftHeader?.Tag?.ToString();
                        if (IsColumnVisible(leftTag))
                        {
                            double min = 40; // 最小宽度保护
                            double newWidth = Math.Max(min, leftCol.Width + e.HorizontalChange);
                            leftCol.Width = newWidth;
                            e.Handled = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                // 该列可见，但其右邻居可能是隐藏列：当向右拖动时，把扩展量施加到当前列，同时保持右邻居为0
                var gridView = FileBrowser?.FilesGrid;
                if (gridView != null && e.HorizontalChange > 0)
                {
                    int idx = gridView.Columns.IndexOf(header.Column);
                    if (idx >= 0 && idx + 1 < gridView.Columns.Count)
                    {
                        var rightCol = gridView.Columns[idx + 1];
                        var rightHeader = rightCol.Header as GridViewColumnHeader;
                        var rightTag = rightHeader?.Tag?.ToString();
                        if (!IsColumnVisible(rightTag))
                        {
                            // 右侧隐藏：放大当前列，并强制右侧维持隐藏
                            double min = 40;
                            header.Column.Width = Math.Max(min, header.Column.Width + e.HorizontalChange);
                            if (rightCol.Width != 0) rightCol.Width = 0;
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        private bool IsColumnVisible(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return true;
            var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
            var set = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            return set.Contains(tag);
        }

        private System.Windows.Controls.Primitives.Thumb FindHeaderThumb(GridViewColumnHeader header)
        {
            // 先尝试按模板名
            var thumb = header.Template?.FindName("PART_HeaderGripper", header) as System.Windows.Controls.Primitives.Thumb;
            if (thumb != null) return thumb;
            // 否则在视觉树中查找
            return FindDescendant<System.Windows.Controls.Primitives.Thumb>(header);
        }

        private T FindDescendant<T>(DependencyObject d) where T : DependencyObject
        {
            if (d == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(d, i);
                if (child is T t) return t;
                var deeper = FindDescendant<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }

        #endregion

        #region 键盘快捷键和文件操作

        private void FilesListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
                return;

            // Ctrl+A - 全选
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                listView.SelectAll();
                e.Handled = true;
                return;
            }

            // Ctrl+C - 复制
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Copy_Click(null, null);
                e.Handled = true;
                return;
            }

            // Ctrl+V - 粘贴
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Paste_Click(null, null);
                e.Handled = true;
                return;
            }

            // Ctrl+X - 剪切
            if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Cut_Click(null, null);
                e.Handled = true;
                return;
            }

            // Delete - 删除
            if (e.Key == Key.Delete)
            {
                Delete_Click(null, null);
                e.Handled = true;
                return;
            }

            // F2 - 重命名
            if (e.Key == Key.F2)
            {
                Rename_Click(null, null);
                e.Handled = true;
                return;
            }

            // F5 - 刷新
            if (e.Key == Key.F5)
            {
                Refresh_Click(null, null);
                e.Handled = true;
                return;
            }

            // Alt+Enter - 属性
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                ShowProperties_Click(null, null);
                e.Handled = true;
                return;
            }

            // Backspace - 返回上一级
            if (e.Key == Key.Back)
            {
                NavigateBack_Click(null, null);
                e.Handled = true;
                return;
            }

            // 处理方向键，防止焦点跑到分割器
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
            {
                if (listView.Items.Count == 0)
                    return;

                int currentIndex = listView.SelectedIndex;
                var wrapPanel = FindDescendant<WrapPanel>(listView);
                bool isTilesView = listView.View == null && wrapPanel != null;
                int columns = 1;
                if (isTilesView)
                {
                    double itemWidth = wrapPanel.ItemWidth > 0 ? wrapPanel.ItemWidth : (wrapPanel.Children.Count > 0 ? ((FrameworkElement)wrapPanel.Children[0]).ActualWidth : 160);
                    double viewportWidth = wrapPanel.ActualWidth > 0 ? wrapPanel.ActualWidth : listView.ActualWidth;
                    columns = Math.Max(1, (int)Math.Floor(viewportWidth / Math.Max(1, itemWidth)));
                }
                
                if (e.Key == Key.Down)
                {
                    if (isTilesView)
                    {
                        int next = currentIndex + columns;
                        if (next < listView.Items.Count)
                        {
                            listView.SelectedIndex = next;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                        else
                        {
                            listView.SelectedIndex = listView.Items.Count - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (currentIndex < listView.Items.Count - 1)
                        {
                            listView.SelectedIndex = currentIndex + 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    if (isTilesView)
                    {
                        int prev = currentIndex - columns;
                        if (prev >= 0)
                        {
                            listView.SelectedIndex = prev;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                        else
                        {
                            listView.SelectedIndex = 0;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (currentIndex > 0)
                        {
                            listView.SelectedIndex = currentIndex - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Home)
                {
                    listView.SelectedIndex = 0;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.End)
                {
                    listView.SelectedIndex = listView.Items.Count - 1;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    if (isTilesView)
                    {
                        if (currentIndex > 0)
                        {
                            listView.SelectedIndex = currentIndex - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        // 返回上一级
                        NavigateBack_Click(null, null);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    if (isTilesView)
                    {
                        if (currentIndex < listView.Items.Count - 1)
                        {
                            listView.SelectedIndex = currentIndex + 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        // 如果是文件夹，进入
                        if (listView.SelectedItem is FileSystemItem selectedItem && selectedItem.IsDirectory)
                        {
                            // 如果是库模式，切换到路径模式并导航
                            if (_currentLibrary != null)
                            {
                                // 切换到路径模式
                                _currentLibrary = null;
                                SwitchNavigationMode("Path");
                            }
                            
                            NavigateToPath(selectedItem.Path);
                        }
                    }
                    e.Handled = true;
                }
            }
            // 处理 Enter 键打开文件/文件夹
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
                {
                    if (selectedItem.IsDirectory)
                    {
                        // 如果是库模式，切换到路径模式并导航
                        if (_currentLibrary != null)
                        {
                            // 切换到路径模式
                            _currentLibrary = null;
                            SwitchNavigationMode("Path");
                        }
                        
                        NavigateToPath(selectedItem.Path);
                    }
                    else
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = selectedItem.Path,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                e.Handled = true;
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (FileBrowser?.FilesSelectedItems == null || FileBrowser.FilesSelectedItems.Count == 0)
                return;

            var selectedPaths = new List<string>();
            foreach (FileSystemItem item in FileBrowser.FilesSelectedItems)
            {
                selectedPaths.Add(item.Path);
            }

            FileClipboardManager.SetCopyPaths(selectedPaths);
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            if (FileBrowser?.FilesSelectedItems == null || FileBrowser.FilesSelectedItems.Count == 0)
                return;

            var selectedPaths = new List<string>();
            foreach (FileSystemItem item in FileBrowser.FilesSelectedItems)
            {
                selectedPaths.Add(item.Path);
            }

            FileClipboardManager.SetCutPaths(selectedPaths);
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            if (!FileClipboardManager.HasContent)
                return;

            var context = GetCurrentFileOperationContext();
            var pasteOperation = new PasteOperation(context);
            var copiedPaths = FileClipboardManager.GetCopiedPaths();
            var isCut = FileClipboardManager.IsCutOperation;

            pasteOperation.Execute(copiedPaths, isCut);

            // 如果是剪切操作，粘贴后清除剪贴板
            if (isCut)
            {
                FileClipboardManager.ClearCutOperation();
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (FileBrowser?.FilesSelectedItems == null || FileBrowser.FilesSelectedItems.Count == 0)
                return;

            var context = GetCurrentFileOperationContext();
            var deleteOperation = new DeleteOperation(context);
            var selectedItems = context.GetSelectedItems();

            deleteOperation.Execute(selectedItems);
        }

        private void Rename_Click(object sender, RoutedEventArgs e)
        {
            if (FileBrowser?.FilesSelectedItem is not FileSystemItem selectedItem)
                return;

            var context = GetCurrentFileOperationContext();
            var renameOperation = new RenameOperation(context, this);

            renameOperation.Execute(selectedItem);
        }

        private void ShowProperties_Click(object sender, RoutedEventArgs e)
        {
            if (FileBrowser?.FilesSelectedItem is not FileSystemItem selectedItem)
                return;

            try
            {
                var info = selectedItem.IsDirectory
                    ? (FileSystemInfo)new DirectoryInfo(selectedItem.Path)
                    : new FileInfo(selectedItem.Path);

                var message = $"名称: {info.Name}\n" +
                             $"位置: {Path.GetDirectoryName(info.FullName)}\n" +
                             $"大小: {selectedItem.Size}\n" +
                             $"创建时间: {info.CreationTime}\n" +
                             $"修改时间: {info.LastWriteTime}\n" +
                             $"访问时间: {info.LastAccessTime}\n" +
                             $"属性: {info.Attributes}";

                MessageBox.Show(message, "属性", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法获取属性: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 列表排序

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var header = sender as GridViewColumnHeader;
            if (header == null || header.Tag == null)
                return;

            var columnName = header.Tag.ToString();

            // 如果点击同一列，切换排序方向；否则默认升序
            if (_lastSortColumn == columnName)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _lastSortColumn = columnName;
                _sortAscending = true;
            }

            // 应用排序
            SortFiles();
            if (FileBrowser != null)
                FileBrowser.FilesItemsSource = null;
            if (FileBrowser != null)
                FileBrowser.FilesItemsSource = _currentFiles;

            // 更新列头显示排序指示器
            UpdateSortIndicators(header);
        }

        private void SortFiles()
        {
            if (_currentFiles == null || _currentFiles.Count == 0)
                return;

            // 分离文件夹和文件
            var directories = _currentFiles.Where(f => f.IsDirectory).ToList();
            var files = _currentFiles.Where(f => !f.IsDirectory).ToList();

            // 对文件夹和文件分别排序
            directories = SortList(directories);
            files = SortList(files);

            // 合并：文件夹在前，文件在后
            _currentFiles.Clear();
            _currentFiles.AddRange(directories);
            _currentFiles.AddRange(files);
        }
        private List<FileSystemItem> SortList(List<FileSystemItem> items)
        {
            IEnumerable<FileSystemItem> sorted = items;

            switch (_lastSortColumn)
            {
                case "Name":
                    sorted = _sortAscending 
                        ? items.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    break;

                case "Size":
                    sorted = _sortAscending
                        ? items.OrderBy(f => ParseSize(f.Size))
                        : items.OrderByDescending(f => ParseSize(f.Size));
                    break;

                case "Type":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Type, StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(f => f.Type, StringComparer.OrdinalIgnoreCase);
                    break;

                case "ModifiedDate":
                    sorted = _sortAscending
                        ? items.OrderBy(f => ParseDate(f.ModifiedDate))
                        : items.OrderByDescending(f => ParseDate(f.ModifiedDate));
                    break;

                case "CreatedTime":
                    sorted = _sortAscending
                        ? items.OrderBy(f => ParseTimeAgo(f.CreatedTime))
                        : items.OrderByDescending(f => ParseTimeAgo(f.CreatedTime));
                    break;

                case "Tags":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Tags ?? "", StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(f => f.Tags ?? "", StringComparer.OrdinalIgnoreCase);
                    break;

                case "Notes":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Notes ?? "", StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(f => f.Notes ?? "", StringComparer.OrdinalIgnoreCase);
                    break;
            }

            return sorted.ToList();
        }

        private long ParseSize(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr) || sizeStr == "-" || sizeStr == "计算中...")
                return 0;

            // 移除空格
            sizeStr = sizeStr.Replace(" ", "");

            try
            {
                // 提取数字和单位
                var number = new string(sizeStr.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
                var unit = sizeStr.Length > number.Length 
                    ? sizeStr.Substring(number.Length).ToUpper() 
                    : sizeStr.ToUpper();

                if (string.IsNullOrEmpty(number))
                    return 0;

                double value = double.Parse(number);

                // 转换为字节
                switch (unit)
                {
                    case "B":
                        return (long)value;
                    case "KB":
                        return (long)(value * 1024);
                    case "MB":
                        return (long)(value * 1024 * 1024);
                    case "GB":
                        return (long)(value * 1024 * 1024 * 1024);
                    case "TB":
                        return (long)(value * 1024 * 1024 * 1024 * 1024);
                    default:
                        return (long)value;
                }
            }
            catch
            {
                return 0;
            }
        }

        private DateTime ParseDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out DateTime result))
                return result;
            return DateTime.MinValue;
        }

        private long ParseTimeAgo(string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr))
                return long.MaxValue;

            try
            {
                // 提取数字
                var number = new string(timeStr.TakeWhile(c => char.IsDigit(c)).ToArray());
                if (string.IsNullOrEmpty(number))
                    return long.MaxValue;

                long value = long.Parse(number);

                // 根据单位转换为秒
                if (timeStr.EndsWith("s"))
                    return value;
                else if (timeStr.EndsWith("m"))
                    return value * 60;
                else if (timeStr.EndsWith("h"))
                    return value * 3600;
                else if (timeStr.EndsWith("d"))
                    return value * 86400;
                else if (timeStr.EndsWith("mo"))
                    return value * 2592000; // 30天
                else if (timeStr.EndsWith("y"))
                    return value * 31536000; // 365天

                return long.MaxValue;
            }
            catch
            {
                return long.MaxValue;
            }
        }

        private void UpdateSortIndicators(GridViewColumnHeader clickedHeader)
        {
            // 清除所有列头的排序指示器
            foreach (var column in FileBrowser.FilesGrid.Columns)
            {
                var header = column.Header as GridViewColumnHeader;
                if (header != null && header.Tag != null)
                {
                    var content = header.Content.ToString();
                    // 移除现有的排序符号
                    content = content.Replace(" ▲", "").Replace(" ▼", "");
                    header.Content = content;
                }
            }

            // 为当前列添加排序指示器
            if (clickedHeader != null)
            {
                var content = clickedHeader.Content.ToString();
                content = content.Replace(" ▲", "").Replace(" ▼", "");
                clickedHeader.Content = content + (_sortAscending ? " ▲" : " ▼");
            }
        }

        #endregion

        // ========== TagTrain 训练面板事件处理方法 ==========
        // 注意：这些是占位实现，完整的TagTrain功能集成需要更深入的开发
        
        private void TagTrainTagSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 重新加载标签列表（应用新的排序方式）
            LoadTagTrainExistingTags();
        }
        
        private void TagTrainTagInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO: 实现标签输入自动补完功能
        }
        
        private void TagTrainTagInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // TODO: 实现标签输入键盘导航功能
        }
        
        private void TagClickModeBtn_Click(object sender, RoutedEventArgs e)
        {
            _tagClickMode = _tagClickMode == OoiMRR.Services.Tag.TagClickMode.Browse ? OoiMRR.Services.Tag.TagClickMode.Edit : OoiMRR.Services.Tag.TagClickMode.Browse;
            try
            {
                if (sender is Button btn)
                {
                    btn.Content = _tagClickMode == OoiMRR.Services.Tag.TagClickMode.Browse ? "👁" : "✏️";
                    btn.ToolTip = _tagClickMode == OoiMRR.Services.Tag.TagClickMode.Browse
                        ? "切换到编辑模式：显示完整TagTrain训练面板"
                        : "切换到浏览模式：只显示标签列表";
                }
                
                // 切换浏览/编辑模式的显示
                SwitchTagMode();
                
                // 根据模式调整相关按钮显示/隐藏
                ApplyTagClickModeVisibility();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TagClickModeBtn_Click error: {ex.Message}");
            }
        }
        
        // 切换标签浏览/编辑模式
        private void SwitchTagMode()
        {
            try
            {
                if (TagBrowsePanel != null && TagEditPanel != null)
                {
                    if (_tagClickMode == OoiMRR.Services.Tag.TagClickMode.Browse)
                    {
                        TagBrowsePanel.Visibility = Visibility.Visible;
                        TagEditPanel.Visibility = Visibility.Collapsed;
                        // 加载浏览模式的标签列表
                        if (TagBrowsePanel.Mode != TagTrain.UI.TagPanel.DisplayMode.Browse)
                        {
                            TagBrowsePanel.Mode = TagTrain.UI.TagPanel.DisplayMode.Browse;
                        }
                        TagBrowsePanel.LoadExistingTags();
                    }
                    else
                    {
                        TagBrowsePanel.Visibility = Visibility.Collapsed;
                        TagEditPanel.Visibility = Visibility.Visible;
                        // 加载编辑模式的标签列表
                        if (TagEditPanel.Mode != TagTrain.UI.TagPanel.DisplayMode.Edit)
                        {
                            TagEditPanel.Mode = TagTrain.UI.TagPanel.DisplayMode.Edit;
                        }
                        TagEditPanel.LoadExistingTags();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SwitchTagMode error: {ex.Message}");
            }
        }
        
        // 分组管理按钮点击
        private void TagCategoryManageBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenCategoryManagement();
        }
        
        // 浏览模式头部的分组管理按钮
        private void TagBrowseCategoryManagement_Click(object sender, RoutedEventArgs e)
        {
            OpenCategoryManagement();
        }
        
        // 打开分组管理窗口（统一方法）
        private void OpenCategoryManagement()
        {
            _tagService.OpenCategoryManagement(this, TagBrowsePanel, TagEditPanel, _tagClickMode);
        }
        
        // EditTagName 已迁移到 TagService
        // 保留方法签名用于向后兼容（已废弃）
        [Obsolete("Use TagService.EditTagName instead")]
        private void EditTagName(int tagId, string oldTagName)
        {
            try
            {
                // 获取标签名称（如果传入的是ID）
                if (string.IsNullOrEmpty(oldTagName))
                {
                    oldTagName = TagTrain.Services.DataManager.GetTagName(tagId);
                    if (string.IsNullOrEmpty(oldTagName))
                    {
                        MessageBox.Show("无法获取标签名称", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                
                // 创建输入对话框
                var inputDialog = new Window
                {
                    Title = "修改标签名称",
                    Width = 400,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.Margin = new Thickness(0);

                var textBlock = new TextBlock
                {
                    Text = $"请输入新的标签名称：",
                    Margin = new Thickness(15, 20, 15, 10),
                    VerticalAlignment = VerticalAlignment.Top
                };
                Grid.SetRow(textBlock, 0);

                var textBox = new TextBox
                {
                    Text = oldTagName,
                    Margin = new Thickness(15, 0, 15, 15),
                    FontSize = 14,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Height = 30
                };
                Grid.SetRow(textBox, 1);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 15, 15)
                };
                Grid.SetRow(buttonPanel, 2);

                var okButton = new Button
                {
                    Content = "确定",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Width = 80,
                    Height = 30,
                    IsCancel = true
                };

                string newTagName = null;
                bool dialogResult = false;

                okButton.Click += (s, e) =>
                {
                    newTagName = textBox.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(newTagName))
                    {
                        dialogResult = true;
                        inputDialog.DialogResult = true;
                        inputDialog.Close();
                    }
                    else
                    {
                        MessageBox.Show("标签名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };

                cancelButton.Click += (s, e) =>
                {
                    inputDialog.DialogResult = false;
                    inputDialog.Close();
                };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                grid.Children.Add(textBlock);
                grid.Children.Add(textBox);
                grid.Children.Add(buttonPanel);

                inputDialog.Content = grid;

                // 设置焦点到文本框并选中所有文本
                textBox.Loaded += (s, e) =>
                {
                    textBox.Focus();
                    textBox.SelectAll();
                };

                if (inputDialog.ShowDialog() == true && dialogResult && !string.IsNullOrWhiteSpace(newTagName))
                {
                    if (newTagName == oldTagName)
                    {
                        return; // 名称未改变
                    }

                    try
                    {
                        // 更新标签名称
                        bool success = TagTrain.Services.DataManager.UpdateTagName(oldTagName, newTagName);
                        
                        if (success)
                        {
                            // 刷新标签列表
                            if (_tagClickMode == OoiMRR.Services.Tag.TagClickMode.Browse)
                            {
                                TagBrowsePanel?.LoadExistingTags();
                            }
                            else
                            {
                                LoadTagTrainExistingTags();
                            }
                            
                            MessageBox.Show($"标签名称已从 \"{oldTagName}\" 修改为 \"{newTagName}\"。\n所有训练数据已保留。", 
                                "修改成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            MessageBox.Show($"修改失败：新标签名称 \"{newTagName}\" 已存在或旧标签不存在。", 
                                "修改失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"修改标签名称时发生错误：{ex.Message}", 
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开修改对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 删除标签（根据ID）
        private void DeleteTagById(int tagId, string tagName)
        {
            try
            {
                // 获取标签名称（如果传入的是ID）
                if (string.IsNullOrEmpty(tagName))
                {
                    tagName = TagTrain.Services.DataManager.GetTagName(tagId);
                    if (string.IsNullOrEmpty(tagName))
                    {
                        tagName = $"标签{tagId}";
                    }
                }
                
                var result = MessageBox.Show(
                    $"确定要删除标签 \"{tagName}\" 吗？\n这将删除所有使用该标签的训练数据。",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        TagTrain.Services.DataManager.DeleteTag(tagId);
                        
                        // 刷新标签列表
                        if (_tagClickMode == OoiMRR.Services.Tag.TagClickMode.Browse)
                        {
                            TagBrowsePanel?.LoadExistingTags();
                        }
                        else
                        {
                            LoadTagTrainExistingTags();
                        }
                        
                        MessageBox.Show($"标签 \"{tagName}\" 已删除。", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除标签时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // 根据浏览/编辑模式控制某些按钮的显示/隐藏
        private void ApplyTagClickModeVisibility()
        {
            try
            {
                // 设计规则：
                // - 浏览模式：显示“批量操作”“训练情况”
                // - 编辑模式：隐藏“批量操作”“训练情况”，以免分散注意力
                // 这些按钮现在由TagPanel内部管理，此方法已废弃
                // if (TagTrainBatchOperationBtn != null)
                //     TagTrainBatchOperationBtn.Visibility = _tagClickMode == TagClickMode.Browse ? Visibility.Visible : Visibility.Collapsed;
                // if (TagTrainTrainingStatusBtn != null)
                //     TagTrainTrainingStatusBtn.Visibility = _tagClickMode == TagClickMode.Browse ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }
        
        private void TagTrainTagInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // TODO: 实现标签输入预览按键处理
        }
        
        private void TagTrainTagInputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // TODO: 实现标签输入框获得焦点时的处理
        }
        
        private void TagTrainTagInputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // TODO: 实现标签输入框失去焦点时的处理
        }
        
        private void TagTrainTagAutocompleteListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // TODO: 实现自动补完列表双击选择功能
        }
        
        private void TagTrainTagAutocompleteListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // TODO: 实现自动补完列表键盘导航功能
        }
        
        private void TagTrainConfirmTag_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var selectedBefore = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().Select(i => i.Path).ToList() ?? new List<string>();
                var text = TagEditPanel?.TagInputTextBox?.Text ?? "";
                var tagNames = (text ?? "")
                    .Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                if (tagNames.Count == 0)
                {
                    MessageBox.Show("请输入至少一个标签名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    MessageBox.Show("请先选择要打标签的图片文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
                foreach (var name in tagNames)
                {
                    var tagId = OoiMRRIntegration.GetOrCreateTagId(name);
                    if (tagId <= 0) continue;
                    
                    foreach (var it in selectedItems)
                    {
                        if (!it.IsDirectory && imageExtensions.Contains(System.IO.Path.GetExtension(it.Path).ToLowerInvariant()))
                        {
                            OoiMRRIntegration.AddTagToFile(it.Path, tagId);
                        }
                    }
                }
                
                // 刷新界面
                LoadTagTrainExistingTags();
                if (_currentTagFilter != null)
                {
                    FilterByTag(_currentTagFilter);
                    RestoreSelectionByPaths(selectedBefore);
                }
                else
                {
                    LoadFiles();
                    RestoreSelectionByPaths(selectedBefore);
                }
                
                if (TagEditPanel?.TagInputTextBox != null)
                    TagEditPanel.TagInputTextBox.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"确认标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TagTrainConfirmAIPrediction_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var selectedBefore = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().Select(i => i.Path).ToList() ?? new List<string>();
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    MessageBox.Show("请先选择图片后再确认AI预测。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
                foreach (var it in selectedItems)
                {
                    if (it.IsDirectory) continue;
                    if (!imageExtensions.Contains(System.IO.Path.GetExtension(it.Path).ToLowerInvariant())) continue;
                    
                    var predictions = OoiMRRIntegration.PredictTagsForImage(it.Path) ?? new List<TagTrain.Services.TagPredictionResult>();
                    // 选取 Top3 且置信度 >= 0.5
                    foreach (var p in predictions
                                 .OrderByDescending(x => x.Confidence)
                                 .Take(3)
                                 .Where(x => x.Confidence >= 0.5f))
                    {
                        OoiMRRIntegration.AddTagToFile(it.Path, p.TagId);
                    }
                }
                
                LoadTagTrainExistingTags();
                if (_currentTagFilter != null)
                {
                    FilterByTag(_currentTagFilter);
                    RestoreSelectionByPaths(selectedBefore);
                }
                else
                {
                    LoadFiles();
                    RestoreSelectionByPaths(selectedBefore);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"确认AI预测失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TagTrainSkip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TagEditPanel?.TagInputTextBox != null)
                    TagEditPanel.TagInputTextBox.Text = "";
                
                // 选中下一个文件项（如果存在）
                if (FileBrowser?.FilesList != null && FileBrowser.FilesList.Items.Count > 0)
                {
                    var idx = FileBrowser.FilesList.SelectedIndex;
                    var next = Math.Min(Math.Max(idx + 1, 0), FileBrowser.FilesList.Items.Count - 1);
                    if (next != idx)
                    {
                        FileBrowser.FilesList.SelectedIndex = next;
                        FileBrowser.FilesList.ScrollIntoView(FileBrowser.FilesList.SelectedItem);
                    }
                }
            }
            catch { }
        }
        
        private void TagTrainStartTraining_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            // 切换：正在训练则作为“停止”处理
            if (_tagTrainIsTraining)
            {
                TagTrainCancelTraining_Click(sender, e);
                return;
            }
            
            _tagTrainTrainingCancellation = new CancellationTokenSource();
            var progress = new Progress<TagTrain.Services.TrainingProgress>(UpdateTagTrainTrainingProgress);
            _tagTrainIsTraining = true;
            if (TagEditPanel?.TrainingProgressGrid != null) TagEditPanel.TrainingProgressGrid.Visibility = Visibility.Visible;
            if (TagEditPanel?.TrainingProgressBar != null) TagEditPanel.TrainingProgressBar.Value = 0;
            if (TagEditPanel?.TrainingStageText != null) TagEditPanel.TrainingStageText.Text = "";
            if (TagEditPanel?.TrainingProgressText != null) TagEditPanel.TrainingProgressText.Text = "";
            // 这些按钮现在由TagPanel内部管理
            // if (TagTrainPauseBtn != null) TagTrainPauseBtn.IsEnabled = true;
            // if (TagTrainCancelTrainingBtn != null) TagTrainCancelTrainingBtn.IsEnabled = true;
            if (TagEditPanel?.StartTrainingBtn != null) TagEditPanel.StartTrainingBtn.Content = "⏹️ 停止训练";
            if (TagEditPanel?.RetrainModelBtn != null) TagEditPanel.RetrainModelBtn.IsEnabled = false;
            
            Task.Run(() =>
            {
                try
                {
                    var result = OoiMRRIntegration.TriggerIncrementalTraining(false, progress, _tagTrainTrainingCancellation.Token);
                    Dispatcher.Invoke(() =>
                    {
                        _tagTrainIsTraining = false;
                        if (TagEditPanel?.TrainingProgressGrid != null) TagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        // if (TagTrainPauseBtn != null) TagTrainPauseBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                        // if (TagTrainCancelTrainingBtn != null) TagTrainCancelTrainingBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                        if (TagEditPanel?.StartTrainingBtn != null) TagEditPanel.StartTrainingBtn.Content = "▶️ 开始训练";
                        if (TagEditPanel?.RetrainModelBtn != null) TagEditPanel.RetrainModelBtn.IsEnabled = true;
                        
                        if (!(result.Success == false && (result.Message ?? "").Contains("已取消")))
                        {
                            if (result.Success)
                                DialogService.Info("训练完成", "成功", this);
                            else
                                DialogService.Error($"训练失败：{result.Message}", "错误", this);
                        }
                        
                        UpdateTagTrainModelStatus();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        _tagTrainIsTraining = false;
                        if (TagEditPanel?.TrainingProgressGrid != null) TagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        // if (TagTrainPauseBtn != null) TagTrainPauseBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                        // if (TagTrainCancelTrainingBtn != null) TagTrainCancelTrainingBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                        if (TagEditPanel?.StartTrainingBtn != null) TagEditPanel.StartTrainingBtn.Content = "▶️ 开始训练";
                        if (TagEditPanel?.RetrainModelBtn != null) TagEditPanel.RetrainModelBtn.IsEnabled = true;
                        DialogService.Error($"训练出错: {ex.Message}", "错误", this);
                        UpdateTagTrainModelStatus();
                    });
                }
            });
        }
        
        private void TagTrainPause_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_tagTrainIsTraining && _tagTrainTrainingCancellation != null && !_tagTrainTrainingCancellation.IsCancellationRequested)
                {
                    _tagTrainTrainingCancellation.Cancel();
                    // if (TagTrainPauseBtn != null) TagTrainPauseBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                }
            }
            catch { }
        }
        
        private void TagTrainRetrainModel_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            if (_tagTrainIsTraining)
            {
                MessageBox.Show("已有训练正在进行，请先取消或等待完成。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            _tagTrainTrainingCancellation = new CancellationTokenSource();
            var progress = new Progress<TagTrain.Services.TrainingProgress>(UpdateTagTrainTrainingProgress);
            _tagTrainIsTraining = true;
            if (TagEditPanel?.TrainingProgressGrid != null) TagEditPanel.TrainingProgressGrid.Visibility = Visibility.Visible;
            if (TagEditPanel?.TrainingProgressBar != null) TagEditPanel.TrainingProgressBar.Value = 0;
            if (TagEditPanel?.TrainingStageText != null) TagEditPanel.TrainingStageText.Text = "";
            if (TagEditPanel?.TrainingProgressText != null) TagEditPanel.TrainingProgressText.Text = "";
            // if (TagTrainPauseBtn != null) TagTrainPauseBtn.IsEnabled = true; // 已废弃，由TagPanel管理
            // if (TagTrainCancelTrainingBtn != null) TagTrainCancelTrainingBtn.IsEnabled = true; // 已废弃，由TagPanel管理
            if (TagEditPanel?.StartTrainingBtn != null) TagEditPanel.StartTrainingBtn.IsEnabled = false;
            if (TagEditPanel?.RetrainModelBtn != null) TagEditPanel.RetrainModelBtn.IsEnabled = false;
            
            Task.Run(() =>
            {
                try
                {
                    var result = OoiMRRIntegration.TriggerIncrementalTraining(true, progress, _tagTrainTrainingCancellation.Token);
                    Dispatcher.Invoke(() =>
                    {
                        _tagTrainIsTraining = false;
                        if (TagEditPanel?.TrainingProgressGrid != null) TagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        // if (TagTrainPauseBtn != null) TagTrainPauseBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                        // if (TagTrainCancelTrainingBtn != null) TagTrainCancelTrainingBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                        if (TagEditPanel?.StartTrainingBtn != null) TagEditPanel.StartTrainingBtn.IsEnabled = true;
                        if (TagEditPanel?.RetrainModelBtn != null) TagEditPanel.RetrainModelBtn.IsEnabled = true;
                        if (!(result.Success == false && (result.Message ?? "").Contains("已取消")))
                        {
                            MessageBox.Show(result.Success ? "重新训练完成" : $"重新训练失败：{result.Message}", 
                                result.Success ? "成功" : "错误",
                                MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
                        }
                        UpdateTagTrainModelStatus();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        _tagTrainIsTraining = false;
                        if (TagEditPanel?.TrainingProgressGrid != null) TagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        // if (TagTrainPauseBtn != null) TagTrainPauseBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                        // if (TagTrainCancelTrainingBtn != null) TagTrainCancelTrainingBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                        if (TagEditPanel?.StartTrainingBtn != null) TagEditPanel.StartTrainingBtn.IsEnabled = true;
                        if (TagEditPanel?.RetrainModelBtn != null) TagEditPanel.RetrainModelBtn.IsEnabled = true;
                        MessageBox.Show($"重新训练出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        UpdateTagTrainModelStatus();
                    });
                }
            });
        }
        
        private void TagTrainCancelTraining_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_tagTrainIsTraining && _tagTrainTrainingCancellation != null && !_tagTrainTrainingCancellation.IsCancellationRequested)
                {
                    _tagTrainTrainingCancellation.Cancel();
                    // 立即重置界面与状态，避免用户感知为“仍在运行”
                    _tagTrainIsTraining = false;
                    // if (TagTrainCancelTrainingBtn != null) TagTrainCancelTrainingBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                    // if (TagTrainPauseBtn != null) TagTrainPauseBtn.IsEnabled = false; // 已废弃，由TagPanel管理
                    if (TagEditPanel?.StartTrainingBtn != null) TagEditPanel.StartTrainingBtn.Content = "▶️ 开始训练";
                    if (TagEditPanel?.TrainingProgressGrid != null) TagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                    if (TagEditPanel?.TrainingProgressBar != null) TagEditPanel.TrainingProgressBar.Value = 0;
                    if (TagEditPanel?.TrainingStageText != null) TagEditPanel.TrainingStageText.Text = "已停止";
                    if (TagEditPanel?.TrainingProgressText != null) TagEditPanel.TrainingProgressText.Text = "";
                    // 允许重新开始
                    if (TagEditPanel?.RetrainModelBtn != null) TagEditPanel.RetrainModelBtn.IsEnabled = true;
                }
            }
            catch { }
        }
        
        private void TagTrainBatchOperation_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("批量操作功能暂未实现。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void TagTrainTrainingStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var stats = OoiMRRIntegration.GetStatistics();
                MessageBox.Show(
                    $"训练样本: {stats.TotalSamples}\n手动样本: {stats.ManualSamples}\n唯一图片: {stats.UniqueImages}\n唯一标签: {stats.UniqueTags}",
                    "训练情况", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取训练情况失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void TagTrainConsolidateTags_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var result = OoiMRRIntegration.ConsolidateDuplicateTags();
                MessageBox.Show(
                    $"合并组数: {result.MergedGroups}\n更新样本: {result.UpdatedSamples}\n删除标签: {result.DeletedTagIds}",
                    "清理重复标签", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadTagTrainExistingTags();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清理重复标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void UpdateTagTrainTrainingProgress(TagTrain.Services.TrainingProgress progress)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (TagEditPanel?.TrainingStageText != null) TagEditPanel.TrainingStageText.Text = progress?.Stage ?? "";
                    if (TagEditPanel?.TrainingProgressBar != null) TagEditPanel.TrainingProgressBar.Value = progress?.Progress ?? 0;
                    if (TagEditPanel?.TrainingProgressText != null) TagEditPanel.TrainingProgressText.Text =
                        $"{(progress?.Progress ?? 0)}% - {(progress?.Message ?? "")}";
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch { }
        }
        
        // 根据路径集合恢复文件列表选中项
        private void RestoreSelectionByPaths(List<string> paths)
        {
            if (paths == null || paths.Count == 0) return;
            try
            {
                // 延迟到UI加载完成后再恢复选中，避免 ItemsSource 变化时修改 SelectedItems
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    var listView = FileBrowser?.FilesList;
                    if (listView == null) return;
                    
                    // 清空并逐项恢复
                    var selected = FileBrowser?.FilesSelectedItems;
                    selected?.Clear();
                    
                    var targetSet = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
                    if (listView.Items != null)
                    {
                        foreach (var obj in listView.Items)
                        {
                            if (obj is FileSystemItem fi && targetSet.Contains(fi.Path))
                            {
                                selected?.Add(obj);
                            }
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch { }
        }
        
        private void TagTrainConfig_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现设置功能（可以打开TagTrain的设置窗口）
            try
            {
                if (App.IsTagTrainAvailable)
                {
                    // 可以尝试打开TagTrain的配置窗口
                    try { TagTrain.Services.SettingsManager.ClearCache(); } catch { }
                    var configWindow = new TagTrain.UI.ConfigWindow();
                    var result = configWindow.ShowDialog();
                    
                    // 保存设置后自动刷新左侧标签，无需重启
                    if (result == true)
                    {
                        // 清理 TagTrain 缓存，确保使用最新设置路径
                        try
                        {
                            TagTrain.Services.SettingsManager.ClearCache();
                            TagTrain.Services.DataManager.ClearDatabasePathCache();
                            
                            // 同步保存到 OoiMRR 自己的配置，便于下次启动前置设置
                            var cfg = ConfigManager.Load();
                            var storageDir = TagTrain.Services.SettingsManager.GetDataStorageDirectory();
                            cfg.TagTrainDataDirectory = storageDir;
                            
                            // 关键：把 DataStorageDirectory 写入默认 settings.txt，
                            // 让下次 LoadSettings 能从默认文件定位到新的目录
                            TagTrain.Services.SettingsManager.SetDataStorageDirectory(storageDir);
                            ConfigManager.Save(cfg);
                        }
                        catch { }
                        
                        // 重新加载状态与标签
                        UpdateTagTrainModelStatus();
                        LoadTagTrainExistingTags();
                    }
                }
                else
                {
                    MessageBox.Show("TagTrain 不可用，无法打开设置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FileBrowser_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }

    public class FileSystemItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string ModifiedDate { get; set; }
        public string CreatedTime { get; set; }
        public string Tags { get; set; }
        public string Notes { get; set; }
        public bool IsDirectory { get; set; }
        public string SourcePath { get; set; } // 库模式下的来源路径
        
        /// <summary>
        /// 标记搜索结果的来源类型
        /// </summary>
        public Services.Search.SearchResultType? SearchResultType { get; set; }
        
        /// <summary>
        /// 是否来自备注搜索
        /// </summary>
        public bool IsFromNotesSearch { get; set; }
        
        /// <summary>
        /// 是否来自文件名搜索
        /// </summary>
        public bool IsFromNameSearch { get; set; }

        /// <summary>
        /// 格式化时间为简洁显示（s/m/h/d/mo/y）
        /// </summary>
        public static string FormatTimeAgo(DateTime createdTime)
        {
            var timeSpan = DateTime.Now - createdTime;
            
            if (timeSpan.TotalSeconds < 60)
                return $"{(int)timeSpan.TotalSeconds}s";
            
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m";
            
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h";
            
            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays}d";
            
            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)}mo";
            
            return $"{(int)(timeSpan.TotalDays / 365)}y";
        }
    }
    
    /// <summary>
    /// 时间单位转换器 - 提取时间字符串中的单位
    /// </summary>
    public class TimeUnitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;
            
            string timeStr = value.ToString();
            if (string.IsNullOrEmpty(timeStr)) return null;

            // 提取单位（最后的字母）
            if (timeStr.EndsWith("s"))
                return "s";
            else if (timeStr.EndsWith("m"))
                return "m";
            else if (timeStr.EndsWith("h"))
                return "h";
            else if (timeStr.EndsWith("d"))
                return "d";
            else if (timeStr.EndsWith("mo"))
                return "mo";
            else if (timeStr.EndsWith("y"))
                return "y";
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}



