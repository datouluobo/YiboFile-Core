using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using OoiMRR.Services.FileNotes;

namespace OoiMRR.Services.FileList
{
    /// <summary>
    /// 文件列表加载服务
    /// 负责从文件系统加载文件和文件夹列表，创建 FileSystemItem 对象
    /// 提供文件夹大小计算、异步加载标签和备注等功能
    /// </summary>
    public class FileListService : IDisposable
    {
        #region 依赖注入字段

        private readonly Dispatcher _dispatcher;
        private readonly FolderSizeCalculator _folderSizeCalculator;
        private readonly FileMetadataEnricher _metadataEnricher;
        private readonly FolderSizeCalculationService _folderSizeCalculationService;

        #endregion

        #region 加载状态字段

        private bool _isLoadingFiles = false;
        private readonly object _loadingLock = new object();

        #endregion

        #region 文件夹大小计算字段

        private CancellationTokenSource _folderSizeCalculationCancellation;
        private readonly SemaphoreSlim _folderSizeCalculationSemaphore = new SemaphoreSlim(1, 1);

        #endregion

        #region 异步加载字段

        private CancellationTokenSource _metadataEnrichmentCancellation;
        private Func<List<int>, List<string>> _orderTagNames;

        #endregion

        #region 事件定义

        /// <summary>
        /// 文件列表加载完成事件
        /// </summary>
        public event EventHandler<List<FileSystemItem>> FilesLoaded;

        /// <summary>
        /// 文件夹大小计算完成事件
        /// </summary>
        public event EventHandler<FileSystemItem> FolderSizeCalculated;

        /// <summary>
        /// 元数据加载完成事件（标签和备注）
        /// </summary>
        public event EventHandler<List<FileSystemItem>> MetadataEnriched;

        /// <summary>
        /// 错误发生事件
        /// </summary>
        public event EventHandler<string> ErrorOccurred;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 FileListService
        /// </summary>
        /// <param name="dispatcher">UI线程调度器，用于更新UI</param>
        public FileListService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _folderSizeCalculator = new FolderSizeCalculator();
            _metadataEnricher = new FileMetadataEnricher();
            _folderSizeCalculationService = new FolderSizeCalculationService();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 从指定路径加载文件和文件夹列表
        /// </summary>
        /// <param name="path">要加载的路径</param>
        /// <param name="getFolderSizeCache">获取文件夹大小缓存的函数（可选）</param>
        /// <param name="formatFileSize">格式化文件大小的函数（可选）</param>
        /// <returns>文件系统项列表</returns>
        public List<FileSystemItem> LoadFileSystemItems(
            string path,
            Func<string, long?> getFolderSizeCache = null,
            Func<long, string> formatFileSize = null)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return new List<FileSystemItem>();
            }

            // 使用默认的格式化函数
            if (formatFileSize == null)
            {
                formatFileSize = FormatFileSize;
            }

            var items = new List<FileSystemItem>();

            // 加载文件夹
            var directories = LoadDirectories(path, getFolderSizeCache, formatFileSize);
            items.AddRange(directories);

            // 加载文件
            var files = LoadFiles(path, formatFileSize);
            items.AddRange(files);

            return items;
        }

        /// <summary>
        /// 从多个路径加载文件系统项，合并结果（同名项保留第一个）
        /// </summary>
        /// <param name="paths">要加载的路径列表</param>
        /// <param name="getFolderSizeCache">获取文件夹大小缓存的函数（可选）</param>
        /// <param name="formatFileSize">格式化文件大小的函数（可选）</param>
        /// <returns>合并后的文件系统项列表</returns>
        public List<FileSystemItem> LoadFileSystemItemsFromMultiplePaths(
            IEnumerable<string> paths,
            Func<string, long?> getFolderSizeCache = null,
            Func<long, string> formatFileSize = null)
        {
            var allItems = new Dictionary<string, FileSystemItem>();

            foreach (var path in paths)
            {
                if (!Directory.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"[FileListService] 路径不存在: {path}");
                    continue;
                }

                try
                {
                    var items = LoadFileSystemItems(path, getFolderSizeCache, formatFileSize);
                    foreach (var item in items)
                    {
                        var key = item.Name.ToLowerInvariant();
                        if (!allItems.ContainsKey(key))
                        {
                            allItems[key] = item;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileListService] 加载路径失败 {path}: {ex.Message}");
                }
            }

            return allItems.Values.ToList();
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>格式化后的字符串</returns>
        public string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 异步加载文件系统项，包含文件夹大小计算和元数据加载
        /// </summary>
        /// <param name="path">要加载的路径</param>
        /// <param name="orderTagNames">标签排序函数（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <returns>文件系统项列表</returns>
        public async Task<List<FileSystemItem>> LoadFileSystemItemsAsync(
            string path,
            Func<List<int>, List<string>> orderTagNames = null,
            CancellationToken cancellationToken = default)
        {
            lock (_loadingLock)
            {
                if (_isLoadingFiles)
                {
                    return new List<FileSystemItem>();
                }
                _isLoadingFiles = true;
            }

            try
            {
                // 异步加载文件和文件夹
                var directories = await LoadDirectoriesAsync(
                    path,
                    p => DatabaseManager.GetFolderSize(p),
                    FormatFileSize,
                    cancellationToken);

                var files = await LoadFilesAsync(
                    path,
                    FormatFileSize,
                    cancellationToken);

                var items = new List<FileSystemItem>();
                items.AddRange(directories);
                items.AddRange(files);

                // 触发加载完成事件
                FilesLoaded?.Invoke(this, items);

                // 异步计算文件夹大小
                _folderSizeCalculationCancellation = new CancellationTokenSource();
                var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _folderSizeCalculationCancellation.Token).Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _folderSizeCalculator.CalculateAsync(
                            items,
                            combinedToken,
                            _dispatcher,
                            FormatFileSize,
                            () => { });
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        OoiMRR.Services.Core.FileLogger.LogException($"[FileListService] 文件夹大小计算失败", ex);
                    }
                }, combinedToken);

                // 异步加载标签和备注
                _metadataEnrichmentCancellation = new CancellationTokenSource();
                _orderTagNames = orderTagNames;
                var combinedMetadataToken = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _metadataEnrichmentCancellation.Token).Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _metadataEnricher.EnrichAsync(
                            items,
                            combinedMetadataToken,
                            _dispatcher,
                            orderTagNames,
                            () => MetadataEnriched?.Invoke(this, items));
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        OoiMRR.Services.Core.FileLogger.LogException($"[FileListService] 元数据加载失败", ex);
                    }
                }, combinedMetadataToken);

                return items;
            }
            catch (UnauthorizedAccessException ex)
            {
                var errorMessage = $"无权限访问路径: {path}";
                if (!string.IsNullOrEmpty(ex.Message))
                {
                    errorMessage += $"\n{ex.Message}";
                }
                ErrorOccurred?.Invoke(this, errorMessage);
                return new List<FileSystemItem>();
            }
            catch (DirectoryNotFoundException ex)
            {
                var errorMessage = $"路径不存在: {path}";
                if (!string.IsNullOrEmpty(ex.Message))
                {
                    errorMessage += $"\n{ex.Message}";
                }
                ErrorOccurred?.Invoke(this, errorMessage);
                return new List<FileSystemItem>();
            }
            catch (Exception ex)
            {
                var errorMessage = $"加载文件列表失败: {path}";
                if (!string.IsNullOrEmpty(ex.Message))
                {
                    errorMessage += $"\n{ex.Message}";
                }
                ErrorOccurred?.Invoke(this, errorMessage);
                System.Diagnostics.Debug.WriteLine($"[FileListService] {errorMessage}");
                return new List<FileSystemItem>();
            }
            finally
            {
                lock (_loadingLock)
                {
                    _isLoadingFiles = false;
                }
            }
        }

        #endregion

        #region 文件夹大小计算方法

        /// <summary>
        /// 异步计算并更新文件夹大小
        /// </summary>
        /// <param name="item">文件夹项</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        public async Task CalculateFolderSizeAsync(
            FileSystemItem item,
            CancellationToken cancellationToken = default)
        {
            if (item == null || !item.IsDirectory || string.IsNullOrEmpty(item.Path))
            {
                return;
            }

            if (!Directory.Exists(item.Path))
            {
                DatabaseManager.RemoveFolderSize(item.Path);
                return;
            }

            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _folderSizeCalculationCancellation?.Token ?? CancellationToken.None).Token;

            await _folderSizeCalculationSemaphore.WaitAsync(combinedToken);
            try
            {
                if (combinedToken.IsCancellationRequested)
                {
                    return;
                }

                var size = await Task.Run(() =>
                {
                    return _folderSizeCalculationService.CalculateDirectorySize(item.Path, combinedToken);
                }, combinedToken);

                if (combinedToken.IsCancellationRequested)
                {
                    return;
                }

                // 检查缓存大小，如果不同则更新
                var cachedSize = DatabaseManager.GetFolderSize(item.Path);
                if (!cachedSize.HasValue || cachedSize.Value != size)
                {
                    DatabaseManager.SetFolderSize(item.Path, size);
                }

                var displaySize = FormatFileSize(size);
                await _dispatcher.InvokeAsync(() =>
                {
                    item.Size = displaySize;
                    item.SizeBytes = size;
                    FolderSizeCalculated?.Invoke(this, item);
                }, DispatcherPriority.Background);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 计算文件夹大小失败 {item.Path}: {ex.Message}");
            }
            finally
            {
                _folderSizeCalculationSemaphore.Release();
            }
        }

        #endregion

        #region 异步加载元数据方法

        /// <summary>
        /// 异步加载文件标签和备注
        /// </summary>
        /// <param name="items">文件系统项列表</param>
        /// <param name="orderTagNames">标签排序函数（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        public async Task EnrichMetadataAsync(
            IEnumerable<FileSystemItem> items,
            Func<List<int>, List<string>> orderTagNames = null,
            CancellationToken cancellationToken = default)
        {
            if (items == null)
            {
                return;
            }

            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _metadataEnrichmentCancellation?.Token ?? CancellationToken.None).Token;

            try
            {
                await _metadataEnricher.EnrichAsync(
                    items,
                    combinedToken,
                    _dispatcher,
                    orderTagNames ?? _orderTagNames,
                    () =>
                    {
                        var itemsList = items.ToList();
                        MetadataEnriched?.Invoke(this, itemsList);
                    });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 元数据加载失败: {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 取消所有正在进行的操作
        /// </summary>
        private void CancelOngoingOperations()
        {
            _folderSizeCalculationCancellation?.Cancel();
            _folderSizeCalculationCancellation?.Dispose();
            _folderSizeCalculationCancellation = null;

            _metadataEnrichmentCancellation?.Cancel();
            _metadataEnrichmentCancellation?.Dispose();
            _metadataEnrichmentCancellation = null;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 取消所有操作
            CancelOngoingOperations();

            // 释放信号量
            _folderSizeCalculationSemaphore?.Dispose();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 异步加载文件夹列表
        /// </summary>
        private async Task<List<FileSystemItem>> LoadDirectoriesAsync(
            string path,
            Func<string, long?> getFolderSizeCache,
            Func<long, string> formatFileSize,
            CancellationToken cancellationToken = default)
        {
            var directories = new List<FileSystemItem>();
            try
            {
                var dirPaths = await Task.Run(() => Directory.GetDirectories(path), cancellationToken);
                foreach (var dirPath in dirPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        // 检查文件夹是否存在（如果不存在，清理数据库缓存）
                        if (!Directory.Exists(dirPath))
                        {
                            DatabaseManager.RemoveFolderSize(dirPath);
                            continue;
                        }

                        var dirInfo = new DirectoryInfo(dirPath);

                        // 从数据库读取文件夹大小缓存
                        string sizeDisplay = "计算中...";
                        long? cachedSize = null; // Decalre outside scope
                        if (getFolderSizeCache != null)
                        {
                            cachedSize = await Task.Run(() => getFolderSizeCache(dirPath), cancellationToken);
                            if (cachedSize.HasValue)
                            {
                                sizeDisplay = formatFileSize(cachedSize.Value);
                            }
                        }

                        directories.Add(new FileSystemItem
                        {
                            Name = Path.GetFileName(dirPath),
                            Path = dirInfo.FullName,
                            Type = "文件夹",
                            Size = sizeDisplay,
                            ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            CreatedTime = FileSystemItem.FormatTimeAgo(dirInfo.CreationTime),
                            IsDirectory = true,
                            SourcePath = path, // 标记来源路径
                            SizeBytes = cachedSize ?? -1,
                            ModifiedDateTime = dirInfo.LastWriteTime,
                            CreatedDateTime = dirInfo.CreationTime
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问文件夹: {dirPath}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 处理文件夹失败 {dirPath}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问路径: {path}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 获取文件夹列表失败 {path}: {ex.Message}");
            }

            return directories;
        }

        /// <summary>
        /// 异步加载文件列表
        /// </summary>
        private async Task<List<FileSystemItem>> LoadFilesAsync(
            string path,
            Func<long, string> formatFileSize,
            CancellationToken cancellationToken = default)
        {
            var files = new List<FileSystemItem>();
            try
            {
                var filePaths = await Task.Run(() => Directory.GetFiles(path), cancellationToken);
                foreach (var filePath in filePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var fileInfo = new System.IO.FileInfo(filePath);
                        files.Add(new FileSystemItem
                        {
                            Name = Path.GetFileName(filePath),
                            Path = fileInfo.FullName,
                            Type = FileTypeManager.GetFileCategory(fileInfo.FullName),
                            Size = formatFileSize(fileInfo.Length),
                            ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            CreatedTime = FileSystemItem.FormatTimeAgo(fileInfo.CreationTime),
                            IsDirectory = false,
                            SourcePath = path, // 标记来源路径
                            SizeBytes = fileInfo.Length,
                            ModifiedDateTime = fileInfo.LastWriteTime,
                            CreatedDateTime = fileInfo.CreationTime
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问文件: {filePath}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 处理文件失败 {filePath}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问路径: {path}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 获取文件列表失败 {path}: {ex.Message}");
            }

            return files;
        }

        /// <summary>
        /// 加载文件夹列表（同步版本，保留以兼容现有代码）
        /// </summary>
        private List<FileSystemItem> LoadDirectories(
            string path,
            Func<string, long?> getFolderSizeCache,
            Func<long, string> formatFileSize)
        {
            var directories = new List<FileSystemItem>();
            try
            {
                var dirPaths = Directory.GetDirectories(path);
                foreach (var dirPath in dirPaths)
                {
                    try
                    {
                        // 检查文件夹是否存在（如果不存在，清理数据库缓存）
                        if (!Directory.Exists(dirPath))
                        {
                            DatabaseManager.RemoveFolderSize(dirPath);
                            continue;
                        }

                        var dirInfo = new DirectoryInfo(dirPath);

                        // 从数据库读取文件夹大小缓存
                        string sizeDisplay = "计算中...";
                        if (getFolderSizeCache != null)
                        {
                            var cachedSize = getFolderSizeCache(dirPath);
                            if (cachedSize.HasValue)
                            {
                                sizeDisplay = formatFileSize(cachedSize.Value);
                            }
                        }

                        directories.Add(new FileSystemItem
                        {
                            Name = Path.GetFileName(dirPath),
                            Path = dirInfo.FullName,
                            Type = "文件夹",
                            Size = sizeDisplay,
                            ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            CreatedTime = FileSystemItem.FormatTimeAgo(dirInfo.CreationTime),
                            IsDirectory = true,
                            SourcePath = path, // 标记来源路径
                            ModifiedDateTime = dirInfo.LastWriteTime,
                            CreatedDateTime = dirInfo.CreationTime
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问文件夹: {dirPath}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 处理文件夹失败 {dirPath}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问路径: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 获取文件夹列表失败 {path}: {ex.Message}");
            }

            return directories;
        }

        /// <summary>
        /// 加载文件列表（同步版本，保留以兼容现有代码）
        /// </summary>
        private List<FileSystemItem> LoadFiles(string path, Func<long, string> formatFileSize)
        {
            var files = new List<FileSystemItem>();
            try
            {
                var filePaths = Directory.GetFiles(path);
                foreach (var filePath in filePaths)
                {
                    try
                    {
                        var fileInfo = new System.IO.FileInfo(filePath);
                        files.Add(new FileSystemItem
                        {
                            Name = Path.GetFileName(filePath),
                            Path = fileInfo.FullName,
                            Type = FileTypeManager.GetFileCategory(fileInfo.FullName),
                            Size = formatFileSize(fileInfo.Length),
                            ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            CreatedTime = FileSystemItem.FormatTimeAgo(fileInfo.CreationTime),
                            IsDirectory = false,
                            SourcePath = path, // 标记来源路径
                            SizeBytes = fileInfo.Length,
                            ModifiedDateTime = fileInfo.LastWriteTime,
                            CreatedDateTime = fileInfo.CreationTime
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问文件: {filePath}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 处理文件失败 {filePath}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问路径: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 获取文件列表失败 {path}: {ex.Message}");
            }

            return files;
        }

        #endregion
    }
}









