using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YiboFile.Services.FullTextSearch
{
    /// <summary>
    /// 索引进度事件参数
    /// </summary>
    public class IndexingProgressEventArgs : EventArgs
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int IndexedFiles { get; set; }
        public string CurrentFile { get; set; }
        public bool IsCompleted { get; set; }
    }

    /// <summary>
    /// 后台文件索引任务服务
    /// </summary>
    public class IndexingTaskService : IDisposable
    {
        private readonly FtsIndexService _ftsService;
        private readonly ContentExtractorManager _extractorManager;
        private CancellationTokenSource _cts;
        private bool _isRunning;
        private bool _disposed;

        public event EventHandler<IndexingProgressEventArgs> ProgressChanged;

        public bool IsRunning => _isRunning;

        public IndexingTaskService(FtsIndexService ftsService = null)
        {
            _ftsService = ftsService ?? new FtsIndexService();
            _extractorManager = new ContentExtractorManager();
        }

        /// <summary>
        /// 开始索引指定目录
        /// </summary>
        public async Task StartIndexingAsync(string directoryPath, bool recursive = true)
        {
            if (_isRunning) return;
            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath)) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();

            try
            {
                await Task.Run(() => IndexDirectory(directoryPath, recursive, _cts.Token), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[IndexingTaskService] Indexing cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IndexingTaskService] Error: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// 索引目录中的文件
        /// </summary>
        private void IndexDirectory(string directoryPath, bool recursive, CancellationToken ct)
        {
            var supportedExtensions = _extractorManager.SupportedExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = new List<string>();

            try
            {
                // 收集所有支持的文件
                foreach (var ext in supportedExtensions)
                {
                    try
                    {
                        files.AddRange(Directory.GetFiles(directoryPath, $"*{ext}", searchOption));
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (PathTooLongException) { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IndexingTaskService] Error enumerating files: {ex.Message}");
                return;
            }

            var totalFiles = files.Count;
            var processedFiles = 0;
            var indexedFiles = 0;

            // Transaction batching configuration
            const int batchSize = 50;
            int currentBatchCount = 0;

            try
            {
                _ftsService.BeginTransaction();

                foreach (var file in files)
                {
                    if (ct.IsCancellationRequested) break;

                    processedFiles++;
                    currentBatchCount++;

                    try
                    {
                        if (_ftsService.IndexFile(file))
                        {
                            indexedFiles++;
                        }
                    }
                    catch { }

                    // Commit batch
                    if (currentBatchCount >= batchSize)
                    {
                        _ftsService.CommitTransaction();
                        _ftsService.BeginTransaction();
                        currentBatchCount = 0;
                    }

                    // 每处理 10 个文件报告一次进度
                    if (processedFiles % 10 == 0 || processedFiles == totalFiles)
                    {
                        RaiseProgress(totalFiles, processedFiles, indexedFiles, file, processedFiles == totalFiles);
                    }
                }

                _ftsService.CommitTransaction();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IndexingTaskService] Error in indexing loop: {ex.Message}");
                // Try to commit whatever we can or it will rollback on dispose
                try { _ftsService.CommitTransaction(); } catch { }
            }

            // 最终报告
            RaiseProgress(totalFiles, processedFiles, indexedFiles, string.Empty, true);
        }

        private void RaiseProgress(int total, int processed, int indexed, string currentFile, bool completed)
        {
            ProgressChanged?.Invoke(this, new IndexingProgressEventArgs
            {
                TotalFiles = total,
                ProcessedFiles = processed,
                IndexedFiles = indexed,
                CurrentFile = currentFile,
                IsCompleted = completed
            });
        }

        /// <summary>
        /// 停止索引
        /// </summary>
        public void StopIndexing()
        {
            _cts?.Cancel();
        }

        /// <summary>
        /// 索引单个文件
        /// </summary>
        public bool IndexFile(string filePath)
        {
            return _ftsService.IndexFile(filePath);
        }

        /// <summary>
        /// 删除文件索引
        /// </summary>
        public void RemoveFile(string filePath)
        {
            _ftsService.DeleteDocument(filePath);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _ftsService?.Dispose();
                _disposed = true;
            }
        }
    }
}

