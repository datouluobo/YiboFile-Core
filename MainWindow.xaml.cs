using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.ComponentModel;
using System.Threading;

using YiboFile.Services;
using YiboFile.Services.Search;
using YiboFile.Services.Navigation;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Favorite;
using YiboFile.Services.QuickAccess;
using YiboFile.Services.FileList;
using YiboFile.Services.Tabs;
using YiboFile.Services.Orchestration;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Config;
using YiboFile.Services.Features;
using YiboFile.Services.Core.Error;

using Microsoft.Extensions.DependencyInjection;

using YiboFile.Models.Navigation;
using YiboFile.Models.UI;
using YiboFile.Models;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Modules;
using YiboFile.Handlers;
using YiboFile.Controls; // For Controls
using YiboFile.Interfaces; // For IShellWindow
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace YiboFile
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window, IShellWindow
    {
        #region Windows 11 DWM Integration
        
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 仅设置深色标题栏标志，与所有深色主题兼容
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                int immersiveDarkMode = 1;
                DwmSetWindowAttribute(hwnd, 20, ref immersiveDarkMode, sizeof(int));
            }
            catch { }
        }

        /// <summary>
        /// 启用 Mica 视觉效果的兼容方法。
        /// 由于 .NET 8 WPF 框架限制（不支持透明 DirectX 交换链），
        /// 实际效果通过 Win11Pro 主题色板的高保真模拟实现。
        /// 保留此方法以兼容 HandlerInitializer 的调用。
        /// </summary>
        public void EnableMicaBackdrop()
        {
            // Win11Pro 主题的 Mica 模拟完全由 XAML 主题色板驱动，无需额外代码
            Services.Core.FileLogger.Log("[Mica] Simulated Mica activated via Win11Pro theme palette.");
        }

        /// <summary>
        /// 关闭 Mica 视觉效果的兼容方法。
        /// </summary>
        public void DisableMicaBackdrop()
        {
            Services.Core.FileLogger.Log("[Mica] Simulated Mica deactivated, theme switched.");
        }
        
        #endregion

        #region IShellWindow Implementation

        // 数组化面板访问
        FileBrowserControl[] IShellWindow.FileBrowsers => new[] { this.FileBrowser, this.SecondFileBrowser };
        TabManagerControl[] IShellWindow.TabManagers => new[] { this.TabManager, this.SecondTabManager };

        // 兼容属性
        FileBrowserControl IShellWindow.FileBrowser => this.FileBrowser;
        FileBrowserControl IShellWindow.SecondFileBrowser => this.SecondFileBrowser;
        TabManagerControl IShellWindow.TabManager => this.TabManager;
        TabManagerControl IShellWindow.SecondTabManager => this.SecondTabManager;
        Grid IShellWindow.SecondFileBrowserContainer => this.SecondFileBrowserContainer;

        // Resource access
        object IShellWindow.TryFindResource(object key) => this.TryFindResource(key);

        // Refresh
        void IShellWindow.RefreshFileList() => this.RefreshFileList();

        // ViewModel (already public property ViewModel)
        // IShellWindow Implementation extensions
        Grid IShellWindow.RootGrid => this.RootGrid;
        ColumnDefinition IShellWindow.ColLeft => this.ColLeft;
        ColumnDefinition IShellWindow.ColCenter => this.ColCenter;
        ColumnDefinition IShellWindow.ColRight => this.ColRight;
        ColumnDefinition IShellWindow.ColRail => this.ColRail;
        CollapsibleGridSplitter IShellWindow.SplitterRight => this.SplitterRight;
        Button IShellWindow.TitleBarMaxRestoreButton => this.TitleBarMaxRestoreButton;
        System.Windows.Controls.Image IShellWindow.TitleBarMaxRestoreImage => this.TitleBarMaxRestoreImage;
        bool IShellWindow.IsSplitterDragging => this._isSplitterDragging;

        bool IShellWindow.IsDualPaneMode => this.IsDualPaneMode;

        NavigationPanelControl IShellWindow.NavigationPanelControl => this.NavigationPanelControl;

        // Expose nested listboxes if they are not directly on MainWindow
        // Assuming NavigationPanelControl structure based on usage
        ListBox IShellWindow.LibrariesListBox => this.NavigationPanelControl?.LibrariesListBoxControl as ListBox;
        ListBox IShellWindow.QuickAccessListBox => this.NavigationPanelControl?.QuickAccessListBoxControl as ListBox;

        PaneContentHost IShellWindow.PrimaryContentHost => this.PrimaryContentHost;
        PaneContentHost IShellWindow.SecondContentHost => this.SecondContentHost;

        ContextMenu IShellWindow.LibraryContextMenu => this.LibraryContextMenu;
        Services.Navigation.PaneId IShellWindow.GetActivePaneId() => this.GetActivePaneId();
        bool IShellWindow.IsInternalUiUpdate => this._isInternalUpdate;

        void IShellWindow.ClearLegacyFileState()
        {
            this._currentFiles.Clear();
            this._currentPath = null;
            var fileBrowser = this.PrimaryContentHost?.InternalFileBrowser;
            if (fileBrowser != null)
            {
                fileBrowser.SetSearchStatus(false);
            }
        }

        #endregion

        #region 字段与属性

        // Core state delegating to ViewModel
        internal string _currentPath
        {
            get => _viewModel?.CurrentPath;
            set { if (_viewModel != null) _viewModel.CurrentPath = value; }
        }
        internal List<FileSystemItem> _currentFiles = new List<FileSystemItem>(); // Keep for legacy search logic
        internal bool _isInternalUpdate = false;

        internal Handlers.DragDropEventHandler _dragDropEventHandler;
        internal Handlers.LibraryEventHandler _libraryEventHandler;

        internal Library _currentLibrary
        {
            get => _viewModel?.ActivePane?.CurrentLibrary;
            set { if (_viewModel?.ActivePane != null) _viewModel.ActivePane.CurrentLibrary = value; }
        }

        // 统一导航协调器
        internal NavigationCoordinator _navigationCoordinator;

        // 窗口编排器
        internal IWindowOrchestrator _orchestrator;

        // MVVM 架构
        internal MainWindowViewModel _viewModel;
        internal IMessageBus _messageBus;

        // Module 引用委派 (用于分部类兼容)
        internal NavigationModule _navigationModule => _viewModel?.Navigation;
        internal TabsModule _tabsModule => _viewModel?.Tabs;
        internal LayoutModule _layoutModule => _viewModel?.Layout;
        internal LibraryModule _libraryModule => _viewModel?.Library;

        // 服务引用委派 (由 Orchestrator 管理，此处仅为兼容部分类代码)
        internal NavigationService _navigationService => _orchestrator?.NavigationService;
        internal NavigationModeService _navigationModeService => _orchestrator?.NavigationModeService;
        internal TabService _tabService => _orchestrator?.TabService;
        internal TabService _secondTabService => _orchestrator?.SecondTabService;
        internal LibraryService _libraryService => _orchestrator?.LibraryService;
        internal FavoriteService _favoriteService => _orchestrator?.FavoriteService;
        internal QuickAccessService _quickAccessService => _orchestrator?.QuickAccessService;
        internal FileListService _fileListService => _orchestrator?.FileListService;
        internal FileListService _secondFileListService => _orchestrator?.SecondFileListService;
        internal SearchService _searchService => _orchestrator?.SearchService;
        internal SearchCacheService _searchCacheService => _orchestrator?.SearchCacheService;
        internal FileSystemWatcherService _fileSystemWatcherService => _orchestrator?.FileSystemWatcherService;
        internal Services.WindowStateManager _windowStateManager => _orchestrator?.WindowStateManager;
        internal Handlers.WindowLifecycleHandler _windowLifecycleHandler => _orchestrator?.LifecycleHandler;

        internal Handlers.ColumnInteractionHandler _columnInteractionHandler => _orchestrator?.ColumnInteractionHandler;
        internal Services.FileOperations.FileOperationService _fileOperationService => _orchestrator?.FileOperationService;
        internal Handlers.KeyboardEventHandler _keyboardEventHandler => _orchestrator?.KeyboardEventHandler;
        internal Handlers.FileListEventHandler _mainFileListHandler => _orchestrator?.MainFileListHandler;
        internal Handlers.FileListEventHandler _secondFileListHandler => _orchestrator?.SecondFileListHandler;
        internal Services.FileInfo.FileInfoService _secondFileInfoService => _orchestrator?.SecondFileInfoService;
        internal Services.ColumnManagement.ColumnService _columnService => _orchestrator?.ColumnService;
        internal Services.UIHelper.IUIHelperService _uiHelperService => _orchestrator?.UIHelperService;

        /// <summary>
        /// 主窗口 ViewModel
        /// </summary>
        public MainWindowViewModel ViewModel => _viewModel;

        internal bool _isSplitterDragging = false;
        internal Services.Search.SearchOptions _searchOptions = new Services.Search.SearchOptions();

        // NavigationPanelControl 控件的便捷访问属性
        internal ListBox LibrariesListBox => NavigationPanelControl?.LibrariesListBoxControl;
        internal TreeView DrivesTreeView => NavigationPanelControl?.DrivesTreeViewControl;
        internal ListBox QuickAccessListBox => NavigationPanelControl?.QuickAccessListBoxControl;
        internal Grid NavPathContent => NavigationPanelControl?.NavPathContentControl;
        internal Grid NavLibraryContent => NavigationPanelControl?.NavLibraryContentControl;
        internal Grid NavTagContent => NavigationPanelControl?.NavTagContentControl;
        internal ContextMenu LibraryContextMenu => NavigationPanelControl?.LibraryContextMenuControl;

        public FileBrowserControl FileBrowser => PrimaryContentHost?.InternalFileBrowser;
        public FileBrowserControl SecondFileBrowser => SecondContentHost?.InternalFileBrowser;

        #endregion

        #region 公共方法



        internal void InitializeEvents()
        {
            // 订阅分割器折叠事件，动态调整标签页边距
            if (SplitterRight != null)
            {
                SplitterRight.CollapsedStateChanged += (s, e) => UpdateTabManagerMargin();
            }

            this.Activated += OnActivated;
        }

        internal void InitializeServiceEvents()
        {
            // 此处的直接服务事件订阅已迁移至 WindowOrchestrator 的服务桥接逻辑中。
            // 详见 WindowOrchestrator.SetupServiceMessageBridges。
        }

        private void OnActivated(object sender, EventArgs e)
        {
            try
            {
                string currentPath = (IsDualPaneMode && IsSecondPaneFocused) ? _viewModel?.SecondaryPane?.CurrentPath : _currentPath;
                if (currentPath != null && currentPath.StartsWith("search://"))
                {
                    // CheckAndRefreshSearchTab(currentPath); // This method might be missing, assume valid or remove if not needed
                }
            }
            catch { }
        }

        internal FileOperationContext GetActiveFileOperationContext()
        {
            bool useSecond = _viewModel?.ActivePane == _viewModel?.SecondaryPane;
            var targetBrowser = useSecond ? SecondFileBrowser : FileBrowser;
            var targetPath = useSecond ? _viewModel?.SecondaryPane?.CurrentPath : _currentPath;

            Library targetLibrary = null;
            if (useSecond)
            {
                if (!string.IsNullOrEmpty(targetPath) && targetPath.StartsWith("lib://", StringComparison.OrdinalIgnoreCase))
                {
                    var libName = targetPath.Substring(6).Split('/')[0];
                    targetLibrary = _libraryService?.GetAllLibraries()?.FirstOrDefault(l =>
                        string.Equals(l.Name, libName, StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                targetLibrary = _currentLibrary;
            }

            return new FileOperationContext
            {
                TargetPath = targetPath,
                CurrentLibrary = targetLibrary,
                OwnerWindow = this,
                RefreshCallback = () =>
                {
                    if (useSecond) RefreshActiveFileList();
                    else RefreshFileList();
                }
            };
        }

        public void RefreshFileList()
        {
            _viewModel?.PrimaryPane?.FileList?.RefreshFiles();
        }

        public void RefreshActiveFileList()
        {
            _viewModel?.ActivePane?.FileList?.RefreshFiles();
        }



        internal Services.Navigation.PaneId GetActivePaneId()
        {
            // 优先检查 ViewModel 状态，因为点击侧边栏会导致列表失去焦点，使 IsSecondPaneFocused 变得不可靠
            if (_viewModel?.ActivePane != null)
            {
                return _viewModel.ActivePane.IsSecondary ? Services.Navigation.PaneId.Second : Services.Navigation.PaneId.Main;
            }
            // 在预览模式下，即便不是标准的双列表模式，也应当根据焦点判定活跃侧 (在此模式下副面板显示列表)
            return IsSecondPaneFocused ? Services.Navigation.PaneId.Second : Services.Navigation.PaneId.Main;
        }

        #endregion

        #region 构造函数

        public MainWindow()
        {
            InitializeComponent();

            // 🔧 关键修复：绑定窗口关闭事件，确保退出时保存状态（标签页、窗口大小等）
            this.Closing += Window_Closing;
            // 订阅渲染完成事件，确保在窗口初次显示时强制修正布局
            // 这对于解决启动时右侧空白间隙至关重要，因为此时 ActualWidth 才有效
            this.ContentRendered += (s, e) =>
            {
                _orchestrator.LifecycleHandler?.AdjustColumnWidths();

                // 再次确认窗口最大化状态 (双重保险，解决持久化可能失效的问题)
                if (ConfigurationService.Instance.Config?.IsMaximized == true && this.WindowState != WindowState.Maximized)
                {
                    this.WindowState = WindowState.Maximized;
                    _orchestrator.LifecycleHandler?.UpdateWindowStateUI();
                }

                // 启动完成，启用配置保存
                YiboFile.Services.Config.ConfigurationService.Instance.EnableSaving();

                // 初始化剪切板监听和服务
                YiboFile.Services.ClipboardHistory.ClipboardHistoryService.Instance.StartListening(this);
                YiboFile.Services.ClipboardHistory.ClipboardHistoryService.Instance.LoadHistory();
                YiboFile.Services.ClipboardHistory.ClipboardHistoryService.Instance.CleanExpiredItems();

                // 启动剪切板定时清理（每小时）
                var clipboardCleanTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromHours(1) };
                clipboardCleanTimer.Tick += (s2, e2) => YiboFile.Services.ClipboardHistory.ClipboardHistoryService.Instance.CleanExpiredItems();
                clipboardCleanTimer.Start();

                // 📌 Mica 启动检查：如果用户上次保存的主题就是 Win11Pro，窗口显示后立刻激活 Mica
                try
                {
                    var themeService = App.ServiceProvider?.GetService(typeof(Services.Theming.IThemeService)) as Services.Theming.IThemeService;
                    if (themeService?.CurrentTheme?.Id == "Win11Pro")
                    {
                        EnableMicaBackdrop();
                    }
                }
                catch (Exception micaEx)
                {
                    Services.Core.FileLogger.Log($"[Mica] Startup check failed: {micaEx.Message}");
                }
            };

            this.SizeChanged += (s, e) => UpdateTabManagerMargin();
            this.StateChanged += (s, e) => UpdateTabManagerMargin();

            // 初始化通知服务
            Services.Core.NotificationService.Instance.Initialize(NotificationContainer);

            // 初始化 UI 事件和布局模式 (Legacy)
            // InitializeEvents(); // Moved to Orchestrator to ensure services are ready

            // 使用编排器接管核心逻辑、消息桥接和状态恢复
            _orchestrator = App.ServiceProvider.GetRequiredService<IWindowOrchestrator>();

            // 异步执行完整初始化序列
            _ = _orchestrator.InitializeAsync(this);
        }

        #endregion

        #region 窗口生命周期 (Delegates to WindowLifecycleHandler)


        internal void WindowMinimize_Click(object sender, RoutedEventArgs e) => _orchestrator.LifecycleHandler?.HandleMinimize();
        internal void WindowMaximize_Click(object sender, RoutedEventArgs e) => _orchestrator.LifecycleHandler?.HandleMaximize();
        internal void WindowClose_Click(object sender, RoutedEventArgs e) => _orchestrator.LifecycleHandler?.HandleClose();

        private void Window_Closing(object sender, CancelEventArgs e) => _orchestrator.LifecycleHandler?.HandleClosing(e);
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => _orchestrator.LifecycleHandler?.HandleSizeChanged(e);
        private void Window_LocationChanged(object sender, EventArgs e) => _orchestrator.LifecycleHandler?.HandleLocationChanged(e);
        private void ListView_SizeChanged(object sender, SizeChangedEventArgs e) => _orchestrator.LifecycleHandler?.HandleListViewSizeChanged(e);
        private void WindowControlButtonsContainer_PreviewMouseDown(object sender, MouseButtonEventArgs e) => _orchestrator.LifecycleHandler?.HandleControlButtonsMouseDown(e, sender);

        internal void AdjustListViewColumnWidths() => _orchestrator.LifecycleHandler?.HandleListViewSizeChanged(null);
        internal void AdjustColumnWidths() => _orchestrator.LifecycleHandler?.AdjustColumnWidths();
        internal void EnsureColumnMinWidths() => _orchestrator.LifecycleHandler?.EnsureColumnMinWidths();
        public void UpdateWindowStateUI() => _orchestrator.LifecycleHandler?.UpdateWindowStateUI();
        internal void UpdateActionButtonsPosition() { /* Layout handled automatically */ }
        internal void UpdateSeparatorPosition() { /* Layout handled automatically */ }


        private void SplitterRight_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                double centerWidth = ColCenter.ActualWidth;
                double rightWidth = ColRight.ActualWidth;
                double totalVisibleSpace = centerWidth + rightWidth;

                // 计算平分所需要的宽度，但不允许破坏限制
                double halfSpace = totalVisibleSpace / 2;
                halfSpace = Math.Max(halfSpace, ColRight.MinWidth);
                halfSpace = Math.Max(halfSpace, ColCenter.MinWidth);

                if (ColCenter.MinWidth + ColRight.MinWidth <= totalVisibleSpace)
                {
                    // 设置为比例宽度（Star），这样在隐藏左侧导航时左右两侧能等比例缩放
                    ColCenter.Width = new GridLength(1, GridUnitType.Star);
                    ColRight.Width = new GridLength(1, GridUnitType.Star);
                }
                
                e.Handled = true;
            }
        }


        #endregion

        #region Layout Glue Code (Moved from MainWindow.LayoutMode.cs)

        internal Handlers.LayoutEventHandler _layoutEventHandler;

        public bool IsDualPaneMode => _layoutModule?.IsDualPaneMode ?? false;
        public bool IsSecondPaneFocused => _layoutModule?.IsSecondPaneFocused ?? false;

        internal void SwitchLayoutModeByIndex(int index) => _layoutEventHandler?.SwitchLayoutModeByIndex(index);
        internal void SetDualPaneMode(bool enable) => _layoutEventHandler?.SetDualPaneMode(enable);
        internal void SwitchFocusedPane() => _layoutEventHandler?.SwitchFocusedPane();
        internal void SwitchFocusedPaneFromKeyboard() => _layoutEventHandler?.SwitchFocusedPaneFromKeyboard();
        internal void UpdateFocusBorders() => _layoutEventHandler?.UpdateFocusBorders();
        internal void UpdateTabManagerLayout() => _layoutEventHandler?.UpdateTabManagerLayout();

        // 仅供 WindowOrchestrator 调用，确保初始化顺序
        internal void InitializeLayoutMode() => _layoutEventHandler?.Initialize();

        internal (Controls.FileBrowserControl browser, string path, Library library) GetActiveContext()
        {
            if (_layoutEventHandler != null) return _layoutEventHandler.GetActiveContext();
            return (PrimaryContentHost?.InternalFileBrowser, _currentPath, _currentLibrary);
        }

        internal void NavigateSecondaryPaneToLibrary(Library library) => _layoutEventHandler?.NavigateSecondaryPaneToLibrary(library);
        internal void NavigateSecondaryPaneToTag(Models.TagViewModel tag) => _layoutEventHandler?.NavigateSecondaryPaneToTag(tag);
        internal void LoadSecondFileBrowserDirectory(string path) => _layoutEventHandler?.LoadSecondFileBrowserDirectory(path);

        #endregion

        #region 导航选择管理

        /// <summary>
        /// 加载驱动器树
        /// </summary>
        internal void LoadDrives()
        {
            if (DrivesTreeView == null) return;
            _quickAccessService.LoadDriveTree(DrivesTreeView, _fileListService.FormatFileSize);
        }

        /// <summary>
        /// 清除其他导航区域的选择状态，确保同时只有一个区域显示选中
        /// </summary>
        /// <param name="exceptSource">不清除哪个源 ("Drives", "QuickAccess", "Favorites")</param>
        private void ClearOtherNavigationSelections(string exceptSource)
        {
            if (exceptSource != "Drives")
            {
                ClearDriveSelection();
            }
            if (exceptSource != "QuickAccess" && QuickAccessListBox != null)
            {
                QuickAccessListBox.SelectedItem = null;
            }
        }

        /// <summary>
        /// 清除驱动器树的选中状态
        /// </summary>
        internal void ClearDriveSelection()
        {
            if (DrivesTreeView?.ItemsSource is System.Collections.IEnumerable items)
            {
                foreach (var item in items)
                {
                    if (item is YiboFile.Services.Navigation.NavigationItem navItem)
                    {
                        RecursivelyClearSelection(navItem);
                    }
                }
            }
        }

        private void RecursivelyClearSelection(YiboFile.Services.Navigation.NavigationItem item)
        {
            if (item.IsSelected) item.IsSelected = false;
            foreach (var child in item.Children)
            {
                RecursivelyClearSelection(child);
            }
        }

        #endregion

        #region 标签页边距管理

        /// <summary>
        /// 更新标签管理器边距
        /// </summary>
        public void UpdateTabManagerMargin()
        {
            this.Dispatcher.InvokeAsync(UpdateTabManagerMarginLogic, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void UpdateTabManagerMarginLogic()
        {
            if (WindowButtonsStackPanel == null) return;

            // 动态获取控制按钮区域的实际宽度或使用安全兜底值 180 (3 * 46 + 42)
            double buttonsWidth = WindowButtonsStackPanel.ActualWidth;
            double rightMargin = (buttonsWidth > 100) ? buttonsWidth + 20 : 180;

            // 获取当前布局状态：是否在逻辑上表现为双列（即副标签页是否当前可见）
            bool isSecondaryTabEffectivelyVisible = this.IsDualPaneMode || 
                (_layoutModule?.CurrentPaneMode == YiboFile.ViewModels.Messaging.Messages.PaneMode.Preview && this.IsSecondPaneFocused);

            bool isRightPanelCollapsed = ColRight == null || ColRight.ActualWidth <= 0;

            // 1. 处理主标签页边距
            if (TabManager != null)
            {
                if (isSecondaryTabEffectivelyVisible)
                {
                    // 双列或右侧预览模式下，主标签（左）只需预留小型拖拽区 30
                    TabManager.Margin = new Thickness(0, 0, 30, 0);
                }
                else
                {
                    // 单列模式或左侧预览模式下，主标签在最右侧，必须避让系统按钮
                    TabManager.Margin = isRightPanelCollapsed
                        ? new Thickness(0, 0, rightMargin, 0)
                        : new Thickness(0, 0, 30, 0);
                }
            }

            // 2. 处理副标签页边距
            if (SecondTabManager != null)
            {
                // 当副标签页有效显示时，它必然位于窗口最右侧，必须避让按钮
                SecondTabManager.Margin = isSecondaryTabEffectivelyVisible
                    ? new Thickness(0, 0, rightMargin, 0)
                    : new Thickness(0);
            }
        }

        #endregion

        #region 列管理 (Adapter 层 — 供 NavigationModeUIAdapter 和 WindowOrchestrator 调用)

        public void AutoSizeGridViewColumn(GridViewColumn column)
        {
            if (_orchestrator?.ColumnHandlers == null) return;
            foreach (var handler in _orchestrator.ColumnHandlers)
            {
                handler?.AutoSizeGridViewColumn(column);
            }
        }

        internal void EnsureHeaderContextMenuHook()
        {
            if (_orchestrator?.ColumnHandlers == null) return;
            foreach (var handler in _orchestrator.ColumnHandlers)
            {
                handler?.EnsureHeaderContextMenuHook();
            }
        }

        internal string GetCurrentModeKey()
        {
            var activePane = _viewModel?.ActivePane;
            if (activePane != null)
            {
                if (activePane.CurrentLibrary != null) return "Library";
                if (activePane.CurrentPath?.StartsWith("tag://", StringComparison.OrdinalIgnoreCase) == true) return "Tag";
            }
            return "Path";
        }

        internal void ApplyVisibleColumnsForCurrentMode()
        {
            if (_orchestrator?.ColumnHandlers == null) return;
            foreach (var handler in _orchestrator.ColumnHandlers)
            {
                handler?.ApplyVisibleColumnsForCurrentMode();
            }
        }

        #endregion

        #region 消息订阅 (Merged from MainWindow.Messages.cs)

        internal void InitializeMessageSubscriptions()
        {
            if (_messageBus == null) return;

            // 1. 库高亮请求
            _messageBus.Subscribe<ViewModels.Messaging.Messages.LibrarySelectedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() => _libraryEventHandler?.HighlightMatchingLibrary(msg.Library));
            });

            // 2. 焦点面板变更 (同步逻辑焦点)
            _messageBus.Subscribe<ViewModels.Messaging.Messages.FocusedPaneChangedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    var browser = msg.IsSecondPaneFocused ? SecondFileBrowser : FileBrowser;
                    browser?.FilesList?.Focus();
                    UpdateTabManagerMargin();
                });
            });

            // 3. 布局模式变更 (更新标签页边距等)
            _messageBus.Subscribe<ViewModels.Messaging.Messages.PaneModeChangedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() => UpdateTabManagerMargin());
            });
        }

        #endregion



        #region VisualTree 工具方法

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

        #endregion
    }
}
