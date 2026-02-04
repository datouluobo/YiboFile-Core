using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using YiboFile;
using YiboFile.Controls;
using YiboFile.Services;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.FileList;
using YiboFile.Models;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Core;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 文件列表 ViewModel
    /// 负责管理文件列表的加载、刷新、排序等功能
    /// </summary>
    public class FileListViewModel : BaseViewModel, IDisposable
    {
        private readonly FileBrowserControl _fileBrowser;
        private readonly Window _ownerWindow;
        private readonly Dispatcher _dispatcher;
        private readonly FileListService _fileListService;
        private readonly ColumnService _columnService;
        private readonly FileMetadataEnricher _metadataEnricher;
        private readonly FolderSizeCalculator _folderSizeCalculator;
        private readonly Action _refreshAction;
        private const int MaxMetadataEnrichCount = 500;

        private string _currentPath = null;
        private string _pendingPath = null;
        private ObservableCollection<FileSystemItem> _files = new ObservableCollection<FileSystemItem>();
        private bool _isLoading = false;
        private string _lastSortColumn = "Name";
        private bool _sortAscending = true;
        private FileSystemWatcher _fileWatcher;
        private DispatcherTimer _refreshDebounceTimer;
        private bool _isLoadingFiles = false;
        private bool _loadFilesPending = false;
        private readonly SemaphoreSlim _loadFilesSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _loadCancellationTokenSource = null;

        public ObservableCollection<FileSystemItem> Files
        {
            get => _files;
            set
            {
                SetProperty(ref _files, value);
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string LastSortColumn
        {
            get => _lastSortColumn;
            set => SetProperty(ref _lastSortColumn, value);
        }

        public bool SortAscending
        {
            get => _sortAscending;
            set => SetProperty(ref _sortAscending, value);
        }

        public void UpdateFiles(IEnumerable<FileSystemItem> items)
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                Files = new ObservableCollection<FileSystemItem>(items);
            }), DispatcherPriority.Normal);
        }

        public FileListViewModel(
            FileBrowserControl fileBrowser,
            Window ownerWindow,
            Action refreshAction = null,
            ColumnService columnService = null,
            FileMetadataEnricher metadataEnricher = null,
            FolderSizeCalculator folderSizeCalculator = null)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _dispatcher = ownerWindow.Dispatcher;

            var errorService = App.ServiceProvider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>();
            var tagService = App.ServiceProvider.GetService<Services.Features.ITagService>();
            _fileListService = new FileListService(_dispatcher, errorService, tagService);

            _columnService = columnService;
            _metadataEnricher = metadataEnricher ?? new FileMetadataEnricher();
            _folderSizeCalculator = folderSizeCalculator ?? new FolderSizeCalculator();
            _refreshAction = refreshAction;

            // 初始化防抖定时器
            _refreshDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(3000)
            };
            _refreshDebounceTimer.Tick += (s, e) =>
            {
                _refreshDebounceTimer.Stop();
                _refreshAction?.Invoke();

                if (_refreshAction == null && !_isLoadingFiles)
                {
                    RefreshFiles();
                }
            };
        }

        /// <summary>
        /// 加载文件列表（替代旧 LoadFiles / LoadCurrentDirectory）
        /// </summary>
        public async Task LoadPathAsync(string path)
        {
            System.Diagnostics.Debug.WriteLine($"[FileListViewModel] LoadPathAsync requested for: {path}");

            // 如果正在加载相同目录，则忽略以防止循环
            if (_isLoadingFiles && path == _currentPath)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListViewModel] LoadPathAsync already loading: {path}. Skipping redundant request.");
                return;
            }

            // 如果正在加载其它目录，则取消旧的并排队
            if (_isLoadingFiles)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListViewModel] Already loading, queuing pending path: {path}");
                _pendingPath = path;
                _loadFilesPending = true;

                // 仅在不同路径时取消，防止微小变动导致的频繁重载
                _loadCancellationTokenSource?.Cancel();
                return;
            }

            try
            {
                // 获取信号量锁，防止并发重入 (加上合理的等待时间)
                if (!await _loadFilesSemaphore.WaitAsync(5000))
                {
                    System.Diagnostics.Debug.WriteLine($"[FileListViewModel] Failed to acquire load semaphore within 5s for {path}. Skipping.");
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListViewModel] Semaphore wait error: {ex.Message}");
                return;
            }

            try
            {
                // 再次检查重入
                if (_isLoadingFiles) return;

                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _loadCancellationTokenSource.Token;

                _currentPath = path;
                System.Diagnostics.Debug.WriteLine($"[FileListViewModel] Starting load for: {path}");

                // Check for virtual protocols to bypass Directory.Exists check
                var protocol = ProtocolManager.Parse(path);
                bool isVirtual = protocol.Type != ProtocolType.Local;

                if (string.IsNullOrEmpty(path) || (!isVirtual && !Directory.Exists(path)))
                {
                    System.Diagnostics.Debug.WriteLine($"[FileListViewModel] Path Empty or Not Exists (Local). Clearing files.");
                    await _dispatcher.InvokeAsync(() =>
                    {
                        Files.Clear();
                    }, DispatcherPriority.Normal);
                    SetupFileWatcher(null);
                    return;
                }

                _isLoadingFiles = true;
                IsLoading = true;

                // 异步加载文件列表
                cancellationToken.ThrowIfCancellationRequested();
                var files = await _fileListService.LoadFileSystemItemsAsync(
                    path,
                    null,
                    cancellationToken);

                System.Diagnostics.Debug.WriteLine($"[FileListViewModel] Files loaded. Count: {files?.Count ?? 0}");
                var sortedFiles = ApplySorting(files);

                // 设置集合，确保在 UI 线程执行
                if (cancellationToken.IsCancellationRequested) return;

                await _dispatcher.InvokeAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Files = new ObservableCollection<FileSystemItem>(sortedFiles);
                    }
                }, DispatcherPriority.Normal);

                // 后台设置文件监视
                if (!cancellationToken.IsCancellationRequested)
                {
                    await _dispatcher.InvokeAsync(() => SetupFileWatcher(_currentPath), DispatcherPriority.Background);
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListViewModel] LoadPathAsync Canceled for: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListViewModel] LoadPathAsync Failed: {ex}");
                await _dispatcher.BeginInvoke(new Action(() =>
                {
                    YiboFile.DialogService.Error($"加载文件列表失败: {ex.Message}", owner: _ownerWindow);
                }), DispatcherPriority.Normal);
            }
            finally
            {
                _isLoadingFiles = false;
                IsLoading = false;
                _loadFilesSemaphore.Release();

                // 确保信号量已释放后再检查 pending 任务
                if (_loadFilesPending)
                {
                    // 使用非阻塞的 BeginInvoke 避免在 UI 线程同步等待时产生逻辑死锁
                    _ = _dispatcher.BeginInvoke(new Action(CheckPendingLoad), DispatcherPriority.Normal);
                }
            }
        }

        /// <summary>
        /// 兼容旧接口，调用 LoadPathAsync。
        /// </summary>
        public Task LoadFilesAsync(string path) => LoadPathAsync(path);

        /// <summary>
        /// 直接设置文件列表（搜索、标签或库合并场景）。
        /// </summary>
        public void SetFiles(IEnumerable<FileSystemItem> files)
        {
            CancelOngoingOperations();
            _loadFilesPending = false;
            _pendingPath = null;
            _currentPath = null;

            var items = files?.ToList() ?? new List<FileSystemItem>();
            var sorted = ApplySorting(items);

            _dispatcher.Invoke(() =>
            {
                Files = new ObservableCollection<FileSystemItem>(sorted);
                if (_fileBrowser != null)
                {
                    // _fileBrowser.FilesItemsSource = Files; // Do not break binding
                }
                SetupFileWatcher(null);
                RefreshCollectionView();
            });

            // 对于手动设置的文件列表（如搜索结果），我们在此启动增强和计算
            var cts = new CancellationTokenSource();
            _ = _metadataEnricher.EnrichAsync(items, cts.Token, _dispatcher, null, null);
            _ = _folderSizeCalculator.CalculateAsync(items, cts.Token, _dispatcher, _fileListService.FormatFileSize, null);
        }

        /// <summary>
        /// 刷新文件列表
        /// </summary>
        public void RefreshFiles()
        {
            System.Diagnostics.Debug.WriteLine($"[FileListViewModel] RefreshFiles called. CurrentPath: {_currentPath}");
            if (_refreshAction != null)
            {
                _refreshAction();
                return;
            }

            var targetPath = _currentPath;
            if (string.IsNullOrEmpty(targetPath) || _isLoadingFiles)
            {
                if (_isLoadingFiles) System.Diagnostics.Debug.WriteLine($"[FileListViewModel] RefreshFiles ignored: IsLoadingFiles=true");
                return;
            }

            _ = LoadPathAsync(targetPath);
        }

        /// <summary>
        /// 设置文件监视器
        /// </summary>
        public void SetupFileWatcher(string path)
        {
            try
            {
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Created -= OnFileSystemChanged;
                    _fileWatcher.Deleted -= OnFileSystemChanged;
                    _fileWatcher.Changed -= OnFileSystemChanged;
                    _fileWatcher.Renamed -= OnFileSystemChanged;
                    _fileWatcher.Dispose();
                    _fileWatcher = null;
                }

                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    return;
                }

                // 虚拟路径不支持监听
                if (ProtocolManager.IsVirtual(path))
                {
                    return;
                }

                _fileWatcher = new FileSystemWatcher
                {
                    Path = path,
                    Filter = "*.*",
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Created += OnFileSystemChanged;
                _fileWatcher.Deleted += OnFileSystemChanged;
                _fileWatcher.Changed += OnFileSystemChanged;
                _fileWatcher.Renamed += OnFileSystemChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListViewModel] Failed to setup FileWatcher for {path}: {ex.Message}");
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {

            _dispatcher.BeginInvoke(new Action(() =>
            {
                _refreshDebounceTimer.Stop();
                _refreshDebounceTimer.Start();
            }), DispatcherPriority.Background);
        }

        /// <summary>
        /// 通过 ColumnService 统一排序入口。
        /// </summary>
        public void ApplySort(string column, bool ascending)
        {
            if (string.IsNullOrWhiteSpace(column) && string.IsNullOrWhiteSpace(LastSortColumn))
            {
                return;
            }

            LastSortColumn = string.IsNullOrWhiteSpace(column) ? LastSortColumn : column;
            SortAscending = ascending;

            var sorted = ApplySorting(Files?.ToList() ?? new List<FileSystemItem>());
            _dispatcher.Invoke(() =>
            {
                Files = new ObservableCollection<FileSystemItem>(sorted);
                if (_fileBrowser != null)
                {
                    _fileBrowser.FilesItemsSource = Files;
                }
                RefreshCollectionView();
            });
        }

        private List<FileSystemItem> ApplySorting(List<FileSystemItem> files)
        {
            if (files == null || files.Count == 0)
            {
                return files ?? new List<FileSystemItem>();
            }

            if (_columnService != null)
            {
                return _columnService.SortFiles(files, LastSortColumn, SortAscending);
            }

            return LegacySort(files, LastSortColumn, SortAscending);
        }

        private List<FileSystemItem> LegacySort(List<FileSystemItem> files, string column = null, bool? ascending = null)
        {
            if (files == null || files.Count == 0)
            {
                return files ?? new List<FileSystemItem>();
            }

            string sortColumn = column ?? LastSortColumn;
            bool sortAscending = ascending ?? SortAscending;

            switch (sortColumn)
            {
                case "Name":
                    files.Sort((a, b) => sortAscending
                        ? string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                        : string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase));
                    break;
                case "Type":
                    files.Sort((a, b) => sortAscending
                        ? string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase)
                        : string.Compare(b.Type, a.Type, StringComparison.OrdinalIgnoreCase));
                    break;
                case "Size":
                    files.Sort((a, b) =>
                    {
                        long sizeA = ParseFileSize(a.Size);
                        long sizeB = ParseFileSize(b.Size);
                        return sortAscending ? sizeA.CompareTo(sizeB) : sizeB.CompareTo(sizeA);
                    });
                    break;
                case "ModifiedDate":
                    files.Sort((a, b) =>
                    {
                        DateTime dateA = ParseDate(a.ModifiedDate);
                        DateTime dateB = ParseDate(b.ModifiedDate);
                        return sortAscending ? dateA.CompareTo(dateB) : dateB.CompareTo(dateA);
                    });
                    break;
            }

            LastSortColumn = sortColumn;
            SortAscending = sortAscending;
            return files;
        }

        private long ParseFileSize(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr))
                return 0;

            sizeStr = sizeStr.Trim().ToUpper();
            if (sizeStr.EndsWith("B"))
            {
                sizeStr = sizeStr.Substring(0, sizeStr.Length - 1).Trim();
            }

            if (sizeStr.EndsWith("KB"))
            {
                if (double.TryParse(sizeStr.Substring(0, sizeStr.Length - 2).Trim(), out double kb))
                    return (long)(kb * 1024);
            }
            else if (sizeStr.EndsWith("MB"))
            {
                if (double.TryParse(sizeStr.Substring(0, sizeStr.Length - 2).Trim(), out double mb))
                    return (long)(mb * 1024 * 1024);
            }
            else if (sizeStr.EndsWith("GB"))
            {
                if (double.TryParse(sizeStr.Substring(0, sizeStr.Length - 2).Trim(), out double gb))
                    return (long)(gb * 1024 * 1024 * 1024);
            }
            else if (long.TryParse(sizeStr, out long bytes))
            {
                return bytes;
            }

            return 0;
        }

        private DateTime ParseDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out DateTime result))
                return result;
            return DateTime.MinValue;
        }


        public void Dispose()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            _refreshDebounceTimer?.Stop();
            CancelOngoingOperations();
            _loadFilesSemaphore?.Dispose();
        }

        private void CancelOngoingOperations()
        {
            if (_loadCancellationTokenSource != null)
            {
                try
                {
                    _loadCancellationTokenSource.Cancel();
                }
                catch
                {
                }
                finally
                {
                    _loadCancellationTokenSource.Dispose();
                    _loadCancellationTokenSource = null;
                }
            }
        }

        private void CheckPendingLoad()
        {
            if (_loadFilesPending)
            {
                _loadFilesPending = false;
                var nextPath = _pendingPath;
                _pendingPath = null;
                if (!string.IsNullOrWhiteSpace(nextPath))
                {
                    _ = LoadPathAsync(nextPath);
                }
            }
        }

        private void RefreshCollectionView()
        {
            var view = CollectionViewSource.GetDefaultView(_fileBrowser?.FilesItemsSource ?? Files);
            view?.Refresh();
        }
    }
}


