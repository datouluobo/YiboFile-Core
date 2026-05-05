using System;
using YiboFile.Models;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YiboFile.Controls.Converters;

namespace YiboFile.Services.UI
{
    /// <summary>
    /// Simplified thumbnail service - async loads file thumbnails into data model.
    /// Uses polling cancellation only - no CancellationToken passed to Task.Run/Delay/Semaphore
    /// to avoid OperationCanceledException spam in debugger.
    /// </summary>
    public class ThumbnailService
    {
        private readonly ThumbnailConverter _converter = new();
        private CancellationTokenSource _cancellationTokenSource;
        private const int MaxConcurrentLoads = 6;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(MaxConcurrentLoads);

        public void LoadThumbnailsAsync(IEnumerable items, int thumbnailSize = 256)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Do NOT pass token to Task.Run - poll instead
            Task.Run(async () =>
            {
                try
                {
                    int index = 0;
                    foreach (var item in items)
                    {
                        if (token.IsCancellationRequested) break;

                        if (item is FileSystemItem fileItem && !string.IsNullOrEmpty(fileItem.Path))
                        {
                            if (fileItem.Thumbnail == null)
                            {
                                var placeholder = GetPlaceholder(thumbnailSize);
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    if (fileItem.Thumbnail == null)
                                        fileItem.Thumbnail = placeholder;
                                }, DispatcherPriority.Background);
                            }
                            else
                            {
                                // 已有缩略图（保留自旧集合），跳过重载
                                continue;
                            }

                            _ = LoadThumbnailForItemAsync(fileItem, thumbnailSize, token);
                        }

                        index++;
                        if (index % 10 == 0)
                        {
                            // Do NOT pass token to Task.Delay - poll instead
                            await Task.Delay(1);
                        }
                    }
                }
                catch (Exception) { }
            });
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource> _thumbnailCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource>();

        private async Task LoadThumbnailForItemAsync(FileSystemItem item, int size, CancellationToken token)
        {
            string cacheKey = $"{item.Path}|{size}|{item.ModifiedDateTime.Ticks}";

            if (_thumbnailCache.TryGetValue(cacheKey, out var cachedImage))
            {
                // 缓存命中：直接赋值（BitmapSource 不可变，跨线程赋值安全）
                // 跳过 Dispatcher.InvokeAsync，避免 Normal 优先级调度堆积
                if (!token.IsCancellationRequested && item.Thumbnail == null)
                    item.Thumbnail = cachedImage;
                return;
            }

            // Do NOT pass token to WaitAsync - poll instead
            await _semaphore.WaitAsync();
            try
            {
                if (token.IsCancellationRequested) return;

                BitmapSource thumbnail = null;
                try
                {
                    // Do NOT pass token to Task.Run - poll instead
                    thumbnail = await Task.Run(() => _converter.LoadThumbnailSync(item.Path, size));
                }
                catch
                {
                    // Ignore load exceptions
                }

                if (thumbnail != null && !token.IsCancellationRequested)
                {
                    CacheThumbnail(cacheKey, thumbnail);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            item.Thumbnail = thumbnail;
                        }
                    }, DispatcherPriority.Normal);
                }
                else if (thumbnail == null && !token.IsCancellationRequested)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            var placeholder = GetPlaceholder(size);
                            CacheThumbnail(cacheKey, placeholder);
                            item.Thumbnail = placeholder;
                        }
                    }, DispatcherPriority.Normal);
                }
            }
            catch (Exception) { }
            finally
            {
                _semaphore.Release();
            }
        }

        private void CacheThumbnail(string key, BitmapSource image)
        {
            if (_thumbnailCache.Count > 2000)
            {
                _thumbnailCache.Clear();
            }
            _thumbnailCache.TryAdd(key, image);
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, BitmapSource> _placeholders
            = new System.Collections.Concurrent.ConcurrentDictionary<int, BitmapSource>();

        private BitmapSource GetPlaceholder(int size)
        {
            return _placeholders.GetOrAdd(size, s =>
            {
                var renderTarget = new RenderTargetBitmap(s, s, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                var visual = new System.Windows.Media.DrawingVisual();
                using (var context = visual.RenderOpen())
                {
                    context.DrawRectangle(
                        new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                        new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220)), 1),
                        new Rect(0, 0, s, s));
                }
                renderTarget.Render(visual);
                renderTarget.Freeze();
                return renderTarget;
            });
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}