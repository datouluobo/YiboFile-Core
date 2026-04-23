using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Config;
using YiboFile.Services.Navigation;
using YiboFile.Services.Tabs;
using YiboFile.Services.Search;
using YiboFile.Services.QuickAccess;
using YiboFile.ViewModels;

using YiboFile.Services.Features; // for IFullTextSearchService

namespace YiboFile.Services.Orchestration
{
    /// <summary>
    /// 状态恢复器
    /// 负责在应用程序启动时恢复先前的状态（布局、标签页、导航模式等）
    /// 从 WindowOrchestrator 中拆分，降低单文件复杂度
    /// </summary>
    internal class StateRestorer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly MainWindow _window;
        private readonly HandlerInitializer _handlerInitializer;
        private readonly NavigationModeService _navigationModeService;
        private readonly MainWindowViewModel _viewModel;
        private readonly TabService _tabService;
        private readonly TabService _secondTabService;
        private readonly LibraryService _libraryService; // Keep instance from orchestrator
        private readonly QuickAccessService _quickAccessService; // Keep instance from orchestrator

        public StateRestorer(
            IServiceProvider serviceProvider,
            MainWindow window,
            HandlerInitializer handlerInitializer,
            NavigationModeService navigationModeService,
            MainWindowViewModel viewModel,
            TabService tabService,
            TabService secondTabService,
            LibraryService libraryService,
            QuickAccessService quickAccessService)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _handlerInitializer = handlerInitializer ?? throw new ArgumentNullException(nameof(handlerInitializer));
            _navigationModeService = navigationModeService;
            _viewModel = viewModel;
            _tabService = tabService;
            _secondTabService = secondTabService;
            _libraryService = libraryService;
            _quickAccessService = quickAccessService;
        }

        public async Task RestoreStateAsync()
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
            if (_quickAccessService != null && _window.QuickAccessListBox != null)
            {
                _quickAccessService.LoadQuickAccess(_window.QuickAccessListBox);
            }

            // 加载驱动器列表
            _window.LoadDrives();

            // 4. 恢复最后的状态 (导航模式等)
            if (!string.IsNullOrEmpty(config?.LastNavigationMode))
            {
                _navigationModeService?.SwitchNavigationMode(config.LastNavigationMode, skipRefresh: true);
            }

            // 恢复标签页
            _handlerInitializer.WindowStateManager?.RestoreTabsState();

            // 5. 阻止在这里进行强制作战，让 Window 的真实 SizeChanged 自己处理，防止还原时 ActualWidth 未更新导致尺寸重置
            // (Removed premature AdjustColumnWidths)

            // 6. 启动后台索引
            _serviceProvider.GetService<IFullTextSearchService>()?.StartBackgroundIndexing();

            // 7. 初始化 UI 事件
            _window.InitializeEvents();
            _window.InitializeServiceEvents();

            // FIX for BUG-018: Force update info panels to avoid empty state
            if (_viewModel != null)
            {
                _viewModel.MainSelectionHandler?.HandleNoSelection(YiboFile.Services.Navigation.PaneId.Main);
                if (config?.IsDualPaneMode == true)
                {
                    _viewModel.SecondSelectionHandler?.HandleNoSelection(YiboFile.Services.Navigation.PaneId.Second);
                }
            }

            await Task.CompletedTask;
        }
    }
}
