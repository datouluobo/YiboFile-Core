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

        /// <summary>
        /// 计算目录大小（递归）
        /// </summary>
        /// <param name="directory">目录路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>目录大小（字节）</returns>
        public long CalculateDirectorySize(string directory, CancellationToken cancellationToken = default)
        {
            try
            {
                var dirInfo = new DirectoryInfo(directory);
                if (!dirInfo.Exists)
                {
                    return 0;
                }

                long size = 0;

                // 计算文件大小
                try
                {
                    var files = dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return size;
                        }

                        try
                        {
                            size += file.Length;
                        }
                        catch { }
                    }
                }
                catch { }

                // 计算子目录大小（使用缓存）
                try
                {
                    var subDirs = dirInfo.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
                    foreach (var subDir in subDirs)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return size;
                        }

                        var cached = DatabaseManager.GetFolderSize(subDir.FullName);
                        if (cached.HasValue)
                        {
                            size += cached.Value;
                        }
                        else
                        {
                            // 递归计算（限制深度避免性能问题）
                            size += CalculateDirectorySize(subDir.FullName, cancellationToken);
                        }
                    }
                }
                catch { }

                return size;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 计算目录大小（优化版本，包含超时和深度限制）
        /// </summary>
        /// <param name="directory">目录路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>目录大小（字节）</returns>
        public long CalculateDirectorySizeOptimized(string directory, CancellationToken cancellationToken = default)
        {
            long size = 0;
            try
            {
                var dirInfo = new DirectoryInfo(directory);
                if (!dirInfo.Exists) return size;

                var startTime = Stopwatch.StartNew();
                int maxTimeMs = 10000; // 10秒超时
                int maxDepth = 20; // 限制递归深度
                int maxFilesPerLevel = 5000; // 每层最多计算5000个文件

                // 使用递归方法计算，包含所有子文件夹的大小
                size = CalculateDirectorySizeRecursiveOptimized(
                    dirInfo, 0, maxDepth, maxFilesPerLevel, startTime, maxTimeMs, cancellationToken);
            }
            catch { }
            return size;
        }

        /// <summary>
        /// 递归计算文件夹大小（优化版本，包含所有子文件夹）
        /// </summary>
        /// <param name="dirInfo">目录信息</param>
        /// <param name="currentDepth">当前深度</param>
        /// <param name="maxDepth">最大深度</param>
        /// <param name="maxFilesPerLevel">每层最大文件数</param>
        /// <param name="startTime">开始时间</param>
        /// <param name="maxTimeMs">最大时间（毫秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>目录大小（字节）</returns>
        public long CalculateDirectorySizeRecursiveOptimized(
            DirectoryInfo dirInfo,
            int currentDepth,
            int maxDepth,
            int maxFilesPerLevel,
            Stopwatch startTime,
            int maxTimeMs,
            CancellationToken cancellationToken)
        {
            long size = 0;

            if (currentDepth >= maxDepth || cancellationToken.IsCancellationRequested)
                return size;

            // 超时检查
            if (startTime.ElapsedMilliseconds > maxTimeMs)
                return size;

            try
            {
                // 先尝试从数据库读取子文件夹的缓存大小（如果存在）
                // 这样可以避免重复计算已缓存的子文件夹
                var subDirs = dirInfo.EnumerateDirectories("*", SearchOption.TopDirectoryOnly);
                var subDirsToCalculate = new List<DirectoryInfo>();
                long cachedSubDirSize = 0;

                foreach (var subDir in subDirs)
                {
                    if (cancellationToken.IsCancellationRequested) return size;
                    if (startTime.ElapsedMilliseconds > maxTimeMs) return size;

                    // 尝试从数据库读取缓存
                    var cachedSize = DatabaseManager.GetFolderSize(subDir.FullName);
                    if (cachedSize.HasValue)
                    {
                        cachedSubDirSize += cachedSize.Value;
                    }
                    else
                    {
                        subDirsToCalculate.Add(subDir);
                    }
                }

                size += cachedSubDirSize;

                // 计算当前目录的直接文件
                int fileCount = 0;
                try
                {
                    var files = dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly);
                    foreach (var file in files)
                    {
                        if (cancellationToken.IsCancellationRequested) return size;
                        if (startTime.ElapsedMilliseconds > maxTimeMs) return size;
                        if (fileCount >= maxFilesPerLevel) break; // 超过限制，停止计算

                        // 每处理100个文件检查一次取消并让出CPU
                        fileCount++;
                        if (fileCount % 100 == 0)
                        {
                            Thread.Sleep(20);
                            if (cancellationToken.IsCancellationRequested) return size;
                            if (startTime.ElapsedMilliseconds > maxTimeMs) return size;
                        }

                        try
                        {
                            size += file.Length;
                        }
                        catch { }
                    }
                }
                catch { }

                // 递归计算子目录（只计算没有缓存的）
                foreach (var subDir in subDirsToCalculate)
                {
                    if (cancellationToken.IsCancellationRequested) return size;
                    if (startTime.ElapsedMilliseconds > maxTimeMs) return size;

                    try
                    {
                        long subDirSize = CalculateDirectorySizeRecursiveOptimized(
                            subDir,
                            currentDepth + 1,
                            maxDepth,
                            maxFilesPerLevel,
                            startTime,
                            maxTimeMs,
                            cancellationToken);
                        size += subDirSize;

                        // 将子文件夹的大小缓存到数据库（异步，不阻塞）
                        if (subDirSize > 0)
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
                    catch { }

                    // 每个子文件夹之间延迟，避免CPU占用过高
                    if (currentDepth < 3) // 只在浅层延迟，深层不延迟以加快速度
                    {
                        Thread.Sleep(10);
                    }
                }
            }
            catch { }

            return size;
        }

        /// <summary>
        /// 递归计算文件夹大小（基础版本）
        /// </summary>
        /// <param name="dirInfo">目录信息</param>
        /// <param name="currentDepth">当前深度</param>
        /// <param name="maxDepth">最大深度</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>目录大小（字节）</returns>
        public long CalculateDirectorySizeRecursive(
            DirectoryInfo dirInfo,
            int currentDepth,
            int maxDepth,
            CancellationToken cancellationToken)
        {
            long size = 0;
            if (currentDepth >= maxDepth || cancellationToken.IsCancellationRequested) return size;

            try
            {
                // 计算当前目录的直接文件
                int fileCount = 0;
                foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
                {
                    if (cancellationToken.IsCancellationRequested) return size;

                    // 每处理20个文件检查一次取消，并让出CPU时间片（增加频率减少CPU占用）
                    fileCount++;
                    if (fileCount % 20 == 0)
                    {
                        Thread.Sleep(10); // 增加到10ms，让出更多CPU时间片
                        if (cancellationToken.IsCancellationRequested) return size;
                    }

                    try
                    {
                        size += file.Length;
                    }
                    catch { }
                }

                // 递归计算子目录（限制深度）
                foreach (var subDir in dirInfo.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
                {
                    if (cancellationToken.IsCancellationRequested) return size;
                    try
                    {
                        size += CalculateDirectorySizeRecursive(subDir, currentDepth + 1, maxDepth, cancellationToken);
                    }
                    catch { }
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
                        Debug.WriteLine($"启动时清理了 {cleanedCount} 条不存在的文件夹大小缓存");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"清理文件夹大小缓存失败: {ex.Message}");
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


