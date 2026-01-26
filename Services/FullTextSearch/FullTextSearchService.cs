using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YiboFile.Services.Config;

namespace YiboFile.Services.FullTextSearch
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
            if (ftsService == null)
            {
                var config = ConfigurationService.Instance.GetSnapshot();
                var dbPath = config.FullTextIndexDbPath;
                if (string.IsNullOrWhiteSpace(dbPath)) dbPath = null;
                _ftsService = new FtsIndexService(dbPath);
            }
            else
            {
                _ftsService = ftsService;
            }

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
        /// 获取索引数据库路径
        /// </summary>
        public string IndexDbPath => _ftsService.IndexDbPath;

        /// <summary>
        /// 清空索引
        /// </summary>
        public void ClearIndex() => _ftsService.ClearIndex();

        /// <summary>
        /// 启动后台自动索引（扫描配置的路径）
        /// </summary>
        public void StartBackgroundIndexing()
        {
            var config = ConfigurationService.Instance.GetSnapshot();
            if (!config.IsEnableFullTextSearch) return;

            // 如果正在运行，则跳过（简单防重入）
            if (_indexingService.IsRunning) return;

            Task.Run(async () =>
            {
                try
                {
                    IEnumerable<string> scanPaths;

                    if (config.FullTextIndexPaths != null && config.FullTextIndexPaths.Count > 0)
                    {
                        scanPaths = config.FullTextIndexPaths.ToList(); // 复制列表防止迭代修改
                    }
                    else
                    {
                        // 默认扫描所有库
                        var libraries = YiboFile.DatabaseManager.GetAllLibraries();
                        scanPaths = libraries?.SelectMany(l => l.Paths ?? Enumerable.Empty<string>()) ?? Enumerable.Empty<string>();
                    }

                    foreach (var path in scanPaths)
                    {
                        if (System.IO.Directory.Exists(path))
                        {
                            // 递归索引
                            await _indexingService.StartIndexingAsync(path, recursive: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FullTextSearchService] Auto indexing error: {ex.Message}");
                }
            });
        }

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

