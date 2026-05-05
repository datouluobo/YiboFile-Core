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
        private readonly FileSystemWatcherService _fileWatcherService;
        private readonly Services.Features.ITagService _tagService;
        private readonly Services.UI.IDialogService _dialogService;
        private const int MaxMetadataEnrichCount = 500;

        private string _currentPath = null;
        private ObservableCollection<FileSystemItem> _files = new ObservableCollection<FileSystemItem>();
        private bool _isLoading = false;
        private string _lastSortColumn = "Name";
        private bool _sortAscending = true;
        private DispatcherTimer _refreshDebounceTimer;
        private CancellationTokenSource _loadCancellationTokenSource = null;
        private readonly YiboFile.Services.Navigation.PaneId _paneId;
        private bool _showFullFileName = false;

        private object _collectionLock = new object();
        private Dictionary<string, FileSystemItem> _filesByPath =
            new Dictionary<string, FileSystemItem>(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<FileSystemItem> Files
        {
            get => _files;
            set
            {
                if (SetProperty(ref _files, value))
                {
                    BindingOperations.EnableCollectionSynchronization(_files, _collectionLock);
                    // 重建 Path→Item 字典，后续按 Path 查找时为 O(1)
                    _filesByPath = new Dictionary<string, FileSystemItem>(StringComparer.OrdinalIgnoreCase);
                    foreach (var item in _files)
                    {
                        if (!string.IsNullOrEmpty(item.Path))
                            _filesByPath[item.Path] = item;
                    }
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
                // 保留旧文件的 Thumbnail，避免集合替换后图标闪烁
                var oldThumbnails = new Dictionary<string, System.Windows.Media.Imaging.BitmapSource>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (var old in _files)
                {
                    if (old.Thumbnail != null && !string.IsNullOrEmpty(old.Path))
                        oldThumbnails[old.Path] = old.Thumbnail;
                }

                var itemList = items.ToList();
                foreach (var item in itemList)
                {
                    if (oldThumbnails.TryGetValue(item.Path, out var thumb))
                        item.Thumbnail = thumb;
                }

                Files = new ObservableCollection<FileSystemItem>(itemList);
            }), DispatcherPriority.Background);
        }

        private readonly IMessageBus _messageBus;

        public FileListViewModel(
            IMessageBus messageBus,
            YiboFile.Services.Navigation.PaneId paneId = YiboFile.Services.Navigation.PaneId.Main,
            ColumnService columnService = null,
            FileMetadataEnricher metadataEnricher = null,
            FileSystemWatcherService fileWatcherService = null,
            Services.UI.IDialogService dialogService = null)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _dispatcher = System.Windows.Application.Current.Dispatcher;
            _paneId = paneId;
            _dialogService = dialogService ?? App.ServiceProvider?.GetService<Services.UI.IDialogService>();

            var errorService = App.ServiceProvider?.GetService<YiboFile.Services.Core.Error.ErrorService>();
            _tagService = App.ServiceProvider?.GetService<Services.Features.ITagService>();
            _fileListService = new FileListService(_dispatcher, errorService, _tagService, _messageBus, _paneId);

            _messageBus.Subscribe<FileTagsChangedMessage>(OnFileTagsChanged);
            _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);

            _columnService = columnService;
            _metadataEnricher = metadataEnricher ?? new FileMetadataEnricher();

            // Inject or resolve FileSystemWatcherService
            _fileWatcherService = fileWatcherService ?? App.ServiceProvider?.GetService<FileSystemWatcherService>();

            // 订阅刷新请求消息
            _messageBus.Subscribe<RefreshFileListMessage>(m =>
            {
                if (m.Pane == _paneId)
                {
                    // 如果指定了路径，仅当与当前路径匹配时才刷新
                    if (!string.IsNullOrEmpty(m.Path))
                    {
                        var normalized = Path.GetFullPath(m.Path).TrimEnd('\\');
                        if (!string.Equals(normalized, _currentPath?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                            return;
                    }
                    _dispatcher.BeginInvoke(new Action(RefreshFiles), DispatcherPriority.Normal);
                }
            });

            // 订阅增量变更消息 — 不触发整体刷新，仅添加/移除项目
            _messageBus.Subscribe<FileItemsChangedMessage>(m =>
            {
                if (m.Pane != _paneId) return;
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    bool changed = false;

                    if (m.RemovedPaths?.Count > 0)
                    {
                        var toRemove = _files.Where(f =>
                            m.RemovedPaths.Any(r => string.Equals(f.Path, r, StringComparison.OrdinalIgnoreCase))
                        ).ToList();
                        foreach (var item in toRemove)
                        {
                            _files.Remove(item);
                            if (!string.IsNullOrEmpty(item.Path))
                                _filesByPath.Remove(item.Path);
                            changed = true;
                        }
                    }

                    if (m.InsertedPaths?.Count > 0)
                    {
                        foreach (var path in m.InsertedPaths)
                        {
                            if (_files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
                                continue;
                            var item = new FileSystemItem
                            {
                                Path = path,
                                Name = Path.GetFileName(path),
                                IsDirectory = Directory.Exists(path)
                            };
                            // 填入基本信息
                            try
                            {
                                var fi = new System.IO.FileInfo(path);
                                if (fi.Exists)
                                {
                                    item.ModifiedDateTime = fi.LastWriteTime;
                                    item.SizeBytes = fi.Length;
                                    item.Size = fi.Length >= 1073741824
                                        ? $"{fi.Length / 1073741824.0:F1} GB"
                                        : fi.Length >= 1048576
                                            ? $"{fi.Length / 1048576.0:F1} MB"
                                            : fi.Length >= 1024
                                                ? $"{fi.Length / 1024.0:F1} KB"
                                                : $"{fi.Length} B";
                                }
                            }
                            catch { }
                            _files.Add(item);
                            if (!string.IsNullOrEmpty(item.Path))
                                _filesByPath[item.Path] = item;
                            changed = true;
                        }
                    }

                    if (changed) RefreshFilter();
                }), DispatcherPriority.Background);
            });

            // 订阅剪贴板剪切状态变更 — 更新文件项目的剪切视觉效果
            _messageBus.Subscribe<ClipboardCutStateChangedMessage>(m =>
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    var cutPaths = new HashSet<string>(m.CutPaths ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                    foreach (var item in _files)
                    {
                        item.IsCutCandidate = item.Path != null && cutPaths.Contains(item.Path);
                    }
                }), DispatcherPriority.Background);
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
                    var item = _filesByPath.TryGetValue(msg.Path, out var found) ? found : null;
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
                    var item = _filesByPath.TryGetValue(msg.Item.Path, out var foundItem) ? foundItem : null;
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

        private long _loadRequestId = 0;

        /// <summary>
        /// 加载文件列表（替代旧 LoadFiles / LoadCurrentDirectory）
        /// </summary>
        public async Task LoadPathAsync(string path)
        {
            var requestId = System.Threading.Interlocked.Increment(ref _loadRequestId);

            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _loadCancellationTokenSource.Token;

            _currentPath = path;

            try
            {
                // Check for virtual protocols to bypass Directory.Exists check
                var protocol = ProtocolManager.Parse(path);
                bool isVirtual = protocol.Type != ProtocolType.Local;

                if (string.IsNullOrEmpty(path) || (!isVirtual && !Directory.Exists(path)))
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    await _dispatcher.InvokeAsync(() =>
                    {
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            Files.Clear();
                        }
                    }, DispatcherPriority.Normal);
                    SetupFileWatcher(null);
                    return;
                }

                IsLoading = true;

                // 异步加载文件列表
                var files = await _fileListService.LoadFileSystemItemsAsync(
                    path,
                    null,
                    cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested) return;

                var sortedFiles = ApplySorting(files);

                if (cancellationToken.IsCancellationRequested) return;

                await _dispatcher.InvokeAsync(() =>
                {
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        // 保留旧文件的 Thumbnail，避免集合替换后图标闪烁
                        // （ThumbnailService 会异步加载，但已有缓存的图标可以直接复用）
                        var oldThumbnails = new Dictionary<string, System.Windows.Media.Imaging.BitmapSource>(
                            StringComparer.OrdinalIgnoreCase);
                        foreach (var old in _files)
                        {
                            if (old.Thumbnail != null && !string.IsNullOrEmpty(old.Path))
                                oldThumbnails[old.Path] = old.Thumbnail;
                        }

                        foreach (var item in sortedFiles)
                        {
                            if (oldThumbnails.TryGetValue(item.Path, out var thumb))
                                item.Thumbnail = thumb;
                        }

                        Files = new ObservableCollection<FileSystemItem>(sortedFiles);
                    }
                }, DispatcherPriority.Background);

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
                if (!cancellationToken.IsCancellationRequested)
                {
                    _dialogService?.ShowError($"加载文件列表失败: {ex.Message}");
                }
            }
            finally
            {
                if (_loadRequestId == requestId)
                {
                    IsLoading = false;
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
            _currentPath = null;

            var items = files?.ToList() ?? new List<FileSystemItem>();
            var sorted = ApplySorting(items);

            _dispatcher.Invoke(() =>
            {
                Files = new ObservableCollection<FileSystemItem>(sorted);
                SetupFileWatcher(null);
            });

            // 对于手动设置的文件列表（如搜索结果），我们在此启动增强和计算
            var cts = new CancellationTokenSource();
            _ = _metadataEnricher.EnrichAsync(items.Count <= 500 ? items : items.GetRange(0, 500), cts.Token, _dispatcher, null, null);
            _fileListService.EnqueueFolderSizeCalculations(items, cts.Token);
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
                        long sizeA = a.SizeBytes;
                        long sizeB = b.SizeBytes;
                        return sortAscending ? sizeA.CompareTo(sizeB) : sizeB.CompareTo(sizeA);
                    });
                    break;
                case "ModifiedDate":
                    files.Sort((a, b) =>
                    {
                        var dateA = a.ModifiedDateTime;
                        var dateB = b.ModifiedDateTime;
                        return sortAscending ? dateA.CompareTo(dateB) : dateB.CompareTo(dateA);
                    });
                    break;
            }

            LastSortColumn = sortColumn;
            SortAscending = sortAscending;
            return files;
        }

        public void Dispose()
        {
            _messageBus?.Unsubscribe<FileTagsChangedMessage>(OnFileTagsChanged);
            _messageBus?.Unsubscribe<NotesUpdatedMessage>(OnNotesUpdated);
            _fileWatcherService?.Dispose();

            _refreshDebounceTimer?.Stop();
            CancelOngoingOperations();
        }

        private void OnFileTagsChanged(FileTagsChangedMessage msg)
        {
            if (string.IsNullOrEmpty(msg.FilePath)) return;

            _dispatcher.BeginInvoke(new Action(() =>
            {
                var item = _filesByPath.TryGetValue(msg.FilePath, out var found) ? found : null;
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
                var item = _filesByPath.TryGetValue(msg.FilePath, out var found) ? found : null;
                if (item != null)
                {
                    item.Notes = msg.Notes;
                }
            }), DispatcherPriority.Background);
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
    }
}


