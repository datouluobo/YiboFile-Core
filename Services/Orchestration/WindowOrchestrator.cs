using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Modules;
using YiboFile.Services.Config;
using YiboFile.Services.UI.Adapters;
using YiboFile.Services.Navigation;
using YiboFile.Services.Search;
using YiboFile.Services.Tabs;
using YiboFile.Services.FileList;
using YiboFile.Services.FileOperations;
using YiboFile.Services.FileOperations.Undo;
using YiboFile.Services.Core.Error;
using YiboFile.Services.Features;
using YiboFile.Services.UI;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Favorite;
using YiboFile.Services.QuickAccess;
using YiboFile.Handlers;
using YiboFile.Models;
using YiboFile.Models.Navigation;

namespace YiboFile.Services.Orchestration
{
    /// <summary>
    /// 窗口编排器实现类
    /// 将业务逻辑从 MainWindow 剥离到独立服务中
    /// 
    /// 职责：
    /// 1. 服务初始化序列编排
    /// 2. MVVM 模块创建与注册
    /// 3. 事件处理器挂载
    /// 4. 应用初始状态恢复
    /// </summary>
    public class WindowOrchestrator : IWindowOrchestrator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBus _messageBus;

        // 缓存的服务引用（用于模块创建）
        private NavigationCoordinator _navigationCoordinator;
        private NavigationService _navigationService;
        private TabService _tabService;
        private TabService _secondTabService;
        private LibraryService _libraryService;
        private Favorite.FavoriteService _favoriteService;
        private SearchService _searchService;
        private SearchCacheService _searchCacheService;
        private FileListService _fileListService;
        private FileOperationService _fileOperationService;
        private Preview.PreviewService _previewService;
        private FolderSizeCalculationService _folderSizeCalculationService;
        private ITagService _tagService;
        private Archive.ArchiveService _archiveService;
        private Services.FileInfo.FileInfoService _fileInfoService;
        private Services.FileInfo.FileInfoService _secondFileInfoService;
        private QuickAccess.QuickAccessService _quickAccessService;
        private FileListService _secondFileListService;
        private FileSystemWatcherService _fileSystemWatcherService;
        private Services.ColumnManagement.ColumnService _columnService;
        private Services.UIHelper.IUIHelperService _uiHelperService;

        // Handlers
        private KeyboardEventHandler _keyboardEventHandler;

        // ViewModel 和模块引用
        private MainWindowViewModel _viewModel;
        private NavigationModule _navigationModule;
        private TabsModule _tabsModule;
        private FileListModule _fileListModule;
        private LayoutModule _layoutModule;
        private FileOperationModule _fileOperationModule;
        private NotesModule _notesModule;
        private TagsModule _tagsModule;
        private LibraryModule _libraryModule;
        private SearchModule _searchModule;
        private FavoritesModule _favoritesModule;

        private NavigationModeService _navigationModeService;
        private WindowStateManager _windowStateManager;

        // Handlers
        public Handlers.WindowLifecycleHandler LifecycleHandler { get; private set; }
        public Services.Settings.SettingsOverlayController SettingsController { get; private set; }
        public Handlers.ColumnInteractionHandler ColumnInteractionHandler { get; private set; }
        public Handlers.ColumnInteractionHandler SecondColumnInteractionHandler { get; private set; }
        public Handlers.FileListEventHandler MainFileListHandler { get; private set; }
        public Handlers.FileListEventHandler SecondFileListHandler { get; private set; }
        public Handlers.FileOperationHandler FileOperationHandler { get; private set; }
        public Services.FileOperations.FileOperationService FileOperationService => _fileOperationService;
        public Services.Navigation.NavigationModeService NavigationModeService => _navigationModeService;
        public Services.Navigation.NavigationCoordinator NavigationCoordinator => _navigationCoordinator;
        public NavigationService NavigationService => _navigationService;
        public TabService TabService => _tabService;
        public TabService SecondTabService => _secondTabService;
        public LibraryService LibraryService => _libraryService;
        public Favorite.FavoriteService FavoriteService => _favoriteService;
        public QuickAccess.QuickAccessService QuickAccessService => _quickAccessService;
        public FileListService FileListService => _fileListService;
        public FileListService SecondFileListService => _secondFileListService;
        public SearchService SearchService => _searchService;
        public SearchCacheService SearchCacheService => _searchCacheService;
        public FileSystemWatcherService FileSystemWatcherService => _fileSystemWatcherService;
        public Services.WindowStateManager WindowStateManager => _windowStateManager;
        public KeyboardEventHandler KeyboardEventHandler => _keyboardEventHandler;
        public Services.ColumnManagement.ColumnService ColumnService => _columnService;
        public Services.FileInfo.FileInfoService SecondFileInfoService => _secondFileInfoService;
        public Services.UIHelper.IUIHelperService UIHelperService => _uiHelperService;

        // 服务桥接（用于将 Service 事件转换为 MessageBus 消息）
        private void SetupServiceMessageBridges(MainWindow window)
        {
            // 1. NavigationService -> MessageBus
            if (_navigationService != null)
            {
                _navigationService.NavigateRequested += (s, path) =>
                {
                    _messageBus.Publish(new NavigationCompleteMessage(path, PaneId.Main, YiboFile.Models.Navigation.NavigationSource.AddressBar));
                };
            }

            // 2. FileListService -> MessageBus
            Action<object, YiboFile.Models.FileSystemItem> onFolderSizeCalculated = (s, item) =>
            {
                var pane = (s == _secondFileListService) ? PaneId.Second : PaneId.Main;
                _messageBus.Publish(new FolderSizeCalculatedMessage(item.Path, item.SizeBytes, item.Size));
            };

            Action<object, System.Collections.Generic.List<YiboFile.Models.FileSystemItem>> onMetadataEnriched = (s, items) =>
            {
                var pane = (s == _secondFileListService) ? PaneId.Second : PaneId.Main;
                // 这里可以发布汇总消息
            };

            if (_fileListService != null)
            {
                _fileListService.FolderSizeCalculated += (s, item) => onFolderSizeCalculated(s, item);
                _fileListService.MetadataEnriched += (s, items) => onMetadataEnriched(s, items);
            }
            if (_secondFileListService != null)
            {
                _secondFileListService.FolderSizeCalculated += (s, item) => onFolderSizeCalculated(s, item);
                _secondFileListService.MetadataEnriched += (s, items) => onMetadataEnriched(s, items);
            }

            // 3. FileSystemWatcherService -> MessageBus
            if (_fileSystemWatcherService != null)
            {
                _fileSystemWatcherService.FileSystemChanged += (s, e) =>
                {
                    _messageBus.Publish(new FileSystemChangedMessage(e.FullPath, e.ChangeType.ToString()));
                };
                _fileSystemWatcherService.RefreshRequested += (s, e) =>
                {
                    _messageBus.Publish(new ViewModels.Messaging.Messages.RefreshFileListMessage());
                };
            }

            // 4. LibraryService -> MessageBus (模块已部分桥接，此处补充汇总)
            if (_libraryService != null)
            {
                // LibraryModule 已经处理了 LibraryFilesLoaded 和 LibrariesLoaded
            }

            // 5. FavoriteService & QuickAccessService
            if (_favoriteService != null)
            {
                _favoriteService.NavigateRequested += (s, path) =>
                {
                    _messageBus.Publish(new NavigateToPathMessage(path));
                };

                _favoriteService.CreateTabRequested += (s, path) =>
                {
                    _messageBus.Publish(new CreateTabMessage(path));
                };

                _favoriteService.FileOpenRequested += (s, path) =>
                {
                    _messageBus.Publish(new OpenFileRequestMessage(path));
                };
            }
        }
        private Services.UI.EventBridgeService _eventBridgeService;

        public WindowOrchestrator(IServiceProvider serviceProvider, IMessageBus messageBus)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        /// <summary>
        /// 获取已创建的 MainWindowViewModel
        /// </summary>
        public MainWindowViewModel ViewModel => _viewModel;

        public async Task InitializeAsync(MainWindow window)
        {
            try
            {
                // 按照规范顺序执行初始化
                InitializeServices(window);
                InitializeMvvmModules(window);
                InitializeHandlers(window);

                // 迁移消息桥接逻辑 (从 MainWindow.Initialization.cs 迁移)
                SetupMessageBridges(window);
                SetupServiceMessageBridges(window); // 新增服务桥接

                // 部分窗口交互需要在 UI 渲染前准备，部分需要异步
                await ApplyInitialStateAsync(window);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初始化失败: {ex.Message}\n{ex.StackTrace}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                // 也可以记录日志
                System.Diagnostics.Debug.WriteLine($"[WindowOrchestrator] Initialization Failed: {ex}");
            }
        }

        public void InitializeServices(MainWindow window)
        {
            // 从 DI 容器获取单例/瞬时服务 (不再从 window 复制)
            _navigationCoordinator = _serviceProvider.GetRequiredService<NavigationCoordinator>();
            _navigationService = _serviceProvider.GetRequiredService<NavigationService>();
            _libraryService = _serviceProvider.GetRequiredService<LibraryService>();
            _favoriteService = _serviceProvider.GetRequiredService<FavoriteService>();
            _fileListService = _serviceProvider.GetRequiredService<FileListService>();
            _tagService = _serviceProvider.GetService<ITagService>();
            _folderSizeCalculationService = _serviceProvider.GetRequiredService<FolderSizeCalculationService>();
            _searchCacheService = _serviceProvider.GetRequiredService<SearchCacheService>();
            _searchService = _serviceProvider.GetRequiredService<SearchService>();
            _fileOperationService = _serviceProvider.GetRequiredService<FileOperationService>();
            _archiveService = _serviceProvider.GetRequiredService<Archive.ArchiveService>();
            _fileSystemWatcherService = _serviceProvider.GetRequiredService<FileSystemWatcherService>();
            _quickAccessService = _serviceProvider.GetRequiredService<QuickAccessService>();
            _secondFileListService = _serviceProvider.GetRequiredService<FileListService>();
            _columnService = _serviceProvider.GetRequiredService<Services.ColumnManagement.ColumnService>();

            if (window.SecondFileBrowser != null)
            {
                _secondFileInfoService = new Services.FileInfo.FileInfoService(
                    window.SecondFileBrowser,
                    _secondFileListService,
                    _navigationCoordinator,
                    _tagService
                );
            }

            // 为两个面板创建独立的服务实例 (Transient from DI)
            _tabService = _serviceProvider.GetRequiredService<TabService>();
            _secondTabService = _serviceProvider.GetRequiredService<TabService>();

            // 初始化协调器关系
            // 初始化协调器关系
            _navigationCoordinator.Initialize(
                _tabService,
                _secondTabService,
                _navigationService, // 使用本地通过 DI 获取的服务实例，因为 window._navigationService 此时尚未赋值
                _libraryService,
                (paneId) => paneId == PaneId.Main ? _viewModel?.PrimaryPane : _viewModel?.SecondaryPane);

            // 初始化列管理服务
            var columnService = _serviceProvider.GetRequiredService<ColumnService>();
            columnService.Initialize(() => window.GetCurrentModeKey());
            // window._columnService = columnService; // 供遗留代码使用 (Removed)

            // ========== MainWindow 内部服务初始化 (从 InitializeServices 迁移) ==========

            // 1. 设置 NavigationService 并绑定 UIHelper
            // window._navigationService = _navigationService; // Removed
            // Assuming Helpers namespace is imported or accessible via YiboFile.Helpers
            _navigationService.UIHelper = new YiboFile.Helpers.NavigationUIHelper(window);

            // 2. 初始化 UIHelperService
            // window._uiHelperService = new Services.UIHelper.UIHelperService(window.FileBrowser, window.Dispatcher); // Removed

            // 3. 初始化 FileInfoService (本地管理，不再挂载到 window)
            _fileInfoService = new Services.FileInfo.FileInfoService(
                window.FileBrowser,
                _fileListService,
                _navigationCoordinator,
                _tagService);

            if (window.SecondFileBrowser != null)
            {
                _secondFileInfoService = new Services.FileInfo.FileInfoService(
                    window.SecondFileBrowser,
                    _fileListService,
                    _navigationCoordinator,
                    _tagService);
            }

            // 4. 绑定 TabService UI 上下文 (已废弃并迁移至消息驱动)
            // window.AttachTabServiceUiContext();
            // AttachSecondTabServiceUiContext is now handled by LayoutEventHandler or upon SetDualListMode
        }

        public void InitializeMvvmModules(MainWindow window)
        {
            // ========== 核心 ViewModel 创建 ==========

            // 创建 UI 适配器
            var configAdapter = new ConfigUIAdapter(window);
            var navAdapter = new NavigationModeUIAdapter(window);

            // 创建 NavigationModeService
            _navigationModeService = new Services.Navigation.NavigationModeService(
                    navAdapter,
                    _navigationService,
                    _tabService,
                    ConfigurationService.Instance);
            // window._navigationModeService = navModeService; // Removed

            // 创建 RightPanelViewModel
            var rightPanelVM = new RightPanelViewModel(_messageBus, ConfigurationService.Instance, _fileListService);

            // 创建主 ViewModel
            _viewModel = new MainWindowViewModel(
                _messageBus,
                rightPanelVM,
                _previewService,
                _fileListService,
                _folderSizeCalculationService);

            // ========== 模块创建与注册 ==========

            // 导航模块
            _navigationModule = new NavigationModule(
                _messageBus,
                _navigationService,
                _navigationCoordinator,
                () => window.GetActivePaneId(), // 注入 Pane 解析器
                path => _navigationCoordinator.HandlePathNavigation(path, NavigationSource.External, ClickType.LeftClick, pane: PaneId.Main));
            _viewModel.RegisterModule(_navigationModule);

            // 标签页模块
            _tabsModule = new TabsModule(
                _messageBus,
                _tabService,
                _secondTabService,
                () => window.IsDualListMode,
                () => window.GetActivePaneId() == PaneId.Second,
                (path, activate) => _navigationCoordinator?.NavigateAsync(new NavigationRequest { Target = NavigationTarget.FromPath(path), ForceNewTab = true, Activate = activate }),
                tabId =>
                {
                    var tab = _tabService.FindTabByPath(tabId);
                    if (tab != null) _tabService.SwitchToTab(tab);
                });
            _viewModel.RegisterModule(_tabsModule);

            // 文件列表模块
            _fileListModule = new FileListModule(_messageBus);
            _viewModel.RegisterModule(_fileListModule);

            // 初始化主/副面板 ViewModel
            _viewModel.PrimaryPane = new PaneViewModel(window.Dispatcher, _messageBus) { IsActive = true };
            _viewModel.SecondaryPane = new PaneViewModel(window.Dispatcher, _messageBus, isSecondary: true)
            {
                IsActive = false,
                IsLoadingDisabled = false
            };

            // 关联模块到 ViewModel
            _viewModel.Navigation = _navigationModule;
            _viewModel.Tabs = _tabsModule;

            // 布局模块
            _layoutModule = new LayoutModule(_messageBus);
            _viewModel.Layout = _layoutModule;
            _viewModel.RegisterModule(_layoutModule);

            // 初始化布局模块状态
            var cfg = ConfigurationService.Instance.Config;
            _layoutModule.InitializeState(
                cfg.LayoutMode ?? "Work",
                cfg.IsDualListMode,
                false,
                cfg.IsSidebarCollapsed,
                cfg.IsPreviewCollapsed);

            // 文件操作模块
            var undoService = _serviceProvider.GetService<UndoService>();
            var errorService = _serviceProvider.GetService<ErrorService>();
            _fileOperationModule = new FileOperationModule(_messageBus, _fileOperationService, undoService, errorService);
            _viewModel.FileOperation = _fileOperationModule;
            _viewModel.RegisterModule(_fileOperationModule);

            // 备注模块
            var notesService = _serviceProvider.GetService<Features.FileNotes.INotesService>();
            if (notesService != null)
            {
                _notesModule = new NotesModule(_messageBus, notesService);
                _viewModel.Notes = _notesModule;
                _viewModel.RegisterModule(_notesModule);
            }

            // 标签模块
            var tagService = _serviceProvider.GetService<ITagService>();
            if (tagService != null)
            {
                _tagsModule = new TagsModule(_messageBus, tagService);
                _viewModel.Tags = _tagsModule;
                _viewModel.RegisterModule(_tagsModule);
            }

            // 库模块
            _libraryModule = new LibraryModule(_messageBus, _libraryService);
            _viewModel.Library = _libraryModule;
            _viewModel.RegisterModule(_libraryModule);

            // 搜索模块
            _searchModule = new SearchModule(
                _messageBus,
                _searchService,
                _searchCacheService,
                _tabService,
                _serviceProvider.GetService<IFullTextSearchService>(),
                _secondTabService,
                () => window.IsDualListMode,
                () => window.GetActivePaneId() == PaneId.Second);
            _viewModel.Search = _searchModule;
            _viewModel.RegisterModule(_searchModule);

            // 搜索模块 (之前已订阅 LibrarySelectedMessage，此处应确保业务逻辑由 PaneViewModel 自行处理)

            // 收藏模块
            _favoritesModule = new FavoritesModule(_messageBus, _favoriteService);
            _viewModel.Favorites = _favoritesModule;
            _viewModel.RegisterModule(_favoritesModule);

            // 初始化所有模块
            _viewModel.InitializeModules();

            // 设置 DataContext
            window.DataContext = _viewModel;

            // 初始化 NavigationRail (侧边栏)
            if (window.NavigationRail != null)
            {
                var railVm = _serviceProvider.GetRequiredService<ViewModels.NavigationRailViewModel>();
                var railCoordinator = _serviceProvider.GetRequiredService<Controllers.NavigationRailCoordinator>();

                window.NavigationRail.ViewModel = railVm;
                window.NavigationRail.Coordinator = railCoordinator;
                // window.NavigationRail.SetupMessageBridge(_messageBus); // Removed: Logic moved to WindowOrchestrator or handled by Coordinator

                // 同步当前状态到 Rail
                railCoordinator.SetNavigationMode(cfg.LastNavigationMode ?? "Path");
                railCoordinator.SetLayoutMode(cfg.LayoutMode ?? "Work");
                railVm.IsDualListMode = cfg.IsDualListMode;
            }

            // 同步回 MainWindow 的字段引用（兼容过渡期）
            SyncModulesToWindow(window);
        }

        /// <summary>
        /// 将模块引用同步回 MainWindow（过渡期兼容）
        /// </summary>
        private void SyncModulesToWindow(MainWindow window)
        {
            // 🔑 关键：设置 DataContext 以启用 XAML 数据绑定
            window.DataContext = _viewModel;

            window._viewModel = _viewModel;
            window._messageBus = _messageBus;
            window.InitializeMessageSubscriptions(); // 初始化 MainWindow 的消息订阅

            // 同步服务引用 (核心且必要的)
            window._navigationCoordinator = _navigationCoordinator;
            // window._tabService = _tabService; // Removed: use ViewModel
            // ... (Removing other window fields)
        }

        public void InitializeHandlers(MainWindow window)
        {
            InitializeInfrastructure(window);
            InitializeOverlays(window);
            InitializeInputHandlers(window);
            InitializeFileListHandlers(window);
            InitializeSupportHandlers(window);
            InitializeNavigationPanelHandlers(window);
            InitializeFileBrowserEvents(window);
            InitializeThemeHandlers(window);

            // 初始化服务事件 (需要所有服务已就绪)
            window.InitializeServiceEvents();
        }

        private void InitializeInfrastructure(MainWindow window)
        {
            // 初始化事件桥接服务
            _eventBridgeService = new Services.UI.EventBridgeService(window, _messageBus);

            // LayoutEventHandler (Initialize first to set up UI state like Dual List)
            var layoutHandler = new Handlers.LayoutEventHandler(window, _messageBus, _layoutModule);
            layoutHandler.Initialize();
            window._layoutEventHandler = layoutHandler;

            // 获取必要的服务
            // 手动实例化 WindowStateManager (因为它依赖 NavigationModeService，而后者未在 DI 中注册)
            var configUIHelper = new Services.UI.Adapters.ConfigUIAdapter(window);
            _uiHelperService = new Services.UIHelper.UIHelperService(window.FileBrowser, window.Dispatcher);

            _windowStateManager = new WindowStateManager(
                configUIHelper,
                _tabService,
                _navigationService,
                _navigationModeService,
                _secondTabService,
                _serviceProvider.GetService<YiboFile.Services.Data.Repositories.ILibraryRepository>()
            );
        }

        private void InitializeOverlays(MainWindow window)
        {
            // Initialize Settings Controller here (migrated from MainWindowInitializer)
            var settingsOverlay = window.FindName("SettingsOverlay") as System.Windows.Controls.Grid;
            var settingsPanel = window.FindName("SettingsPanel") as Controls.SettingsPanelControl;
            var rightPanel = window.FindName("RightPanel") as System.Windows.UIElement;
            if (settingsOverlay != null && settingsPanel != null)
            {
                this.SettingsController = new Services.Settings.SettingsOverlayController(
                    settingsOverlay,
                    settingsPanel,
                    rightPanel,
                    (cfg) => { /* Auto-handled */ }
                );

                // Subscribe to Settings messages
                _messageBus.Subscribe<ShowSettingsMessage>(msg => window.Dispatcher.Invoke(() => this.SettingsController?.Show()));
            }

            // Initialize About overlay logic
            var aboutOverlay = window.FindName("AboutOverlay") as System.Windows.Controls.Grid;
            var aboutPanel = window.FindName("AboutPanel") as Controls.AboutPanelControl;
            if (aboutOverlay != null && aboutPanel != null)
            {
                _messageBus.Subscribe<ShowAboutMessage>(msg => window.Dispatcher.Invoke(() => aboutOverlay.Visibility = System.Windows.Visibility.Visible));
                aboutPanel.CloseRequested += (s, e) => aboutOverlay.Visibility = System.Windows.Visibility.Collapsed;
            }

            // Clipboard History Panel - Handle interactions
            if (window.ClipboardHistoryPanelControl != null)
            {
                window.ClipboardHistoryPanelControl.ItemPasted += (item) =>
                {
                    // 1. Close Panel
                    if (_layoutModule != null)
                    {
                        _layoutModule.ActiveSpecialPanel = "None";
                        _layoutModule.IsMainLayoutVisible = true;
                    }

                    // 2. Trigger Paste in Active Pane
                    window.Dispatcher.InvokeAsync(() =>
                    {
                        // Restore focus
                        if (_layoutModule?.IsSecondPaneFocused == true)
                        {
                            window.SecondFileBrowser?.Focus();
                            window.SecondFileBrowser?.FilesList?.Focus();
                        }
                        else
                        {
                            window.FileBrowser?.Focus();
                            window.FileBrowser?.FilesList?.Focus();
                        }

                        // Execute Paste
                        _viewModel?.FileOperation?.PasteCommand?.Execute(_viewModel.ActivePane);
                    });
                };
            }
        }

        private void InitializeInputHandlers(MainWindow window)
        {
            var undoService = _serviceProvider.GetService<UndoService>();

            // 1. KeyboardEventHandler
            var keyboardHandler = new Handlers.KeyboardEventHandler(
                window.FileBrowser,
                () => window.GetActivePaneId() == PaneId.Second ? window.SecondFileBrowser : window.FileBrowser,
                () => window.GetActivePaneId() == PaneId.Second ? _secondTabService : _tabService,
                tab => (window.GetActivePaneId() == PaneId.Second ? _secondTabService : _tabService).RemoveTab(tab),
                path => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.External, YiboFile.Models.Navigation.ClickType.LeftClick, pane: window.GetActivePaneId()), // Using Coordinator
                tab => (window.GetActivePaneId() == PaneId.Second ? _secondTabService : _tabService).SetActiveTab(tab), // Consistent method name
                () => _viewModel?.ActivePane?.Commands?.NewFolderCommand?.Execute(null),
                path => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.External, YiboFile.Models.Navigation.ClickType.LeftClick, pane: window.GetActivePaneId()), // Using Coordinator
                mode => _navigationModeService?.SwitchNavigationMode(mode), // Using Service
                () => _viewModel?.ActivePane?.NavigationMode == "Library",
                () => window.CloseOverlays(),
                () => { if (_navigationService?.CanNavigateBack == true) _navigationService.NavigateBack(); },
                messageBus: _messageBus,
                switchLayoutMode: index => window.SwitchLayoutModeByIndex(index),
                isDualListMode: () => _layoutModule?.IsDualListMode ?? false,
                switchDualPaneFocus: () => _layoutModule?.SwitchFocusedPane()
            );
            _keyboardEventHandler = keyboardHandler;

            // 4. MouseEventHandler
            var mouseHandler = new Handlers.MouseEventHandler(
                () => window.WindowMaximize_Click(null, null),
                () => window.DragMove(),
                () => window.NavigationPanelControl?.QuickAccessListBox,
                _navigationCoordinator,
                fav => _navigationCoordinator.HandleFavoriteNavigation(fav, YiboFile.Models.Navigation.ClickType.LeftClick, window.GetActivePaneId()),
                path => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.QuickAccess, YiboFile.Models.Navigation.ClickType.LeftClick, pane: window.GetActivePaneId()),
                () => window.GetActivePaneId()
            );

            // Hook listbox events to decentralized handler
            if (window.NavigationPanelControl != null)
            {
                if (window.NavigationPanelControl.LibrariesListBoxControl != null)
                    window.NavigationPanelControl.LibrariesListBoxControl.PreviewMouseDown += mouseHandler.LibrariesListBox_PreviewMouseDown;

                if (window.NavigationPanelControl.QuickAccessListBoxControl != null)
                {
                    window.NavigationPanelControl.QuickAccessListBoxControl.PreviewMouseDown += mouseHandler.QuickAccessListBox_PreviewMouseDown;
                }
            }

            // Global Mouse Down for handling focus/edit mode logic outside controls
            window.PreviewMouseDown += (s, e) =>
            {
                mouseHandler.HandleGlobalMouseDown(s, e, window.SecondFileBrowser);
            };
        }

        private void InitializeFileListHandlers(MainWindow window)
        {
            // 2. ColumnInteractionHandler (主面板)
            var mainColumnHandler = new Handlers.ColumnInteractionHandler(window, window.FileBrowser, _columnService);
            mainColumnHandler.Initialize();
            mainColumnHandler.HookHeaderThumbs();

            // 3. ColumnInteractionHandler (副面板)
            Handlers.ColumnInteractionHandler secondColumnHandler = null;
            if (window.SecondFileBrowser != null)
            {
                secondColumnHandler = new Handlers.ColumnInteractionHandler(window, window.SecondFileBrowser, _columnService);
                secondColumnHandler.Initialize();
                secondColumnHandler.HookHeaderThumbs();
            }

            this.ColumnInteractionHandler = mainColumnHandler;
            this.SecondColumnInteractionHandler = secondColumnHandler;

            // 7. FileListEventHandler (主面板)
            this.MainFileListHandler = new Handlers.FileListEventHandler(
                window.FileBrowser,
                _navigationCoordinator,
                () => _viewModel?.PrimaryPane?.NavigationMode == "Library",
                mode => _navigationModeService?.SwitchNavigationMode(mode), // Using Service
                path => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.FileList, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Main), // Using Coordinator
                () => _viewModel?.PrimaryPane?.Commands?.NavigateBackCommand?.Execute(null),
                col => window.AutoSizeGridViewColumn(col),
                () => _viewModel?.PrimaryPane?.CurrentPath,
                () => _viewModel?.PrimaryPane?.Commands?.PropertiesCommand?.Execute(null), // Using ViewModel
                (path, force, activate) => _ = _navigationCoordinator.NavigateAsync(new NavigationRequest
                {
                    Target = NavigationTarget.FromPath(path),
                    ForceNewTab = force,
                    Activate = activate ?? true,
                    Pane = PaneId.Main
                }), // Using Coordinator
                PaneId.Main
            );
            this.MainFileListHandler.Initialize(window.FileBrowser.FilesList);

            // 8. FileListEventHandler (副面板)
            if (window.SecondFileBrowser != null)
            {
                this.SecondFileListHandler = new Handlers.FileListEventHandler(
                    window.SecondFileBrowser,
                    _navigationCoordinator,
                    () => _viewModel?.SecondaryPane?.NavigationMode == "Library",
                    mode => _navigationModeService?.SwitchNavigationMode(mode), // Using Service
                    path => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.FileList, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Second), // Using Coordinator
                    () => _viewModel?.SecondaryPane?.Commands?.NavigateBackCommand?.Execute(null),
                    col => window.AutoSizeGridViewColumn(col),
                    () => _viewModel?.SecondaryPane?.CurrentPath,
                    () => _viewModel?.SecondaryPane?.Commands?.PropertiesCommand?.Execute(null), // Using ViewModel
                    (path, force, activate) => _ = _navigationCoordinator.NavigateAsync(new NavigationRequest
                    {
                        Target = NavigationTarget.FromPath(path),
                        ForceNewTab = force,
                        Activate = activate ?? true,
                        Pane = PaneId.Second
                    }), // Using Coordinator
                    PaneId.Second
                );
                this.SecondFileListHandler.Initialize(window.SecondFileBrowser.FilesList);
            }
        }

        private void InitializeSupportHandlers(MainWindow window)
        {
            var undoService = _serviceProvider.GetService<UndoService>();

            // 5. WindowLifecycleHandler
            this.LifecycleHandler = new Handlers.WindowLifecycleHandler(window, _windowStateManager, _columnService);

            // 6. FileOperationHandler
            this.FileOperationHandler = new Handlers.FileOperationHandler(window, undoService, _fileOperationService);

            // 9. LibraryEventHandler
            window._libraryEventHandler = new Handlers.LibraryEventHandler(
                window,
                _libraryService,
                _navigationCoordinator,
                _navigationService,
                _fileListService,
                _columnService
            );
            window._libraryEventHandler.Initialize();

            // 订阅 TabManager 的关闭覆盖层请求
            if (window.TabManager != null)
            {
                window.TabManager.CloseOverlayRequested += (s, e) => window.CloseOverlays();
            }
            if (window.SecondTabManager != null)
            {
                window.SecondTabManager.CloseOverlayRequested += (s, e) => window.CloseOverlays();
            }

            // 初始化拖放
            window._dragDropEventHandler = new Handlers.DragDropEventHandler(window);
            window._dragDropEventHandler.Initialize();
        }

        private void InitializeNavigationPanelHandlers(MainWindow window)
        {
            if (window.NavigationPanelControl == null) return;

            window.NavigationPanelControl.LibraryManageClick += (s, e) => _viewModel?.ActivePane?.Commands?.NewLibraryCommand?.Execute(null);

            window.NavigationPanelControl.PathManageClick += (s, e) =>
            {
                var settingsWindow = new YiboFile.Windows.NavigationSettingsWindow("Path");
                settingsWindow.Owner = window;
                settingsWindow.ShowDialog();
            };

            if (window.NavigationPanelControl.TagBrowsePanelControl != null)
            {
                window.NavigationPanelControl.TagBrowsePanelControl.TagClicked += (tagId, tagName) =>
                {
                    if (string.IsNullOrEmpty(tagName)) return;
                    _ = _navigationCoordinator?.NavigateAsync(new YiboFile.Models.Navigation.NavigationRequest
                    {
                        Target = YiboFile.Models.Navigation.NavigationTarget.FromTag(tagName),
                        Pane = window.GetActivePaneId(),
                        Source = YiboFile.Models.Navigation.NavigationSource.SidebarTag
                    });
                };
                window.NavigationPanelControl.TagBrowsePanelControl.BackRequested += (s, e) =>
                {
                    _viewModel?.Navigation?.NavigateBackCommand?.Execute(null);
                };
            }
        }

        private void InitializeFileBrowserEvents(MainWindow window)
        {
            if (window.FileBrowser != null)
            {
                window.FileBrowser.PathChanged += (s, path) => _navigationCoordinator?.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.AddressBar, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Main);
                window.FileBrowser.BreadcrumbClicked += (s, path) => _navigationCoordinator?.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.Breadcrumb, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Main);
            }

            if (window.SecondFileBrowser != null)
            {
                window.SecondFileBrowser.PathChanged += (s, path) => _navigationCoordinator?.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.AddressBar, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Second);
                window.SecondFileBrowser.BreadcrumbClicked += (s, path) => _navigationCoordinator?.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.Breadcrumb, YiboFile.Models.Navigation.ClickType.LeftClick, pane: PaneId.Second);
            }
        }

        private void InitializeThemeHandlers(MainWindow window)
        {
            // 订阅主题切换事件,刷新导航面板图标
            Services.Theming.ThemeManager.ThemeChanged += (s, e) =>
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 重新加载快速访问、驱动器和收藏列表以刷新图标
                        if (window.QuickAccessListBox != null)
                            _quickAccessService?.LoadQuickAccess(window.QuickAccessListBox);
                        if (window.DrivesTreeView != null)
                            _quickAccessService?.LoadDriveTree(window.DrivesTreeView, _fileListService.FormatFileSize);
                        _viewModel?.Favorites?.LoadFavorites();
                    }
                    catch (Exception) { }

                    // 修复：切换主题时，如果有副列表，强制刷新布局以防止地址栏错位
                    if (window.IsDualListMode && window.SecondFileBrowserContainer != null)
                    {
                        window.SecondFileBrowserContainer.InvalidateVisual();
                        window.SecondFileBrowserContainer.UpdateLayout();
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }

        private void SetupMessageBridges(MainWindow window)
        {
            // 迁移自 MainWindow.Initialization.cs

            // 1. 信息面板更新消息
            _messageBus.Subscribe<ShowFileInfoMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (msg.Pane == PaneId.Second)
                        _secondFileInfoService?.ShowFileInfo(msg.Item);
                    else
                        _fileInfoService?.ShowFileInfo(msg.Item);
                });
            });

            _messageBus.Subscribe<ShowLibraryInfoMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (msg.Pane == PaneId.Second)
                        _secondFileInfoService?.ShowLibraryInfo(msg.Library);
                    else
                        _fileInfoService?.ShowLibraryInfo(msg.Library);
                });
            });

            // 2. 预览导航请求
            _messageBus.Subscribe<PreviewNavigationRequestMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    var isSecond = window.IsDualListMode && window.IsSecondPaneFocused;
                    var activeBrowser = isSecond ? window.SecondFileBrowser : window.FileBrowser;
                    if (activeBrowser != null && activeBrowser.FilesList != null)
                    {
                        var list = activeBrowser.FilesList;
                        if (list.Items.Count == 0) return;

                        int newIndex = list.SelectedIndex == -1 ? 0 : (msg.IsNext ? list.SelectedIndex + 1 : list.SelectedIndex - 1);

                        if (newIndex >= 0 && newIndex < list.Items.Count)
                        {
                            list.SelectedIndex = newIndex;
                            list.ScrollIntoView(list.Items[newIndex]);
                        }
                    }
                });
            });


            // 4. 打开文件请求
            _messageBus.Subscribe<OpenFileRequestMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(msg.FilePath))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = msg.FilePath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            DialogService.Error($"无法打开文件: {ex.Message}", owner: window);
                        }
                    }
                });
            });

            // 5. 初始化预览服务 (MVVM 桥接)
            _previewService = new Services.Preview.PreviewService(
                _messageBus,
                window.Dispatcher,
                () => _navigationCoordinator.HandlePathNavigation(_viewModel.CurrentPath, NavigationSource.External, ClickType.LeftClick),
                path => _navigationCoordinator.HandlePathNavigation(path, YiboFile.Models.Navigation.NavigationSource.External, YiboFile.Models.Navigation.ClickType.LeftClick, forceNewTab: true)
            );

            // 6. 设置文件操作上下文提供者
            _fileOperationService.SetContextProvider(() => window.GetActiveFileOperationContext());

            // 7. 全局错误事件订阅
            var errorService = _serviceProvider.GetRequiredService<ErrorService>();
            errorService.ErrorOccurred += (s, e) =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (e.Severity == YiboFile.Services.Core.Error.ErrorSeverity.Critical)
                    {
                        DialogService.Error(e.Message, "严重错误", window);
                    }
                    else
                    {
                        var notificationType = e.Severity switch
                        {
                            YiboFile.Services.Core.Error.ErrorSeverity.Warning => YiboFile.Controls.NotificationType.Warning,
                            YiboFile.Services.Core.Error.ErrorSeverity.Error => YiboFile.Controls.NotificationType.Error,
                            _ => YiboFile.Controls.NotificationType.Info
                        };
                        Services.Core.NotificationService.Show(e.Message, notificationType);
                    }
                });
            };
        }



        public async Task ApplyInitialStateAsync(MainWindow window)
        {
            var config = ConfigurationService.Instance.Config;

            // 0. Update configs for services
            if (config != null)
            {
                _tabService?.UpdateConfig(config);
                _secondTabService?.UpdateConfig(config);
            }

            // 1. 恢复窗口和布局状态
            _windowStateManager?.RestoreAllState();

            // 2. 加载初始数据
            _libraryService?.LoadLibraries();

            // 加载快速访问列表 (通过反射或直接获取服务)
            var quickAccessService = _serviceProvider.GetService<QuickAccessService>();
            if (quickAccessService != null && window.QuickAccessListBox != null)
            {
                quickAccessService.LoadQuickAccess(window.QuickAccessListBox);
            }

            // 加载驱动器列表
            window.LoadDrives();

            // 4. 恢复最后的状态 (导航模式等)
            if (!string.IsNullOrEmpty(config.LastNavigationMode))
            {
                _navigationModeService?.SwitchNavigationMode(config.LastNavigationMode, skipRefresh: true);
            }

            // 恢复标签页
            _windowStateManager?.RestoreTabsState();

            // 5. 强制修正布局
            window.Dispatcher.Invoke(() =>
            {
                this.LifecycleHandler?.AdjustColumnWidths();
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            // 6. 启动后台索引
            _serviceProvider.GetService<IFullTextSearchService>()?.StartBackgroundIndexing();

            // 7. 初始化 UI 事件 (确保服务已就绪)
            window.InitializeEvents();
            window.InitializeServiceEvents(); // 虽然主要是空方法或委派，但为了结构完整性

            await Task.CompletedTask;
        }
    }
}
