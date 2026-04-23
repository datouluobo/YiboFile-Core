using System;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Modules;
using YiboFile.Services.Config;
using YiboFile.Services.UI.Adapters;
using YiboFile.Services.Navigation;
using YiboFile.Services.Tabs;
using YiboFile.Services.FileList;
using YiboFile.Services.FileOperations;
using YiboFile.Services.FileOperations.Undo;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.QuickAccess;
using YiboFile.Models.Navigation;
using YiboFile.Services; // LibraryService
using YiboFile.Services.FileInfo; // FileInfoService
using YiboFile.Services.Search; // SearchCacheService

namespace YiboFile.Services.Orchestration
{
    /// <summary>
    /// 事件处理器初始化器
    /// 负责创建和挂载所有 Handler（键盘、鼠标、文件列表、布局等）
    /// 从 WindowOrchestrator 中拆分，降低单文件复杂度
    /// </summary>
    internal class HandlerInitializer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBus _messageBus;
        private readonly YiboFile.Services.Theming.IThemeService _themeService;

        // 初始化结果（供 WindowOrchestrator 读取）
        public Handlers.WindowLifecycleHandler LifecycleHandler { get; private set; }

        /// <summary>每个面板的 ColumnInteractionHandler（索引 0=左, 1=右）</summary>
        public Handlers.ColumnInteractionHandler[] ColumnHandlers { get; } = new Handlers.ColumnInteractionHandler[2];
        /// <summary>每个面板的 FileListEventHandler</summary>
        public Handlers.FileListEventHandler[] FileListHandlers { get; } = new Handlers.FileListEventHandler[2];

        // 兼容属性（向后兼容，指向数组元素）
        public Handlers.ColumnInteractionHandler ColumnInteractionHandler => ColumnHandlers[0];
        public Handlers.ColumnInteractionHandler SecondColumnInteractionHandler => ColumnHandlers[1];
        public Handlers.FileListEventHandler MainFileListHandler => FileListHandlers[0];
        public Handlers.FileListEventHandler SecondFileListHandler => FileListHandlers[1];

        public Handlers.KeyboardEventHandler KeyboardEventHandler { get; private set; }
        public WindowStateManager WindowStateManager { get; private set; }
        public Services.UIHelper.IUIHelperService UIHelperService { get; private set; }

        public HandlerInitializer(YiboFile.Services.Theming.IThemeService themeService, IServiceProvider serviceProvider, IMessageBus messageBus)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        /// <summary>
        /// 初始化所有事件处理器
        /// </summary>
        public void Initialize(
            MainWindow window,
            INavigationCoordinator navigationCoordinator,
            NavigationService navigationService,
            NavigationModeService navigationModeService,
            TabService tabService,
            TabService secondTabService,
            FileListService fileListService,
            FileOperationService fileOperationService,
            ColumnService columnService,
            LayoutModule layoutModule,
            MainWindowViewModel viewModel,
            QuickAccessService quickAccessService,
            LibraryService libraryService,
            FileInfoService secondFileInfoService,
            SearchCacheService searchCacheService)
        {
            InitializeInfrastructure(
                window,
                navigationCoordinator,
                navigationService,
                navigationModeService,
                tabService,
                secondTabService,
                columnService,
                layoutModule,
                libraryService,
                secondFileInfoService,
                searchCacheService);
            InitializeOverlays(window, layoutModule, viewModel);
            InitializeInputHandlers(window, navigationCoordinator, navigationService, navigationModeService, tabService, secondTabService, layoutModule, viewModel);
            InitializeFileListHandlers(window, navigationCoordinator, navigationModeService, columnService, viewModel);
            InitializeSupportHandlers(
                window,
                fileOperationService,
                columnService,
                secondTabService,
                navigationCoordinator,
                libraryService,
                navigationService,
                fileListService);
            InitializeNavigationPanelHandlers(window, navigationCoordinator, viewModel);
            InitializeFileBrowserEvents(window, navigationCoordinator);
            InitializeThemeHandlers(window, quickAccessService, fileListService, viewModel);

            // 初始化服务事件 (需要所有服务已就绪)
            window.InitializeServiceEvents();
        }

        private void InitializeInfrastructure(
            MainWindow window,
            INavigationCoordinator navigationCoordinator,
            NavigationService navigationService,
            NavigationModeService navigationModeService,
            TabService tabService,
            TabService secondTabService,
            ColumnService columnService,
            LayoutModule layoutModule,
            LibraryService libraryService,
            FileInfoService secondFileInfoService,
            SearchCacheService searchCacheService)
        {
            // 初始化事件桥接服务
            var eventBridgeService = new Services.UI.EventBridgeService(window, _messageBus);

            // 获取必要的服务
            var configUIHelper = new ConfigUIAdapter(window);
            UIHelperService = new Services.UIHelper.UIHelperService(window.FileBrowser, window.Dispatcher);
            var dialogService = _serviceProvider.GetService<Services.UI.IDialogService>();

            WindowStateManager = new WindowStateManager(
                configUIHelper,
                tabService,
                navigationService,
                navigationModeService,
                secondTabService,
                _serviceProvider.GetService<YiboFile.Services.Data.Repositories.ILibraryRepository>()
            );

            // LayoutEventHandler (Initialize first to set up UI state like Dual List)
            var layoutHandler = new Handlers.LayoutEventHandler(
                window,
                _messageBus,
                layoutModule,
                navigationModeService,
                secondTabService,
                WindowStateManager,
                navigationCoordinator,
                searchCacheService,
                secondFileInfoService,
                libraryService,
                dialogService
            );
            layoutHandler.Initialize();
            window._layoutEventHandler = layoutHandler;
        }

        private void InitializeOverlays(MainWindow window, LayoutModule layoutModule, MainWindowViewModel viewModel)
        {
            // Subscribe to Settings messages
            _messageBus.Subscribe<ShowSettingsMessage>(msg =>
                window.Dispatcher.Invoke(() =>
                    _messageBus.Publish(new OpenContentTabMessage(TabContentTypes.Settings))));

            // Subscribe to About messages
            _messageBus.Subscribe<ShowAboutMessage>(msg =>
                window.Dispatcher.Invoke(() =>
                    _messageBus.Publish(new OpenContentTabMessage(TabContentTypes.About))));


        }

        private void InitializeInputHandlers(
            MainWindow window,
            INavigationCoordinator navigationCoordinator,
            NavigationService navigationService,
            NavigationModeService navigationModeService,
            TabService tabService,
            TabService secondTabService,
            LayoutModule layoutModule,
            MainWindowViewModel viewModel)
        {
            // 1. KeyboardEventHandler
            var keyboardHandler = new Handlers.KeyboardEventHandler(
                window.FileBrowser,
                () => window.GetActivePaneId() == PaneId.Second ? window.SecondFileBrowser : window.FileBrowser,
                () => window.GetActivePaneId() == PaneId.Second ? secondTabService : tabService,
                tab => (window.GetActivePaneId() == PaneId.Second ? secondTabService : tabService).RemoveTab(tab),
                path => navigationCoordinator.HandlePathNavigation(path, NavigationSource.External, ClickType.LeftClick, pane: window.GetActivePaneId()),
                tab => (window.GetActivePaneId() == PaneId.Second ? secondTabService : tabService).SetActiveTab(tab),
                () => viewModel?.ActivePane?.Commands?.NewFolderCommand?.Execute(null),
                path => navigationCoordinator.HandlePathNavigation(path, NavigationSource.External, ClickType.LeftClick, pane: window.GetActivePaneId()),
                mode => navigationModeService?.SwitchNavigationMode(mode),
                () => viewModel?.ActivePane?.NavigationMode == "Library",
                () => { if (navigationService?.CanNavigateBack == true) navigationService.NavigateBack(); },
                messageBus: _messageBus
            );
            KeyboardEventHandler = keyboardHandler;

            // 4. MouseEventHandler
            var mouseHandler = new Handlers.MouseEventHandler(
                () => window.WindowMaximize_Click(null, null),
                () => window.DragMove(),
                () => window.NavigationPanelControl?.QuickAccessListBox,
                navigationCoordinator,
                fav => navigationCoordinator.HandleFavoriteNavigation(fav, ClickType.LeftClick, window.GetActivePaneId()),
                path => navigationCoordinator.HandlePathNavigation(path, NavigationSource.QuickAccess, ClickType.LeftClick, pane: window.GetActivePaneId()),
                () => window.GetActivePaneId()
            );

            // Hook listbox events to decentralized handler
            if (window.NavigationPanelControl != null)
            {
                // QuickAccess handled by NavigationPanelControl internally now (Fix BUG-SimultaneousNav)
                // if (window.NavigationPanelControl.QuickAccessListBoxControl != null)
                // {
                //    window.NavigationPanelControl.QuickAccessListBoxControl.PreviewMouseDown += mouseHandler.QuickAccessListBox_PreviewMouseDown;
                // }
            }

            // Global Mouse Down for handling focus/edit mode logic outside controls
            window.PreviewMouseDown += (s, e) =>
            {
                mouseHandler.HandleGlobalMouseDown(s, e, window.SecondFileBrowser);
            };
        }

        private void InitializeFileListHandlers(
            MainWindow window,
            INavigationCoordinator navigationCoordinator,
            NavigationModeService navigationModeService,
            ColumnService columnService,
            MainWindowViewModel viewModel)
        {
            // 面板 FileBrowser 数组：[0]=主, [1]=副
            var browsers = new[] { window.FileBrowser, window.SecondFileBrowser };
            var paneIds = new[] { PaneId.Main, PaneId.Second };

            for (int i = 0; i < browsers.Length; i++)
            {
                if (browsers[i] == null) continue;

                // ColumnInteractionHandler
                var colHandler = new Handlers.ColumnInteractionHandler(browsers[i], columnService);
                colHandler.Initialize();
                colHandler.HookHeaderThumbs();
                ColumnHandlers[i] = colHandler;

                // FileListEventHandler
                var flHandler = new Handlers.FileListEventHandler(
                    browsers[i],
                    navigationCoordinator,
                    navigationModeService,
                    window,
                    paneIds[i]
                );
                flHandler.Initialize(browsers[i].FilesList);
                FileListHandlers[i] = flHandler;
            }
        }

        private void InitializeSupportHandlers(
            MainWindow window,
            FileOperationService fileOperationService,
            ColumnService columnService,
            TabService secondTabService,
            INavigationCoordinator navigationCoordinator,
            LibraryService libraryService,
            NavigationService navigationService,
            FileListService fileListService)
        {
            var undoService = _serviceProvider.GetService<UndoService>();

            // 5. WindowLifecycleHandler
            LifecycleHandler = new Handlers.WindowLifecycleHandler(window, WindowStateManager, columnService);

            // Shell Menu Handler
            var shellMenuHandler = new Handlers.ShellMenuHandler(_messageBus);

            // 9. LibraryEventHandler
            window._libraryEventHandler = new Handlers.LibraryEventHandler(
                window,
                libraryService,
                navigationCoordinator,
                navigationService,
                fileListService,
                columnService,
                _serviceProvider.GetService<Services.UI.IDialogService>()
            );
            window._libraryEventHandler.Initialize();



            // 初始化拖放
            window._dragDropEventHandler = new Handlers.DragDropEventHandler(
                window,
                navigationCoordinator,
                window._libraryEventHandler,
                secondTabService);
            window._dragDropEventHandler.Initialize();
        }

        private void InitializeNavigationPanelHandlers(MainWindow window, INavigationCoordinator navigationCoordinator, MainWindowViewModel viewModel)
        {
            if (window.NavigationPanelControl == null) return;

            window.NavigationPanelControl.LibraryManageClick += (s, e) =>
            {
                _messageBus.Publish(new OpenContentTabMessage(YiboFile.Services.Tabs.TabContentTypes.Management));
            };

            window.NavigationPanelControl.PathManageClick += (s, e) =>
            {
                _messageBus.Publish(new OpenContentTabMessage(YiboFile.Services.Tabs.TabContentTypes.Management));
            };

            // 订阅收藏列表加载事件，挂载拖拽排序和外部拖入逻辑
            window.NavigationPanelControl.FavoriteListBoxLoaded += (s, listBox) =>
            {
                var favService = _serviceProvider.GetService<Favorite.FavoriteService>();
                favService?.ConfigureListBoxEvents(listBox);
            };

            window.NavigationPanelControl.FavoriteGroupHeaderLoaded += (s, grid) =>
            {
                var favService = _serviceProvider.GetService<Favorite.FavoriteService>();
                favService?.ConfigureGroupHeaderEvents(grid);
            };

            if (window.NavigationPanelControl.TagBrowsePanelControl != null)
            {
                window.NavigationPanelControl.TagBrowsePanelControl.TagClicked += (tagId, tagName) =>
                {
                    if (string.IsNullOrEmpty(tagName)) return;
                    _ = navigationCoordinator?.NavigateAsync(new NavigationRequest
                    {
                        Target = NavigationTarget.FromTag(tagName),
                        Pane = window.GetActivePaneId(),
                        Source = NavigationSource.SidebarTag
                    });
                };
                window.NavigationPanelControl.TagBrowsePanelControl.TagMiddleClicked += (tagId, tagName) =>
                {
                    if (string.IsNullOrEmpty(tagName)) return;
                    var path = $"tag://{tagName}";
                    if (window.NavigationPanelControl.OpenInNewTabCommand?.CanExecute(path) == true)
                    {
                        window.NavigationPanelControl.OpenInNewTabCommand.Execute(path);
                    }
                };
                window.NavigationPanelControl.TagBrowsePanelControl.BackRequested += (s, e) =>
                {
                    viewModel?.Navigation?.NavigateBackCommand?.Execute(null);
                };
            }
        }

        private void InitializeFileBrowserEvents(MainWindow window, INavigationCoordinator navigationCoordinator)
        {
            if (window.FileBrowser != null)
            {
                window.FileBrowser.PathChanged += (s, path) => navigationCoordinator?.HandlePathNavigation(path, NavigationSource.AddressBar, ClickType.LeftClick, pane: PaneId.Main);
                window.FileBrowser.BreadcrumbClicked += (s, path) => navigationCoordinator?.HandlePathNavigation(path, NavigationSource.Breadcrumb, ClickType.LeftClick, pane: PaneId.Main);
            }

            if (window.SecondFileBrowser != null)
            {
                window.SecondFileBrowser.PathChanged += (s, path) => navigationCoordinator?.HandlePathNavigation(path, NavigationSource.AddressBar, ClickType.LeftClick, pane: PaneId.Second);
                window.SecondFileBrowser.BreadcrumbClicked += (s, path) => navigationCoordinator?.HandlePathNavigation(path, NavigationSource.Breadcrumb, ClickType.LeftClick, pane: PaneId.Second);
            }
        }

        private void InitializeThemeHandlers(
            MainWindow window,
            QuickAccessService quickAccessService,
            FileListService fileListService,
            MainWindowViewModel viewModel)
        {
            // 订阅主题切换事件,刷新导航面板图标 + 动态切换 Mica
            _themeService.ThemeChanged += (s, e) =>
            {
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 重新加载快速访问、驱动器和收藏列表以刷新图标
                        if (window.QuickAccessListBox != null)
                            quickAccessService?.LoadQuickAccess(window.QuickAccessListBox);
                        if (window.DrivesTreeView != null)
                            quickAccessService?.LoadDriveTree(window.DrivesTreeView, fileListService.FormatFileSize);
                        viewModel?.Favorites?.LoadFavorites();
                    }
                    catch (Exception) { }

                    // 修复：切换主题时，如果有副列表，强制刷新布局以防止地址栏错位
                    if (window.IsDualPaneMode && window.SecondFileBrowserContainer != null)
                    {
                        window.SecondFileBrowserContainer.InvalidateVisual();
                        window.SecondFileBrowserContainer.UpdateLayout();
                    }

                    // 📌 Mica 动态控制核心：根据当前主题 ID 切换 DWM 玻璃特效
                    try
                    {
                        if (e.NewTheme?.Id == "Win11Pro")
                        {
                            window.EnableMicaBackdrop();
                        }
                        else
                        {
                            window.DisableMicaBackdrop();
                        }
                    }
                    catch (Exception micaEx)
                    {
                        Services.Core.FileLogger.Log($"[Mica] Theme switch error: {micaEx.Message}");
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }
    }
}

