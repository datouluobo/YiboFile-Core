using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YiboFile.Services.Search
{
    /// <summary>
    /// 搜索结果构建器
    /// 负责从文件路径构建 FileSystemItem 对象
    /// </summary>
    public class SearchResultBuilder
    {
        private readonly Func<string, long?> _getFolderSizeCache;
        private readonly Func<long, string> _formatFileSize;
        private readonly Func<string, List<int>> _getFileTagIds;
        private readonly Func<int, string> _getTagName;
        private readonly Func<string, string> _getFileNotes;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="formatFileSize">格式化文件大小的函数</param>
        /// <param name="getFileTagIds">获取文件标签ID的函数（可选）</param>
        /// <param name="getTagName">获取标签名称的函数（可选）</param>
        /// <param name="getFileNotes">获取文件备注的函数（可选）</param>
        /// <param name="getFolderSizeCache">获取文件夹大小缓存的函数（可选）</param>
        public SearchResultBuilder(
            Func<long, string> formatFileSize,
            Func<string, List<int>> getFileTagIds = null,
            Func<int, string> getTagName = null,
            Func<string, string> getFileNotes = null,
            Func<string, long?> getFolderSizeCache = null)
        {
            _formatFileSize = formatFileSize ?? throw new ArgumentNullException(nameof(formatFileSize));
            _getFileTagIds = getFileTagIds;
            _getTagName = getTagName;
            _getFileNotes = getFileNotes;
            _getFolderSizeCache = getFolderSizeCache;
        }

        /// <summary>
        /// 从路径列表构建 FileSystemItem 列表
        /// </summary>
        /// <param name="paths">文件路径列表</param>
        /// <returns>FileSystemItem 列表</returns>
        public List<FileSystemItem> BuildItemsFromPaths(IEnumerable<string> paths)
        {
            var list = new List<FileSystemItem>();

            if (paths == null)
                return list;

            foreach (var filePath in paths)
            {
                if (!File.Exists(filePath) && !Directory.Exists(filePath))
                    continue;

                try
                {
                    var item = CreateFileSystemItem(filePath);
                    if (item != null)
                    {
                        list.Add(item);
                    }
                }
                catch (Exception)
                {
                }
            }

            return list;
        }

        /// <summary>
        /// 从单个路径创建 FileSystemItem
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>FileSystemItem 对象</returns>
        private FileSystemItem CreateFileSystemItem(string filePath)
        {
            bool isDirectory = Directory.Exists(filePath);

            var item = new FileSystemItem
            {
                Name = Path.GetFileName(filePath),
                Path = filePath,
                Type = isDirectory ? "文件夹" : Path.GetExtension(filePath),
                IsDirectory = isDirectory
            };

            // 设置大小和修改时间
            if (isDirectory)
            {
                item.Size = "";
                item.ModifiedDate = Directory.GetLastWriteTime(filePath).ToString("yyyy-MM-dd HH:mm");
                item.CreatedTime = FileSystemItem.FormatTimeAgo(Directory.GetCreationTime(filePath));
                item.ModifiedDateTime = Directory.GetLastWriteTime(filePath);
                item.CreatedDateTime = Directory.GetCreationTime(filePath);
            }
            else
            {
                try
                {
                    var fileInfo = new System.IO.FileInfo(filePath);
                    item.Size = _formatFileSize(fileInfo.Length);
                    item.ModifiedDate = File.GetLastWriteTime(filePath).ToString("yyyy-MM-dd HH:mm");
                    item.CreatedTime = FileSystemItem.FormatTimeAgo(fileInfo.CreationTime);
                    item.SizeBytes = fileInfo.Length;
                    item.ModifiedDateTime = fileInfo.LastWriteTime;
                    item.CreatedDateTime = fileInfo.CreationTime;
                }
                catch
                {
                    item.Size = "";
                    item.ModifiedDate = "";
                    item.CreatedTime = "";
                }
            }

            // 设置标签（如果可用）
            if (_getFileTagIds != null && _getTagName != null)
            {
                try
                {
                    var fileTagIds = _getFileTagIds(item.Path);
                    if (fileTagIds != null && fileTagIds.Count > 0)
                    {
                        var fileTagNames = fileTagIds
                            .Select(tagId => _getTagName(tagId))
                            .Where(name => !string.IsNullOrEmpty(name))
                            .ToList();
                        item.Tags = string.Join(", ", fileTagNames);
                    }
                    else
                    {
                        item.Tags = "";
                    }
                }
                catch
                {
                    item.Tags = "";
                }
            }
            else
            {
                item.Tags = "";
            }

            // 设置备注
            if (_getFileNotes != null)
            {
                try
                {
                    var notes = _getFileNotes(item.Path);
                    if (!string.IsNullOrEmpty(notes))
                    {
                        var firstLine = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .FirstOrDefault() ?? "";
                        item.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                    }
                    else
                    {
                        item.Notes = "";
                    }
                }
                catch (Exception)
                {
                    item.Notes = "";
                }
            }
            else
            {
                item.Notes = "";
            }

            return item;
        }

        /// <summary>
        /// 对搜索结果进行排序（按相关性分数）
        /// </summary>
        /// <param name="paths">路径列表</param>
        /// <param name="keyword">搜索关键词</param>
        /// <returns>排序后的路径列表</returns>
        public IEnumerable<string> SortByRelevance(IEnumerable<string> paths, string keyword)
        {
            if (paths == null || string.IsNullOrEmpty(keyword))
                return paths ?? Enumerable.Empty<string>();

            var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in paths)
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                    continue;

                int score = 0;
                var name = Path.GetFileName(path);

                // 完全匹配文件名：最高分
                if (string.Equals(name, keyword, StringComparison.OrdinalIgnoreCase))
                    score += 100;
                // 文件名包含关键词：高分
                else if (!string.IsNullOrEmpty(keyword) && name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 80;

                // 路径包含关键词：中分
                if (!string.IsNullOrEmpty(keyword) && path.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 70;

                scores[path] = score;
            }

            return paths.OrderByDescending(p => scores.ContainsKey(p) ? scores[p] : 0);
        }
    }
}


