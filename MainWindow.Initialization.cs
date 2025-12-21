using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
using OoiMRR.Services.Tag;
using OoiMRR.Models.UI;

namespace OoiMRR
{
    public partial class MainWindow
    {
        private void InitializeServices()
        {
            // 初始化统一导航协调器
            _navigationCoordinator = new NavigationCoordinator();

            // 初始化服务实例
            _navigationService = new NavigationService(_currentPath);

            // 创建并设置 UI Helper
            var uiHelper = new NavigationUIHelper(this);
            _navigationService.UIHelper = uiHelper;

            _libraryService = new LibraryService(this.Dispatcher);
            _favoriteService = new FavoriteService(this.Dispatcher);
            _quickAccessService = new QuickAccessService(this.Dispatcher);
            _fileListService = new FileListService(this.Dispatcher);
            _fileSystemWatcherService = new FileSystemWatcherService(this.Dispatcher);
            _folderSizeCalculationService = new FolderSizeCalculationService();

            // 初始化标签页服务（需要配置，在加载配置后更新）
            // 注意：_config 将在 InitializeApplication 中加载，这里先创建空配置
            _tabService = new TabService(new AppConfig());

            // 初始化搜索服务
            var searchFilterService = new SearchFilterService();
            _searchCacheService = new SearchCacheService();
            var searchResultBuilder = new SearchResultBuilder(
                formatFileSize: size => _fileListService.FormatFileSize(size),
                getFileTagIds: path => App.IsTagTrainAvailable ? OoiMRRIntegration.GetFileTagIds(path) : null,
                getTagName: tagId => App.IsTagTrainAvailable ? OoiMRRIntegration.GetTagName(tagId) : null,
                getFileNotes: path => FileNotesService.GetFileNotes(path)
            );
            _searchService = new SearchService(searchFilterService, _searchCacheService, searchResultBuilder);

            // 初始化列管理服务
            // 注意：_config 将在 InitializeApplication 中加载，这里先创建空配置
            var tempConfig = new AppConfig();
            _columnService = new ColumnService(
                tempConfig,
                () => GetCurrentModeKey(),
                () => { if (_configService != null) _configService.SaveCurrentConfig(); }
            );

            AttachTabServiceUiContext();

            // 初始化 UI 辅助服务（需要在 InitializeComponent 之后，因为需要 FileBrowser）
            _uiHelperService = new Services.UIHelper.UIHelperService(FileBrowser, this.Dispatcher);

            // 初始化文件信息服务（需要在 InitializeComponent 之后，因为需要 FileBrowser）
            _fileInfoService = new Services.FileInfo.FileInfoService(FileBrowser, _fileListService);

            // 初始化备注UI处理器（需要在 InitializeComponent 之后，因为需要 RightPanel 和 FileBrowser）
            _fileNotesUIHandler = new Services.FileNotes.FileNotesUIHandler(RightPanel, FileBrowser);

            // 初始化标签UI处理器（需要创建上下文）
            var tagUIHandlerContext = new TagUIHandlerContextImpl(this);
            _tagUIHandler = new Services.Tag.TagUIHandler(tagUIHandlerContext);

            // 初始化预览服务（需要在 InitializeComponent 之后，因为需要 RightPanel 和 FileBrowser）
            _previewService = new Services.Preview.PreviewService(
                RightPanel,
                FileBrowser,
                this.Dispatcher,
                LoadCurrentDirectory,
                path => CreateTab(path, true)
            );
        }

        private void InitializeEvents()
        {
            // 订阅 RightPanel 事件
            if (RightPanel != null)
            {
                RightPanel.NotesTextChanged += NotesTextBox_TextChanged;
                RightPanel.NotesAutoSaved += NotesAutoSaved_Handler;
                RightPanel.PreviewOpenFileRequested += RightPanel_PreviewOpenFileRequested;
                RightPanel.PreviewMiddleClickRequested += RightPanel_PreviewMiddleClickRequested;
            }

            // 订阅 FileBrowser 导航事件
            if (FileBrowser != null)
            {
                FileBrowser.NavigationBack += NavigateBack_Click;
                FileBrowser.NavigationForward += NavigateForward_Click;
                FileBrowser.NavigationUp += NavigateUp_Click;
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
                }
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
            // _fileListService.FilesLoaded += OnFileListServiceFilesLoaded; // 已改为直接在 LoadFilesAsync 中处理
            _fileListService.FolderSizeCalculated += OnFileListServiceFolderSizeCalculated;
            _fileListService.MetadataEnriched += OnFileListServiceMetadataEnriched;
            _fileListService.ErrorOccurred += OnFileListServiceErrorOccurred;

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
                _isLoadingFiles = false;
                _loadFilesSemaphore.Release();

                if (e.IsEmpty)
                {
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = null;
                        FileBrowser.AddressText = e.Library.Name + " (无位置)";
                    }
                    ShowEmptyLibraryMessage(e.Library.Name);
                    ClearPreviewAndInfo();
                    ClearItemHighlights();
                    ClearTabsInLibraryMode();
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
                _navigationService.LastLeftNavSource = "Favorites";
                _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.Favorite, NavigationCoordinator.ClickType.LeftClick);
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
                    MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            _favoriteService.CreateTabRequested += (s, path) =>
            {
                CreateTab(path, true);
            };

            _favoriteService.FavoritesLoaded += (s, e) =>
            {
                // 收藏列表已加载，UI已更新
            };

            // 订阅快速访问服务事件
            _quickAccessService.NavigateRequested += (s, path) =>
            {
                _navigationService.LastLeftNavSource = "QuickAccess";
                _navigationCoordinator.HandlePathNavigation(path, NavigationCoordinator.NavigationSource.QuickAccess, NavigationCoordinator.ClickType.LeftClick);
            };

            _quickAccessService.CreateTabRequested += (s, path) =>
            {
                CreateTab(path, true);
            };

            _navigationCoordinator.PathNavigateRequested += (path, forceNewTab) =>
            {
                System.Diagnostics.Debug.WriteLine($"[PathNavigateRequested] path={path}, forceNewTab={forceNewTab}");
                if (forceNewTab)
                {
                    System.Diagnostics.Debug.WriteLine($"[PathNavigateRequested] 创建新标签页");
                    CreateTab(path, true);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[PathNavigateRequested] 调用NavigateToPath");
                    NavigateToPath(path);
                }
            };
            _navigationCoordinator.LibraryNavigateRequested += (library, forceNewTab) =>
            {
                OpenLibraryInTab(library, forceNewTab);
            };
            _navigationCoordinator.TagNavigateRequested += (tag, forceNewTab) =>
            {
                OpenTagInTab(tag, forceNewTab);
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
                    MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            _navigationCoordinator.FavoritePathNotFound += (favorite) =>
            {
                var result = MessageBox.Show(
                    $"路径不存在: {favorite.Path}\n\n是否从收藏中移除？",
                    "提示",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    DatabaseManager.RemoveFavorite(favorite.Path);
                    _favoriteService.LoadFavorites(FavoritesListBox);
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
                NavigationPanelControl.DrivesListBoxPreviewMouseDown += DrivesListBox_PreviewMouseDown;
                NavigationPanelControl.QuickAccessListBoxPreviewMouseDown += QuickAccessListBox_PreviewMouseDown;
                NavigationPanelControl.FavoritesListBoxPreviewMouseDown += FavoritesListBox_PreviewMouseDown;
                NavigationPanelControl.LibrariesListBoxSelectionChanged += LibrariesListBox_SelectionChanged;
                NavigationPanelControl.LibrariesListBoxContextMenuOpening += LibrariesListBox_ContextMenuOpening;
                NavigationPanelControl.AddFavoriteClick += AddFavorite_Click;
                NavigationPanelControl.AddTagToFileClick += AddTagToFile_Click;
                NavigationPanelControl.LibraryManageClick += ManageLibraries_Click;
                // NavigationPanelControl.LibraryRefreshClick += LibraryRefresh_Click;
                NavigationPanelControl.TagClickModeClick += TagClickModeBtn_Click;
                NavigationPanelControl.TagCategoryManageClick += TagCategoryManageBtn_Click;
                NavigationPanelControl.TagBrowsePanelTagClicked += TagBrowsePanel_TagClicked;
                NavigationPanelControl.TagEditPanelTagClicked += TagEditPanel_TagClicked;
            }

            // 订阅 FileBrowser 事件
            if (FileBrowser != null)
            {
                FileBrowser.FileAddTag += AddTagToFile_Click;
            }

            // 使用 MainWindowInitializer 进行初始化
            var initializer = new Services.MainWindowInitializer(this);
            initializer.InitializeApplication();
            this.Activated += (s, e) =>
            {
                var activeTab = _tabService.ActiveTab;
                if (activeTab != null && activeTab.Path != null && activeTab.Path.StartsWith("search://"))
                {
                    CheckAndRefreshSearchTab(activeTab.Path);
                }
            };
        }

        private void AttachTabServiceUiContext()
        {
            if (_tabService == null) return;
            var context = new TabUiContext
            {
                FileBrowser = FileBrowser,

                Dispatcher = this.Dispatcher,
                OwnerWindow = this,
                GetConfig = () => _configService?.Config ?? new AppConfig(),
                SaveConfig = ConfigManager.Save,
                GetCurrentLibrary = () => _currentLibrary,
                SetCurrentLibrary = lib => _currentLibrary = lib,
                GetCurrentPath = () => _currentPath,
                SetCurrentPath = path => _currentPath = path,
                SetNavigationCurrentPath = path => _navigationService.CurrentPath = path,
                GetCurrentTagFilter = () => _currentTagFilter,
                SetCurrentTagFilter = tag => _currentTagFilter = tag,
                FilterByTag = FilterByTag,
                LoadLibraryFiles = lib => LoadLibraryFiles(lib),
                NavigateToPathInternal = NavigateToPathInternal,
                UpdateNavigationButtonsState = UpdateNavigationButtonsState,

                SearchService = _searchService,
                GetSearchCacheService = () => _searchCacheService,
                GetSearchOptions = () => _searchOptions,
                GetCurrentFiles = () => _currentFiles,
                SetCurrentFiles = files => _currentFiles = files,
                ClearFilter = ClearFilter,
                RefreshSearchTab = path => { CheckAndRefreshSearchTab(path); return Task.CompletedTask; },
                FindResource = key => FindResource(key),
                IsTagTrainAvailable = () => App.IsTagTrainAvailable,

                // 获取当前导航模式
                GetCurrentNavigationMode = () => _configService?.Config?.LastNavigationMode ?? "Path"
            };
            _tabService.AttachUiContext(context);
        }
    }
}
