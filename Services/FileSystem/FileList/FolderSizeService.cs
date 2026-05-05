using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Threading;
using YiboFile.Models;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Services.FileList
{
    public sealed class FolderSizeService : IDisposable
    {
        private readonly record struct CalculationRequest(long Generation, string FolderPath);

        private readonly Channel<CalculationRequest> _channel;
        private readonly Task[] _workers;
        private readonly IMessageBus _messageBus;
        private readonly Dispatcher _dispatcher;
        private readonly CancellationTokenSource _disposeCts = new();
        private volatile bool _disposed;
        private long _generation;

        private const int MaxEnqueuePerCall = 500;

        private static readonly EnumerationOptions _fileOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        private static readonly EnumerationOptions _dirOptions = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        public FolderSizeService(
            IMessageBus messageBus,
            Dispatcher dispatcher,
            int concurrency = 4)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _channel = Channel.CreateBounded<CalculationRequest>(new BoundedChannelOptions(5000)
            {
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropWrite
            });

            _workers = new Task[concurrency];
            for (int i = 0; i < concurrency; i++)
            {
                _workers[i] = Task.Run(() => ProcessQueueAsync());
            }
        }

        public void ClearPending()
        {
            Interlocked.Increment(ref _generation);
            while (_channel.Reader.TryRead(out _)) { }
        }

        public void EnqueueChildren(string parentPath, IEnumerable<FileSystemItem> items, CancellationToken cancellationToken = default)
        {
            if (_disposed || items == null) return;

            var gen = Interlocked.Read(ref _generation);
            int count = 0;
            foreach (var item in items)
            {
                if (cancellationToken.IsCancellationRequested || _disposed) break;
                if (count >= MaxEnqueuePerCall) break;
                if (item == null || !item.IsDirectory || string.IsNullOrEmpty(item.Path)) continue;
                if (item.SizeBytes > 0 && item.Size != "计算中...") continue;

                try
                {
                    _channel.Writer.TryWrite(new CalculationRequest(gen, item.Path));
                    count++;
                }
                catch (ChannelClosedException) { break; }
            }
        }

        public void EnqueueSingle(string folderPath, CancellationToken cancellationToken = default)
        {
            if (_disposed || string.IsNullOrEmpty(folderPath)) return;

            var gen = Interlocked.Read(ref _generation);
            try
            {
                _channel.Writer.TryWrite(new CalculationRequest(gen, folderPath));
            }
            catch (ChannelClosedException) { }
        }

        public Task CalculateAndUpdateFolderSizeAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            EnqueueSingle(folderPath, cancellationToken);
            return Task.CompletedTask;
        }

        public Task CalculateAllSubfolderSizesOnFirstOpenAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void CleanupFolderSizeCacheOnStartup()
        {
            Task.Run(() =>
            {
                try
                {
                    int totalCount = DatabaseManager.GetFolderSizeCacheCount();
                    if (totalCount == 0) return;
                    int maxProcessed = totalCount > 5000 ? 1000 : 0;
                    DatabaseManager.CleanupNonExistentFolderSizes(batchSize: 100, maxProcessed: maxProcessed);
                }
                catch { }
            });
        }

        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                await foreach (var request in _channel.Reader.ReadAllAsync(_disposeCts.Token).ConfigureAwait(false))
                {
                    if (_disposed) break;

                    if (request.Generation < Interlocked.Read(ref _generation))
                        continue;

                    try
                    {
                        CalculateSingleFolder(request.FolderPath);
                    }
                    catch { }
                }
            }
            catch (OperationCanceledException) { }
            catch (ChannelClosedException) { }
        }

        private void CalculateSingleFolder(string folderPath)
        {
            if (_disposed) return;

            var cached = DatabaseManager.GetFolderSize(folderPath);
            if (cached.HasValue)
            {
                NotifyUI(folderPath, cached.Value, FormatFileSize(cached.Value));
                return;
            }

            long size = ScanDirectorySize(folderPath);
            if (_disposed) return;

            DatabaseManager.SetFolderSize(folderPath, size);
            NotifyUI(folderPath, size, FormatFileSize(size));
        }

        private static long ScanDirectorySize(string folderPath)
        {
            long totalSize = 0;
            try
            {
                if (!Directory.Exists(folderPath)) return 0;

                // Scan files in current directory only (non-recursive, closes handle after enumeration)
                foreach (var file in Directory.EnumerateFiles(folderPath, "*", _fileOptions))
                {
                    try
                    {
                        var fi = new System.IO.FileInfo(file);
                        if (fi.Exists)
                            totalSize += fi.Length;
                    }
                    catch { }
                }

                // Manually recurse into subdirectories — each subdirectory's handle is opened
                // and closed independently, avoiding long-held handles on the root folder.
                foreach (var subDir in Directory.EnumerateDirectories(folderPath, "*", _dirOptions))
                {
                    totalSize += ScanDirectorySize(subDir);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            catch { }
            return totalSize;
        }

        private void NotifyUI(string folderPath, long size, string formattedSize)
        {
            if (_disposed) return;
            try
            {
                _dispatcher.BeginInvoke(() =>
                {
                    try { _messageBus.Publish(new FolderSizeCalculatedMessage(folderPath, size, formattedSize)); }
                    catch { }
                }, DispatcherPriority.Background);
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _channel.Writer.TryComplete();
            _disposeCts.Cancel();
            _disposeCts.Dispose();
            try { Task.WaitAll(_workers, TimeSpan.FromSeconds(3)); }
            catch { }
        }
    }
}