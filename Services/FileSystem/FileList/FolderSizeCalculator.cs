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
        private readonly SemaphoreSlim _calculationSemaphore = new SemaphoreSlim(4, 4);
        private readonly Queue<FileSystemItem> _pendingFolders = new Queue<FileSystemItem>();
        private readonly object _queueLock = new object();
        private readonly FolderSizeCalculationService _calculationService;

        public FolderSizeCalculator(FolderSizeCalculationService calculationService = null)
        {
            _calculationService = calculationService ?? new FolderSizeCalculationService(4);
        }

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

            await _calculationSemaphore.WaitAsync(cancellationToken);
            try
            {
                var sw = Stopwatch.StartNew();

                var size = await Task.Run(() => _calculationService.CalculateDirectorySizeOptimized(item.Path, cancellationToken), cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // Update DB - this might be the blocking part if locked?
                try
                {
                    DatabaseManager.SetFolderSize(item.Path, size);
                }
                catch (Exception)
                {

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
    }
}

















