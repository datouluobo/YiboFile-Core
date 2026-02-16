using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Modules;
using YiboFile.Services.Config;
using YiboFile.Services.Navigation;
using YiboFile.Services.Search;
using YiboFile.Services.Tabs;
using YiboFile.Services.FileList;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Features;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.QuickAccess;
using YiboFile.Handlers;
using YiboFile.Models.Navigation;

namespace YiboFile.Services.Orchestration
{
    /// <summary>
    /// 窗口编排器实现类
    /// 将业务逻辑从 MainWindow 剥离到独立服务中
    /// 
    /// 职责（仅编排序列，具体逻辑委托给子初始化器）：
    /// 1. 服务获取与初始化
    /// 2. 委托 ModuleInitializer 创建 MVVM 模块
    /// 3. 委托 HandlerInitializer 挂载事件处理器
    /// 4. 委托 MessageBridgeSetup 配置消息桥接
    /// 5. 应用初始状态恢复
    /// 
    /// 拆分自原 981 行的单体实现：
    /// - ModuleInitializer.cs   — MVVM 模块创建与注册
    /// - HandlerInitializer.cs  — 事件处理器挂载
    /// - MessageBridgeSetup.cs  — 消息桥接配置
    /// </summary>
    public class WindowOrchestrator : IWindowOrchestrator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBus _messageBus;

        // 子初始化器
        private readonly ModuleInitializer _moduleInitializer;
        private readonly HandlerInitializer _handlerInitializer;
        private readonly MessageBridgeSetup _messageBridgeSetup;

        // 缓存的服务引用
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
        private QuickAccessService _quickAccessService;
        private FileListService _secondFileListService;
        private FileSystemWatcherService _fileSystemWatcherService;
        private ColumnService _columnService;
        private Services.FileInfo.FileInfoService _fileInfoService;
        private Services.FileInfo.FileInfoService _secondFileInfoService;

        // ViewModel 和模块引用（由 ModuleInitializer 填充）
        private MainWindowViewModel _viewModel;
        private LayoutModule _layoutModule;
        private NavigationModeService _navigationModeService;

        // WindowStateManager（由 HandlerInitializer 创建）
        private WindowStateManager _windowStateManager;

        public WindowOrchestrator(IServiceProvider serviceProvider, IMessageBus messageBus)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            _moduleInitializer = new ModuleInitializer(serviceProvider, messageBus);
            _handlerInitializer = new HandlerInitializer(serviceProvider, messageBus);
            _messageBridgeSetup = new MessageBridgeSetup(serviceProvider, messageBus);
        }

        #region IWindowOrchestrator — 公共属性

        public MainWindowViewModel ViewModel => _viewModel;

        // Handler 属性（委托给 HandlerInitializer）
        public WindowLifecycleHandler LifecycleHandler => _handlerInitializer.LifecycleHandler;
        public Settings.SettingsOverlayController SettingsController => _handlerInitializer.SettingsController;
        public ColumnInteractionHandler ColumnInteractionHandler => _handlerInitializer.ColumnInteractionHandler;
        public ColumnInteractionHandler SecondColumnInteractionHandler => _handlerInitializer.SecondColumnInteractionHandler;
        public FileListEventHandler MainFileListHandler => _handlerInitializer.MainFileListHandler;
        public FileListEventHandler SecondFileListHandler => _handlerInitializer.SecondFileListHandler;
        public FileOperationHandler FileOperationHandler => _handlerInitializer.FileOperationHandler;
        public KeyboardEventHandler KeyboardEventHandler => _handlerInitializer.KeyboardEventHandler;
        public Services.UIHelper.IUIHelperService UIHelperService => _handlerInitializer.UIHelperService;

        // 服务属性
        public FileOperationService FileOperationService => _fileOperationService;
        public NavigationModeService NavigationModeService => _navigationModeService;
        public NavigationCoordinator NavigationCoordinator => _navigationCoordinator;
        public NavigationService NavigationService => _navigationService;
        public TabService TabService => _tabService;
        public TabService SecondTabService => _secondTabService;
        public LibraryService LibraryService => _libraryService;
        public Favorite.FavoriteService FavoriteService => _favoriteService;
        public QuickAccessService QuickAccessService => _quickAccessService;
        public FileListService FileListService => _fileListService;
        public FileListService SecondFileListService => _secondFileListService;
        public SearchService SearchService => _searchService;
        public SearchCacheService SearchCacheService => _searchCacheService;
        public FileSystemWatcherService FileSystemWatcherService => _fileSystemWatcherService;
        public WindowStateManager WindowStateManager => _handlerInitializer.WindowStateManager;
        public ColumnService ColumnService => _columnService;
        public Services.FileInfo.FileInfoService SecondFileInfoService => _secondFileInfoService;

        #endregion

        #region IWindowOrchestrator — 初始化序列

        public async Task InitializeAsync(MainWindow window)
        {
            try
            {
                // 按照规范顺序执行初始化
                InitializeServices(window);

                // 消息桥接（先于模块，因为 PreviewService 需要在模块创建前可用）
                _previewService = _messageBridgeSetup.SetupMessageSubscriptions(
                    window,
                    _navigationCoordinator,
                    _fileOperationService,
                    _fileInfoService,
                    _secondFileInfoService,
                    _viewModel);

                // MVVM 模块创建
                InitializeMvvmModules(window);

                // 服务事件桥接（将 Service event → MessageBus）
                _messageBridgeSetup.SetupServiceBridges(
                    _navigationService,
                    _fileListService,
                    _secondFileListService,
                    _fileSystemWatcherService,
                    _favoriteService);

                // 事件处理器挂载
                InitializeHandlers(window);

                // 应用初始状态
                await ApplyInitialStateAsync(window);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"初始化失败: {ex.Message}\n{ex.StackTrace}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"[WindowOrchestrator] Initialization Failed: {ex}");
            }
        }

        public void InitializeServices(MainWindow window)
        {
            // 从 DI 容器获取单例/瞬时服务
            _navigationCoordinator = _serviceProvider.GetRequiredService<NavigationCoordinator>();
            _navigationService = _serviceProvider.GetRequiredService<NavigationService>();
            _libraryService = _serviceProvider.GetRequiredService<LibraryService>();
            _favoriteService = _serviceProvider.GetRequiredService<Favorite.FavoriteService>();
            _fileListService = _serviceProvider.GetRequiredService<FileListService>();
            _tagService = _serviceProvider.GetService<ITagService>();
            _folderSizeCalculationService = _serviceProvider.GetRequiredService<FolderSizeCalculationService>();
            _searchCacheService = _serviceProvider.GetRequiredService<SearchCacheService>();
            _searchService = _serviceProvider.GetRequiredService<SearchService>();
            _fileOperationService = _serviceProvider.GetRequiredService<FileOperationService>();
            _fileSystemWatcherService = _serviceProvider.GetRequiredService<FileSystemWatcherService>();
            _quickAccessService = _serviceProvider.GetRequiredService<QuickAccessService>();
            _secondFileListService = _serviceProvider.GetRequiredService<FileListService>();
            _columnService = _serviceProvider.GetRequiredService<ColumnService>();

            // 为两个面板创建独立的服务实例
            _tabService = _serviceProvider.GetRequiredService<TabService>();
            _tabService.Pane = PaneId.Main;

            _secondTabService = _serviceProvider.GetRequiredService<TabService>();
            _secondTabService.Pane = PaneId.Second;

            // 初始化协调器关系
            _navigationCoordinator.Initialize(
                _tabService,
                _secondTabService,
                _navigationService,
                _libraryService,
                (paneId) => paneId == PaneId.Main ? _viewModel?.PrimaryPane : _viewModel?.SecondaryPane);

            // 初始化列管理服务
            _columnService.Initialize(() => window.GetCurrentModeKey());

            // 设置 NavigationService 并绑定 UIHelper
            _navigationService.UIHelper = new YiboFile.Helpers.NavigationUIHelper(window);

            // 初始化 FileInfoService
            _fileInfoService = new Services.FileInfo.FileInfoService(
                window.FileBrowser,
                _fileListService,
                _navigationCoordinator,
                _tagService);

            if (window.SecondFileBrowser != null)
            {
                _secondFileInfoService = new Services.FileInfo.FileInfoService(
                    window.SecondFileBrowser,
                    _secondFileListService,
                    _navigationCoordinator,
                    _tagService);
            }
        }

        public void InitializeMvvmModules(MainWindow window)
        {
            _moduleInitializer.Initialize(
                window,
                _navigationCoordinator,
                _navigationService,
                _tabService,
                _secondTabService,
                _libraryService,
                _searchService,
                _searchCacheService,
                _fileListService,
                _fileOperationService,
                _folderSizeCalculationService,
                _previewService);

            // 从初始化器中获取结果
            _viewModel = _moduleInitializer.ViewModel;
            _layoutModule = _moduleInitializer.LayoutModule;
            _navigationModeService = _moduleInitializer.NavigationModeService;
        }

        public void InitializeHandlers(MainWindow window)
        {
            _handlerInitializer.Initialize(
                window,
                _navigationCoordinator,
                _navigationService,
                _navigationModeService,
                _tabService,
                _secondTabService,
                _fileListService,
                _fileOperationService,
                _columnService,
                _layoutModule,
                _viewModel,
                _quickAccessService,
                _libraryService,
                _secondFileInfoService,
                _searchCacheService);
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
            _handlerInitializer.WindowStateManager?.RestoreAllState();

            // 2. 加载初始数据
            _libraryService?.LoadLibraries();

            // 加载快速访问列表
            if (_quickAccessService != null && window.QuickAccessListBox != null)
            {
                _quickAccessService.LoadQuickAccess(window.QuickAccessListBox);
            }

            // 加载驱动器列表
            window.LoadDrives();

            // 4. 恢复最后的状态 (导航模式等)
            if (!string.IsNullOrEmpty(config.LastNavigationMode))
            {
                _navigationModeService?.SwitchNavigationMode(config.LastNavigationMode, skipRefresh: true);
            }

            // 恢复标签页
            _handlerInitializer.WindowStateManager?.RestoreTabsState();

            // 5. 强制修正布局
            window.Dispatcher.Invoke(() =>
            {
                _handlerInitializer.LifecycleHandler?.AdjustColumnWidths();
            }, System.Windows.Threading.DispatcherPriority.Loaded);

            // 6. 启动后台索引
            _serviceProvider.GetService<IFullTextSearchService>()?.StartBackgroundIndexing();

            // 7. 初始化 UI 事件
            window.InitializeEvents();
            window.InitializeServiceEvents();

            await Task.CompletedTask;
        }

        #endregion
    }
}
