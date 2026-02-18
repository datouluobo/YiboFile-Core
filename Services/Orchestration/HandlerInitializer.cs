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

        // 初始化结果（供 WindowOrchestrator 读取）
        public Handlers.WindowLifecycleHandler LifecycleHandler { get; private set; }
        public Settings.SettingsOverlayController SettingsController { get; private set; }
        public Handlers.ColumnInteractionHandler ColumnInteractionHandler { get; private set; }
        public Handlers.ColumnInteractionHandler SecondColumnInteractionHandler { get; private set; }
        public Handlers.FileListEventHandler MainFileListHandler { get; private set; }
        public Handlers.FileListEventHandler SecondFileListHandler { get; private set; }
        public Handlers.FileOperationHandler FileOperationHandler { get; private set; }
        public Handlers.KeyboardEventHandler KeyboardEventHandler { get; private set; }
        public WindowStateManager WindowStateManager { get; private set; }
        public Services.UIHelper.IUIHelperService UIHelperService { get; private set; }

        public HandlerInitializer(IServiceProvider serviceProvider, IMessageBus messageBus)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        /// <summary>
        /// 初始化所有事件处理器
        /// </summary>
        public void Initialize(
            MainWindow window,
            NavigationCoordinator navigationCoordinator,
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
            NavigationCoordinator navigationCoordinator,
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
                libraryService
            );
            layoutHandler.Initialize();
            window._layoutEventHandler = layoutHandler;
        }

        private void InitializeOverlays(MainWindow window, LayoutModule layoutModule, MainWindowViewModel viewModel)
        {
            // Initialize Settings Controller
            var settingsOverlay = window.FindName("SettingsOverlay") as System.Windows.Controls.Grid;
            var settingsPanel = window.FindName("SettingsPanel") as Controls.SettingsPanelControl;
            var rightPanel = window.FindName("RightPanel") as System.Windows.UIElement;
            if (settingsOverlay != null && settingsPanel != null)
            {
                SettingsController = new Settings.SettingsOverlayController(
                    settingsOverlay,
                    settingsPanel,
                    rightPanel,
                    (cfg) => { /* Auto-handled */ }
                );

                // Subscribe to Settings messages
                _messageBus.Subscribe<ShowSettingsMessage>(msg => window.Dispatcher.Invoke(() => SettingsController?.Show()));
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
                    if (layoutModule != null)
                    {
                        layoutModule.ActiveSpecialPanel = "None";
                        layoutModule.IsMainLayoutVisible = true;
                    }

                    // 2. Trigger Paste in Active Pane
                    window.Dispatcher.InvokeAsync(() =>
                    {
                        // Restore focus
                        if (layoutModule?.IsSecondPaneFocused == true)
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
                        viewModel?.FileOperation?.PasteCommand?.Execute(viewModel.ActivePane);
                    });
                };
            }
        }

        private void InitializeInputHandlers(
            MainWindow window,
            NavigationCoordinator navigationCoordinator,
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
                () => window.CloseOverlays(),
                () => { if (navigationService?.CanNavigateBack == true) navigationService.NavigateBack(); },
                messageBus: _messageBus,
                switchLayoutMode: index => window.SwitchLayoutModeByIndex(index),
                isDualListMode: () => layoutModule?.IsDualListMode ?? false,
                switchDualPaneFocus: () => layoutModule?.SwitchFocusedPane()
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
            NavigationCoordinator navigationCoordinator,
            NavigationModeService navigationModeService,
            ColumnService columnService,
            MainWindowViewModel viewModel)
        {
            // 2. ColumnInteractionHandler (主面板)
            var mainColumnHandler = new Handlers.ColumnInteractionHandler(window.FileBrowser, columnService);
            mainColumnHandler.Initialize();
            mainColumnHandler.HookHeaderThumbs();

            // 3. ColumnInteractionHandler (副面板)
            Handlers.ColumnInteractionHandler secondColumnHandler = null;
            if (window.SecondFileBrowser != null)
            {
                secondColumnHandler = new Handlers.ColumnInteractionHandler(window.SecondFileBrowser, columnService);
                secondColumnHandler.Initialize();
                secondColumnHandler.HookHeaderThumbs();
            }

            ColumnInteractionHandler = mainColumnHandler;
            SecondColumnInteractionHandler = secondColumnHandler;

            // 7. FileListEventHandler (主面板)
            MainFileListHandler = new Handlers.FileListEventHandler(
                window.FileBrowser,
                navigationCoordinator,
                navigationModeService,
                window,
                PaneId.Main
            );
            MainFileListHandler.Initialize(window.FileBrowser.FilesList);

            // 8. FileListEventHandler (副面板)
            if (window.SecondFileBrowser != null)
            {
                SecondFileListHandler = new Handlers.FileListEventHandler(
                    window.SecondFileBrowser,
                    navigationCoordinator,
                    navigationModeService,
                    window,
                    PaneId.Second
                );
                SecondFileListHandler.Initialize(window.SecondFileBrowser.FilesList);
            }
        }

        private void InitializeSupportHandlers(
            MainWindow window,
            FileOperationService fileOperationService,
            ColumnService columnService,
            TabService secondTabService,
            NavigationCoordinator navigationCoordinator,
            LibraryService libraryService,
            NavigationService navigationService,
            FileListService fileListService)
        {
            var undoService = _serviceProvider.GetService<UndoService>();

            // 5. WindowLifecycleHandler
            LifecycleHandler = new Handlers.WindowLifecycleHandler(window, WindowStateManager, columnService);

            // 9. LibraryEventHandler (Create first for FileOperationHandler dependency)
            window._libraryEventHandler = new Handlers.LibraryEventHandler(
                window,
                libraryService,
                navigationCoordinator,
                navigationService,
                fileListService,
                columnService
            );
            window._libraryEventHandler.Initialize();

            // 6. FileOperationHandler
            FileOperationHandler = new Handlers.FileOperationHandler(
                window,
                undoService,
                navigationCoordinator,
                window._libraryEventHandler,
                fileOperationService);

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
            window._dragDropEventHandler = new Handlers.DragDropEventHandler(
                window,
                navigationCoordinator,
                window._libraryEventHandler,
                secondTabService);
            window._dragDropEventHandler.Initialize();
        }

        private void InitializeNavigationPanelHandlers(MainWindow window, NavigationCoordinator navigationCoordinator, MainWindowViewModel viewModel)
        {
            if (window.NavigationPanelControl == null) return;

            window.NavigationPanelControl.LibraryManageClick += (s, e) =>
            {
                var settingsWindow = new YiboFile.Windows.NavigationSettingsWindow("Library");
                settingsWindow.Owner = window;
                settingsWindow.ShowDialog();
            };

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
                    _ = navigationCoordinator?.NavigateAsync(new NavigationRequest
                    {
                        Target = NavigationTarget.FromTag(tagName),
                        Pane = window.GetActivePaneId(),
                        Source = NavigationSource.SidebarTag
                    });
                };
                window.NavigationPanelControl.TagBrowsePanelControl.BackRequested += (s, e) =>
                {
                    viewModel?.Navigation?.NavigateBackCommand?.Execute(null);
                };
            }
        }

        private void InitializeFileBrowserEvents(MainWindow window, NavigationCoordinator navigationCoordinator)
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
            // 订阅主题切换事件,刷新导航面板图标
            Services.Theming.ThemeManager.ThemeChanged += (s, e) =>
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
                    if (window.IsDualListMode && window.SecondFileBrowserContainer != null)
                    {
                        window.SecondFileBrowserContainer.InvalidateVisual();
                        window.SecondFileBrowserContainer.UpdateLayout();
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }
    }
}
