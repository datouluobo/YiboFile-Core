using System;
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
    /// 简化的缩略图服务 - 负责异步加载文件缩略图到数据模型
    /// 替代原来的 ThumbnailViewManager（471行），简化架构
    /// </summary>
    public class ThumbnailService
    {
        private readonly ThumbnailConverter _converter = new();
        private CancellationTokenSource _cancellationTokenSource;
        private const int BatchDelayMs = 2; // 批处理延迟 (不再频繁使用)
        private const int MaxConcurrentLoads = 6; // 最大并发数
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(MaxConcurrentLoads);

        /// <summary>
        /// 为文件列表异步加载缩略图
        /// </summary>
        /// <param name="items">文件列表</param>
        /// <param name="thumbnailSize">缩略图大小 (默认256以支持高清缩放)</param>
        public void LoadThumbnailsAsync(IEnumerable items, int thumbnailSize = 256)
        {
            // 取消之前的加载任务
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

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
                            // 先设置占位符（立即显示）
                            if (fileItem.Thumbnail == null)
                            {
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    fileItem.Thumbnail = CreatePlaceholder(thumbnailSize);
                                }, DispatcherPriority.Normal);
                            }

                            // 异步加载真实缩略图
                            _ = LoadThumbnailForItemAsync(fileItem, thumbnailSize, token);
                        }

                        // 优化：大幅减少人为延迟
                        // 原逻辑：每项Delay 20ms -> 100项耗时2秒
                        // 新逻辑：每10项Delay 1ms -> 100项耗时10ms
                        index++;
                        if (index % 10 == 0)
                        {
                            await Task.Delay(1, token);
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception)
                {
                    // 忽略其他异常
                }
            }, token);
        }

        // 静态缓存，跨实例（Tab）共享
        // Key: Path|ModifiedTicks, Value: BitmapSource
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource> _thumbnailCache
            = new System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource>();

        /// <summary>
        /// 为单个文件异步加载缩略图
        /// </summary>
        private async Task LoadThumbnailForItemAsync(FileSystemItem item, int size, CancellationToken token)
        {
            // 生成缓存Key
            string cacheKey = $"{item.Path}|{item.ModifiedDateTime.Ticks}";

            // 1. 尝试从缓存获取
            if (_thumbnailCache.TryGetValue(cacheKey, out var cachedImage))
            {
                if (!token.IsCancellationRequested)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested) item.Thumbnail = cachedImage;
                    }, DispatcherPriority.Normal);
                }
                return;
            }

            // 2. 缓存未命中，执行加载
            await _semaphore.WaitAsync(token);
            try
            {
                if (token.IsCancellationRequested) return;

                // 后台线程加载缩略图
                BitmapSource thumbnail = null;
                try
                {
                    thumbnail = await Task.Run(() => _converter.LoadThumbnailSync(item.Path, size), token);
                }
                catch
                {
                    // 忽略加载异常，thumbnail保持为null
                }

                if (thumbnail != null && !token.IsCancellationRequested)
                {
                    // 成功加载，存入缓存
                    CacheThumbnail(cacheKey, thumbnail);

                    // UI线程更新数据模型
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            item.Thumbnail = thumbnail;
                        }
                    }, DispatcherPriority.Normal);
                }
                else if (thumbnail == null && !token.IsCancellationRequested) // 加载失败
                {
                    // 加载失败（如文件夹或其他不支持的文件），生成并缓存占位符
                    // 这样下次就不会重复尝试加载了
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            var placeholder = CreatePlaceholder(size);
                            CacheThumbnail(cacheKey, placeholder);

                            // 即使已经是占位符了，也更新一下确保使用缓存对象
                            item.Thumbnail = placeholder;
                        }
                    }, DispatcherPriority.Normal);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
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

        /// <summary>
        /// 创建占位符图片
        /// </summary>
        private BitmapSource CreatePlaceholder(int size)
        {
            var renderTarget = new RenderTargetBitmap(size, size, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var visual = new System.Windows.Media.DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(
                    new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)),
                    new System.Windows.Media.Pen(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)), 1),
                    new Rect(0, 0, size, size));
            }
            renderTarget.Render(visual);
            renderTarget.Freeze();
            return renderTarget;
        }

        /// <summary>
        /// 停止所有加载任务
        /// </summary>
        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }
    }
}

