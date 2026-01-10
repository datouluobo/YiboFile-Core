using System;
using System.Collections.Generic;
using System.Linq;

namespace OoiMRR.Services.FullTextSearch
{
    /// <summary>
    /// 全文搜索服务 - 提供 content: 语法支持的搜索接口
    /// </summary>
    public class FullTextSearchService : IDisposable
    {
        private readonly FtsIndexService _ftsService;
        private readonly IndexingTaskService _indexingService;
        private bool _disposed;

        private static FullTextSearchService _instance;
        public static FullTextSearchService Instance => _instance ??= new FullTextSearchService();

        public FullTextSearchService(FtsIndexService ftsService = null)
        {
            _ftsService = ftsService ?? new FtsIndexService();
            _indexingService = new IndexingTaskService(_ftsService);
        }

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

        /// <summary>
        /// 执行内容搜索
        /// </summary>
        public List<FileSystemItem> SearchContent(string keyword, int maxResults = 100)
        {
            if (string.IsNullOrWhiteSpace(keyword))
                return new List<FileSystemItem>();

            var ftsResults = _ftsService.Search(keyword, maxResults);

            return ftsResults.Select(r => new FileSystemItem
            {
                Path = r.Path,
                Name = r.FileName,
                // 可以在 Notes 字段显示匹配片段
                Notes = r.Snippet?.Replace("<b>", "").Replace("</b>", "") ?? string.Empty
            }).ToList();
        }

        /// <summary>
        /// 获取索引服务（用于手动索引操作）
        /// </summary>
        public IndexingTaskService IndexingService => _indexingService;

        /// <summary>
        /// 获取已索引文件数量
        /// </summary>
        public int IndexedFileCount => _ftsService.GetIndexedCount();

        /// <summary>
        /// 清空索引
        /// </summary>
        public void ClearIndex() => _ftsService.ClearIndex();

        public void Dispose()
        {
            if (!_disposed)
            {
                _indexingService?.Dispose();
                _ftsService?.Dispose();
                _disposed = true;
            }
        }
    }
}
