using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using YiboFile.Models;

namespace YiboFile.Services.FileList
{
    /// <summary>
    /// 提供文件夹大小的异步计算能力，并控制并发与延迟。
    /// </summary>
    public class FolderSizeCalculator
    {
        private readonly SemaphoreSlim _calculationSemaphore = new SemaphoreSlim(1, 1);
        private readonly Queue<FileSystemItem> _pendingFolders = new Queue<FileSystemItem>();
        private readonly object _queueLock = new object();

        /// <summary>
        /// 计算文件夹大小，包含前几个文件夹的延迟并发计算与剩余队列处理。
        /// </summary>
        /// <param name="items">文件或文件夹列表。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="dispatcher">用于 UI 更新的 Dispatcher。</param>
        /// <param name="formatFileSize">文件大小格式化委托。</param>
        /// <param name="refreshAction">计算后触发的刷新动作。</param>
        public async Task CalculateAsync(
            IEnumerable<FileSystemItem> items,
            CancellationToken cancellationToken,
            Dispatcher dispatcher,
            Func<long, string> formatFileSize,
            Action refreshAction = null)
        {
            if (items == null || formatFileSize == null)
            {
                return;
            }

            var directories = items.Where(i => i?.IsDirectory == true).ToList();
            if (directories.Count == 0)
            {
                return;
            }

            ClearQueue();

            int maxCalculations = Math.Min(5, directories.Count);
            EnqueueRemaining(directories.Skip(maxCalculations));

            var initialTasks = new List<Task>();
            for (int i = 0; i < maxCalculations; i++)
            {
                var directoryItem = directories[i];
                var delay = i * 1000;

                initialTasks.Add(Task.Run(async () =>
                {
                    if (delay > 0)
                    {
                        await Task.Delay(delay, cancellationToken);
                    }

                    await CalculateSingleAsync(
                        directoryItem,
                        dispatcher,
                        cancellationToken,
                        formatFileSize,
                        DispatcherPriority.SystemIdle,
                        refreshAction);
                }, cancellationToken));
            }

            var pendingTask = ProcessPendingQueueAsync(dispatcher, cancellationToken, formatFileSize, refreshAction);

            try
            {
                await Task.WhenAll(initialTasks);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await pendingTask;
        }

        private void ClearQueue()
        {
            lock (_queueLock)
            {
                _pendingFolders.Clear();
            }
        }

        private void EnqueueRemaining(IEnumerable<FileSystemItem> items)
        {
            if (items == null)
            {
                return;
            }

            lock (_queueLock)
            {
                foreach (var item in items)
                {
                    if (item != null)
                    {
                        _pendingFolders.Enqueue(item);
                    }
                }
            }
        }

        private async Task ProcessPendingQueueAsync(
            Dispatcher dispatcher,
            CancellationToken cancellationToken,
            Func<long, string> formatFileSize,
            Action refreshAction)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                FileSystemItem next = null;
                lock (_queueLock)
                {
                    if (_pendingFolders.Count > 0)
                    {
                        next = _pendingFolders.Dequeue();
                    }
                }

                if (next == null)
                {
                    break;
                }

                try
                {
                    await Task.Delay(3000, cancellationToken);
                    await CalculateSingleAsync(
                        next,
                        dispatcher,
                        cancellationToken,
                        formatFileSize,
                        DispatcherPriority.SystemIdle,
                        refreshAction);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task CalculateSingleAsync(
            FileSystemItem item,
            Dispatcher dispatcher,
            CancellationToken cancellationToken,
            Func<long, string> formatFileSize,
            DispatcherPriority priority,
            Action refreshAction)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Path))
            {
                return;
            }

            // System.Diagnostics.Debug.WriteLine($"[FolderSizeCalculator] Waiting for semaphore: {item.Path}");
            await _calculationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var sw = Stopwatch.StartNew();
                // System.Diagnostics.Debug.WriteLine($"[FolderSizeCalculator] Calculating: {item.Path}");

                var size = await Task.Run(() => CalculateDirectorySize(item.Path, cancellationToken), cancellationToken);

                // System.Diagnostics.Debug.WriteLine($"[FolderSizeCalculator] Calculated: {item.Path} Size: {size} Time: {sw.ElapsedMilliseconds}ms");

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Update DB - this might be the blocking part if locked?
                try
                {
                    DatabaseManager.SetFolderSize(item.Path, size);
                }
                catch (Exception dbEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[FolderSizeCalculator] DB Update Failed: {dbEx.Message}");
                }

                var displaySize = formatFileSize(size);

                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        item.Size = displaySize;
                        refreshAction?.Invoke();
                    }, priority);
                }
                else
                {
                    item.Size = displaySize;
                    refreshAction?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _calculationSemaphore.Release();
            }
        }

        private long CalculateDirectorySize(string directory, CancellationToken cancellationToken)
        {
            try
            {
                var dirInfo = new DirectoryInfo(directory);
                if (!dirInfo.Exists)
                {
                    return 0;
                }

                var stopwatch = Stopwatch.StartNew();
                const int maxDepth = 20;
                const int maxEntriesPerLevel = 5000;
                const int maxTimeMs = 10000;

                // Pre-fetch all subfolder sizes for this root
                // This ONE query replaces potentially thousands of GetFolderSize calls
                // Note: This fetches sizes for the entire tree under 'directory'
                var folderSizeCache = DatabaseManager.GetAllSubFolderSizes(directory);

                return CalculateDirectorySizeRecursive(
                    dirInfo,
                    0,
                    maxDepth,
                    maxEntriesPerLevel,
                    stopwatch,
                    maxTimeMs,
                    cancellationToken,
                    folderSizeCache);
            }
            catch
            {
                return 0;
            }
        }

        private long CalculateDirectorySizeRecursive(
            DirectoryInfo dirInfo,
            int currentDepth,
            int maxDepth,
            int maxEntriesPerLevel,
            Stopwatch stopwatch,
            int maxTimeMs,
            CancellationToken cancellationToken,
            Dictionary<string, (long SizeBytes, DateTime LastModified)> folderSizeCache)
        {
            if (currentDepth >= maxDepth || cancellationToken.IsCancellationRequested)
            {
                return 0;
            }

            // 黑名单：跳过已知的系统受保护文件夹，减少异常抛出和扫描开销
            var dirName = dirInfo.Name;
            if (dirName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            // 检查是否为符号链接或挂载点，避免循环引用
            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return 0;
            }

            if (stopwatch.ElapsedMilliseconds > maxTimeMs)
            {
                return 0;
            }

            long size = 0;

            try
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = false
                };

                // 使用 EnumerateFiles 减少大目录的内存占用
                foreach (var file in dirInfo.EnumerateFiles("*", options))
                {
                    if (cancellationToken.IsCancellationRequested || stopwatch.ElapsedMilliseconds > maxTimeMs)
                    {
                        break;
                    }

                    size += file.Length;
                }

                // Use EnumerateDirectories to reduce memory usage
                foreach (var subDir in dirInfo.EnumerateDirectories("*", options))
                {
                    if (cancellationToken.IsCancellationRequested || stopwatch.ElapsedMilliseconds > maxTimeMs)
                    {
                        break;
                    }

                    // Check cache first
                    if (folderSizeCache != null && folderSizeCache.TryGetValue(subDir.FullName, out var cached))
                    {
                        // Verify timestamp to ensure cache is fresh
                        // We do this check here because we have the actual subDir info essentially for free (from enumeration)
                        // Wait, subDir is DirectoryInfo, accessing LastWriteTime causes IO? 
                        // EnumerateDirectories returns DirectoryInfo which *should* have loaded attributes.
                        // .NET Core usually caches this info from the WIN32_FIND_DATA
                        if (subDir.LastWriteTime <= cached.LastModified)
                        {
                            size += cached.SizeBytes;
                            continue;
                        }
                    }

                    // If not in cache or stale, recurse
                    size += CalculateDirectorySizeRecursive(
                        subDir,
                        currentDepth + 1,
                        maxDepth,
                        maxEntriesPerLevel,
                        stopwatch,
                        maxTimeMs,
                        cancellationToken,
                        folderSizeCache);
                }
            }
            catch
            {
                return size;
            }

            return size;
        }
    }
}

















