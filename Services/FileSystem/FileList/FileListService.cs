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
            if (paths == null || !paths.Any()) return new List<FileSystemItem>();

            var results = new List<List<FileSystemItem>>();
            foreach (var path in paths)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // Use Task.Run for Directory.Exists to avoid UI blocking if path is network drive
                    if (!await Task.Run(() => Directory.Exists(path), cancellationToken).ConfigureAwait(false))
                    {
                        continue;
                    }

                    // 加载文件项
                    var items = await LoadFileSystemItemsAsync(path, null, cancellationToken, false, true).ConfigureAwait(false);
                    results.Add(items);
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileListService] 路径被拒绝: {path} ({ex.Message}). Skip.");
                    _errorService.ReportError($"路径被拒绝: {path} ({ex.Message})", YiboFile.Services.Core.Error.ErrorSeverity.Warning, ex);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileListService] Error loading path {path} in multiple load: {ex.Message}");
                }
            }

            // 合并结果并去重
            var allItems = new Dictionary<string, FileSystemItem>();
            foreach (var items in results)
            {
                if (items == null) continue;
                foreach (var item in items)
                {
                    if (item == null) continue;
                    var key = item.Name?.ToLowerInvariant() ?? "";
                    if (!string.IsNullOrEmpty(key) && !allItems.ContainsKey(key))
                    {
                        allItems[key] = item;
                    }
                }
            }

            return allItems.Values.OrderBy(i => i.IsDirectory ? 0 : 1).ThenBy(i => i.Name ?? string.Empty).ToList();
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
            CancellationToken cancellationToken = default,
            bool resetOngoingOperations = true,
            bool skipBackgroundTasks = false)
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
                return await _archiveService.GetArchiveContentAsync(protocolInfo.TargetPath, protocolInfo.ExtraData).ConfigureAwait(false);
            }

            // Case 1.5: Path is "lib://..." -> Virtual Path
            if (protocolInfo.Type == ProtocolType.Library)
            {
                var fullTarget = protocolInfo.TargetPath;
                if (string.IsNullOrEmpty(fullTarget)) return new List<FileSystemItem>();

                // 拆分库名和可能的子路径 (例如: lib://MyLib/FolderName -> libName="MyLib", subPath="FolderName")
                var parts = fullTarget.Split(new[] { '/', '\\' }, 2);
                var libraryName = parts[0];
                var subPath = parts.Length > 1 ? parts[1] : "";

                var libRepo = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<YiboFile.Services.Data.Repositories.ILibraryRepository>(App.ServiceProvider);
                var allLibs = await Task.Run(() => libRepo.GetAllLibraries(), cancellationToken).ConfigureAwait(false);

                var lib = allLibs.FirstOrDefault(l => l.Name.Equals(libraryName, StringComparison.OrdinalIgnoreCase));
                if (lib != null && lib.Paths != null)
                {
                    // 确保所有路径都是绝对路径，防止相对路径导致找不到文件
                    var absoluteLibPaths = lib.Paths?.Select(p =>
                    {
                        try { return Path.GetFullPath(p); } catch { return p; }
                    }).ToList() ?? new List<string>();

                    // 如果存在子路径，则将子路径附加到库的每个物理路径后
                    var actualPaths = string.IsNullOrEmpty(subPath)
                        ? absoluteLibPaths
                        : absoluteLibPaths.Select(p => Path.Combine(p, subPath)).Where(Directory.Exists).ToList();

                    if (!actualPaths.Any()) return new List<FileSystemItem>();

                    var items = await LoadFileSystemItemsFromMultiplePathsAsync(
                        actualPaths,
                        p => DatabaseManager.GetFolderSize(p),
                        FormatFileSize,
                        cancellationToken).ConfigureAwait(false);

                    // 标记来源，以便之后能正确识别虚拟路径下的层级
                    foreach (var item in items) item.SourcePath = path;

                    // 触发后续的各种事件和后台任务（大小计算、元数据等）
                    return ProcessLoadedItems(items, cancellationToken, resetOngoingOperations, skipBackgroundTasks, orderTagNames);
                }
                return new List<FileSystemItem>();
            }

            // Case 2: Path is "tag://..." -> Virtual Path
            if (protocolInfo.Type == ProtocolType.Tag)
            {
                return await Task.Run(async () =>
                {
                    var files = new List<FileSystemItem>();
                    try
                    {
                        var tagName = protocolInfo.TargetPath;
                        List<string> filePaths = new List<string>();

                        if (_tagService != null)
                        {
                            var filesEnumerable = await _tagService.GetFilesByTagNameAsync(tagName).ConfigureAwait(false);
                            filePaths = filesEnumerable.ToList();

                            if ((filePaths == null || filePaths.Count == 0) && int.TryParse(tagName, out int tagId))
                            {
                                var filesById = await _tagService.GetFilesByTagAsync(tagId).ConfigureAwait(false);
                                filePaths = filesById.ToList();
                            }
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

                    // Metadata enrichment
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
                                () => MetadataEnriched?.Invoke(this, files)).ConfigureAwait(false);
                        }
                        catch { }
                    }, combinedMetadataToken);

                    return files;
                }, cancellationToken).ConfigureAwait(false);
            }

            // Case 3: Path is a physical file that is an archive (User tried to "open" it like a folder)
            bool isFile = File.Exists(path);
            bool isDir = Directory.Exists(path);

            if (isFile && !isDir)
            {
                if (_archiveService.IsArchive(path))
                {
                    // Redirect to root of archive
                    return await _archiveService.GetArchiveContentAsync(path, "").ConfigureAwait(false);
                }
            }

            bool semaphoreAcquired = false;
            try
            {
                // 如果是子路径加载（skipBackgroundTasks = true），跳过信号量获取
                // 这允许库的多路径并行加载，避免死锁
                if (!skipBackgroundTasks)
                {
                    // 等待获取信号量，支持取消
                    await _loadingSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    semaphoreAcquired = true;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return new List<FileSystemItem>();
                }

                // 异步加载文件和文件夹
                var directories = await LoadDirectoriesAsync(
                    path,
                    p => DatabaseManager.GetFolderSize(p),
                    FormatFileSize,
                    cancellationToken).ConfigureAwait(false);

                var files = await LoadFilesAsync(
                    path,
                    FormatFileSize,
                    cancellationToken).ConfigureAwait(false);

                var items = new List<FileSystemItem>();
                items.AddRange(directories);
                items.AddRange(files);

                return ProcessLoadedItems(items, cancellationToken, resetOngoingOperations, skipBackgroundTasks, orderTagNames);
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
                return new List<FileSystemItem>();
            }
            catch (OperationCanceledException)
            {
                return new List<FileSystemItem>();
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
                if (semaphoreAcquired)
                {
                    _loadingSemaphore.Release();
                }
            }
        }

        private List<FileSystemItem> ProcessLoadedItems(
            List<FileSystemItem> items,
            CancellationToken cancellationToken,
            bool resetOngoingOperations,
            bool skipBackgroundTasks,
            Func<List<int>, List<string>> orderTagNames)
        {
            // 触发加载完成事件 (仅当不跳过后台任务时触发，或者是顶层调用)
            // 如果是库子项加载，我们不触发个别完成事件，以免 flooding
            if (!skipBackgroundTasks)
            {
                FilesLoaded?.Invoke(this, items);
            }

            if (skipBackgroundTasks)
            {
                return items;
            }

            // 异步计算文件夹大小
            if (resetOngoingOperations)
            {
                _folderSizeCalculationCancellation?.Cancel();
                _folderSizeCalculationCancellation = new CancellationTokenSource();
            }
            else
            {
                _folderSizeCalculationCancellation ??= new CancellationTokenSource();
            }
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
                        () => { }).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    YiboFile.Services.Core.FileLogger.LogException($"[FileListService] 文件夹大小计算失败", ex);
                }
            }, combinedToken);

            // 异步加载标签和备注
            if (resetOngoingOperations)
            {
                _metadataEnrichmentCancellation?.Cancel();
                _metadataEnrichmentCancellation = new CancellationTokenSource();
            }
            else
            {
                _metadataEnrichmentCancellation ??= new CancellationTokenSource();
            }
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
                        () => MetadataEnriched?.Invoke(this, items)).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    YiboFile.Services.Core.FileLogger.LogException($"[FileListService] 元数据加载失败", ex);
                }
            }, combinedMetadataToken);

            return items;
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

            await _folderSizeCalculationSemaphore.WaitAsync(combinedToken).ConfigureAwait(false);
            try
            {
                if (combinedToken.IsCancellationRequested)
                {
                    return;
                }

                var size = await Task.Run(() =>
                {
                    return _folderSizeCalculationService.CalculateDirectorySize(item.Path, combinedToken);
                }, combinedToken).ConfigureAwait(false);

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
                    }).ConfigureAwait(false);
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
        public void CancelOngoingOperations()
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
            return await Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [FileListService] LoadDirectoriesAsync Started for: {path}");

                var directories = new List<FileSystemItem>();
                try
                {
                    if (!Directory.Exists(path)) return directories;

                    string[] dirPaths = Directory.GetDirectories(path);
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [FileListService] IO GetDirectories done. Count: {dirPaths.Length}");

                    if (dirPaths.Length > 0)
                    {
                        var validDirs = new System.Collections.Generic.List<(string DirPath, string ActualPath, string DisplayName)>();

                        // 1. 预处理：解析路径和符号链接，过滤不存在的文件夹
                        foreach (var dirPath in dirPaths)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            try
                            {
                                string dirName = Path.GetFileName(dirPath);

                                // 黑名单：主动跳过无权访问的系统目录，避免后续读取 Metadata 抛出异常
                                if (dirName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                                    dirName.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                                    dirName.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string actualPath = dirPath;
                                string displayName = dirName;

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

                                validDirs.Add((dirPath, actualPath, displayName));
                            }
                            catch { }
                        }

                        // 2. 批量获取文件夹大小缓存
                        var folderSizeCacheMap = new System.Collections.Generic.Dictionary<string, long>();
                        if (getFolderSizeCache != null && validDirs.Count > 0)
                        {
                            try
                            {
                                var swDb = System.Diagnostics.Stopwatch.StartNew();
                                var pathsToQuery = validDirs.Select(d => d.ActualPath).ToList();
                                folderSizeCacheMap = DatabaseManager.GetFolderSizesBatch(pathsToQuery);
                                swDb.Stop();
                                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [FileListService] Batch DB Query done. Count: {pathsToQuery.Count}. Time: {swDb.ElapsedMilliseconds}ms");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Batch folder size query failed: {ex.Message}");
                            }
                        }

                        // 3. 构建 FileSystemItem
                        foreach (var (dirPath, actualPath, displayName) in validDirs)
                        {
                            if (cancellationToken.IsCancellationRequested) break;

                            try
                            {
                                var dirInfo = new DirectoryInfo(actualPath);

                                string sizeDisplay = "计算中...";
                                long? cachedSize = null;

                                if (folderSizeCacheMap.TryGetValue(actualPath, out long size))
                                {
                                    cachedSize = size;
                                    sizeDisplay = formatFileSize(size);
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
                            catch (UnauthorizedAccessException) { /* 跳过无法访问元数据的文件夹 */ }
                            catch (Exception) { }
                        }
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    // 已在内部处理，此处仅记录
                    System.Diagnostics.Debug.WriteLine($"[FileListService] UnauthorizedAccessException (Caught): {ex.Message}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [FileListService] LoadDirectoriesAsync Error: {ex.Message}");
                }

                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [FileListService] LoadDirectoriesAsync Completed for: {path}. Time: {sw.ElapsedMilliseconds}ms. Count: {directories.Count}");
                return directories;
            }, cancellationToken).ConfigureAwait(false);
        }



        /// <summary>
        /// 异步加载文件列表
        /// </summary>
        private async Task<List<FileSystemItem>> LoadFilesAsync(
            string path,
            Func<long, string> formatFileSize,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [FileListService] LoadFilesAsync Started for: {path}");

                var files = new List<FileSystemItem>();
                try
                {
                    if (!Directory.Exists(path)) return files;

                    var filePaths = Directory.GetFiles(path);
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [FileListService] LoadFilesAsync IO GetFiles done. Count: {filePaths.Length}");

                    foreach (var filePath in filePaths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        try
                        {
                            string fileName = Path.GetFileName(filePath);

                            // 移除硬编码的黑名单检查，让后面统一的异常处理来负责
                            var fileInfo = new System.IO.FileInfo(filePath);

                            // 使用安全访问方法获取属性
                            long sizeBytes = -1;
                            try { sizeBytes = fileInfo.Length; } catch { }

                            DateTime lastModified = DateTime.MinValue;
                            try { lastModified = fileInfo.LastWriteTime; } catch { }

                            DateTime created = DateTime.MinValue;
                            try { created = fileInfo.CreationTime; } catch { }

                            files.Add(new FileSystemItem
                            {
                                // 对于只有扩展名的文件（如.gitconfig），总是显示完整文件名
                                Name = GetDisplayFileName(filePath, ShowFullFileName),
                                Path = fileInfo.FullName,
                                Type = !string.IsNullOrEmpty(fileInfo.Extension) ? fileInfo.Extension : "文件",
                                Size = sizeBytes >= 0 ? formatFileSize(sizeBytes) : "未知",
                                ModifiedDate = lastModified != DateTime.MinValue ? lastModified.ToString("yyyy-MM-dd") : "未知",
                                CreatedTime = created != DateTime.MinValue ? FileSystemItem.FormatTimeAgo(created) : "未知",
                                IsDirectory = false,
                                SourcePath = path, // 标记来源路径
                                SizeBytes = sizeBytes,
                                ModifiedDateTime = lastModified,
                                CreatedDateTime = created
                            });
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[FileListService] UnauthorizedAccessException for file {filePath}: {ex.Message}");
                            continue;
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [FileListService] LoadFilesAsync Error: {ex.Message}");
                }

                sw.Stop();
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Thread {Environment.CurrentManagedThreadId}] [FileListService] LoadFilesAsync Completed for: {path}. Time: {sw.ElapsedMilliseconds}ms. Count: {files.Count}");
                return files;
            }, cancellationToken).ConfigureAwait(false);
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










