using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YiboFile.Models;

namespace YiboFile.Services.Features
{
    /// <summary>
    /// 全文搜索进度事件参数
    /// </summary>
    public class IndexingProgressEventArgs : EventArgs
    {
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public int IndexedFiles { get; set; }
        public string CurrentFile { get; set; }
        public bool IsCompleted { get; set; }
    }

    /// <summary>
    /// 全文搜索服务接口
    /// </summary>
    public interface IFullTextSearchService
    {
        bool IsRunning { get; }
        int IndexedFileCount { get; }
        string IndexDbPath { get; }

        event EventHandler<IndexingProgressEventArgs> ProgressChanged;

        void StartBackgroundIndexing();
        Task StartIndexingAsync(string directoryPath, bool recursive = true);
        void StopIndexing();
        void ClearIndex();
        List<FileSystemItem> SearchContent(string keyword, int maxResults = 100);
    }

    /// <summary>
    /// 全文搜索工具类
    /// </summary>
    public static class FullTextSearchHelper
    {
        /// <summary>
        /// 解析搜索关键词，检测是否为内容搜索
        /// </summary>
        public static (bool isContentSearch, string keyword) ParseSearchQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return (false, query);

            query = query.Trim();

            // 检测 content:// 协议头 (新 - 显式全文搜索)
            if (query.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                var keyword = query.Substring("content://".Length).Trim();
                // 移除可能的引号
                if (keyword.StartsWith("\"") && keyword.EndsWith("\"") && keyword.Length > 1)
                {
                    keyword = keyword.Substring(1, keyword.Length - 2);
                }
                return (true, keyword);
            }

            // 检测 content: 前缀
            if (query.StartsWith("content:", StringComparison.OrdinalIgnoreCase))
            {
                var keyword = query.Substring("content:".Length).Trim();
                // 移除可能的引号
                if (keyword.StartsWith("\"") && keyword.EndsWith("\"") && keyword.Length > 1)
                {
                    keyword = keyword.Substring(1, keyword.Length - 2);
                }
                return (true, keyword);
            }

            return (false, query);
        }
    }
}
