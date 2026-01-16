using System;
using System.IO;
using System.Linq;
using YiboFile.Services.Core;

namespace YiboFile.Services.FileOperations.Undo
{
    /// <summary>
    /// 备份清理服务 - 防止备份目录无限增长
    /// </summary>
    public static class BackupCleanupService
    {
        // 默认配置
        public static long MaxBackupSizeMB { get; set; } = 500; // 最大 500MB
        public static int MaxBackupAgeDays { get; set; } = 7;   // 最多保留 7 天
        public static int MaxBackupCount { get; set; } = 100;   // 最多 100 个备份

        /// <summary>
        /// 执行备份清理
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                var backupDir = DeleteUndoAction.BackupDirectory;
                if (!Directory.Exists(backupDir)) return;

                var entries = Directory.GetFileSystemEntries(backupDir)
                    .Select(p => new
                    {
                        Path = p,
                        Time = GetCreationTime(p),
                        Size = GetSize(p)
                    })
                    .OrderByDescending(e => e.Time)
                    .ToList();

                if (entries.Count == 0) return;

                var now = DateTime.Now;
                long totalSize = entries.Sum(e => e.Size);
                int deleteCount = 0;

                // 按时间倒序遍历，删除超出限制的
                for (int i = entries.Count - 1; i >= 0; i--)
                {
                    var entry = entries[i];
                    bool shouldDelete = false;

                    // 检查年龄限制
                    if ((now - entry.Time).TotalDays > MaxBackupAgeDays)
                    {
                        shouldDelete = true;
                    }
                    // 检查数量限制
                    else if (i >= MaxBackupCount)
                    {
                        shouldDelete = true;
                    }
                    // 检查大小限制
                    else if (totalSize > MaxBackupSizeMB * 1024 * 1024)
                    {
                        shouldDelete = true;
                    }

                    if (shouldDelete)
                    {
                        try
                        {
                            if (Directory.Exists(entry.Path))
                                Directory.Delete(entry.Path, true);
                            else if (File.Exists(entry.Path))
                                File.Delete(entry.Path);

                            totalSize -= entry.Size;
                            deleteCount++;
                        }
                        catch { }
                    }
                }

                if (deleteCount > 0)
                {
                    FileLogger.Log($"[BackupCleanup] 已清理 {deleteCount} 个过期备份");
                }
            }
            catch (Exception ex)
            {
                FileLogger.Log($"[BackupCleanup] 清理失败: {ex.Message}");
            }
        }

        private static DateTime GetCreationTime(string path)
        {
            try
            {
                return Directory.Exists(path)
                    ? Directory.GetCreationTime(path)
                    : File.GetCreationTime(path);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private static long GetSize(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                        .Sum(f => new System.IO.FileInfo(f).Length);
                }
                else if (File.Exists(path))
                {
                    return new System.IO.FileInfo(path).Length;
                }
            }
            catch { }
            return 0;
        }
    }
}

