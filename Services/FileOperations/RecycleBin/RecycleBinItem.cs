using System;

namespace YiboFile.Services.FileOperations.RecycleBin
{
    /// <summary>
    /// 回收站中的单个文件/目录记录
    /// </summary>
    public class RecycleBinItem
    {
        /// <summary>原始完整路径（还原目标路径）</summary>
        public string OriginalPath { get; set; }

        /// <summary>文件名</summary>
        public string Name { get; set; }

        /// <summary>文件大小（字节）</summary>
        public long Size { get; set; }

        /// <summary>文件大小（格式化字符串）</summary>
        public string SizeDisplay { get; set; }

        /// <summary>是否为目录</summary>
        public bool IsDirectory { get; set; }

        /// <summary>删除时间（近似）</summary>
        public DateTime DeletionTime { get; set; }

        /// <summary>格式化后的删除日期</summary>
        public string DeletionDateDisplay => DeletionTime.ToString("yyyy-MM-dd HH:mm");

        /// <summary>备份路径（回收站中的实际位置，用于预览）</summary>
        public string BackupPath { get; set; }

        /// <summary>Shell item 索引（用于批量恢复优化）</summary>
        internal int ShellIndex { get; set; }

        /// <summary>原始所在目录</summary>
        public string OriginalDirectory { get; set; }
    }
}
