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
        private readonly bool _isSecondary;
        private readonly NavigationService _navigationService;

        private string _currentPath;
        private string _navigationMode = "Path"; // Path, Library, Tag, Search
        private Library _currentLibrary;
        private TagViewModel _currentTag;

        private string _fileViewMode = "List"; // List, Grid, LargeIcon
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

                    RequestRefresh();
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

        public string FileViewMode
        {
            get => _fileViewMode;
            set
            {
                if (_fileViewMode != value)
                {
                    _fileViewMode = value;
                    OnPropertyChanged(nameof(FileViewMode));

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

        public bool CanNavigateBack => _navigationService?.CanNavigateBackFor(MyPaneId) ?? false;
        public bool CanNavigateForward => _navigationService?.CanNavigateForwardFor(MyPaneId) ?? false;
        public bool CanNavigateUp => !string.IsNullOrEmpty(CurrentPath) && CurrentPath != "Home";

        public IEnumerable<string> BackStack => _navigationService?.GetBackStack(MyPaneId) ?? Enumerable.Empty<string>();
        public IEnumerable<string> ForwardStack => _navigationService?.GetForwardStack(MyPaneId) ?? Enumerable.Empty<string>();

        public FileListViewModel FileList { get; private set; }
        public bool IsSecondary => _isSecondary;
        public IMessageBus MessageBus => _messageBus;
        public void RequestActivation() => _messageBus.Publish(new SetFocusedPaneMessage(_isSecondary));

        public SearchViewModel Search { get; }
        public FilterViewModel Filter { get; private set; }

        public PaneCommandSet Commands { get; private set; }
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

            _navigationService = App.ServiceProvider.GetService<NavigationService>();

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

            // Messages
            _messageBus.Subscribe<SearchOptionsChangedMessage>(OnSearchOptionsChanged);
            _messageBus.Subscribe<SearchResultUpdatedMessage>(OnSearchResultUpdated);
            _messageBus.Subscribe<Messaging.Messages.FocusedPaneChangedMessage>(OnFocusedPaneChanged);
            _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);
            _messageBus.Subscribe<FileTagsChangedMessage>(OnFileTagsChanged);
            _messageBus.Subscribe<RefreshFileListMessage>(OnRefreshFileList);
            _messageBus.Subscribe<LibrarySelectedMessage>(OnLibrarySelected);
            _messageBus.Subscribe<FileSelectionChangedMessage>(OnFileSelectionChanged);
            _messageBus.Subscribe<LibraryFilesLoadedMessage>(OnLibraryFilesLoaded);

            // New Navigation Handling
            _messageBus.Subscribe<NavigationCompleteMessage>(OnNavigationComplete);

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

            _fileViewMode = ConfigurationService.Instance.Get(cfg => cfg.FileViewMode) ?? "List";

            // Init Path from Service if available
            if (_navigationService != null)
            {
                var initial = _navigationService.GetCurrentPath(MyPaneId);
                if (!string.IsNullOrEmpty(initial)) CurrentPath = initial;
                else CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
            else
            {
                CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }
        }

        #endregion

        #region Internal Executes

        internal void ExecuteSwitchViewMode(string mode) { if (!string.IsNullOrEmpty(mode)) FileViewMode = mode; }

        // Navigation executes removed - handled by Commands/MessageBus direct calls or CommandSet updates
        // However, PaneCommandSet currently calls _pane.ExecuteNavigateBack().
        // We will update PaneCommandSet separately, but for safety due to lingering references?
        // No, I'll update PaneCommandSet next.
        // I will keep the methods but change them to publish messages, to keep PaneCommandSet signature valid until updated.
        internal void ExecuteNavigateBack() => _messageBus.Publish(new NavigateBackMessage(MyPaneId));
        internal void ExecuteNavigateForward() => _messageBus.Publish(new NavigateForwardMessage(MyPaneId));
        internal void ExecuteNavigateUp() => _messageBus.Publish(new NavigateUpMessage(MyPaneId));

        internal void ExecuteSelectAll() => _messageBus.Publish(new SelectAllRequestMessage(_isSecondary ? PaneId.Second : PaneId.Main));

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
                if (msg.Pane == null || msg.Pane == (_isSecondary ? PaneId.Second : PaneId.Main))
                {
                    // Use NavigateTo (which sends message)
                    NavigateTo($"lib://{msg.Library.Name}");
                }
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
                // Update Path properties - use backing field to avoid logic loops unless setter is safe?
                // Setter calls RequestRefresh() and updates NavigationMode. This IS required regardless of source.
                // So we use setter.
                // The setter logic I updated: it NO LONGER publishes PathChangedMessage or Updates History.
                // It ONLY updates local state and refreshes. This is SAFE.
                CurrentPath = msg.Path;

                OnPropertyChanged(nameof(CanNavigateBack));
                OnPropertyChanged(nameof(CanNavigateForward));
                // Also update BackStack/ForwardStack if bound
                OnPropertyChanged(nameof(BackStack));
                OnPropertyChanged(nameof(ForwardStack));

                Commands?.NotifyCommandStatesChanged();
            }
        }

        private void OnLibraryFilesLoaded(LibraryFilesLoadedMessage msg)
        {
            if (msg.TargetPane == (_isSecondary ? PaneId.Second : PaneId.Main))
            {
                FileList?.UpdateFiles(msg.Files);
                _dispatcher.Invoke(() =>
                {
                    StatusText = $"库: {msg.Library.Name} ({msg.Files?.Count ?? 0} 项)";
                    IsLoading = false;
                });
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
