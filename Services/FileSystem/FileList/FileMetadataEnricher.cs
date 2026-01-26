using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using YiboFile.Services.FileNotes;

namespace YiboFile.Services.FileList
{
    /// <summary>
    /// 提供文件标签与备注的批量填充能力。
    /// </summary>
    public class FileMetadataEnricher
    {
        /// <summary>
        /// 异步为文件填充标签与备注。
        /// </summary>
        /// <param name="items">文件或文件夹列表。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <param name="dispatcher">用于更新 UI 的 Dispatcher。</param>
        /// <param name="orderTagNames">标签排序委托，默认为按名称升序。</param>
        /// <param name="refreshAction">填充完成后触发的刷新动作。</param>
        public async Task EnrichAsync(
            IEnumerable<FileSystemItem> items,
            CancellationToken cancellationToken,
            Dispatcher dispatcher,
            Func<List<int>, List<string>> orderTagNames = null,
            Action refreshAction = null)
        {
            if (items == null)
            {
                return;
            }

            var targets = items.Where(i => i != null).ToList();
            if (targets.Count == 0)
            {
                return;
            }

            var semaphore = new SemaphoreSlim(2, 2);
            var tasks = targets.Select(item => EnrichItemAsync(item, semaphore, cancellationToken, orderTagNames)).ToList();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() => refreshAction?.Invoke(), DispatcherPriority.Background);
            }
            else
            {
                refreshAction?.Invoke();
            }
        }

        private async Task EnrichItemAsync(
            FileSystemItem item,
            SemaphoreSlim semaphore,
            CancellationToken cancellationToken,
            Func<List<int>, List<string>> orderTagNames)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                item.Tags = BuildTags(item.Path, orderTagNames);
                item.Notes = BuildNotes(item.Path);

                // Enhance: Extract Media Metadata
                await EnrichMediaMetadataAsync(item);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task EnrichMediaMetadataAsync(FileSystemItem item)
        {
            if (item.IsDirectory || string.IsNullOrEmpty(item.Path)) return;

            string ext = System.IO.Path.GetExtension(item.Path).ToLowerInvariant();
            if (YiboFile.Services.Search.SearchFilterService.ImageExtensions.Contains(ext))
            {
                try
                {
                    // Use System.Drawing.Common for images
                    // Run in Task to avoid blocking
                    await Task.Run(() =>
                    {
                        try
                        {
                            // Use FileStream to avoid locking the file if possible, or just Image.FromFile
                            // Image.FromFile locks until disposed. 
                            using (var fs = new System.IO.FileStream(item.Path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                            {
                                using (var img = System.Drawing.Image.FromStream(fs, false, false))
                                {
                                    item.PixelWidth = img.Width;
                                    item.PixelHeight = img.Height;
                                }
                            }
                        }
                        catch { }
                    });
                }
                catch { }
            }
            else if (YiboFile.Services.Search.SearchFilterService.VideoExtensions.Contains(ext) ||
                     YiboFile.Services.Search.SearchFilterService.AudioExtensions.Contains(ext))
            {
                try
                {
                    // Use Native Shell Property (reliable, built-in)
                    // Replaces FFMpegCore which requires external binaries
                    long duration = YiboFile.Services.Core.ShellPropertyHelper.GetDuration(item.Path);
                    item.DurationMs = duration;

                    // For video dimensions, ShellPropertyHelper could also be used but requires more PKEYs.
                    // For now, if duration is retrieved, that's good.
                    // If we really need dimensions for video, we can try FFMpeg as backup or add PKEYs.
                }
                catch { }
            }
        }

        private string BuildTags(string path, Func<List<int>, List<string>> orderTagNames)
        {
            // Tag加载已移除 - Phase 2将重新实现
            return string.Empty;
        }

        private string BuildNotes(string path)
        {
            var notes = FileNotesService.GetFileNotes(path);
            if (string.IsNullOrWhiteSpace(notes))
            {
                return string.Empty;
            }

            var firstLine = notes
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? string.Empty;

            return firstLine.Length > 100 ? firstLine[..100] + "..." : firstLine;
        }

        // DefaultOrderTags 已移除 - Phase 2将重新实现
        // private List<string> DefaultOrderTags(List<int> tagIds)
        // {
        //     return new List<string>();
        // }
    }
}

















