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

namespace YiboFile
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window, IShellWindow
    {
        #region IShellWindow Implementation

        FileBrowserControl IShellWindow.FileBrowser => this.FileBrowser;
        FileBrowserControl IShellWindow.SecondFileBrowser => this.SecondFileBrowser;
        TabManagerControl IShellWindow.TabManager => this.TabManager;
        TabManagerControl IShellWindow.SecondTabManager => this.SecondTabManager;
        // SettingsOverlay is accessed via FindName
        Grid IShellWindow.SettingsOverlay => this.FindName("SettingsOverlay") as Grid;

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
        Button IShellWindow.TitleBarMaxRestoreButton => this.TitleBarMaxRestoreButton;
        bool IShellWindow.IsSplitterDragging => this._isSplitterDragging;

        bool IShellWindow.IsDualListMode => this.IsDualListMode;

        NavigationPanelControl IShellWindow.NavigationPanelControl => this.NavigationPanelControl;

        // Expose nested listboxes if they are not directly on MainWindow
        // Assuming NavigationPanelControl structure based on usage
        ListBox IShellWindow.LibrariesListBox => this.NavigationPanelControl?.LibrariesListBoxControl as ListBox;
        ListBox IShellWindow.QuickAccessListBox => this.NavigationPanelControl?.QuickAccessListBoxControl as ListBox;

        // SecondTabManager likely already implemented?
        // TabManagerControl IShellWindow.SecondTabManager => this.SecondTabManager; 
        // No, check IShellWindow definition, likely implemented implicitly if property exists.
        // Explicit impl only needed if visibility issues or naming conflict.

        ContextMenu IShellWindow.LibraryContextMenu => this.LibraryContextMenu;
        Services.Navigation.PaneId IShellWindow.GetActivePaneId() => this.GetActivePaneId();
        bool IShellWindow.IsInternalUiUpdate => this._isInternalUpdate;

        void IShellWindow.ClearLegacyFileState()
        {
            this._currentFiles.Clear();
            this._currentPath = null;
            if (this.FileBrowser != null)
            {
                this.FileBrowser.SetSearchStatus(false);
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
        internal Services.Settings.SettingsOverlayController _settingsOverlayController => _orchestrator?.SettingsController;
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
                string currentPath = (IsDualListMode && IsSecondPaneFocused) ? _viewModel?.SecondaryPane?.CurrentPath : _currentPath;
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

        /// <summary>
        /// 关闭所有覆盖层
        /// </summary>
        public void CloseOverlays()
        {
            if (SettingsOverlay != null && SettingsOverlay.Visibility == Visibility.Visible)
            {
                _settingsOverlayController?.Hide();
            }
            if (AboutOverlay != null && AboutOverlay.Visibility == Visibility.Visible)
            {
                AboutOverlay.Visibility = Visibility.Collapsed;
            }
        }

        internal Services.Navigation.PaneId GetActivePaneId()
        {
            // 优先检查 ViewModel 状态，因为点击侧边栏会导致列表失去焦点，使 IsSecondPaneFocused 变得不可靠
            if (_viewModel?.ActivePane != null)
            {
                return _viewModel.ActivePane.IsSecondary ? Services.Navigation.PaneId.Second : Services.Navigation.PaneId.Main;
            }
            // 降级使用 LayoutModule/UI 状态
            return (IsDualListMode && IsSecondPaneFocused) ? Services.Navigation.PaneId.Second : Services.Navigation.PaneId.Main;
        }

        #endregion

        #region 构造函数

        public MainWindow()
        {
            try
            {
                string msg = $"{DateTime.Now:O} [MainWindow.Constructor] Start";
                System.Diagnostics.Debug.WriteLine(msg);
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_debug.log"), msg + "\n");
            }
            catch { }

            try { System.IO.File.AppendAllText(@"f:\Download\GitHub\YiboFile\YiboFile-Core\debug_log.txt", $"[MainWindow] Constructor called at {DateTime.Now}\n"); } catch { }
            InitializeComponent();
            this.Title += " [FIXED]";

            // 订阅渲染完成事件，确保在窗口初次显示时强制修正布局
            // 这对于解决启动时右侧空白间隙至关重要，因为此时 ActualWidth 才有效
            this.ContentRendered += (s, e) =>
            {
                try
                {
                    string msg = $"{DateTime.Now:O} [MainWindow.ContentRendered] WindowState={this.WindowState}, Config.IsMaximized={ConfigurationService.Instance.Config?.IsMaximized}";
                    System.Diagnostics.Debug.WriteLine(msg);
                    System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_debug.log"), msg + "\n");
                }
                catch { }

                _orchestrator.LifecycleHandler?.AdjustColumnWidths();

                // 再次确认窗口最大化状态 (双重保险，解决持久化可能失效的问题)
                if (ConfigurationService.Instance.Config?.IsMaximized == true && this.WindowState != WindowState.Maximized)
                {
                    try
                    {
                        string msg = $"{DateTime.Now:O} [MainWindow.ContentRendered] Forcing Maximize";
                        System.Diagnostics.Debug.WriteLine(msg);
                        System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_debug.log"), msg + "\n");
                    }
                    catch { }

                    this.WindowState = WindowState.Maximized;
                    _orchestrator.LifecycleHandler?.UpdateWindowStateUI();
                }

                // 启动完成，启用配置保存
                YiboFile.Services.Config.ConfigurationService.Instance.EnableSaving();
                try
                {
                    string msg = $"{DateTime.Now:O} [MainWindow.ContentRendered] Configuration Saving Enabled";
                    System.Diagnostics.Debug.WriteLine(msg);
                    System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_debug.log"), msg + "\n");
                }
                catch { }
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

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果是在全屏覆盖层打开的情况下点击标题栏空白处，关闭覆盖层
            if (SettingsOverlay != null && SettingsOverlay.Visibility == Visibility.Visible)
            {
                _settingsOverlayController?.Hide();
            }
            if (AboutOverlay != null && AboutOverlay.Visibility == Visibility.Visible)
            {
                AboutOverlay.Visibility = Visibility.Collapsed;
            }

            // 双击最大化/还原
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
            {
                if (WindowState == WindowState.Maximized)
                    WindowState = WindowState.Normal;
                else
                    WindowState = WindowState.Maximized;
                return;
            }

            // 支持通过拖动标题栏移动窗口
            if (e.ChangedButton == MouseButton.Left)
            {
                try { this.DragMove(); } catch { }
            }
        }

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

        private void SettingsOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == sender)
            {
                _orchestrator.SettingsController?.Hide();
            }
        }

        #endregion

        #region Layout Glue Code (Moved from MainWindow.LayoutMode.cs)

        internal Handlers.LayoutEventHandler _layoutEventHandler;

        public bool IsDualListMode => _layoutModule?.IsDualListMode ?? false;
        public bool IsSecondPaneFocused => _layoutModule?.IsSecondPaneFocused ?? false;

        internal void SwitchLayoutModeByIndex(int index) => _layoutEventHandler?.SwitchLayoutModeByIndex(index);
        internal void SetDualListMode(bool enable) => _layoutEventHandler?.SetDualListMode(enable);
        internal void SwitchFocusedPane() => _layoutEventHandler?.SwitchFocusedPane();
        internal void SwitchFocusedPaneFromKeyboard() => _layoutEventHandler?.SwitchFocusedPaneFromKeyboard();
        internal void UpdateFocusBorders() => _layoutEventHandler?.UpdateFocusBorders();
        internal void UpdateTabManagerLayout() => _layoutEventHandler?.UpdateTabManagerLayout();

        // 仅供 WindowOrchestrator 调用，确保初始化顺序
        internal void AttachSecondTabServiceUiContext() => _layoutEventHandler?.AttachSecondTabServiceUiContext();
        internal void InitializeLayoutMode() => _layoutEventHandler?.Initialize();

        internal (Controls.FileBrowserControl browser, string path, Library library) GetActiveContext()
        {
            if (_layoutEventHandler != null) return _layoutEventHandler.GetActiveContext();
            return (FileBrowser, _currentPath, _currentLibrary);
        }

        internal void NavigateSecondaryPaneToLibrary(Library library) => _layoutEventHandler?.NavigateSecondaryPaneToLibrary(library);
        internal void NavigateSecondaryPaneToTag(Models.TagViewModel tag) => _layoutEventHandler?.NavigateSecondaryPaneToTag(tag);
        internal void LoadSecondFileBrowserDirectory(string path) => _layoutEventHandler?.LoadSecondFileBrowserDirectory(path);

        #endregion

        #region 导航选择管理

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

            double rightMargin = WindowButtonsStackPanel.ActualWidth + 15;
            bool isDualMode = this.IsDualListMode;
            bool isRightPanelCollapsed = SplitterRight != null && SplitterRight.IsNextCollapsed;

            if (TabManager != null)
            {
                if (isDualMode)
                {
                    TabManager.Margin = new Thickness(0, 0, 0, 0);
                }
                else
                {
                    TabManager.Margin = isRightPanelCollapsed
                        ? new Thickness(0, 0, rightMargin, 0)
                        : new Thickness(0, 0, 0, 0);
                }
            }

            if (SecondTabManager != null)
            {
                SecondTabManager.Margin = isDualMode
                    ? new Thickness(0, 0, rightMargin, 0)
                    : new Thickness(0);
            }
        }

        #endregion

        #region 列管理 (Adapter 层 — 供 NavigationModeUIAdapter 和 WindowOrchestrator 调用)

        public void AutoSizeGridViewColumn(GridViewColumn column)
        {
            _orchestrator.ColumnInteractionHandler?.AutoSizeGridViewColumn(column);
        }

        internal void EnsureHeaderContextMenuHook()
        {
            _orchestrator.ColumnInteractionHandler?.EnsureHeaderContextMenuHook();
        }

        internal string GetCurrentModeKey()
        {
            return ConfigurationService.Instance.Config.LastNavigationMode ?? "Path";
        }

        internal void ApplyVisibleColumnsForCurrentMode()
        {
            _orchestrator.ColumnInteractionHandler?.ApplyVisibleColumnsForCurrentMode();
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
                    if (msg.IsSecondPaneFocused)
                    {
                        SecondFileBrowser?.Focus();
                        SecondFileBrowser?.FilesList?.Focus();
                    }
                    else
                    {
                        FileBrowser?.Focus();
                        FileBrowser?.FilesList?.Focus();
                    }
                });
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
