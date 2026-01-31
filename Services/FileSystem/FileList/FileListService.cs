using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using YiboFile.Models;
using YiboFile.Services.FileNotes;
using YiboFile.Services.FileSystem;
using YiboFile.Services.Core;
using YiboFile.Services.Features;
using YiboFile;

namespace YiboFile.Services.FileList
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
        private readonly YiboFile.Services.Core.Error.ErrorService _errorService;
        private readonly ITagService _tagService;
        private readonly FolderSizeCalculator _folderSizeCalculator;
        private readonly FileMetadataEnricher _metadataEnricher;
        private readonly FolderSizeCalculationService _folderSizeCalculationService;

        #endregion

        #region 加载状态字段



        /// <summary>
        /// 是否显示完整文件名（包括扩展名）
        /// 当空间不足隐藏类型列时设置为 true
        /// </summary>
        public bool ShowFullFileName { get; set; } = false; // 列表模式默认不显示扩展名
        /// <summary>
        /// 信号量，用于控制并发加载
        /// </summary>
        private readonly SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(1, 1);

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



        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 FileListService
        /// </summary>
        /// <param name="dispatcher">UI线程调度器，用于更新UI</param>
        /// <param name="errorService">统一错误处理服务</param>
        /// <param name="tagService">标签服务（可选）</param>
        public FileListService(Dispatcher dispatcher, YiboFile.Services.Core.Error.ErrorService errorService, ITagService tagService = null)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _errorService = errorService ?? throw new ArgumentNullException(nameof(errorService));
            _tagService = tagService;
            _folderSizeCalculator = new FolderSizeCalculator();
            _metadataEnricher = new FileMetadataEnricher(_tagService);
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
        /// <summary>
        /// 从指定路径加载文件和文件夹列表
        /// </summary>
        /// <param name="path">要加载的路径</param>
        /// <param name="getFolderSizeCache">获取文件夹大小缓存的函数（可选）</param>
        /// <param name="formatFileSize">格式化文件大小的函数（可选）</param>
        /// <returns>文件系统项列表</returns>
        [Obsolete("Use LoadFileSystemItemsAsync instead")]
        public List<FileSystemItem> LoadFileSystemItems(
            string path,
            Func<string, long?> getFolderSizeCache = null,
            Func<long, string> formatFileSize = null)
        {
            // 拦截搜索路径
            var protocolInfo = ProtocolManager.Parse(path);
            if (protocolInfo.Type == ProtocolType.Search ||
                protocolInfo.Type == ProtocolType.ContentSearch)
            {
                return new List<FileSystemItem>();
            }

            // 同步等待信号量，防止与异步加载冲突
            _loadingSemaphore.Wait();
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    return new List<FileSystemItem>();
                }

                if (!Directory.Exists(path))
                {
                    // _errorService.ReportError($"路径不存在: {path}", YiboFile.Services.Core.Error.ErrorSeverity.Warning);
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
            finally
            {
                _loadingSemaphore.Release();
            }
        }

        /// <summary>
        /// 从多个路径加载文件系统项，合并结果（同名项保留第一个）
        /// </summary>
        /// <param name="paths">要加载的路径列表</param>
        /// <param name="getFolderSizeCache">获取文件夹大小缓存的函数（可选）</param>
        /// <param name="formatFileSize">格式化文件大小的函数（可选）</param>
        /// <returns>合并后的文件系统项列表</returns>
        [Obsolete("Use LoadFileSystemItemsFromMultiplePathsAsync instead")]
        public List<FileSystemItem> LoadFileSystemItemsFromMultiplePaths(
            IEnumerable<string> paths,
            Func<string, long?> getFolderSizeCache = null,
            Func<long, string> formatFileSize = null)
        {
            var task = LoadFileSystemItemsFromMultiplePathsAsync(paths, getFolderSizeCache, formatFileSize);
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// 异步从多个路径加载文件系统项，合并结果（同名项保留第一个）
        /// </summary>
        /// <param name="paths">要加载的路径列表</param>
        /// <param name="getFolderSizeCache">获取文件夹大小缓存的函数（可选）</param>
        /// <param name="formatFileSize">格式化文件大小的函数（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <returns>合并后的文件系统项列表</returns>
        public async Task<List<FileSystemItem>> LoadFileSystemItemsFromMultiplePathsAsync(
            IEnumerable<string> paths,
            Func<string, long?> getFolderSizeCache = null,
            Func<long, string> formatFileSize = null,
            CancellationToken cancellationToken = default)
        {
            var allItems = new Dictionary<string, FileSystemItem>();

            foreach (var path in paths)
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }

                try
                {
                    // 使用异步方法
                    // 注意：这里串行加载，如果路径很多可能慢，但对于库来说通常只有几个路径
                    var items = await LoadFileSystemItemsAsync(path, null, cancellationToken);

                    foreach (var item in items)
                    {
                        var key = item.Name.ToLowerInvariant();
                        if (!allItems.ContainsKey(key))
                        {
                            allItems[key] = item;
                        }
                    }
                }
                catch (Exception)
                {
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
        /// 获取文件的显示名称
        /// 对于只有扩展名的文件（如.gitconfig），总是显示完整文件名
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="showFullFileName">是否显示完整文件名（用于缩略图模式）</param>
        /// <returns>文件显示名称</returns>
        private string GetDisplayFileName(string filePath, bool showFullFileName)
        {
            string fileName = Path.GetFileName(filePath);

            // 如果要求显示完整文件名（缩略图模式），直接返回
            if (showFullFileName)
                return fileName;

            // 尝试去掉扩展名
            string nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);

            // 如果去掉扩展名后为空（如.gitconfig），返回完整文件名
            if (string.IsNullOrEmpty(nameWithoutExt))
                return fileName;

            // 否则返回不含扩展名的文件名
            return nameWithoutExt;
        }

        private readonly YiboFile.Services.Archive.ArchiveService _archiveService = new YiboFile.Services.Archive.ArchiveService();

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
            System.Diagnostics.Debug.WriteLine($"[FileListService] LoadFileSystemItemsAsync called for: {path}");

            // 拦截搜索路径，防止 Directory.GetDirectories 抛出异常
            var protocolInfo = ProtocolManager.Parse(path);
            System.Diagnostics.Debug.WriteLine($"[FileListService] Protocol parsed: Type={protocolInfo.Type}, Target={protocolInfo.TargetPath}");

            if (protocolInfo.Type == ProtocolType.Search ||
                protocolInfo.Type == ProtocolType.ContentSearch)
            {
                return new List<FileSystemItem>();
            }

            // [Archive Support] Check for Archive Paths
            // Case 1: Path is "zip://..." -> Virtual Path
            if (protocolInfo.Type == ProtocolType.Archive)
            {
                return await _archiveService.GetArchiveContentAsync(protocolInfo.TargetPath, protocolInfo.ExtraData);
            }

            // Case 1.5: Path is "library://..." -> Virtual Path
            if (protocolInfo.Type == ProtocolType.Library)
            {
                var libraryName = protocolInfo.TargetPath;
                var libRepo = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<YiboFile.Services.Data.Repositories.ILibraryRepository>(App.ServiceProvider);
                var allLibs = await Task.Run(() => libRepo.GetAllLibraries(), cancellationToken);

                if (string.IsNullOrEmpty(libraryName))
                {
                    return new List<FileSystemItem>();
                }

                var lib = allLibs.FirstOrDefault(l => l.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase));
                if (lib != null && lib.Paths != null)
                {
                    return await LoadFileSystemItemsFromMultiplePathsAsync(
                        lib.Paths,
                        p => DatabaseManager.GetFolderSize(p),
                        FormatFileSize,
                        cancellationToken);
                }
                return new List<FileSystemItem>();
            }

            // Case 2: Path is "tag://..." -> Virtual Path
            if (protocolInfo.Type == ProtocolType.Tag)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] Handling Tag protocol: {protocolInfo.TargetPath}");
                var files = new List<FileSystemItem>();
                try
                {
                    // TargetPath is the tag NAME (e.g., "111" from "tag://111")
                    var tagName = protocolInfo.TargetPath;
                    List<string> filePaths;

                    // Primary: Query by tag name
                    if (_tagService != null)
                    {
                        var filesEnumerable = await _tagService.GetFilesByTagNameAsync(tagName);
                        filePaths = filesEnumerable.ToList();

                        // Fallback: If no results and it looks like an ID, try by ID for backward compatibility
                        if ((filePaths == null || filePaths.Count == 0) && int.TryParse(tagName, out int tagId))
                        {
                            var filesById = await _tagService.GetFilesByTagAsync(tagId);
                            filePaths = filesById.ToList();
                        }
                    }
                    else
                    {
                        filePaths = new List<string>();
                    }
                    foreach (var filePath in filePaths)
                    {
                        if (cancellationToken.IsCancellationRequested) break;

                        bool itemIsFile = File.Exists(filePath);
                        bool itemIsDir = !itemIsFile && Directory.Exists(filePath);

                        if (!itemIsFile && !itemIsDir) continue;

                        try
                        {
                            if (itemIsFile)
                            {
                                var fileInfo = new System.IO.FileInfo(filePath);
                                files.Add(new FileSystemItem
                                {
                                    Name = GetDisplayFileName(filePath, ShowFullFileName),
                                    Path = fileInfo.FullName,
                                    Type = !string.IsNullOrEmpty(fileInfo.Extension) ? fileInfo.Extension : "文件",
                                    Size = FormatFileSize(fileInfo.Length),
                                    ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd"),
                                    CreatedTime = FileSystemItem.FormatTimeAgo(fileInfo.CreationTime),
                                    IsDirectory = false,
                                    SourcePath = path,
                                    SizeBytes = fileInfo.Length,
                                    ModifiedDateTime = fileInfo.LastWriteTime,
                                    CreatedDateTime = fileInfo.CreationTime
                                });
                            }
                            else if (itemIsDir)
                            {
                                var dirInfo = new System.IO.DirectoryInfo(filePath);
                                files.Add(new FileSystemItem
                                {
                                    Name = dirInfo.Name,
                                    Path = dirInfo.FullName,
                                    Type = "文件夹",
                                    Size = "-",
                                    ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd"),
                                    CreatedTime = FileSystemItem.FormatTimeAgo(dirInfo.CreationTime),
                                    IsDirectory = true,
                                    SourcePath = path,
                                    SizeBytes = 0,
                                    ModifiedDateTime = dirInfo.LastWriteTime,
                                    CreatedDateTime = dirInfo.CreationTime
                                });
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    YiboFile.Services.Core.FileLogger.LogException($"[FileListService] Error loading tag files", ex);
                }

                // Trigger metadata enrichment for these files
                // We need to do this manually because we return early
                _metadataEnrichmentCancellation = new CancellationTokenSource();
                var combinedMetadataToken = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    _metadataEnrichmentCancellation.Token).Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _metadataEnricher.EnrichAsync(
                            files,
                            combinedMetadataToken,
                            _dispatcher,
                            orderTagNames,
                            () => MetadataEnriched?.Invoke(this, files));
                    }
                    catch { }
                }, combinedMetadataToken);

                return files;
            }

            // Case 3: Path is a physical file that is an archive (User tried to "open" it like a folder)
            // But FileListService is usually called with a Directory path. 
            // If the UI passes a File Path here, it means we want to open it.
            // We need to check if 'path' refers to an existing FILE, and if it is an archive.
            bool isFile = File.Exists(path);
            bool isDir = Directory.Exists(path);

            if (isFile && !isDir)
            {
                if (_archiveService.IsArchive(path))
                {
                    // Redirect to root of archive
                    return await _archiveService.GetArchiveContentAsync(path, "");
                }
            }

            // 等待获取信号量，支持取消
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] Waiting for semaphore for: {path}");
                await _loadingSemaphore.WaitAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"[FileListService] Semaphore acquired for: {path}");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] Semaphore wait cancelled for: {path}");
                throw;
            }

            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new List<FileSystemItem>();
                }



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
                        YiboFile.Services.Core.FileLogger.LogException($"[FileListService] 文件夹大小计算失败", ex);
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
                        YiboFile.Services.Core.FileLogger.LogException($"[FileListService] 元数据加载失败", ex);
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
                _errorService.ReportError(errorMessage);
                return new List<FileSystemItem>();
            }
            catch (DirectoryNotFoundException)
            {
                // var errorMessage = $"路径不存在: {path}";
                // if (!string.IsNullOrEmpty(ex.Message))
                // {
                //     errorMessage += $"\n{ex.Message}";
                // }
                // _errorService.ReportError(errorMessage);
                return new List<FileSystemItem>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var errorMessage = $"加载文件列表失败: {path}";
                if (!string.IsNullOrEmpty(ex.Message))
                {
                    errorMessage += $"\n{ex.Message}";
                }
                _errorService.ReportError(errorMessage);
                return new List<FileSystemItem>();
            }
            finally
            {

                _loadingSemaphore.Release();
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
            catch (Exception)
            {
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
            IEnumerable<YiboFile.Models.FileSystemItem> items,
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
            catch (Exception)
            {
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
            _loadingSemaphore?.Dispose();

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
                string[] dirPaths;
                try
                {
                    dirPaths = await Task.Run(() => Directory.GetDirectories(path), cancellationToken);
                }
                catch (UnauthorizedAccessException ex)
                {
                    // 通知用户无权限访问
                    _errorService.ReportError($"无权限访问文件夹: {path}", YiboFile.Services.Core.Error.ErrorSeverity.Warning, ex);
                    return directories;
                }

                foreach (var dirPath in dirPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        string actualPath = dirPath;
                        string displayName = Path.GetFileName(dirPath);

                        // 检测并解析符号链接/Junction Points
                        if (SymbolicLinkHelper.IsSymbolicLink(dirPath))
                        {
                            string targetPath = SymbolicLinkHelper.GetSymbolicLinkTarget(dirPath);
                            if (!string.IsNullOrEmpty(targetPath) && targetPath != dirPath)
                            {
                                actualPath = targetPath;
                            }
                        }

                        // 检查文件夹是否存在（如果不存在，清理数据库缓存）
                        if (!Directory.Exists(actualPath))
                        {
                            DatabaseManager.RemoveFolderSize(dirPath);
                            continue;
                        }

                        var dirInfo = new DirectoryInfo(actualPath);

                        // 从数据库读取文件夹大小缓存
                        string sizeDisplay = "计算中...";
                        long? cachedSize = null;
                        if (getFolderSizeCache != null)
                        {
                            // 移除 Task.Run，直接同步读取缓存以提高加载速度
                            // loop 中 await Task.Run 会导致严重的性能开销
                            cachedSize = getFolderSizeCache(actualPath);
                            if (cachedSize.HasValue)
                            {
                                sizeDisplay = formatFileSize(cachedSize.Value);
                            }
                        }

                        directories.Add(new FileSystemItem
                        {
                            Name = displayName,
                            Path = actualPath,
                            Type = "文件夹",
                            Size = sizeDisplay,
                            ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd"),
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
                        continue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
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
                            // 对于只有扩展名的文件（如.gitconfig），总是显示完整文件名
                            Name = GetDisplayFileName(filePath, ShowFullFileName),
                            Path = fileInfo.FullName,
                            Type = !string.IsNullOrEmpty(fileInfo.Extension) ? fileInfo.Extension : "文件",
                            Size = formatFileSize(fileInfo.Length),
                            ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd"),
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
                        continue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
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
                        string actualPath = dirPath;
                        string displayName = Path.GetFileName(dirPath);

                        // 检测并解析符号链接/Junction Points
                        if (SymbolicLinkHelper.IsSymbolicLink(dirPath))
                        {
                            string targetPath = SymbolicLinkHelper.GetSymbolicLinkTarget(dirPath);
                            if (!string.IsNullOrEmpty(targetPath) && targetPath != dirPath)
                            {
                                actualPath = targetPath;
                            }
                        }

                        // 检查文件夹是否存在（如果不存在，清理数据库缓存）
                        if (!Directory.Exists(actualPath))
                        {
                            DatabaseManager.RemoveFolderSize(dirPath);
                            continue;
                        }

                        var dirInfo = new DirectoryInfo(actualPath);

                        // 从数据库读取文件夹大小缓存
                        string sizeDisplay = "计算中...";
                        if (getFolderSizeCache != null)
                        {
                            var cachedSize = getFolderSizeCache(actualPath);
                            if (cachedSize.HasValue)
                            {
                                sizeDisplay = formatFileSize(cachedSize.Value);
                            }
                        }

                        directories.Add(new FileSystemItem
                        {
                            Name = displayName,
                            Path = actualPath,
                            Type = "文件夹",
                            Size = sizeDisplay,
                            ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd"),
                            CreatedTime = FileSystemItem.FormatTimeAgo(dirInfo.CreationTime),
                            IsDirectory = true,
                            SourcePath = path, // 标记来源路径
                            ModifiedDateTime = dirInfo.LastWriteTime,
                            CreatedDateTime = dirInfo.CreationTime
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (Exception)
            {
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
                            // 对于只有扩展名的文件（如.gitconfig），总是显示完整文件名
                            Name = GetDisplayFileName(filePath, ShowFullFileName),
                            Path = fileInfo.FullName,
                            Type = !string.IsNullOrEmpty(fileInfo.Extension) ? fileInfo.Extension : "文件",
                            Size = formatFileSize(fileInfo.Length),
                            ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd"),
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
                        continue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (Exception)
            {
            }

            return files;
        }

        /// <summary>
        /// 根据路径创建单个文件系统项（支持文件和文件夹）
        /// </summary>
        public FileSystemItem CreateFileSystemItem(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (System.IO.Directory.Exists(path))
            {
                var dirInfo = new System.IO.DirectoryInfo(path);
                return new FileSystemItem
                {
                    Name = System.IO.Path.GetFileName(path),
                    Path = path,
                    Type = "文件夹",
                    Size = FormatFileSize(DatabaseManager.GetFolderSize(path) ?? 0),
                    ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd"),
                    CreatedTime = FileSystemItem.FormatTimeAgo(dirInfo.CreationTime),
                    IsDirectory = true,
                    ModifiedDateTime = dirInfo.LastWriteTime,
                    CreatedDateTime = dirInfo.CreationTime
                };
            }
            else if (System.IO.File.Exists(path))
            {
                var fileInfo = new System.IO.FileInfo(path);
                return new FileSystemItem
                {
                    Name = GetDisplayFileName(path, ShowFullFileName),
                    Path = path,
                    Type = !string.IsNullOrEmpty(fileInfo.Extension) ? fileInfo.Extension : "文件",
                    Size = FormatFileSize(fileInfo.Length),
                    ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd"),
                    CreatedTime = FileSystemItem.FormatTimeAgo(fileInfo.CreationTime),
                    IsDirectory = false,
                    SizeBytes = fileInfo.Length,
                    ModifiedDateTime = fileInfo.LastWriteTime,
                    CreatedDateTime = fileInfo.CreationTime
                };
            }
            return null;
        }

        #endregion
    }
}










