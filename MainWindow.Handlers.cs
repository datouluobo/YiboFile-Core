using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using OoiMRR.Handlers;
using HandlerMouseEventHandler = OoiMRR.Handlers.MouseEventHandler;
using OoiMRR.Services;
using OoiMRR.Services.FileNotes;
using OoiMRR.Services.FileOperations;
using OoiMRR.Services.Navigation;
using OoiMRR.Services.Search;
using OoiMRR.Services.Tabs;
using Microsoft.Extensions.DependencyInjection;
using OoiMRR.Services.Settings;
// using OoiMRR.Services.TagTrain; // Phase 2
// using TagTrain.UI; // Phase 2
using System.Windows.Media;

namespace OoiMRR
{
    public partial class MainWindow
    {
        internal void CloseOverlays()
        {
            if (SettingsOverlay != null && SettingsOverlay.Visibility == Visibility.Visible)
            {
                SettingsOverlay.Visibility = Visibility.Collapsed;
            }
            if (AboutOverlay != null && AboutOverlay.Visibility == Visibility.Visible)
            {
                AboutOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void InitializeHandlers()
        {
            // 订阅 TabManager 的关闭覆盖层请求
            if (TabManager != null)
            {
                TabManager.CloseOverlayRequested += (s, e) => CloseOverlays();
            }

            // 初始化 FileBrowserEventHandler
            _fileBrowserEventHandler = new FileBrowserEventHandler(
                FileBrowser,
                _navigationCoordinator,
                _tabService,
                _searchService, // searchService
                _searchCacheService,
                NavigateToPath,
                (query) => PerformSearch(query, _searchOptions?.SearchNames ?? true, _searchOptions?.SearchNotes ?? true),
                SwitchNavigationMode,
                LoadCurrentDirectory,
                () => // ClearFilter
                {
                    _currentTagFilter = null;
                    FileBrowser.IsAddressReadOnly = false;
                    FileBrowser.SetTagBreadcrumb(null);
                    LoadCurrentDirectory();
                    // Also hide empty state
                    HideEmptyStateMessage();
                },
                HideEmptyStateMessage,
                (header) => GridViewColumnHeader_Click(header, null), // Action<GridViewColumnHeader> -> Wrapped
                FileBrowser_FilesSizeChanged, // Action<SizeChangedEventArgs>
                FileBrowser_GridSplitterDragDelta, // Action<DragDeltaEventArgs>
                () => _currentPath,
                () => _configService?.Config,
                () => _currentTagFilter,
                (tag) => _currentTagFilter = tag,
                () => _currentFiles,
                (files) => _currentFiles = files,
                () => _searchOptions,
                FilesListView_SelectionChanged,
                (e) => FilesListView_MouseDoubleClick(FileBrowser?.FilesList, e), // Wrapped
                (e) => { }, // PreviewMouseDoubleClick - not used
                (e) => { }, // PreviewKeyDown - handled by KeyboardEventHandler
                (e) => FilesListView_PreviewMouseLeftButtonDown(FileBrowser?.FilesList, e), // Wrapped
                (e) => FilesListView_MouseLeftButtonUp(FileBrowser?.FilesList, e), // Wrapped
                (e) => FilesListView_PreviewMouseDown(FileBrowser?.FilesList, e), // Wrapped
                (e) => FilesListView_PreviewMouseDoubleClickForBlank(FileBrowser?.FilesList, e), // Wrapped
                (e) => { }, // FilesListView_PreviewMouseMove handled by DragDropManager directly
                () => ColLeft, // Func<ColumnDefinition>
                (e) => // CommitRename
                {                    var (browser, path, library) = GetActiveContext();
                    Services.FileOperations.IFileOperationContext context = null;
                    if (library != null)
                        context = new Services.FileOperations.LibraryOperationContext(library, browser, this, RefreshActiveFileList);
                    else
                        context = new Services.FileOperations.PathOperationContext(path, browser, this, RefreshActiveFileList);

                    var op = new Services.FileOperations.RenameOperation(context, this, _fileOperationService);
                    op.Execute(e.Item, e.NewName);
                }
            );
            _fileBrowserEventHandler.Initialize();

            _selectionEventHandler = new SelectionEventHandler(
                this,
                _previewService, // Was _filePreviewService, but InitializeServices uses _previewService
                _fileNotesUIHandler,
                item => _fileInfoService?.ShowFileInfo(item), // Was UpdateFileInfoPanel(item)
                () => ClearPreviewAndInfo(),
                _fileListService,
                () => _currentFiles,
                () => _currentPath
            );

            // 初始化 ColumnInteractionHandler
            _columnInteractionHandler = new Handlers.ColumnInteractionHandler(this, _columnService, _configService);
            _columnInteractionHandler.EnsureHeaderContextMenuHook();
            _columnInteractionHandler.HookHeaderThumbs(); // 挂载列头拖拽事件

            // 初始化 WindowLifecycleHandler
            _windowLifecycleHandler = new Handlers.WindowLifecycleHandler(this, _windowStateManager, _configService, _columnService);

            // 初始化 FileOperationHandler
            _fileOperationHandler = new Handlers.FileOperationHandler(this);

            // 初始化统一文件操作服务 (新架构) - 逐步迁移中
            var errorService = App.ServiceProvider.GetRequiredService<OoiMRR.Services.Core.Error.ErrorService>();
            var undoService = App.ServiceProvider.GetService(typeof(OoiMRR.Services.FileOperations.Undo.UndoService)) as OoiMRR.Services.FileOperations.Undo.UndoService;
            var taskQueueService = App.ServiceProvider.GetService(typeof(OoiMRR.Services.FileOperations.TaskQueue.TaskQueueService)) as OoiMRR.Services.FileOperations.TaskQueue.TaskQueueService;

            _fileOperationService = new Services.FileOperations.FileOperationService(
                () =>
                {
                    var (browser, path, library) = GetActiveContext();
                    return new Services.FileOperations.FileOperationContext
                    {
                        TargetPath = path,
                        CurrentLibrary = library,
                        OwnerWindow = this,
                        RefreshCallback = RefreshActiveFileList
                    };
                },
                errorService,
                undoService,
                taskQueueService
            );

            // 监听撤销/重做事件以刷新UI
            if (undoService != null)
            {
                undoService.ActionUndone += (s, e) =>
                {
                    Application.Current?.Dispatcher?.Invoke(() => RefreshActiveFileList());
                };
                undoService.ActionRedone += (s, e) =>
                {
                    Application.Current?.Dispatcher?.Invoke(() => RefreshActiveFileList());
                };
            }

            // 初始化 MenuEventHandler
            _menuEventHandler = new MenuEventHandler(
                FileBrowser,
                _libraryService,
                RefreshActiveFileList,
                LoadCurrentDirectory,
                () => // ClearFilter
                {
                    _currentTagFilter = null;
                    FileBrowser.IsAddressReadOnly = false;
                    FileBrowser.SetTagBreadcrumb(null);
                    LoadCurrentDirectory();
                    HideEmptyStateMessage();
                },
                () => Close(),
                () => // settings
                {
                    if (SettingsOverlay == null) return;
                    if (SettingsOverlay.Visibility == Visibility.Visible)
                    {
                        SettingsOverlay.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        CloseOverlays(); // Close About if open
                        _settingsOverlayController?.Show();
                    }
                },
                () => // about
                {
                    if (AboutOverlay == null) return;
                    if (AboutOverlay.Visibility == Visibility.Visible)
                    {
                        AboutOverlay.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        CloseOverlays(); // Close Settings if open
                        AboutOverlay.Visibility = Visibility.Visible;
                    }
                },
                EditNotes_Click_Logic, // Action editNotes
                () => { }, // BatchAddTags_Click_Logic - Phase 2
                () => { }, // showTagStatistics - Phase 2
                ImportLibrary_Click_Logic, // importLibrary
                ExportLibrary_Click_Logic, // exportLibrary
                () => { }, // addFileToLibrary - Implement logic if needed
                async () => await CopySelectedFilesAsync(), // Copy - 使用统一服务
                async () => await CutSelectedFilesAsync(), // Cut - 使用统一服务
                async () => await PasteFilesAsync(), // Paste - 使用统一服务
                async () => await DeleteSelectedFilesAsync(), // Delete - 使用统一服务
                () => // Rename
                {
                    var (browser, path, library) = GetActiveContext();
                    var item = browser?.FilesSelectedItem as FileSystemItem;
                    if (item != null)
                    {
                        // Trigger inline rename
                        item.IsRenaming = true;
                    }
                },
                () => { }, // ShowProperties - Implement if needed
                NavigateToPath,
                SwitchNavigationMode,
                () => GetActiveContext().path,
                () => GetActiveContext().library,
                () => GetActiveContext().browser?.FilesItemsSource as System.Collections.Generic.List<FileSystemItem>,
                (files) =>
                {
                    var b = GetActiveContext().browser;
                    if (b != null) b.FilesItemsSource = files;
                    if (b == FileBrowser) _currentFiles = files;
                },
                () => this,
                (lib) => _tabService.OpenLibraryTab(lib),
                (lib) => { }, // HighlightMatchingLibrary
                () => _libraryService.LoadLibraries(), // LoadLibraries
                () => LibrariesListBox, // Func<ListBox>
                () => LibraryContextMenu, // Func<ContextMenu>
                (ext) => // CreateNewFileWithExtension
                {
                    var (browser, path, library) = GetActiveContext();
                    Services.FileOperations.IFileOperationContext context;
                    if (library != null)
                        context = new Services.FileOperations.LibraryOperationContext(library, browser, this, RefreshActiveFileList);
                    else
                        context = new Services.FileOperations.PathOperationContext(path, browser, this, RefreshActiveFileList);

                    var op = new Services.FileOperations.NewFileOperation(context, this, _fileOperationService);
                    op.Execute(ext);
                },
                async (path) => // CreateNewFolder
                {
                    if (_fileOperationService != null)
                    {
                        var parent = System.IO.Path.GetDirectoryName(path);
                        var name = System.IO.Path.GetFileName(path);
                        var result = await _fileOperationService.CreateFolderAsync(parent, name);
                        return result != null;
                    }
                    return false;
                },
                 () => _configService?.Config,
                 (cfg) => _configService?.ApplyConfig(cfg),
                 () => _configService?.SaveCurrentConfig()
            );

            // 定义获取当前活动标签页服务的逻辑
            Func<Services.Tabs.TabService> getActiveTabService = () =>
                (_isDualListMode && _isSecondPaneFocused && _secondTabService != null) ? _secondTabService : _tabService;

            // 初始化 KeyboardEventHandler
            _keyboardEventHandler = new OoiMRR.Handlers.KeyboardEventHandler(
                FileBrowser,
                getActiveTabService,
                (tab) => getActiveTabService().RemoveTab(tab),
                (path) => CreateTab(path),
                (tab) => getActiveTabService().SwitchToTab(tab),
                () => _menuEventHandler.NewFolder_Click(null, null), // NewFolderClick
                RefreshFileList,
                () => _menuEventHandler.Copy_Click(null, null),
                () => _menuEventHandler.Paste_Click(null, null),
                () => _menuEventHandler.Cut_Click(null, null),
                () => _menuEventHandler.Delete_Click(null, null),
                async () => await DeleteSelectedFilesAsync(permanent: true), // Shift+Delete 永久删除
                () => _menuEventHandler.Rename_Click(null, null),
                NavigateToPath,
                SwitchNavigationMode,
                () => _currentLibrary != null,
                Back_Click_Logic, // navigateBack
                () => Undo_Click(null, null),
                () => Redo_Click(null, null),
                SwitchLayoutModeByIndex,  // 添加布局切换回调
                () => IsDualListMode,     // isDualListMode 检查
                () => SwitchFocusedPane() // switchDualPaneFocus 回调
            );

            // 初始化 MouseEventHandler
            _mouseEventHandler = new HandlerMouseEventHandler(
                () => WindowMaximize_Click(null, null),
                () => DragMove(),
                () => FavoritesListBox,
                () => DrivesListBox,
                () => QuickAccessListBox,
                _navigationCoordinator,
                (fav) => _navigationCoordinator.HandleFavoriteNavigation(fav, NavigationCoordinator.ClickType.LeftClick),
                (drivePath) => _navigationCoordinator.HandlePathNavigation(drivePath, NavigationCoordinator.NavigationSource.Drive, NavigationCoordinator.ClickType.LeftClick),
                (path) => _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.QuickAccess, NavigationCoordinator.ClickType.LeftClick)
            );

            // TagTrainEventHandler 初始化已移除 - Phase 2将重新实现

            // Initialize Drag & Drop
            InitializeDragDrop();
            if (AboutPanel != null)
            {
                AboutPanel.CloseRequested += (s, e) =>
                {
                    if (AboutOverlay != null) AboutOverlay.Visibility = Visibility.Collapsed;
                };
            }
        }

        internal void FilesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 如果双击发生在列头或分隔线，拦截
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
            // 备用处理（如果 Preview 事件没有被处理）
            HandleDoubleClick(e);
        }

        private void HandleDoubleClick(MouseButtonEventArgs e)
        {
            // 获取双击位置对应的项目
            if (FileBrowser?.FilesList == null) return;
            var hitResult = VisualTreeHelper.HitTest(FileBrowser.FilesList, e.GetPosition(FileBrowser.FilesList));
            if (hitResult == null) return;

            // 向上查找 ListViewItem
            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != FileBrowser.FilesList)
            {
                if (current is System.Windows.Controls.ListViewItem item)
                {
                    if (item.Content is FileSystemItem selectedItem)
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

                            // 使用统一导航协调器处理文件列表双击导航
                            var clickType = NavigationCoordinator.GetClickType(e);
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, clickType);
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            // 打开文件
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = selectedItem.Path,
                                    UseShellExecute = true
                                });
                                e.Handled = true;
                                return;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }

            // 备用：使用选中项
            if (FileBrowser?.FilesSelectedItem is FileSystemItem backupItem)
            {
                if (backupItem.IsDirectory)
                {
                    if (Directory.Exists(backupItem.Path))
                    {
                        if (_currentLibrary != null)
                        {
                            _currentLibrary = null;
                            SwitchNavigationMode("Path");
                        }
                        NavigateToPath(backupItem.Path);
                        e.Handled = true;
                    }
                }
                else
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = backupItem.Path,
                            UseShellExecute = true
                        });
                        e.Handled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        internal void FilesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 记录鼠标按下位置，用于区分点击和拖动
            var listView = sender as ListView;
            if (listView == null)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 支持双击列头分隔线（Thumb）自动适配列宽
            if (e.ClickCount == 2)
            {
                var src = e.OriginalSource as DependencyObject;
                if (src != null)
                {
                    var header = FindAncestor<GridViewColumnHeader>(src);
                    var thumb = FindAncestor<System.Windows.Controls.Primitives.Thumb>(src);
                    if (header != null && thumb != null && header.Column != null)
                    {
                        AutoSizeGridViewColumn(header.Column);
                        e.Handled = true;
                        return;
                    }
                }
            }

            System.Windows.Point hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);

            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;
                int depth = 0;
                while (current != null && current != listView && depth < 10)
                {
                    string typeName = current.GetType().Name;

                    // 检查是否是列头相关元素
                    if (current is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }

                    // 检查是否是 Thumb（调整大小的拖拽句柄）
                    if (current.GetType().Name.Contains("Thumb") || current.GetType().Name == "Thumb")
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }

                    // 检查父元素是否是 GridViewColumnHeader
                    var parent = VisualTreeHelper.GetParent(current);
                    if (parent is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }

                    current = parent;
                    depth++;
                }

                // 额外检查：如果点击位置在列头行的 Y 坐标范围内，也不处理
                if (listView.View is GridView gridView && gridView.Columns.Count > 0)
                {
                    // 获取列头行的高度
                    if (hitPoint.Y < 30) // 列头通常高度约为 25-30 像素
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        // 不设置 e.Handled，让列头的排序和调整宽度功能正常工作
                        return;
                    }
                }
            }

            // 不是在列头区域，记录按下位置
            _mouseDownPoint = e.GetPosition(listView);
            _isMouseDownOnListView = true;
            _isMouseDownOnColumnHeader = false; // 清除列头标志

            // 检查是否点击在空白区域（不是 ListViewItem）
            bool isListViewItem = false;

            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;

                // 向上查找，检查是否点击在 ListViewItem 上
                while (current != null && current != listView)
                {
                    if (current is System.Windows.Controls.ListViewItem)
                    {
                        isListViewItem = true;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            // 如果点击在空白区域（hitResult 为 null 或不是 ListViewItem），清除选择
            if (!isListViewItem && e.ChangedButton == MouseButton.Left)
            {
                listView.SelectedItem = null;
                listView.SelectedItems.Clear();

                // 显式调用 HandleNoSelection，确保信息面板更新（即使 SelectionChanged 未按预期触发）
                _selectionEventHandler?.HandleNoSelection();
            }
        }

        internal void FilesListView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 只处理中键点击（打开新标签页），Ctrl+左键用于多选，不在这里处理
            if (e.ChangedButton != MouseButton.Middle) return;

            // 如果 sender 为 null，尝试从 FileBrowser 获取 ListView
            var listView = sender as ListView;
            if (listView == null)
            {
                listView = FileBrowser?.FilesList;
            }
            if (listView == null) return;

            // 获取点击位置对应的项目
            var hitResult = VisualTreeHelper.HitTest(listView, e.GetPosition(listView));
            if (hitResult == null) return;

            // 向上查找 ListViewItem
            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listView)
            {
                if (current is System.Windows.Controls.ListViewItem item)
                {
                    if (item.Content is FileSystemItem selectedItem)
                    {
                        if (selectedItem.IsDirectory)
                        {
                            // 中键点击：在新标签页打开文件夹
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.MiddleClick);
                            e.Handled = true;
                            return;
                        }
                    }
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        internal void FilesListView_PreviewMouseDoubleClickForBlank(object sender, MouseButtonEventArgs e)
        {
            // 双击空白区域：返回上一级
            // 只有在路径模式下才生效
            if (_currentLibrary == null && !string.IsNullOrEmpty(_currentPath))
            {
                try
                {
                    string parentPath = Path.GetDirectoryName(_currentPath);
                    if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
                    {
                        _navigationCoordinator.HandlePathNavigation(parentPath, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.LeftClick);
                        e.Handled = true;
                    }
                }
                catch { }
            }
        }

        private string ExtractPathFromListBoxItem(ListBox listBox, System.Windows.Point position)
        {
            var hitResult = VisualTreeHelper.HitTest(listBox, position);
            if (hitResult == null) return null;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext != null)
                {
                    var pathProperty = item.DataContext.GetType().GetProperty("Path");
                    if (pathProperty != null)
                    {
                        return pathProperty.GetValue(item.DataContext) as string;
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private Favorite ExtractFavoriteFromListBoxItem(ListBox listBox, System.Windows.Point position)
        {
            var hitResult = VisualTreeHelper.HitTest(listBox, position);
            if (hitResult == null) return null;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext != null)
                {
                    var favoriteProperty = item.DataContext.GetType().GetProperty("Favorite");
                    if (favoriteProperty != null)
                    {
                        return favoriteProperty.GetValue(item.DataContext) as Favorite;
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果是在全屏覆盖层打开的情况下点击标题栏空白处，关闭覆盖层
            if (SettingsOverlay != null && SettingsOverlay.Visibility == Visibility.Visible)
            {
                SettingsOverlay.Visibility = Visibility.Collapsed;
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

        private void DrivesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == NavigationCoordinator.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var path = ExtractPathFromListBoxItem(listBox, e.GetPosition(listBox));
            if (!string.IsNullOrEmpty(path))
            {
                _navigationService.LastLeftNavSource = "Drive";
                _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.Drive, clickType);
                e.Handled = true;
            }
        }

        private void QuickAccessListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == NavigationCoordinator.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var path = ExtractPathFromListBoxItem(listBox, e.GetPosition(listBox));
            if (!string.IsNullOrEmpty(path))
            {
                _navigationService.LastLeftNavSource = "QuickAccess";
                _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.QuickAccess, clickType);
                e.Handled = true;
            }
        }

        private void FavoritesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == NavigationCoordinator.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var favorite = ExtractFavoriteFromListBoxItem(listBox, e.GetPosition(listBox));
            if (favorite != null)
            {
                _navigationService.LastLeftNavSource = "Favorites";
                _navigationCoordinator.HandleFavoriteNavigation(favorite, clickType);
                e.Handled = true;
            }
        }

        internal void FilesListView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 如果按下时在列头区域，无论抬起位置在哪里，都不处理清除选中
            if (_isMouseDownOnColumnHeader)
            {
                _isMouseDownOnColumnHeader = false;
                _isMouseDownOnListView = false;
                return;
            }

            // 只有在按下和抬起都在 ListView 上，且没有明显移动时，才处理点击空白区域
            if (!_isMouseDownOnListView)
                return;

            var listView = sender as ListView;
            if (listView == null)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 首先检查事件的原始源，看是否是列头相关元素
            var originalSource = e.OriginalSource as DependencyObject;
            DependencyObject checkSource = originalSource;
            while (checkSource != null)
            {
                if (checkSource is GridViewColumnHeader)
                {
                    _isMouseDownOnListView = false;
                    return;
                }
                if (checkSource.GetType().Name.Contains("Thumb") || checkSource.GetType().Name == "Thumb")
                {
                    _isMouseDownOnListView = false;
                    return;
                }
                checkSource = VisualTreeHelper.GetParent(checkSource);
            }

            System.Windows.Point mouseUpPoint = e.GetPosition(listView);

            // 计算鼠标移动距离
            double distance = Math.Sqrt(Math.Pow(mouseUpPoint.X - _mouseDownPoint.X, 2) +
                                      Math.Pow(mouseUpPoint.Y - _mouseDownPoint.Y, 2));

            // 如果移动距离超过阈值，说明是拖动而不是点击，不处理
            if (distance > SystemParameters.MinimumHorizontalDragDistance)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 额外检查：如果抬起位置在列头区域，也不处理
            if (mouseUpPoint.Y < 30)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 检测点击位置是否是空白区域
            System.Windows.Point hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);

            if (hitResult != null)
            {
                // 向上查找，看是否点击在 ListViewItem 上
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listView)
                {
                    if (current is ListViewItem)
                    {
                        // 点击在 ListViewItem 上，不处理（让默认行为处理）
                        _isMouseDownOnListView = false;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                // 如果到达 ListView 都没有找到 ListViewItem，说明点击的是空白区域
                // 再次检查是否点击在列头相关区域
                current = hitResult.VisualHit;
                while (current != null)
                {
                    if (current is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                // 点击的是空白区域，清除文件列表选中（但保留库的选择状态）
                if (listView.SelectedItems.Count > 0)
                {
                    listView.SelectedItems.Clear();
                }

                // 在库模式下，确保库的选择状态保持正确显示
                if (_currentLibrary != null && LibrariesListBox != null)
                {
                    // 延迟执行，确保UI更新完成
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // 确保库列表中的选择状态正确
                        if (LibrariesListBox.SelectedItem != _currentLibrary)
                        {
                            LibrariesListBox.SelectedItem = _currentLibrary;
                        }
                        // 确保库的高亮显示正确
                        HighlightMatchingLibrary(_currentLibrary);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }

            _isMouseDownOnListView = false;
            _isMouseDownOnColumnHeader = false; // 清除列头标志
        }



        private DateTime _lastColumnClickTime = DateTime.MinValue;
        private string _lastClickedColumn = null;

        internal void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var header = sender as GridViewColumnHeader;
            if (header == null || FileBrowser == null) return;

            // 防抖：忽略200ms内的重复点击
            var now = DateTime.Now;
            var columnTag = header.Tag?.ToString();
            if ((now - _lastColumnClickTime).TotalMilliseconds < 200 && columnTag == _lastClickedColumn)
            {
                return;
            }
            _lastColumnClickTime = now;
            _lastClickedColumn = columnTag;

            _columnService?.HandleColumnHeaderClick(
                header,
                _currentFiles,
                (sortedFiles) =>
                {
                    _currentFiles = sortedFiles;
                    FileBrowser.FilesItemsSource = _currentFiles;
                },
                FileBrowser.FilesGrid
            );
        }

        // ==================== Existing but separate ====================

        private void FileBrowser_FilesSizeChanged(SizeChangedEventArgs e)
        {
            _columnService?.AdjustListViewColumnWidths(FileBrowser);
        }

        private void FileBrowser_GridSplitterDragDelta(DragDeltaEventArgs e)
        {
            if (ColLeft != null)
            {
                double newWidth = ColLeft.Width.Value + e.HorizontalChange;
                if (newWidth < 150) newWidth = 150; // Minimum width
                ColLeft.Width = new GridLength(newWidth);
            }
        }

        private void FilesListView_SelectionChanged(SelectionChangedEventArgs e)
        {
            // 1. Update Preview
            if (FileBrowser?.FilesSelectedItems != null && FileBrowser.FilesSelectedItems.Count == 1)
            {
                if (FileBrowser.FilesSelectedItem is FileSystemItem item)
                {
                    _fileInfoService?.ShowFileInfo(item);
                    _fileNotesUIHandler?.LoadFileNotes(item);
                    LoadFilePreview(item);
                }
            }
            else
            {
                ClearPreviewAndInfo();
            }



        }

        // Helpers for MenuEventHandler
        private void EditNotes_Click_Logic() => _fileNotesUIHandler?.ToggleNotesPanel();





        private void Back_Click_Logic()
        {
            if (_navigationService != null && _navigationService.CanNavigateBack)
            {
                _navigationService.NavigateBack();
            }
        }

        private void SetClipboardDataObjectWithRetry(System.Windows.DataObject data)
        {
            const int MaxRetries = 10;    // 从50减少到10
            const int DelayMs = 50;        // 从100ms减少到50ms

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    System.Windows.Clipboard.SetDataObject(data, true);
                    return;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    // CLIPBRD_E_CANT_OPEN = 0x800401D0
                    const uint CLIPBRD_E_CANT_OPEN = 0x800401D0;
                    if ((uint)ex.ErrorCode != CLIPBRD_E_CANT_OPEN)
                    {
                        throw;
                    }
                    if (i == MaxRetries - 1)
                    {
                        System.Windows.MessageBox.Show("剪贴板被占用，请稍后再试。", "复制失败", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }
                    System.Threading.Thread.Sleep(DelayMs);
                }
            }
        }
    }
}
