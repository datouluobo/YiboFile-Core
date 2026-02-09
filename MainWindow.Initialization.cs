using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;


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
            // Global mouse down logic is now handled within individual controls (FileBrowserControl)

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
            // SERVICE INITIALIZATION MIGRATED TO WINDOWORCHESTRATOR
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
                NavigationPanelControl.QuickAccessListBoxSelectionChanged += QuickAccessListBox_SelectionChanged; // [FIX] Left click nav
                NavigationPanelControl.FavoriteListBoxPreviewMouseDown += OnFavoriteListBoxPreviewMouseDown;
                NavigationPanelControl.FavoriteListBoxSelectionChanged += OnFavoriteListBoxSelectionChanged; // [FIX] Left click nav
                NavigationPanelControl.FavoriteListBoxLoaded += OnFavoriteListBoxLoaded;
                NavigationPanelControl.RenameFavoriteGroupRequested += OnRenameFavoriteGroupRequested;
                NavigationPanelControl.DeleteFavoriteGroupRequested += OnDeleteFavoriteGroupRequested;
                NavigationPanelControl.LibrariesListBoxSelectionChanged += LibrariesListBox_SelectionChanged; // [FIX] Library selection nav
                NavigationPanelControl.LibrariesListBoxContextMenuOpening += LibrariesListBox_ContextMenuOpening;
                NavigationPanelControl.LibraryContextMenuClick += LibraryContextMenu_Click;
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
                FileBrowser.ViewModeChanged += FileBrowser_ViewModeChanged;

                // [FIX] 显式绑定路径变更事件，确保主面板导航正确
                FileBrowser.PathChanged += (s, path) => NavigateToPath(path, Services.Navigation.PaneId.Main);
                FileBrowser.BreadcrumbClicked += (s, path) => NavigateToPath(path, Services.Navigation.PaneId.Main);
            }



            // 初始化主题切换事件
            InitializeThemeEvents();

            // 订阅分割器折叠事件，动态调整标签页边距
            if (SplitterRight != null)
            {
                SplitterRight.CollapsedStateChanged += (s, e) => UpdateTabManagerMargin();
            }
        }

        internal void InitializeServiceEvents()
        {
            if (_navigationService != null)
                _navigationService.NavigateRequested += OnNavigationServiceNavigateRequested;

            if (_tabService != null)
            {
                _tabService.TabAdded += (s, tab) => { /* UI 已通过 CreateTabInternal 处理 */ };
                _tabService.TabRemoved += (s, tab) => { /* UI 已通过 CloseTab 处理 */ };
                _tabService.ActiveTabChanged += (s, tab) =>
                {
                    if (tab != null)
                    {
                        // 同步 MainWindow 的状态以确保 SelectionEventHandler 使用正确的上下文
                        _currentPath = tab.Path;
                        _currentLibrary = tab.Library;

                        if (NavigationPanelControl != null) NavigationPanelControl.CurrentPath = tab.Path;

                        UpdateTabStyles();

                        // 切换标签页时自动刷新信息面板（处理空选状态）
                        _viewModel?.SelectionHandler?.HandleNoSelection();

                        // 切换标签页时自动聚焦主文件列表
                        if (IsDualListMode && IsSecondPaneFocused)
                        {
                            _layoutModule?.SetFocusedPane(false);
                            FileBrowser?.FilesList?.Focus();
                        }
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
            }

            if (_secondTabService != null)
            {
                _secondTabService.ActiveTabChanged += (s, tab) =>
                {
                    if (tab != null)
                    {
                        // 1. 同步 ViewModel 状态
                        _viewModel?.SecondaryPane?.NavigateTo(tab.Path);

                        // 2. 监听属性变更
                        // 先移除旧的监听以防泄漏（虽然此处是 lambda 捕获，但 tab 实例会变）
                        // 为简单起见，我们统一在 OnActiveTabPropertyChanged 中处理，需要确保事件源区分
                        tab.PropertyChanged -= OnActiveTabPropertyChanged;
                        tab.PropertyChanged += OnActiveTabPropertyChanged;

                        // 3. 切换标签页时自动聚焦副文件列表
                        if (IsDualListMode && !IsSecondPaneFocused)
                        {
                            _layoutModule?.SetFocusedPane(true);
                            SecondFileBrowser?.FilesList?.Focus();
                        }
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
            }

            // 订阅 FileListService 事件
            if (_fileListService != null)
            {
                _fileListService.FolderSizeCalculated += OnFileListServiceFolderSizeCalculated;
                _fileListService.MetadataEnriched += OnFileListServiceMetadataEnriched;
            }

            // 订阅副文件列表服务事件
            if (_secondFileListService != null)
            {
                _secondFileListService.FolderSizeCalculated += OnFileListServiceFolderSizeCalculated;
                _secondFileListService.MetadataEnriched += OnFileListServiceMetadataEnriched;
            }

            // 订阅 FileSystemWatcherService 事件
            if (_fileSystemWatcherService != null)
            {
                _fileSystemWatcherService.FileSystemChanged += OnFileSystemWatcherServiceFileSystemChanged;
                _fileSystemWatcherService.RefreshRequested += OnFileSystemWatcherServiceRefreshRequested;
            }

            // 订阅库服务事件
            if (_libraryService != null)
            {
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
                        if (e.TargetPane == PaneId.Second)
                        {
                            if (SecondFileBrowser != null)
                            {
                                _viewModel?.SecondaryPane?.FileList?.Files?.Clear();
                                SecondFileBrowser.AddressText = e.Library.Name + " (无位置)";
                                SecondFileBrowser.SetLibraryBreadcrumb(e.Library.Name);
                                SecondFileBrowser.ShowEmptyState($"库 \"{e.Library.Name}\" 中没有文件或文件夹");
                            }
                        }
                        else
                        {
                            _currentFiles.Clear();
                            if (FileBrowser != null)
                            {
                                _viewModel?.PrimaryPane?.FileList?.Files?.Clear();
                                FileBrowser.AddressText = e.Library.Name + " (无位置)";
                                FileBrowser.SetLibraryBreadcrumb(e.Library.Name);
                                FileBrowser.ShowEmptyState($"库 \"{e.Library.Name}\" 中没有文件或文件夹");
                            }
                        }
                    }
                    else
                    {
                        ShowMergedLibraryFiles(e.Files, e.Library, e.TargetPane);
                    }
                };

                _libraryService.LibraryHighlightRequested += (s, library) =>
                {
                    HighlightMatchingLibrary(library);
                };
            }

            // 订阅收藏服务事件
            if (_favoriteService != null)
            {
                _favoriteService.NavigateRequested += (s, path) =>
                {
                    _navigationCoordinator?.HandlePathNavigation(path, NavigationSource.Favorite, ClickType.LeftClick);
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
                    _navigationCoordinator?.HandlePathNavigation(path, NavigationSource.Favorite, ClickType.LeftClick, forceNewTab: true);
                };
            }

            // 订阅快速访问服务事件
            if (_quickAccessService != null)
            {
                _quickAccessService.NavigateRequested += (s, path) =>
                {
                    _navigationCoordinator?.HandlePathNavigation(path, NavigationSource.QuickAccess, ClickType.LeftClick);
                };

                _quickAccessService.CreateTabRequested += (s, path) =>
                {
                    _navigationCoordinator?.HandlePathNavigation(path, NavigationSource.QuickAccess, ClickType.LeftClick, forceNewTab: true);
                };
            }

            if (_navigationCoordinator != null)
            {
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
                        _favoriteService?.RemoveFavorite(favorite.Path);
                    }
                };
            }

            // 恢复 Activated 事件订阅 (在服务初始化后)
            this.Activated += OnActivated;
        }

        internal void AttachTabServiceUiContext()
        {
            if (_tabService == null) return;
            var context = new TabUiContext
            {
                FileBrowser = FileBrowser,
                TabManager = TabManager,

                Dispatcher = this.Dispatcher,
                OwnerWindow = this,
                GetConfig = () => ConfigurationService.Instance.Config ?? new AppConfig(),
                SaveConfig = ConfigManager.Save,
                GetCurrentLibrary = () => _viewModel?.PrimaryPane?.CurrentLibrary,

                SetCurrentLibrary = lib =>
                {
                    if (_viewModel?.PrimaryPane != null)
                    {
                        _viewModel.PrimaryPane.NavigateTo(lib, loadData: false);
                    }
                },
                GetCurrentPath = () => _viewModel?.PrimaryPane?.CurrentPath,
                SetCurrentPath = path =>
                {
                    if (_viewModel?.PrimaryPane != null)
                    {
                        // Update VM state, but maybe loadData:false if caller handles loading?
                        // Usually TabService expects to just set the property.
                        _viewModel.PrimaryPane.CurrentPath = path;
                    }
                },
                SetNavigationCurrentPath = path =>
                {
                    if (_navigationService != null) _navigationService.CurrentPath = path;
                    if (_viewModel?.PrimaryPane != null) _viewModel.PrimaryPane.CurrentPath = path;
                },
                LoadLibraryFiles = lib =>
                {
                    if (_viewModel?.PrimaryPane != null)
                    {
                        _viewModel.PrimaryPane.NavigateTo(lib, loadData: true);
                    }
                },
                NavigateToPathInternal = NavigateToPathFromModule,
                UpdateNavigationButtonsState = UpdateNavigationButtonsState,

                SearchService = _searchService,
                GetSearchCacheService = () => _searchCacheService,
                GetSearchOptions = () => _searchOptions,
                GetCurrentFiles = () => _viewModel?.PrimaryPane?.FileList?.Files?.ToList() ?? new List<FileSystemItem>(),
                SetCurrentFiles = files =>
                {
                    _viewModel?.PrimaryPane?.FileList?.UpdateFiles(files);
                    // _currentFiles = files; // Legacy field
                },
                ClearFilter = ClearFilter,
                RefreshSearchTab = path => { CheckAndRefreshSearchTab(path); return Task.CompletedTask; },
                FindResource = key => FindResource(key),

                // 获取当前导航模式
                GetCurrentNavigationMode = () => _viewModel?.PrimaryPane?.NavigationMode ?? "Path",


                TagService = _tagService
            };
            _tabService.AttachUiContext(context);

            _tabService.ActiveTabChanged += (s, tab) => SyncUiWithActiveTab(tab);
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
                    // Ensure ViewModel is updated to Library mode so UI binds correctly
                    _viewModel?.PrimaryPane?.NavigateTo(tab.Library, loadData: false);
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

        private void OnActivated(object sender, EventArgs e)
        {
            var activeTab = _tabService?.ActiveTab;
            if (activeTab != null && activeTab.Path != null && activeTab.Path.StartsWith("search://"))
            {
                CheckAndRefreshSearchTab(activeTab.Path);
            }
        }

        private void OnActiveTabPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is PathTab tab)
            {
                // 主标签页变更
                if (tab == _tabService?.ActiveTab)
                {
                    if (e.PropertyName == nameof(PathTab.Path) || e.PropertyName == nameof(PathTab.Library))
                    {
                        SyncUiWithActiveTab(tab);
                    }
                }
                // 副标签页变更
                else if (tab == _secondTabService?.ActiveTab)
                {
                    if (e.PropertyName == nameof(PathTab.Path))
                    {
                        // 直接同步 ViewModel (副面板暂不支持库模式)
                        _viewModel?.SecondaryPane?.NavigateTo(tab.Path);
                    }
                }
            }
        }

        internal FileOperationContext GetActiveFileOperationContext()
        {
            // 修复：基于 ViewModel 的 ActivePane 状态来判断，而不是不稳定的控件焦点
            bool useSecond = _viewModel?.ActivePane == _viewModel?.SecondaryPane;

            var targetBrowser = useSecond ? SecondFileBrowser : FileBrowser;
            // 修复：使用 ViewModel 的 CurrentPath 而不是 AddressText
            // AddressText 可能是库名等非绝对路径，而 CurrentPath 才是真实的路径
            var targetPath = useSecond ? _viewModel?.SecondaryPane?.CurrentPath : _currentPath;

            // 解析副面板的库信息（如果副面板显示的是库）
            Library targetLibrary = null;
            if (useSecond)
            {
                // 从副面板的地址栏解析库
                if (!string.IsNullOrEmpty(targetPath) && targetPath.StartsWith("lib://", StringComparison.OrdinalIgnoreCase))
                {
                    var libName = targetPath.Substring(6);
                    var slashIndex = libName.IndexOf('/');
                    if (slashIndex > 0)
                    {
                        libName = libName.Substring(0, slashIndex);
                    }

                    // 从 LibraryService 获取库信息
                    targetLibrary = _libraryService?.GetAllLibraries()?.FirstOrDefault(l =>
                        string.Equals(l.Name, libName, StringComparison.OrdinalIgnoreCase));
                }
            }
            else
            {
                targetLibrary = _currentLibrary;
            }

            // 确保 targetPath 是绝对路径（修复相对路径问题）
            if (!string.IsNullOrEmpty(targetPath) && !targetPath.StartsWith("lib://", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Path.IsPathRooted(targetPath))
                    {
                        targetPath = Path.GetFullPath(targetPath);
                        System.Diagnostics.Debug.WriteLine($"[GetActiveFileOperationContext] Converted relative path to absolute: {targetPath}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GetActiveFileOperationContext] Path.GetFullPath failed for {targetPath}: {ex.Message}");
                }
            }

            return new FileOperationContext
            {
                TargetPath = targetPath,
                CurrentLibrary = targetLibrary,
                OwnerWindow = this,
                RefreshCallback = () =>
                {
                    if (useSecond)
                    {
                        // 只刷新副面板 - 使用 RefreshActiveFileList 确保正确刷新
                        RefreshActiveFileList();
                    }
                    else
                    {
                        // 只刷新主面板
                        RefreshFileList();
                    }
                }
            };
        }
    }
}

