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
        /// <summary>
        /// 格式化相对时间
        /// </summary>
        public static string FormatTimeAgo(DateTime time)
        {
            var span = DateTime.Now - time;
            if (span.TotalSeconds < 60) return $"{Math.Max(0, (int)span.TotalSeconds)} 秒";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}'";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} 时";
            if (span.TotalDays < 65) return $"{(int)span.TotalDays} 天"; // 保持“天”直到约2个月
            if (span.TotalDays < 365) return $"{(int)(span.TotalDays / 30)} 月";
            return $"{(int)(span.TotalDays / 365)} 年";
        }

        /// <summary>
        /// 获取相对时间的背景颜色 (Hex String)
        /// </summary>
        public string CreatedTimeBrush
        {
            get
            {
                var span = DateTime.Now - CreatedDateTime;
                if (span.TotalMinutes < 60) return "#FFCDD2"; // Red 100 (分钟)
                if (span.TotalHours < 24) return "#FFF9C4";   // Yellow 100 (小时)
                if (span.TotalDays < 3) return "#FFF9C4";     // Yellow 100 (< 3天)
                if (span.TotalDays < 30) return "#C8E6C9";    // Green 100 (天)
                if (span.TotalDays < 90) return "#B2EBF2";    // Cyan 100 (1-3个月)
                if (span.TotalDays < 365) return "#BBDEFB";   // Blue 100 (月)
                return "#E1BEE7";                             // Purple 100 (年)
            }
        }
    }
}
