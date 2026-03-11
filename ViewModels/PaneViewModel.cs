using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using YiboFile.Models;
using YiboFile.Services;
using YiboFile.Services.Core;
using YiboFile.Services.Core.Error;
using YiboFile.Services.FileList;
using YiboFile.Services.Favorite;
using YiboFile.Services.Config;
using YiboFile.Services.Navigation;
using YiboFile.Services.Search;
using YiboFile.Services.Features;
using YiboFile.Services.ColumnManagement;
using YiboFile.Controllers;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Models.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.ViewModels
{
    public class PaneViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        private readonly IMessageBus _messageBus;
        private readonly Dispatcher _dispatcher;
        private bool _isSecondary;
        private readonly NavigationService _navigationService;
        private bool _isInitializing = true; // 防止构造期间触发文件加载

        private string _currentPath;
        private string _navigationMode = "Path"; // Path, Library, Tag, Search
        private Library _currentLibrary;
        private TagViewModel _currentTag;

        private YiboFile.Models.Enums.FileListViewMode _fileViewMode = YiboFile.Models.Enums.FileListViewMode.List; // List, Grid, LargeIcon
        private bool _isLoading;
        private bool _isLoadingDisabled;
        private string _statusText = "准备就绪";

        private readonly SearchCoordinator _searchCoordinator;
        private readonly SearchFilterService _searchFilterService;
        private readonly ITagService _tagService;
        private readonly LibraryService _libraryService;

        private readonly ObservableCollection<ContextMenuItemViewModel> _libraryMenuItems = new ObservableCollection<ContextMenuItemViewModel>();
        private readonly ObservableCollection<ContextMenuItemViewModel> _tagMenuItems = new ObservableCollection<ContextMenuItemViewModel>();
        private readonly ObservableCollection<ContextMenuItemViewModel> _favoriteMenuItems = new ObservableCollection<ContextMenuItemViewModel>();

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Properties

        public PaneId MyPaneId => _isSecondary ? PaneId.Second : PaneId.Main;

        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                if (_currentPath != value)
                {
                    _currentPath = value;
                    OnPropertyChanged(nameof(CurrentPath));

                    // Update NavigationMode and clear context based on protocol
                    if (value != null && value.StartsWith("lib://"))
                    {
                        NavigationMode = "Library";
                        CurrentTag = null;

                        string libName = value.Substring(6);
                        if (CurrentLibrary == null || CurrentLibrary.Name != libName)
                        {
                            var libs = _libraryService?.GetAllLibraries();
                            CurrentLibrary = libs?.FirstOrDefault(l => l.Name == libName);
                        }
                    }
                    else if (value != null && value.StartsWith("tag://"))
                    {
                        NavigationMode = "Tag";
                        CurrentLibrary = null;
                    }
                    else if (value != null && value.StartsWith("search://"))
                    {
                        NavigationMode = "Search";
                        CurrentLibrary = null;
                        CurrentTag = null;
                    }
                    else
                    {
                        NavigationMode = "Path";
                        // Fix BUG-018: Clear context to avoid stale info in panel
                        CurrentLibrary = null;
                        CurrentTag = null;
                    }

                    // No history management here (Moved to NavigationService)

                    // 初始化阶段不触发文件加载，由标签页恢复流程统一驱动
                    if (!_isInitializing)
                    {
                        RequestRefresh();
                    }
                    // No PathChangedMessage publishing here (Service handles it)
                }
            }
        }

        public string NavigationMode
        {
            get => _navigationMode;
            set
            {
                if (_navigationMode != value)
                {
                    _navigationMode = value;
                    OnPropertyChanged(nameof(NavigationMode));
                }
            }
        }

        public Library CurrentLibrary
        {
            get => _currentLibrary;
            set { if (_currentLibrary != value) { _currentLibrary = value; OnPropertyChanged(nameof(CurrentLibrary)); } }
        }

        public TagViewModel CurrentTag
        {
            get => _currentTag;
            set { if (_currentTag != value) { _currentTag = value; OnPropertyChanged(nameof(CurrentTag)); } }
        }

        public ObservableCollection<FileSystemItem> Files
        {
            get => FileList?.Files;
            set
            {
                if (FileList != null) FileList.Files = value;
                OnPropertyChanged(nameof(Files));
            }
        }

        public YiboFile.Models.Enums.FileListViewMode FileViewMode
        {
            get => _fileViewMode;
            set
            {
                if (_fileViewMode != value)
                {
                    _fileViewMode = value;
                    OnPropertyChanged(nameof(FileViewMode));
                    OnPropertyChanged(nameof(ViewModeIcon));

                    // Persist the change
                    ConfigurationService.Instance.Set(cfg => cfg.FileViewMode, value);

                    _messageBus.Publish(new ViewModeChangedMessage(value, MyPaneId));
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { if (_isLoading != value) { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); } }
        }

        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value) { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
            }
        }

        public bool IsLoadingDisabled { get => _isLoadingDisabled; set => _isLoadingDisabled = value; }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged(nameof(IsActive));
                }
            }
        }

        private bool _isInnerPreviewVisible = true;
        public bool IsInnerPreviewVisible
        {
            get => _isInnerPreviewVisible;
            set
            {
                if (_isInnerPreviewVisible != value)
                {
                    _isInnerPreviewVisible = value;
                    OnPropertyChanged(nameof(IsInnerPreviewVisible));
                }
            }
        }

        public SelectionViewModel Selection { get; private set; }

        internal ObservableCollection<FileSystemItem> SelectedItems => Selection?.SelectedItems;
        internal FileSystemItem SelectedItem => Selection?.SelectedItem;

        public void UpdateSelection(System.Collections.IList items)
        {
            if (Selection != null)
            {
                Selection.UpdateSelection(items, CurrentPath);
                Commands?.NotifyCommandStatesChanged();
            }
        }

        public bool CanNavigateBack => _navigationService?.CanNavigateBackFor(MyPaneId) ?? false;
        public bool CanNavigateForward => _navigationService?.CanNavigateForwardFor(MyPaneId) ?? false;
        public bool CanNavigateUp => !string.IsNullOrEmpty(CurrentPath) && CurrentPath != "Home";

        public IEnumerable<string> BackStack => _navigationService?.GetBackStack(MyPaneId) ?? Enumerable.Empty<string>();
        public IEnumerable<string> ForwardStack => _navigationService?.GetForwardStack(MyPaneId) ?? Enumerable.Empty<string>();

        public FileListViewModel FileList { get; private set; }
        public bool IsSecondary
        {
            get => _isSecondary;
            set
            {
                if (_isSecondary != value)
                {
                    _isSecondary = value;
                    OnPropertyChanged(nameof(IsSecondary));
                }
            }
        }
        /// <summary>面板标识 ("A" / "B")，供 PaneState 索引</summary>
        public string PaneLabel { get; }
        public IMessageBus MessageBus => _messageBus;
        public void RequestActivation() => _messageBus.Publish(new SetFocusedPaneMessage(_isSecondary));

        public SearchViewModel Search { get; }
        public Previews.PanePreviewViewModel Preview { get; private set; }
        public FilterViewModel Filter { get; private set; }

        public PaneCommandSet Commands { get; private set; }
        public PaneMenuViewModel Menu { get; private set; }

        public ObservableCollection<ContextMenuItemViewModel> LibraryMenuItems => Menu?.LibraryMenuItems;
        public ObservableCollection<ContextMenuItemViewModel> FavoriteMenuItems => Menu?.FavoriteMenuItems;
        public ObservableCollection<ContextMenuItemViewModel> TagMenuItems => Menu?.TagMenuItems;

        public bool IsAddressReadOnly => false;
        public bool IsPropertiesButtonVisible => true;

        public string ViewModeIcon
        {
            get
            {
                return FileViewMode switch
                {
                    YiboFile.Models.Enums.FileListViewMode.List => "\uE8C6",       // ViewList
                    YiboFile.Models.Enums.FileListViewMode.Compact => "\uF0E2",    // ViewCompact
                    YiboFile.Models.Enums.FileListViewMode.Thumbnail => "\uE8B9",  // ViewThumbnails
                    YiboFile.Models.Enums.FileListViewMode.Tiles => "\uE8CA",      // ViewTiles
                    YiboFile.Models.Enums.FileListViewMode.SmallIcons => "\uE80A", // ViewSmallIcons
                    YiboFile.Models.Enums.FileListViewMode.Content => "\uE8C4",    // ViewContent
                    _ => "\uE8C6"
                };
            }
        }

        #endregion

        #region Constructor

        public PaneViewModel(Dispatcher dispatcher, IMessageBus messageBus, bool isSecondary = false)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _isSecondary = isSecondary;
            PaneLabel = isSecondary ? "B" : "A";

            _navigationService = App.ServiceProvider.GetService<NavigationService>();

            Selection = new SelectionViewModel(_messageBus, isSecondary);
            Menu = new PaneMenuViewModel(this, _messageBus);
            Commands = new PaneCommandSet(this, _messageBus);
            Preview = new Previews.PanePreviewViewModel(_messageBus, ConfigurationService.Instance, MyPaneId);
            
            // Fix initial state logic: if the Preview pane is visible, the inner preview should be hidden
            _isInnerPreviewVisible = !Preview.IsVisible;

            Filter = new FilterViewModel(_messageBus,
                App.ServiceProvider?.GetService<SearchService>(),
                App.ServiceProvider?.GetService<SearchCacheService>());

            Filter.FilterChanged += (s, e) => ApplyFilter();
            Filter.MoreResultsLoaded += (s, newFiles) =>
            {
                _dispatcher.Invoke(() =>
                {
                    if (FileList?.Files != null)
                        foreach (var item in newFiles) FileList.Files.Add(item);
                });
            };

            // Messages
            _messageBus.Subscribe<SearchOptionsChangedMessage>(OnSearchOptionsChanged);
            _messageBus.Subscribe<SearchResultUpdatedMessage>(OnSearchResultUpdated);
            // _messageBus.Subscribe<Messaging.Messages.FocusedPaneChangedMessage>(OnFocusedPaneChanged);

            _messageBus.Subscribe<RefreshFileListMessage>(OnRefreshFileList);
            _messageBus.Subscribe<LibrarySelectedMessage>(OnLibrarySelected);
            _messageBus.Subscribe<FileSelectionChangedMessage>(OnFileSelectionChanged);
            _messageBus.Subscribe<LibraryFilesLoadedMessage>(OnLibraryFilesLoaded);

            // New Navigation Handling
            _messageBus.Subscribe<NavigationCompleteMessage>(OnNavigationComplete);
            _messageBus.Subscribe<Messaging.Messages.RestoreNavigationStateMessage>(OnRestoreNavigationState);

            // 订阅库和标签变更
            _messageBus.Subscribe<LibraryListChangedMessage>(OnLibraryListChanged);
            _messageBus.Subscribe<Messaging.Messages.FocusedPaneChangedMessage>(msg => IsActive = (msg.IsSecondPaneFocused == _isSecondary));

            _searchFilterService = App.ServiceProvider?.GetService<SearchFilterService>();
            var errorService = App.ServiceProvider?.GetService<ErrorService>();
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            _libraryService = App.ServiceProvider?.GetService<LibraryService>();

            var columnService = App.ServiceProvider?.GetService<ColumnService>();
            FileList = new FileListViewModel(_messageBus, isSecondary ? YiboFile.Services.Navigation.PaneId.Second : YiboFile.Services.Navigation.PaneId.Main, columnService);

            // Sync with FileListViewModel
            FileList.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileList.IsLoading)) IsLoading = FileList.IsLoading;
                if (e.PropertyName == nameof(FileList.Files)) OnPropertyChanged(nameof(Files));
            };

            Search = new SearchViewModel(_messageBus);
            _searchCoordinator = new SearchCoordinator(_messageBus, Search);
            _searchCoordinator.SetTargetPane(isSecondary ? "Secondary" : "Primary");

            _fileViewMode = ConfigurationService.Instance.Get(cfg => cfg.FileViewMode);

            // 仅预设路径值，不触发文件加载
            // 标签页恢复 (RestoreTabsState → SwitchToTab → RestoreNavigationStateMessage) 会在稍后设置正确路径并触发加载
            if (_navigationService != null)
            {
                var initial = _navigationService.GetCurrentPath(MyPaneId);
                if (!string.IsNullOrEmpty(initial)) _currentPath = initial;
                else _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
            else
            {
                _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            // 构造完成，允许后续路径变更触发文件加载
            _isInitializing = false;
        }

        #endregion

        #region Internal Executes

        internal void ExecuteSwitchViewMode(YiboFile.Models.Enums.FileListViewMode mode) { FileViewMode = mode; }

        // Navigation executes removed - handled by Commands/MessageBus direct calls or CommandSet updates
        // We will update PaneCommandSet separately, but for safety due to lingering references?
        // No, I'll update PaneCommandSet next.
        // I will keep the methods but change them to publish messages, to keep PaneCommandSet signature valid until updated.

        #endregion

        #region Private Logic

        public void Refresh() => RequestRefresh();

        private void RequestRefresh()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            if (NavigationMode == "Library" && CurrentLibrary != null) LoadLibraryAsync(CurrentLibrary);
            else if (NavigationMode == "Tag") LoadTagAsync(CurrentPath);
            else LoadPathAsync(CurrentPath);
        }

        public void NavigateTo(string path)
        {
            // Delegate logic to Module via Message
            if (!string.IsNullOrEmpty(path))
                _messageBus.Publish(new NavigateToPathMessage(path, true, MyPaneId));
        }

        private async void LoadPathAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (FileList != null) await FileList.LoadPathAsync(path);
                Menu?.UpdateDynamicMenuItems();
            }
            catch (TaskCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception)
            {
                // Silent catch or use formal logger
            }
        }

        private void LoadLibraryAsync(Library lib)
        {
            if (IsLoading || lib == null) return;
            if (_libraryService == null) return;

            IsLoading = true;
            StatusText = "加载库数据...";
            _libraryService.LoadLibraryFiles(lib, targetPane: _isSecondary ? PaneId.Second : PaneId.Main);
        }

        private async void LoadTagAsync(string pathOrInfo)
        {
            if (string.IsNullOrEmpty(pathOrInfo)) return;

            // Fix BUG-018: Ensure context is set for Tags
            string identifier = pathOrInfo;
            if (pathOrInfo.StartsWith("tag://"))
                identifier = pathOrInfo.Substring(6);

            // Resolve CurrentTag if context is missing or mismatch
            if (CurrentTag == null || (CurrentTag.Name != identifier && CurrentTag.Id.ToString() != identifier))
            {
                if (_tagService != null)
                {
                    try
                    {
                        var tags = await _tagService.GetAllTagsAsync();
                        var match = tags.FirstOrDefault(t => t.Name == identifier || t.Id.ToString() == identifier);
                        if (match != null)
                        {
                            CurrentTag = new TagViewModel
                            {
                                Id = match.Id,
                                Name = match.Name,
                                Color = match.Color
                            };
                        }
                    }
                    catch (Exception)
                    {

                    }
                }
            }

            // Ensure path has protocol
            string fullPath = pathOrInfo.StartsWith("tag://") ? pathOrInfo : $"tag://{pathOrInfo}";

            if (FileList != null) await FileList.LoadPathAsync(fullPath);
            Menu?.UpdateDynamicMenuItems();
        }

        #endregion

        #region Messages

        private void OnSearchResultUpdated(SearchResultUpdatedMessage message)
        {
            if (message.TargetPaneId == "Any" || (_isSecondary && message.TargetPaneId == "Secondary") || (!_isSecondary && message.TargetPaneId == "Primary"))
            {
                // Only update if we are still in Search mode
                if (NavigationMode == "Search")
                {
                    FileList?.SetFiles(message.Results);
                }
            }
        }

        private void OnSearchOptionsChanged(SearchOptionsChangedMessage message)
        {
            if (message.TargetPaneId == "Any" || (_isSecondary && message.TargetPaneId == "Secondary") || (!_isSecondary && message.TargetPaneId == "Primary"))
            {
                if (Filter != null) Filter.SearchOptions = message.Options;
            }
        }

        private void ApplyFilter()
        {
            if (FileList == null) return;
            if (Filter == null || !Filter.IsFilterActive) { FileList.ClearFilter(); return; }
            if (_searchFilterService != null) FileList.ApplyFilter(item => _searchFilterService.MatchesOptions(item, Filter.SearchOptions));
        }

        // 已废弃：焦点状态由 MainWindowViewModel.ActivePane 属性统一调度，防止交换面板时焦点错位
        // private void OnFocusedPaneChanged(Messaging.Messages.FocusedPaneChangedMessage message) { IsActive = (message.IsSecondPaneFocused == _isSecondary); OnPropertyChanged(nameof(IsActive)); }

        private void OnRefreshFileList(RefreshFileListMessage msg)
        {
            // 路径为空（全局刷新请求）时，必须匹配面板ID才执行
            if (string.IsNullOrEmpty(msg.Path))
            {
                if (msg.Pane == MyPaneId)
                    RequestRefresh();
                return;
            }

            // 有具体路径时：基于路径匹配决定刷新（不限制面板ID）
            // 因为文件操作（复制/删除）可能影响任意面板
            if (string.Equals(CurrentPath, msg.Path, StringComparison.OrdinalIgnoreCase))
            {
                RequestRefresh();
                return;
            }

            if (NavigationMode == "Library" && CurrentLibrary != null && CurrentLibrary.Paths != null)
            {
                if (CurrentLibrary.Paths.Any(libPath =>
                    msg.Path.StartsWith(libPath, StringComparison.OrdinalIgnoreCase) ||
                    libPath.StartsWith(msg.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    RequestRefresh();
                }
            }
        }
        private void OnLibrarySelected(LibrarySelectedMessage msg)
        {
            if (msg.Library != null)
            {
                // Fix BUG-019: Prevent simultaneous navigation by checking target pane
                // If pane is specified, match it. If null, only active pane should respond.
                if (msg.Pane != null && msg.Pane != MyPaneId) return;
                if (msg.Pane == null && !IsActive) return;

                // Use NavigateTo (which sends message)
                NavigateTo($"lib://{msg.Library.Name}");
            }
        }

        private void OnFileSelectionChanged(FileSelectionChangedMessage msg)
        {
            if (msg.Pane == (_isSecondary ? PaneId.Second : PaneId.Main))
            {
                Commands?.NotifyCommandStatesChanged();

                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                        Menu?.UpdateDynamicMenuItems();
                    }
                    catch { }
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void OnNavigationComplete(NavigationCompleteMessage msg)
        {
            if (msg.Pane == MyPaneId)
            {
                CurrentPath = msg.Path;

                OnPropertyChanged(nameof(CanNavigateBack));
                OnPropertyChanged(nameof(CanNavigateForward));
                OnPropertyChanged(nameof(BackStack));
                OnPropertyChanged(nameof(ForwardStack));

                Commands?.NotifyCommandStatesChanged();
            }
        }

        /// <summary>
        /// 处理标签页切换时的导航状态恢复
        /// 由 TabsModule.OnActiveTabChanged 发布，用于在切换 Tab 时重新加载对应路径的文件列表
        /// </summary>
        private void OnRestoreNavigationState(Messaging.Messages.RestoreNavigationStateMessage msg)
        {
            if (msg.Pane == MyPaneId)
            {
                bool pathChanged = !string.Equals(CurrentPath, msg.Path, System.StringComparison.OrdinalIgnoreCase);
                bool listEmpty = FileList?.Files == null || FileList.Files.Count == 0;

                if (pathChanged)
                {
                    // 路径变化 → 设 CurrentPath 触发 RequestRefresh
                    CurrentPath = msg.Path;
                }
                else if (listEmpty)
                {
                    // 路径相同但列表为空（启动首次加载被跳过） → 强制刷新
                    RequestRefresh();
                }

                OnPropertyChanged(nameof(CanNavigateBack));
                OnPropertyChanged(nameof(CanNavigateForward));
                OnPropertyChanged(nameof(BackStack));
                OnPropertyChanged(nameof(ForwardStack));

                Commands?.NotifyCommandStatesChanged();
            }
        }

        private void OnLibraryListChanged(LibraryListChangedMessage message)
        {
            // 如果当前在库模式但库对象丢失（通常发生在启动初期或库被重命名），尝试重新解析
            if (NavigationMode == "Library" && CurrentLibrary == null && !string.IsNullOrEmpty(CurrentPath))
            {
                string libPrefix = "lib://";
                if (CurrentPath.StartsWith(libPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string libName = CurrentPath.Substring(libPrefix.Length).Split('/')[0];
                    var lib = _libraryService?.GetAllLibraries()?.FirstOrDefault(l => string.Equals(l.Name, libName, StringComparison.OrdinalIgnoreCase));
                    if (lib != null)
                    {
                        CurrentLibrary = lib;
                        RequestRefresh();
                    }
                }
            }
        }

        private void OnLibraryFilesLoaded(LibraryFilesLoadedMessage message)
        {
            // 严格检查：仅当模式匹配且库ID匹配且面板ID匹配时才更新
            if (NavigationMode == "Library" && CurrentLibrary != null && message.Library != null && 
                message.Library.Id == CurrentLibrary.Id && message.TargetPane == MyPaneId)
            {
                FileList?.SetFiles(message.Files);
                IsLoading = false;
                StatusText = $"已加载 {message.Files.Count} 个项";
                Menu?.UpdateDynamicMenuItems();
            }
        }

        #endregion

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public void Dispose()
        {
            Menu?.Dispose();
        }
    }
}
