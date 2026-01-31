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
using YiboFile.Services.FileOperations.Undo;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Preview;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Config;
using YiboFile.Handlers;
using System.Threading;
using System.Text.Json;
using System.Text;

using YiboFile.Models.UI;
using YiboFile.Models;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.ViewModels.Modules;
using YiboFile.Services.Core.Error;

namespace YiboFile
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, IConfigUIHelper, Services.Navigation.INavigationModeUIHelper
    {
        internal string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        internal List<FileSystemItem> _currentFiles = new List<FileSystemItem>();

        private DragDropManager _dragDropManager;

        internal Library _currentLibrary = null;
        internal bool _isUpdatingTagSelection = false;


        // 统一导航协调器
        internal NavigationCoordinator _navigationCoordinator;

        // 服务实例
        internal NavigationService _navigationService;
        internal LibraryService _libraryService;
        private FavoriteService _favoriteService;
        private QuickAccessService _quickAccessService;
        internal FileListService _fileListService;
        private FileSystemWatcherService _fileSystemWatcherService;
        internal FolderSizeCalculationService _folderSizeCalculationService;
        internal TabService _tabService;
        internal TabService _secondTabService;
        internal Services.Preview.PreviewService _previewService;
        internal SearchService _searchService;
        internal SearchCacheService _searchCacheService;
        internal Services.ColumnManagement.ColumnService _columnService;
        internal ConfigService _configService;
        internal Services.Settings.SettingsOverlayController _settingsOverlayController;
        internal Services.Navigation.NavigationModeService _navigationModeService;
        internal Services.UIHelper.UIHelperService _uiHelperService;
        internal Services.WindowStateManager _windowStateManager;
        private Services.FileInfo.FileInfoService _fileInfoService;
        private Services.FileInfo.FileInfoService _secondFileInfoService;
        internal Services.Archive.ArchiveService _archiveService; // ARCHIVE SERVICE
        internal Services.Features.ITagService _tagService;



        // 事件处理器
        internal Handlers.FileBrowserEventHandler _fileBrowserEventHandler;
        internal Handlers.FileListEventHandler _mainFileListHandler;
        internal Handlers.FileListEventHandler _secondFileListHandler;
        internal Handlers.MenuEventHandler _menuEventHandler;
        internal Handlers.KeyboardEventHandler _keyboardEventHandler;
        internal Handlers.MouseEventHandler _mouseEventHandler;
        internal Handlers.ColumnInteractionHandler _columnInteractionHandler;
        internal Handlers.ColumnInteractionHandler _secondColumnInteractionHandler;
        internal Handlers.WindowLifecycleHandler _windowLifecycleHandler;
        internal Handlers.FileOperationHandler _fileOperationHandler;
        private SelectionEventHandler _selectionEventHandler;

        // 统一文件操作服务 (新架构)
        internal Services.FileOperations.FileOperationService _fileOperationService;

        // MVVM 架构
        private MainWindowViewModel _viewModel;
        private IMessageBus _messageBus;
        private NavigationModule _navigationModule;
        private TabsModule _tabsModule;
        private FileListModule _fileListModule;
        private LayoutModule _layoutModule;
        private FileOperationModule _fileOperationModule;
        private NotesModule _notesModule;
        private TagsModule _tagsModule;
        private FavoritesModule _favoritesModule;
        private LibraryModule _libraryModule;

        /// <summary>
        /// 主窗口 ViewModel
        /// </summary>
        public MainWindowViewModel ViewModel => _viewModel;

        // 定时器管理
        // 定时器管理
        internal System.Windows.Threading.DispatcherTimer _periodicTimer = new System.Windows.Threading.DispatcherTimer();
        internal System.Windows.Threading.DispatcherTimer _layoutCheckTimer = new System.Windows.Threading.DispatcherTimer();
        internal bool _isSplitterDragging = false; // 标记是否正在拖拽分割器
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

        // INavigationModeUIHelper 实现
        System.Windows.Threading.Dispatcher Services.Navigation.INavigationModeUIHelper.Dispatcher => this.Dispatcher;
        Library Services.Navigation.INavigationModeUIHelper.CurrentLibrary
        {
            get => _libraryModule?.SelectedLibrary ?? _currentLibrary;
            set
            {
                _currentLibrary = value;
                if (_libraryModule != null && _libraryModule.SelectedLibrary != value)
                {
                    _libraryModule.SelectedLibrary = value;
                }
            }
        }
        string Services.Navigation.INavigationModeUIHelper.CurrentPath
        {
            get => _currentPath;
            set => _currentPath = value;
        }



        // TagPanel TagBrowsePanel => NavigationPanelControl?.TagBrowsePanelControl; // Phase 2
        // TagPanel TagEditPanel => NavigationPanelControl?.TagEditPanelControl; // Phase 2
        Controls.FileBrowserControl Services.Navigation.INavigationModeUIHelper.FileBrowser => FileBrowser;
        ListBox Services.Navigation.INavigationModeUIHelper.LibrariesListBox => LibrariesListBox;
        Controls.NavigationPanelControl Services.Navigation.INavigationModeUIHelper.NavigationPanelControl => NavigationPanelControl;
        // Legacy buttons removed, UIHelper should rely on NavigationModeService or Rail events
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavPathButton => NavigationRail?.PathButton;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavLibraryButton => NavigationRail?.LibraryButton;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavTagButton => NavigationRail?.TagButton;


        // INavigationModeUIHelper 方法实现

        void Services.Navigation.INavigationModeUIHelper.SwitchToTab(Services.Tabs.PathTab tab) => SwitchToTab(tab);
        void Services.Navigation.INavigationModeUIHelper.CreateTab(string path) => CreateTab(path);
        void Services.Navigation.INavigationModeUIHelper.HighlightMatchingLibrary(Library library) => HighlightMatchingLibrary(library);
        void Services.Navigation.INavigationModeUIHelper.EnsureSelectedItemVisible(ListBox listBox, object selectedItem) => _uiHelperService?.EnsureSelectedItemVisible(listBox, selectedItem);
        void Services.Navigation.INavigationModeUIHelper.LoadLibraryFiles(Library library) => LoadLibraryFiles(library);
        void Services.Navigation.INavigationModeUIHelper.InitializeLibraryDragDrop() => InitializeLibraryDragDrop();
        void Services.Navigation.INavigationModeUIHelper.ApplyVisibleColumnsForCurrentMode() => ApplyVisibleColumnsForCurrentMode();
        void Services.Navigation.INavigationModeUIHelper.EnsureHeaderContextMenuHook() => EnsureHeaderContextMenuHook();
        void Services.Navigation.INavigationModeUIHelper.RefreshFileList() => RefreshFileList();
        void Services.Navigation.INavigationModeUIHelper.RefreshTagList() => NavigationPanelControl?.TagBrowsePanelControl?.RefreshTags();

        // IConfigUIHelper 实现 (部分属性已经是隐式实现，这里补充显式实现或新增属性)
        // IConfigUIHelper 实现 (显式实现以支持 internal 字段)
        Controls.FileBrowserControl Services.Config.IConfigUIHelper.FileBrowser => FileBrowser;
        RightPanelControl Services.Config.IConfigUIHelper.RightPanelControl => RightPanel;
        System.Windows.Window Services.Config.IConfigUIHelper.Window => this;
        System.Windows.Controls.Grid Services.Config.IConfigUIHelper.RootGrid => RootGrid;
        System.Windows.Controls.ColumnDefinition Services.Config.IConfigUIHelper.ColLeft => ColLeft;
        System.Windows.Controls.ColumnDefinition Services.Config.IConfigUIHelper.ColCenter => ColCenter;
        System.Windows.Controls.ColumnDefinition Services.Config.IConfigUIHelper.ColRight => ColRight;
        Controls.TitleActionBar Services.Config.IConfigUIHelper.TitleActionBar => FileBrowser?.ActionBar;
        string Services.Config.IConfigUIHelper.CurrentPath
        {
            get => _currentPath;
            set => _currentPath = value;
        }
        object Services.Config.IConfigUIHelper.CurrentLibrary => _currentLibrary;

        void Services.Config.IConfigUIHelper.AdjustColumnWidths() => _windowLifecycleHandler?.AdjustColumnWidths();
        void Services.Config.IConfigUIHelper.EnsureColumnMinWidths() => _windowLifecycleHandler?.EnsureColumnMinWidths();
        System.Windows.Threading.Dispatcher Services.Config.IConfigUIHelper.Dispatcher => this.Dispatcher;
        void Services.Config.IConfigUIHelper.UpdateWindowStateUI() => _windowLifecycleHandler?.UpdateWindowStateUI();


        public MainWindow()
        {
            try { System.IO.File.AppendAllText(@"f:\Download\GitHub\YiboFile\YiboFile-Core\debug_log.txt", $"[MainWindow] Constructor called at {DateTime.Now}\n"); } catch { }
            InitializeComponent();
            this.Title += " [FIXED]";







            // 订阅渲染完成事件，确保在窗口初次显示时强制修正布局
            // 这对于解决启动时右侧空白间隙至关重要，因为此时 ActualWidth 才有效
            this.ContentRendered += (s, e) =>
            {
                _windowLifecycleHandler?.AdjustColumnWidths();
            };

            InitializeServices();

            this.SizeChanged += (s, e) => UpdateTabManagerMargin();
            this.StateChanged += (s, e) => UpdateTabManagerMargin();

            // Step 1: Initialize services and config (Service Initialization Phase)
            var initializer = new Services.MainWindowInitializer(this);
            initializer.InitializeConfigServices();

            // Initialize Notification Service
            Services.Core.NotificationService.Instance.Initialize(NotificationContainer);

            // Step 2: Initialize Events (UI interactions)
            // 初始化 MVVM 架构 (Before Events to ensure ViewModel is ready for command binding)
            InitializeMvvmModules();

            // Step 3: Initialize Handlers (now they have access to _configService and _messageBus)
            InitializeHandlers();

            InitializeEvents();
            InitializeRailEvents();

            // Step 4: Apply Initial State (Load data, Restore persistence)
            initializer.ApplyInitialState();
        }

        /// <summary>
        /// 初始化 MVVM 模块
        /// </summary>
        private void InitializeMvvmModules()
        {
            // 获取消息总线
            _messageBus = App.ServiceProvider?.GetService<IMessageBus>() ?? MessageBus.Instance;

            // 创建 RightPanelViewModel
            var rightPanelVM = new RightPanelViewModel(_messageBus, _configService, _fileListService);

            // 创建 ViewModel
            _viewModel = new MainWindowViewModel(_messageBus, rightPanelVM);
            this.DataContext = _viewModel;

            // 创建并注册导航模块
            _navigationModule = new NavigationModule(
                _messageBus,
                _navigationService,
                path => NavigateToPathFromModule(path));
            _viewModel.RegisterModule(_navigationModule);


            // 创建并注册标签页模块
            // 注入双列表支持所需的 Service 和 状态判断委托
            _tabsModule = new TabsModule(
                _messageBus,
                _tabService,
                _secondTabService,
                () => this.IsDualListMode,
                () => _isSecondPaneFocused,
                (path, activate) => CreateTab(path, activate), // Keep callback for now just in case
                tabId => { /* SwitchToTab logic */ });
            _viewModel.RegisterModule(_tabsModule);

            // 初始化 NavigationModeService (必需，否则侧栏导航无效)
            _navigationModeService = new NavigationModeService(this, _navigationService, _tabService, _configService);


            // 创建并注册文件列表模块
            _fileListModule = new FileListModule(
                _messageBus,
                () => RefreshFileList(),
                () => ClearFilter());
            _viewModel.RegisterModule(_fileListModule);

            // 初始化主/副面板 MVVM (新的架构)
            _viewModel.PrimaryPane = new ViewModels.PaneViewModel(Dispatcher);
            _viewModel.SecondaryPane = new ViewModels.PaneViewModel(Dispatcher, isSecondary: true);

            // 初始化 FileListViewModel (用于数据绑定和加载)
            // 注意：FileBrowser 是 MainWindow 中的控件名称
            var fileListVM = new FileListViewModel(
                FileBrowser,
                this,
                () => RefreshFileList(),
                _columnService);
            _viewModel.PrimaryPane.FileList = fileListVM;

            // 关联模块到 ViewModel (方便直接访问)
            _viewModel.Navigation = _navigationModule;
            _viewModel.Tabs = _tabsModule;

            // 创建并注册布局模块
            _layoutModule = new LayoutModule(_messageBus);
            _viewModel.Layout = _layoutModule;
            _viewModel.RegisterModule(_layoutModule);

            // 初始化 LayoutMode，订阅 MVVM 消息 (必需，否则布局切换无效)
            InitializeLayoutMode();

            // 创建并注册文件操作模块
            var undoService = App.ServiceProvider.GetService<UndoService>();
            var errorService = App.ServiceProvider.GetService<ErrorService>();
            _fileOperationModule = new FileOperationModule(_messageBus, _fileOperationService, undoService, errorService);
            _viewModel.FileOperation = _fileOperationModule;
            _viewModel.RegisterModule(_fileOperationModule);

            // 创建并注册备注模块
            var notesService = App.ServiceProvider.GetService<Services.Features.FileNotes.INotesService>();
            if (notesService != null)
            {
                _notesModule = new NotesModule(_messageBus, notesService);
                _viewModel.Notes = _notesModule;
                _viewModel.RegisterModule(_notesModule);
            }

            // 创建并注册标签模块
            var tagService = App.ServiceProvider.GetService<Services.Features.ITagService>();
            if (tagService != null)
            {
                _tagsModule = new TagsModule(_messageBus, tagService);
                _viewModel.Tags = _tagsModule;
                _viewModel.RegisterModule(_tagsModule);
            }

            _libraryModule = new LibraryModule(_messageBus, _libraryService);
            _viewModel.Library = _libraryModule;
            _viewModel.RegisterModule(_libraryModule);

            // 订阅库选择消息，处理 UI 到逻辑的导航触发
            _messageBus.Subscribe<LibrarySelectedMessage>(m =>
            {
                if (m.Library != null)
                {
                    // 重新从数据库加载库信息，确保路径信息是最新的
                    var updatedLibrary = _libraryService.GetLibrary(m.Library.Id);
                    if (updatedLibrary != null)
                    {
                        // 使用统一导航协调器处理库导航（左键点击）
                        _navigationCoordinator.HandleLibraryNavigation(updatedLibrary, ClickType.LeftClick);
                        HighlightMatchingLibrary(updatedLibrary);
                    }
                }
            });

            // 创建并注册收藏模块
            _favoritesModule = new FavoritesModule(_messageBus, _favoriteService);
            _viewModel.Favorites = _favoritesModule;
            _viewModel.RegisterModule(_favoritesModule);

            // 初始化所有模块
            _viewModel.InitializeModules();

            System.Diagnostics.Debug.WriteLine("[MainWindow] MVVM modules initialized");
        }

        /// <summary>
        /// 从模块导航到路径（桥接方法）
        /// </summary>
        private void NavigateToPathFromModule(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            _currentPath = path;
            if (NavigationPanelControl != null) NavigationPanelControl.CurrentPath = path;

            _tabService?.UpdateActiveTabPath(path);
            ClearFilter();
            LoadCurrentDirectory();
            UpdateNavigationButtonsState();
            UpdatePropertiesButtonVisibility();
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

        private void InitializeRailEvents()
        {
            if (NavigationRail != null)
            {
                NavigationRail.NavigationModeChanged += OnRailNavigationModeChanged;
                NavigationRail.LayoutFocusRequested += (s, e) => SwitchLayoutMode(LayoutMode.Focus);
                NavigationRail.LayoutWorkRequested += (s, e) => SwitchLayoutMode(LayoutMode.Work);
                NavigationRail.LayoutFullRequested += (s, e) => SwitchLayoutMode(LayoutMode.Full);
                NavigationRail.DualListToggleRequested += (s, e) => DualListToggle_Click(s, null);
                NavigationRail.SettingsRequested += OnRailSettingsRequested;
                NavigationRail.AboutRequested += OnRailAboutRequested;
                NavigationRail.SetActiveMode("Path"); // Default
            }
        }

        private void OnRailNavigationModeChanged(object sender, string mode)
        {
            // Reset all special panels first
            if (TaskQueuePanel != null) TaskQueuePanel.Visibility = Visibility.Collapsed;
            if (ClipboardHistoryPanelControl != null) ClipboardHistoryPanelControl.Visibility = Visibility.Collapsed;
            if (BackupBrowser != null) BackupBrowser.Visibility = Visibility.Collapsed;

            if (mode == "Path" || mode == "Library" || mode == "Tag")
            {
                _navigationModeService.SwitchNavigationMode(mode);

                // Restore Main View (FileBrowser)
                if (FileBrowser != null) FileBrowser.Visibility = Visibility.Visible;

                // Ensure NavigationPanel is visible
                if (NavigationPanelControl != null) NavigationPanelControl.Visibility = Visibility.Visible;

                // Restore Side Panel (Standard Work Layout)
                if (SplitterLeft != null) SplitterLeft.Visibility = Visibility.Visible;
                if (ColLeft != null && ColLeft.Width.Value == 0) ColLeft.Width = new GridLength(220);

                // Restore Right Panel or Second File Browser based on mode
                if (RightPanel != null)
                    RightPanel.Visibility = _isDualListMode ? Visibility.Collapsed : Visibility.Visible;

                if (SecondFileBrowserContainer != null)
                    SecondFileBrowserContainer.Visibility = _isDualListMode ? Visibility.Visible : Visibility.Collapsed;

                if (SplitterRight != null) SplitterRight.Visibility = Visibility.Visible;
                if (ColRight != null && ColRight.Width.Value == 0) ColRight.Width = new GridLength(360);
            }
            else
            {
                // For Backup, Tasks, Clipboard:
                // 1. Show Navigation Side Panel (Column 1)
                if (NavigationPanelControl != null) NavigationPanelControl.Visibility = Visibility.Visible;
                if (SplitterLeft != null) SplitterLeft.Visibility = Visibility.Visible;
                if (ColLeft != null && ColLeft.Width.Value == 0) ColLeft.Width = new GridLength(220);

                // 2. Hide Main File Browser
                if (FileBrowser != null) FileBrowser.Visibility = Visibility.Collapsed;

                // 3. Hide Right Panel & Second Browser (occupied by special panel)
                if (RightPanel != null) RightPanel.Visibility = Visibility.Collapsed;
                if (SecondFileBrowserContainer != null) SecondFileBrowserContainer.Visibility = Visibility.Collapsed;
                if (SplitterRight != null) SplitterRight.Visibility = Visibility.Collapsed;
                // Don't set ColRight width to 0, just let the Grid.ColumnSpan="4" element take over, 
                // but we should ensure the column definitions allow it. 
                // Actually if we hide SplitterRight and RightPanel contents, and the BackupBrowser spans to Col 5, it works.

                // 4. Show Specific Panel
                if (mode == "Tasks")
                {
                    if (TaskQueuePanel != null) TaskQueuePanel.Visibility = Visibility.Visible;
                }
                else if (mode == "Backup")
                {
                    if (BackupBrowser != null) BackupBrowser.Visibility = Visibility.Visible;
                }
                else if (mode == "Clipboard")
                {
                    if (ClipboardHistoryPanelControl != null) ClipboardHistoryPanelControl.Visibility = Visibility.Visible;
                }
            }
        }

        private void OnRailSettingsRequested(object sender, EventArgs e) => _settingsOverlayController?.Toggle();

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
                _settingsOverlayController?.Hide();
            }
        }


        private void OnTagSelected(int tagId, string tagName)
        {
            // Consistent navigation: Use tag protocol with tag NAME
            // User requested opening in new tab
            CreateTab($"tag://{tagName}");
        }




        private void UpdateTabManagerMargin()
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



        internal void GridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            _fileBrowserEventHandler?.FileBrowser_GridSplitterDragDelta(sender, e);
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            // 关键修复：拖拽结束后，强制将中间列恢复为 Star，以消除右侧可能出现的空白间隙
            // 分割器拖拽会导致列宽变为固定值，如果总宽度小于窗口宽度就会产生空白
            if (ColCenter != null && !ColCenter.Width.IsStar)
            {
                ColCenter.Width = new GridLength(1, GridUnitType.Star);
            }

            // 拖拽结束后，立即保存（不延迟）
            if (_windowStateManager != null && this.IsLoaded)
            {
                // 强制更新布局
                RootGrid?.UpdateLayout();
                _windowStateManager.SaveAllState();
            }
        }





        #region 事件处理

        // Legacy handlers NavigateBack_Click and NavigateForward_Click have been migrated to MVVM Commands
        // private void NavigateBack_Click(object sender, RoutedEventArgs e) { ... }
        // private void NavigateForward_Click(object sender, RoutedEventArgs e) { ... }

        private void FileBrowser_ViewModeChanged(object sender, string mode)
        {
            // 根据视图模式设置文件名显示方式
            if (_fileListService != null)
            {
                // 缩略图模式：显示完整文件名（包括扩展名）
                // 列表模式：不显示扩展名（有单独的“类型”列）
                _fileListService.ShowFullFileName = (mode == "Thumbnail");
            }

            if (_configService != null)
            {
                _configService.Config.FileViewMode = mode;
                _configService.SaveCurrentConfig();
            }

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
            if (_configService != null)
            {
                _configService.Config.RightPanelNotesHeight = height;
                _configService.SaveCurrentConfig();
            }
        }

        private void FileBrowser_InfoHeightChanged(object sender, double height)
        {
            if (_configService != null)
            {
                _configService.Config.CenterPanelInfoHeight = height;
                _configService.SaveCurrentConfig();
            }
        }

        /// <summary>
        /// NavigationService 导航请求事件处理
        /// </summary>
        private void OnNavigationServiceNavigateRequested(object sender, string path)
        {
            // 导航服务已更新 CurrentPath，这里只需要同步 _currentPath
            _currentPath = path;
            // 同步更新标签页标题
            _tabService?.UpdateActiveTabPath(path);
            UpdateNavigationButtonsState();
        }

        /// <summary>
        /// 更新导航按钮状态
        /// </summary>
        internal void UpdateNavigationButtonsState()
        {
            // 使用 NavigationModeService 更新导航按钮状态
            if (_navigationModeService != null)
            {
                _navigationModeService.UpdateNavigationButtonsState();
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

            _selectionEventHandler?.HandleSelectionChanged(selectedItems);
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
        private void AutoSizeGridViewColumn(GridViewColumn column)
        {
            _columnInteractionHandler?.AutoSizeGridViewColumn(column);
        }

        // 右键列头 -> 列显示设置
        internal void EnsureHeaderContextMenuHook()
        {
            _columnInteractionHandler?.EnsureHeaderContextMenuHook();
        }

        internal string GetCurrentModeKey()
        {
            return _configService?.Config.LastNavigationMode ?? "Path";
        }

        internal string GetVisibleColumnsForCurrentMode()
        {
            return _columnService?.GetVisibleColumnsForCurrentMode() ?? "";
        }

        private void SetVisibleColumnsForCurrentMode(string csv)
        {
            _columnService?.SetVisibleColumnsForCurrentMode(csv);
        }

        internal void ApplyVisibleColumnsForCurrentMode()
        {
            _columnInteractionHandler?.ApplyVisibleColumnsForCurrentMode();
        }

        // 绑定列头分隔线双击
        internal void HookHeaderThumbs()
        {
            _columnInteractionHandler?.HookHeaderThumbs();
        }

        #region 键盘快捷键和文件操作



        // 文件操作桥接方法 - 已迁移到 FileOperationHandler
        private void Copy_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Copy_Click(sender, e);
        private void Cut_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Cut_Click(sender, e);

        /// <summary>
        /// 获取当前操作上下文
        /// </summary>
        private IFileOperationContext GetCurrentOperationContext() => _fileOperationHandler?.GetCurrentOperationContext();

        /// <summary>
        /// 执行复制操作
        /// </summary>
        /// <summary>
        /// 执行复制操作
        /// </summary>
        internal async void PerformCopyOperation() => await (_fileOperationHandler?.PerformCopyOperationAsync() ?? Task.CompletedTask);

        /// <summary>
        /// 执行剪切操作
        /// </summary>
        internal async void PerformCutOperation() => await (_fileOperationHandler?.PerformCutOperationAsync() ?? Task.CompletedTask);

        /// <summary>
        /// 执行删除操作
        /// </summary>
        internal async void PerformDeleteOperation() => await (_fileOperationHandler?.PerformDeleteOperationAsync() ?? Task.CompletedTask);

        private void Paste_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Paste_Click(sender, e);
        internal void Delete_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Delete_Click(sender, e);
        internal void Rename_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Rename_Click(sender, e);
        internal void ShowProperties_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ShowProperties_Click(sender, e);

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
            await _fileOperationService.CopyAsync(paths);
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
            await _fileOperationService.CutAsync(paths);
            Services.Core.NotificationService.ShowSuccess($"已剪切 {items.Count} 个项目");
        }

        /// <summary>
        /// 粘贴剪贴板内容 (使用 FileOperationService)
        /// 进度显示由 TaskQueuePanel 统一管理
        /// </summary>
        internal async Task PasteFilesAsync(CancellationToken ct = default)
        {
            var result = await _fileOperationService.PasteAsync(ct);
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
                _selectionEventHandler?.HandleNoSelection();
            }

            var result = await _fileOperationService.DeleteAsync(items, permanent);

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


    }







}
