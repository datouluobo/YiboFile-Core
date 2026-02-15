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
        private ObservableCollection<FileSystemItem> _files;
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
        private readonly FileListService _fileListService;
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
            get => _files;
            set { _files = value; OnPropertyChanged(nameof(Files)); }
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

        public ObservableCollection<ContextMenuItemViewModel> LibraryMenuItems => _libraryMenuItems;
        public ObservableCollection<ContextMenuItemViewModel> FavoriteMenuItems => _favoriteMenuItems;
        public ObservableCollection<ContextMenuItemViewModel> TagMenuItems => _tagMenuItems;

        #endregion

        #region Constructor

        public PaneViewModel(Dispatcher dispatcher, IMessageBus messageBus, bool isSecondary = false)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _isSecondary = isSecondary;

            _files = new ObservableCollection<FileSystemItem>();
            _folderSizeService = App.ServiceProvider.GetService(typeof(FolderSizeCalculationService)) as FolderSizeCalculationService;

            Selection = new SelectionViewModel(_messageBus, isSecondary);
            Commands = new PaneCommandSet(this, _messageBus);

            Filter = new FilterViewModel(_messageBus,
                App.ServiceProvider?.GetService<SearchService>(),
                App.ServiceProvider?.GetService<SearchCacheService>());

            Filter.FilterChanged += (s, e) => ApplyFilter();
            Filter.MoreResultsLoaded += (s, newFiles) =>
            {
                _dispatcher.Invoke(() => { foreach (var item in newFiles) Files.Add(item); });
            };

            _messageBus.Subscribe<SearchOptionsChangedMessage>(OnSearchOptionsChanged);
            _messageBus.Subscribe<SearchResultUpdatedMessage>(OnSearchResultUpdated);
            _messageBus.Subscribe<Messaging.Messages.FocusedPaneChangedMessage>(OnFocusedPaneChanged);

            _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);
            _messageBus.Subscribe<FileTagsChangedMessage>(OnFileTagsChanged);
            _messageBus.Subscribe<TagListChangedMessage>(OnTagListChanged);
            _messageBus.Subscribe<LibraryListChangedMessage>(OnLibraryListChanged);
            _messageBus.Subscribe<FavoritesUpdatedMessage>(OnFavoritesUpdated);
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

            if (errorService != null) _fileListService = new FileListService(_dispatcher, errorService, _tagService);
            if (_libraryService != null)
            {
                _libraryService.LibrariesLoaded += OnLibrariesLoaded;
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

        internal void ExecuteShowProperties()
        {
            if (SelectedItem != null) _messageBus.Publish(new ShowPropertiesRequestMessage(SelectedItem, CurrentPath));
            else if (!string.IsNullOrEmpty(CurrentPath)) _messageBus.Publish(new ShowPropertiesRequestMessage(null, CurrentPath));
        }

        internal void ExecuteNewFolder() => _messageBus.Publish(new CreateFolderRequestMessage(CurrentPath));
        internal void ExecuteNewFile() => _messageBus.Publish(new CreateFileRequestMessage(CurrentPath));
        internal void ExecuteDelete() => _messageBus.Publish(new DeleteItemsRequestMessage(SelectedItems.ToList()));
        internal void ExecuteCopy() => _messageBus.Publish(new CopyItemsRequestMessage(SelectedItems.ToList()));
        internal void ExecuteCut() => _messageBus.Publish(new CutItemsRequestMessage(SelectedItems.ToList()));
        internal void ExecutePaste() => _messageBus.Publish(new PasteItemsRequestMessage(CurrentPath));
        internal void ExecuteRename() => _messageBus.Publish(new RenameItemRequestMessage(SelectedItem));
        internal void ExecuteUndo() => _messageBus.Publish(new UndoRequestMessage());
        internal void ExecuteRedo() => _messageBus.Publish(new RedoRequestMessage());

        internal void ExecuteToggleLibrary(Library library)
        {
            if (library == null || SelectedItems.Count == 0) return;
            _messageBus.Publish(new ToggleLibraryPathRequestMessage(library, SelectedItems.Select(i => i.Path).ToList()));
        }

        internal void ExecuteAddToFavorite(int groupId)
        {
            if (SelectedItems.Count == 0) return;
            _messageBus.Publish(new AddFavoriteRequestMessage(SelectedItems.ToList(), groupId));
        }

        internal void ExecuteNewLibrary()
        {
            var dialog = new YiboFile.Controls.Dialogs.InputDialog("新建库", "请输入库名称:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                _messageBus.Publish(new CreateLibraryRequestMessage(dialog.InputText, SelectedItems.Where(i => i.IsDirectory).Select(i => i.Path).ToList()));
            }
        }

        internal void ExecuteNewFavoriteGroup()
        {
            var inputName = YiboFile.DialogService.ShowInput("请输入新分组名称：", "新分组", "新建分组");
            if (!string.IsNullOrEmpty(inputName))
            {
                _messageBus.Publish(new CreateFavoriteGroupRequestMessage(inputName.Trim(), SelectedItems.ToList()));
            }
        }

        internal void ExecuteToggleTag(ITag tag)
        {
            if (tag == null || SelectedItems.Count == 0) return;
            _messageBus.Publish(new ToggleTagRequestMessage(tag.Id, SelectedItems.Select(i => i.Path).ToList()));
        }

        internal void ExecuteManageTags()
        {
            var dialog = new YiboFile.Controls.Dialogs.TagManagementDialog();
            if (Application.Current?.MainWindow != null) dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();
            _messageBus.Publish(new TagListChangedMessage());
            NotifyDynamicMenuItemsChanged();
        }

        internal void ExecuteBatchAddTags()
        {
            if (SelectedItems.Count == 0) return;
            var dialog = new YiboFile.Controls.Dialogs.TagSelectionDialog();
            if (Application.Current?.MainWindow != null) dialog.Owner = Application.Current.MainWindow;
            if (dialog.ShowDialog() == true) _messageBus.Publish(new AddTagToFilesRequestMessage(SelectedItems.Select(i => i.Path).ToList(), dialog.SelectedTagId));
        }

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

        internal void NotifyDynamicMenuItemsChanged() => UpdateDynamicMenuItems();

        #endregion

        #region Private Logic

        public void Refresh() => RequestRefresh();

        private void RequestRefresh()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            if (NavigationMode == "Library" && CurrentLibrary != null) LoadLibraryAsync(CurrentLibrary);
            else if (NavigationMode == "Tag" && CurrentTag != null) LoadTagAsync(CurrentTag.Id.ToString());
            else LoadPathAsync(CurrentPath);
        }

        public void NavigateTo(string path) { if (!string.IsNullOrEmpty(path)) CurrentPath = path; }

        private async void LoadPathAsync(string path)
        {
            if (IsLoading || string.IsNullOrEmpty(path)) return;
            try
            {
                IsLoading = true;
                StatusText = "正在加载...";
                // LoadFileSystemItemsAsync handles paths, virtual paths (lib://, tag://) internally
                var results = await _fileListService.LoadFileSystemItemsAsync(path);
                _dispatcher.Invoke(() => { Files.Clear(); foreach (var item in results) Files.Add(item); });
                StatusText = $"共 {results.Count} 项";
            }
            catch { StatusText = "加载失败"; }
            finally { IsLoading = false; }
        }

        private void LoadLibraryAsync(Library lib)
        {
            if (IsLoading || lib == null) return;
            if (_libraryService == null) return;

            IsLoading = true;
            StatusText = "加载库数据...";
            _libraryService.LoadLibraryFiles(lib, targetPane: _isSecondary ? PaneId.Second : PaneId.Main);
            // Result will be handled in OnLibraryFilesLoaded
        }

        private async void LoadTagAsync(string tagIdOrName)
        {
            if (IsLoading || string.IsNullOrEmpty(tagIdOrName)) return;
            try
            {
                IsLoading = true;
                StatusText = "筛选标签内容...";
                // Use FileListService protocol support for tags
                var results = await _fileListService.LoadFileSystemItemsAsync($"tag://{tagIdOrName}");
                _dispatcher.Invoke(() => { Files.Clear(); foreach (var item in results) Files.Add(item); });
                StatusText = $"标签筛选 ({results.Count} 项)";
            }
            catch { StatusText = "标签内容加载失败"; }
            finally { IsLoading = false; }
        }

        private void UpdateDynamicMenuItems()
        {
            if (_dispatcher == null) return;
            _dispatcher.Invoke(() =>
            {
                var libraries = _libraryService?.GetAllLibraries() ?? new List<Library>();
                _libraryMenuItems.Clear();
                foreach (var lib in libraries)
                {
                    bool isChecked = SelectedItems.Count > 0 && SelectedItems.All(i => lib.Paths != null && lib.Paths.Contains(i.Path));
                    _libraryMenuItems.Add(new ContextMenuItemViewModel { Header = lib.Name, Command = Commands?.ToggleLibraryCommand, CommandParameter = lib, IsCheckable = true, IsChecked = isChecked, Icon = Application.Current.TryFindResource("Icon_Library") });
                }
                if (libraries.Count > 0) _libraryMenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });
                _libraryMenuItems.Add(new ContextMenuItemViewModel { Header = "新建库...", Command = Commands?.NewLibraryCommand });

                _tagMenuItems.Clear();
                if (App.IsTagTrainAvailable)
                {
                    var tags = _tagService?.GetAllTags() ?? new List<ITag>();
                    foreach (var tag in tags) { bool isChecked = SelectedItems.Count > 0 && SelectedItems.All(i => i.TagList != null && i.TagList.Any(t => t.Id == tag.Id)); _tagMenuItems.Add(new ContextMenuItemViewModel { Header = tag.Name, Command = Commands?.ToggleTagCommand, CommandParameter = tag, IsCheckable = true, IsChecked = isChecked, IconBrush = tag.Color ?? "#808080" }); }
                }

                var groups = _favoriteService?.GetAllGroups() ?? new List<FavoriteGroup>();
                _favoriteMenuItems.Clear();
                foreach (var group in groups) _favoriteMenuItems.Add(new ContextMenuItemViewModel { Header = group.Name, Command = Commands?.AddToFavoriteCommand, CommandParameter = group.Id, Icon = Application.Current.TryFindResource("Icon_Favorite") });
                if (groups.Count > 0) _favoriteMenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });
                _favoriteMenuItems.Add(new ContextMenuItemViewModel { Header = "+ 新建分组...", Command = Commands?.NewFavoriteGroupCommand });
            });
        }

        #endregion

        #region Messages

        private void OnSearchResultUpdated(SearchResultUpdatedMessage message)
        {
            if (message.TargetPaneId == "Any" || (_isSecondary && message.TargetPaneId == "Secondary") || (!_isSecondary && message.TargetPaneId == "Primary"))
            {
                _dispatcher.Invoke(() => { Files.Clear(); foreach (var item in message.Results) Files.Add(item); StatusText = $"搜索结果: {message.Results.Count} 项"; });
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
        private void OnTagListChanged(TagListChangedMessage msg) { UpdateDynamicMenuItems(); RequestRefresh(); }
        private void OnLibraryListChanged(LibraryListChangedMessage msg) { UpdateDynamicMenuItems(); RequestRefresh(); }
        private void OnFavoritesUpdated(FavoritesUpdatedMessage msg) => UpdateDynamicMenuItems();
        private void OnRefreshFileList(RefreshFileListMessage msg) => RequestRefresh();
        private void OnLibrarySelected(LibrarySelectedMessage msg) { if (msg.Library != null) NavigateTo($"lib://{msg.Library.Name}"); }
        private void OnFileSelectionChanged(FileSelectionChangedMessage msg) { if (msg.Pane == (_isSecondary ? PaneId.Second : PaneId.Main)) Commands?.NotifyCommandStatesChanged(); }
        private void OnNavigateToPath(NavigateToPathMessage msg) => NavigateTo(msg.Path);

        private void OnLibraryFilesLoaded(object sender, LibraryFilesLoadedEventArgs e)
        {
            // 只有当是本面板请求时才处理
            if (e.TargetPane == (_isSecondary ? PaneId.Second : PaneId.Main))
            {
                _dispatcher.Invoke(() =>
                {
                    Files.Clear();
                    foreach (var item in e.Files) Files.Add(item);
                    StatusText = $"库: {e.Library.Name} ({e.Files.Count} 项)";
                    IsLoading = false;
                });
            }
        }

        private void OnLibrariesLoaded(object sender, List<Library> libraries) => UpdateDynamicMenuItems();

        #endregion

        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public void Dispose()
        {
            if (_libraryService != null)
            {
                _libraryService.LibrariesLoaded -= OnLibrariesLoaded;
                _libraryService.LibraryFilesLoaded -= OnLibraryFilesLoaded;
            }
        }
    }
}
