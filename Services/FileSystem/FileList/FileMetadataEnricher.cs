using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using OoiMRR.Services.FileNotes;

namespace OoiMRR.Services.FileList
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
            }
            finally
            {
                semaphore.Release();
            }
        }

        private string BuildTags(string path, Func<List<int>, List<string>> orderTagNames)
        {
            if (!App.IsTagTrainAvailable || string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            var tagIds = OoiMRRIntegration.GetFileTagIds(path);
            if (tagIds == null || tagIds.Count == 0)
            {
                return string.Empty;
            }

            var ordered = orderTagNames?.Invoke(tagIds) ?? DefaultOrderTags(tagIds);
            return string.Join(", ", ordered);
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

        private List<string> DefaultOrderTags(List<int> tagIds)
        {
            return tagIds
                .Select(OoiMRRIntegration.GetTagName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
    }
}
















