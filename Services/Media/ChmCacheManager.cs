using System;
using System.IO;
using System.Linq;

namespace YiboFile.Services
{
    /// <summary>
    /// CHM 缓存管理器
    /// </summary>
    public static class ChmCacheManager
    {
        private static readonly string CacheBaseDir = Path.Combine(Path.GetTempPath(), "MRR_CHM_Cache");
        private const long MaxCacheSizeBytes = 250 * 1024 * 1024; // 250MB (从500MB降低，减少磁盘占用)
        private const int CacheExpirationDays = 7;

        /// <summary>
        /// 清理过期的缓存（超过7天未访问）
        /// </summary>
        public static void CleanupExpiredCache()
        {
            if (!Directory.Exists(CacheBaseDir))
                return;

            try
            {
                var now = DateTime.UtcNow;
                var dirs = Directory.GetDirectories(CacheBaseDir);

                foreach (var dir in dirs)
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        if ((now - dirInfo.LastAccessTime).TotalDays > CacheExpirationDays)
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 强制执行缓存大小限制（最大250MB）
        /// </summary>
        public static void EnforceCacheSizeLimit()
        {
            if (!Directory.Exists(CacheBaseDir))
                return;

            try
            {
                var dirs = Directory.GetDirectories(CacheBaseDir)
                    .Select(d => new DirectoryInfo(d))
                    .OrderBy(d => d.LastAccessTime)
                    .ToList();

                long totalSize = 0;
                foreach (var dir in dirs)
                {
                    totalSize += GetDirectorySize(dir);
                }

                // 如果超过限制，删除最旧的缓存
                int index = 0;
                while (totalSize > MaxCacheSizeBytes && index < dirs.Count)
                {
                    var dirToDelete = dirs[index];
                    long dirSize = GetDirectorySize(dirToDelete);

                    try
                    {
                        dirToDelete.Delete(true);
                        totalSize -= dirSize;
                    }
                    catch
                    {
                    }

                    index++;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// 获取目录大小
        /// </summary>
        private static long GetDirectorySize(DirectoryInfo dir)
        {
            try
            {
                return dir.GetFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 清除所有缓存
        /// </summary>
        public static void ClearAllCache()
        {
            if (Directory.Exists(CacheBaseDir))
            {
                try
                {
                    Directory.Delete(CacheBaseDir, true);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public static (int Count, long SizeBytes) GetCacheStats()
        {
            if (!Directory.Exists(CacheBaseDir))
                return (0, 0);

            try
            {
                var dirs = Directory.GetDirectories(CacheBaseDir);
                long totalSize = dirs.Sum(d => GetDirectorySize(new DirectoryInfo(d)));
                return (dirs.Length, totalSize);
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}

