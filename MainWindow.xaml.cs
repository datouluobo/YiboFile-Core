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
using OoiMRR.Services;
using OoiMRR.Services.FileNotes;
using OoiMRR.Services.Search;
using OoiMRR.Services.Navigation;
using OoiMRR.Services.FileOperations;
using OoiMRR.Services.Favorite;
using OoiMRR.Services.QuickAccess;
using OoiMRR.Services.FileList;
using OoiMRR.Services.Tabs;
using OoiMRR.Services.Preview;
using OoiMRR.Services.ColumnManagement;
using OoiMRR.Services.Config;
using OoiMRR.Handlers;
using System.Threading;
using System.Text.Json;
using System.Text;
// using TagTrain.UI; // Phase 2将重新实现
using OoiMRR.Models.UI;

namespace OoiMRR
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, IConfigUIHelper, Services.Navigation.INavigationModeUIHelper
    {
        internal string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        internal List<FileSystemItem> _currentFiles = new List<FileSystemItem>();

        private DragDropManager _dragDropManager;
        private System.Windows.Point _mouseDownPoint;
        private bool _isMouseDownOnListView = false;
        private bool _isMouseDownOnColumnHeader = false;
        internal Library _currentLibrary = null;
        internal Tag _currentTagFilter = null;
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
        private Services.FileNotes.FileNotesUIHandler _fileNotesUIHandler;
        // private Services.Tag.TagUIHandler _tagUIHandler; // Phase 2将重新实现

        // 事件处理器
        internal Handlers.FileBrowserEventHandler _fileBrowserEventHandler;
        internal Handlers.MenuEventHandler _menuEventHandler;
        internal Handlers.KeyboardEventHandler _keyboardEventHandler;
        internal Handlers.MouseEventHandler _mouseEventHandler;
        internal Handlers.ColumnInteractionHandler _columnInteractionHandler;
        internal Handlers.WindowLifecycleHandler _windowLifecycleHandler;
        internal Handlers.FileOperationHandler _fileOperationHandler;
        private SelectionEventHandler _selectionEventHandler;
        // internal Services.TagTrain.TagTrainEventHandler _tagTrainEventHandler; // Phase 2将重新实现

        // 加载锁定，防止重复加载导致卡死
        // TODO: 过渡代码 - 这些字段在事件处理器中仍在使用，后续考虑通过服务状态管理
        private bool _isLoadingFiles = false;
        private System.Threading.SemaphoreSlim _loadFilesSemaphore = new System.Threading.SemaphoreSlim(1, 1);

        // 定时器管理
        // 定时器管理
        internal System.Windows.Threading.DispatcherTimer _periodicTimer = new System.Windows.Threading.DispatcherTimer();
        internal System.Windows.Threading.DispatcherTimer _layoutCheckTimer = new System.Windows.Threading.DispatcherTimer();
        internal bool _isSplitterDragging = false; // 标记是否正在拖拽分割器
        internal Services.Search.SearchOptions _searchOptions = new Services.Search.SearchOptions();

        // OrderTagNames 方法已注释 - Phase 2将重新实现
        // internal List<string> OrderTagNames(List<int> tagIds)
        // {
        //     return new List<string>(); // 返回空列表
        // }

        // TagTrain 训练状态
        internal CancellationTokenSource _tagTrainTrainingCancellation = null;
        internal bool _tagTrainIsTraining = false;

        // TagClickMode 移除 - Phase 2将重新实现
        // Services.Navigation.TagClickMode Services.Navigation.INavigationModeUIHelper.TagClickMode
        // {
        //     get => (Services.Navigation.TagClickMode)_tagClickMode;
        //     set => _tagClickMode = (TagClickMode)value;
        // }


        private List<DraggableButton> _currentActionButtons = new List<DraggableButton>();
        private List<ActionItem> _actionItems = new List<ActionItem>(); // 保存按钮和分隔符的完整顺序

        // NavigationPanelControl控件的便捷访问属性
        // 改为 internal 以便 NavigationUIHelper 可以访问
        internal ListBox LibrariesListBox => NavigationPanelControl?.LibrariesListBoxControl;
        internal ListBox DrivesListBox => NavigationPanelControl?.DrivesListBoxControl;
        internal ListBox QuickAccessListBox => NavigationPanelControl?.QuickAccessListBoxControl;
        internal ListBox FavoritesListBox => NavigationPanelControl?.FavoritesListBoxControl;
        internal Grid NavPathContent => NavigationPanelControl?.NavPathContentControl;
        internal Grid NavLibraryContent => NavigationPanelControl?.NavLibraryContentControl;
        internal Grid NavTagContent => NavigationPanelControl?.NavTagContentControl;
        // internal TagPanel TagBrowsePanel => NavigationPanelControl?.TagBrowsePanelControl; // Phase 2
        // internal TagPanel TagEditPanel => NavigationPanelControl?.TagEditPanelControl; // Phase 2
        private StackPanel TagBottomButtons => NavigationPanelControl?.TagBottomButtonsControl;
        private StackPanel LibraryBottomButtons => NavigationPanelControl?.LibraryBottomButtonsControl;
        internal ContextMenu LibraryContextMenu => NavigationPanelControl?.LibraryContextMenuControl;

        // INavigationModeUIHelper 实现
        System.Windows.Threading.Dispatcher Services.Navigation.INavigationModeUIHelper.Dispatcher => this.Dispatcher;
        Library Services.Navigation.INavigationModeUIHelper.CurrentLibrary
        {
            get => _currentLibrary;
            set => _currentLibrary = value;
        }
        string Services.Navigation.INavigationModeUIHelper.CurrentPath
        {
            get => _currentPath;
            set => _currentPath = value;
        }
        StackPanel Services.Navigation.INavigationModeUIHelper.TagBottomButtons => TagBottomButtons;
        StackPanel Services.Navigation.INavigationModeUIHelper.LibraryBottomButtons => LibraryBottomButtons;
        Grid Services.Navigation.INavigationModeUIHelper.NavTagContent => NavTagContent;
        // TagTrain.UI.TagPanel Services.Navigation.INavigationModeUIHelper.TagBrowsePanel => TagBrowsePanel; // Phase 2
        // TagTrain.UI.TagPanel Services.Navigation.INavigationModeUIHelper.TagEditPanel => TagEditPanel; // Phase 2
        Controls.FileBrowserControl Services.Navigation.INavigationModeUIHelper.FileBrowser => FileBrowser;
        ListBox Services.Navigation.INavigationModeUIHelper.LibrariesListBox => LibrariesListBox;
        Controls.NavigationPanelControl Services.Navigation.INavigationModeUIHelper.NavigationPanelControl => NavigationPanelControl;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavPathButton => NavPathBtn;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavLibraryButton => NavLibraryBtn;
        // System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavTagButton => NavTagBtn; // Phase 2

        // INavigationModeUIHelper 方法实现
        // void Services.Navigation.INavigationModeUIHelper.InitializeTagTrainPanel() => InitializeTagTrainPanel(); // Phase 2
        void Services.Navigation.INavigationModeUIHelper.SwitchToTab(Services.Tabs.PathTab tab) => SwitchToTab(tab);
        void Services.Navigation.INavigationModeUIHelper.CreateTab(string path) => CreateTab(path);
        void Services.Navigation.INavigationModeUIHelper.HighlightMatchingLibrary(Library library) => HighlightMatchingLibrary(library);
        void Services.Navigation.INavigationModeUIHelper.EnsureSelectedItemVisible(ListBox listBox, object selectedItem) => _uiHelperService?.EnsureSelectedItemVisible(listBox, selectedItem);
        void Services.Navigation.INavigationModeUIHelper.LoadLibraryFiles(Library library) => LoadLibraryFiles(library);
        void Services.Navigation.INavigationModeUIHelper.InitializeLibraryDragDrop() => InitializeLibraryDragDrop();
        void Services.Navigation.INavigationModeUIHelper.ApplyVisibleColumnsForCurrentMode() => ApplyVisibleColumnsForCurrentMode();
        void Services.Navigation.INavigationModeUIHelper.EnsureHeaderContextMenuHook() => EnsureHeaderContextMenuHook();
        void Services.Navigation.INavigationModeUIHelper.RefreshFileList() => RefreshFileList();

        // IConfigUIHelper 实现 (部分属性已经是隐式实现，这里补充显式实现或新增属性)
        Controls.FileBrowserControl Services.Config.IConfigUIHelper.FileBrowser => FileBrowser;
        RightPanelControl Services.Config.IConfigUIHelper.RightPanelControl => RightPanel;

        public MainWindow()
        {
            InitializeComponent();







            // 订阅渲染完成事件，确保在窗口初次显示时强制修正布局
            // 这对于解决启动时右侧空白间隙至关重要，因为此时 ActualWidth 才有效
            this.ContentRendered += (s, e) =>
            {
                _windowLifecycleHandler?.AdjustColumnWidths();
            };

            InitializeServices();

            // Step 1: Initialize services and config (Service Initialization Phase)
            var initializer = new Services.MainWindowInitializer(this);
            initializer.InitializeConfigServices();

            // Initialize Notification Service
            Services.Core.NotificationService.Instance.Initialize(NotificationContainer);

            // Step 2: Initialize Handlers (now they have access to _configService)
            InitializeHandlers();

            // Step 3: Initialize Events (UI interactions)
            InitializeEvents();

            // Step 4: Apply Initial State (Logic/UI Update Phase)
            initializer.ApplyInitialState();

            // 订阅标签页管理器的新建标签页事件
            TabManager.NewTabRequested += (s, e) =>
            {
                // 创建空白标签页
                CreateTab(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            };

            // Hook up dynamic tab margin adjustment
            if (WindowButtonsStackPanel != null)
            {
                // 订阅窗口按钮栏size变化以调整TabManager的Margin
                WindowButtonsStackPanel.SizeChanged += (s, e) => UpdateTabManagerMargin();
            }

            // 初始化时和窗口状态/大小变化时更新TabManager的Margin
            this.Loaded += (s, e) => UpdateTabManagerMargin();
            this.Loaded += (s, e) => InitializeLayoutMode(); // 初始化布局(仅恢复)
            this.StateChanged += (s, e) => UpdateTabManagerMargin();
            this.SizeChanged += (s, e) => UpdateTabManagerMargin();

            // 立即更新一次
            UpdateTabManagerMargin();
        }

        private void UpdateTabManagerMargin()
        {
            this.Dispatcher.InvokeAsync(UpdateTabManagerMarginLogic, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void UpdateTabManagerMarginLogic()
        {
            if (TabManager != null && WindowButtonsStackPanel != null)
            {
                // Ensure tabs don't overlap with window control buttons
                // Add a small buffer (e.g. 10px) to the buttons' actual width
                double rightMargin = WindowButtonsStackPanel.ActualWidth + 15;

                // Keep the other margins as defined in XAML (0,0,0,0) - wait, XAML had 0,0,250,0
                // We overwrite the Right margin dynamically.
                TabManager.Margin = new Thickness(0, 0, rightMargin, 0);
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

        internal void NavigateBack_Click(object sender, RoutedEventArgs e)
        {
            string path = _navigationService.NavigateBack();
            if (!string.IsNullOrEmpty(path))
            {
                _currentPath = path;
                ClearFilter();
                LoadCurrentDirectory();
                UpdateNavigationButtonsState();
            }
        }

        private void NavigateForward_Click(object sender, RoutedEventArgs e)
        {
            string path = _navigationService.NavigateForward();
            if (!string.IsNullOrEmpty(path))
            {
                _currentPath = path;
                ClearFilter();
                LoadCurrentDirectory();
                UpdateNavigationButtonsState();
            }
        }

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

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            string parentPath = _navigationService.NavigateUp();
            if (!string.IsNullOrEmpty(parentPath))
            {
                NavigateToPath(parentPath);
            }
        }

        private void NavigateUp_Click(object sender, RoutedEventArgs e)
        {
            string parentPath = _navigationService.NavigateUp();
            if (!string.IsNullOrEmpty(parentPath))
            {
                NavigateToPath(parentPath);
            }
        }

        /// <summary>
        /// NavigationService 导航请求事件处理
        /// </summary>
        private void OnNavigationServiceNavigateRequested(object sender, string path)
        {
            // 导航服务已更新 CurrentPath，这里只需要同步 _currentPath
            _currentPath = path;
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
        internal void Refresh_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Refresh_Click(sender, e);
        private void ClearFilter_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ClearFilter_Click(sender, e);

        internal void ClearFilter()
        {
            // 清除过滤状态，恢复正常的文件浏览
            _currentTagFilter = null;
            // TagsListBox已移除，标签选择现在由TagTrain面板处理

            _currentFiles.Clear();
            if (FileBrowser != null)
                FileBrowser.FilesItemsSource = null;
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

        internal void FilesListView_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 如果双击发生在列头或分隔线，拦截，不进行文件/文件夹打开
            var src = e.OriginalSource as DependencyObject;
            if (src != null)
            {
                if (FindAncestor<GridViewColumnHeader>(src) != null ||
                    FindAncestor<System.Windows.Controls.Primitives.Thumb>(src) != null)
                {
                    e.Handled = true;
                    return;
                }
            }
            // Preview 事件优先处理
            HandleDoubleClick(e);
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
        internal void PerformCopyOperation() => _fileOperationHandler?.PerformCopyOperation();

        /// <summary>
        /// 执行剪切操作
        /// </summary>
        internal void PerformCutOperation() => _fileOperationHandler?.PerformCutOperation();

        /// <summary>
        /// 执行删除操作
        /// </summary>
        internal async void PerformDeleteOperation() => await (_fileOperationHandler?.PerformDeleteOperationAsync() ?? Task.CompletedTask);

        private void Paste_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Paste_Click(sender, e);
        internal void Delete_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Delete_Click(sender, e);
        internal void Rename_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Rename_Click(sender, e);
        internal void ShowProperties_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ShowProperties_Click(sender, e);

        #endregion

        #region 列表排序



        #endregion


    }



    #region IConfigUIHelper 实现

    /// <summary>
    /// IConfigUIHelper 接口实现
    /// </summary>
    public partial class MainWindow
    {
        Window IConfigUIHelper.Window => this;

        Grid IConfigUIHelper.RootGrid => this.RootGrid;

        ColumnDefinition IConfigUIHelper.ColLeft => this.ColLeft;

        ColumnDefinition IConfigUIHelper.ColCenter => this.ColCenter;

        ColumnDefinition IConfigUIHelper.ColRight => this.ColRight;

        Controls.TitleActionBar IConfigUIHelper.TitleActionBar => this.FileBrowser?.ActionBar;

        string IConfigUIHelper.CurrentPath
        {
            get => _currentPath;
            set => _currentPath = value;
        }

        object IConfigUIHelper.CurrentLibrary => _currentLibrary;





        void IConfigUIHelper.AdjustColumnWidths()
        {
            AdjustColumnWidths();
        }

        void IConfigUIHelper.EnsureColumnMinWidths()
        {
            EnsureColumnMinWidths();
        }

        System.Windows.Threading.Dispatcher IConfigUIHelper.Dispatcher => this.Dispatcher;



        void IConfigUIHelper.UpdateWindowStateUI()
        {
            UpdateWindowStateUI();
        }

        /// <summary>
        /// 清除其他导航区域的选择状态，确保同时只有一个区域显示选中
        /// </summary>
        /// <param name="exceptSource">不清除哪个源 ("Drives", "QuickAccess", "Favorites")</param>
        private void ClearOtherNavigationSelections(string exceptSource)
        {
            if (exceptSource != "Drives" && DrivesListBox != null)
            {
                DrivesListBox.SelectedItem = null;
            }
            if (exceptSource != "QuickAccess" && QuickAccessListBox != null)
            {
                QuickAccessListBox.SelectedItem = null;
            }
            if (exceptSource != "Favorites" && FavoritesListBox != null)
            {
                FavoritesListBox.SelectedItem = null;
            }
        }
    }

    #endregion
}


