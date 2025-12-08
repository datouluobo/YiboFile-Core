using System;
using System.Collections.Generic;

namespace OoiMRR.Services.Search
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
        Documents,  // 文档
        Folders     // 文件夹
    }

    /// <summary>
    /// 路径范围过滤器
    /// </summary>
    public enum PathRangeFilter
    {
        AllDrives,      // 全部磁盘
        CurrentDrive    // 当前磁盘
    }

    /// <summary>
    /// 搜索选项
    /// </summary>
    public class SearchOptions
    {
        public FileTypeFilter Type { get; set; } = FileTypeFilter.All;
        public PathRangeFilter PathRange { get; set; } = PathRangeFilter.AllDrives;
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

