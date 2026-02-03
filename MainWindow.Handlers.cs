using System;
using YiboFile.Models;
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
using YiboFile.Handlers;
using HandlerMouseEventHandler = YiboFile.Handlers.MouseEventHandler;
using YiboFile.Services;
using YiboFile.Services.FileNotes;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Navigation;
using YiboFile.Services.Search;
using YiboFile.Services.Tabs;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Settings;
// using YiboFile.Services.TagTrain; // Phase 2
// using TagTrain.UI; // Phase 2
using System.Windows.Media;
using YiboFile.Services.Core;
using YiboFile.Services.Config;
using YiboFile.ViewModels.Messaging.Messages;


namespace YiboFile
{
    public partial class MainWindow
    {
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

        private void InitializeHandlers()
        {
            // 订阅 TabManager 的关闭覆盖层请求
            if (TabManager != null)
            {
                TabManager.CloseOverlayRequested += (s, e) => CloseOverlays();
            }

            if (SecondTabManager != null)
            {
                SecondTabManager.CloseOverlayRequested += (s, e) => CloseOverlays();
            }

            // 初始化 FileBrowserEventHandler
            _fileBrowserEventHandler = new FileBrowserEventHandler(
                _messageBus,
                "Primary",
                FileBrowser,
                _navigationCoordinator,
                _tabService,
                _searchService, // searchService
                _searchCacheService,
                NavigateToPath,
                SwitchNavigationMode,
                LoadCurrentDirectory,
                () => // ClearFilter
                {
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
                () => ConfigurationService.Instance.Config,
                () => null,

                (tag) => { },
                // MVVM 迁移: Handler 现在读取和更新 VM 数据
                () => _viewModel?.PrimaryPane?.FileList?.Files?.ToList() ?? new List<FileSystemItem>(),
                (files) => { _viewModel?.PrimaryPane?.FileList?.UpdateFiles(files); },
                () => _searchOptions,
                (e) => _selectionEventHandler?.HandleSelectionChanged(e.AddedItems), // FilesListView_SelectionChanged
                (e) => { }, // FilesListView_MouseDoubleClick
                (e) => { }, // PreviewMouseDoubleClick - not used
                (e) => { }, // PreviewKeyDown - handled by KeyboardEventHandler
                (e) => { }, // FilesListView_PreviewMouseLeftButtonDown
                (e) => { }, // FilesListView_MouseLeftButtonUp
                (e) => { }, // FilesListView_PreviewMouseDown
                (e) =>
                {
                    var result = _navigationService?.NavigateUp();
                    if (result != null)
                    {
                        // Actually perform the UI navigation with the returned path!
                        NavigateToPath(result);
                        e.Handled = true;
                    }
                }, // FilesListView_PreviewMouseDoubleClickForBlank
                (e) => { }, // FilesListView_PreviewMouseMove handled by DragDropManager directly
                () => ColLeft, // Func<ColumnDefinition>
                (e) => // CommitRename
                {
                    var (browser, path, library) = GetActiveContext();
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

            // Initialize Info Services
            var tagService = App.ServiceProvider?.GetService<YiboFile.Services.Features.ITagService>();
            _fileInfoService = new Services.FileInfo.FileInfoService(FileBrowser, _fileListService, _navigationCoordinator, tagService);

            if (SecondFileBrowser != null)
            {
                _secondFileInfoService = new Services.FileInfo.FileInfoService(SecondFileBrowser, _fileListService, _navigationCoordinator, tagService);
            }

            _selectionEventHandler = new SelectionEventHandler(
                _previewService,
                _messageBus,
                _fileListService,
                () => _currentFiles,
                () => _currentPath,
                () => _currentLibrary,
                () => IsDualListMode,
                path => _folderSizeCalculationService != null ? _folderSizeCalculationService.CalculateAndUpdateFolderSizeAsync(path) : Task.CompletedTask,
                item => _fileInfoService?.ShowFileInfo(item),
                lib => _fileInfoService?.ShowLibraryInfo(lib)
            );

            // Subscribe to Preview Navigation Requests
            _messageBus.Subscribe<PreviewNavigationRequestMessage>(msg =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var activeBrowser = GetActiveContext().browser;
                    if (activeBrowser != null && activeBrowser.FilesList != null)
                    {
                        var list = activeBrowser.FilesList;
                        if (list.Items.Count == 0) return;

                        int newIndex = -1;
                        if (list.SelectedIndex == -1)
                        {
                            newIndex = 0;
                        }
                        else
                        {
                            newIndex = msg.IsNext ? list.SelectedIndex + 1 : list.SelectedIndex - 1;
                        }

                        if (newIndex >= 0 && newIndex < list.Items.Count)
                        {
                            list.SelectedIndex = newIndex;
                            list.ScrollIntoView(list.Items[newIndex]);
                        }
                    }
                });
            });

            // Subscribe to Open File Requests
            _messageBus.Subscribe<OpenFileRequestMessage>(msg =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(msg.FilePath))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = msg.FilePath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            DialogService.Error($"无法打开文件: {ex.Message}", owner: this);
                        }
                    }
                });
            });

            // 初始化 ColumnInteractionHandler
            _columnInteractionHandler = new Handlers.ColumnInteractionHandler(this, FileBrowser, _columnService);

            _columnInteractionHandler.Initialize();
            _columnInteractionHandler.HookHeaderThumbs(); // 挂载列头拖拽事件

            // 初始化 Second ColumnInteractionHandler
            if (SecondFileBrowser != null)
            {
                _secondColumnInteractionHandler = new Handlers.ColumnInteractionHandler(this, SecondFileBrowser, _columnService);

                _secondColumnInteractionHandler.Initialize();
                _secondColumnInteractionHandler.Initialize();
                _secondColumnInteractionHandler.HookHeaderThumbs();

                // Wire up SecondFileBrowser Tag Click
                SecondFileBrowser.TagClicked += (s, tag) =>
                {
                    if (tag != null && !string.IsNullOrEmpty(tag.Name))
                    {
                        // Navigate in Second Pane
                        _navigationCoordinator.HandlePathNavigation(
                            $"tag://{tag.Name}",
                            NavigationSource.AddressBar,
                            ClickType.LeftClick,
                            pane: YiboFile.Services.Navigation.PaneId.Second
                        );
                    }
                };

                // Wire up SecondFileBrowser Sorting
                SecondFileBrowser.GridViewColumnHeaderClick += SecondGridViewColumnHeader_Click;

                // Wire up SecondFileBrowser Filter Click
                SecondFileBrowser.FilterClicked += (s, e) =>
                {
                    try
                    {
                        if (_searchOptions == null) return;
                        SecondFileBrowser.ToggleFilterPanel(_searchOptions, (sender, args) =>
                        {
                            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(SecondFileBrowser.FilesItemsSource);
                            if (view != null)
                            {
                                view.Filter = obj =>
                                {
                                    if (obj is not FileSystemItem item) return true;
                                    var opts = _searchOptions;
                                    // Reuse static logic from FileBrowserEventHandler
                                    if (opts.Type != FileTypeFilter.All && !Handlers.FileBrowserEventHandler.MatchesTypeFilter(item, opts.Type)) return false;
                                    if (opts.DateRange != DateRangeFilter.All && !Handlers.FileBrowserEventHandler.MatchesDateFilter(item, opts.DateRange)) return false;
                                    if (opts.SizeRange != SizeRangeFilter.All && !Handlers.FileBrowserEventHandler.MatchesSizeFilter(item, opts.SizeRange)) return false;
                                    return true;
                                };
                                view.Refresh();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SecondFilterClicked] Error: {ex.Message}");
                    }
                };
            }

            // 初始化 WindowLifecycleHandler
            _windowLifecycleHandler = new Handlers.WindowLifecycleHandler(this, _windowStateManager, _columnService);


            // 初始化统一文件操作服务 (新架构) - 逐步迁移中
            var errorService = App.ServiceProvider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>();
            var undoService = App.ServiceProvider.GetService(typeof(YiboFile.Services.FileOperations.Undo.UndoService)) as YiboFile.Services.FileOperations.Undo.UndoService;
            var taskQueueService = App.ServiceProvider.GetService(typeof(YiboFile.Services.FileOperations.TaskQueue.TaskQueueService)) as YiboFile.Services.FileOperations.TaskQueue.TaskQueueService;

            // 初始化 FileOperationHandler
            _fileOperationHandler = new Handlers.FileOperationHandler(this, undoService, _fileOperationService);

            // Wire up Undo/Redo events from FileBrowser
            if (FileBrowser != null)
            {
                FileBrowser.FileUndo += (s, e) => _fileOperationHandler.PerformUndo();
                FileBrowser.FileRedo += (s, e) => _fileOperationHandler.PerformRedo();
            }



            // 初始化 Main FileListEventHandler
            _mainFileListHandler = new Handlers.FileListEventHandler(
                FileBrowser,
                _navigationCoordinator,
                item => _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { item })),
                item => _previewService?.LoadFilePreview(item),
                path => { _folderSizeCalculationService?.CalculateAndUpdateFolderSizeAsync(path); }, // Fixed: Fire-and-forget async update
                () =>
                {
                    _messageBus.Publish(new FileSelectionChangedMessage(null));
                    // 修复：取消选择时显示当前文件夹/标签信息，而不是清空
                    _selectionEventHandler?.HandleNoSelection();
                },
                () => _currentLibrary != null, // IsLibraryMode
                mode => SwitchNavigationMode(mode),
                path => NavigateToPath(path),
                () => Back_Click_Logic(),
                col => AutoSizeGridViewColumn(col),
                () => _currentPath,
                () => CopySelectedFilesAsync().Wait(), // Simple wrapper, async void fire-and-forget style for events usually
                () => PasteFilesAsync().Wait(),
                () => { _menuEventHandler?.Cut_Click(null, null); },
                () => DeleteSelectedFilesAsync().Wait(),
                () => { _menuEventHandler?.Rename_Click(null, null); },
                () => RefreshActiveFileList(),
                () => ShowSelectedFileProperties(),
                (path, force, activate) => CreateTab(path, force, activate) // Main Browser CreateTab
            );
            _mainFileListHandler.Initialize(FileBrowser.FilesList);

            // 初始化 Second FileListEventHandler
            if (SecondFileBrowser != null)
            {
                _secondFileListHandler = new Handlers.FileListEventHandler(
                    SecondFileBrowser,
                    _navigationCoordinator,
                    item =>
                    {
                        _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { item }));
                        _secondFileInfoService?.ShowFileInfo(item); // Update Second Info Panel
                    },
                    item => { }, // Second browser preview might not be supported or same preview service
                    path => { _folderSizeCalculationService?.CalculateAndUpdateFolderSizeAsync(path); },
                    () =>
                    {
                        // 修复：取消选择时显示当前文件夹/标签信息，而不是清空
                        // 由于 SelectionEventHandler 目前绑定到 PrimaryPane，我们需要在这里手动执行类似的逻辑

                        try
                        {
                            // 1. 获取当前 Secondary 路径和上下文
                            string currentPath = _secondCurrentPath;

                            // 2. 也是先清除预览
                            // 注意：双栏模式下，Secondary 的预览请求应该也会更新 RightPanelViewModel
                            // 如果 RightPanelViewModel 是全局唯一的，我们需要告诉它“现在没选中文件了，但也别清空如果是文件夹信息的话”
                            // 但在这里，我们显式构建一个 Container Item 并显示它。

                            FileSystemItem containerItem = null;

                            // 处理 Tag 模式
                            if (!string.IsNullOrEmpty(currentPath) && currentPath.StartsWith("tag://", StringComparison.OrdinalIgnoreCase))
                            {
                                var tagName = currentPath.Substring(6);
                                containerItem = new FileSystemItem
                                {
                                    Name = tagName,
                                    Path = currentPath,
                                    Type = "标签",
                                    IsDirectory = true,
                                    Size = "-",
                                    ModifiedDate = "-",
                                    Tags = tagName
                                };
                            }
                            // 处理 Path 模式
                            else if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
                            {
                                var dirInfo = new System.IO.DirectoryInfo(currentPath);
                                containerItem = new FileSystemItem
                                {
                                    Name = dirInfo.Name,
                                    Path = dirInfo.FullName,
                                    Type = "文件夹",
                                    IsDirectory = true,
                                    ModifiedDateTime = dirInfo.LastWriteTime,
                                    ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy/M/d HH:mm"),
                                    Size = "-",
                                    Tags = ""
                                };
                            }

                            if (containerItem != null)
                            {
                                // 更新 Secondary 信息面板
                                _secondFileInfoService?.ShowFileInfo(containerItem);

                                // 发布消息以更新预览面板（显示文件夹预览或空）
                                _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { containerItem }, RequestPreview: false));
                            }
                            else
                            {
                                _secondFileInfoService?.ShowFileInfo(null);
                                _messageBus.Publish(new FileSelectionChangedMessage(null));
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SecondBrowser] HandleNoSelection Error: {ex.Message}");
                            _secondFileInfoService?.ShowFileInfo(null);
                        }
                    },
                    () => _viewModel?.SecondaryPane?.NavigationMode == "Library", // IsLibraryMode
                    mode => // SwitchNavigationMode
                    {
                        // Basic mode switch if needed, though usually handled by NavigationModeService
                        if (mode == "Length") { /* handled elsewhere */ }
                    },
                    path =>
                    {
                        // 直接调用路径加载，避免递归调用 ViewModel
                        LoadSecondFileBrowserDirectory(path);
                    },
                    () => { /* Second Browser Back Logic? */ },
                    col => AutoSizeGridViewColumn(col), // Helper might need adjustment for second browser context
                    () => _secondCurrentPath,
                    () => CopySelectedFilesAsync().Wait(),
                    () => PasteFilesAsync().Wait(),
                    () => { _menuEventHandler?.Cut_Click(null, null); }, // Context aware?
                    () => { /* Delete logic specific to second browser? Handled by GetActiveContext */ DeleteSelectedFilesAsync().Wait(); },
                    () => { _menuEventHandler?.Rename_Click(null, null); },
                    () => LoadSecondFileBrowserDirectory(_secondCurrentPath),
                    () => ShowSelectedFileProperties(), // Use the new method
                    (path, force, activate) => // Second Browser CreateTab
                    {
                        bool shouldActivate = activate ?? ConfigurationService.Instance.Config?.ActivateNewTabOnMiddleClick ?? true;
                        _secondTabService?.CreatePathTab(path, force, false, shouldActivate);
                    },

                    YiboFile.Services.Navigation.PaneId.Second
                );
                _secondFileListHandler.Initialize(SecondFileBrowser.FilesList);
            }

            var backupService = App.ServiceProvider.GetService(typeof(YiboFile.Services.Backup.IBackupService)) as YiboFile.Services.Backup.IBackupService;

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
                    taskQueueService,
                    backupService
                );

            // 监听撤销/重做事件以刷新UI
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

                // 定义更新 Undo/Redo 按钮状态的方法
                void UpdateUndoUI()
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        if (FileBrowser != null)
                        {
                            FileBrowser.UndoEnabled = undoService.CanUndo;
                            FileBrowser.RedoEnabled = undoService.CanRedo;
                            FileBrowser.UndoToolTipText = undoService.CanUndo ? $"撤销 {undoService.NextUndoDescription}" : "撤销";
                            FileBrowser.RedoToolTipText = undoService.CanRedo ? $"重做 {undoService.NextRedoDescription}" : "重做";
                        }
                    });
                }

                // 订阅 StackChanged 事件
                undoService.StackChanged += (s, e) => UpdateUndoUI();

                // 初始化状态
                UpdateUndoUI();
            }

            // 初始化 MenuEventHandler
            _menuEventHandler = new MenuEventHandler(
                FileBrowser,
                _libraryService,
                RefreshActiveFileList,
                LoadCurrentDirectory,
                () => // ClearFilter
                {
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
                        _settingsOverlayController?.Hide();
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
                () => { }, // Action editNotes (Deprecated)
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
                () => ShowSelectedFileProperties(), // ShowProperties
                NavigateToPath,
                SwitchNavigationMode,
                () => GetActiveContext().path,
                () => GetActiveContext().library,
                () => GetActiveContext().browser?.FilesItemsSource as System.Collections.Generic.List<FileSystemItem>,
                (files) =>
                {
                    var b = GetActiveContext().browser;
                    if (b != null)
                    {
                        if (b == FileBrowser) _viewModel?.PrimaryPane?.FileList?.UpdateFiles(files);
                        else if (b == SecondFileBrowser) _viewModel?.SecondaryPane?.FileList?.UpdateFiles(files);
                        else b.FilesItemsSource = files;
                    }
                    if (b == FileBrowser) _currentFiles = files;
                    else if (b == SecondFileBrowser) _secondCurrentFiles = files;
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
                 () => ConfigurationService.Instance.Config,
                 (cfg) => { /* ConfigurationService.Instance already handles config updates */ },
                 () => ConfigurationService.Instance.SaveNow()
            );


            // 定义获取当前活动标签页服务的逻辑
            Func<Services.Tabs.TabService> getActiveTabService = () =>
                (IsDualListMode && IsSecondPaneFocused && _secondTabService != null) ? _secondTabService : _tabService;

            // 初始化 KeyboardEventHandler
            _keyboardEventHandler = new YiboFile.Handlers.KeyboardEventHandler(
                FileBrowser,
                () => GetActiveContext().browser, // NEW: Active browser delegate
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
                () => CloseOverlays(), // closeOverlays
                Back_Click_Logic, // navigateBack
                () => Undo_Click(null, null),
                () => Redo_Click(null, null),
                SwitchLayoutModeByIndex,  // 添加布局切换回调
                () => _layoutModule?.IsDualListMode ?? false, // isDualListMode 检查
                () => _layoutModule?.SwitchFocusedPane() // switchDualPaneFocus 回调
            );

            // 初始化 MouseEventHandler
            _mouseEventHandler = new HandlerMouseEventHandler(
                () => WindowMaximize_Click(null, null),
                () => DragMove(),
                // () => FavoritesListBox, // Removed
                () => QuickAccessListBox,
                _navigationCoordinator,
                (fav) => _navigationCoordinator.HandleFavoriteNavigation(fav, ClickType.LeftClick),
                (path) => _navigationCoordinator.HandlePathNavigation(path, NavigationSource.QuickAccess, ClickType.LeftClick)
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



        private void QuickAccessListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var path = ExtractPathFromListBoxItem(listBox, e.GetPosition(listBox));
            if (!string.IsNullOrEmpty(path))
            {
                _navigationService.LastLeftNavSource = "QuickAccess";
                _navigationCoordinator.HandlePathNavigation(path, NavigationSource.QuickAccess, clickType);
                e.Handled = true;
            }
        }

        private void FolderFavoritesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HandleFavoriteListBoxPreviewMouseDown(sender as ListBox, e, "FolderFavorites");
        }

        private void FileFavoritesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HandleFavoriteListBoxPreviewMouseDown(sender as ListBox, e, "FileFavorites");
        }

        private void HandleFavoriteListBoxPreviewMouseDown(ListBox listBox, MouseButtonEventArgs e, string sourceName)
        {
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var favorite = ExtractFavoriteFromListBoxItem(listBox, e.GetPosition(listBox));
            if (favorite != null)
            {
                _navigationService.LastLeftNavSource = sourceName;
                _navigationCoordinator.HandleFavoriteNavigation(favorite, clickType);
                e.Handled = true;
            }
        }

        private void ShowSelectedFileProperties()
        {
            var (browser, path, library) = GetActiveContext();
            var item = browser?.FilesSelectedItem as FileSystemItem;

            // 目标路径：优先选中项，否则当前文件夹
            string targetPath = null;
            if (item != null && !string.IsNullOrEmpty(item.Path))
            {
                targetPath = item.Path;
            }
            else if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !ProtocolManager.IsVirtual(path))
            {
                // 注意：只有物理路径才支持文件夹属性
                targetPath = path;
            }

            if (!string.IsNullOrEmpty(targetPath))
            {
                // 如果是虚拟路径（如 zip 内部），可能无法显示系统属性，给予提示或处理
                if (ProtocolManager.IsVirtual(targetPath))
                {
                    // 暂时不支持压缩包内文件的系统属性
                    MessageBox.Show($"暂不支持查看此类型的系统属性：\n{targetPath}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Services.Core.ShellNative.ShowFileProperties(targetPath);
            }
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
                    _viewModel?.PrimaryPane?.FileList?.UpdateFiles(_currentFiles);
                },
                FileBrowser.FilesGrid
            );
        }

        internal void SecondGridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var header = sender as GridViewColumnHeader;
            if (header == null || SecondFileBrowser == null) return;

            // Simple debounce (optional, but good practice)
            // Reusing same variables might be tricky if dual clicking, but explicit click is serial.
            // Let's use local debounce if needed or shared - shared is fine for UI clicks.
            var now = DateTime.Now;
            var columnTag = header.Tag?.ToString();
            if ((now - _lastColumnClickTime).TotalMilliseconds < 200 && columnTag == _lastClickedColumn)
            {
                return;
            }
            _lastColumnClickTime = now;
            _lastClickedColumn = columnTag;

            var currentFiles = SecondFileBrowser.FilesItemsSource as IEnumerable<FileSystemItem>;
            if (currentFiles == null) return;
            var fileList = currentFiles.ToList();

            _columnService?.HandleColumnHeaderClick(
                header,
                fileList,
                (sortedFiles) =>
                {
                    _secondCurrentFiles = sortedFiles;
                    _viewModel?.SecondaryPane?.FileList?.UpdateFiles(sortedFiles);
                },
                SecondFileBrowser.FilesGrid
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



        // Helpers for MenuEventHandler





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
                        DialogService.Warning("剪贴板被占用，请稍后再试。", owner: this);
                        return;
                    }
                    System.Threading.Thread.Sleep(DelayMs);
                }
            }
        }
    }
}

