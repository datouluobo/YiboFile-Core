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

        // FileBrowser 事件桥接方法 - 已迁移到 FileBrowserEventHandler
        private void FileBrowser_PathChanged(object sender, string path) => _fileBrowserEventHandler?.FileBrowser_PathChanged(sender, path);
        private void FileBrowser_BreadcrumbMiddleClicked(object sender, string path) => _fileBrowserEventHandler?.FileBrowser_BreadcrumbMiddleClicked(sender, path);
        private void FileBrowser_BreadcrumbClicked(object sender, string path) => _fileBrowserEventHandler?.FileBrowser_BreadcrumbClicked(sender, path);
        private void FileBrowser_FilesSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 如果事件处理器未初始化，直接调用选择变化处理方法
            if (_fileBrowserEventHandler != null)
            {
                _fileBrowserEventHandler.FileBrowser_FilesSelectionChanged(sender, e);
            }
            else
            {
                // 直接处理选择变化，确保预览、备注、文件信息功能可用
                FilesListView_SelectionChanged(sender, e);
            }
        }
        private void FileBrowser_FilesMouseDoubleClick(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesMouseDoubleClick(sender, e);
        private void FileBrowser_FilesPreviewMouseDoubleClick(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewMouseDoubleClick(sender, e);
        private void FileBrowser_FilesPreviewKeyDown(object sender, KeyEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewKeyDown(sender, e);
        private void FileBrowser_FilesPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewMouseLeftButtonDown(sender, e);
        private void FileBrowser_FilesMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesMouseLeftButtonUp(sender, e);
        private void FileBrowser_FilesPreviewMouseDown(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewMouseDown(sender, e);
        private void FileBrowser_GridViewColumnHeaderClick(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_GridViewColumnHeaderClick(sender, e);
        private void FileBrowser_FilesSizeChanged(object sender, SizeChangedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesSizeChanged(sender, e);
        private void FileBrowser_FilesPreviewMouseDoubleClickForBlank(object sender, MouseButtonEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilesPreviewMouseDoubleClickForBlank(sender, e);
        private void FileBrowser_SearchClicked(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_SearchClicked(sender, e);
        private void FileBrowser_FilterClicked(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_FilterClicked(sender, e);
        private void FileBrowser_LoadMoreClicked(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_LoadMoreClicked(sender, e);
        private void FileBrowser_Loaded(object sender, RoutedEventArgs e) => _fileBrowserEventHandler?.FileBrowser_Loaded(sender, e);

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

        internal void FilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var rawSelectedItem = FileBrowser?.FilesSelectedItem;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] FilesListView_SelectionChanged: Raw SelectedItem: {rawSelectedItem}, Type: {rawSelectedItem?.GetType().Name}");

            if (rawSelectedItem is FileSystemItem selectedItem)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] FilesListView_SelectionChanged: Selected {selectedItem.Name}");
                _fileInfoService?.ShowFileInfo(selectedItem);
                LoadFilePreview(selectedItem);
                _fileNotesUIHandler?.LoadFileNotes(selectedItem);

                // 标签页：对图片执行AI预测并渲染到预测面板
                try
                {
                    if (NavTagContent != null && NavTagContent.Visibility == Visibility.Visible)
                    {
                        var ext = System.IO.Path.GetExtension(selectedItem.Path)?.ToLowerInvariant();
                        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
                        if (!selectedItem.IsDirectory && !string.IsNullOrEmpty(ext) && imageExtensions.Contains(ext))
                        {
                            // 预测占位
                            if (TagEditPanel != null)
                            {
                                TagEditPanel.PredictionPanel.Children.Clear();
                                TagEditPanel.NoPredictionText.Visibility = Visibility.Visible;
                                TagEditPanel.NoPredictionText.Text = "预测中...";
                            }

                            Task.Run(() =>
                            {
                                var preds = OoiMRRIntegration.PredictTagsForImage(selectedItem.Path) ?? new List<TagTrain.Services.TagPredictionResult>();
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    RenderPredictionResults(preds);
                                }), System.Windows.Threading.DispatcherPriority.Background);
                            });
                        }
                        else
                        {
                            RenderPredictionResults(new List<TagTrain.Services.TagPredictionResult>());
                        }
                    }
                }
                catch { }

                // 如果选中的是文件夹且大小未计算，立即计算
                if (selectedItem.IsDirectory)
                {
                    // 检查大小是否已计算（Size为空、"-"、"计算中..."或null表示未计算）
                    if (string.IsNullOrEmpty(selectedItem.Size) ||
                        selectedItem.Size == "-" ||
                        selectedItem.Size == "计算中...")
                    {
                        // 立即计算该文件夹的大小
                        CalculateFolderSizeImmediately(selectedItem.Path);
                    }
                }
            }
            else
            {
                // 没有选择文件时，清除预览区和文件信息
                ClearPreviewAndInfo();
            }
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






        // 键盘事件桥接方法 - 已迁移到 KeyboardEventHandler
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e) => _keyboardEventHandler?.MainWindow_PreviewKeyDown(sender, e);
        private void MainWindow_KeyDown(object sender, KeyEventArgs e) => _keyboardEventHandler?.MainWindow_KeyDown(sender, e);





        #endregion





        #region 库功能



        // 菜单事件桥接方法 - 已迁移到 MenuEventHandler
        private void AddLibrary_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.AddLibrary_Click(sender, e);
        private void ManageLibraries_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ManageLibraries_Click(sender, e);



        // 库管理事件桥接方法 - 已迁移到 MenuEventHandler
        private void LibraryRename_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryRename_Click(sender, e);
        private void LibraryDelete_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryDelete_Click(sender, e);
        private void LibraryManage_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryManage_Click(sender, e);
        private void LibraryOpenInExplorer_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryOpenInExplorer_Click(sender, e);
        private void LibraryRefresh_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryRefresh_Click(sender, e);


        /// <summary>
        /// 显示合并的库文件（所有路径的文件合并显示）
        /// </summary>
        private void ShowMergedLibraryFiles(List<FileSystemItem> items, Library library)
        {
            if (library == null) return;

            _currentFiles.Clear();
            _currentFiles.AddRange(items ?? new List<FileSystemItem>());

            System.Diagnostics.Debug.WriteLine($"[加载库文件] 合并后共有 {_currentFiles.Count} 项");

            // 应用排序
            if (_columnService != null)
            {
                _currentFiles = _columnService.SortFiles(_currentFiles);
            }

            System.Diagnostics.Debug.WriteLine($"[加载库文件] 设置 ItemsSource，文件数量: {_currentFiles.Count}");

            // 确保UI控件存在
            if (FileBrowser != null)
            {
                FileBrowser.FilesItemsSource = null; // 先清空
                FileBrowser.FilesItemsSource = _currentFiles; // 再设置
                FileBrowser.FilesList?.Items.Refresh(); // 强制刷新
            }

            // 高亮当前库（作为匹配当前库）
            HighlightMatchingLibrary(library);

            // 如果文件列表为空，显示空状态提示；否则隐藏
            if (_currentFiles.Count == 0)
            {
                ShowEmptyStateMessage($"库 \"{library.Name}\" 中没有文件或文件夹");
            }
            else
            {
                HideEmptyStateMessage();
            }

            // 更新地址栏显示库名称
            if (FileBrowser != null)
            {
                FileBrowser.AddressText = library.Name;
                // 允许在库标签页中进行搜索，不设置为只读
                FileBrowser.SetLibraryBreadcrumb(library.Name);
            }

            System.Diagnostics.Debug.WriteLine($"[加载库文件] 完成，ItemsSource 已设置");

            // 取消之前的文件夹大小计算任务
            _folderSizeCalculationService?.Cancel();

            // 异步加载标签和备注（延迟加载，避免阻塞UI）
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // 批量加载标签和备注（限制并发，减少到2个避免CPU占用过高）
                    var semaphore = new System.Threading.SemaphoreSlim(2, 2); // 最多2个并发查询
                    var tasks = _currentFiles.Select(async item =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            // 从 TagTrain 获取文件的标签
                            if (App.IsTagTrainAvailable)
                            {
                                var fileTagIds = OoiMRRIntegration.GetFileTagIds(item.Path);
                                if (fileTagIds != null && fileTagIds.Count > 0)
                                {
                                    var fileTagNames = OrderTagNames(fileTagIds);
                                    item.Tags = string.Join(", ", fileTagNames);
                                }
                                else
                                {
                                    item.Tags = "";
                                }
                            }
                            else
                            {
                                item.Tags = "";
                            }

                            var notes = FileNotesService.GetFileNotes(item.Path);
                            if (notes != null && notes.Length > 0)
                            {
                                var firstLine = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                                item.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                            }
                            else
                            {
                                item.Notes = "";
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).ToList();

                    try
                    {
                        System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
                    }
                    catch { }

                    // 批量更新UI（减少刷新次数）
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                        collectionView?.Refresh();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                catch { }
            });

            // 异步计算文件夹大小（使用服务方法，限制数量和延迟，避免资源消耗过大）
            var dirsToCalculate = _currentFiles.Where(f => f.IsDirectory).ToList();
            // 只计算前5个文件夹，大幅减少资源消耗
            int maxCalculations = Math.Min(5, dirsToCalculate.Count);

            // 立即计算前5个文件夹
            for (int i = 0; i < maxCalculations; i++)
            {
                var dir = dirsToCalculate[i];
                var path = dir.Path;
                var currentDelay = i * 1000; // 每个任务延迟1秒，避免同时启动

                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // 延迟启动，避免同时启动太多任务
                        if (currentDelay > 0)
                        {
                            await System.Threading.Tasks.Task.Delay(currentDelay);
                        }

                        // 使用服务方法计算文件夹大小
                        await _folderSizeCalculationService.CalculateAndUpdateFolderSizeAsync(path);

                        // 更新UI
                        _ = Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var item = _currentFiles.FirstOrDefault(f => f.Path == path);
                            if (item != null)
                            {
                                var cachedSize = DatabaseManager.GetFolderSize(path);
                                if (cachedSize.HasValue)
                                {
                                    item.Size = _fileListService.FormatFileSize(cachedSize.Value);
                                    // 使用低优先级批量更新，减少UI刷新频率
                                    var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                                    collectionView?.Refresh();
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.SystemIdle);
                    }
                    catch { }
                });
            }

            // 剩余文件夹通过服务的批量计算方法异步处理
            if (dirsToCalculate.Count > maxCalculations)
            {
                var remainingPaths = dirsToCalculate.Skip(maxCalculations).Select(d => d.Path).ToArray();
                _folderSizeCalculationService?.CalculateSubfolderSizesBatchAsync(remainingPaths);
            }
        }

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











        #region 收藏功能

        internal void LoadFavorites()
        {
            if (FavoritesListBox == null) return;
            _favoriteService.LoadFavorites(FavoritesListBox);
        }

        private void FavoritesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FavoritesListBox.SelectedItem == null) return;

            // 清除其他导航区域的选择
            ClearOtherNavigationSelections("Favorites");

            if (_draggedFavorite != null) return; // 如果正在拖拽，不处理单击
            if (_suppressFavoriteSelectionNavigation) return; // 右键上下文菜单打开时不导航

            // 使用反射获取Favorite对象
            var selectedItem = FavoritesListBox.SelectedItem;
            var favoriteProperty = selectedItem.GetType().GetProperty("Favorite");
            if (favoriteProperty == null) return;

            var favorite = favoriteProperty.GetValue(selectedItem) as Favorite;
            if (favorite == null) return;

            _navigationService.LastLeftNavSource = "Favorites";
            _navigationCoordinator.HandleFavoriteNavigation(favorite, NavigationCoordinator.ClickType.LeftClick);

            // 清除选择，避免残留选中状态
            FavoritesListBox.SelectedItem = null;
        }

        private bool _suppressFavoriteSelectionNavigation = false;

        private void FavoritesListBox_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = true;
            var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (item != null)
            {
                item.IsSelected = true;
            }
        }

        private void FavoritesListBox_ContextMenuClosed(object sender, RoutedEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = false;
            // 关闭菜单后清除选择，避免残留状态
            if (FavoritesListBox != null)
                FavoritesListBox.SelectedItem = null;
        }

        private void FavoritesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 已改为单击进入，此方法保留但不再使用
        }

        private ContextMenu CreateFavoritesContextMenu()
        {
            var menu = new ContextMenu();
            menu.Closed += FavoritesContextMenu_Closed;

            var removeItem = new MenuItem { Header = "删除收藏" };
            removeItem.Click += (s, e) =>
            {
                if (FavoritesListBox.SelectedItem != null)
                {
                    var selectedItem = FavoritesListBox.SelectedItem;
                    var favoriteProperty = selectedItem.GetType().GetProperty("Favorite");
                    if (favoriteProperty != null)
                    {
                        var favorite = favoriteProperty.GetValue(selectedItem) as Favorite;
                        if (favorite != null)
                        {
                            DatabaseManager.RemoveFavorite(favorite.Path);
                            LoadFavorites();
                        }
                    }
                }
            };
            menu.Items.Add(removeItem);

            return menu;
        }

        private void FavoritesContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            _suppressFavoriteSelectionNavigation = false;
            if (FavoritesListBox != null)
                FavoritesListBox.SelectedItem = null;
        }

        private void InitializeFavoritesDragDrop()
        {
            if (FavoritesListBox == null) return;

            // 启用拖拽排序
            FavoritesListBox.PreviewMouseLeftButtonDown += FavoritesListBox_PreviewMouseLeftButtonDown;
            FavoritesListBox.Drop += FavoritesListBox_Drop;
            FavoritesListBox.DragOver += FavoritesListBox_DragOver;
            FavoritesListBox.DragLeave += FavoritesListBox_DragLeave;
            FavoritesListBox.AllowDrop = true;
            FavoritesListBox.PreviewMouseMove += FavoritesListBox_PreviewMouseMove;
        }

        private void FavoritesListBox_DragLeave(object sender, DragEventArgs e)
        {
            // 清除所有高亮
            var listBox = sender as ListBox;
            if (listBox != null)
            {
                foreach (ListBoxItem item in FindVisualChildren<ListBoxItem>(listBox))
                {
                    item.Background = System.Windows.Media.Brushes.Transparent;
                }
            }
        }

        private void FavoritesListBox_DragOver(object sender, DragEventArgs e)
        {
            // 检查数据格式
            if (!e.Data.GetDataPresent("Favorite"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            var draggedItem = e.Data.GetData("Favorite") as Favorite;
            if (draggedItem == null)
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = DragDropEffects.Move;
            e.Handled = true;

            // 提供拖拽视觉效果
            var listBox = sender as ListBox;
            if (listBox != null)
            {
                var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
                if (targetItem != null)
                {
                    // 高亮目标项
                    foreach (ListBoxItem item in FindVisualChildren<ListBoxItem>(listBox))
                    {
                        if (item == targetItem)
                        {
                            item.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(100, 33, 150, 243));
                        }
                        else if (item.Background is SolidColorBrush brush && brush.Color.A == 100)
                        {
                            item.Background = System.Windows.Media.Brushes.Transparent;
                        }
                    }
                }
            }
        }

        private Favorite _draggedFavorite = null;
        private System.Windows.Point _dragStartPoint;

        private void FavoritesListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (listBoxItem != null)
            {
                var item = listBoxItem.DataContext;
                var favoriteProperty = item.GetType().GetProperty("Favorite");
                if (favoriteProperty != null)
                {
                    _draggedFavorite = favoriteProperty.GetValue(item) as Favorite;
                }
            }
        }

        private void FavoritesListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedFavorite != null)
            {
                var currentPoint = e.GetPosition(null);
                var diff = _dragStartPoint - currentPoint;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    var listBox = sender as ListBox;
                    if (listBox != null)
                    {
                        // 创建数据对象并传递Favorite对象
                        var dataObject = new DataObject("Favorite", _draggedFavorite);
                        DragDrop.DoDragDrop(listBox, dataObject, DragDropEffects.Move);
                    }
                }
            }
        }
        private void FavoritesListBox_Drop(object sender, DragEventArgs e)
        {
            // 检查数据格式
            if (!e.Data.GetDataPresent("Favorite"))
            {
                return;
            }

            var draggedFavorite = e.Data.GetData("Favorite") as Favorite;
            if (draggedFavorite == null) return;

            var listBox = sender as ListBox;
            if (listBox == null)
            {
                return;
            }

            // 清除所有高亮
            foreach (ListBoxItem item in FindVisualChildren<ListBoxItem>(listBox))
            {
                item.Background = System.Windows.Media.Brushes.Transparent;
            }

            var targetItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
            if (targetItem == null || targetItem.DataContext == null)
            {
                return;
            }

            var targetData = targetItem.DataContext;
            var favoriteProperty = targetData.GetType().GetProperty("Favorite");
            if (favoriteProperty == null)
            {
                return;
            }

            var targetFavorite = favoriteProperty.GetValue(targetData) as Favorite;
            if (targetFavorite == null || targetFavorite.Id == draggedFavorite.Id)
            {
                return;
            }

            // 更新排序顺序并重新加载
            var favorites = DatabaseManager.GetAllFavorites().ToList();
            var draggedIndex = favorites.FindIndex(f => f.Id == draggedFavorite.Id);
            var targetIndex = favorites.FindIndex(f => f.Id == targetFavorite.Id);

            if (draggedIndex >= 0 && targetIndex >= 0 && draggedIndex != targetIndex)
            {
                // 重新排序：移除拖拽项，插入到目标位置
                var newOrder = new List<Favorite>();
                for (int i = 0; i < favorites.Count; i++)
                {
                    if (i == draggedIndex) continue; // 跳过被拖拽的项
                    if (i == targetIndex)
                    {
                        // 在目标位置插入
                        if (draggedIndex < targetIndex)
                        {
                            // 向下拖拽：先插入目标项，再插入被拖拽项
                            newOrder.Add(favorites[targetIndex]);
                            newOrder.Add(draggedFavorite);
                        }
                        else
                        {
                            // 向上拖拽：先插入被拖拽项，再插入目标项
                            newOrder.Add(draggedFavorite);
                            newOrder.Add(favorites[targetIndex]);
                        }
                    }
                    else
                    {
                        newOrder.Add(favorites[i]);
                    }
                }

                // 更新数据库中的SortOrder（在文件夹和文件分组内排序）
                // 先按文件夹/文件分组，再更新SortOrder
                var folderGroup = newOrder.Where(f => f.IsDirectory).ToList();
                var fileGroup = newOrder.Where(f => !f.IsDirectory).ToList();

                int sortOrder = 0;
                foreach (var fav in folderGroup)
                {
                    DatabaseManager.UpdateFavoriteSortOrder(fav.Id, sortOrder++);
                }
                foreach (var fav in fileGroup)
                {
                    DatabaseManager.UpdateFavoriteSortOrder(fav.Id, sortOrder++);
                }

                // 重新加载显示
                LoadFavorites();
            }

            _draggedFavorite = null;
            e.Handled = true;
        }

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

        private void AddFavorite_Click(object sender, RoutedEventArgs e)
        {
            // 获取选中的文件或文件夹
            var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要收藏的文件或文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int successCount = 0;
            int skipCount = 0;

            foreach (var item in selectedItems)
            {
                try
                {
                    // 检查是否已收藏
                    if (DatabaseManager.IsFavorite(item.Path))
                    {
                        skipCount++;
                        continue;
                    }

                    string displayName = item.Name;
                    DatabaseManager.AddFavorite(item.Path, item.IsDirectory, displayName);
                    successCount++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"收藏失败: {item.Name} - {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // 刷新收藏列表
            LoadFavorites();

            // 不再显示提示框，静默完成
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

        #endregion

        #region 键盘快捷键和文件操作

        internal void FilesListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
                return;

            // Ctrl+A - 全选
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                listView.SelectAll();
                e.Handled = true;
                return;
            }

            // Ctrl+C - 复制
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Copy_Click(null, null);
                e.Handled = true;
                return;
            }

            // Ctrl+V - 粘贴
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Paste_Click(null, null);
                e.Handled = true;
                return;
            }

            // Ctrl+X - 剪切
            if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
            {
                Cut_Click(null, null);
                e.Handled = true;
                return;
            }

            // Delete - 删除
            if (e.Key == Key.Delete)
            {
                Delete_Click(null, null);
                e.Handled = true;
                return;
            }

            // F2 - 重命名
            if (e.Key == Key.F2)
            {
                Rename_Click(null, null);
                e.Handled = true;
                return;
            }

            // F5 - 刷新
            if (e.Key == Key.F5)
            {
                Refresh_Click(null, null);
                e.Handled = true;
                return;
            }

            // Alt+Enter - 属性
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                ShowProperties_Click(null, null);
                e.Handled = true;
                return;
            }

            // Backspace - 返回上一级
            if (e.Key == Key.Back)
            {
                NavigateBack_Click(null, null);
                e.Handled = true;
                return;
            }

            // 处理方向键，防止焦点跑到分割器
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
            {
                if (listView.Items.Count == 0)
                    return;

                int currentIndex = listView.SelectedIndex;
                var wrapPanel = FindDescendant<WrapPanel>(listView);
                bool isTilesView = listView.View == null && wrapPanel != null;
                int columns = 1;
                if (isTilesView)
                {
                    double itemWidth = wrapPanel.ItemWidth > 0 ? wrapPanel.ItemWidth : (wrapPanel.Children.Count > 0 ? ((FrameworkElement)wrapPanel.Children[0]).ActualWidth : 160);
                    double viewportWidth = wrapPanel.ActualWidth > 0 ? wrapPanel.ActualWidth : listView.ActualWidth;
                    columns = Math.Max(1, (int)Math.Floor(viewportWidth / Math.Max(1, itemWidth)));
                }

                if (e.Key == Key.Down)
                {
                    if (isTilesView)
                    {
                        int next = currentIndex + columns;
                        if (next < listView.Items.Count)
                        {
                            listView.SelectedIndex = next;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                        else
                        {
                            listView.SelectedIndex = listView.Items.Count - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (currentIndex < listView.Items.Count - 1)
                        {
                            listView.SelectedIndex = currentIndex + 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    if (isTilesView)
                    {
                        int prev = currentIndex - columns;
                        if (prev >= 0)
                        {
                            listView.SelectedIndex = prev;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                        else
                        {
                            listView.SelectedIndex = 0;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (currentIndex > 0)
                        {
                            listView.SelectedIndex = currentIndex - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Home)
                {
                    listView.SelectedIndex = 0;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.End)
                {
                    listView.SelectedIndex = listView.Items.Count - 1;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    if (isTilesView)
                    {
                        if (currentIndex > 0)
                        {
                            listView.SelectedIndex = currentIndex - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        // 返回上一级
                        NavigateBack_Click(null, null);
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    if (isTilesView)
                    {
                        if (currentIndex < listView.Items.Count - 1)
                        {
                            listView.SelectedIndex = currentIndex + 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        // 如果是文件夹，进入
                        if (listView.SelectedItem is FileSystemItem selectedItem && selectedItem.IsDirectory)
                        {
                            // 如果是库模式，切换到路径模式并导航
                            if (_currentLibrary != null)
                            {
                                // 切换到路径模式
                                _currentLibrary = null;
                                SwitchNavigationMode("Path");
                            }

                            // 使用统一导航协调器处理Enter键导航（左键点击）
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.LeftClick);
                        }
                    }
                    e.Handled = true;
                }
            }
            // 处理 Enter 键打开文件/文件夹
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (FileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
                {
                    if (selectedItem.IsDirectory)
                    {
                        // 如果是库模式，切换到路径模式并导航
                        if (_currentLibrary != null)
                        {
                            // 切换到路径模式
                            _currentLibrary = null;
                            SwitchNavigationMode("Path");
                        }

                        // 使用统一导航协调器处理Enter键导航（左键点击）
                        _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.LeftClick);
                    }
                    else
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = selectedItem.Path,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                e.Handled = true;
            }
        }

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

            System.Diagnostics.Debug.WriteLine($"[SetItemHighlight] listType={listType}, item={item?.GetType().Name}, highlight={highlight}");

            try
            {
                var container = listBox.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
                System.Diagnostics.Debug.WriteLine($"[SetItemHighlight] container={container}, status={listBox.ItemContainerGenerator.Status}");
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


