using System;
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
        private Services.UI.EventBridgeService _eventBridgeService;
        private FileListService _secondFileListService;
        private FileSystemWatcherService _fileSystemWatcherService;
        private QuickAccessService _quickAccessService;

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

            // 为两个面板创建独立的服务实例 (Transient from DI)
            _tabService = _serviceProvider.GetRequiredService<TabService>();
            _secondTabService = _serviceProvider.GetRequiredService<TabService>();

            // 初始化协调器关系
            _navigationCoordinator.Initialize(
                _tabService,
                _secondTabService,
                window._navigationService, // 暂时保留 MainWindow 的引用
                _libraryService,
                (paneId) => paneId == PaneId.Main ? _viewModel?.PrimaryPane : _viewModel?.SecondaryPane);

            // 初始化列管理服务
            var columnService = _serviceProvider.GetRequiredService<ColumnService>();
            columnService.Initialize(() => window.GetCurrentModeKey());
            window._columnService = columnService; // 供遗留代码使用

            // ========== MainWindow 内部服务初始化 (从 InitializeServices 迁移) ==========

            // 1. 设置 NavigationService 并绑定 UIHelper
            window._navigationService = _navigationService;
            // Assuming Helpers namespace is imported or accessible via YiboFile.Helpers
            window._navigationService.UIHelper = new YiboFile.Helpers.NavigationUIHelper(window);

            // 2. 初始化 UIHelperService
            window._uiHelperService = new Services.UIHelper.UIHelperService(window.FileBrowser, window.Dispatcher);

            // 3. 初始化 FileInfoService
            window._fileInfoService = new Services.FileInfo.FileInfoService(
                window.FileBrowser,
                _fileListService,
                _navigationCoordinator,
                _tagService);

            if (window.SecondFileBrowser != null)
            {
                window._secondFileInfoService = new Services.FileInfo.FileInfoService(
                    window.SecondFileBrowser,
                    _fileListService,
                    _navigationCoordinator,
                    _tagService);
            }

            // 4. 绑定 TabService UI 上下文 (必需在 TabService 初始化之后)
            window.AttachTabServiceUiContext();
            window.AttachSecondTabServiceUiContext();
        }

        public void InitializeMvvmModules(MainWindow window)
        {
            // ========== 核心 ViewModel 创建 ==========

            // 创建 UI 适配器
            var configAdapter = new ConfigUIAdapter(window);
            var navAdapter = new NavigationModeUIAdapter(window);

            // 创建 NavigationModeService
            var navModeService = window._navigationModeService;
            if (navModeService == null)
            {
                // 如果 Initializer 未创建，则在此创建
                navModeService = new Services.Navigation.NavigationModeService(
                    navAdapter,
                    window._navigationService,
                    _tabService,
                    ConfigurationService.Instance);
                window._navigationModeService = navModeService;
            }

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
                path => window.NavigateToPathFromModule(path));
            _viewModel.RegisterModule(_navigationModule);

            // 标签页模块
            _tabsModule = new TabsModule(
                _messageBus,
                _tabService,
                _secondTabService,
                () => window.IsDualListMode,
                () => window.GetActivePaneId() == PaneId.Second,
                (path, activate) => window.CreateTab(path, activate),
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
                window.NavigationRail.SetupMessageBridge(_messageBus);

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
            window._navigationModule = _navigationModule;
            window._tabsModule = _tabsModule;
            window._fileListModule = _fileListModule;
            window._layoutModule = _layoutModule;
            window._fileOperationModule = _fileOperationModule;
            window._notesModule = _notesModule;
            window._tagsModule = _tagsModule;
            window._favoritesModule = _favoritesModule;
            window._libraryModule = _libraryModule;
            window._searchModule = _searchModule;

            // 同步服务引用
            window._navigationCoordinator = _navigationCoordinator;
            window._tabService = _tabService;
            window._secondTabService = _secondTabService;
            window._libraryService = _libraryService;
            window._favoriteService = _favoriteService;
            window._fileListService = _fileListService;
            window._folderSizeCalculationService = _folderSizeCalculationService;
            window._tagService = _tagService;
            window._searchService = _searchService;
            window._searchCacheService = _searchCacheService;
            window._fileOperationService = _fileOperationService;
            window._archiveService = _archiveService;
            window._fileSystemWatcherService = _fileSystemWatcherService;
            window._quickAccessService = _quickAccessService;
            window._secondFileListService = _secondFileListService;
        }

        public void InitializeHandlers(MainWindow window)
        {
            // 初始化事件桥接服务
            _eventBridgeService = new Services.UI.EventBridgeService(window, _messageBus);

            // ========== Handler 初始化 (从 MainWindow.Handlers.cs 迁移) ==========

            // 获取必要的服务
            var undoService = _serviceProvider.GetService<UndoService>();
            var errorService = _serviceProvider.GetService<ErrorService>();
            var columnService = _serviceProvider.GetRequiredService<ColumnService>();

            // 手动实例化 WindowStateManager (因为它依赖 NavigationModeService，而后者未在 DI 中注册)
            var configUIHelper = new Services.UI.Adapters.ConfigUIAdapter(window);
            var windowStateManager = new WindowStateManager(
                configUIHelper,
                _tabService,
                window._navigationService,
                window._navigationModeService,
                _secondTabService,
                _serviceProvider.GetService<YiboFile.Services.Data.Repositories.ILibraryRepository>()
            );
            window._windowStateManager = windowStateManager;

            // 1. KeyboardEventHandler
            var keyboardHandler = new Handlers.KeyboardEventHandler(
                window.FileBrowser,
                () => window.GetActivePaneId() == PaneId.Second ? window.SecondFileBrowser : window.FileBrowser,
                () => window.GetActivePaneId() == PaneId.Second ? _secondTabService : _tabService,
                tab => (window.GetActivePaneId() == PaneId.Second ? _secondTabService : _tabService).RemoveTab(tab),
                path => window.CreateTab(path),
                tab => (window.GetActivePaneId() == PaneId.Second ? _secondTabService : _tabService).SwitchToTab(tab),
                () => _viewModel?.ActivePane?.NewFolderCommand?.Execute(null),
                () => window.RefreshFileList(),
                () => _viewModel?.ActivePane?.CopyCommand?.Execute(null),
                () => _viewModel?.ActivePane?.PasteCommand?.Execute(null),
                () => _viewModel?.ActivePane?.CutCommand?.Execute(null),
                () => _viewModel?.ActivePane?.DeleteCommand?.Execute(null),
                async () => await window.DeleteSelectedFilesAsync(permanent: true),
                () => _viewModel?.ActivePane?.RenameCommand?.Execute(null),
                path => window.NavigateToPath(path),
                mode => window.SwitchNavigationMode(mode),
                () => _viewModel?.ActivePane?.NavigationMode == "Library",
                () => window.CloseOverlays(),
                () => window.Back_Click_Logic(),
                () => window.Undo_Click(null, null),
                () => window.Redo_Click(null, null),
                messageBus: _messageBus,
                switchLayoutMode: index => window.SwitchLayoutModeByIndex(index),
                isDualListMode: () => _layoutModule?.IsDualListMode ?? false,
                switchDualPaneFocus: () => _layoutModule?.SwitchFocusedPane()
            );

            // 2. ColumnInteractionHandler (主面板)
            var mainColumnHandler = new Handlers.ColumnInteractionHandler(window, window.FileBrowser, columnService);
            mainColumnHandler.Initialize();
            mainColumnHandler.HookHeaderThumbs();

            // 3. ColumnInteractionHandler (副面板)
            Handlers.ColumnInteractionHandler secondColumnHandler = null;
            if (window.SecondFileBrowser != null)
            {
                secondColumnHandler = new Handlers.ColumnInteractionHandler(window, window.SecondFileBrowser, columnService);
                secondColumnHandler.Initialize();
                secondColumnHandler.HookHeaderThumbs();
            }

            // 4. MouseEventHandler
            var mouseHandler = new Handlers.MouseEventHandler(
                () => window.WindowMaximize_Click(null, null),
                () => window.DragMove(),
                () => window.NavigationPanelControl?.QuickAccessListBox,
                _navigationCoordinator,
                fav => _navigationCoordinator.HandleFavoriteNavigation(fav, ClickType.LeftClick, window.GetActivePaneId()),
                path => _navigationCoordinator.HandlePathNavigation(path, NavigationSource.QuickAccess, ClickType.LeftClick, pane: window.GetActivePaneId())
            );

            // 5. WindowLifecycleHandler
            var lifecycleHandler = new Handlers.WindowLifecycleHandler(window, windowStateManager, columnService);

            // 6. FileOperationHandler
            var fileOpHandler = new Handlers.FileOperationHandler(window, undoService, _fileOperationService);

            // 7. FileListEventHandler (主面板)
            var mainFileListHandler = new Handlers.FileListEventHandler(
                window.FileBrowser,
                _navigationCoordinator,
                () => _viewModel?.PrimaryPane?.NavigationMode == "Library",
                mode => window.SwitchNavigationMode(mode),
                path => window.NavigateToPath(path, PaneId.Main),
                () => _viewModel?.PrimaryPane?.NavigateBackCommand?.Execute(null),
                col => window.AutoSizeGridViewColumn(col),
                () => _viewModel?.PrimaryPane?.CurrentPath,
                () => window.ShowSelectedFileProperties(),
                (path, force, activate) => window.CreateTab(path, force, activate, PaneId.Main)
            );
            mainFileListHandler.Initialize(window.FileBrowser.FilesList);

            // 8. FileListEventHandler (副面板)
            Handlers.FileListEventHandler secondFileListHandler = null;
            if (window.SecondFileBrowser != null)
            {
                secondFileListHandler = new Handlers.FileListEventHandler(
                    window.SecondFileBrowser,
                    _navigationCoordinator,
                    () => _viewModel?.SecondaryPane?.NavigationMode == "Library",
                    mode => { /* handled elsewhere */ },
                    path => window.NavigateToPath(path, PaneId.Second),
                    () => _viewModel?.SecondaryPane?.NavigateBackCommand?.Execute(null),
                    col => window.AutoSizeGridViewColumn(col),
                    () => _viewModel?.SecondaryPane?.CurrentPath,
                    () => window.ShowSelectedFileProperties(),
                    (path, force, activate) => window.CreateTab(path, force, activate, PaneId.Second)
                );
                secondFileListHandler.Initialize(window.SecondFileBrowser.FilesList);
            }

            // 存储 Handler 引用到 window (用于生命周期管理)
            window._keyboardEventHandler = keyboardHandler;
            window._columnInteractionHandler = mainColumnHandler;
            window._secondColumnInteractionHandler = secondColumnHandler;
            window._mouseEventHandler = mouseHandler;
            window._windowLifecycleHandler = lifecycleHandler;
            window._fileOperationHandler = fileOpHandler;
            window._mainFileListHandler = mainFileListHandler;
            window._secondFileListHandler = secondFileListHandler;

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
            window.InitializeDragDrop();

            // AboutPanel 事件订阅
            if (window.AboutPanel != null)
            {
                window.AboutPanel.CloseRequested += (s, e) =>
                {
                    if (window.AboutOverlay != null) window.AboutOverlay.Visibility = System.Windows.Visibility.Collapsed;
                };
            }

            // 初始化 LayoutMode (订阅消息)
            window.InitializeLayoutMode();

            // 初始化服务事件 (需要所有服务已就绪)
            window.InitializeServiceEvents();
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
                        window._secondFileInfoService?.ShowFileInfo(msg.Item);
                    else
                        window._fileInfoService?.ShowFileInfo(msg.Item);
                });
            });

            _messageBus.Subscribe<ShowLibraryInfoMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (msg.Pane == PaneId.Second)
                        window._secondFileInfoService?.ShowLibraryInfo(msg.Library);
                    else
                        window._fileInfoService?.ShowLibraryInfo(msg.Library);
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

            // 3. 布局变更桥接 (从 MainWindow.LayoutMode.cs 迁移)
            _messageBus.Subscribe<LayoutModeChangedMessage>(m =>
            {
                window.Dispatcher.Invoke(() => window.UpdateTabManagerMargin());
            });

            _messageBus.Subscribe<DualListModeChangedMessage>(m =>
            {
                window.Dispatcher.Invoke(() => window.SetDualListMode(m.IsEnabled));
            });

            _messageBus.Subscribe<FocusedPaneChangedMessage>(m =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    window.UpdateFocusBorders();
                    if (m.IsSecondPaneFocused) window.SecondFileBrowser?.FilesList?.Focus();
                    else window.FileBrowser?.FilesList?.Focus();
                });
            });

            _messageBus.Subscribe<NavigationModeChangedMessage>(m =>
            {
                window.Dispatcher.Invoke(() => window.SwitchNavigationMode(m.Mode));
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
                window.LoadCurrentDirectory,
                path => window.CreateTab(path, true)
            );
            window._previewService = _previewService;

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
            // 模式迁移：将原来的 MainWindowInitializer 逻辑完全集成
            var config = ConfigurationService.Instance.Config;

            // 1. 初始化窗口状态管理器 (WindowStateManager)
            // 1. 初始化窗口状态管理器 (WindowStateManager)
            // 已在 InitializeHandlers 中手动创建并赋值
            var windowStateManager = window._windowStateManager;


            // 2. 恢复窗口和布局状态
            windowStateManager?.RestoreAllState();

            // 3. 加载初始数据
            // 加载库列表
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
                window._navigationModeService?.SwitchNavigationMode(config.LastNavigationMode, skipRefresh: true);
            }

            // 恢复标签页
            windowStateManager?.RestoreTabsState();

            // 5. 强制修正布局 (解决 Star/Pixel 转换问题)
            window.Dispatcher.Invoke(() =>
            {
                window._windowLifecycleHandler?.AdjustColumnWidths();
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            // 6. 启动后台索引
            _serviceProvider.GetService<IFullTextSearchService>()?.StartBackgroundIndexing();

            await Task.CompletedTask;
        }
    }
}
