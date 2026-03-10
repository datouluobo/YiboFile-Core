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
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 文件列表 ViewModel
    /// 负责管理文件列表的加载、刷新、排序等功能
    /// </summary>
    public class FileListViewModel : BaseViewModel, IDisposable
    {
        private readonly Dispatcher _dispatcher;
        private readonly FileListService _fileListService;
        private readonly ColumnService _columnService;
        private readonly FileMetadataEnricher _metadataEnricher;
        private readonly FolderSizeCalculator _folderSizeCalculator;
        private readonly FileSystemWatcherService _fileWatcherService;
        private readonly Services.Features.ITagService _tagService;
        private const int MaxMetadataEnrichCount = 500;

        private string _currentPath = null;
        private string _pendingPath = null;
        private ObservableCollection<FileSystemItem> _files = new ObservableCollection<FileSystemItem>();
        private bool _isLoading = false;
        private string _lastSortColumn = "Name";
        private bool _sortAscending = true;
        private DispatcherTimer _refreshDebounceTimer;
        private bool _isLoadingFiles = false;
        private bool _loadFilesPending = false;
        private readonly SemaphoreSlim _loadFilesSemaphore = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _loadCancellationTokenSource = null;
        private readonly YiboFile.Services.Navigation.PaneId _paneId;
        private bool _showFullFileName = false;

        private object _collectionLock = new object();

        public ObservableCollection<FileSystemItem> Files
        {
            get => _files;
            set
            {
                if (SetProperty(ref _files, value))
                {
                    BindingOperations.EnableCollectionSynchronization(_files, _collectionLock);
                    RefreshFilter();
                }
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

        public bool ShowFullFileName
        {
            get => _showFullFileName;
            set
            {
                if (SetProperty(ref _showFullFileName, value))
                {
                    if (_fileListService != null)
                    {
                        _fileListService.ShowFullFileName = value;
                    }
                }
            }
        }

        public void UpdateFiles(IEnumerable<FileSystemItem> items)
        {
            _dispatcher.BeginInvoke(new Action(() =>
            {
                Files = new ObservableCollection<FileSystemItem>(items);
            }), DispatcherPriority.Normal);
        }

        private readonly IMessageBus _messageBus;

        public FileListViewModel(
            IMessageBus messageBus,
            YiboFile.Services.Navigation.PaneId paneId = YiboFile.Services.Navigation.PaneId.Main,
            ColumnService columnService = null,
            FileMetadataEnricher metadataEnricher = null,
            FolderSizeCalculator folderSizeCalculator = null,
            FileSystemWatcherService fileWatcherService = null)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _dispatcher = System.Windows.Application.Current.Dispatcher;

            var errorService = App.ServiceProvider?.GetService<YiboFile.Services.Core.Error.ErrorService>();
            _tagService = App.ServiceProvider?.GetService<Services.Features.ITagService>();
            _fileListService = new FileListService(_dispatcher, errorService, _tagService, _messageBus, _paneId);

            _messageBus.Subscribe<FileTagsChangedMessage>(OnFileTagsChanged);
            _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);

            _columnService = columnService;
            _metadataEnricher = metadataEnricher ?? new FileMetadataEnricher();
            _folderSizeCalculator = folderSizeCalculator ?? new FolderSizeCalculator();

            // Inject or resolve FileSystemWatcherService
            _fileWatcherService = fileWatcherService ?? App.ServiceProvider?.GetService<FileSystemWatcherService>();

            // 订阅刷新请求消息
            _messageBus.Subscribe<RefreshFileListMessage>(m =>
            {
                if (m.Pane == _paneId)
                {
                    _dispatcher.BeginInvoke(new Action(RefreshFiles), DispatcherPriority.Normal);
                }
            });

            // 初始化防抖定时器
            _refreshDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _refreshDebounceTimer.Tick += (s, e) =>
            {
                _refreshDebounceTimer.Stop();
                RefreshFiles();
            };

            BindingOperations.EnableCollectionSynchronization(_files, _collectionLock);

            _paneId = paneId;
            _messageBus.Subscribe<ViewModeChangedMessage>(m =>
            {
                if (m.TargetPane == _paneId)
                {
                    ShowFullFileName = (m.Mode == YiboFile.Models.Enums.FileListViewMode.Thumbnail);
                    // 重新应用显示名称 (如果当前已有文件)
                    if (Files != null && Files.Count > 0)
                    {
                        foreach (var item in Files)
                        {
                            if (!item.IsDirectory)
                            {
                                item.Name = GetDisplayFileName(item.Path, ShowFullFileName);
                            }
                        }
                    }
                }
            });

            // 订阅文件夹大小计算完成消息 (增量更新)
            _messageBus.Subscribe<FolderSizeCalculatedMessage>(msg =>
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    var item = Files?.FirstOrDefault(f => string.Equals(f.Path, msg.Path, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        item.Size = msg.FormattedSize;
                        item.SizeBytes = msg.Size;
                    }
                }), DispatcherPriority.Background);
            });

            // 订阅元数据增强完成消息 (增量更新)
            _messageBus.Subscribe<MetadataEnrichedMessage>(msg =>
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    var item = Files?.FirstOrDefault(f => string.Equals(f.Path, msg.Item.Path, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        item.Tags = msg.Item.Tags;
                        item.TagList = msg.Item.TagList;
                        item.Notes = msg.Item.Notes;
                        item.NotifyTagsChanged();
                    }
                }), DispatcherPriority.Background);
            });
        }

        private string GetDisplayFileName(string filePath, bool showFullFileName)
        {
            string fileName = Path.GetFileName(filePath);
            if (showFullFileName) return fileName;
            string nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            return string.IsNullOrEmpty(nameWithoutExt) ? fileName : nameWithoutExt;
        }

        /// <summary>
        /// 加载文件列表（替代旧 LoadFiles / LoadCurrentDirectory）
        /// </summary>
        public async Task LoadPathAsync(string path)
        {



            // 如果正在加载其它目录，则取消旧的并排队
            if (_isLoadingFiles)
            {

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

                    return;
                }
            }
            catch (Exception)
            {

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


                // Check for virtual protocols to bypass Directory.Exists check
                var protocol = ProtocolManager.Parse(path);
                bool isVirtual = protocol.Type != ProtocolType.Local;

                if (string.IsNullOrEmpty(path) || (!isVirtual && !Directory.Exists(path)))
                {

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
            catch (TaskCanceledException)
            {
                // Ignore cancellation
            }
            catch (OperationCanceledException)
            {
                // Ignore cancellation
            }
            catch (Exception ex)
            {
                await _dispatcher.BeginInvoke(new Action(() =>
                {
                    YiboFile.DialogService.Error($"加载文件列表失败: {ex.Message}");
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
            int count = files?.Count() ?? 0;

            CancelOngoingOperations();
            _loadFilesPending = false;
            _pendingPath = null;
            _currentPath = null;

            var items = files?.ToList() ?? new List<FileSystemItem>();
            var sorted = ApplySorting(items);

            _dispatcher.Invoke(() =>
            {
                Files = new ObservableCollection<FileSystemItem>(sorted);
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


            var targetPath = _currentPath;
            if (string.IsNullOrEmpty(targetPath))
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
            _fileWatcherService?.SetupFileWatcher(path);
        }

        // 已迁移至 FileSystemWatcherService 处理消息发布
        // private void OnFileSystemChanged(object sender, FileSystemEventArgs e) { ... }

        /// <summary>
        /// 通过 ColumnService 统一排序入口。
        /// </summary>
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

            // 如果使用 CollectionView 排序而非修改源集合
            // 但目前架构似乎是修改源集合顺序
            // 为了兼容 CollectionView 过滤，最好也使用 CollectionView 排序
            // 暂时保持修改源集合顺序，但触发 View 刷新
            var sorted = ApplySorting(Files?.ToList() ?? new List<FileSystemItem>());
            _dispatcher.Invoke(() =>
            {
                Files = new ObservableCollection<FileSystemItem>(sorted);
                RefreshCollectionView();
            });
        }

        private Predicate<FileSystemItem> _currentFilter;

        /// <summary>
        /// 应用过滤 (持久化，直到被清除)
        /// </summary>
        public void ApplyFilter(Predicate<FileSystemItem> filter)
        {
            _currentFilter = filter;
            RefreshFilter();
        }

        /// <summary>
        /// 清除过滤
        /// </summary>
        public void ClearFilter()
        {
            _currentFilter = null;
            RefreshFilter();
        }

        private void RefreshFilter()
        {
            _dispatcher.Invoke(() =>
            {
                var view = CollectionViewSource.GetDefaultView(Files);
                if (view != null)
                {
                    view.Filter = _currentFilter == null ? null : (obj) =>
                    {
                        if (obj is FileSystemItem item)
                        {
                            return _currentFilter(item);
                        }
                        return true;
                    };
                    view.Refresh();
                }
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


        private void OnFileTagsChanged(FileTagsChangedMessage msg)
        {
            if (string.IsNullOrEmpty(msg.FilePath)) return;

            _dispatcher.BeginInvoke(new Action(() =>
            {
                var item = Files.FirstOrDefault(f => string.Equals(f.Path, msg.FilePath, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    RefreshItemTags(item);
                }
            }), DispatcherPriority.Background);
        }

        private async void RefreshItemTags(FileSystemItem item)
        {
            if (item == null || _tagService == null) return;

            try
            {
                var tags = await _tagService.GetFileTagsAsync(item.Path);
                var tagVms = tags.Select(t => new TagViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    Color = t.Color
                }).ToList();

                item.TagList = tagVms;
                item.Tags = string.Join(", ", tagVms.Select(t => t.Name));
            }
            catch (Exception ex)
            {
                FileLogger.LogException($"Error refreshing tags for {item.Path}", ex);
            }
        }

        private void OnNotesUpdated(NotesUpdatedMessage msg)
        {
            if (string.IsNullOrEmpty(msg.FilePath)) return;

            _dispatcher.BeginInvoke(new Action(() =>
            {
                var item = Files.FirstOrDefault(f => string.Equals(f.Path, msg.FilePath, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    item.Notes = msg.Notes;
                }
            }), DispatcherPriority.Background);
        }

        public void Dispose()
        {
            _messageBus?.Unsubscribe<FileTagsChangedMessage>(OnFileTagsChanged);
            _messageBus?.Unsubscribe<NotesUpdatedMessage>(OnNotesUpdated);
            _fileWatcherService?.Dispose();

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
            var view = CollectionViewSource.GetDefaultView(Files);
            view?.Refresh();
        }
    }
}


