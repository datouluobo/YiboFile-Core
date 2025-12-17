using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
using System.Windows.Controls.Primitives;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.ComponentModel;
using OoiMRR.Services;
using OoiMRR.Services.FileNotes;
using OoiMRR.Services.Search;
using OoiMRR.Services.Navigation;
using OoiMRR.Services.FileOperations;
using OoiMRR.Services.Favorite;
using OoiMRR.Services.QuickAccess;
using OoiMRR.Services.FileList;
using OoiMRR.Services.Tabs;
using OoiMRR.Services.Preview;
using OoiMRR.Services.ColumnManagement;
using OoiMRR.Services.Config;
using OoiMRR.Handlers;
using System.Threading;
using System.Text.Json;
using System.Text;
using TagTrain.UI;
using OoiMRR.Models.UI;

namespace OoiMRR
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, IConfigUIHelper, Services.Navigation.INavigationModeUIHelper
    {
        internal string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        internal List<FileSystemItem> _currentFiles = new List<FileSystemItem>();

        private DragDropManager _dragDropManager;
        private System.Windows.Point _mouseDownPoint;
        private bool _isMouseDownOnListView = false;
        private bool _isMouseDownOnColumnHeader = false;
        internal Library _currentLibrary = null;
        internal Tag _currentTagFilter = null;
        internal bool _isUpdatingTagSelection = false;

        
        // 统一导航协调器
        internal NavigationCoordinator _navigationCoordinator;
        
        // 服务实例
        internal NavigationService _navigationService;
        internal LibraryService _libraryService;
        private FavoriteService _favoriteService;
        private QuickAccessService _quickAccessService;
        internal FileListService _fileListService;
        private FileSystemWatcherService _fileSystemWatcherService;
        internal FolderSizeCalculationService _folderSizeCalculationService;
        internal TabService _tabService;
        internal Services.Preview.PreviewService _previewService;
        internal SearchService _searchService;
        internal SearchCacheService _searchCacheService;
        internal Services.ColumnManagement.ColumnService _columnService;
        internal ConfigService _configService;
        internal Services.Settings.SettingsOverlayController _settingsOverlayController;
        internal Services.Navigation.NavigationModeService _navigationModeService;
        internal Services.UIHelper.UIHelperService _uiHelperService;
        internal Services.WindowStateManager _windowStateManager;
        private Services.FileInfo.FileInfoService _fileInfoService;
        private Services.FileNotes.FileNotesUIHandler _fileNotesUIHandler;
        private Services.Tag.TagUIHandler _tagUIHandler;
        
        // 事件处理器
        internal Handlers.FileBrowserEventHandler _fileBrowserEventHandler;
        internal Handlers.MenuEventHandler _menuEventHandler;
        internal Handlers.KeyboardEventHandler _keyboardEventHandler;
        internal Handlers.MouseEventHandler _mouseEventHandler;
        internal Services.TagTrain.TagTrainEventHandler _tagTrainEventHandler;
        
        // 加载锁定，防止重复加载导致卡死
        // TODO: 过渡代码 - 这些字段在事件处理器中仍在使用，后续考虑通过服务状态管理
        private bool _isLoadingFiles = false;
        private System.Threading.SemaphoreSlim _loadFilesSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        private System.Threading.CancellationTokenSource _currentFolderSizeCTS;
        
        // 定时器管理
        // 定时器管理
        internal System.Windows.Threading.DispatcherTimer _periodicTimer = new System.Windows.Threading.DispatcherTimer();
        internal System.Windows.Threading.DispatcherTimer _layoutCheckTimer = new System.Windows.Threading.DispatcherTimer();
        internal bool _isSplitterDragging = false; // 标记是否正在拖拽分割器
        internal Services.Search.SearchOptions _searchOptions = new Services.Search.SearchOptions();
        
        // 统一的标签显示排序：当前过滤标签优先，其余按名称升序
        internal List<string> OrderTagNames(List<int> tagIds)
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
        internal CancellationTokenSource _tagTrainTrainingCancellation = null;
        internal bool _tagTrainIsTraining = false;
        
        // 标签点击模式
        internal enum TagClickMode { Browse, Edit }
        internal TagClickMode _tagClickMode = TagClickMode.Browse;

        // INavigationModeUIHelper 实现
        Services.Navigation.TagClickMode Services.Navigation.INavigationModeUIHelper.TagClickMode
        {
            get => (Services.Navigation.TagClickMode)_tagClickMode;
            set => _tagClickMode = (TagClickMode)value;
        }
        
        
        private List<DraggableButton> _currentActionButtons = new List<DraggableButton>();
        private List<ActionItem> _actionItems = new List<ActionItem>(); // 保存按钮和分隔符的完整顺序

        // NavigationPanelControl控件的便捷访问属性
        // 改为 internal 以便 NavigationUIHelper 可以访问
        internal ListBox LibrariesListBox => NavigationPanelControl?.LibrariesListBoxControl;
        internal ListBox DrivesListBox => NavigationPanelControl?.DrivesListBoxControl;
        internal ListBox QuickAccessListBox => NavigationPanelControl?.QuickAccessListBoxControl;
        internal ListBox FavoritesListBox => NavigationPanelControl?.FavoritesListBoxControl;
        internal Grid NavPathContent => NavigationPanelControl?.NavPathContentControl;
        internal Grid NavLibraryContent => NavigationPanelControl?.NavLibraryContentControl;
        internal Grid NavTagContent => NavigationPanelControl?.NavTagContentControl;
        internal TagPanel TagBrowsePanel => NavigationPanelControl?.TagBrowsePanelControl;
        internal TagPanel TagEditPanel => NavigationPanelControl?.TagEditPanelControl;
        private StackPanel TagBottomButtons => NavigationPanelControl?.TagBottomButtonsControl;
        private StackPanel LibraryBottomButtons => NavigationPanelControl?.LibraryBottomButtonsControl;
        internal ContextMenu LibraryContextMenu => NavigationPanelControl?.LibraryContextMenuControl;

        // INavigationModeUIHelper 实现
        System.Windows.Threading.Dispatcher Services.Navigation.INavigationModeUIHelper.Dispatcher => this.Dispatcher;
        Library Services.Navigation.INavigationModeUIHelper.CurrentLibrary
        {
            get => _currentLibrary;
            set => _currentLibrary = value;
        }
        string Services.Navigation.INavigationModeUIHelper.CurrentPath
        {
            get => _currentPath;
            set => _currentPath = value;
        }
        StackPanel Services.Navigation.INavigationModeUIHelper.TagBottomButtons => TagBottomButtons;
        StackPanel Services.Navigation.INavigationModeUIHelper.LibraryBottomButtons => LibraryBottomButtons;
        Grid Services.Navigation.INavigationModeUIHelper.NavTagContent => NavTagContent;
        TagTrain.UI.TagPanel Services.Navigation.INavigationModeUIHelper.TagBrowsePanel => TagBrowsePanel;
        TagTrain.UI.TagPanel Services.Navigation.INavigationModeUIHelper.TagEditPanel => TagEditPanel;
        Controls.FileBrowserControl Services.Navigation.INavigationModeUIHelper.FileBrowser => FileBrowser;
        ListBox Services.Navigation.INavigationModeUIHelper.LibrariesListBox => LibrariesListBox;
        Controls.NavigationPanelControl Services.Navigation.INavigationModeUIHelper.NavigationPanelControl => NavigationPanelControl;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavPathButton => NavPathBtn;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavLibraryButton => NavLibraryBtn;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavTagButton => NavTagBtn;

        // INavigationModeUIHelper 方法实现
        void Services.Navigation.INavigationModeUIHelper.InitializeTagTrainPanel() => InitializeTagTrainPanel();
        void Services.Navigation.INavigationModeUIHelper.SwitchToTab(Services.Tabs.PathTab tab) => SwitchToTab(tab);
        void Services.Navigation.INavigationModeUIHelper.CreateTab(string path) => CreateTab(path);
        void Services.Navigation.INavigationModeUIHelper.HighlightMatchingLibrary(Library library) => HighlightMatchingLibrary(library);
        void Services.Navigation.INavigationModeUIHelper.EnsureSelectedItemVisible(ListBox listBox, object selectedItem) => _uiHelperService?.EnsureSelectedItemVisible(listBox, selectedItem);
        void Services.Navigation.INavigationModeUIHelper.LoadLibraryFiles(Library library) => LoadLibraryFiles(library);
        void Services.Navigation.INavigationModeUIHelper.InitializeLibraryDragDrop() => InitializeLibraryDragDrop();
        void Services.Navigation.INavigationModeUIHelper.ApplyVisibleColumnsForCurrentMode() => ApplyVisibleColumnsForCurrentMode();
        void Services.Navigation.INavigationModeUIHelper.EnsureHeaderContextMenuHook() => EnsureHeaderContextMenuHook();
        void Services.Navigation.INavigationModeUIHelper.RefreshFileList() => RefreshFileList();

        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化统一导航协调器
            _navigationCoordinator = new NavigationCoordinator();
            
            // 初始化服务实例
            _navigationService = new NavigationService(_currentPath);
            
            // 创建并设置 UI Helper
            var uiHelper = new NavigationUIHelper(this);
            _navigationService.UIHelper = uiHelper;
            
            _libraryService = new LibraryService(this.Dispatcher);
            _favoriteService = new FavoriteService(this.Dispatcher);
            _quickAccessService = new QuickAccessService(this.Dispatcher);
            _fileListService = new FileListService(this.Dispatcher);
            _fileSystemWatcherService = new FileSystemWatcherService(this.Dispatcher);
            _folderSizeCalculationService = new FolderSizeCalculationService();
            
            // 初始化标签页服务（需要配置，在加载配置后更新）
            // 注意：_config 将在 InitializeApplication 中加载，这里先创建空配置
            _tabService = new TabService(new AppConfig());
            
            // 初始化搜索服务
            var searchFilterService = new SearchFilterService();
            _searchCacheService = new SearchCacheService();
            var searchResultBuilder = new SearchResultBuilder(
                formatFileSize: size => _fileListService.FormatFileSize(size),
                getFileTagIds: path => App.IsTagTrainAvailable ? OoiMRRIntegration.GetFileTagIds(path) : null,
                getTagName: tagId => App.IsTagTrainAvailable ? OoiMRRIntegration.GetTagName(tagId) : null,
                getFileNotes: path => FileNotesService.GetFileNotes(path)
            );
            _searchService = new SearchService(searchFilterService, _searchCacheService, searchResultBuilder);

            // 初始化列管理服务
            // 注意：_config 将在 InitializeApplication 中加载，这里先创建空配置
            var tempConfig = new AppConfig();
            _columnService = new ColumnService(
                tempConfig,
                () => GetCurrentModeKey(),
                () => { if (_configService != null) _configService.SaveCurrentConfig(); }
            );

            AttachTabServiceUiContext();

            // 初始化 UI 辅助服务（需要在 InitializeComponent 之后，因为需要 FileBrowser）
            _uiHelperService = new Services.UIHelper.UIHelperService(FileBrowser, this.Dispatcher);

            // 初始化文件信息服务（需要在 InitializeComponent 之后，因为需要 FileBrowser）
            _fileInfoService = new Services.FileInfo.FileInfoService(FileBrowser, _fileListService);

            // 初始化备注UI处理器（需要在 InitializeComponent 之后，因为需要 RightPanel 和 FileBrowser）
            _fileNotesUIHandler = new Services.FileNotes.FileNotesUIHandler(RightPanel, FileBrowser);

            // 初始化标签UI处理器（需要创建上下文）
            var tagUIHandlerContext = new TagUIHandlerContextImpl(this);
            _tagUIHandler = new Services.Tag.TagUIHandler(tagUIHandlerContext);

            // 初始化预览服务（需要在 InitializeComponent 之后，因为需要 RightPanel 和 FileBrowser）
            _previewService = new Services.Preview.PreviewService(
                RightPanel,
                FileBrowser,
                this.Dispatcher,
                LoadCurrentDirectory,
                path => CreateTab(path, true)
            );

            // 订阅 RightPanel 事件
            if (RightPanel != null)
            {
                RightPanel.NotesTextChanged += NotesTextBox_TextChanged;
                RightPanel.NotesAutoSaved += NotesAutoSaved_Handler;
                RightPanel.PreviewOpenFileRequested += RightPanel_PreviewOpenFileRequested;
                RightPanel.PreviewMiddleClickRequested += RightPanel_PreviewMiddleClickRequested;
            }
            
            // 订阅 NavigationService 事件
            _navigationService.NavigateRequested += OnNavigationServiceNavigateRequested;
            
            // 订阅标签页服务事件
            _tabService.TabAdded += (s, tab) => { /* UI 已通过 CreateTabInternal 处理 */ };
            _tabService.TabRemoved += (s, tab) => { /* UI 已通过 CloseTab 处理 */ };
            _tabService.ActiveTabChanged += (s, tab) => 
            {
                if (tab != null)
                {
                    UpdateTabStyles();
                }
            };
            _tabService.TabPinStateChanged += (s, tab) =>
            {
                _tabService.ApplyPinVisual(tab);
                _tabService.ReorderTabs();
            };
            _tabService.TabTitleChanged += (s, tab) =>
            {
                _tabService.ApplyPinVisual(tab);
            };
            
            // 订阅 FileListService 事件
            // _fileListService.FilesLoaded += OnFileListServiceFilesLoaded; // 已改为直接在 LoadFilesAsync 中处理
            _fileListService.FolderSizeCalculated += OnFileListServiceFolderSizeCalculated;
            _fileListService.MetadataEnriched += OnFileListServiceMetadataEnriched;
            _fileListService.ErrorOccurred += OnFileListServiceErrorOccurred;
            
            // 订阅 FileSystemWatcherService 事件
            _fileSystemWatcherService.FileSystemChanged += OnFileSystemWatcherServiceFileSystemChanged;
            _fileSystemWatcherService.RefreshRequested += OnFileSystemWatcherServiceRefreshRequested;
            
            // 订阅库服务事件
            _libraryService.LibrariesLoaded += (s, libraries) =>
            {
                var currentSelected = LibrariesListBox?.SelectedItem;
                LibrariesListBox.ItemsSource = null;
                LibrariesListBox.ItemsSource = libraries;
                LibrariesListBox.Items.Refresh();
                
                if (currentSelected != null)
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _uiHelperService?.EnsureSelectedItemVisible(LibrariesListBox, currentSelected);
                        HighlightMatchingLibrary(currentSelected as Library);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            };
            
            // InitializeApplication moved to App.xaml.cs to avoid double initialization
            // try 
            // {
            //    new MainWindowInitializer(this).InitializeApplication();
            // }
            // catch (Exception ex)
            // {
            //    System.Diagnostics.Debug.WriteLine($"InitializeApplication failed: {ex.Message}");
            // }
            
            _libraryService.LibraryFilesLoaded += (s, e) =>
            {
                _isLoadingFiles = false;
                _loadFilesSemaphore.Release();
                
                if (e.IsEmpty)
                {
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = null;
                        FileBrowser.AddressText = e.Library.Name + " (无位置)";
                    }
                    ShowEmptyLibraryMessage(e.Library.Name);
                    ClearPreviewAndInfo();
                    ClearItemHighlights();
                    ClearTabsInLibraryMode();
                }
                else
                {
                    ShowMergedLibraryFiles(e.Files, e.Library);
                }
            };
            
            _libraryService.LibraryHighlightRequested += (s, library) =>
            {
                HighlightMatchingLibrary(library);
            };
            
            // 订阅收藏服务事件
            _favoriteService.NavigateRequested += (s, path) =>
            {
                _navigationService.LastLeftNavSource = "Favorites";
                _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.Favorite, NavigationCoordinator.ClickType.LeftClick);
            };
            
            _favoriteService.FileOpenRequested += (s, filePath) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            _favoriteService.CreateTabRequested += (s, path) =>
            {
                CreateTab(path, true);
            };
            
            _favoriteService.FavoritesLoaded += (s, e) =>
            {
                // 收藏列表已加载，UI已更新
            };
            
            // 订阅快速访问服务事件
            _quickAccessService.NavigateRequested += (s, path) =>
            {
                _navigationService.LastLeftNavSource = "QuickAccess";
                _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.QuickAccess, NavigationCoordinator.ClickType.LeftClick);
            };
            
            _quickAccessService.CreateTabRequested += (s, path) =>
            {
                CreateTab(path, true);
            };
            
            _navigationCoordinator.PathNavigateRequested += (path, forceNewTab) =>
            {
                if (forceNewTab)
                    CreateTab(path, true);
                else
                    NavigateToPath(path);
            };
            _navigationCoordinator.LibraryNavigateRequested += (library, forceNewTab) =>
            {
                OpenLibraryInTab(library, forceNewTab);
            };
            _navigationCoordinator.TagNavigateRequested += (tag, forceNewTab) =>
            {
                OpenTagInTab(tag, forceNewTab);
            };
            _navigationCoordinator.FileOpenRequested += (filePath) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            _navigationCoordinator.FavoritePathNotFound += (favorite) =>
            {
                var result = MessageBox.Show(
                    $"路径不存在: {favorite.Path}\n\n是否从收藏中移除？",
                    "提示",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    DatabaseManager.RemoveFavorite(favorite.Path);
                    _favoriteService.LoadFavorites(FavoritesListBox);
                }
            };
            
            // 为库列表添加鼠标事件处理，检测鼠标中键和Ctrl键
            if (NavigationPanelControl?.LibrariesListBoxControl != null)
            {
                NavigationPanelControl.LibrariesListBoxControl.PreviewMouseDown += LibrariesListBox_PreviewMouseDown;
            }
            
            // 订阅NavigationPanelControl的事件
            if (NavigationPanelControl != null)
            {
                NavigationPanelControl.LibrariesListBoxPreviewMouseDown += LibrariesListBox_PreviewMouseDown;
                NavigationPanelControl.DrivesListBoxPreviewMouseDown += DrivesListBox_PreviewMouseDown;
                NavigationPanelControl.QuickAccessListBoxPreviewMouseDown += QuickAccessListBox_PreviewMouseDown;
                NavigationPanelControl.FavoritesListBoxPreviewMouseDown += FavoritesListBox_PreviewMouseDown;
                NavigationPanelControl.LibrariesListBoxSelectionChanged += LibrariesListBox_SelectionChanged;
                NavigationPanelControl.LibrariesListBoxContextMenuOpening += LibrariesListBox_ContextMenuOpening;
                NavigationPanelControl.AddFavoriteClick += AddFavorite_Click;
                NavigationPanelControl.AddTagToFileClick += AddTagToFile_Click;
                NavigationPanelControl.LibraryManageClick += ManageLibraries_Click;
                // NavigationPanelControl.LibraryRefreshClick += LibraryRefresh_Click;
                NavigationPanelControl.TagClickModeClick += TagClickModeBtn_Click;
                NavigationPanelControl.TagCategoryManageClick += TagCategoryManageBtn_Click;
                NavigationPanelControl.TagBrowsePanelTagClicked += TagBrowsePanel_TagClicked;
                NavigationPanelControl.TagEditPanelTagClicked += TagEditPanel_TagClicked;
            }
            
            // 使用 MainWindowInitializer 进行初始化
            var initializer = new Services.MainWindowInitializer(this);
            initializer.InitializeApplication();
            this.Activated += (s, e) =>
            {
                var activeTab = _tabService.ActiveTab;
                if (activeTab != null && activeTab.Path != null && activeTab.Path.StartsWith("search://"))
                {
                    CheckAndRefreshSearchTab(activeTab.Path);
                }
            };
            
            InitializeHandlers();
        }



        private void AttachTabServiceUiContext()
        {
            if (_tabService == null) return;
            var context = new TabUiContext
            {
                FileBrowser = FileBrowser,

                Dispatcher = this.Dispatcher,
                OwnerWindow = this,
                GetConfig = () => _configService?.Config ?? new AppConfig(),
                SaveConfig = ConfigManager.Save,
                GetCurrentLibrary = () => _currentLibrary,
                SetCurrentLibrary = lib => _currentLibrary = lib,
                GetCurrentPath = () => _currentPath,
                SetCurrentPath = path => _currentPath = path,
                SetNavigationCurrentPath = path => _navigationService.CurrentPath = path,
                GetCurrentTagFilter = () => _currentTagFilter,
                SetCurrentTagFilter = tag => _currentTagFilter = tag,
                FilterByTag = FilterByTag,
                LoadLibraryFiles = lib => LoadLibraryFiles(lib),
                NavigateToPathInternal = NavigateToPathInternal,
                UpdateNavigationButtonsState = UpdateNavigationButtonsState,

                SearchService = _searchService,
                GetSearchCacheService = () => _searchCacheService,
                GetSearchOptions = () => _searchOptions,
                GetCurrentFiles = () => _currentFiles,
                SetCurrentFiles = files => _currentFiles = files,
                ClearFilter = ClearFilter,
                RefreshSearchTab = path => { CheckAndRefreshSearchTab(path); return Task.CompletedTask; },
                FindResource = key => FindResource(key),
                IsTagTrainAvailable = () => App.IsTagTrainAvailable
            };
            _tabService.AttachUiContext(context);
        }

        internal void GridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            _fileBrowserEventHandler?.FileBrowser_GridSplitterDragDelta(sender, e);
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[GridSplitter] DragCompleted 触发，开始保存");
            // 拖拽结束后，立即保存（不延迟）
            if (_windowStateManager != null && this.IsLoaded)
            {
                // 强制更新布局
                RootGrid?.UpdateLayout();
                System.Diagnostics.Debug.WriteLine("[GridSplitter] 布局已更新，保存状态");
                    _windowStateManager.SaveAllState();
            }
        }

        #region 拖拽功能

        internal void InitializeDragDrop()
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
        
        internal void InitializeLibraryDragDrop()
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
                        LoadLibraries();
                        
                        // 选中新创建的库
                        _uiHelperService?.EnsureSelectedItemVisible(LibrariesListBox, targetLibrary);
                    }
                    else if (libraryId < 0)
                    {
                        // 库已存在（虽然理论上不应该发生，因为我们已经检查过）
                        LoadLibraries();
                        var existingLibrary = DatabaseManager.GetAllLibraries()
                            .FirstOrDefault(l => l.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase));
                        if (existingLibrary != null)
                        {
                            targetLibrary = existingLibrary;
                            _uiHelperService?.EnsureSelectedItemVisible(LibrariesListBox, targetLibrary);
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
                        LoadLibraryFiles(_currentLibrary);
                    }
                    
                    // 刷新库列表
                    LoadLibraries();
                    
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

        internal void NavigateBack_Click(object sender, RoutedEventArgs e)
        {
            string path = _navigationService.NavigateBack();
            if (!string.IsNullOrEmpty(path))
            {
                _currentPath = path;
                ClearFilter();
                LoadCurrentDirectory();
                UpdateNavigationButtonsState();
            }
        }

        private void NavigateForward_Click(object sender, RoutedEventArgs e)
        {
            string path = _navigationService.NavigateForward();
            if (!string.IsNullOrEmpty(path))
            {
                _currentPath = path;
                ClearFilter();
                LoadCurrentDirectory();
                UpdateNavigationButtonsState();
            }
        }

        private void NavigateUp_Click(object sender, RoutedEventArgs e)
        {
            string parentPath = _navigationService.NavigateUp();
            if (!string.IsNullOrEmpty(parentPath))
            {
                NavigateToPath(parentPath);
            }
        }
        
        /// <summary>
        /// NavigationService 导航请求事件处理
        /// </summary>
        private void OnNavigationServiceNavigateRequested(object sender, string path)
        {
            // 导航服务已更新 CurrentPath，这里只需要同步 _currentPath
            _currentPath = path;
            UpdateNavigationButtonsState();
        }
        
        /// <summary>
        /// 更新导航按钮状态
        /// </summary>
        internal void UpdateNavigationButtonsState()
        {
            // 使用 NavigationModeService 更新导航按钮状态
            if (_navigationModeService != null)
            {
                _navigationModeService.UpdateNavigationButtonsState();
            }
        }

        // FileBrowser 事件桥接方法 - 已迁移到 FileBrowserEventHandler
        private void FileBrowser_PathChanged(object sender, string path) => _fileBrowserEventHandler?.FileBrowser_PathChanged(sender, path);
        private void FileBrowser_BreadcrumbMiddleClicked(object sender, string path) => _fileBrowserEventHandler?.FileBrowser_BreadcrumbMiddleClicked(sender, path);
        private void FileBrowser_BreadcrumbClicked(object sender, string path) => _fileBrowserEventHandler?.FileBrowser_BreadcrumbClicked(sender, path);
        private void FileBrowser_FilesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 如果事件处理器未初始化，直接调用选择变化处理方法
            if (_fileBrowserEventHandler != null)
            {
                _fileBrowserEventHandler.FileBrowser_FilesSelectionChanged(sender, e);
            }
            else
            {
                // 直接处理选择变化，确保预览、备注、文件信息功能可用
                FilesListView_SelectionChanged(sender, e);
            }
        }
        private void FileBrowser_FilesMouseDoubleClick(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesMouseDoubleClick(sender, e);
        private void FileBrowser_FilesPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewMouseDoubleClick(sender, e);
        private void FileBrowser_FilesPreviewKeyDown(object sender, KeyEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewKeyDown(sender, e);
        private void FileBrowser_FilesPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewMouseLeftButtonDown(sender, e);
        private void FileBrowser_FilesMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesMouseLeftButtonUp(sender, e);
        private void FileBrowser_FilesPreviewMouseDown(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewMouseDown(sender, e);
        private void FileBrowser_GridViewColumnHeaderClick(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_GridViewColumnHeaderClick(sender, e);
        private void FileBrowser_FilesSizeChanged(object sender, SizeChangedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesSizeChanged(sender, e);
        private void FileBrowser_FilesPreviewMouseDoubleClickForBlank(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewMouseDoubleClickForBlank(sender, e);
        private void FileBrowser_SearchClicked(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_SearchClicked(sender, e);
        private void FileBrowser_FilterClicked(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilterClicked(sender, e);
        private void FileBrowser_LoadMoreClicked(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_LoadMoreClicked(sender, e);
        private void FileBrowser_Loaded(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_Loaded(sender, e);
        
        // 菜单事件桥接方法 - 已迁移到 MenuEventHandler
        internal void Refresh_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Refresh_Click(sender, e);
        private void ClearFilter_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ClearFilter_Click(sender, e);

        internal void ClearFilter()
        {
            // 清除过滤状态，恢复正常的文件浏览
            _currentTagFilter = null;
            // TagsListBox已移除，标签选择现在由TagTrain面板处理
            
            _currentFiles.Clear();
            if (FileBrowser != null)
                FileBrowser.FilesItemsSource = null;
            HideEmptyStateMessage();
        }

        internal void FilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var rawSelectedItem = FileBrowser?.FilesSelectedItem;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] FilesListView_SelectionChanged: Raw SelectedItem: {rawSelectedItem}, Type: {rawSelectedItem?.GetType().Name}");

            if (rawSelectedItem is FileSystemItem selectedItem)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] FilesListView_SelectionChanged: Selected {selectedItem.Name}");
                _fileInfoService?.ShowFileInfo(selectedItem);
                LoadFilePreview(selectedItem);
                _fileNotesUIHandler?.LoadFileNotes(selectedItem);
                
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
        

        
        private void ShowEmptyLibraryMessage(string libraryName)
        {
            if (FileBrowser != null)
            {
                FileBrowser.ShowEmptyState($"库 \"{libraryName}\" 没有添加任何位置。\n\n请在管理库中添加位置。");
            }
        }
        
        internal void HideEmptyStateMessage()
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

        internal void FilesListView_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
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
        
        internal void FilesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
                            // 如果是库模式，切换到路径模式并导航
                            if (_currentLibrary != null)
                            {
                                // 切换到路径模式
                                _currentLibrary = null;
                                SwitchNavigationMode("Path");
                            }
                            
                            // 使用统一导航协调器处理文件列表双击导航
                            var clickType = NavigationCoordinator.GetClickType(e);
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, clickType);
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
        
        internal void FilesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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
        
        internal void FilesListView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 只处理中键点击（打开新标签页），Ctrl+左键用于多选，不在这里处理
            if (e.ChangedButton != MouseButton.Middle) return;

            // 如果 sender 为 null，尝试从 FileBrowser 获取 ListView
            var listView = sender as ListView;
            if (listView == null)
            {
                listView = FileBrowser?.FilesList;
            }
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
                            // 中键点击：在新标签页打开文件夹
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.MiddleClick);
                            e.Handled = true;
                            return;
                        }
                    }
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }
        /// <summary>
        /// 从ListBoxItem中提取路径
        /// </summary>
        private string ExtractPathFromListBoxItem(ListBox listBox, System.Windows.Point position)
        {
            var hitResult = VisualTreeHelper.HitTest(listBox, position);
            if (hitResult == null) return null;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext != null)
                {
                    var pathProperty = item.DataContext.GetType().GetProperty("Path");
                    if (pathProperty != null)
                    {
                        return pathProperty.GetValue(item.DataContext) as string;
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        /// <summary>
        /// 从ListBoxItem中提取Favorite对象
        /// </summary>
        private Favorite ExtractFavoriteFromListBoxItem(ListBox listBox, System.Windows.Point position)
        {
            var hitResult = VisualTreeHelper.HitTest(listBox, position);
            if (hitResult == null) return null;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext != null)
                {
                    var favoriteProperty = item.DataContext.GetType().GetProperty("Favorite");
                    if (favoriteProperty != null)
                    {
                        return favoriteProperty.GetValue(item.DataContext) as Favorite;
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void DrivesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == NavigationCoordinator.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var path = ExtractPathFromListBoxItem(listBox, e.GetPosition(listBox));
            if (!string.IsNullOrEmpty(path))
            {
                _navigationService.LastLeftNavSource = "Drive";
                _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.Drive, clickType);
                e.Handled = true;
            }
        }

        private void QuickAccessListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == NavigationCoordinator.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var path = ExtractPathFromListBoxItem(listBox, e.GetPosition(listBox));
            if (!string.IsNullOrEmpty(path))
            {
                _navigationService.LastLeftNavSource = "QuickAccess";
                _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.QuickAccess, clickType);
                e.Handled = true;
            }
        }

        private void FavoritesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == NavigationCoordinator.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var favorite = ExtractFavoriteFromListBoxItem(listBox, e.GetPosition(listBox));
            if (favorite != null)
            {
                _navigationService.LastLeftNavSource = "Favorites";
                _navigationCoordinator.HandleFavoriteNavigation(favorite, clickType);
                e.Handled = true;
            }
        }

        internal void FilesListView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
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
                        HighlightMatchingLibrary(_currentLibrary);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            
            _isMouseDownOnListView = false;
            _isMouseDownOnColumnHeader = false; // 清除列头标志
        }
        

        
        // 键盘事件桥接方法 - 已迁移到 KeyboardEventHandler
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e) => _keyboardEventHandler?.MainWindow_PreviewKeyDown(sender, e);
        private void MainWindow_KeyDown(object sender, KeyEventArgs e) => _keyboardEventHandler?.MainWindow_KeyDown(sender, e);





        #endregion



        #region 备注功能

        internal void NotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _fileNotesUIHandler?.NotesTextBox_TextChanged(sender, e);
        }
        
        internal async void NotesAutoSaved_Handler(object sender, RoutedEventArgs e)
        {
            _fileNotesUIHandler?.NotesAutoSaved_Handler(sender, e);
        }

        #endregion

        #region 库功能

        internal void LoadLibraries()
        {
            _libraryService.LoadLibraries();
        }

        // 菜单事件桥接方法 - 已迁移到 MenuEventHandler
        private void AddLibrary_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.AddLibrary_Click(sender, e);
        private void ManageLibraries_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ManageLibraries_Click(sender, e);

        private void LibrariesListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // 根据是否有选中项来启用/禁用菜单项
            bool hasSelection = LibrariesListBox.SelectedItem != null;
            
            if (LibraryContextMenu != null)
            {
                var renameItem = LibraryContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "LibraryRenameMenuItem");
                var deleteItem = LibraryContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "LibraryDeleteMenuItem");
                var manageItem = LibraryContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "LibraryManageMenuItem");
                var openItem = LibraryContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "LibraryOpenInExplorerMenuItem");
                
                if (renameItem != null) renameItem.IsEnabled = hasSelection;
                if (deleteItem != null) deleteItem.IsEnabled = hasSelection;
                if (manageItem != null) manageItem.IsEnabled = hasSelection;
                if (openItem != null) openItem.IsEnabled = hasSelection;
            }
        }

        // 库管理事件桥接方法 - 已迁移到 MenuEventHandler
        private void LibraryRename_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryRename_Click(sender, e);
        private void LibraryDelete_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryDelete_Click(sender, e);
        private void LibraryManage_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryManage_Click(sender, e);
        private void LibraryOpenInExplorer_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryOpenInExplorer_Click(sender, e);
        private void LibraryRefresh_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryRefresh_Click(sender, e);

        private void LibrariesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == NavigationCoordinator.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var hitResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext is Library library)
                {
                    e.Handled = true;
                    var updatedLibrary = DatabaseManager.GetLibrary(library.Id);
                    if (updatedLibrary != null)
                    {
                        _navigationCoordinator.HandleLibraryNavigation(updatedLibrary, clickType);
                    }
                    return;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        private void LibrariesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                // 重新从数据库加载库信息，确保路径信息是最新的
                var updatedLibrary = DatabaseManager.GetLibrary(selectedLibrary.Id);
                if (updatedLibrary != null)
                {
                    // 使用统一导航协调器处理库导航（左键点击）
                    _navigationCoordinator.HandleLibraryNavigation(updatedLibrary, NavigationCoordinator.ClickType.LeftClick);
                    
                    // 高亮当前选中的库（作为匹配当前库）- 在加载文件后执行，确保库列表已更新
                    HighlightMatchingLibrary(updatedLibrary);
                }
                else
                {
                    _currentLibrary = null;
                    if (_configService != null)
                    {
                        _configService.Config.LastLibraryId = 0;
                        _configService.SaveCurrentConfig();
                    }
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                    if (FileBrowser != null)
                        FileBrowser.AddressText = "";
                }
            }
            else
            {
                _currentLibrary = null;
                if (_configService != null)
                {
                    _configService.Config.LastLibraryId = 0;
                    _configService.SaveCurrentConfig();
                }
                _currentFiles.Clear();
                if (FileBrowser != null)
                {
                    FileBrowser.FilesItemsSource = null;
                    FileBrowser.AddressText = "";
                }
                
                // 清除所有库的高亮
                _navigationService.ClearItemHighlights();
            }
        }

        internal void LoadLibraryFiles(Library library)
        {
            // 使用信号量防止重复加载
            if (!_loadFilesSemaphore.Wait(0))
            {
                System.Diagnostics.Debug.WriteLine("LoadLibraryFiles: 已有加载任务在进行，跳过此次调用");
                return;
            }
            
            try
            {
                // 设置加载标志
                _isLoadingFiles = true;
                
                _currentFiles.Clear();
                _currentPath = null; // 标记当前在库模式下
                if (FileBrowser != null) FileBrowser.NavUpEnabled = false;
                
                // 使用库服务加载文件
                _libraryService.LoadLibraryFiles(library, 
                    (path) => DatabaseManager.GetFolderSize(path),
                    (bytes) => _fileListService.FormatFileSize(bytes));
            }
            catch (Exception ex)
            {
                // 确保释放锁
                _isLoadingFiles = false;
                _loadFilesSemaphore.Release();
                MessageBox.Show($"加载库文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示合并的库文件（所有路径的文件合并显示）
        /// </summary>
        private void ShowMergedLibraryFiles(List<FileSystemItem> items, Library library)
        {
            if (library == null) return;
            
            _currentFiles.Clear();
            _currentFiles.AddRange(items ?? new List<FileSystemItem>());
            
            System.Diagnostics.Debug.WriteLine($"[加载库文件] 合并后共有 {_currentFiles.Count} 项");

            // 应用排序
            if (_columnService != null)
            {
                _currentFiles = _columnService.SortFiles(_currentFiles);
            }

            System.Diagnostics.Debug.WriteLine($"[加载库文件] 设置 ItemsSource，文件数量: {_currentFiles.Count}");
            
            // 确保UI控件存在
            if (FileBrowser != null)
            {
                FileBrowser.FilesItemsSource = null; // 先清空
                FileBrowser.FilesItemsSource = _currentFiles; // 再设置
                FileBrowser.FilesList?.Items.Refresh(); // 强制刷新
            }

            // 高亮当前库（作为匹配当前库）
            HighlightMatchingLibrary(library);

            // 如果文件列表为空，显示空状态提示；否则隐藏
            if (_currentFiles.Count == 0)
            {
                ShowEmptyStateMessage($"库 \"{library.Name}\" 中没有文件或文件夹");
            }
            else
            {
                HideEmptyStateMessage();
            }

            // 更新地址栏显示库名称
            if (FileBrowser != null)
            {
                FileBrowser.AddressText = library.Name;
                FileBrowser.IsAddressReadOnly = true;
                FileBrowser.SetLibraryBreadcrumb(library.Name);
            }
            
            System.Diagnostics.Debug.WriteLine($"[加载库文件] 完成，ItemsSource 已设置");

            // 取消之前的文件夹大小计算任务
            _folderSizeCalculationService?.Cancel();

            // 异步加载标签和备注（延迟加载，避免阻塞UI）
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // 批量加载标签和备注（限制并发，减少到2个避免CPU占用过高）
                    var semaphore = new System.Threading.SemaphoreSlim(2, 2); // 最多2个并发查询
                    var tasks = _currentFiles.Select(async item =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
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
                            
                            var notes = FileNotesService.GetFileNotes(item.Path);
                            if (notes != null && notes.Length > 0)
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
                        System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
                    }
                    catch { }
                    
                    // 批量更新UI（减少刷新次数）
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                        collectionView?.Refresh();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                catch { }
            });

            // 异步计算文件夹大小（使用服务方法，限制数量和延迟，避免资源消耗过大）
            var dirsToCalculate = _currentFiles.Where(f => f.IsDirectory).ToList();
            // 只计算前5个文件夹，大幅减少资源消耗
            int maxCalculations = Math.Min(5, dirsToCalculate.Count);
            
            // 立即计算前5个文件夹
            for (int i = 0; i < maxCalculations; i++)
            {
                var dir = dirsToCalculate[i];
                var path = dir.Path;
                var currentDelay = i * 1000; // 每个任务延迟1秒，避免同时启动
                
                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // 延迟启动，避免同时启动太多任务
                        if (currentDelay > 0)
                        {
                            await System.Threading.Tasks.Task.Delay(currentDelay);
                        }
                        
                        // 使用服务方法计算文件夹大小
                        await _folderSizeCalculationService.CalculateAndUpdateFolderSizeAsync(path);
                        
                        // 更新UI
                        _ = Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var item = _currentFiles.FirstOrDefault(f => f.Path == path);
                            if (item != null)
                            {
                                var cachedSize = DatabaseManager.GetFolderSize(path);
                                if (cachedSize.HasValue)
                                {
                                    item.Size = _fileListService.FormatFileSize(cachedSize.Value);
                                    // 使用低优先级批量更新，减少UI刷新频率
                                    var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                                    collectionView?.Refresh();
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.SystemIdle);
                    }
                    catch { }
                });
            }
            
            // 剩余文件夹通过服务的批量计算方法异步处理
            if (dirsToCalculate.Count > maxCalculations)
            {
                var remainingPaths = dirsToCalculate.Skip(maxCalculations).Select(d => d.Path).ToArray();
                _folderSizeCalculationService?.CalculateSubfolderSizesBatchAsync(remainingPaths);
            }
        }

        /// <summary>
        /// 立即计算指定文件夹的大小（用户选中时触发）
        /// </summary>
        private void CalculateFolderSizeImmediately(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;
            
            // 取消之前的计算任务
            if (_currentFolderSizeCTS != null)
            {
                _currentFolderSizeCTS.Cancel();
                _currentFolderSizeCTS.Dispose();
            }
            _currentFolderSizeCTS = new System.Threading.CancellationTokenSource();
            var token = _currentFolderSizeCTS.Token;

            // 先更新UI显示"计算中..."，给用户即时反馈
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (token.IsCancellationRequested) return;

                var item = _currentFiles.FirstOrDefault(f => f.Path == folderPath);
                if (item != null && (string.IsNullOrEmpty(item.Size) || item.Size == "-" || item.Size == "计算中..."))
                {
                    item.Size = "计算中...";
                    // 仅刷新该项，避免刷新整个列表导致闪烁（如果有机制的话，这里还是刷新全部）
                    // 优化：如果可能，只更新单个Item的UI，但Binding通常需要NotifyPropertyChanged
                    // 这里FileSystemItem实现了INotifyPropertyChanged吗？假设它绑定机制有效。
                    // 刷新CollectionView虽然开销大，但为了确保"计算中..."立即显示
                    // var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                    // collectionView?.Refresh();
                    
                    // 使用 FileListService 计算文件夹大小
                    // 计算完成后会通过 FolderSizeCalculated 事件更新UI
                    _ = _fileListService.CalculateFolderSizeAsync(item, token);
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        #endregion

        #region 标签功能

        internal void LoadTags()
        {
            // TagsListBox已移除，标签加载现在由TagTrain面板处理
            // 调用TagTrain面板的初始化
            InitializeTagTrainPanel();
        }
        
        // 初始化TagTrain训练面板
        private void InitializeTagTrainPanel()
        {
            if (!App.IsTagTrainAvailable)
            {
                System.Diagnostics.Debug.WriteLine("InitializeTagTrainPanel: TagTrain 不可用");
                return;
            }
            
            try
            {
                // 初始化浏览模式的TagPanel
                // 注意：TagClicked事件已通过NavigationPanelControl订阅，这里不再重复订阅
                if (TagBrowsePanel != null)
                {
                    TagBrowsePanel.Mode = TagTrain.UI.TagPanel.DisplayMode.Browse;
                    // TagBrowsePanel.TagClicked += TagBrowsePanel_TagClicked; // 已通过NavigationPanelControl订阅，避免重复
                    TagBrowsePanel.CategoryManagementRequested += TagBrowsePanel_CategoryManagementRequested;
                    TagBrowsePanel.LoadExistingTags();
                }
                
                // 初始化编辑模式的TagPanel
                // 注意：TagClicked事件已通过NavigationPanelControl订阅，这里不再重复订阅
                if (TagEditPanel != null)
                {
                    TagEditPanel.Mode = TagTrain.UI.TagPanel.DisplayMode.Edit;
                    // TagEditPanel.TagClicked += TagEditPanel_TagClicked; // 已通过NavigationPanelControl订阅，避免重复
                    TagEditPanel.CategoryManagementRequested += TagEditPanel_CategoryManagementRequested;
                    // 编辑模式的初始化由SwitchTagMode处理
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitializeTagTrainPanel: 初始化失败: {ex.Message}");
            }
        }
        
        // 浏览模式：标签点击事件 - 打开标签对应的文件
        private void TagBrowsePanel_TagClicked(string tagName, bool forceNewTab)
        {
            try
            {
                // 通过标签名称获取标签ID，确保能正确识别已存在的标签页
                int tagId = OoiMRRIntegration.GetOrCreateTagId(tagName);
                if (tagId > 0)
                {
                    var tag = new Tag { Id = tagId, Name = tagName };
                    var clickType = forceNewTab ? NavigationCoordinator.ClickType.MiddleClick : NavigationCoordinator.ClickType.LeftClick;
                    _navigationCoordinator.HandleTagNavigation(tag, clickType);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TagBrowsePanel_TagClicked error: {ex.Message}");
            }
        }
        
        // 浏览模式：打开分组管理
        private void TagBrowsePanel_CategoryManagementRequested()
        {
            _tagTrainEventHandler?.OpenCategoryManagement();
        }
        
        // 编辑模式：标签点击事件 - 应用标签（需要实现TagTrain的功能）
        private void TagEditPanel_TagClicked(string tagName, bool forceNewTab)
        {
            // TODO: 实现编辑模式的标签点击功能（应用标签到当前图片）
            System.Diagnostics.Debug.WriteLine($"TagEditPanel_TagClicked: {tagName}, forceNewTab: {forceNewTab}");
        }
        
        // 编辑模式：打开分组管理
        private void TagEditPanel_CategoryManagementRequested()
        {
            _tagTrainEventHandler?.OpenCategoryManagement();
        }
        
        // 更新TagTrain模型状态
        internal void UpdateTagTrainModelStatus()
        {
            // 模型状态现在由TagPanel内部管理，此方法已废弃
            // TagEditPanel会自动更新模型状态
                return;
            }
            
        // 加载TagTrain已有标签列表
        internal void LoadTagTrainExistingTags()
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
                EditTagName(tagInfo.Id, tagName);
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
                        _tagTrainEventHandler?.OpenCategoryManagement();
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
                        _tagTrainEventHandler?.OpenCategoryManagement();
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
                DeleteTagById(tagInfo.Id, tagName);
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
            if (_tagClickMode == TagClickMode.Browse)
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

        internal void UpdateTagFilesUI(Tag tag, List<FileSystemItem> tagFiles)
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

        internal void FilterByTag(Tag tag)
        {
            _tagUIHandler?.FilterByTag(tag);
        }

        private void NewTag_Click(object sender, RoutedEventArgs e)
        {
            _tagUIHandler?.NewTag_Click(sender, e);
        }

        private void ManageTags_Click(object sender, RoutedEventArgs e)
        {
            _tagUIHandler?.ManageTags_Click(sender, e);
        }

        private void AddTagToFile_Click(object sender, RoutedEventArgs e)
        {
            _tagUIHandler?.AddTagToFile_Click(sender, e);
        }

        private void OpenTagDialogForSelectedItems()
        {
            _tagUIHandler?.OpenTagDialogForSelectedItems();
        }


        private void TagsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _tagUIHandler?.TagsListBox_SelectionChanged(sender, e);
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



        #region 快速访问

        internal void LoadQuickAccess()
        {
            if (QuickAccessListBox == null) return;
            _quickAccessService.LoadQuickAccess(QuickAccessListBox);
        }
        
        private void QuickAccessListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 事件处理已由QuickAccessService内部处理，此方法保留以兼容现有代码
        }

        #endregion

        #region 驱动器功能

        internal void LoadDrives()
        {
            if (DrivesListBox == null) return;
            _quickAccessService.LoadDrives(DrivesListBox, _fileListService.FormatFileSize);
        }
        
        private void DrivesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 事件处理已由QuickAccessService内部处理，此方法保留以兼容现有代码
        }

        #endregion

        #region 菜单事件

        // 文件操作事件桥接方法 - 已迁移到 MenuEventHandler
        internal void NewFolder_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.NewFolder_Click(sender, e);
        private void NewFile_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.NewFile_Click(sender, e);

        internal void CreateNewFileWithExtension(string extension)
        {
            try
            {
                string targetPath = null;
                
                // 判断当前模式：库模式还是路径模式
                if (_currentLibrary != null)
                {
                    // 库模式：使用库的第一个位置
                    if (_currentLibrary.Paths == null || _currentLibrary.Paths.Count == 0)
                    {
                        MessageBox.Show("当前库没有添加任何位置，请先在管理库中添加位置", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    
                    // 如果有多个位置，让用户选择
                    if (_currentLibrary.Paths.Count > 1)
                    {
                        var paths = string.Join("\n", _currentLibrary.Paths.Select((p, i) => $"{i + 1}. {p}"));
                        var result = MessageBox.Show(
                            $"当前库有多个位置，将在第一个位置创建文件：\n\n{_currentLibrary.Paths[0]}\n\n是否继续？\n\n所有位置：\n{paths}",
                            "选择位置",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        
                        if (result != MessageBoxResult.Yes)
                        {
                            return;
                        }
                    }
                    
                    targetPath = _currentLibrary.Paths[0];
                    if (!Directory.Exists(targetPath))
                    {
                        MessageBox.Show($"库位置不存在: {targetPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
                else if (!string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath))
                {
                    // 路径模式：使用当前路径
                    targetPath = _currentPath;
                }
                else
                {
                    MessageBox.Show("当前没有可用的路径", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 根据扩展名生成文件名
                string baseFileName = $"新建文件{extension}";
                string filePath = Path.Combine(targetPath, baseFileName);

                // 如果已存在，自动添加序号
                if (File.Exists(filePath))
                {
                    string fileNameWithoutExt = Path.GetFileNameWithoutExtension(baseFileName);
                    int counter = 2;
                    
                    do
                    {
                        string candidateFileName = $"{fileNameWithoutExt} ({counter}){extension}";
                        filePath = Path.Combine(targetPath, candidateFileName);
                        counter++;
                    }
                    while (File.Exists(filePath));
                }

                // 根据文件类型创建合适的文件内容
                CreateFileWithProperFormat(filePath, extension.ToLower());
                
                // 刷新显示
                RefreshFileList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void CreateFileWithProperFormat(string filePath, string extension)
        {
            switch (extension)
            {
                case ".docx":
                case ".xlsx":
                case ".pptx":
                    CreateOfficeFile(filePath, extension);
                    break;

                case ".html":
                    var htmlLines = new[]
                    {
                        "<!DOCTYPE html>",
                        "<html lang=\"zh-CN\">",
                        "<head>",
                        "    <meta charset=\"UTF-8\">",
                        "    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">",
                        "    <title>新建网页</title>",
                        "</head>",
                        "<body>",
                        "    <h1>Hello World</h1>",
                        "</body>",
                        "</html>",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, htmlLines));
                    break;

                case ".css":
                    var cssLines = new[]
                    {
                        "/* CSS Stylesheet */",
                        string.Empty,
                        "body {",
                        "    margin: 0;",
                        "    padding: 0;",
                        "}",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, cssLines));
                    break;

                case ".js":
                    var jsLines = new[]
                    {
                        "// JavaScript",
                        string.Empty,
                        "console.log('Hello World');",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, jsLines));
                    break;

                case ".cs":
                    var csLines = new[]
                    {
                        "using System;",
                        string.Empty,
                        "namespace MyNamespace",
                        "{",
                        "    class Program",
                        "    {",
                        "        static void Main(string[] args)",
                        "        {",
                        "            Console.WriteLine(\"Hello World\");",
                        "        }",
                        "    }",
                        "}",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, csLines));
                    break;

                case ".py":
                    var pyLines = new[]
                    {
                        "# Python Script",
                        string.Empty,
                        "def main():",
                        "    print('Hello World')",
                        string.Empty,
                        "if __name__ == '__main__':",
                        "    main()",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, pyLines));
                    break;

                case ".java":
                    var className = Path.GetFileNameWithoutExtension(filePath).Replace(" ", "_");
                    var javaLines = new[]
                    {
                        $"public class {className} {{",
                        "    public static void main(String[] args) {",
                        "        System.out.println(\"Hello World\");",
                        "    }",
                        "}",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, javaLines));
                    break;

                case ".json":
                    var jsonLines = new[]
                    {
                        "{",
                        "    \"name\": \"example\",",
                        "    \"version\": \"1.0.0\"",
                        "}",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, jsonLines));
                    break;

                case ".xml":
                    var xmlLines = new[]
                    {
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
                        "<root>",
                        "    <item>Example</item>",
                        "</root>",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, xmlLines));
                    break;

                case ".md":
                    var mdLines = new[]
                    {
                        "# 标题",
                        string.Empty,
                        "这是一个 Markdown 文档。",
                        string.Empty,
                        "## 二级标题",
                        string.Empty,
                        "- 列表项 1",
                        "- 列表项 2",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, mdLines));
                    break;

                case ".ini":
                    var iniLines = new[]
                    {
                        "[Settings]",
                        "Key=Value",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, iniLines));
                    break;

                case ".bat":
                    var batLines = new[]
                    {
                        "@echo off",
                        "echo Hello World",
                        "pause",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, batLines));
                    break;

                case ".ps1":
                    var psLines = new[]
                    {
                        "# PowerShell Script",
                        string.Empty,
                        "Write-Host \"Hello World\"",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, psLines));
                    break;

                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                    CreateImageFile(filePath, extension);
                    break;

                case ".svg":
                    var svgLines = new[]
                    {
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
                        "<svg width=\"500\" height=\"500\" xmlns=\"http://www.w3.org/2000/svg\">",
                        "    <rect width=\"500\" height=\"500\" fill=\"#FFFFFF\"/>",
                        "</svg>",
                        string.Empty
                    };
                    File.WriteAllText(filePath, string.Join(Environment.NewLine, svgLines));
                    break;

                default:
                    File.WriteAllText(filePath, string.Empty);
                    break;
            }
        }

        private void CreateImageFile(string filePath, string extension)
        {
            try
            {
                // 创建一个500x500的空白图片
                using (var bitmap = new Bitmap(500, 500))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        // 填充白色背景
                        graphics.Clear(System.Drawing.Color.White);
                    }

                    // 根据扩展名保存为相应格式
                    switch (extension.ToLower())
                    {
                        case ".png":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                            break;
                        case ".jpg":
                        case ".jpeg":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Jpeg);
                            break;
                        case ".bmp":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp);
                            break;
                        case ".gif":
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Gif);
                            break;
                        default:
                            bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建图片文件失败: {ex.Message}\n将创建空文件", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                File.WriteAllText(filePath, string.Empty);
            }
        }

        private void CreateOfficeFile(string filePath, string extension)
        {
            try
            {
                dynamic app = null;
                dynamic doc = null;

                switch (extension)
                {
                    case ".docx":
                        try
                        {
                            var wordType = Type.GetTypeFromProgID("Word.Application");
                            if (wordType == null)
                            {
                                CreateBasicDocx(filePath);
                                MessageBox.Show("未检测到 Microsoft Word，已创建基本 DOCX 模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                return;
                            }

                            app = Activator.CreateInstance(wordType);
                            app.Visible = false;
                            app.DisplayAlerts = 0;
                            doc = app.Documents.Add();
                            doc.SaveAs2(filePath);
                            doc.Close(false);
                        }
                        catch (Exception ex)
                        {
                            CreateBasicDocx(filePath);
                            MessageBox.Show($"创建文件失败，已回退为基本 DOCX: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit(false);
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
                            }
                        }
                        break;

                    case ".xlsx":
                        try
                        {
                            var excelType = Type.GetTypeFromProgID("Excel.Application");
                            if (excelType == null)
                            {
                                MessageBox.Show("未检测到 Microsoft Excel，将创建空文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                File.WriteAllText(filePath, string.Empty);
                                return;
                            }

                            app = Activator.CreateInstance(excelType);
                            app.Visible = false;
                            app.DisplayAlerts = false;
                            doc = app.Workbooks.Add();
                            doc.SaveAs(filePath);
                            doc.Close(false);
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit();
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
                            }
                        }
                        break;

                    case ".pptx":
                        try
                        {
                            var pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                            if (pptType == null)
                            {
                                MessageBox.Show("未检测到 Microsoft PowerPoint，将创建空文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                                File.WriteAllText(filePath, string.Empty);
                                return;
                            }

                            app = Activator.CreateInstance(pptType);
                            doc = app.Presentations.Add();
                            doc.SaveAs(filePath);
                            doc.Close();
                        }
                        finally
                        {
                            if (app != null)
                            {
                                app.Quit();
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    CreateBasicDocx(filePath);
                }
                else
                {
                    File.WriteAllText(filePath, string.Empty);
                }
                MessageBox.Show($"创建 Office 文件失败: {ex.Message}\n已写入占位文件", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CreateBasicDocx(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                void AddEntry(string entryName, string content)
                {
                    var entry = archive.CreateEntry(entryName);
                    using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                    writer.Write(content);
                }

                AddEntry("[Content_Types].xml",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">
  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>
  <Default Extension=""xml"" ContentType=""application/xml""/>
  <Override PartName=""/word/document.xml"" ContentType=""application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml""/>
  <Override PartName=""/docProps/core.xml"" ContentType=""application/vnd.openxmlformats-package.core-properties+xml""/>
  <Override PartName=""/docProps/app.xml"" ContentType=""application/vnd.openxmlformats-officedocument.extended-properties+xml""/>
</Types>");

                AddEntry("_rels/.rels",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""word/document.xml""/>
  <Relationship Id=""rId2"" Type=""http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties"" Target=""docProps/core.xml""/>
  <Relationship Id=""rId3"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties"" Target=""docProps/app.xml""/>
</Relationships>");

                AddEntry("word/_rels/document.xml.rels",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
</Relationships>");

                AddEntry("word/document.xml",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<w:document xmlns:wpc=""http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas""
 xmlns:mc=""http://schemas.openxmlformats.org/markup-compatibility/2006""
 xmlns:o=""urn:schemas-microsoft-com:office:office""
 xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""
 xmlns:m=""http://schemas.openxmlformats.org/officeDocument/2006/math""
 xmlns:v=""urn:schemas-microsoft-com:vml""
 xmlns:wp14=""http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing""
 xmlns:wp=""http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing""
 xmlns:w10=""urn:schemas-microsoft-com:office:word""
 xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""
 xmlns:w14=""http://schemas.microsoft.com/office/word/2010/wordml""
 xmlns:w15=""http://schemas.microsoft.com/office/word/2012/wordml""
 mc:Ignorable=""w14 w15 wp14"">
  <w:body>
    <w:p>
      <w:r>
        <w:t>Hello World</w:t>
      </w:r>
    </w:p>
  </w:body>
</w:document>");

                AddEntry("docProps/core.xml",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<cp:coreProperties xmlns:cp=""http://schemas.openxmlformats.org/package/2006/core-properties""
 xmlns:dc=""http://purl.org/dc/elements/1.1/""
 xmlns:dcterms=""http://purl.org/dc/terms/""
 xmlns:dcmitype=""http://purl.org/dc/dcmitype/""
 xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <dc:title>New Document</dc:title>
  <dc:creator>OoiMRR</dc:creator>
  <cp:lastModifiedBy>OoiMRR</cp:lastModifiedBy>
  <dcterms:created xsi:type=""dcterms:W3CDTF"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:created>
  <dcterms:modified xsi:type=""dcterms:W3CDTF"">{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</dcterms:modified>
</cp:coreProperties>");

                AddEntry("docProps/app.xml",
                    @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Properties xmlns=""http://schemas.openxmlformats.org/officeDocument/2006/extended-properties""
 xmlns:vt=""http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes"">
  <Application>OoiMRR</Application>
</Properties>");
            }
        }




        #endregion

        #region 新增按钮事件处理

        internal void BatchAddTags_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable)
            {
                MessageBox.Show("TagTrain 不可用，无法批量添加标签。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (FileBrowser == null)
                return;
            
            var selectedItems = FileBrowser.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要添加标签的文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 使用标签选择对话框
            OpenTagDialogForSelectedItems();
        }

        internal void TagStatistics_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable)
            {
                MessageBox.Show("TagTrain 不可用，无法查看标签统计。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            try
            {
                var stats = OoiMRRIntegration.GetStatistics();
                var modelExists = OoiMRRIntegration.ModelExists();
                var modelPath = OoiMRRIntegration.GetModelPath();
                
                var message = $"标签统计信息\n\n" +
                              $"总标签数: {stats.UniqueTags}\n" +
                              $"总样本数: {stats.TotalSamples}\n" +
                              $"手动标注: {stats.ManualSamples}\n" +
                              $"唯一图片: {stats.UniqueImages}\n\n" +
                              $"模型状态: {(modelExists ? "已加载" : "未训练")}\n" +
                              $"模型路径: {modelPath}";
                
                MessageBox.Show(message, "标签统计", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取标签统计失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal void ImportLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导入库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        internal void ExportLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导出库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        internal void AddFileToLibrary_Click(object sender, RoutedEventArgs e)
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
                LoadLibraryFiles(_currentLibrary);
            }
        }


        private void WindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private bool _isPseudoMaximized = false;
        private Rect _restoreBounds;

        internal void WindowMaximize_Click(object sender, RoutedEventArgs e)
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

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 窗口关闭前统一保存所有状态（窗口大小/位置、分割线、导航、标签页）
            try
            {
                _windowStateManager?.SaveAllState();

                // 停止并刷新配置服务的定时器（如果有），确保配置落盘
                _configService?.StopAllTimers();
                _configService?.SaveCurrentConfig();
            }
            catch
            {
                // 关闭阶段不再向外抛异常，避免影响程序退出
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 调整列宽适应新窗口大小
            AdjustColumnWidths();

            // 窗口大小变化时不立即保存，避免覆盖分割线拖拽的保存
            // 保存会在下次用户操作时进行
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            // 保存窗口位置
            if (_windowStateManager != null && this.IsLoaded)
            {
                _windowStateManager.SaveAllState();
            }
        }

        internal void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            AdjustListViewColumnWidths();
        }

        private void AdjustListViewColumnWidths()
        {
            if (FileBrowser == null || _isSplitterDragging) return;
            _columnService?.AdjustListViewColumnWidths(FileBrowser);
        }
        /// <summary>
        /// 调整列宽以适应窗口大小变化
        /// </summary>
        internal void AdjustColumnWidths()
        {
            if (RootGrid == null) return;

            double total = RootGrid.ActualWidth - 12; // 减去分割器宽度
            double left = ColLeft.ActualWidth;
            double center = ColCenter.ActualWidth;
            double sum = left + center;

            // 如果空间不足，压缩列宽
            if (total < sum)
            {
                double scale = total / sum;
                ColLeft.Width = new GridLength(Math.Max(ColLeft.MinWidth, left * scale));
                ColCenter.Width = new GridLength(Math.Max(ColCenter.MinWidth, center * scale));
            }
        }

        private void EnsureColumnMinWidths()
        {
            // 强制检查并应用所有列的最小宽度约束
            if (RootGrid == null) return;
            
            // 简化逻辑，不需要强制转换
            
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

        public void UpdateWindowStateUI()
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

        private static class NativeMethods
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
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public struct MONITORINFO
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public int dwFlags;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MARGINS
            {
                public int cxLeftWidth;
                public int cxRightWidth;
                public int cyTopHeight;
                public int cyBottomHeight;
            }
        }
        internal void UpdateActionButtonsPosition()
        {
            // TitleActionBar已经自动处理按钮布局，不再需要手动调整位置
        }
        
        internal void UpdateSeparatorPosition()
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

        // 鼠标事件桥接方法 - 已迁移到 MouseEventHandler
        // 顶部标题栏鼠标按下：支持拖动窗口和双击最大化/还原
        private void TitleBar_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left)
                return;

            bool isMaximized = _isPseudoMaximized || this.WindowState == WindowState.Maximized;

            // 双击：最大化/还原
            if (e.ClickCount == 2)
            {
                WindowMaximize_Click(sender, new RoutedEventArgs());
                return;
            }

            // 单击：仅在非最大化时允许拖动窗口
            if (e.ClickCount == 1 && !isMaximized)
            {
                try
                {
                    this.DragMove();
                }
                catch
                {
                    // 忽略拖动过程中的异常（例如在最大化状态下快速拖动）
                }
            }
        }

        // 右上角按钮容器的鼠标事件：非按钮区域也要支持拖动窗口
        private void WindowControlButtonsContainer_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left)
                return;

            bool isMaximized = _isPseudoMaximized || this.WindowState == WindowState.Maximized;

            var element = sender as System.Windows.UIElement;
            if (element == null) return;

            // 命中测试：如果点击的是按钮，则让按钮自己处理
            var hit = System.Windows.Media.VisualTreeHelper.HitTest(element, e.GetPosition(element));
            if (hit != null)
            {
                var current = hit.VisualHit;
                while (current != null && current != element)
                {
                    if (current is System.Windows.Controls.Button)
                    {
                        // 点击在按钮上，不做拖动处理
                        return;
                    }
                    current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                }
            }

            // 非按钮区域：仅在非最大化时允许拖动窗口
            if (!isMaximized)
            {
                try
                {
                    this.DragMove();
                }
                catch
                {
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

        internal void LoadFavorites()
        {
            if (FavoritesListBox == null) return;
            _favoriteService.LoadFavorites(FavoritesListBox);
        }
        
        private void FavoritesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FavoritesListBox.SelectedItem == null) return;
            if (_draggedFavorite != null) return; // 如果正在拖拽，不处理单击
            if (_suppressFavoriteSelectionNavigation) return; // 右键上下文菜单打开时不导航
            
            // 使用反射获取Favorite对象
            var selectedItem = FavoritesListBox.SelectedItem;
            var favoriteProperty = selectedItem.GetType().GetProperty("Favorite");
            if (favoriteProperty == null) return;
            
            var favorite = favoriteProperty.GetValue(selectedItem) as Favorite;
            if (favorite == null) return;
            
            _navigationService.LastLeftNavSource = "Favorites";
            _navigationCoordinator.HandleFavoriteNavigation(favorite, NavigationCoordinator.ClickType.LeftClick);

            // 清除选择，避免残留选中状态
            FavoritesListBox.SelectedItem = null;
        }

        private bool _suppressFavoriteSelectionNavigation = false;

        private void FavoritesListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = true;
            var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (item != null)
            {
                item.IsSelected = true;
            }
        }

        private void FavoritesListBox_ContextMenuClosed(object sender, RoutedEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = false;
            // 关闭菜单后清除选择，避免残留状态
            if (FavoritesListBox != null)
                FavoritesListBox.SelectedItem = null;
        }
        
        private void FavoritesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 已改为单击进入，此方法保留但不再使用
        }
        
        private ContextMenu CreateFavoritesContextMenu()
        {
            var menu = new ContextMenu();
            menu.Closed += FavoritesContextMenu_Closed;
            
            var removeItem = new MenuItem { Header = "删除收藏" };
            removeItem.Click += (s, e) =>
            {
                if (FavoritesListBox.SelectedItem != null)
                {
                    var selectedItem = FavoritesListBox.SelectedItem;
                    var favoriteProperty = selectedItem.GetType().GetProperty("Favorite");
                    if (favoriteProperty != null)
                    {
                        var favorite = favoriteProperty.GetValue(selectedItem) as Favorite;
                        if (favorite != null)
                        {
                            DatabaseManager.RemoveFavorite(favorite.Path);
                            LoadFavorites();
                        }
                    }
                }
            };
            menu.Items.Add(removeItem);
            
            return menu;
        }

        private void FavoritesContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = false;
            if (FavoritesListBox != null)
                FavoritesListBox.SelectedItem = null;
        }
        
        private void InitializeFavoritesDragDrop()
        {
            if (FavoritesListBox == null) return;
            
            // 启用拖拽排序
            FavoritesListBox.PreviewMouseLeftButtonDown += FavoritesListBox_PreviewMouseLeftButtonDown;
            FavoritesListBox.Drop += FavoritesListBox_Drop;
            FavoritesListBox.DragOver += FavoritesListBox_DragOver;
            FavoritesListBox.DragLeave += FavoritesListBox_DragLeave;
            FavoritesListBox.AllowDrop = true;
            FavoritesListBox.PreviewMouseMove += FavoritesListBox_PreviewMouseMove;
        }
        
        private void FavoritesListBox_DragLeave(object sender, DragEventArgs e)
        {
            // 清除所有高亮
            var listBox = sender as ListBox;
            if (listBox != null)
            {
                foreach (ListBoxItem item in FindVisualChildren<ListBoxItem>(listBox))
                {
                    item.Background = System.Windows.Media.Brushes.Transparent;
                }
            }
        }
        
        private void FavoritesListBox_DragOver(object sender, DragEventArgs e)
        {
            // 检查数据格式
            if (!e.Data.GetDataPresent("Favorite"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            
            var draggedItem = e.Data.GetData("Favorite") as Favorite;
            if (draggedItem == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
            
            // 提供拖拽视觉效果
            var listBox = sender as ListBox;
            if (listBox != null)
            {
                var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetItem != null)
                {
                    // 高亮目标项
                    foreach (ListBoxItem item in FindVisualChildren<ListBoxItem>(listBox))
                    {
                        if (item == targetItem)
                        {
                            item.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 33, 150, 243));
                        }
                        else if (item.Background is SolidColorBrush brush && brush.Color.A == 100)
                        {
                            item.Background = System.Windows.Media.Brushes.Transparent;
                        }
                    }
                }
            }
        }
        
        private Favorite _draggedFavorite = null;
        private System.Windows.Point _dragStartPoint;
        
        private void FavoritesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (listBoxItem != null)
            {
                var item = listBoxItem.DataContext;
                var favoriteProperty = item.GetType().GetProperty("Favorite");
                if (favoriteProperty != null)
                {
                    _draggedFavorite = favoriteProperty.GetValue(item) as Favorite;
                }
            }
        }
        
        private void FavoritesListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedFavorite != null)
            {
                var currentPoint = e.GetPosition(null);
                var diff = _dragStartPoint - currentPoint;
                
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var listBox = sender as ListBox;
                    if (listBox != null)
                    {
                        // 创建数据对象并传递Favorite对象
                        var dataObject = new DataObject("Favorite", _draggedFavorite);
                        DragDrop.DoDragDrop(listBox, dataObject, DragDropEffects.Move);
                    }
                }
            }
        }
        private void FavoritesListBox_Drop(object sender, DragEventArgs e)
        {
            // 检查数据格式
            if (!e.Data.GetDataPresent("Favorite"))
            {
                return;
            }
            
            var draggedFavorite = e.Data.GetData("Favorite") as Favorite;
            if (draggedFavorite == null) return;
            
            var listBox = sender as ListBox;
            if (listBox == null)
            {
                return;
            }
            
            // 清除所有高亮
            foreach (ListBoxItem item in FindVisualChildren<ListBoxItem>(listBox))
            {
                item.Background = System.Windows.Media.Brushes.Transparent;
            }
            
            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (targetItem == null || targetItem.DataContext == null)
            {
                return;
            }
            
            var targetData = targetItem.DataContext;
            var favoriteProperty = targetData.GetType().GetProperty("Favorite");
            if (favoriteProperty == null)
            {
                return;
            }
            
            var targetFavorite = favoriteProperty.GetValue(targetData) as Favorite;
            if (targetFavorite == null || targetFavorite.Id == draggedFavorite.Id)
            {
                return;
            }
            
            // 更新排序顺序并重新加载
            var favorites = DatabaseManager.GetAllFavorites().ToList();
            var draggedIndex = favorites.FindIndex(f => f.Id == draggedFavorite.Id);
            var targetIndex = favorites.FindIndex(f => f.Id == targetFavorite.Id);
            
            if (draggedIndex >= 0 && targetIndex >= 0 && draggedIndex != targetIndex)
            {
                // 重新排序：移除拖拽项，插入到目标位置
                var newOrder = new List<Favorite>();
                for (int i = 0; i < favorites.Count; i++)
                {
                    if (i == draggedIndex) continue; // 跳过被拖拽的项
                    if (i == targetIndex)
                    {
                        // 在目标位置插入
                        if (draggedIndex < targetIndex)
                        {
                            // 向下拖拽：先插入目标项，再插入被拖拽项
                            newOrder.Add(favorites[targetIndex]);
                            newOrder.Add(draggedFavorite);
                        }
                        else
                        {
                            // 向上拖拽：先插入被拖拽项，再插入目标项
                            newOrder.Add(draggedFavorite);
                            newOrder.Add(favorites[targetIndex]);
                        }
                    }
                    else
                    {
                        newOrder.Add(favorites[i]);
                    }
                }
                
                // 更新数据库中的SortOrder（在文件夹和文件分组内排序）
                // 先按文件夹/文件分组，再更新SortOrder
                var folderGroup = newOrder.Where(f => f.IsDirectory).ToList();
                var fileGroup = newOrder.Where(f => !f.IsDirectory).ToList();
                
                int sortOrder = 0;
                foreach (var fav in folderGroup)
                {
                    DatabaseManager.UpdateFavoriteSortOrder(fav.Id, sortOrder++);
                }
                foreach (var fav in fileGroup)
                {
                    DatabaseManager.UpdateFavoriteSortOrder(fav.Id, sortOrder++);
                }
                
                // 重新加载显示
                LoadFavorites();
            }
            
            _draggedFavorite = null;
            e.Handled = true;
        }
        
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
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要收藏的文件或文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int successCount = 0;
            int skipCount = 0;

            foreach (var item in selectedItems)
            {
                try
                {
                    // 检查是否已收藏
                    if (DatabaseManager.IsFavorite(item.Path))
                    {
                        skipCount++;
                        continue;
                    }

                    string displayName = item.Name;
                    DatabaseManager.AddFavorite(item.Path, item.IsDirectory, displayName);
                    successCount++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"收藏失败: {item.Name} - {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // 刷新收藏列表
            LoadFavorites();

            // 不再显示提示框，静默完成
        }
        
        internal void FilesListView_PreviewMouseDoubleClickForBlank(object sender, MouseButtonEventArgs e)
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
                    _columnService?.AutoSizeGridViewColumn(thumbHeader.Column, FileBrowser);
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
            if (FileBrowser == null || column == null) return;
            _columnService?.AutoSizeGridViewColumn(column, FileBrowser);
        }

        // 右键列头 -> 列显示设置
        internal void EnsureHeaderContextMenuHook()
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
                                    "Name" => _configService?.Config.ColNameWidth ?? 200,
                                    "Size" => _configService?.Config.ColSizeWidth ?? 100,
                                    "Type" => _configService?.Config.ColTypeWidth ?? 100,
                                    "ModifiedDate" => _configService?.Config.ColModifiedDateWidth ?? 150,
                                    "CreatedTime" => _configService?.Config.ColCreatedTimeWidth ?? 50,
                                    "Tags" => _configService?.Config.ColTagsWidth ?? 150,
                                    "Notes" => _configService?.Config.ColNotesWidth ?? 200,
                                    _ => column.ActualWidth > 0 ? column.ActualWidth : 100
                                };
                                column.Width = Math.Max(40, w);
                            }
                            // 更新配置
                            var currentVisible = GetVisibleColumnsForCurrentMode() ?? "";
                            var currentSet = new HashSet<string>(currentVisible.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                            currentSet.Add(tag);
                            SetVisibleColumnsForCurrentMode(string.Join(",", currentSet));
                            _configService?.SaveCurrentConfig();
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
                            _configService?.SaveCurrentConfig();
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

        internal string GetCurrentModeKey()
        {
            return _configService?.Config.LastNavigationMode ?? "Path";
        }

        internal string GetVisibleColumnsForCurrentMode()
        {
            return _columnService?.GetVisibleColumnsForCurrentMode() ?? "";
        }

        private void SetVisibleColumnsForCurrentMode(string csv)
        {
            _columnService?.SetVisibleColumnsForCurrentMode(csv);
        }

        internal void ApplyVisibleColumnsForCurrentMode()
        {
            if (FileBrowser == null) return;
            _columnService?.ApplyVisibleColumnsForCurrentMode(FileBrowser);
            HookHeaderThumbs();
        }

        // 绑定列头分隔线双击
        internal void HookHeaderThumbs()
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
        internal void FilesList_HeaderThumbDoubleClick(object sender, MouseButtonEventArgs e)
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
            return _columnService?.IsColumnVisible(tag) ?? true;
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

        internal void FilesListView_PreviewKeyDown(object sender, KeyEventArgs e)
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
                            
                            // 使用统一导航协调器处理Enter键导航（左键点击）
                        _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.LeftClick);
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
                        
                        // 使用统一导航协调器处理Enter键导航（左键点击）
                        _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.LeftClick);
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

        // 文件操作事件桥接方法 - 已迁移到 MenuEventHandler
        private void Copy_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Copy_Click(sender, e);
        private void Cut_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Cut_Click(sender, e);

        /// <summary>
        /// 获取当前操作上下文
        /// </summary>
        private IFileOperationContext GetCurrentOperationContext()
        {
            if (_currentLibrary != null)
            {
                return new LibraryOperationContext(_currentLibrary, FileBrowser, this, () => LoadLibraryFiles(_currentLibrary));
            }
            else if (_currentTagFilter != null)
            {
                return new TagOperationContext(_currentTagFilter, FileBrowser, this, () => FilterByTag(_currentTagFilter));
            }
            else
            {
                return new PathOperationContext(_currentPath, FileBrowser, this, LoadCurrentDirectory);
            }
        }

        /// <summary>
        /// 执行复制操作
        /// </summary>
        internal void PerformCopyOperation()
        {
            try
            {
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var paths = selectedItems.Select(item => item.Path).ToList();
                FileClipboardManager.SetCopyPaths(paths);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行剪切操作
        /// </summary>
        internal void PerformCutOperation()
        {
            try
            {
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var paths = selectedItems.Select(item => item.Path).ToList();
                FileClipboardManager.SetCutPaths(paths);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"剪切操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行粘贴操作
        /// </summary>
        internal void PerformPasteOperation()
        {
            try
            {
                var copiedPaths = FileClipboardManager.GetCopiedPaths();
                if (copiedPaths == null || copiedPaths.Count == 0)
                {
                    return;
                }

                var context = GetCurrentOperationContext();
                if (context == null)
                {
                    return;
                }

                var isCut = FileClipboardManager.IsCutOperation;
                var pasteOperation = new PasteOperation(context);
                pasteOperation.Execute(copiedPaths, isCut);

                if (isCut)
                {
                    FileClipboardManager.ClearCutOperation();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行删除操作
        /// </summary>
        internal async void PerformDeleteOperation()
        {
            try
            {
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var context = GetCurrentOperationContext();
                if (context == null)
                {
                    return;
                }

                var deleteOperation = new DeleteOperation(context);
                await deleteOperation.ExecuteAsync(selectedItems);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Paste_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Paste_Click(sender, e);
        internal void Delete_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Delete_Click(sender, e);
        internal void Rename_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Rename_Click(sender, e);
        internal void ShowProperties_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ShowProperties_Click(sender, e);

        #endregion

        #region 列表排序

        internal void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var header = sender as GridViewColumnHeader;
            if (header == null || FileBrowser == null) return;

            _columnService?.HandleColumnHeaderClick(
                header,
                _currentFiles,
                (sortedFiles) =>
                {
                    _currentFiles = sortedFiles;
                    FileBrowser.FilesItemsSource = null;
                    FileBrowser.FilesItemsSource = _currentFiles;
                },
                FileBrowser.FilesGrid
            );
        }

        #endregion

        // ========== TagTrain 训练面板事件处理方法 ==========
        // 已迁移到 Services.TagTrain.TagTrainEventHandler
        
        private void TagTrainTagSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) 
            => _tagTrainEventHandler?.TagTrainTagSortComboBox_SelectionChanged(sender, e);
        
        private void TagTrainTagInputTextBox_TextChanged(object sender, TextChangedEventArgs e) 
            => _tagTrainEventHandler?.TagTrainTagInputTextBox_TextChanged(sender, e);
        
        private void TagTrainTagInputTextBox_KeyDown(object sender, KeyEventArgs e) 
            => _tagTrainEventHandler?.TagTrainTagInputTextBox_KeyDown(sender, e);
        
        private void TagClickModeBtn_Click(object sender, RoutedEventArgs e) 
            => _tagTrainEventHandler?.TagClickModeBtn_Click(sender, e);
        
        private void TagCategoryManageBtn_Click(object sender, RoutedEventArgs e) 
            => _tagTrainEventHandler?.TagCategoryManageBtn_Click(sender, e);
        
        private void TagBrowseCategoryManagement_Click(object sender, RoutedEventArgs e) 
            => _tagTrainEventHandler?.TagBrowseCategoryManagement_Click(sender, e);
        
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
                            if (_tagClickMode == TagClickMode.Browse)
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
                        if (_tagClickMode == TagClickMode.Browse)
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
        internal void RestoreSelectionByPaths(List<string> paths)
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
            => _tagTrainEventHandler?.TagTrainConfig_Click(sender, e);


    }



    /// <summary>
    /// 导航 UI 辅助实现类
    /// </summary>
    internal class NavigationUIHelper : Services.Navigation.INavigationUIHelper
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

                        // 匹配当前路径/库：无论是否选中，都显示黄色（优先级最高）
                        var yellowBg = _mainWindow.FindResource("HighlightBrush") as SolidColorBrush;
                        var blackFg = _mainWindow.FindResource("HighlightForegroundBrush") as SolidColorBrush;
                        var orangeBorder = _mainWindow.FindResource("HighlightBorderBrush") as SolidColorBrush;

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
                    _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var retryContainer = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                        if (retryContainer != null)
                        {
                            if (highlight)
                            {
                                if (retryContainer.Tag as string == "Match")
                                    return;

                                var yellowBg = _mainWindow.FindResource("HighlightBrush") as SolidColorBrush;
                                var blackFg = _mainWindow.FindResource("HighlightForegroundBrush") as SolidColorBrush;
                                var orangeBorder = _mainWindow.FindResource("HighlightBorderBrush") as SolidColorBrush;

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

    #region IConfigUIHelper 实现

    /// <summary>
    /// IConfigUIHelper 接口实现
    /// </summary>
    public partial class MainWindow
    {
        Window IConfigUIHelper.Window => this;

        Grid IConfigUIHelper.RootGrid => this.RootGrid;

        ColumnDefinition IConfigUIHelper.ColLeft => this.ColLeft;

        ColumnDefinition IConfigUIHelper.ColCenter => this.ColCenter;

        ColumnDefinition IConfigUIHelper.ColRight => this.ColRight;

        Controls.TitleActionBar IConfigUIHelper.TitleActionBar => this.TitleActionBar;

        string IConfigUIHelper.CurrentPath
        {
            get => _currentPath;
            set => _currentPath = value;
        }

        object IConfigUIHelper.CurrentLibrary => _currentLibrary;

        bool IConfigUIHelper.IsPseudoMaximized
        {
            get => _isPseudoMaximized;
            set => _isPseudoMaximized = value;
        }

        Rect IConfigUIHelper.RestoreBounds
        {
            get => _restoreBounds;
            set => _restoreBounds = value;
        }

        Rect IConfigUIHelper.GetCurrentMonitorWorkAreaDIPs()
        {
            return GetCurrentMonitorWorkAreaDIPs();
        }

        void IConfigUIHelper.AdjustColumnWidths()
        {
            AdjustColumnWidths();
        }

        void IConfigUIHelper.EnsureColumnMinWidths()
        {
            EnsureColumnMinWidths();
        }

        System.Windows.Threading.Dispatcher IConfigUIHelper.Dispatcher => this.Dispatcher;

        void IConfigUIHelper.ExtendFrameIntoClientArea(int left, int right, int top, int bottom)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var margins = new NativeMethods.MARGINS { cxLeftWidth = left, cxRightWidth = right, cyTopHeight = top, cyBottomHeight = bottom };
            NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }

        void IConfigUIHelper.UpdateWindowStateUI()
        {
            UpdateWindowStateUI();
        }
    }

    #endregion

    /// <summary>
    /// TagUIHandler 上下文实现类
    /// </summary>
    internal class TagUIHandlerContextImpl : Services.Tag.ITagUIHandlerContext
    {
        private readonly MainWindow _mainWindow;

        public TagUIHandlerContextImpl(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public Controls.FileBrowserControl FileBrowser => _mainWindow.FileBrowser;
        public System.Windows.Threading.Dispatcher Dispatcher => _mainWindow.Dispatcher;
        public System.Windows.Window OwnerWindow => _mainWindow;
        public Func<Tag> GetCurrentTagFilter => () => _mainWindow._currentTagFilter;
        public Action<Tag> SetCurrentTagFilter => tag => _mainWindow._currentTagFilter = tag;
        public Func<List<FileSystemItem>> GetCurrentFiles => () => _mainWindow._currentFiles;
        public Action<List<FileSystemItem>> SetCurrentFiles => files => _mainWindow._currentFiles = files;
        public Func<Library> GetCurrentLibrary => () => _mainWindow._currentLibrary;
        public Func<string> GetCurrentPath => () => _mainWindow._currentPath;
        public Func<bool> GetIsUpdatingTagSelection => () => _mainWindow._isUpdatingTagSelection;
        public Func<List<int>, List<string>> OrderTagNames => tagIds => _mainWindow.OrderTagNames(tagIds);
        public Action<Tag, List<FileSystemItem>> UpdateTagFilesUI => (tag, files) => _mainWindow.UpdateTagFilesUI(tag, files);
        public Action LoadFiles => () => _mainWindow.LoadFiles();
        public Action<Library> LoadLibraryFiles => library => _mainWindow.LoadLibraryFiles(library);
        public Action LoadCurrentDirectory => () => _mainWindow.LoadCurrentDirectory();
        public Action LoadTags => () => _mainWindow.LoadTags();
        public Func<System.Windows.Controls.Grid> GetNavTagContent => () => _mainWindow.NavTagContent;
        public Func<Services.FileList.FileListService> GetFileListService => () => _mainWindow._fileListService;
    }
}


