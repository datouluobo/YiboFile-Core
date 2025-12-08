using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OoiMRR.Services.FileList
{
    /// <summary>
    /// 文件列表加载服务
    /// 负责从文件系统加载文件和文件夹列表，创建 FileSystemItem 对象
    /// </summary>
    public class FileListService
    {
        #region 公共方法

        /// <summary>
        /// 从指定路径加载文件和文件夹列表
        /// </summary>
        /// <param name="path">要加载的路径</param>
        /// <param name="getFolderSizeCache">获取文件夹大小缓存的函数（可选）</param>
        /// <param name="formatFileSize">格式化文件大小的函数（可选）</param>
        /// <returns>文件系统项列表</returns>
        public List<FileSystemItem> LoadFileSystemItems(
            string path,
            Func<string, long?> getFolderSizeCache = null,
            Func<long, string> formatFileSize = null)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return new List<FileSystemItem>();
            }

            // 使用默认的格式化函数
            if (formatFileSize == null)
            {
                formatFileSize = FormatFileSize;
            }

            var items = new List<FileSystemItem>();

            // 加载文件夹
            var directories = LoadDirectories(path, getFolderSizeCache, formatFileSize);
            items.AddRange(directories);

            // 加载文件
            var files = LoadFiles(path, formatFileSize);
            items.AddRange(files);

            return items;
        }

        /// <summary>
        /// 从多个路径加载文件系统项，合并结果（同名项保留第一个）
        /// </summary>
        /// <param name="paths">要加载的路径列表</param>
        /// <param name="getFolderSizeCache">获取文件夹大小缓存的函数（可选）</param>
        /// <param name="formatFileSize">格式化文件大小的函数（可选）</param>
        /// <returns>合并后的文件系统项列表</returns>
        public List<FileSystemItem> LoadFileSystemItemsFromMultiplePaths(
            IEnumerable<string> paths,
            Func<string, long?> getFolderSizeCache = null,
            Func<long, string> formatFileSize = null)
        {
            var allItems = new Dictionary<string, FileSystemItem>();

            foreach (var path in paths)
            {
                if (!Directory.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"[FileListService] 路径不存在: {path}");
                    continue;
                }

                try
                {
                    var items = LoadFileSystemItems(path, getFolderSizeCache, formatFileSize);
                    foreach (var item in items)
                    {
                        var key = item.Name.ToLowerInvariant();
                        if (!allItems.ContainsKey(key))
                        {
                            allItems[key] = item;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileListService] 加载路径失败 {path}: {ex.Message}");
                }
            }

            return allItems.Values.ToList();
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        /// <param name="bytes">字节数</param>
        /// <returns>格式化后的字符串</returns>
        public string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 加载文件夹列表
        /// </summary>
        private List<FileSystemItem> LoadDirectories(
            string path,
            Func<string, long?> getFolderSizeCache,
            Func<long, string> formatFileSize)
        {
            var directories = new List<FileSystemItem>();
            try
            {
                var dirPaths = Directory.GetDirectories(path);
                foreach (var dirPath in dirPaths)
                {
                    try
                    {
                        // 检查文件夹是否存在（如果不存在，清理数据库缓存）
                        if (!Directory.Exists(dirPath))
                        {
                            DatabaseManager.RemoveFolderSize(dirPath);
                            continue;
                        }

                        var dirInfo = new DirectoryInfo(dirPath);

                        // 从数据库读取文件夹大小缓存
                        string sizeDisplay = "计算中...";
                        if (getFolderSizeCache != null)
                        {
                            var cachedSize = getFolderSizeCache(dirPath);
                            if (cachedSize.HasValue)
                            {
                                sizeDisplay = formatFileSize(cachedSize.Value);
                            }
                        }

                        directories.Add(new FileSystemItem
                        {
                            Name = Path.GetFileName(dirPath),
                            Path = dirInfo.FullName,
                            Type = "文件夹",
                            Size = sizeDisplay,
                            ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            CreatedTime = FileSystemItem.FormatTimeAgo(dirInfo.CreationTime),
                            IsDirectory = true,
                            SourcePath = path // 标记来源路径
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问文件夹: {dirPath}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 处理文件夹失败 {dirPath}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问路径: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 获取文件夹列表失败 {path}: {ex.Message}");
            }

            return directories;
        }

        /// <summary>
        /// 加载文件列表
        /// </summary>
        private List<FileSystemItem> LoadFiles(string path, Func<long, string> formatFileSize)
        {
            var files = new List<FileSystemItem>();
            try
            {
                var filePaths = Directory.GetFiles(path);
                foreach (var filePath in filePaths)
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        files.Add(new FileSystemItem
                        {
                            Name = Path.GetFileName(filePath),
                            Path = fileInfo.FullName,
                            Type = FileTypeManager.GetFileCategory(fileInfo.FullName),
                            Size = formatFileSize(fileInfo.Length),
                            ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            CreatedTime = FileSystemItem.FormatTimeAgo(fileInfo.CreationTime),
                            IsDirectory = false,
                            SourcePath = path // 标记来源路径
                        });
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问文件: {filePath}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileListService] 处理文件失败 {filePath}: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 无权限访问路径: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileListService] 获取文件列表失败 {path}: {ex.Message}");
            }

            return files;
        }

        #endregion
    }
}


