using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YiboFile.Services.FileSystem
{
    /// <summary>
    /// 文件系统核心工具类 - 提供跨模块的统一逻辑
    /// </summary>
    public static class FileSystemCoreUtils
    {
        /// <summary>
        /// 判断两个路径是否在同一个磁盘卷上
        /// </summary>
        public static bool IsSameVolume(string path1, string path2)
        {
            try
            {
                var d1 = Path.GetPathRoot(Path.GetFullPath(path1));
                var d2 = Path.GetPathRoot(Path.GetFullPath(path2));
                return string.Equals(d1, d2, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// 获取不冲突的文件夹/文件路径
        /// </summary>
        public static string GetUniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;

            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            int counter = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
                counter++;
            } while (File.Exists(newPath) || Directory.Exists(newPath));

            return newPath;
        }

        /// <summary>
        /// 安全删除文件（带重试机制，应对系统锁定）
        /// </summary>
        public static async Task SafeDeleteFileAsync(string path)
        {
            if (!File.Exists(path)) return;

            const int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    await Task.Delay(100 * (i + 1));
                }
            }

            // 终试：吞掉异常，文件已复制到目标，保留源文件比崩溃好
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
            catch { }
        }

        /// <summary>
        /// 安全删除目录（带重试机制）
        /// </summary>
        public static async Task SafeDeleteDirectoryAsync(string path)
        {
            if (!Directory.Exists(path)) return;

            const int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    RemoveReadOnlyRecursive(path);
                    Directory.Delete(path, true);
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    await Task.Delay(100 * (i + 1));
                }
            }

            // 终试
            try
            {
                RemoveReadOnlyRecursive(path);
                Directory.Delete(path, true);
            }
            catch { }
        }

        private static void RemoveReadOnlyRecursive(string dir)
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            foreach (var subDir in Directory.GetDirectories(dir))
            {
                RemoveReadOnlyRecursive(subDir);
            }
        }

        /// <summary>
        /// 递归复制目录
        /// </summary>
        public static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            // 安全检查：防止源目录和目标目录相同，或源目录是目标目录的子目录
            var normalizedSource = NormalizePath(sourceDir);
            var normalizedDest = NormalizePath(destinationDir);
            
            if (string.Equals(normalizedSource, normalizedDest, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("源目录和目标目录不能相同");
            }
            
            if (normalizedDest.StartsWith(normalizedSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("目标目录不能是源目录的子目录");
            }

            Directory.CreateDirectory(destinationDir);

            foreach (System.IO.FileInfo file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }

        /// <summary>
        /// 获取目录总大小
        /// </summary>
        public static long GetDirectorySize(string path)
        {
            if (!Directory.Exists(path)) return 0;
            var dirInfo = new DirectoryInfo(path);
            return dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => (long)fi.Length);
        }

        /// <summary>
        /// 路径规范化：移除末尾分隔符，转为绝对路径
        /// </summary>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch { return path; }
        }
    }
}
