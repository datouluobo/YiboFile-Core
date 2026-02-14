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
        private bool _isInternalUpdate = false;

        private DragDropManager _dragDropManager;

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

        // 定时器管理
        internal bool _isSplitterDragging = false;
        internal Services.Search.SearchOptions _searchOptions = new Services.Search.SearchOptions();

        // TagTrain 训练状态
        internal CancellationTokenSource _tagTrainTrainingCancellation = null;
        internal bool _tagTrainIsTraining = false;




        private List<DraggableButton> _currentActionButtons = new List<DraggableButton>();
        private List<ActionItem> _actionItems = new List<ActionItem>(); // 保存按钮和分隔符的完整顺序

        // NavigationPanelControl控件的便捷访问属性
        // 改为 internal 以便 NavigationUIHelper 可以访问
        internal ListBox LibrariesListBox => NavigationPanelControl?.LibrariesListBoxControl;
        internal TreeView DrivesTreeView => NavigationPanelControl?.DrivesTreeViewControl;
        // Obsolete: internal ListBox DrivesListBox => NavigationPanelControl?.DrivesListBoxControl;
        internal ListBox QuickAccessListBox => NavigationPanelControl?.QuickAccessListBoxControl;
        internal Grid NavPathContent => NavigationPanelControl?.NavPathContentControl;
        internal Grid NavLibraryContent => NavigationPanelControl?.NavLibraryContentControl;
        internal Grid NavTagContent => NavigationPanelControl?.NavTagContentControl;


        internal ContextMenu LibraryContextMenu => NavigationPanelControl?.LibraryContextMenuControl;

        // UI Adapter implementations removed




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

        /// <summary>
        /// 从模块导航到路径（桥接方法）
        /// </summary>
        internal void NavigateToPathFromModule(string path)
        {
            NavigateToPath(path, Services.Navigation.PaneId.Main);
        }

        private void OnTagUpdated(int tagId, string newColor)
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentFiles != null)
                {
                    foreach (var file in _currentFiles)
                    {
                        if (file.TagList != null)
                        {
                            var tag = file.TagList.FirstOrDefault(t => t.Id == tagId);
                            if (tag != null)
                            {
                                tag.Color = newColor;
                            }
                        }
                    }
                }
            });
        }



        private void OnRailSettingsRequested(object sender, EventArgs e) => _orchestrator.SettingsController?.Toggle();

        private void OnRailAboutRequested(object sender, EventArgs e)
        {
            if (AboutOverlay != null)
            {
                AboutOverlay.Visibility = AboutOverlay.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            }
        }



        // Legacy handlers removed

        internal void Refresh_Click(object sender, RoutedEventArgs e) => RefreshFileList();

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


        private void OnTagSelected(int tagId, string tagName)
        {
            // Consistent navigation: Use tag protocol with tag NAME
            NavigateToTag(tagName);
        }

        internal void NavigateToTag(string tagName, Services.Navigation.PaneId? targetPane = null)
        {
            if (string.IsNullOrEmpty(tagName)) return;

            _navigationCoordinator?.NavigateAsync(new YiboFile.Models.Navigation.NavigationRequest
            {
                Target = YiboFile.Models.Navigation.NavigationTarget.FromTag(tagName),
                Pane = targetPane ?? GetActivePaneId(),
                Source = NavigationSource.SidebarTag
            });
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

        /// <summary>
        /// NavigationService 导航请求事件处理
        /// </summary>
        private void OnNavigationServiceNavigateRequested(object sender, string path)
        {
            _currentPath = path;

            // [DEPRECATED] 标签页同步现在通过 MessageBus (PathChangedMessage) 自动处理。
            // 直接调用 UpdateActiveTabPath 会因缺少 PaneId 而默认操作主面板，导致双面板状态串扰。
            // _tabsModule?.UpdateActiveTabPath(path);

            UpdateNavigationButtonsState();
        }

        /// <summary>
        /// 更新导航按钮状态
        /// </summary>
        internal void UpdateNavigationButtonsState()
        {
            // 使用 NavigationModeService 更新导航按钮状态
            if (_orchestrator.NavigationModeService != null)
            {
                _orchestrator.NavigationModeService.UpdateNavigationButtonsState();
            }
        }


        // 菜单事件桥接方法 - 已迁移到 MenuEventHandler
        // internal void Refresh_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Refresh_Click(sender, e);
        // private void ClearFilter_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ClearFilter_Click(sender, e);


        internal void ClearFilter()
        {
            // 清除过滤状态，恢复正常的文件浏览
            _currentFiles.Clear();
            if (FileBrowser != null)
                _viewModel?.PrimaryPane?.FileList?.UpdateFiles(new List<FileSystemItem>());
            HideEmptyStateMessage();
        }

        private void FilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileBrowser == null || FileBrowser.FilesList == null) return;
            var selectedItems = FileBrowser.FilesList.SelectedItems;

            _viewModel?.SelectionHandler?.HandleSelectionChanged(selectedItems);
        }



        private void ShowEmptyLibraryMessage(string libraryName)
        {
            if (FileBrowser != null)
            {
                FileBrowser.ShowEmptyState($"库 \"{libraryName}\" 没有添加任何位置。\n\n请在管理库中添加位置。");
            }
        }

        internal void HideEmptyStateMessage()
        {
            if (FileBrowser != null)
            {
                FileBrowser.HideEmptyState();
            }
        }

        private void ShowEmptyStateMessage(string message)
        {
            if (FileBrowser != null)
            {
                FileBrowser.ShowEmptyState(message);
            }
        }






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

        /// <summary>
        /// 撤销操作
        /// </summary>
        internal void Undo_Click(object sender, RoutedEventArgs e)
        {
            var undoService = App.ServiceProvider.GetService<UndoService>();
            var errorService = App.ServiceProvider.GetService<YiboFile.Services.Core.Error.ErrorService>();

            if (undoService?.CanUndo == true)
            {
                var description = undoService.NextUndoDescription;
                if (undoService.Undo())
                {
                    errorService?.ReportError($"已撤销: {description}", YiboFile.Services.Core.Error.ErrorSeverity.Info);
                    RefreshFileList();
                }
                else
                {
                    errorService?.ReportError("撤销失败", YiboFile.Services.Core.Error.ErrorSeverity.Warning);
                }
            }
            else
            { }
        }

        /// <summary>
        /// 重做操作
        /// </summary>
        internal void Redo_Click(object sender, RoutedEventArgs e)
        {
            var undoService = App.ServiceProvider.GetService<UndoService>();
            var errorService = App.ServiceProvider.GetService<YiboFile.Services.Core.Error.ErrorService>();

            if (undoService?.CanRedo == true)
            {
                var description = undoService.NextRedoDescription;
                if (undoService.Redo())
                {
                    errorService?.ReportError($"已重做: {description}", YiboFile.Services.Core.Error.ErrorSeverity.Info);
                    RefreshFileList();
                }
                else
                {
                    errorService?.ReportError("重做失败", YiboFile.Services.Core.Error.ErrorSeverity.Warning);
                }
            }
            else
            { }
        }


        #endregion

        #region 列表排序



        #endregion

        private void NavigationRail_Loaded(object sender, RoutedEventArgs e)
        {
            _messageBus?.Publish(new NavigationRailLoadedMessage());
        }
    }







}
