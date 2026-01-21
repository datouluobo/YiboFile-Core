using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YiboFile.Models;

namespace YiboFile.Services.Search
{
    /// <summary>
    /// 搜索过滤器服务
    /// 负责应用类型过滤和路径范围过滤
    /// </summary>
    public class SearchFilterService
    {
        #region 文件类型扩展名定义

        public static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff"
        };

        public static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".flv", ".webm"
        };

        public static readonly HashSet<string> AudioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"
        };

        public static readonly HashSet<string> DocumentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt"
        };

        #endregion

        /// <summary>
        /// 应用类型过滤器
        /// </summary>
        /// <param name="paths">要过滤的路径列表</param>
        /// <param name="typeFilter">类型过滤器</param>
        /// <returns>过滤后的路径列表</returns>
        public IEnumerable<string> ApplyTypeFilter(IEnumerable<string> paths, FileTypeFilter typeFilter)
        {
            if (paths == null)
                return Enumerable.Empty<string>();

            switch (typeFilter)
            {
                case FileTypeFilter.Images:
                    return paths.Where(p => ImageExtensions.Contains(Path.GetExtension(p)));

                case FileTypeFilter.Videos:
                    return paths.Where(p => VideoExtensions.Contains(Path.GetExtension(p)));

                case FileTypeFilter.Audio:
                    return paths.Where(p => AudioExtensions.Contains(Path.GetExtension(p)));

                case FileTypeFilter.Documents:
                    return paths.Where(p => DocumentExtensions.Contains(Path.GetExtension(p)));

                case FileTypeFilter.Folders:
                    return paths.Where(p => Directory.Exists(p));

                case FileTypeFilter.All:
                default:
                    return paths;
            }
        }

        /// <summary>
        /// 获取路径范围（根据路径范围过滤器）
        /// </summary>
        /// <param name="pathRangeFilter">路径范围过滤器</param>
        /// <param name="currentPath">当前路径</param>
        /// <returns>限制搜索的根路径，null 表示不限制</returns>
        public string GetRangePath(PathRangeFilter pathRangeFilter, string currentPath)
        {
            if (pathRangeFilter == PathRangeFilter.CurrentDrive && !string.IsNullOrEmpty(currentPath))
            {
                // Prevent crash on virtual paths
                if (Services.Core.ProtocolManager.IsVirtual(currentPath)) return null;

                try
                {
                    var driveInfo = new DriveInfo(currentPath);
                    return driveInfo.RootDirectory.FullName;
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取当前文件夹路径（用于 CurrentFolder 范围）
        /// </summary>
        public string GetCurrentFolderPath(PathRangeFilter pathRangeFilter, string currentPath)
        {
            if (pathRangeFilter == PathRangeFilter.CurrentFolder && !string.IsNullOrEmpty(currentPath))
            {
                return currentPath;
            }
            return GetRangePath(pathRangeFilter, currentPath);
        }

        /// <summary>
        /// 应用日期过滤器
        /// </summary>
        public IEnumerable<FileSystemItem> ApplyDateFilter(IEnumerable<FileSystemItem> items, DateRangeFilter dateFilter, DateTime? customFrom = null, DateTime? customTo = null)
        {
            if (items == null || dateFilter == DateRangeFilter.All)
                return items ?? Enumerable.Empty<FileSystemItem>();

            var now = DateTime.Now;
            DateTime? minDate = null;
            DateTime? maxDate = null;

            switch (dateFilter)
            {
                case DateRangeFilter.Today:
                    minDate = now.Date;
                    maxDate = now.Date.AddDays(1).AddSeconds(-1);
                    break;
                case DateRangeFilter.ThisWeek:
                    var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
                    minDate = startOfWeek;
                    maxDate = now;
                    break;
                case DateRangeFilter.ThisMonth:
                    minDate = new DateTime(now.Year, now.Month, 1);
                    maxDate = now;
                    break;
                case DateRangeFilter.ThisYear:
                    minDate = new DateTime(now.Year, 1, 1);
                    maxDate = now;
                    break;
                case DateRangeFilter.Custom:
                    minDate = customFrom;
                    maxDate = customTo;
                    break;
            }

            return items.Where(item =>
            {
                var modTime = item.ModifiedDateTime;
                if (modTime == default) return true; // 无日期信息则保留
                if (minDate.HasValue && modTime < minDate.Value) return false;
                if (maxDate.HasValue && modTime > maxDate.Value) return false;
                return true;
            });
        }

        /// <summary>
        /// 应用大小过滤器
        /// </summary>
        public IEnumerable<FileSystemItem> ApplySizeFilter(IEnumerable<FileSystemItem> items, SizeRangeFilter sizeFilter, long? customMin = null, long? customMax = null)
        {
            if (items == null || sizeFilter == SizeRangeFilter.All)
                return items ?? Enumerable.Empty<FileSystemItem>();

            long? minSize = null;
            long? maxSize = null;

            const long KB = 1024;
            const long MB = 1024 * KB;

            switch (sizeFilter)
            {
                case SizeRangeFilter.Tiny:      // < 100KB
                    maxSize = 100 * KB;
                    break;
                case SizeRangeFilter.Small:     // 100KB - 1MB
                    minSize = 100 * KB;
                    maxSize = MB;
                    break;
                case SizeRangeFilter.Medium:    // 1MB - 10MB
                    minSize = MB;
                    maxSize = 10 * MB;
                    break;
                case SizeRangeFilter.Large:     // 10MB - 100MB
                    minSize = 10 * MB;
                    maxSize = 100 * MB;
                    break;
                case SizeRangeFilter.Huge:      // > 100MB
                    minSize = 100 * MB;
                    break;
                case SizeRangeFilter.Custom:
                    minSize = customMin;
                    maxSize = customMax;
                    break;
            }

            return items.Where(item =>
            {
                if (item.IsDirectory) return true; // 文件夹不按大小过滤
                var size = item.SizeBytes >= 0 ? item.SizeBytes : 0;
                if (minSize.HasValue && size < minSize.Value) return false;
                if (maxSize.HasValue && size > maxSize.Value) return false;
                return true;
            });
        }
        public IEnumerable<FileSystemItem> ApplyImageDimensionFilter(IEnumerable<FileSystemItem> items, ImageDimensionFilter filter)
        {
            if (items == null || filter == ImageDimensionFilter.All)
                return items ?? Enumerable.Empty<FileSystemItem>();

            return items.Where(item =>
            {
                if (item.IsDirectory) return false;
                // Assuming width is populated. If 0, it counts as Small? Or ignore?
                // Let's assume 0 means unknown, but technically 0 < 800.
                // If we want to filter rigidly, maybe exclude 0 unless filtering for "Tiny"?
                // For now, treat 0 as small.
                int width = item.PixelWidth > 0 ? item.PixelWidth : 0;
                int height = item.PixelHeight > 0 ? item.PixelHeight : 0;
                int maxDim = Math.Max(width, height);

                return filter switch
                {
                    ImageDimensionFilter.Small => maxDim < 800,
                    ImageDimensionFilter.Medium => maxDim >= 800 && maxDim < 1920,
                    ImageDimensionFilter.Large => maxDim >= 1920 && maxDim < 3840,
                    ImageDimensionFilter.Huge => maxDim >= 3840,
                    _ => true
                };
            });
        }

        public IEnumerable<FileSystemItem> ApplyAudioDurationFilter(IEnumerable<FileSystemItem> items, AudioDurationFilter filter)
        {
            if (items == null || filter == AudioDurationFilter.All)
                return items ?? Enumerable.Empty<FileSystemItem>();

            return items.Where(item =>
            {
                if (item.IsDirectory) return false;
                long duration = item.DurationMs;

                const long Minute = 60 * 1000;

                return filter switch
                {
                    AudioDurationFilter.Short => duration < Minute,
                    AudioDurationFilter.Medium => duration >= Minute && duration < 5 * Minute,
                    AudioDurationFilter.Long => duration >= 5 * Minute && duration < 20 * Minute,
                    AudioDurationFilter.VeryLong => duration >= 20 * Minute,
                    _ => true
                };
            });
        }
    }
}

























