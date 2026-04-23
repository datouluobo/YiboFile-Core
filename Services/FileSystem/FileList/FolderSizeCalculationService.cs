using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace YiboFile.Services.FileList
{
    /// <summary>
    /// 文件夹大小计算服务
    /// 提供文件夹大小的计算、缓存更新等功能
    /// </summary>
    public class FolderSizeCalculationService
    {
        private readonly SemaphoreSlim _calculationSemaphore;
        private CancellationTokenSource _cancellationSource;

        /// <summary>
        /// 初始化文件夹大小计算服务
        /// </summary>
        /// <param name="maxConcurrency">最大并发计算数，默认为1</param>
        public FolderSizeCalculationService(int maxConcurrency = 1)
        {
            _calculationSemaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
            _cancellationSource = new CancellationTokenSource();
        }


        public long CalculateDirectorySizeOptimized(string directory, CancellationToken cancellationToken = default)
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

                // 核心特性：预取整颗缓存树以避免目录递归下的 N+1 次数据库调用
                var folderSizeCache = DatabaseManager.GetAllSubFolderSizes(directory);

                return CalculateDirectorySizeRecursiveOptimized(
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

        /// <summary>
        /// 递归计算文件夹大小（优化版本，包含所有子文件夹）
        /// </summary>
        public long CalculateDirectorySizeRecursiveOptimized(
            DirectoryInfo dirInfo,
            int currentDepth,
            int maxDepth,
            int maxFilesPerLevel,
            Stopwatch startTime,
            int maxTimeMs,
            CancellationToken cancellationToken,
            Dictionary<string, (long SizeBytes, DateTime LastModified)> folderSizeCache = null)
        {
            long size = 0;

            if (currentDepth >= maxDepth || cancellationToken.IsCancellationRequested)
                return size;

            if (startTime.ElapsedMilliseconds > maxTimeMs)
                return size;

            // 黑名单：跳过已知的系统受保护文件夹，减少异常抛出和扫描开销
            var dirName = dirInfo.Name;
            if (dirName.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            // 检查是否为符号链接或挂载点
            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return 0;
            }

            try
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                    ReturnSpecialDirectories = false
                };

                // 计算当前目录的直接文件
                int fileCount = 0;
                foreach (var file in dirInfo.EnumerateFiles("*", options))
                {
                    if (cancellationToken.IsCancellationRequested || startTime.ElapsedMilliseconds > maxTimeMs) return size;
                    if (fileCount >= maxFilesPerLevel) break; 

                    size += file.Length;
                    fileCount++;
                }

                // 递归计算子目录
                foreach (var subDir in dirInfo.EnumerateDirectories("*", options))
                {
                    if (cancellationToken.IsCancellationRequested || startTime.ElapsedMilliseconds > maxTimeMs) return size;

                    // 尝试全量预取树缓存匹配
                    if (folderSizeCache != null && folderSizeCache.TryGetValue(subDir.FullName, out var cached))
                    {
                        if (subDir.LastWriteTime <= cached.LastModified)
                        {
                            size += cached.SizeBytes;
                            continue;
                        }
                    }

                    long subDirSize = CalculateDirectorySizeRecursiveOptimized(
                        subDir,
                        currentDepth + 1,
                        maxDepth,
                        maxFilesPerLevel,
                        startTime,
                        maxTimeMs,
                        cancellationToken,
                        folderSizeCache);

                    size += subDirSize;

                    // 将子文件夹的大小缓存到数据库（异步，不阻塞），只在浅层操作防止炸毁 I/O
                    if (subDirSize > 0 && currentDepth <= 3)
                    {
                        Task.Run(() =>
                        {
                            try
                            {
                                DatabaseManager.SetFolderSize(subDir.FullName, subDirSize);
                            }
                            catch { }
                        });
                    }
                }
            }
            catch { }

            return size;
        }

        /// <summary>
        /// 计算并更新文件夹大小（进入文件夹时调用）
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public async Task CalculateAndUpdateFolderSizeAsync(
            string folderPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;

            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _cancellationSource?.Token ?? CancellationToken.None).Token;

            await _calculationSemaphore.WaitAsync(combinedToken);
            try
            {
                if (combinedToken.IsCancellationRequested) return;

                var size = CalculateDirectorySizeOptimized(folderPath, combinedToken);
                if (combinedToken.IsCancellationRequested) return;

                // 更新数据库缓存
                DatabaseManager.SetFolderSize(folderPath, size);
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                _calculationSemaphore.Release();
            }
        }

        /// <summary>
        /// 如果文件夹大小有变化，则计算并更新（进入文件夹时调用，已有缓存）
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <param name="cachedSize">缓存的文件夹大小</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public async Task CalculateAndUpdateFolderSizeIfChangedAsync(
            string folderPath,
            long cachedSize,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;

            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _cancellationSource?.Token ?? CancellationToken.None).Token;

            await _calculationSemaphore.WaitAsync(combinedToken);
            try
            {
                if (combinedToken.IsCancellationRequested) return;

                var size = CalculateDirectorySizeOptimized(folderPath, combinedToken);
                if (combinedToken.IsCancellationRequested) return;

                // 如果大小有变化，更新数据库缓存
                if (size != cachedSize)
                {
                    DatabaseManager.SetFolderSize(folderPath, size);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
            finally
            {
                _calculationSemaphore.Release();
            }
        }

        /// <summary>
        /// 第一次打开文件夹时，计算所有子文件夹的大小（性能优化版本）
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public async Task CalculateAllSubfolderSizesOnFirstOpenAsync(
            string folderPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return;

            await Task.Run(() =>
            {
                try
                {
                    // 获取所有子文件夹
                    string[] subfolders;
                    try
                    {
                        subfolders = Directory.GetDirectories(folderPath);
                    }
                    catch
                    {
                        return; // 无法访问，跳过
                    }

                    if (subfolders.Length == 0)
                        return; // 没有子文件夹，不需要计算

                    // 检查有多少子文件夹已有缓存
                    var cachedCount = 0;
                    foreach (var subfolder in subfolders)
                    {
                        var cachedSize = DatabaseManager.GetFolderSize(subfolder);
                        if (cachedSize.HasValue)
                        {
                            cachedCount++;
                        }
                    }

                    // 如果缓存率低于50%，认为是第一次打开，计算所有子文件夹大小
                    var cacheRate = (double)cachedCount / subfolders.Length;
                    if (cacheRate < 0.5)
                    {
                        // 异步计算所有子文件夹大小（分批处理，控制性能）
                        CalculateSubfolderSizesBatchAsync(subfolders, cancellationToken);
                    }
                }
                catch { }
            }, cancellationToken);
        }

        /// <summary>
        /// 分批计算子文件夹大小（性能优化：限制并发、延迟处理）
        /// </summary>
        /// <param name="folderPaths">文件夹路径数组</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public void CalculateSubfolderSizesBatchAsync(
            string[] folderPaths,
            CancellationToken cancellationToken = default)
        {
            if (folderPaths == null || folderPaths.Length == 0)
                return;

            var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _cancellationSource?.Token ?? CancellationToken.None).Token;

            // 分批处理，每批最多10个文件夹
            int batchSize = 10;
            int delayBetweenBatches = 2000; // 每批之间延迟2秒

            for (int i = 0; i < folderPaths.Length; i += batchSize)
            {
                var batch = folderPaths.Skip(i).Take(batchSize).ToArray();
                var batchIndex = i / batchSize;
                var delay = batchIndex * delayBetweenBatches;

                Task.Run(async () =>
                {
                    try
                    {
                        // 延迟启动，避免同时启动太多任务
                        if (delay > 0)
                        {
                            await Task.Delay(delay, combinedToken);
                        }

                        if (combinedToken.IsCancellationRequested) return;

                        // 处理当前批次
                        foreach (var folderPath in batch)
                        {
                            if (combinedToken.IsCancellationRequested) return;

                            // 检查是否已有缓存
                            var cachedSize = DatabaseManager.GetFolderSize(folderPath);
                            if (cachedSize.HasValue)
                            {
                                continue; // 已有缓存，跳过
                            }

                            // 尝试获取信号量（非阻塞，如果无法获取则跳过）
                            if (!await _calculationSemaphore.WaitAsync(100, combinedToken))
                            {
                                // 无法获取，延迟后重试或跳过
                                continue;
                            }

                            try
                            {
                                if (combinedToken.IsCancellationRequested) return;

                                // 计算文件夹大小
                                var size = CalculateDirectorySizeOptimized(folderPath, combinedToken);
                                if (combinedToken.IsCancellationRequested) return;

                                // 更新数据库缓存
                                DatabaseManager.SetFolderSize(folderPath, size);
                            }
                            catch (OperationCanceledException) { }
                            catch { }
                            finally
                            {
                                _calculationSemaphore.Release();
                            }

                            // 每个文件夹之间延迟100ms，避免CPU占用过高
                            await Task.Delay(100, combinedToken);
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                }, combinedToken);
            }
        }

        /// <summary>
        /// 程序启动时清理不存在的文件夹大小缓存
        /// </summary>
        public void CleanupFolderSizeCacheOnStartup()
        {
            // 异步执行，不阻塞UI
            Task.Run(() =>
            {
                try
                {
                    // 获取缓存总数
                    int totalCount = DatabaseManager.GetFolderSizeCacheCount();
                    if (totalCount == 0)
                        return; // 没有缓存，不需要清理

                    // 如果缓存数量较少，清理所有；如果较多，只清理一部分（避免启动时耗时过长）
                    int maxProcessed = totalCount > 5000 ? 1000 : 0; // 超过5000条时，只清理1000条
                    int cleanedCount = DatabaseManager.CleanupNonExistentFolderSizes(batchSize: 100, maxProcessed: maxProcessed);

                    if (cleanedCount > 0)
                    {

                    }
                }
                catch (Exception)
                {

                }
            });
        }

        /// <summary>
        /// 取消所有正在进行的计算
        /// </summary>
        public void Cancel()
        {
            _cancellationSource?.Cancel();
            _cancellationSource?.Dispose();
            _cancellationSource = new CancellationTokenSource();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Cancel();
            _calculationSemaphore?.Dispose();
        }
    }
}


