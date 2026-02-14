using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.ComponentModel;
using YiboFile.Services;
using YiboFile.Services.FileNotes;
using YiboFile.Services.Search;
using YiboFile.Services.Navigation;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Favorite;
using YiboFile.Services.QuickAccess;
using YiboFile.Services.FileList;
using YiboFile.Services.Tabs;
using YiboFile.Services.Orchestration;
using YiboFile.Services.FileOperations.Undo;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Preview;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Config;
using YiboFile.Handlers;
using System.Threading;
using System.Text.Json;
using YiboFile.Models.Navigation;
using YiboFile.Models.UI;
using YiboFile.Models;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Modules;
using YiboFile.Services.Features;
using YiboFile.Services.Core.Error;

namespace YiboFile
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
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
        internal bool _isUpdatingTagSelection = false;


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

        public void RefreshFileList()
        {
            _viewModel?.PrimaryPane?.FileList?.RefreshFiles();
        }

        public void RefreshActiveFileList()
        {
            _viewModel?.ActivePane?.FileList?.RefreshFiles();
        }




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

        internal Services.Search.SearchOptions _searchOptions = new Services.Search.SearchOptions();
        internal bool _isSplitterDragging = false;

        // NavigationPanelControl控件的便捷访问属性
        internal ListBox LibrariesListBox => NavigationPanelControl?.LibrariesListBoxControl;
        internal TreeView DrivesTreeView => NavigationPanelControl?.DrivesTreeViewControl;
        internal ListBox QuickAccessListBox => NavigationPanelControl?.QuickAccessListBoxControl;
        internal Grid NavPathContent => NavigationPanelControl?.NavPathContentControl;
        internal Grid NavLibraryContent => NavigationPanelControl?.NavLibraryContentControl;
        internal Grid NavTagContent => NavigationPanelControl?.NavTagContentControl;
        internal ContextMenu LibraryContextMenu => NavigationPanelControl?.LibraryContextMenuControl;

        // 定时器管理 - Restored fields
        private List<DraggableButton> _currentActionButtons = new List<DraggableButton>();
        private List<ActionItem> _actionItems = new List<ActionItem>(); // 保存按钮和分隔符的完整顺序

        internal void CloseOverlays()
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
                // 使用属性判断而非引用判断，更稳健
                return _viewModel.ActivePane.IsSecondary ? Services.Navigation.PaneId.Second : Services.Navigation.PaneId.Main;
            }
            // 降级使用 LayoutModule/UI 状态
            return (IsDualListMode && IsSecondPaneFocused) ? Services.Navigation.PaneId.Second : Services.Navigation.PaneId.Main;
        }

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
            InitializeEvents();

            // 使用编排器接管核心逻辑、消息桥接和状态恢复
            _orchestrator = App.ServiceProvider.GetRequiredService<IWindowOrchestrator>();

            // 异步执行完整初始化序列
            _ = _orchestrator.InitializeAsync(this);
        }

        #region Window Lifecycle Handlers (Delegates to WindowLifecycleHandler)

        internal void Back_Click_Logic()
        {
            if (_navigationService != null && _navigationService.CanNavigateBack)
            {
                _navigationService.NavigateBack();
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

        // These are helper methods called by XAML or other parts, delegating logic
        internal void AdjustListViewColumnWidths() => _orchestrator.LifecycleHandler?.HandleListViewSizeChanged(null);
        internal void AdjustColumnWidths() => _orchestrator.LifecycleHandler?.AdjustColumnWidths();
        internal void EnsureColumnMinWidths() => _orchestrator.LifecycleHandler?.EnsureColumnMinWidths();
        public void UpdateWindowStateUI() => _orchestrator.LifecycleHandler?.UpdateWindowStateUI();
        internal void UpdateActionButtonsPosition() { /* Layout handled automatically */ }
        internal void UpdateSeparatorPosition() { /* Layout handled automatically */ }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
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




        private void OnRailSettingsRequested(object sender, EventArgs e) => _orchestrator.SettingsController?.Toggle();

        private void OnRailAboutRequested(object sender, EventArgs e)
        {
            if (AboutOverlay != null)
            {
                AboutOverlay.Visibility = AboutOverlay.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
        }



        // Legacy handlers removed



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

        private void SettingsOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == sender)
            {
                _orchestrator.SettingsController?.Hide();
            }
        }







        internal void UpdateTabManagerMargin()
        {
            this.Dispatcher.InvokeAsync(UpdateTabManagerMarginLogic, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void UpdateTabManagerMarginLogic()
        {
            if (WindowButtonsStackPanel == null) return;

            // Ensure tabs don't overlap with window control buttons
            // Add a small buffer (e.g. 15px) to the buttons' actual width
            double rightMargin = WindowButtonsStackPanel.ActualWidth + 15;

            // Check if we are in Dual List Mode (using the property from LayoutMode.cs)
            bool isDualMode = this.IsDualListMode;

            // Check if rights panel is collapsed (even in single mode, if not collapsed, it provides enough space for buttons)
            bool isRightPanelCollapsed = SplitterRight != null && SplitterRight.IsNextCollapsed;

            if (TabManager != null)
            {
                if (isDualMode)
                {
                    // 双列表模式：右侧面板 (Col 5) 可见且作为副列表容器。主标签页管理器位于 (Col 3) 是安全的，无需边距。
                    TabManager.Margin = new Thickness(0, 0, 0, 0);
                }
                else
                {
                    // 单列表模式
                    // 如果右面板折叠 -> 标签页管理器延伸到最右侧边缘 -> 需要避开窗口控制按钮的边距
                    // 如果右面板展开 (显示预览面板) -> 标签页管理器在中间 -> 安全
                    if (isRightPanelCollapsed)
                    {
                        TabManager.Margin = new Thickness(0, 0, rightMargin, 0);
                    }
                    else
                    {
                        TabManager.Margin = new Thickness(0, 0, 0, 0);
                    }
                }
            }

            if (SecondTabManager != null)
            {
                if (isDualMode)
                {
                    // 副标签页始终在右侧，始终需要避开按钮
                    SecondTabManager.Margin = new Thickness(0, 0, rightMargin, 0);
                }
                else
                {
                    // 不显示，边距无关紧要
                    SecondTabManager.Margin = new Thickness(0);
                }
            }
        }








        #region 事件处理

        // Legacy handlers NavigateBack_Click and NavigateForward_Click have been migrated to MVVM Commands
        // private void NavigateBack_Click(object sender, RoutedEventArgs e) { ... }
        // private void NavigateForward_Click(object sender, RoutedEventArgs e) { ... }

        private void FileBrowser_ViewModeChanged(object sender, string mode)
        {
            // 根据视图模式设置文件名显示方式
            var fileListService = App.ServiceProvider.GetService<FileListService>();
            if (fileListService != null)
            {
                // 缩略图模式：显示完整文件名（包括扩展名）
                // 列表模式：不显示扩展名（有单独的“类型”列）
                fileListService.ShowFullFileName = (mode == "Thumbnail");
            }

            ConfigurationService.Instance.Set(cfg => cfg.FileViewMode, mode);


            // 恢复剪切状态的视觉效果
            // 需要延迟执行等待容器生成
            this.Dispatcher.InvokeAsync(async () =>
            {
                // 等待 UI 更新
                await Task.Delay(100);
                var (files, isCut) = await YiboFile.Services.FileOperations.ClipboardService.Instance.GetPathsFromClipboardAsync();
                if (files != null && files.Count > 0)
                {
                    UpdateCutItemsVisualState(files.ToList().AsReadOnly());
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void RightPanel_NotesHeightChanged(object sender, double height)
        {
            ConfigurationService.Instance.Set(cfg => cfg.RightPanelNotesHeight, height);

        }

        private void FileBrowser_InfoHeightChanged(object sender, double height)
        {
            ConfigurationService.Instance.Set(cfg => cfg.CenterPanelInfoHeight, height);

        }




        // 菜单事件桥接方法 - 已迁移到 MenuEventHandler
        // internal void Refresh_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Refresh_Click(sender, e);
        // private void ClearFilter_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ClearFilter_Click(sender, e);









        /// <summary>
        /// 从ListBoxItem中提取路径
        /// </summary>












        #endregion





        #region 库功能














        #endregion








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

        private T FindDescendant<T>(DependencyObject d) where T : DependencyObject
        {
            if (d == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(d, i);
                if (child is T t) return t;
                var deeper = FindDescendant<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }


        // 根据内容自动调整列宽（用于双击列分隔条）
        internal void AutoSizeGridViewColumn(GridViewColumn column)
        {
            _orchestrator.ColumnInteractionHandler?.AutoSizeGridViewColumn(column);
        }

        // 右键列头 -> 列显示设置
        internal void EnsureHeaderContextMenuHook()
        {
            _orchestrator.ColumnInteractionHandler?.EnsureHeaderContextMenuHook();
        }

        internal string GetCurrentModeKey()
        {
            return ConfigurationService.Instance.Config.LastNavigationMode ?? "Path";

        }

        internal string GetVisibleColumnsForCurrentMode()
        {
            var columnService = App.ServiceProvider.GetService<ColumnService>();
            return columnService?.GetVisibleColumnsForCurrentMode() ?? "";
        }

        private void SetVisibleColumnsForCurrentMode(string csv)
        {
            var columnService = App.ServiceProvider.GetService<ColumnService>();
            columnService?.SetVisibleColumnsForCurrentMode(csv);
        }

        internal void ApplyVisibleColumnsForCurrentMode()
        {
            _orchestrator.ColumnInteractionHandler?.ApplyVisibleColumnsForCurrentMode();
        }

        // 绑定列头分隔线双击
        internal void HookHeaderThumbs()
        {
            _orchestrator.ColumnInteractionHandler?.HookHeaderThumbs();
        }

        #region 键盘快捷键和文件操作



        // [已移除] 文件操作桥接方法 - 功能已由 PaneViewModel Command 接管
        // Copy_Click, Cut_Click, Paste_Click, Delete_Click, Rename_Click, ShowProperties_Click


        #region 统一文件操作 (新架构)

        /// <summary>
        /// 复制选中文件到剪贴板 (使用 FileOperationService)
        /// </summary>
        internal async Task CopySelectedFilesAsync()
        {
            var (browser, _, _) = GetActiveContext();
            if (browser?.FilesSelectedItems == null) return;
            var items = browser.FilesSelectedItems.Cast<YiboFile.Models.FileSystemItem>().ToList();
            var paths = items.Select(i => i.Path).ToList();
            await _orchestrator.FileOperationService.CopyAsync(paths);
            Services.Core.NotificationService.ShowSuccess($"已复制 {items.Count} 个项目");
        }

        /// <summary>
        /// 剪切选中文件到剪贴板 (使用 FileOperationService)
        /// </summary>
        internal async Task CutSelectedFilesAsync()
        {
            var (browser, _, _) = GetActiveContext();
            if (browser?.FilesSelectedItems == null) return;
            var items = browser.FilesSelectedItems.Cast<YiboFile.Models.FileSystemItem>().ToList();
            var paths = items.Select(i => i.Path).ToList();
            await _orchestrator.FileOperationService.CutAsync(paths);
            Services.Core.NotificationService.ShowSuccess($"已剪切 {items.Count} 个项目");
        }

        /// <summary>
        /// 粘贴剪贴板内容 (使用 FileOperationService)
        /// 进度显示由 TaskQueuePanel 统一管理
        /// </summary>
        internal async Task PasteFilesAsync(CancellationToken ct = default)
        {
            var result = await _orchestrator.FileOperationService.PasteAsync(null, ct);
            if (result.Success && result.ProcessedCount > 0)
            {
                Services.Core.NotificationService.ShowSuccess("粘贴完成");
            }
        }

        /// <summary>
        /// 删除选中文件 (使用 FileOperationService)
        /// </summary>
        internal async Task DeleteSelectedFilesAsync(bool permanent = false)
        {
            var (browser, _, _) = GetActiveContext();
            if (browser?.FilesSelectedItems == null) return;
            var items = browser.FilesSelectedItems.Cast<YiboFile.Models.FileSystemItem>().ToList();

            // 先清除选择，释放文件句柄
            if (browser?.FilesList != null)
            {
                browser.FilesList.SelectedItem = null;
                browser.FilesList.SelectedItems.Clear();
                _viewModel?.SelectionHandler?.HandleNoSelection();
            }

            var result = await _orchestrator.FileOperationService.DeleteAsync(items, permanent);

            if (result.Success && result.ProcessedCount > 0)
            {
                var msg = permanent ? "永久删除" : "删除";
                Services.Core.NotificationService.ShowSuccess($"已{msg} {items.Count} 个项目");
            }
        }

        /// <summary>
        /// 更新剪切文件的视觉状态（半透明效果）
        /// </summary>
        private void UpdateCutItemsVisualState(IReadOnlyList<string> cutPaths)
        {
            if (FileBrowser?.FilesList == null) return;

            var hashSet = new HashSet<string>(cutPaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            foreach (var item in FileBrowser.FilesList.Items)
            {
                if (item is FileSystemItem fileItem)
                {
                    var container = FileBrowser.FilesList.ItemContainerGenerator.ContainerFromItem(fileItem) as System.Windows.Controls.ListViewItem;
                    if (container != null)
                    {
                        container.Opacity = hashSet.Contains(fileItem.Path) ? 0.5 : 1.0;
                    }
                }
            }
        }

        #endregion

        #endregion

        #region 列表排序



        #endregion

        private void NavigationRail_Loaded(object sender, RoutedEventArgs e)
        {
            _messageBus?.Publish(new NavigationRailLoadedMessage());
        }
    }







}
