using System;

namespace OoiMRR
{
    public class FileSystemItem
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public string Size { get; set; }
        public string ModifiedDate { get; set; }
        public string CreatedTime { get; set; }
        public string Tags { get; set; }
        public string Notes { get; set; }
        public bool IsDirectory { get; set; }
        public string SourcePath { get; set; } // 库模式下的来源路径
        
        // 原始数据用于排序，避免字符串解析异常
        public long SizeBytes { get; set; } = -1;
        public DateTime ModifiedDateTime { get; set; } = DateTime.MinValue;
        public DateTime CreatedDateTime { get; set; } = DateTime.MinValue;

        /// <summary>
        /// 是否来自备注搜索
        /// </summary>
        public bool IsFromNotesSearch { get; set; }
        
        /// <summary>
        /// 是否来自名称搜索
        /// </summary>
        public bool IsFromNameSearch { get; set; }
        
        /// <summary>
        /// 搜索结果类型（用于搜索结果显示）
        /// </summary>
        public OoiMRR.Services.Search.SearchResultType? SearchResultType { get; set; }
        
        /// <summary>
        /// 格式化相对时间
        /// </summary>
        public static string FormatTimeAgo(DateTime time)
        {
            var span = DateTime.Now - time;
            if (span.TotalSeconds < 60) return $"{Math.Max(0, (int)span.TotalSeconds)}s ago";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 30) return $"{(int)span.TotalDays}d ago";
            if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)}mo ago";
            return $"{(int)(span.TotalDays / 365)}y ago";
        }
    }
}
