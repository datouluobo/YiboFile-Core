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
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.ViewModels
{
    public class PaneViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Fields

        private readonly IMessageBus _messageBus;
        private readonly Dispatcher _dispatcher;
        private readonly bool _isSecondary;

        private string _currentPath;
        private string _navigationMode = "Path"; // Path, Library, Tag, Search
        private Library _currentLibrary;
        private TagViewModel _currentTag;

        private string _fileViewMode = "List"; // List, Grid, LargeIcon
        private bool _isLoading;
        private bool _isLoadingDisabled;
        private string _statusText = "准备就绪";

        private Stack<string> _backStack = new Stack<string>();
        private Stack<string> _forwardStack = new Stack<string>();
        private bool _isNavigatingHistory;

        private readonly SearchCoordinator _searchCoordinator;
        private readonly SearchFilterService _searchFilterService;
        private readonly ITagService _tagService;
        private readonly LibraryService _libraryService;
        private readonly FavoriteService _favoriteService;
        private readonly SearchService _searchService;
        private readonly SearchCacheService _searchCacheService;

        private readonly FolderSizeCalculationService _folderSizeService;

        private readonly ObservableCollection<ContextMenuItemViewModel> _libraryMenuItems = new ObservableCollection<ContextMenuItemViewModel>();
        private readonly ObservableCollection<ContextMenuItemViewModel> _tagMenuItems = new ObservableCollection<ContextMenuItemViewModel>();
        private readonly ObservableCollection<ContextMenuItemViewModel> _favoriteMenuItems = new ObservableCollection<ContextMenuItemViewModel>();

        #endregion

        #region Events

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Properties

        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                if (_currentPath != value)
                {
                    string oldPath = _currentPath;
                    _currentPath = value;
                    OnPropertyChanged(nameof(CurrentPath));

                    if (!_isNavigatingHistory)
                    {
                        if (value != null && value.StartsWith("lib://")) NavigationMode = "Library";
                        else if (value != null && value.StartsWith("tag://")) NavigationMode = "Tag";
                        else if (value != null && value.StartsWith("search://")) NavigationMode = "Search";
                        else NavigationMode = "Path";

                        if (!string.IsNullOrEmpty(oldPath))
                            _backStack.Push(oldPath);
                        _forwardStack.Clear();
                        OnPropertyChanged(nameof(CanNavigateBack));
                        OnPropertyChanged(nameof(CanNavigateForward));
                        Commands?.NotifyCommandStatesChanged();
                    }

                    RequestRefresh();
                    _messageBus.Publish(new PathChangedMessage(value, _isSecondary ? PaneId.Second : PaneId.Main));
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
                    _messageBus.Publish(new NavigationModeChangedMessage(value));
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

        public string FileViewMode
        {
            get => _fileViewMode;
            set
            {
                if (_fileViewMode != value)
                {
                    _fileViewMode = value;
                    OnPropertyChanged(nameof(FileViewMode));
                    _messageBus.Publish(new ViewModeChangedMessage(value, _isSecondary ? PaneId.Second : PaneId.Main));
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

        public bool IsActive { get; set; }

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

        public bool CanNavigateBack => _backStack.Count > 0;
        public bool CanNavigateForward => _forwardStack.Count > 0;
        public bool CanNavigateUp => !string.IsNullOrEmpty(CurrentPath) && CurrentPath != "Home";

        public FileListViewModel FileList { get; private set; }
        public bool IsSecondary => _isSecondary;
        public IMessageBus MessageBus => _messageBus;
        public void RequestActivation() => _messageBus.Publish(new SetFocusedPaneMessage(_isSecondary));

        public SearchViewModel Search { get; }
        public FilterViewModel Filter { get; private set; }

        public PaneCommandSet Commands { get; private set; }

        #region Forwarding Commands (Backward Compatibility)
        public ICommand RefreshCommand => Commands?.RefreshCommand;
        public ICommand NavigateBackCommand => Commands?.NavigateBackCommand;
        public ICommand NavigateForwardCommand => Commands?.NavigateForwardCommand;
        public ICommand NavigateUpCommand => Commands?.NavigateUpCommand;
        public ICommand NavigateHomeCommand => Commands?.NavigateHomeCommand;
        public ICommand OpenParentFolderCommand => Commands?.OpenParentFolderCommand;
        public ICommand SwitchViewModeCommand => Commands?.SwitchViewModeCommand;
        public ICommand SelectAllCommand => Commands?.SelectAllCommand;
        public ICommand PropertiesCommand => Commands?.PropertiesCommand;
        public ICommand NewFolderCommand => Commands?.NewFolderCommand;
        public ICommand NewFileCommand => Commands?.NewFileCommand;
        public ICommand DeleteCommand => Commands?.DeleteCommand;
        public ICommand CopyCommand => Commands?.CopyCommand;
        public ICommand CutCommand => Commands?.CutCommand;
        public ICommand PasteCommand => Commands?.PasteCommand;
        public ICommand RenameCommand => Commands?.RenameCommand;
        public ICommand UndoCommand => Commands?.UndoCommand;
        public ICommand RedoCommand => Commands?.RedoCommand;
        public ICommand ToggleLibraryCommand => Commands?.ToggleLibraryCommand;
        public ICommand AddToFavoriteCommand => Commands?.AddToFavoriteCommand;
        public ICommand ToggleTagCommand => Commands?.ToggleTagCommand;
        public ICommand NewLibraryCommand => Commands?.NewLibraryCommand;
        public ICommand NewFavoriteGroupCommand => Commands?.NewFavoriteGroupCommand;
        public ICommand NewTagCommand => Commands?.NewTagCommand;
        public ICommand ManageTagsCommand => Commands?.ManageTagsCommand;
        public ICommand BatchAddTagsCommand => Commands?.BatchAddTagsCommand;
        public ICommand TagStatisticsCommand => Commands?.TagStatisticsCommand;
        public ICommand LoadMoreCommand => Commands?.LoadMoreCommand;
        #endregion

        public PaneMenuViewModel Menu { get; private set; }

        public ObservableCollection<ContextMenuItemViewModel> LibraryMenuItems => Menu?.LibraryMenuItems;
        public ObservableCollection<ContextMenuItemViewModel> FavoriteMenuItems => Menu?.FavoriteMenuItems;
        public ObservableCollection<ContextMenuItemViewModel> TagMenuItems => Menu?.TagMenuItems;

        #endregion

        #region Constructor

        public PaneViewModel(Dispatcher dispatcher, IMessageBus messageBus, bool isSecondary = false)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _isSecondary = isSecondary;

            _folderSizeService = App.ServiceProvider.GetService(typeof(FolderSizeCalculationService)) as FolderSizeCalculationService;

            Selection = new SelectionViewModel(_messageBus, isSecondary);
            Menu = new PaneMenuViewModel(this, _messageBus);
            Commands = new PaneCommandSet(this, _messageBus);

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

            _messageBus.Subscribe<SearchOptionsChangedMessage>(OnSearchOptionsChanged);
            _messageBus.Subscribe<SearchResultUpdatedMessage>(OnSearchResultUpdated);
            _messageBus.Subscribe<Messaging.Messages.FocusedPaneChangedMessage>(OnFocusedPaneChanged);

            _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);
            _messageBus.Subscribe<FileTagsChangedMessage>(OnFileTagsChanged);
            // Dynamic Menu events moved to PaneMenuViewModel
            _messageBus.Subscribe<RefreshFileListMessage>(OnRefreshFileList);
            _messageBus.Subscribe<LibrarySelectedMessage>(OnLibrarySelected);
            _messageBus.Subscribe<FileSelectionChangedMessage>(OnFileSelectionChanged);
            _messageBus.Subscribe<NavigateToPathMessage>(OnNavigateToPath);
            // LibraryFilesLoaded 现在通过 C# 事件订阅，不通过消息总线

            _searchFilterService = App.ServiceProvider?.GetService<SearchFilterService>();
            var errorService = App.ServiceProvider?.GetService<ErrorService>();
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            _libraryService = App.ServiceProvider?.GetService<LibraryService>();
            _favoriteService = App.ServiceProvider?.GetService<FavoriteService>();
            _searchService = App.ServiceProvider?.GetService<SearchService>();
            _searchCacheService = App.ServiceProvider?.GetService<SearchCacheService>();

            var columnService = App.ServiceProvider?.GetService<ColumnService>();
            FileList = new FileListViewModel(_messageBus, isSecondary ? YiboFile.Services.Navigation.PaneId.Second : YiboFile.Services.Navigation.PaneId.Main, columnService);

            // Sync with FileListViewModel
            FileList.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileList.IsLoading)) IsLoading = FileList.IsLoading;
                if (e.PropertyName == nameof(FileList.Files)) OnPropertyChanged(nameof(Files));
            };

            if (_libraryService != null)
            {
                // LibrariesLoaded moved to PaneMenuViewModel
                _libraryService.LibraryFilesLoaded += OnLibraryFilesLoaded;
            }

            Search = new SearchViewModel(_messageBus);
            _searchCoordinator = new SearchCoordinator(_messageBus, Search);
            _searchCoordinator.SetTargetPane(isSecondary ? "Secondary" : "Primary");

            _fileViewMode = ConfigurationService.Instance.Get(cfg => cfg.FileViewMode) ?? "List";
        }

        #endregion

        #region Internal Executes

        internal void ExecuteSwitchViewMode(string mode) { if (!string.IsNullOrEmpty(mode)) FileViewMode = mode; }

        internal void ExecuteNavigateBack()
        {
            if (_backStack.Count == 0) return;
            _isNavigatingHistory = true;
            _forwardStack.Push(CurrentPath);
            var prev = _backStack.Pop();
            NavigateTo(prev);
            _isNavigatingHistory = false;
            OnPropertyChanged(nameof(CanNavigateBack));
            OnPropertyChanged(nameof(CanNavigateForward));
            Commands?.NotifyCommandStatesChanged();
        }

        internal void ExecuteNavigateForward()
        {
            if (_forwardStack.Count == 0) return;
            _isNavigatingHistory = true;
            _backStack.Push(CurrentPath);
            var next = _forwardStack.Pop();
            NavigateTo(next);
            _isNavigatingHistory = false;
            OnPropertyChanged(nameof(CanNavigateBack));
            OnPropertyChanged(nameof(CanNavigateForward));
            Commands?.NotifyCommandStatesChanged();
        }

        internal void ExecuteNavigateUp()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            string upPath = null;
            if (ProtocolManager.IsVirtual(CurrentPath))
            {
                int lastSlash = CurrentPath.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    var pathToCheck = CurrentPath.EndsWith("/") ? CurrentPath.Substring(0, CurrentPath.Length - 1) : CurrentPath;
                    lastSlash = pathToCheck.LastIndexOf('/');
                    if (lastSlash > 0) upPath = pathToCheck.Substring(0, lastSlash);
                }
                if (upPath == null && CurrentPath.Contains("|"))
                {
                    upPath = CurrentPath.Substring(CurrentPath.IndexOf("//") + 2);
                    if (upPath.Contains("|")) upPath = upPath.Substring(0, upPath.IndexOf("|"));
                }
            }
            else upPath = Path.GetDirectoryName(CurrentPath);

            if (!string.IsNullOrEmpty(upPath)) NavigateTo(upPath);
        }

        internal void ExecuteSelectAll() => _messageBus.Publish(new SelectAllRequestMessage(_isSecondary ? PaneId.Second : PaneId.Main));

        // Note: NewFolder, Delete, etc. are now handled directly in PaneCommandSet via MessageBus

        internal void ExecuteTagStatistics()
        {
            if (_tagService == null) return;
            try
            {
                var tags = _tagService.GetAllTags();
                var groups = _tagService.GetTagGroups();
                string stats = $"标签总数: {tags.Count()}\n标签分组: {groups.Count()}";
                MessageBox.Show(stats, "标签统计", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"获取统计失败: {ex.Message}"); }
        }

        #endregion

        #region Private Logic

        public void Refresh() => RequestRefresh();

        private void RequestRefresh()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            if (NavigationMode == "Library" && CurrentLibrary != null) LoadLibraryAsync(CurrentLibrary);
            else if (NavigationMode == "Tag" && CurrentTag != null) LoadTagAsync(CurrentTag.Id.ToString());
            else FileList?.RefreshFiles();
        }

        public void NavigateTo(string path) { if (!string.IsNullOrEmpty(path)) CurrentPath = path; }

        private async void LoadPathAsync(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            // Delegate all logic to FileListViewModel
            if (FileList != null) await FileList.LoadPathAsync(path);
            Menu?.UpdateDynamicMenuItems();
        }

        private void LoadLibraryAsync(Library lib)
        {
            if (IsLoading || lib == null) return;
            if (_libraryService == null) return;

            IsLoading = true;
            StatusText = "加载库数据...";
            _libraryService.LoadLibraryFiles(lib, targetPane: _isSecondary ? PaneId.Second : PaneId.Main);
        }

        private async void LoadTagAsync(string tagIdOrName)
        {
            if (string.IsNullOrEmpty(tagIdOrName)) return;
            if (FileList != null) await FileList.LoadPathAsync($"tag://{tagIdOrName}");
            Menu?.UpdateDynamicMenuItems();
        }

        #endregion

        #region Messages

        private void OnSearchResultUpdated(SearchResultUpdatedMessage message)
        {
            if (message.TargetPaneId == "Any" || (_isSecondary && message.TargetPaneId == "Secondary") || (!_isSecondary && message.TargetPaneId == "Primary"))
            {
                FileList?.UpdateFiles(message.Results);
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

        private void OnFocusedPaneChanged(Messaging.Messages.FocusedPaneChangedMessage message) { IsActive = (message.IsSecondPaneFocused == _isSecondary); OnPropertyChanged(nameof(IsActive)); }
        private void OnNotesUpdated(NotesUpdatedMessage msg) => RequestRefresh();
        private void OnFileTagsChanged(FileTagsChangedMessage msg) => RequestRefresh();
        // TagListChanged etc moved to Menu
        private void OnRefreshFileList(RefreshFileListMessage msg)
        {
            if (string.IsNullOrEmpty(msg.Path))
            {
                RequestRefresh();
                return;
            }

            if (string.Equals(CurrentPath, msg.Path, StringComparison.OrdinalIgnoreCase))
            {
                RequestRefresh();
                return;
            }

            // 支持库模式下的刷新
            if (NavigationMode == "Library" && CurrentLibrary != null && CurrentLibrary.Paths != null)
            {
                // 如果变更路径是库包含路径的子路径，则刷新
                if (CurrentLibrary.Paths.Any(libPath =>
                    msg.Path.StartsWith(libPath, StringComparison.OrdinalIgnoreCase) ||
                    libPath.StartsWith(msg.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    RequestRefresh();
                }
            }
        }
        private void OnLibrarySelected(LibrarySelectedMessage msg) { if (msg.Library != null) NavigateTo($"lib://{msg.Library.Name}"); }



        private void OnFileSelectionChanged(FileSelectionChangedMessage msg)
        {
            if (msg.Pane == (_isSecondary ? PaneId.Second : PaneId.Main))
            {
                Commands?.NotifyCommandStatesChanged();

                // Debounce menu updates to avoid freezing on rapid selection
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

        private void OnNavigateToPath(NavigateToPathMessage msg)
        {
            if (msg.Pane == null || msg.Pane == (_isSecondary ? PaneId.Second : PaneId.Main))
            {
                NavigateTo(msg.Path);
            }
        }

        private void OnLibraryFilesLoaded(object sender, LibraryFilesLoadedEventArgs e)
        {
            // 只有当是本面板请求时才处理
            if (e.TargetPane == (_isSecondary ? PaneId.Second : PaneId.Main))
            {
                FileList?.UpdateFiles(e.Files);
                _dispatcher.Invoke(() =>
                {
                    StatusText = $"库: {e.Library.Name} ({e.Files?.Count ?? 0} 项)";
                    IsLoading = false;
                });
                Menu?.UpdateDynamicMenuItems();
            }
        }

        // OnLibrariesLoaded moved to Menu

        #endregion

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public void Dispose()
        {
            if (_libraryService != null)
            {
                _libraryService.LibraryFilesLoaded -= OnLibraryFilesLoaded;
            }
            Menu?.Dispose();
        }
    }
}
