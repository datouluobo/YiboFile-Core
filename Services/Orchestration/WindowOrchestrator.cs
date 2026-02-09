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
            // 按照规范顺序执行初始化
            InitializeServices(window);
            InitializeMvvmModules(window);
            InitializeHandlers(window);

            // 部分窗口交互需要在 UI 渲染前准备，部分需要异步
            await ApplyInitialStateAsync(window);
        }

        public void InitializeServices(MainWindow window)
        {
            // 阶段 2 - 暂时保持 MainWindow.InitializeServices() 的调用
            // 未来版本会将服务创建逻辑完全迁移到此处
            // 目前仅缓存服务实例供 InitializeMvvmModules 使用

            _navigationCoordinator = window._navigationCoordinator;
            _navigationService = window._navigationService;
            _tabService = window._tabService;
            _secondTabService = window._secondTabService;
            _libraryService = window._libraryService;
            _favoriteService = window._favoriteService;
            _searchService = window._searchService;
            _searchCacheService = window._searchCacheService;
            _fileListService = window._fileListService;
            _fileOperationService = window._fileOperationService;
            _previewService = window._previewService;
            _folderSizeCalculationService = window._folderSizeCalculationService;
            _tagService = window._tagService;
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
                    window._tabService,
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
            window._navigationModule = _navigationModule;
            window._tabsModule = _tabsModule;
            window._fileListModule = _fileListModule;
            window._layoutModule = _layoutModule;
            window._fileOperationModule = _fileOperationModule;
            window._notesModule = _notesModule;
            window._tagsModule = _tagsModule;
            window._libraryModule = _libraryModule;
            window._searchModule = _searchModule;
            window._favoritesModule = _favoritesModule;
        }

        public void InitializeHandlers(MainWindow window)
        {
            // 初始化事件桥接服务
            _eventBridgeService = new Services.UI.EventBridgeService(window, _messageBus);

            // 阶段 2 - 暂时保持 MainWindow.InitializeHandlers() 的调用
            // 未来版本会将处理器逻辑迁移到消息订阅模式
        }

        public async Task ApplyInitialStateAsync(MainWindow window)
        {
            // 阶段 2 - 暂时保持 MainWindowInitializer.ApplyInitialState() 的调用
            // 未来版本会将状态恢复逻辑迁移到此处
            await Task.CompletedTask;
        }
    }
}
