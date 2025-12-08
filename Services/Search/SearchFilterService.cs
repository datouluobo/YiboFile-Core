using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OoiMRR.Services.Search
{
    /// <summary>
    /// 搜索过滤器服务
    /// 负责应用类型过滤和路径范围过滤
    /// </summary>
    public class SearchFilterService
    {
        #region 文件类型扩展名定义

        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff"
        };

        private static readonly HashSet<string> VideoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".flv", ".webm"
        };

        private static readonly HashSet<string> DocumentExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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
    }
}


