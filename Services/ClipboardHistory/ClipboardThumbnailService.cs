using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YiboFile.Services.ClipboardHistory
{
    /// <summary>
    /// 剪切板缩略图生成与缓存服务
    /// </summary>
    public static class ClipboardThumbnailService
    {
        private static readonly string ThumbnailDir = Path.Combine(
            ConfigManager.GetBaseDirectory(), "clipboard_thumbnails");

        /// <summary>最大缩略图尺寸</summary>
        private const int MaxThumbnailSize = 200;

        /// <summary>
        /// 为图片文件生成缩略图，返回缓存路径
        /// </summary>
        public static string GenerateFileThumbnail(string imagePath)
        {
            try
            {
                Directory.CreateDirectory(ThumbnailDir);
                var hash = GetPathHash(imagePath);
                var cachePath = Path.Combine(ThumbnailDir, $"{hash}.jpg");

                if (File.Exists(cachePath)) return cachePath;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath);
                bitmap.DecodePixelWidth = MaxThumbnailSize;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                SaveBitmapToFile(bitmap, cachePath);
                return cachePath;
            }
            catch { return null; }
        }

        /// <summary>
        /// 保存截屏 BitmapSource 到文件，返回缓存路径
        /// </summary>
        public static string SaveScreenCapture(BitmapSource bitmap)
        {
            try
            {
                Directory.CreateDirectory(ThumbnailDir);
                var fileName = $"capture_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
                var fullPath = Path.Combine(ThumbnailDir, fileName);

                using var stream = new FileStream(fullPath, FileMode.Create);
                var encoder = new PngBitmapEncoder();
                
                // Fix for WPF Clipboard transparent image bug: drop the alpha channel
                BitmapSource sourceToSave = bitmap;
                if (bitmap.Format != PixelFormats.Bgr32)
                {
                    try { sourceToSave = new FormatConvertedBitmap(bitmap, PixelFormats.Bgr32, null, 0); }
                    catch { /* fallback if format conversion is not supported */ }
                }

                encoder.Frames.Add(BitmapFrame.Create(sourceToSave));
                encoder.Save(stream);

                // 同时生成缩略图
                var thumbPath = Path.Combine(ThumbnailDir, $"thumb_{fileName}");
                GenerateThumbnailFromBitmap(bitmap, thumbPath);

                return fullPath;
            }
            catch { return null; }
        }

        /// <summary>
        /// 从 BitmapSource 生成缩略图
        /// </summary>
        public static string GenerateThumbnailFromBitmap(BitmapSource source, string outputPath)
        {
            try
            {
                double scale = Math.Min(
                    (double)MaxThumbnailSize / source.PixelWidth,
                    (double)MaxThumbnailSize / source.PixelHeight);

                if (scale >= 1.0)
                {
                    // 原图已足够小
                    SaveBitmapSourceToFile(source, outputPath);
                    return outputPath;
                }

                var transform = new ScaleTransform(scale, scale);
                var thumbnailBitmap = new TransformedBitmap(source, transform);
                thumbnailBitmap.Freeze();

                SaveBitmapSourceToFile(thumbnailBitmap, outputPath);
                return outputPath;
            }
            catch { return null; }
        }

        /// <summary>清理过期缩略图缓存</summary>
        public static void CleanupThumbnails(int retentionDays)
        {
            try
            {
                if (!Directory.Exists(ThumbnailDir)) return;
                var cutoff = DateTime.Now.AddDays(-retentionDays);
                foreach (var file in Directory.GetFiles(ThumbnailDir))
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch { }
        }

        private static string GetPathHash(string path)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(path));
            return BitConverter.ToString(bytes[..8]).Replace("-", "").ToLower();
        }

        private static void SaveBitmapSourceToFile(BitmapSource bitmap, string path)
        {
            using var stream = new FileStream(path, FileMode.Create);
            var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        }

        // 别名
        private static void SaveBitmapToFile(BitmapImage bitmap, string path)
            => SaveBitmapSourceToFile(bitmap, path);
    }
}
