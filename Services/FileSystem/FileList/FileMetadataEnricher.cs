using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using YiboFile.Models;
using YiboFile.Services.FileNotes;
using YiboFile.Services.Features;
using YiboFile.Services.Navigation;

namespace YiboFile.Services.FileList
{
    /// <summary>
    /// Provides batch tag and note enrichment for file items.
    /// </summary>
    public class FileMetadataEnricher
    {
        private readonly ITagService _tagService;

        public FileMetadataEnricher(ITagService tagService = null)
        {
            _tagService = tagService ?? App.ServiceProvider?.GetService(typeof(ITagService)) as ITagService;
        }

        /// <summary>
        /// Enrich items with tags, notes, and media metadata.
        /// Uses polling cancellation only - no CancellationToken passed to async methods
        /// to avoid OperationCanceledException spam in debugger.
        /// </summary>
        public async Task EnrichAsync(
            IEnumerable<FileSystemItem> items,
            CancellationToken cancellationToken,
            Dispatcher dispatcher,
            Func<List<int>, List<string>> orderTagNames = null,
            Action refreshAction = null)
        {
            if (items == null) return;

            var targets = items.Where(i => i != null).ToList();
            if (targets.Count == 0) return;

            var semaphore = new SemaphoreSlim(2, 2);
            var tasks = targets.Select(item => EnrichItemAsync(item, semaphore, cancellationToken, orderTagNames)).ToList();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception) { }

            if (cancellationToken.IsCancellationRequested) return;

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
            // Do NOT pass cancellationToken to WaitAsync - poll instead
            await semaphore.WaitAsync();
            try
            {
                if (cancellationToken.IsCancellationRequested) return;

                item.Tags = BuildTags(item.Path, item, orderTagNames);
                item.Notes = BuildNotes(item.Path);

                await EnrichMediaMetadataAsync(item, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task EnrichMediaMetadataAsync(FileSystemItem item, CancellationToken cancellationToken)
        {
            if (item.IsDirectory || string.IsNullOrEmpty(item.Path)) return;
            if (cancellationToken.IsCancellationRequested) return;

            string ext = System.IO.Path.GetExtension(item.Path).ToLowerInvariant();
            if (YiboFile.Services.Search.SearchFilterService.ImageExtensions.Contains(ext))
            {
                try
                {
                    // Do NOT pass cancellationToken to Task.Run - poll instead
                    await Task.Run(() =>
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        try
                        {
                            using (var fs = new System.IO.FileStream(item.Path, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                            {
                                var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                                    fs,
                                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation,
                                    System.Windows.Media.Imaging.BitmapCacheOption.None);

                                if (decoder.Frames.Count > 0)
                                {
                                    var frame = decoder.Frames[0];
                                    item.PixelWidth = frame.PixelWidth;
                                    item.PixelHeight = frame.PixelHeight;
                                }
                            }
                        }
                        catch (Exception) { }
                    });
                }
                catch { }
            }
            else if (YiboFile.Services.Search.SearchFilterService.VideoExtensions.Contains(ext) ||
                     YiboFile.Services.Search.SearchFilterService.AudioExtensions.Contains(ext))
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested) return;
                    long duration = YiboFile.Services.Core.ShellPropertyHelper.GetDuration(item.Path);
                    item.DurationMs = duration;
                }
                catch { }
            }
        }

        private string BuildTags(string path, FileSystemItem item, Func<List<int>, List<string>> orderTagNames)
        {
            try
            {
                if (_tagService == null) return string.Empty;

                var dbTags = _tagService.GetFileTags(path)?.ToList();
                if (dbTags == null || dbTags.Count == 0)
                {
                    item.TagList = new List<TagViewModel>();
                    return string.Empty;
                }

                item.TagList = dbTags.Select(t => new TagViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    Color = t.Color
                }).ToList();

                return string.Join(", ", dbTags.Select(t => t.Name));
            }
            catch (Exception)
            {
                item.TagList = new List<TagViewModel>();
                return string.Empty;
            }
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
    }
}