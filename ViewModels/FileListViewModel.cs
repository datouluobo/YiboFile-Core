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
            set => SetProperty(ref _files, value);
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
            _fileListService = new FileListService(_dispatcher, errorService);

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
            if (!_loadFilesSemaphore.Wait(0))
            {
                _loadFilesPending = true;
                _pendingPath = path;
                return;
            }

            try
            {
                CancelOngoingOperations();
                _loadCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _loadCancellationTokenSource.Token;

                _currentPath = path;
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        Files.Clear();
                        _fileBrowser.FilesItemsSource = Files;
                    }, DispatcherPriority.Background);
                    SetupFileWatcher(null);
                    return;
                }

                if (_isLoadingFiles)
                {
                    return;
                }

                _isLoadingFiles = true;
                IsLoading = true;

                // 异步加载文件列表
                // 使用 Async 方法替换过时的同步方法
                cancellationToken.ThrowIfCancellationRequested();
                var files = await _fileListService.LoadFileSystemItemsAsync(
                    path,
                    null,
                    cancellationToken);

                var sortedFiles = ApplySorting(files);

                await _dispatcher.InvokeAsync(() =>
                {
                    Files = new ObservableCollection<FileSystemItem>(sortedFiles);
                    if (_fileBrowser != null)
                    {
                        _fileBrowser.FilesItemsSource = Files;
                    }
                }, DispatcherPriority.Background);

                await _dispatcher.InvokeAsync(() => SetupFileWatcher(_currentPath), DispatcherPriority.Background);

                var enrichmentTargets = Files.Take(MaxMetadataEnrichCount).ToList();
                if (enrichmentTargets.Count > 0)
                {
                    _ = _metadataEnricher.EnrichAsync(
                        enrichmentTargets,
                        cancellationToken,
                        _dispatcher,
                        null,
                        RefreshCollectionView);
                }

                _ = _folderSizeCalculator.CalculateAsync(
                    Files,
                    cancellationToken,
                    _dispatcher,
                    _fileListService.FormatFileSize,
                    RefreshCollectionView);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await _dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(_ownerWindow, $"加载文件列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }, DispatcherPriority.Normal);
            }
            finally
            {
                _isLoadingFiles = false;
                IsLoading = false;
                _loadFilesSemaphore.Release();
                CheckPendingLoad();
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
                    _fileBrowser.FilesItemsSource = Files;
                }
                SetupFileWatcher(null);
                RefreshCollectionView();
            });
        }

        /// <summary>
        /// 刷新文件列表
        /// </summary>
        public void RefreshFiles()
        {
            if (_refreshAction != null)
            {
                _refreshAction();
                return;
            }

            var targetPath = _currentPath;
            if (string.IsNullOrEmpty(targetPath) || _isLoadingFiles)
            {
                return;
            }

            _ = LoadPathAsync(targetPath);
        }

        /// <summary>
        /// 设置文件监视器
        /// </summary>
        public void SetupFileWatcher(string path)
        {
            _currentPath = path;

            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            try
            {
                _fileWatcher = new FileSystemWatcher
                {
                    Path = path,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    Filter = "*.*",
                    IncludeSubdirectories = false,
                    InternalBufferSize = 8192
                };

                _fileWatcher.Created += OnFileSystemChanged;
                _fileWatcher.Deleted += OnFileSystemChanged;
                _fileWatcher.Renamed += OnFileSystemChanged;
                _fileWatcher.Changed += OnFileSystemChanged;

                _fileWatcher.EnableRaisingEvents = true;
            }
            catch (Exception)
            {
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            if (_isLoadingFiles)
                return;

            _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isLoadingFiles && _refreshDebounceTimer != null)
                {
                    _refreshDebounceTimer.Stop();
                    _refreshDebounceTimer.Start();
                }
            }), DispatcherPriority.SystemIdle);
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


