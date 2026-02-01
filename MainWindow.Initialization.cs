using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services;
using YiboFile.Services.FileNotes;
using YiboFile.Services.Search;
using YiboFile.Services.Navigation;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Favorite;
using YiboFile.Services.QuickAccess;
using YiboFile.Services.FileList;
using YiboFile.Services.Tabs;
using YiboFile.Services.Preview;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Config;
using YiboFile.Services.Archive; // Import Archive Service


using YiboFile.Helpers;
using YiboFile.Handlers;
using YiboFile.Models.UI;
using System.ComponentModel;

namespace YiboFile
{
    public partial class MainWindow
    {
        internal FileListService _secondFileListService;
        internal List<FileSystemItem> _secondCurrentFiles = new List<FileSystemItem>();

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _fileBrowserEventHandler?.HandleGlobalMouseDown(e);

            // Apply the same global mouse down logic for the Secondary File Browser
            // If the Secondary Address Bar is in edit mode and the click is outside it, close edit mode.
            if (SecondFileBrowser != null && SecondFileBrowser.AddressBarControl != null &&
                SecondFileBrowser.AddressBarControl.IsEditMode)
            {
                var source = e.OriginalSource as DependencyObject;
                bool isAddressBar = false;

                // Check if the click target is within the AddressBarControl
                var current = source;
                while (current != null)
                {
                    if (current == SecondFileBrowser.AddressBarControl)
                    {
                        isAddressBar = true;
                        break;
                    }
                    if (current is Visual || current is System.Windows.Media.Media3D.Visual3D)
                    {
                        current = VisualTreeHelper.GetParent(current);
                    }
                    else if (current is FrameworkContentElement fce)
                    {
                        current = fce.Parent;
                    }
                    else
                    {
                        current = null;
                    }
                }

                if (!isAddressBar)
                {
                    // If clicked outside, exit edit mode
                    SecondFileBrowser.AddressBarControl.SwitchToBreadcrumbMode();
                }
            }
        }

        // ... existing codes ...


        // 响应式布局现在由 FileListControl 内部的 ListView.SizeChanged 处理
        // 此方法已废弃
        /*
        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 将 ColCenter 的实际宽度传递给 FileListControl 进行响应式布局
            if (FileBrowser?.FileList != null && ColCenter != null)
            {
                FileBrowser.FileList.ApplyResponsiveLayout(ColCenter.ActualWidth);
            }
        }
        */

        private void InitializeServices()
        {
            // 初始化统一导航协调器
            _navigationCoordinator = new NavigationCoordinator();

            // 初始化服务实例
            _navigationService = new NavigationService(_currentPath);

            // 创建并设置 UI Helper
            var uiHelper = new NavigationUIHelper(this);
            _navigationService.UIHelper = uiHelper;

            _libraryService = App.ServiceProvider.GetRequiredService<LibraryService>();
            _favoriteService = App.ServiceProvider.GetRequiredService<FavoriteService>();
            _quickAccessService = App.ServiceProvider.GetRequiredService<QuickAccessService>();
            _fileListService = App.ServiceProvider.GetRequiredService<FileListService>();

            // 初始化 TagService
            if (App.IsTagTrainAvailable)
            {
                _tagService = App.ServiceProvider.GetService<Services.Features.ITagService>();
            }

            // 将 FileListService 传递给 FileListControl
            FileBrowser?.FileList?.SetFileListService(_fileListService);

            // 初始化副文件列表服务
            _secondFileListService = App.ServiceProvider.GetRequiredService<FileListService>();
            SecondFileBrowser?.GetFileListControl()?.SetFileListService(_secondFileListService);

            _fileSystemWatcherService = App.ServiceProvider.GetRequiredService<FileSystemWatcherService>();
            _folderSizeCalculationService = App.ServiceProvider.GetRequiredService<FolderSizeCalculationService>();
            _archiveService = App.ServiceProvider.GetRequiredService<ArchiveService>();


            // 初始化标签页服务（需要配置，在加载配置后更新）
            // 注意：_config 将在 InitializeApplication 中加载，这里先创建空配置
            // 初始化标签页服务（需要配置，在加载配置后更新）
            // 使用 DI 获取 Transient 实例，分别为两个面板创建独立的服务
            _tabService = App.ServiceProvider.GetRequiredService<TabService>();
            _secondTabService = App.ServiceProvider.GetRequiredService<TabService>();

            // 初始化协调器与服务的关联
            _navigationCoordinator.Initialize(
                _tabService,
                _secondTabService,
                _navigationService,
                _libraryService,
                (path) => NavigateToPath(path),
                (path) => SecondFileBrowser_PathChanged(this, path));

            // 初始化搜索服务
            // 注意：SearchResultBuilder 已在 DI 中注册但需要 FileListService 的依赖，这里通过 DI 获取 SearchService
            // 虽然 SearchResultBuilder 是 Transient 的，但 SearchService 是 Transient (or Singleton? App.xaml.cs says Transient)，
            // 且 SearchService 依赖 SearchFilterService (Singleton) 和 SearchCacheService (Singleton) 和 SearchResultBuilder (Transient)
            // 我们在 App.xaml.cs 中已经注册了 SearchService 及其依赖
            _searchCacheService = App.ServiceProvider.GetRequiredService<SearchCacheService>();
            _searchService = App.ServiceProvider.GetRequiredService<SearchService>();

            // 保存 ConfigService 引用并注入 UIHelper
            _configService = App.ServiceProvider.GetRequiredService<ConfigService>();
            _configService.UIHelper = this;

            // 初始化列管理服务
            _columnService = App.ServiceProvider.GetRequiredService<ColumnService>();
            _columnService.Initialize(
                () => GetCurrentModeKey(),
                () => { if (_configService != null) _configService.SaveCurrentConfig(); }
            );

            AttachTabServiceUiContext();
            AttachSecondTabServiceUiContext();
            _tabService.InitializeTabSizeHandler(); // Enable tab width compression

            // 初始化 UI 辅助服务（需要在 InitializeComponent 之后，因为需要 FileBrowser）
            _uiHelperService = new Services.UIHelper.UIHelperService(FileBrowser, this.Dispatcher);


            // tagUIHandler 初始化已注释 - Phase 2将重新实现
            // var tagUIHandlerContext = new TagUIHandlerContextImpl(this);
            // _tagUIHandler = new Services.Tag.TagUIHandler(tagUIHandlerContext);

            // 初始化预览服务（使用 MVVM 模式）
            var messageBus = App.ServiceProvider.GetRequiredService<YiboFile.ViewModels.Messaging.IMessageBus>();
            _previewService = new Services.Preview.PreviewService(
                messageBus,
                this.Dispatcher,
                LoadCurrentDirectory,
                path => CreateTab(path, true)
            );

            // 初始化文件操作服务
            // 此时 UndoService, TaskQueueService 已通过 DI 注入到 FileOperationService 的构造函数中
            // 我们只需要提供 ContextProvider
            _fileOperationService = new FileOperationService(
                () => GetActiveFileOperationContext(),
                App.ServiceProvider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>(),
                App.ServiceProvider.GetRequiredService<YiboFile.Services.FileOperations.Undo.UndoService>(),
                App.ServiceProvider.GetRequiredService<YiboFile.Services.FileOperations.TaskQueue.TaskQueueService>()
            );

            // 订阅全局错误事件
            var errorService = App.ServiceProvider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>();
            errorService.ErrorOccurred += (s, e) =>
            {
                // 确保在UI线程执行
                this.Dispatcher.Invoke(() =>
                {
                    if (e.Severity == YiboFile.Services.Core.Error.ErrorSeverity.Critical)
                    {
                        DialogService.Error(e.Message, "严重错误", this);
                    }
                    else
                    {
                        var notificationType = e.Severity switch
                        {
                            YiboFile.Services.Core.Error.ErrorSeverity.Warning => YiboFile.Controls.NotificationType.Warning,
                            YiboFile.Services.Core.Error.ErrorSeverity.Error => YiboFile.Controls.NotificationType.Error,
                            _ => YiboFile.Controls.NotificationType.Info
                        };

                        Services.Core.NotificationService.Show(e.Message, notificationType);
                    }
                });
            };
        }

        private void InitializeEvents()
        {
            // 全局鼠标事件
            this.PreviewMouseDown += MainWindow_PreviewMouseDown;

            // 响应式布局现在由 FileListControl 内部处理，不再需要此事件
            /*
            if (RootGrid != null)
            {
                RootGrid.SizeChanged += RootGrid_SizeChanged;
            }
            */

            // 订阅 RightPanel 事件
            if (RightPanel != null)
            {
                RightPanel.NotesHeightChanged += RightPanel_NotesHeightChanged;
            }



            // 订阅 NavigationService 事件
            _navigationService.NavigateRequested += OnNavigationServiceNavigateRequested;

            // 订阅标签页服务事件
            _tabService.TabAdded += (s, tab) => { /* UI 已通过 CreateTabInternal 处理 */ };
            _tabService.TabRemoved += (s, tab) => { /* UI 已通过 CloseTab 处理 */ };
            _tabService.ActiveTabChanged += (s, tab) =>
            {
                if (tab != null)
                {
                    UpdateTabStyles();

                    // 切换标签页时自动刷新信息面板（处理空选状态）
                    _selectionEventHandler?.HandleNoSelection();

                    // 切换标签页时自动聚焦主文件列表
                    if (_isDualListMode && _isSecondPaneFocused)
                    {
                        _isSecondPaneFocused = false;
                        UpdateFocusBorders();
                        FileBrowser?.FilesList?.Focus();
                    }
                }
            };

            // 订阅副标签页服务事件
            _secondTabService.ActiveTabChanged += (s, tab) =>
            {
                if (tab != null)
                {
                    // 切换标签页时自动聚焦副文件列表
                    if (_isDualListMode && !_isSecondPaneFocused)
                    {
                        _isSecondPaneFocused = true;
                        UpdateFocusBorders();
                        SecondFileBrowser?.FilesList?.Focus();
                    }
                    _secondTabService?.UpdateTabStyles();
                }
            };

            _secondTabService.TabPinStateChanged += (s, tab) =>
            {
                _secondTabService.ApplyPinVisual(tab);
                _secondTabService.ReorderTabs();
            };
            _secondTabService.TabTitleChanged += (s, tab) =>
            {
                _secondTabService.ApplyPinVisual(tab);
            };

            _tabService.TabPinStateChanged += (s, tab) =>
            {
                _tabService.ApplyPinVisual(tab);
                _tabService.ReorderTabs();
            };
            _tabService.TabTitleChanged += (s, tab) =>
            {
                _tabService.ApplyPinVisual(tab);
            };

            // 订阅 FileListService 事件
            _fileListService.FolderSizeCalculated += OnFileListServiceFolderSizeCalculated;
            _fileListService.MetadataEnriched += OnFileListServiceMetadataEnriched;

            // 订阅副文件列表服务事件
            if (_secondFileListService != null)
            {
                _secondFileListService.FolderSizeCalculated += OnFileListServiceFolderSizeCalculated;
                _secondFileListService.MetadataEnriched += OnFileListServiceMetadataEnriched;
            }


            // 订阅 FileSystemWatcherService 事件
            _fileSystemWatcherService.FileSystemChanged += OnFileSystemWatcherServiceFileSystemChanged;
            _fileSystemWatcherService.RefreshRequested += OnFileSystemWatcherServiceRefreshRequested;

            // 订阅库服务事件
            _libraryService.LibrariesLoaded += (s, libraries) =>
            {
                var currentSelected = LibrariesListBox?.SelectedItem;
                LibrariesListBox.ItemsSource = null;
                LibrariesListBox.ItemsSource = libraries;
                LibrariesListBox.Items.Refresh();

                if (currentSelected != null)
                {
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        _uiHelperService?.EnsureSelectedItemVisible(LibrariesListBox, currentSelected);
                        HighlightMatchingLibrary(currentSelected as Library);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            };

            _libraryService.LibraryFilesLoaded += (s, e) =>
            {

                if (e.IsEmpty)
                {
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                    {
                        _viewModel?.PrimaryPane?.FileList?.Files?.Clear();
                        FileBrowser.AddressText = e.Library.Name + " (无位置)";
                    }
                    // ShowEmptyLibraryMessage(e.Library.Name);
                    // ClearPreviewAndInfo();
                    // ClearItemHighlights();
                    // ClearTabsInLibraryMode(); // 移除此调用，避免清空所有标签页
                }
                else
                {
                    ShowMergedLibraryFiles(e.Files, e.Library);
                }
            };

            _libraryService.LibraryHighlightRequested += (s, library) =>
            {
                HighlightMatchingLibrary(library);
            };

            // 订阅收藏服务事件
            _favoriteService.NavigateRequested += (s, path) =>
            {
                _navigationCoordinator.HandlePathNavigation(path, NavigationSource.Favorite, ClickType.LeftClick);
            };

            _favoriteService.FileOpenRequested += (s, filePath) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    DialogService.Error($"无法打开文件: {ex.Message}", owner: this);
                }
            };

            _favoriteService.CreateTabRequested += (s, path) =>
            {
                _navigationCoordinator.HandlePathNavigation(path, NavigationSource.Favorite, ClickType.LeftClick, forceNewTab: true);
            };



            // 订阅快速访问服务事件
            _quickAccessService.NavigateRequested += (s, path) =>
            {
                _navigationCoordinator.HandlePathNavigation(path, NavigationSource.QuickAccess, ClickType.LeftClick);
            };

            _quickAccessService.CreateTabRequested += (s, path) =>
            {
                _navigationCoordinator.HandlePathNavigation(path, NavigationSource.QuickAccess, ClickType.LeftClick, forceNewTab: true);
            };

            _navigationCoordinator.PathNavigateRequested += (path, forceNewTab, activate) =>
            {
                if (forceNewTab)
                {
                    CreateTab(path, true, activate);
                }
                else
                {
                    // 统一使用 NavigateToPath，该方法会根据当前标签页状态决定是激活还是刷新
                    NavigateToPath(path);
                }
            };
            _navigationCoordinator.LibraryNavigateRequested += (library, forceNewTab, activate) =>
            {
                OpenLibraryInTab(library, forceNewTab, activate);
            };
            _navigationCoordinator.FileOpenRequested += (filePath) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    DialogService.Error($"无法打开文件: {ex.Message}", owner: this);
                }
            };
            _navigationCoordinator.FavoritePathNotFound += (favorite) =>
            {
                if (DialogService.Ask($"路径不存在: {favorite.Path}\n\n是否从收藏中移除？", "提示", this))
                {
                    _favoriteService.RemoveFavorite(favorite.Path);
                }
            };

            // 为库列表添加鼠标事件处理，检测鼠标中键和Ctrl键
            if (NavigationPanelControl?.LibrariesListBoxControl != null)
            {
                NavigationPanelControl.LibrariesListBoxControl.PreviewMouseDown += LibrariesListBox_PreviewMouseDown;
            }

            // 订阅NavigationPanelControl的事件
            if (NavigationPanelControl != null)
            {
                NavigationPanelControl.LibrariesListBoxPreviewMouseDown += LibrariesListBox_PreviewMouseDown;
                NavigationPanelControl.DrivesTreeViewItemClick += DrivesTreeViewItem_Click;
                // NavigationPanelControl.DrivesListBoxPreviewMouseDown += DrivesListBox_PreviewMouseDown;
                NavigationPanelControl.QuickAccessListBoxPreviewMouseDown += QuickAccessListBox_PreviewMouseDown;
                NavigationPanelControl.FavoriteListBoxPreviewMouseDown += OnFavoriteListBoxPreviewMouseDown;
                NavigationPanelControl.FavoriteListBoxLoaded += OnFavoriteListBoxLoaded;
                NavigationPanelControl.RenameFavoriteGroupRequested += OnRenameFavoriteGroupRequested;
                NavigationPanelControl.DeleteFavoriteGroupRequested += OnDeleteFavoriteGroupRequested;
                NavigationPanelControl.LibrariesListBoxContextMenuOpening += LibrariesListBox_ContextMenuOpening;
                NavigationPanelControl.LibraryManageClick += ManageLibraries_Click;


                NavigationPanelControl.PathManageClick += (s, e) =>
                {
                    var window = new YiboFile.Windows.NavigationSettingsWindow("Path");
                    window.Owner = this;
                    window.ShowDialog();
                };

                if (NavigationPanelControl.TagBrowsePanelControl != null)
                {
                    NavigationPanelControl.TagBrowsePanelControl.TagClicked += OnTagSelected;
                    NavigationPanelControl.TagBrowsePanelControl.BackRequested += (s, e) =>
                    {
                        // Navigate back when back button is clicked in TagBrowsePanel
                        _viewModel?.Navigation?.NavigateBackCommand?.Execute(null);
                    };
                }
            }

            // 订阅 FileBrowser 事件
            if (FileBrowser != null)
            {
                FileBrowser.InfoHeightChanged += FileBrowser_InfoHeightChanged;
                FileBrowser.NavigationBack += (s, e) => _viewModel?.Navigation?.NavigateBackCommand?.Execute(null);
                FileBrowser.NavigationForward += (s, e) => _viewModel?.Navigation?.NavigateForwardCommand?.Execute(null);
                FileBrowser.NavigationUp += (s, e) => _viewModel?.Navigation?.NavigateUpCommand?.Execute(null);
                FileBrowser.ViewModeChanged += FileBrowser_ViewModeChanged;

                // Toolbar & Context Menu operations
                FileBrowser.FileNewFolder += (s, e) => _menuEventHandler?.NewFolder_Click(s, e);
                FileBrowser.FileNewFile += (s, e) => _menuEventHandler?.NewFile_Click(s, e);
                FileBrowser.FileCopy += (s, e) => _menuEventHandler?.Copy_Click(s, e);
                FileBrowser.FileCut += (s, e) => _menuEventHandler?.Cut_Click(s, e);
                FileBrowser.FilePaste += (s, e) => _menuEventHandler?.Paste_Click(s, e);
                FileBrowser.FileDelete += async (s, e) =>
                {
                    try
                    {
                        await DeleteSelectedFilesAsync();
                    }
                    catch (Exception ex)
                    {
                        DialogService.Error($"删除操作失败: {ex.Message}", owner: this);
                    }
                };
                FileBrowser.FileRename += (s, e) => _menuEventHandler?.Rename_Click(s, e);
                FileBrowser.FileRefresh += (s, e) => RefreshFileList();
                FileBrowser.FileProperties += (s, e) => ShowSelectedFileProperties();
                FileBrowser.FileAddTag += FileAddTag_Click;
            }

            this.Activated += (s, e) =>
            {
                var activeTab = _tabService.ActiveTab;
                if (activeTab != null && activeTab.Path != null && activeTab.Path.StartsWith("search://"))
                {
                    CheckAndRefreshSearchTab(activeTab.Path);
                }
            };

            // 初始化主题切换事件
            InitializeThemeEvents();

            // 订阅分割器折叠事件，动态调整标签页边距
            if (SplitterRight != null)
            {
                SplitterRight.CollapsedStateChanged += (s, e) => UpdateTabManagerMargin();
            }
        }

        private void AttachTabServiceUiContext()
        {
            if (_tabService == null) return;
            var context = new TabUiContext
            {
                FileBrowser = FileBrowser,
                TabManager = TabManager,

                Dispatcher = this.Dispatcher,
                OwnerWindow = this,
                GetConfig = () => _configService?.Config ?? new AppConfig(),
                SaveConfig = ConfigManager.Save,
                GetCurrentLibrary = () => _currentLibrary,
                SetCurrentLibrary = lib => _currentLibrary = lib,
                GetCurrentPath = () => _currentPath,
                SetCurrentPath = path => _currentPath = path,
                SetNavigationCurrentPath = path => _navigationService.CurrentPath = path,
                LoadLibraryFiles = lib => LoadLibraryFiles(lib),
                NavigateToPathInternal = NavigateToPathFromModule,
                UpdateNavigationButtonsState = UpdateNavigationButtonsState,

                SearchService = _searchService,
                GetSearchCacheService = () => _searchCacheService,
                GetSearchOptions = () => _searchOptions,
                GetCurrentFiles = () => _currentFiles,
                SetCurrentFiles = files => { _currentFiles = files; _viewModel?.PrimaryPane?.FileList?.UpdateFiles(files); },
                ClearFilter = ClearFilter,
                RefreshSearchTab = path => { CheckAndRefreshSearchTab(path); return Task.CompletedTask; },
                FindResource = key => FindResource(key),

                // 获取当前导航模式
                GetCurrentNavigationMode = () => _configService?.Config?.LastNavigationMode ?? "Path",

                TagService = _tagService
            };
            _tabService.AttachUiContext(context);

            // [SSOT] 核心订阅：当活动标签页改变时同步UI
            _tabService.ActiveTabChanged += (s, tab) => SyncUiWithActiveTab(tab);

            // 订阅新建标签页事件
            TabManager.NewTabRequested += (s, e) =>
            {
                try
                {
                    _tabService?.CreateBlankTab();
                }
                catch
                {
                    // 忽略错误
                }
            };
        }

        /// <summary>
        /// [SSOT] 基于当前活动标签页状态同步全屏 UI
        /// </summary>
        private void SyncUiWithActiveTab(PathTab tab)
        {
            if (tab == null) return;

            // 1. 同步库/路径上下文
            if (tab.Type == TabType.Library)
            {
                if (_currentLibrary == tab.Library && _currentFiles.Count > 0)
                {
                    // 已经是当前库且有文件，跳过重新加载
                    HighlightMatchingLibrary(tab.Library);
                    return;
                }

                _currentLibrary = tab.Library;
                _currentPath = null;
                if (tab.Library != null)
                {
                    HighlightMatchingLibrary(tab.Library);
                    LoadLibraryFiles(tab.Library);
                }
            }
            else
            {
                if (_currentPath == tab.Path && _currentFiles.Count > 0)
                {
                    // 已经是当前路径且有文件，跳过重新加载
                    // 但需要确保导航服务路径同步
                    _navigationService.CurrentPath = tab.Path;
                    HighlightMatchingLibrary(null);
                    return;
                }

                _currentLibrary = null;
                _currentPath = tab.Path;
                _navigationService.CurrentPath = tab.Path;
                HighlightMatchingLibrary(null); // 清除库高亮

                // 2. 只有在不处于搜索模式时才执行地址栏同步
                // 搜索模式下 AddressText 由搜索逻辑动态维护
                if (tab.Path != null && !tab.Path.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
                {
                    NavigateToPathFromModule(tab.Path);
                }
            }

            // 3. 监听标签页内部状态变更（例如路径在后台加载完成或重命名）
            tab.PropertyChanged -= OnActiveTabPropertyChanged; // 防重复
            tab.PropertyChanged += OnActiveTabPropertyChanged;

            // 4. 更新导航按钮（前进/后退等）
            UpdateNavigationButtonsState();
        }

        private void OnActiveTabPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is PathTab tab && tab == _tabService.ActiveTab)
            {
                if (e.PropertyName == nameof(PathTab.Path) || e.PropertyName == nameof(PathTab.Library))
                {
                    // 当标签页路径变更时，重新同步 UI
                    SyncUiWithActiveTab(tab);
                }
            }
        }

        private FileOperationContext GetActiveFileOperationContext()
        {
            // 确定当前活动的面板
            bool useSecond = IsDualListMode && _isSecondPaneFocused;

            var targetBrowser = useSecond ? SecondFileBrowser : FileBrowser;
            var targetPath = useSecond ? SecondFileBrowser?.AddressText : _currentPath;

            // TODO: Determine library for second pane if separate
            var targetLibrary = useSecond ? null : _currentLibrary;

            return new FileOperationContext
            {
                TargetPath = targetPath,
                CurrentLibrary = targetLibrary,
                OwnerWindow = this,
                RefreshCallback = () =>
                {
                    if (useSecond)
                    {
                        if (SecondFileBrowser != null && !string.IsNullOrEmpty(SecondFileBrowser.AddressText))
                            LoadSecondFileBrowserDirectory(SecondFileBrowser.AddressText);
                    }
                    else
                    {
                        RefreshFileList();
                    }
                }
            };
        }
    }
}

