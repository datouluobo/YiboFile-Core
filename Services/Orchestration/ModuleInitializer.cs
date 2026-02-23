using System;
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
using YiboFile.Models.Navigation;

namespace YiboFile.Services.Orchestration
{
    /// <summary>
    /// MVVM 模块初始化器
    /// 负责创建和注册所有 ViewModel 及 Module，设置 DataContext
    /// 从 WindowOrchestrator 中拆分，降低单文件复杂度
    /// </summary>
    internal class ModuleInitializer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBus _messageBus;

        // 初始化结果（供 WindowOrchestrator 读取）
        public MainWindowViewModel ViewModel { get; private set; }
        public NavigationModule NavigationModule { get; private set; }
        public TabsModule TabsModule { get; private set; }
        public FileListModule FileListModule { get; private set; }
        public LayoutModule LayoutModule { get; private set; }
        public FileOperationModule FileOperationModule { get; private set; }
        public NotesModule NotesModule { get; private set; }
        public TagsModule TagsModule { get; private set; }
        public LibraryModule LibraryModule { get; private set; }
        public SearchModule SearchModule { get; private set; }
        public FavoritesModule FavoritesModule { get; private set; }
        public NavigationModeService NavigationModeService { get; private set; }

        public ModuleInitializer(IServiceProvider serviceProvider, IMessageBus messageBus)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        /// <summary>
        /// 创建并注册所有 MVVM 模块
        /// </summary>
        public void Initialize(
            MainWindow window,
            NavigationCoordinator navigationCoordinator,
            NavigationService navigationService,
            TabService tabService,
            TabService secondTabService,
            LibraryService libraryService,
            SearchService searchService,
            SearchCacheService searchCacheService,
            FileListService fileListService,
            FileOperationService fileOperationService,
            FolderSizeCalculationService folderSizeCalculationService,
            Preview.PreviewService previewService)
        {
            // ========== 核心 ViewModel 创建 ==========

            // 创建 UI 适配器
            var configAdapter = new ConfigUIAdapter(window);
            var navAdapter = new NavigationModeUIAdapter(window);

            // 创建 NavigationModeService
            NavigationModeService = new NavigationModeService(
                    navAdapter,
                    navigationService,
                    tabService,
                    ConfigurationService.Instance);

            // 创建 RightPanelViewModel
            var rightPanelVM = new RightPanelViewModel(_messageBus, ConfigurationService.Instance, fileListService);

            // 创建主 ViewModel
            ViewModel = new MainWindowViewModel(
                _messageBus,
                rightPanelVM,
                previewService,
                fileListService,
                folderSizeCalculationService);

            // ========== 模块创建与注册 ==========

            // 导航模块
            NavigationModule = new NavigationModule(
                _messageBus,
                navigationService,
                navigationCoordinator,
                () => window.GetActivePaneId()); // 注入 Pane 解析器
            ViewModel.RegisterModule(NavigationModule);

            // 标签页模块
            var tabContentRegistry = _serviceProvider.GetService<TabContentRegistry>();
            TabsModule = new TabsModule(
                _messageBus,
                tabService,
                secondTabService,
                tabContentRegistry,
                () => window.IsDualListMode,
                () => window.GetActivePaneId() == PaneId.Second);
            ViewModel.RegisterModule(TabsModule);

            // 文件列表模块
            FileListModule = new FileListModule(_messageBus);
            ViewModel.RegisterModule(FileListModule);

            // 初始化主/副面板 ViewModel
            ViewModel.PrimaryPane = new PaneViewModel(window.Dispatcher, _messageBus) { IsActive = true };
            ViewModel.SecondaryPane = new PaneViewModel(window.Dispatcher, _messageBus, isSecondary: true)
            {
                IsActive = false,
                IsLoadingDisabled = false
            };

            // 关联模块到 ViewModel
            ViewModel.Navigation = NavigationModule;
            ViewModel.Tabs = TabsModule;

            // 布局模块
            LayoutModule = new LayoutModule(_messageBus);
            ViewModel.Layout = LayoutModule;
            ViewModel.RegisterModule(LayoutModule);

            // 初始化布局模块状态
            var cfg = ConfigurationService.Instance.Config;
            LayoutModule.InitializeState(
                cfg.LayoutMode ?? "Work",
                cfg.IsDualListMode,
                false,
                cfg.IsSidebarCollapsed,
                cfg.IsPreviewCollapsed);

            // 文件操作模块
            var undoService = _serviceProvider.GetService<UndoService>();
            var errorService = _serviceProvider.GetService<ErrorService>();
            FileOperationModule = new FileOperationModule(_messageBus, fileOperationService, undoService, errorService);
            ViewModel.FileOperation = FileOperationModule;
            ViewModel.RegisterModule(FileOperationModule);

            // 备注模块
            var notesService = _serviceProvider.GetService<Features.FileNotes.INotesService>();
            if (notesService != null)
            {
                NotesModule = new NotesModule(_messageBus, notesService);
                ViewModel.Notes = NotesModule;
                ViewModel.RegisterModule(NotesModule);
            }

            // 标签模块
            var tagService = _serviceProvider.GetService<ITagService>();
            if (tagService != null)
            {
                TagsModule = new TagsModule(_messageBus, tagService);
                ViewModel.Tags = TagsModule;
                ViewModel.RegisterModule(TagsModule);
            }

            // 库模块
            LibraryModule = new LibraryModule(_messageBus, libraryService);
            ViewModel.Library = LibraryModule;
            ViewModel.RegisterModule(LibraryModule);

            // 搜索模块
            SearchModule = new SearchModule(
                _messageBus,
                searchService,
                searchCacheService,
                tabService,
                _serviceProvider.GetService<IFullTextSearchService>(),
                secondTabService,
                () => window.IsDualListMode,
                () => window.GetActivePaneId() == PaneId.Second);
            ViewModel.Search = SearchModule;
            ViewModel.RegisterModule(SearchModule);

            // 收藏模块
            FavoritesModule = new FavoritesModule(_messageBus, _serviceProvider.GetRequiredService<Favorite.FavoriteService>());
            ViewModel.Favorites = FavoritesModule;
            ViewModel.RegisterModule(FavoritesModule);

            // 初始化所有模块
            ViewModel.InitializeModules();

            // 设置 DataContext
            window.DataContext = ViewModel;

            // 初始化 NavigationRail (侧边栏)
            InitializeNavigationRail(window, cfg);

            // 同步回 MainWindow 的字段引用（兼容过渡期）
            SyncModulesToWindow(window);
        }

        private void InitializeNavigationRail(MainWindow window, AppConfig cfg)
        {
            if (window.NavigationRail == null) return;

            var railVm = _serviceProvider.GetRequiredService<ViewModels.NavigationRailViewModel>();
            var railCoordinator = _serviceProvider.GetRequiredService<Controllers.NavigationRailCoordinator>();

            window.NavigationRail.ViewModel = railVm;
            window.NavigationRail.Coordinator = railCoordinator;

            // 同步当前状态到 Rail
            railCoordinator.SetNavigationMode(cfg.LastNavigationMode ?? "Path");
            railCoordinator.SetLayoutMode(cfg.LayoutMode ?? "Work");
            railVm.IsDualListMode = cfg.IsDualListMode;
        }

        /// <summary>
        /// 将模块引用同步回 MainWindow（过渡期兼容）
        /// </summary>
        private void SyncModulesToWindow(MainWindow window)
        {
            // 🔑 关键：设置 DataContext 以启用 XAML 数据绑定
            window.DataContext = ViewModel;

            window._viewModel = ViewModel;
            window._messageBus = _messageBus;
            window.InitializeMessageSubscriptions(); // 初始化 MainWindow 的消息订阅

            // 同步服务引用 (核心且必要的)
            window._navigationCoordinator = _serviceProvider.GetRequiredService<NavigationCoordinator>();
        }
    }
}
