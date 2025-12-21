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
using TagTrain.UI;
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
        private Services.Tag.TagUIHandler _tagUIHandler;

        // 事件处理器
        internal Handlers.FileBrowserEventHandler _fileBrowserEventHandler;
        internal Handlers.MenuEventHandler _menuEventHandler;
        internal Handlers.KeyboardEventHandler _keyboardEventHandler;
        internal Handlers.MouseEventHandler _mouseEventHandler;
        private SelectionEventHandler _selectionEventHandler;
        internal Services.TagTrain.TagTrainEventHandler _tagTrainEventHandler;

        // 加载锁定，防止重复加载导致卡死
        // TODO: 过渡代码 - 这些字段在事件处理器中仍在使用，后续考虑通过服务状态管理
        private bool _isLoadingFiles = false;
        private System.Threading.SemaphoreSlim _loadFilesSemaphore = new System.Threading.SemaphoreSlim(1, 1);
        private System.Threading.CancellationTokenSource _currentFolderSizeCTS;

        // 定时器管理
        // 定时器管理
        internal System.Windows.Threading.DispatcherTimer _periodicTimer = new System.Windows.Threading.DispatcherTimer();
        internal System.Windows.Threading.DispatcherTimer _layoutCheckTimer = new System.Windows.Threading.DispatcherTimer();
        internal bool _isSplitterDragging = false; // 标记是否正在拖拽分割器
        internal Services.Search.SearchOptions _searchOptions = new Services.Search.SearchOptions();

        // 统一的标签显示排序：当前过滤标签优先，其余按名称升序
        internal List<string> OrderTagNames(List<int> tagIds)
        {
            try
            {
                var pairs = tagIds
                    .Select(id => new { Id = id, Name = OoiMRRIntegration.GetTagName(id) })
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .ToList();

                var comparer = StringComparer.CurrentCultureIgnoreCase;
                int currentId = _currentTagFilter?.Id ?? int.MinValue;

                var ordered = pairs
                    .OrderBy(p => p.Id == currentId ? 0 : 1)   // 当前筛选的标签放最前
                    .ThenBy(p => p.Name, comparer)
                    .Select(p => p.Name)
                    .ToList();

                return ordered;
            }
            catch
            {
                // 回退到按名称排序
                return tagIds
                    .Select(id => OoiMRRIntegration.GetTagName(id))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
        }

        // TagTrain 训练状态
        internal CancellationTokenSource _tagTrainTrainingCancellation = null;
        internal bool _tagTrainIsTraining = false;

        // 标签点击模式
        internal enum TagClickMode { Browse, Edit }
        internal TagClickMode _tagClickMode = TagClickMode.Browse;

        // INavigationModeUIHelper 实现
        Services.Navigation.TagClickMode Services.Navigation.INavigationModeUIHelper.TagClickMode
        {
            get => (Services.Navigation.TagClickMode)_tagClickMode;
            set => _tagClickMode = (TagClickMode)value;
        }


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
        internal TagPanel TagBrowsePanel => NavigationPanelControl?.TagBrowsePanelControl;
        internal TagPanel TagEditPanel => NavigationPanelControl?.TagEditPanelControl;
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
        TagTrain.UI.TagPanel Services.Navigation.INavigationModeUIHelper.TagBrowsePanel => TagBrowsePanel;
        TagTrain.UI.TagPanel Services.Navigation.INavigationModeUIHelper.TagEditPanel => TagEditPanel;
        Controls.FileBrowserControl Services.Navigation.INavigationModeUIHelper.FileBrowser => FileBrowser;
        ListBox Services.Navigation.INavigationModeUIHelper.LibrariesListBox => LibrariesListBox;
        Controls.NavigationPanelControl Services.Navigation.INavigationModeUIHelper.NavigationPanelControl => NavigationPanelControl;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavPathButton => NavPathBtn;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavLibraryButton => NavLibraryBtn;
        System.Windows.Controls.Button Services.Navigation.INavigationModeUIHelper.NavTagButton => NavTagBtn;

        // INavigationModeUIHelper 方法实现
        void Services.Navigation.INavigationModeUIHelper.InitializeTagTrainPanel() => InitializeTagTrainPanel();
        void Services.Navigation.INavigationModeUIHelper.SwitchToTab(Services.Tabs.PathTab tab) => SwitchToTab(tab);
        void Services.Navigation.INavigationModeUIHelper.CreateTab(string path) => CreateTab(path);
        void Services.Navigation.INavigationModeUIHelper.HighlightMatchingLibrary(Library library) => HighlightMatchingLibrary(library);
        void Services.Navigation.INavigationModeUIHelper.EnsureSelectedItemVisible(ListBox listBox, object selectedItem) => _uiHelperService?.EnsureSelectedItemVisible(listBox, selectedItem);
        void Services.Navigation.INavigationModeUIHelper.LoadLibraryFiles(Library library) => LoadLibraryFiles(library);
        void Services.Navigation.INavigationModeUIHelper.InitializeLibraryDragDrop() => InitializeLibraryDragDrop();
        void Services.Navigation.INavigationModeUIHelper.ApplyVisibleColumnsForCurrentMode() => ApplyVisibleColumnsForCurrentMode();
        void Services.Navigation.INavigationModeUIHelper.EnsureHeaderContextMenuHook() => EnsureHeaderContextMenuHook();
        void Services.Navigation.INavigationModeUIHelper.RefreshFileList() => RefreshFileList();

        public MainWindow()
        {
            InitializeComponent();

            InitializeServices();
            InitializeEvents();

            InitializeHandlers();
        }



        internal void GridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            _fileBrowserEventHandler?.FileBrowser_GridSplitterDragDelta(sender, e);
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[GridSplitter] DragCompleted 触发，开始保存");
            // 拖拽结束后，立即保存（不延迟）
            if (_windowStateManager != null && this.IsLoaded)
            {
                // 强制更新布局
                RootGrid?.UpdateLayout();
                System.Diagnostics.Debug.WriteLine("[GridSplitter] 布局已更新，保存状态");
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












        /// <summary>
        /// 立即计算指定文件夹的大小（用户选中时触发）
        /// </summary>
        private void CalculateFolderSizeImmediately(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return;

            // 取消之前的计算任务
            if (_currentFolderSizeCTS != null)
            {
                _currentFolderSizeCTS.Cancel();
                _currentFolderSizeCTS.Dispose();
            }
            _currentFolderSizeCTS = new System.Threading.CancellationTokenSource();
            var token = _currentFolderSizeCTS.Token;

            // 先更新UI显示"计算中..."，给用户即时反馈
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (token.IsCancellationRequested) return;

                var item = _currentFiles.FirstOrDefault(f => f.Path == folderPath);
                if (item != null && (string.IsNullOrEmpty(item.Size) || item.Size == "-" || item.Size == "计算中..."))
                {
                    item.Size = "计算中...";
                    // 仅刷新该项，避免刷新整个列表导致闪烁（如果有机制的话，这里还是刷新全部）
                    // 优化：如果可能，只更新单个Item的UI，但Binding通常需要NotifyPropertyChanged
                    // 这里FileSystemItem实现了INotifyPropertyChanged吗？假设它绑定机制有效。
                    // 刷新CollectionView虽然开销大，但为了确保"计算中..."立即显示
                    // var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                    // collectionView?.Refresh();

                    // 使用 FileListService 计算文件夹大小
                    // 计算完成后会通过 FolderSizeCalculated 事件更新UI
                    _ = _fileListService.CalculateFolderSizeAsync(item, token);
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

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





        // 根据内容自动调整列宽（用于双击列分隔条）
        private void AutoSizeGridViewColumn(GridViewColumn column)
        {
            if (FileBrowser == null || column == null) return;
            _columnService?.AutoSizeGridViewColumn(column, FileBrowser);
        }

        // 右键列头 -> 列显示设置
        internal void EnsureHeaderContextMenuHook()
        {
            if (FileBrowser?.FilesList == null) return;
            FileBrowser.FilesList.PreviewMouseRightButtonUp -= FilesList_PreviewMouseRightButtonUp_HeaderMenu;
            FileBrowser.FilesList.PreviewMouseRightButtonUp += FilesList_PreviewMouseRightButtonUp_HeaderMenu;
        }

        private void FilesList_PreviewMouseRightButtonUp_HeaderMenu(object sender, MouseButtonEventArgs e)
        {
            var src = e.OriginalSource as DependencyObject;
            if (src == null) return;

            // 检查是否点击在列头上
            var header = FindAncestor<GridViewColumnHeader>(src);
            if (header != null)
            {
                // 在列头上右键，显示列选择菜单（弹出选项菜单，与列3一致）
                e.Handled = true;

                // 创建弹出菜单
                var cm = new ContextMenu();
                var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
                var visibleSet = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

                if (FileBrowser?.FilesGrid != null)
                {
                    foreach (var column in FileBrowser.FilesGrid.Columns)
                    {
                        var colHeader = column.Header as GridViewColumnHeader;
                        var tag = colHeader?.Tag?.ToString();
                        if (string.IsNullOrEmpty(tag)) continue;

                        string title = colHeader?.Content?.ToString() ?? tag;
                        bool isVisible = visibleSet.Contains(tag);

                        var mi = new MenuItem
                        {
                            Header = $"列: {title}",
                            IsCheckable = true,
                            IsChecked = isVisible
                        };

                        mi.Checked += (s, ev) =>
                        {
                            // 显示列
                            if (column.Width <= 1)
                            {
                                double w = tag switch
                                {
                                    "Name" => _configService?.Config.ColNameWidth ?? 200,
                                    "Size" => _configService?.Config.ColSizeWidth ?? 100,
                                    "Type" => _configService?.Config.ColTypeWidth ?? 100,
                                    "ModifiedDate" => _configService?.Config.ColModifiedDateWidth ?? 150,
                                    "CreatedTime" => _configService?.Config.ColCreatedTimeWidth ?? 50,
                                    "Tags" => _configService?.Config.ColTagsWidth ?? 150,
                                    "Notes" => _configService?.Config.ColNotesWidth ?? 200,
                                    _ => column.ActualWidth > 0 ? column.ActualWidth : 100
                                };
                                column.Width = Math.Max(40, w);
                            }
                            // 更新配置
                            var currentVisible = GetVisibleColumnsForCurrentMode() ?? "";
                            var currentSet = new HashSet<string>(currentVisible.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                            currentSet.Add(tag);
                            SetVisibleColumnsForCurrentMode(string.Join(",", currentSet));
                            _configService?.SaveCurrentConfig();
                        };

                        mi.Unchecked += (s, ev) =>
                        {
                            // 隐藏列
                            column.Width = 0;
                            // 更新配置
                            var currentVisible = GetVisibleColumnsForCurrentMode() ?? "";
                            var currentSet = new HashSet<string>(currentVisible.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                            currentSet.Remove(tag);
                            SetVisibleColumnsForCurrentMode(string.Join(",", currentSet));
                            _configService?.SaveCurrentConfig();
                        };

                        cm.Items.Add(mi);
                    }
                }

                // 将菜单附加到列头元素上并显示
                cm.PlacementTarget = header;
                cm.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                cm.IsOpen = true;
                return;
            }

            // 不在列头上，不处理，让文件列表的右键菜单显示
            // 不设置 e.Handled，让事件继续传播到文件列表的 ContextMenu
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
            if (FileBrowser == null) return;
            _columnService?.ApplyVisibleColumnsForCurrentMode(FileBrowser);
            HookHeaderThumbs();
        }

        // 绑定列头分隔线双击
        internal void HookHeaderThumbs()
        {
            if (FileBrowser?.FilesGrid == null) return;
            foreach (var column in FileBrowser.FilesGrid.Columns)
            {
                if (column.Header is GridViewColumnHeader header)
                {
                    header.PreviewMouseLeftButtonDown -= Header_PreviewMouseLeftButtonDown_ForThumb;
                    header.PreviewMouseLeftButtonDown += Header_PreviewMouseLeftButtonDown_ForThumb;

                    // 确保模板应用后再挂载Thumb事件
                    header.Loaded -= Header_Loaded_AttachThumb;
                    header.Loaded += Header_Loaded_AttachThumb;
                }
            }
        }

        private void Header_PreviewMouseLeftButtonDown_ForThumb(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            if (sender is GridViewColumnHeader header)
            {
                // Header 级别也拦截（无论Thumb是否已处理）
                if (header.Column != null)
                {
                    AutoSizeGridViewColumn(header.Column);
                    e.Handled = true;
                }
            }
        }

        // 使用 AddHandler 捕获的分隔线双击
        internal void FilesList_HeaderThumbDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) return;
            var src = e.OriginalSource as DependencyObject;
            if (src == null) return;
            var header = FindAncestor<GridViewColumnHeader>(src);
            var thumb = FindAncestor<System.Windows.Controls.Primitives.Thumb>(src);
            if (header != null && thumb != null && header.Column != null)
            {
                AutoSizeGridViewColumn(header.Column);
                e.Handled = true;
            }
        }

        private void Header_Loaded_AttachThumb(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader header)
            {
                var thumb = FindHeaderThumb(header);
                if (thumb != null)
                {
                    thumb.DragStarted -= HeaderThumb_DragStarted;
                    thumb.DragDelta -= HeaderThumb_DragDelta;
                    thumb.DragStarted += HeaderThumb_DragStarted;
                    thumb.DragDelta += HeaderThumb_DragDelta;
                }
            }
        }

        private void HeaderThumb_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            var header = FindAncestor<GridViewColumnHeader>(sender as DependencyObject);
            if (header?.Column == null) return;
            var tag = header.Tag?.ToString();
            if (!IsColumnVisible(tag))
            {
                // 阻止隐藏列被拖动展开
                header.Column.Width = 0;
                e.Handled = true;
            }
        }

        private void HeaderThumb_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var header = FindAncestor<GridViewColumnHeader>(sender as DependencyObject);
            if (header?.Column == null) return;
            var tag = header.Tag?.ToString();
            if (!IsColumnVisible(tag))
            {
                // 该列是隐藏列：把拖动量转嫁到左侧最近的可见列，避免用户感觉被阻塞
                header.Column.Width = 0; // 自身保持隐藏

                // 在列集合中找到当前列索引
                var gridView = FileBrowser?.FilesGrid;
                if (gridView != null)
                {
                    int idx = gridView.Columns.IndexOf(header.Column);
                    // 向左寻找最近的可见列
                    for (int i = idx - 1; i >= 0; i--)
                    {
                        var leftCol = gridView.Columns[i];
                        var leftHeader = leftCol.Header as GridViewColumnHeader;
                        var leftTag = leftHeader?.Tag?.ToString();
                        if (IsColumnVisible(leftTag))
                        {
                            double min = 40; // 最小宽度保护
                            double newWidth = Math.Max(min, leftCol.Width + e.HorizontalChange);
                            leftCol.Width = newWidth;
                            e.Handled = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                // 该列可见，但其右邻居可能是隐藏列：当向右拖动时，把扩展量施加到当前列，同时保持右邻居为0
                var gridView = FileBrowser?.FilesGrid;
                if (gridView != null && e.HorizontalChange > 0)
                {
                    int idx = gridView.Columns.IndexOf(header.Column);
                    if (idx >= 0 && idx + 1 < gridView.Columns.Count)
                    {
                        var rightCol = gridView.Columns[idx + 1];
                        var rightHeader = rightCol.Header as GridViewColumnHeader;
                        var rightTag = rightHeader?.Tag?.ToString();
                        if (!IsColumnVisible(rightTag))
                        {
                            // 右侧隐藏：放大当前列，并强制右侧维持隐藏
                            double min = 40;
                            header.Column.Width = Math.Max(min, header.Column.Width + e.HorizontalChange);
                            if (rightCol.Width != 0) rightCol.Width = 0;
                            e.Handled = true;
                        }
                    }
                }
            }
        }

        private bool IsColumnVisible(string tag)
        {
            return _columnService?.IsColumnVisible(tag) ?? true;
        }

        private System.Windows.Controls.Primitives.Thumb FindHeaderThumb(GridViewColumnHeader header)
        {
            // 先尝试按模板名
            var thumb = header.Template?.FindName("PART_HeaderGripper", header) as System.Windows.Controls.Primitives.Thumb;
            if (thumb != null) return thumb;
            // 否则在视觉树中查找
            return FindDescendant<System.Windows.Controls.Primitives.Thumb>(header);
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



        #region 键盘快捷键和文件操作



        // 文件操作事件桥接方法 - 已迁移到 MenuEventHandler
        private void Copy_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Copy_Click(sender, e);
        private void Cut_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Cut_Click(sender, e);

        /// <summary>
        /// 获取当前操作上下文
        /// </summary>
        private IFileOperationContext GetCurrentOperationContext()
        {
            if (_currentLibrary != null)
            {
                return new LibraryOperationContext(_currentLibrary, FileBrowser, this, () => LoadLibraryFiles(_currentLibrary));
            }
            else if (_currentTagFilter != null)
            {
                return new TagOperationContext(_currentTagFilter, FileBrowser, this, () => FilterByTag(_currentTagFilter));
            }
            else
            {
                return new PathOperationContext(_currentPath, FileBrowser, this, LoadCurrentDirectory);
            }
        }

        /// <summary>
        /// 执行复制操作
        /// </summary>
        internal void PerformCopyOperation()
        {
            try
            {
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var paths = selectedItems.Select(item => item.Path).ToList();
                FileClipboardManager.SetCopyPaths(paths);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行剪切操作
        /// </summary>
        internal void PerformCutOperation()
        {
            try
            {
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var paths = selectedItems.Select(item => item.Path).ToList();
                FileClipboardManager.SetCutPaths(paths);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"剪切操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行粘贴操作
        /// </summary>
        internal void PerformPasteOperation()
        {
            try
            {
                var copiedPaths = FileClipboardManager.GetCopiedPaths();
                if (copiedPaths == null || copiedPaths.Count == 0)
                {
                    return;
                }

                var context = GetCurrentOperationContext();
                if (context == null)
                {
                    return;
                }

                var isCut = FileClipboardManager.IsCutOperation;
                var pasteOperation = new PasteOperation(context);
                pasteOperation.Execute(copiedPaths, isCut);

                if (isCut)
                {
                    FileClipboardManager.ClearCutOperation();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行删除操作
        /// </summary>
        internal async void PerformDeleteOperation()
        {
            try
            {
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var context = GetCurrentOperationContext();
                if (context == null)
                {
                    return;
                }

                var deleteOperation = new DeleteOperation(context);
                await deleteOperation.ExecuteAsync(selectedItems);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Paste_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Paste_Click(sender, e);
        internal void Delete_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Delete_Click(sender, e);
        internal void Rename_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.Rename_Click(sender, e);
        internal void ShowProperties_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ShowProperties_Click(sender, e);

        #endregion

        #region 列表排序



        #endregion


    }



    /// <summary>
    /// 导航 UI 辅助实现类
    /// </summary>
    internal class NavigationUIHelper : Services.Navigation.INavigationUIHelper
    {
        private readonly MainWindow _mainWindow;

        public NavigationUIHelper(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public System.Windows.Threading.Dispatcher Dispatcher => _mainWindow.Dispatcher;

        public IEnumerable GetDrivesListItems()
        {
            return _mainWindow.DrivesListBox?.Items;
        }

        public IEnumerable GetQuickAccessListItems()
        {
            return _mainWindow.QuickAccessListBox?.Items;
        }

        public IEnumerable GetFavoritesListItems()
        {
            return _mainWindow.FavoritesListBox?.Items;
        }

        public IEnumerable GetLibrariesListItems()
        {
            return _mainWindow.LibrariesListBox?.Items;
        }

        public void SetItemHighlight(string listType, object item, bool highlight)
        {
            ListBox listBox = null;
            switch (listType)
            {
                case "Drive":
                    listBox = _mainWindow.DrivesListBox;
                    break;
                case "QuickAccess":
                    listBox = _mainWindow.QuickAccessListBox;
                    break;
                case "Favorites":
                    listBox = _mainWindow.FavoritesListBox;
                    break;
                case "Library":
                    listBox = _mainWindow.LibrariesListBox;
                    break;
            }

            if (listBox == null || item == null) return;

            try
            {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                if (container != null)
                {
                    if (highlight)
                    {
                        // 检查是否已经高亮，避免重复设置
                        if (container.Tag as string == "Match")
                            return;

                        // 匹配当前路径/库：无论是否选中，都显示黄色（优先级最高）
                        var yellowBg = _mainWindow.FindResource("HighlightBrush") as SolidColorBrush;
                        var blackFg = _mainWindow.FindResource("HighlightForegroundBrush") as SolidColorBrush;
                        var orangeBorder = _mainWindow.FindResource("HighlightBorderBrush") as SolidColorBrush;

                        container.ClearValue(ListBoxItem.BackgroundProperty);
                        container.ClearValue(ListBoxItem.ForegroundProperty);
                        container.ClearValue(ListBoxItem.BorderBrushProperty);
                        container.SetValue(ListBoxItem.BackgroundProperty, yellowBg);
                        container.SetValue(ListBoxItem.ForegroundProperty, blackFg);
                        container.SetValue(ListBoxItem.BorderBrushProperty, orangeBorder);
                        container.Tag = "Match";
                    }
                    else
                    {
                        // 清除匹配高亮
                        var tag = container.Tag as string;
                        if (tag == "Match")
                        {
                            container.Tag = null;
                            container.ClearValue(ListBoxItem.BackgroundProperty);
                            container.ClearValue(ListBoxItem.ForegroundProperty);
                            container.ClearValue(ListBoxItem.BorderBrushProperty);
                        }
                    }
                }
                else
                {
                    // 如果容器还未生成，延迟执行（使用低优先级避免阻塞UI）
                    _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var retryContainer = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                        if (retryContainer != null)
                        {
                            if (highlight)
                            {
                                if (retryContainer.Tag as string == "Match")
                                    return;

                                var yellowBg = _mainWindow.FindResource("HighlightBrush") as SolidColorBrush;
                                var blackFg = _mainWindow.FindResource("HighlightForegroundBrush") as SolidColorBrush;
                                var orangeBorder = _mainWindow.FindResource("HighlightBorderBrush") as SolidColorBrush;

                                retryContainer.ClearValue(ListBoxItem.BackgroundProperty);
                                retryContainer.ClearValue(ListBoxItem.ForegroundProperty);
                                retryContainer.ClearValue(ListBoxItem.BorderBrushProperty);
                                retryContainer.SetValue(ListBoxItem.BackgroundProperty, yellowBg);
                                retryContainer.SetValue(ListBoxItem.ForegroundProperty, blackFg);
                                retryContainer.SetValue(ListBoxItem.BorderBrushProperty, orangeBorder);
                                retryContainer.Tag = "Match";
                            }
                            else
                            {
                                var tag = retryContainer.Tag as string;
                                if (tag == "Match")
                                {
                                    retryContainer.Tag = null;
                                    retryContainer.ClearValue(ListBoxItem.BackgroundProperty);
                                    retryContainer.ClearValue(ListBoxItem.ForegroundProperty);
                                    retryContainer.ClearValue(ListBoxItem.BorderBrushProperty);
                                }
                            }
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置列表项高亮失败: {ex.Message}");
            }
        }

        public void ClearListBoxHighlights(string listType)
        {
            ListBox listBox = null;
            switch (listType)
            {
                case "Drive":
                    listBox = _mainWindow.DrivesListBox;
                    break;
                case "QuickAccess":
                    listBox = _mainWindow.QuickAccessListBox;
                    break;
                case "Favorites":
                    listBox = _mainWindow.FavoritesListBox;
                    break;
                case "Library":
                    listBox = _mainWindow.LibrariesListBox;
                    break;
            }

            if (listBox == null || listBox.Items == null) return;

            foreach (var item in listBox.Items)
            {
                SetItemHighlight(listType, item, false);
            }
        }

        public void SetNavigationContentVisibility(string mode)
        {
            // 隐藏所有导航内容（添加空值检查）
            if (_mainWindow.NavPathContent != null) _mainWindow.NavPathContent.Visibility = Visibility.Collapsed;
            if (_mainWindow.NavLibraryContent != null) _mainWindow.NavLibraryContent.Visibility = Visibility.Collapsed;
            if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Collapsed;

            // 根据模式显示对应内容
            switch (mode)
            {
                case "Path":
                    if (_mainWindow.NavPathContent != null) _mainWindow.NavPathContent.Visibility = Visibility.Visible;
                    if (_mainWindow.NavLibraryContent != null) _mainWindow.NavLibraryContent.Visibility = Visibility.Collapsed;
                    if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Collapsed;
                    break;
                case "Library":
                    if (_mainWindow.NavPathContent != null) _mainWindow.NavPathContent.Visibility = Visibility.Collapsed;
                    if (_mainWindow.NavLibraryContent != null) _mainWindow.NavLibraryContent.Visibility = Visibility.Visible;
                    if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Collapsed;
                    break;
                case "Tag":
                    if (_mainWindow.NavPathContent != null) _mainWindow.NavPathContent.Visibility = Visibility.Collapsed;
                    if (_mainWindow.NavLibraryContent != null) _mainWindow.NavLibraryContent.Visibility = Visibility.Collapsed;
                    if (_mainWindow.NavTagContent != null) _mainWindow.NavTagContent.Visibility = Visibility.Visible;
                    break;
            }
        }

        public void UpdateActionButtons(string mode)
        {
            // 使用 ConfigService 更新操作按钮
            _mainWindow._configService?.UpdateActionButtons(mode);
        }

        public SolidColorBrush GetResourceBrush(string resourceKey)
        {
            return _mainWindow.FindResource(resourceKey) as SolidColorBrush;
        }

        public void SetLibrarySelectedItem(object library)
        {
            if (_mainWindow.LibrariesListBox != null)
            {
                _mainWindow.LibrariesListBox.SelectedItem = library;
            }
        }
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

        Controls.TitleActionBar IConfigUIHelper.TitleActionBar => this.TitleActionBar;

        string IConfigUIHelper.CurrentPath
        {
            get => _currentPath;
            set => _currentPath = value;
        }

        object IConfigUIHelper.CurrentLibrary => _currentLibrary;

        bool IConfigUIHelper.IsPseudoMaximized
        {
            get => _isPseudoMaximized;
            set => _isPseudoMaximized = value;
        }

        Rect IConfigUIHelper.RestoreBounds
        {
            get => _restoreBounds;
            set => _restoreBounds = value;
        }

        Rect IConfigUIHelper.GetCurrentMonitorWorkAreaDIPs()
        {
            return GetCurrentMonitorWorkAreaDIPs();
        }

        void IConfigUIHelper.AdjustColumnWidths()
        {
            AdjustColumnWidths();
        }

        void IConfigUIHelper.EnsureColumnMinWidths()
        {
            EnsureColumnMinWidths();
        }

        System.Windows.Threading.Dispatcher IConfigUIHelper.Dispatcher => this.Dispatcher;

        void IConfigUIHelper.ExtendFrameIntoClientArea(int left, int right, int top, int bottom)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var margins = new NativeMethods.MARGINS { cxLeftWidth = left, cxRightWidth = right, cyTopHeight = top, cyBottomHeight = bottom };
            NativeMethods.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }

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

    /// <summary>
    /// TagUIHandler 上下文实现类
    /// </summary>
    internal class TagUIHandlerContextImpl : Services.Tag.ITagUIHandlerContext
    {
        private readonly MainWindow _mainWindow;

        public TagUIHandlerContextImpl(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        public Controls.FileBrowserControl FileBrowser => _mainWindow.FileBrowser;
        public System.Windows.Threading.Dispatcher Dispatcher => _mainWindow.Dispatcher;
        public System.Windows.Window OwnerWindow => _mainWindow;
        public Func<Tag> GetCurrentTagFilter => () => _mainWindow._currentTagFilter;
        public Action<Tag> SetCurrentTagFilter => tag => _mainWindow._currentTagFilter = tag;
        public Func<List<FileSystemItem>> GetCurrentFiles => () => _mainWindow._currentFiles;
        public Action<List<FileSystemItem>> SetCurrentFiles => files => _mainWindow._currentFiles = files;
        public Func<Library> GetCurrentLibrary => () => _mainWindow._currentLibrary;
        public Func<string> GetCurrentPath => () => _mainWindow._currentPath;
        public Func<bool> GetIsUpdatingTagSelection => () => _mainWindow._isUpdatingTagSelection;
        public Func<List<int>, List<string>> OrderTagNames => tagIds => _mainWindow.OrderTagNames(tagIds);
        public Action<Tag, List<FileSystemItem>> UpdateTagFilesUI => (tag, files) => _mainWindow.UpdateTagFilesUI(tag, files);
        public Action LoadFiles => () => _mainWindow.LoadFiles();
        public Action<Library> LoadLibraryFiles => library => _mainWindow.LoadLibraryFiles(library);
        public Action LoadCurrentDirectory => () => _mainWindow.LoadCurrentDirectory();
        public Action LoadTags => () => _mainWindow.LoadTags();
        public Func<System.Windows.Controls.Grid> GetNavTagContent => () => _mainWindow.NavTagContent;
        public Func<Services.FileList.FileListService> GetFileListService => () => _mainWindow._fileListService;
    }
}


