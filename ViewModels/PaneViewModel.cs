using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

            Search = new SearchViewModel(_messageBus);
            Search.SetTargetPane(isSecondary ? "Secondary" : "Primary");

            // 订阅搜索结果消息
            _messageBus.Subscribe<SearchResultUpdatedMessage>(OnSearchResultUpdated);

            // 获取服务
            var errorService = App.ServiceProvider?.GetService<YiboFile.Services.Core.Error.ErrorService>();
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            _libraryService = App.ServiceProvider?.GetService<LibraryService>();

            if (errorService != null)
            {
                _fileListService = new FileListService(_dispatcher, errorService, _tagService);
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

                System.Diagnostics.Debug.WriteLine($"[PaneViewModel] LoadPathAsync started for: {path}, IsSecondary: {_isSecondary}");
                IsLoading = true;

                var items = await Task.Run(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[PaneViewModel] Task.Run started for: {path}");
                        var result = await _fileListService.LoadFileSystemItemsAsync(path, null, token);
                        System.Diagnostics.Debug.WriteLine($"[PaneViewModel] LoadFileSystemItemsAsync returned {result?.Count ?? 0} items for: {path}");
                        if ((result == null || result.Count == 0) && path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase) && _tagService != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PaneViewModel] Fallback check for tag: {path}");
                            // Fallback: try loading tags directly if service returned empty
                            System.Diagnostics.Debug.WriteLine($"[LoadPathAsync] Fallback for tag: {path}");
                            var tagName = path.Substring(6); // len(tag://)
                            var files = await _tagService.GetFilesByTagNameAsync(tagName);
                            // Convert paths to items... manually?
                            // This confirms if service is failing.
                        }
                        return result;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PaneViewModel] Background Error in LoadPathAsync: {ex}");
                        System.Diagnostics.Debug.WriteLine($"[LoadPathAsync] Background Error: {ex}");
                        return new System.Collections.Generic.List<FileSystemItem>();
                    }
                }, token);

                if (token.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine($"[PaneViewModel] LoadPathAsync cancelled for: {path}");
                    return;
                }

                await _dispatcher.InvokeAsync(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"[PaneViewModel] UI update started for: {path}, Items: {items?.Count ?? 0}");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaneViewModel.LoadPathAsync] Error: {ex.Message}");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaneViewModel.SetupFileWatcher] Error: {ex.Message}");
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 使用防抖刷新
            _dispatcher.BeginInvoke(new Action(RequestRefresh));
        }

        #endregion

        #region Message Handlers

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

        #endregion

        #region IDisposable

        public void Dispose()
        {
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
