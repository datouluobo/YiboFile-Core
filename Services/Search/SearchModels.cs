using System;
using System.Collections.Generic;

namespace YiboFile.Services.Search
{
    /// <summary>
    /// 搜索结果类型
    /// </summary>
    public enum SearchResultType
    {
        Notes,      // 备注匹配
        Folder,     // 文件夹匹配
        File,       // 文件匹配
        Tag,        // 标签匹配（未来扩展）
        Date,       // 日期匹配（未来扩展）
        Other       // 其他类型（未来扩展）
    }

    /// <summary>
    /// 文件类型过滤器
    /// </summary>
    public enum FileTypeFilter
    {
        All,        // 全部
        Images,     // 图片
        Videos,     // 视频
        Audio,      // 音频
        Documents,  // 文档
        Folders     // 文件夹
    }

    /// <summary>
    /// 路径范围过滤器
    /// </summary>
    public enum PathRangeFilter
    {
        AllDrives,      // 全部磁盘
        CurrentDrive,   // 当前磁盘
        CurrentFolder   // 当前文件夹及子目录
    }

    /// <summary>
    /// 日期范围过滤器
    /// </summary>
    public enum DateRangeFilter
    {
        All,         // 全部时间
        Today,       // 今天
        ThisWeek,    // 本周
        ThisMonth,   // 本月
        ThisYear,    // 今年
        Custom       // 自定义范围
    }

    /// <summary>
    /// 文件大小过滤器
    /// </summary>
    public enum SizeRangeFilter
    {
        All,         // 全部大小
        Tiny,        // < 100KB
        Small,       // 100KB - 1MB
        Medium,      // 1MB - 10MB
        Large,       // 10MB - 100MB
        Huge,        // > 100MB
        Custom       // 自定义范围
    }

    /// <summary>
    /// 图片尺寸过滤器
    /// </summary>
    public enum ImageDimensionFilter
    {
        All,        // 全部
        Small,      // 小 (< 800px)
        Medium,     // 中 (800px - 1920px)
        Large,      // 大 (> 1920px)
        Huge        // 超大 (> 3840px)
    }

    /// <summary>
    /// 音频/视频时长过滤器
    /// </summary>
    public enum AudioDurationFilter
    {
        All,        // 全部
        Short,      // 短 (< 1 min)
        Medium,     // 中 (1 - 5 min)
        Long,       // 长 (5 - 20 min)
        VeryLong    // 超长 (> 20 min)
    }

    /// <summary>
    /// 搜索模式
    /// </summary>
    public enum SearchMode
    {
        FileName,    // 文件名搜索
        Folder,      // 文件夹名搜索
        Content,     // 内容搜索 (未来)
        Notes,       // 备注搜索
        Tags,        // 标签搜索
        All          // 组合搜索
    }

    /// <summary>
    /// 搜索选项
    /// </summary>
    public class SearchOptions
    {
        public FileTypeFilter Type { get; set; } = FileTypeFilter.All;
        public PathRangeFilter PathRange { get; set; } = PathRangeFilter.AllDrives;
        public DateRangeFilter DateRange { get; set; } = DateRangeFilter.All;
        public SizeRangeFilter SizeRange { get; set; } = SizeRangeFilter.All;
        public ImageDimensionFilter ImageSize { get; set; } = ImageDimensionFilter.All;
        public AudioDurationFilter Duration { get; set; } = AudioDurationFilter.All;
        public SearchMode Mode { get; set; } = SearchMode.FileName;
        public bool SearchNames { get; set; } = true;
        public bool SearchFolders { get; set; } = false;
        public bool SearchNotes { get; set; } = true;

        // 自定义日期范围
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // 自定义大小范围 (bytes)
        public long? SizeMin { get; set; }
        public long? SizeMax { get; set; }
    }

    /// <summary>
    /// 搜索缓存项
    /// </summary>
    public class SearchCache
    {
        public string Keyword { get; set; }
        public List<FileSystemItem> Items { get; set; } = new List<FileSystemItem>();
        public DateTime LastUpdated { get; set; }
        public FileTypeFilter Type { get; set; }
        public PathRangeFilter PathRange { get; set; }
        public string RangePath { get; set; }
        public int Offset { get; set; }
        public bool HasMore { get; set; }
    }

    /// <summary>
    /// 搜索结果
    /// </summary>
    public class SearchResult
    {
        public List<FileSystemItem> Items { get; set; } = new List<FileSystemItem>();
        public string Keyword { get; set; }
        public int Offset { get; set; }
        public bool HasMore { get; set; }
        public int PageSize { get; set; }
        public int MaxResults { get; set; }

        /// <summary>
        /// 按类型分组的搜索结果
        /// </summary>
        public Dictionary<SearchResultType, List<FileSystemItem>> GroupedItems { get; set; }
            = new Dictionary<SearchResultType, List<FileSystemItem>>();
    }

    /// <summary>
    /// 搜索结果事件参数
    /// </summary>
    public class SearchResultEventArgs : EventArgs
    {
        public SearchResult Result { get; set; }
        public string TabPath { get; set; }
    }
}


