using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using YiboFile.Models;
using YiboFile.Controls;
using YiboFile.Services;
using YiboFile.Services.Core;
using YiboFile.Services.Features;
using YiboFile.Services.FileList;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 面板 ViewModel，管理单个面板的完整状态
    /// 支持主面板和副面板，可独立运作
    /// </summary>
    public class PaneViewModel : BaseViewModel, IPaneContext, IDisposable
    {
        #region Private Fields

        private readonly Dispatcher _dispatcher;
        private readonly bool _isSecondary;
        private readonly FileListService _fileListService;
        private readonly LibraryService _libraryService;
        private readonly ITagService _tagService;
        private readonly IMessageBus _messageBus;

        private string _currentPath;
        private Library _currentLibrary;
        private TagViewModel _currentTag;
        private string _navigationMode = "Path";
        private ObservableCollection<FileSystemItem> _files;
        private ObservableCollection<FileSystemItem> _selectedItems = new ObservableCollection<FileSystemItem>();
        private bool _isLoading;

        private CancellationTokenSource _loadCts;
        private FileSystemWatcher _fileWatcher;
        private DispatcherTimer _refreshDebounceTimer;
        private string _searchStatusText;
        private bool _isSearching;

        #endregion

        #region Events

        /// <summary>
        /// 路径变更事件
        /// </summary>
        public event EventHandler<string> PathChanged;

        /// <summary>
        /// 库变更事件
        /// </summary>
        public event EventHandler<Library> LibraryChanged;

        /// <summary>
        /// 标签变更事件
        /// </summary>
        public event EventHandler<TagViewModel> TagChanged;

        /// <summary>
        /// 导航模式变更事件
        /// </summary>
        public event EventHandler<string> NavigationModeChanged;

        /// <summary>
        /// 文件列表加载完成事件
        /// </summary>
        public event EventHandler<ObservableCollection<FileSystemItem>> FilesLoaded;

        #endregion

        #region Properties

        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                if (SetProperty(ref _currentPath, value))
                {
                    PathChanged?.Invoke(this, value);
                }
            }
        }

        public Library CurrentLibrary
        {
            get => _currentLibrary;
            set
            {
                if (SetProperty(ref _currentLibrary, value))
                {
                    LibraryChanged?.Invoke(this, value);
                }
            }
        }

        public TagViewModel CurrentTag
        {
            get => _currentTag;
            set
            {
                if (SetProperty(ref _currentTag, value))
                {
                    TagChanged?.Invoke(this, value);
                }
            }
        }

        public string NavigationMode
        {
            get => _navigationMode;
            set
            {
                if (SetProperty(ref _navigationMode, value))
                {
                    NavigationModeChanged?.Invoke(this, value);
                }
            }
        }

        public ObservableCollection<FileSystemItem> Files
        {
            get => FileList?.Files ?? _files;
        }

        public ObservableCollection<FileSystemItem> SelectedItems
        {
            get => _selectedItems;
            private set => SetProperty(ref _selectedItems, value);
        }

        private FileSystemItem _selectedItem;
        public FileSystemItem SelectedItem
        {
            get => _selectedItem;
            private set => SetProperty(ref _selectedItem, value);
        }

        public void UpdateSelection(System.Collections.IList items)
        {
            _selectedItems.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item is FileSystemItem fsItem)
                    {
                        _selectedItems.Add(fsItem);
                    }
                }
            }
            SelectedItem = _selectedItems.FirstOrDefault();

            // Notify commands to re-evaluate
            // CommandManager.InvalidateRequerySuggested(); // In WPF this is static
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private FileListViewModel _fileList;
        public FileListViewModel FileList
        {
            get => _fileList;
            set
            {
                var oldFileList = _fileList;
                if (SetProperty(ref _fileList, value))
                {
                    if (oldFileList != null)
                    {
                        oldFileList.PropertyChanged -= OnFileListPropertyChanged;
                    }
                    if (_fileList != null)
                    {
                        _fileList.PropertyChanged += OnFileListPropertyChanged;
                    }
                    OnPropertyChanged(nameof(Files));
                }
            }
        }

        private void OnFileListPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FileListViewModel.Files))
            {
                OnPropertyChanged(nameof(Files));
            }
        }

        public bool IsSecondary => _isSecondary;

        public void RequestActivation()
        {
            _messageBus.Publish(new SetFocusedPaneMessage(_isSecondary));
        }

        public string SearchStatusText
        {
            get => _searchStatusText;
            set => SetProperty(ref _searchStatusText, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public SearchViewModel Search { get; }

        public ICommand RefreshCommand { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// 创建面板 ViewModel
        /// </summary>
        /// <param name="dispatcher">UI 调度器</param>
        /// <param name="isSecondary">是否为次要面板</param>
        public PaneViewModel(Dispatcher dispatcher, IMessageBus messageBus, bool isSecondary = false)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _isSecondary = isSecondary;
            _files = new ObservableCollection<FileSystemItem>();

            RefreshCommand = new RelayCommand(() => RequestRefresh());

            Search = new SearchViewModel(_messageBus);
            Search.SetTargetPane(isSecondary ? "Secondary" : "Primary");

            // 订阅搜索结果消息
            _messageBus.Subscribe<SearchResultUpdatedMessage>(OnSearchResultUpdated);

            // 订阅实时更新消息
            _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);
            _messageBus.Subscribe<FileTagsChangedMessage>(OnFileTagsChanged);
            _messageBus.Subscribe<TagListChangedMessage>(OnTagListChanged);
            _messageBus.Subscribe<TagListChangedMessage>(OnTagListChanged);
            // 订阅刷新消息
            _messageBus.Subscribe<RefreshFileListMessage>(OnRefreshFileList);
            // 订阅库选择消息
            _messageBus.Subscribe<LibrarySelectedMessage>(OnLibrarySelected);

            // 获取服务
            var errorService = App.ServiceProvider?.GetService<YiboFile.Services.Core.Error.ErrorService>();
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            _libraryService = App.ServiceProvider?.GetService<LibraryService>();

            if (errorService != null)
            {
                _fileListService = new FileListService(_dispatcher, errorService, _tagService);
            }

            if (_libraryService != null)
            {
                _libraryService.LibrariesLoaded += OnLibrariesLoaded;
            }

            // 初始化防抖定时器
            _refreshDebounceTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _refreshDebounceTimer.Tick += (s, e) =>
            {
                _refreshDebounceTimer.Stop();
                Refresh();
            };
        }

        #endregion

        #region Navigation Methods

        /// <summary>
        /// 导航到指定路径
        /// </summary>
        public void NavigateTo(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            NavigationMode = "Path";
            CurrentLibrary = null;
            CurrentTag = null;
            CurrentPath = path;

            _ = LoadPathAsync(path);
        }

        /// <summary>
        /// 导航到指定库
        /// </summary>
        public void NavigateTo(Library library)
        {
            if (library == null) return;

            NavigationMode = "Library";
            CurrentTag = null;
            CurrentPath = null;
            CurrentLibrary = library;

            if (FileList != null)
            {
                _ = FileList.LoadPathAsync($"lib://{library.Name}");
            }
            else
            {
                _ = LoadLibraryAsync(library);
            }
        }

        /// <summary>
        /// 导航到指定标签
        /// </summary>
        public void NavigateTo(TagViewModel tag)
        {
            if (tag == null) return;

            NavigationMode = "Tag";
            CurrentLibrary = null;
            CurrentPath = null;
            CurrentTag = tag;

            if (FileList != null)
            {
                _ = FileList.LoadPathAsync($"tag://{tag.Name}");
            }
            else
            {
                _ = LoadTagAsync(tag);
            }
        }

        /// <summary>
        /// 刷新当前视图
        /// </summary>
        public void Refresh()
        {
            switch (NavigationMode)
            {
                case "Path":
                    if (!string.IsNullOrEmpty(CurrentPath))
                        _ = LoadPathAsync(CurrentPath);
                    break;
                case "Library":
                    if (CurrentLibrary != null)
                        _ = LoadLibraryAsync(CurrentLibrary);
                    break;
                case "Tag":
                    if (CurrentTag != null)
                        _ = LoadTagAsync(CurrentTag);
                    break;
            }
        }

        /// <summary>
        /// 请求刷新（带防抖）
        /// </summary>
        public void RequestRefresh()
        {
            _refreshDebounceTimer.Stop();
            _refreshDebounceTimer.Start();
        }

        #endregion

        #region Loading Methods

        /// <summary>
        /// 异步加载路径
        /// </summary>
        private async Task LoadPathAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || _fileListService == null) return;

            // 取消之前的加载
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                if (FileList != null)
                {
                    // Delegate to FileListViewModel which handles threading/IO robustly
                    await FileList.LoadPathAsync(path);
                    return;
                }

                IsLoading = true;

                var items = await Task.Run(async () =>
                {
                    try
                    {
                        var result = await _fileListService.LoadFileSystemItemsAsync(path, null, token);
                        if ((result == null || result.Count == 0) && path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase) && _tagService != null)
                        {
                            // Fallback: try loading tags directly if service returned empty
                            var tagName = path.Substring(6); // len(tag://)
                            var files = await _tagService.GetFilesByTagNameAsync(tagName);
                            // Convert paths to items... manually?
                            // This confirms if service is failing.
                        }
                        return result;
                    }
                    catch (Exception)
                    {
                        return new System.Collections.Generic.List<FileSystemItem>();
                    }
                }, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                await _dispatcher.InvokeAsync(() =>
                {
                    Files.Clear();
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            Files.Add(item);
                        }
                    }
                    FilesLoaded?.Invoke(this, Files);
                });

                // 设置文件监视
                SetupFileWatcher(path);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception)
            {
                // _errorService?.ReportError($"Load error: {ex.Message}"); 
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 异步加载库
        /// </summary>
        private async Task LoadLibraryAsync(Library library)
        {
            if (library == null || _fileListService == null) return;

            // 取消之前的加载
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                IsLoading = true;

                // 获取库的所有路径
                var paths = library.Paths?.Where(p => Directory.Exists(p)).ToList()
                            ?? new System.Collections.Generic.List<string>();

                if (paths.Count == 0)
                {
                    await _dispatcher.InvokeAsync(() => Files.Clear());
                    return;
                }

                var items = await Task.Run(async () =>
                {
                    token.ThrowIfCancellationRequested();
                    return await _fileListService.LoadFileSystemItemsFromMultiplePathsAsync(paths, null, null, token);
                }, token);

                if (token.IsCancellationRequested) return;

                await _dispatcher.InvokeAsync(() =>
                {
                    Files.Clear();
                    foreach (var item in items)
                    {
                        Files.Add(item);
                    }
                    FilesLoaded?.Invoke(this, Files);
                });
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaneViewModel.LoadLibraryAsync] Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 异步加载标签
        /// </summary>
        private async Task LoadTagAsync(TagViewModel tag)
        {
            if (tag == null || _tagService == null) return;

            // 取消之前的加载
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                IsLoading = true;

                var filePaths = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    return _tagService.GetFilesByTag(tag.Id);
                }, token);

                if (token.IsCancellationRequested) return;

                // 使用 FileListService 创建 FileSystemItem
                var items = new System.Collections.Generic.List<FileSystemItem>();
                foreach (var path in filePaths)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                    {
#pragma warning disable CS0618
                        var fileItems = _fileListService?.LoadFileSystemItems(Path.GetDirectoryName(path));
                        var item = fileItems?.FirstOrDefault(f => f.Path == path);
                        if (item != null)
                        {
                            items.Add(item);
                        }
#pragma warning restore CS0618
                    }
                }

                await _dispatcher.InvokeAsync(() =>
                {
                    Files.Clear();
                    foreach (var item in items)
                    {
                        Files.Add(item);
                    }
                    FilesLoaded?.Invoke(this, Files);
                });
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaneViewModel.LoadTagAsync] Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 直接设置文件列表（用于搜索结果等外部数据源）
        /// </summary>
        public void SetFiles(System.Collections.Generic.IEnumerable<FileSystemItem> files)
        {
            _dispatcher.Invoke(() =>
            {
                Files.Clear();
                if (files != null)
                {
                    foreach (var item in files)
                    {
                        Files.Add(item);
                    }
                }
                FilesLoaded?.Invoke(this, Files);
            });
        }

        #endregion

        #region File Watcher

        private void SetupFileWatcher(string path)
        {
            // 清理旧的监视器
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            // 虚拟路径不监视
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            try
            {
                _fileWatcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                   NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Created += OnFileSystemChanged;
                _fileWatcher.Deleted += OnFileSystemChanged;
                _fileWatcher.Renamed += OnFileSystemChanged;
                _fileWatcher.Changed += OnFileSystemChanged;
            }
            catch (Exception)
            {
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 使用防抖刷新
            _dispatcher.BeginInvoke(new Action(RequestRefresh));
        }

        #endregion

        private void OnLibrariesLoaded(object sender, System.Collections.Generic.List<Library> libraries)
        {
            _dispatcher.Invoke(() =>
            {
                if (NavigationMode == "Library" && CurrentLibrary != null)
                {
                    var newLib = libraries.FirstOrDefault(l => l.Id == CurrentLibrary.Id);
                    if (newLib != null)
                    {
                        // 更新引用并刷新
                        // 注意：设置 CurrentLibrary 可能会触发 PathChanged 或其他事件
                        // 但这里我们主要想确保刷新时使用有效的数据
                        if (SetProperty(ref _currentLibrary, newLib, nameof(CurrentLibrary)))
                        {
                            // 仅在实际更改时刷新？
                        }

                        RequestRefresh();
                    }
                }
            });
        }

        #region Message Handlers

        private void OnNotesUpdated(NotesUpdatedMessage message)
        {
            if (Files == null) return;
            var item = Files.FirstOrDefault(f => string.Equals(f.Path, message.FilePath, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                _dispatcher.Invoke(() =>
                {
                    item.Notes = message.Notes;
                });
            }
        }

        private async void OnFileTagsChanged(FileTagsChangedMessage message)
        {
            if (Files == null || _tagService == null) return;
            var item = Files.FirstOrDefault(f => string.Equals(f.Path, message.FilePath, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                // 刷新该项的标签
                try
                {
                    var tags = await _tagService.GetFileTagsAsync(message.FilePath);
                    var tagList = tags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name, Color = t.Color }).ToList();

                    await _dispatcher.InvokeAsync(() =>
                    {
                        item.TagList = tagList;
                        item.Tags = string.Join(", ", tagList.Select(t => t.Name));
                    });
                }
                catch { }
            }
        }

        private void OnTagListChanged(TagListChangedMessage message)
        {
            // 如果正在浏览特定标签，刷新以反映潜在的定义更改（如颜色、名称）
            if (NavigationMode == "Tag")
            {
                RequestRefresh();
            }
        }

        private void OnSearchResultUpdated(SearchResultUpdatedMessage message)
        {
            // 检查目标面板是否匹配
            bool isTargetSecondary = message.TargetPaneId == "Secondary";
            if (isTargetSecondary != _isSecondary) return;

            _dispatcher.Invoke(() =>
            {
                if (message.Results != null)
                {
                    SetFiles(message.Results);
                    if (!string.IsNullOrEmpty(message.SearchTabPath))
                    {
                        // 这是一个搜索路径 (search:// 或 content://)
                        // 设置当前路径但不触发普通加载，因为结果已经传过来了
                        _currentPath = message.SearchTabPath;
                        OnPropertyChanged(nameof(CurrentPath));
                        PathChanged?.Invoke(this, message.SearchTabPath);
                    }
                }

                SearchStatusText = message.StatusMessage;
                IsSearching = message.IsSearching;
            });
        }

        private void OnRefreshFileList(RefreshFileListMessage message)
        {
            // 如果指定了路径，检查是否相关
            if (!string.IsNullOrEmpty(message.Path) && NavigationMode == "Path")
            {
                if (!string.Equals(CurrentPath, message.Path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            RequestRefresh();
        }

        private void OnLibrarySelected(LibrarySelectedMessage message)
        {
            if (message.Library == null) return;
            // 仅当当前面板处于活动状态或指定的面板时响应
            // 此处简化逻辑：活动面板响应该消息
            // 更好的做法是 NavigationModule 协调分配，或者检查是否是当前聚焦的面板

            // 假设这是主导航操作，我们让当前活动面板（或者默认主面板）响应
            // 这里简单地全部响应可能导致两个面板都跳转，需结合 ActivePane 逻辑

            // 检查逻辑：如果是主面板且是全局消息

            // 暂时：如果是主面板，则导航
            if (!_isSecondary)
            {
                NavigateTo(message.Library);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_libraryService != null)
            {
                _libraryService.LibrariesLoaded -= OnLibrariesLoaded;
            }

            _loadCts?.Cancel();
            _loadCts?.Dispose();

            _refreshDebounceTimer?.Stop();

            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
        }

        #endregion
    }
}
