using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YiboFile.Services;

namespace YiboFile.Services.Search
{
    /// <summary>
    /// Everything 搜索执行器
    /// 负责执行 Everything 搜索的分页逻辑
    /// </summary>
    public class EverythingSearchExecutor
    {
        private readonly SearchFilterService _filterService;
        private readonly SearchResultBuilder _resultBuilder;
        private readonly int _pageSize;
        private readonly int _maxResults;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filterService">过滤器服务</param>
        /// <param name="resultBuilder">结果构建器</param>
        /// <param name="pageSize">页面大小</param>
        /// <param name="maxResults">最大结果数</param>
        public EverythingSearchExecutor(
            SearchFilterService filterService,
            SearchResultBuilder resultBuilder,
            int pageSize = 1000,
            int maxResults = 5000)
        {
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
            _resultBuilder = resultBuilder ?? throw new ArgumentNullException(nameof(resultBuilder));
            _pageSize = pageSize > 0 ? pageSize : 1000;
            _maxResults = maxResults > 0 ? maxResults : 5000;
        }

        /// <summary>
        /// 执行 Everything 搜索（异步分页加载）
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="searchOptions">搜索选项</param>
        /// <param name="currentPath">当前路径</param>
        /// <param name="resultPaths">结果路径集合（用于去重）</param>
        /// <param name="progressCallback">进度回调（每页加载完成后调用）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>搜索到的路径集合</returns>
        public async Task<HashSet<string>> ExecuteAsync(
            string keyword,
            SearchOptions searchOptions,
            string currentPath,
            HashSet<string> resultPaths,
            Action<SearchResult> progressCallback,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(keyword) || resultPaths == null)
            {
                return resultPaths ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var rangePath = _filterService.GetRangePath(searchOptions.PathRange, currentPath);

            // 加载第一页
            var firstPage = EverythingHelper.SearchFilesPaged(keyword, 0, _pageSize, rangePath);
            if (firstPage == null || firstPage.Count == 0)
            {
                return resultPaths;
            }

            var filteredFirst = _filterService.ApplyTypeFilter(firstPage, searchOptions.Type);
            foreach (var path in filteredFirst)
            {
                resultPaths.Add(path);
            }

            // 通知第一页完成
            var firstPageItems = _resultBuilder.BuildItemsFromPaths(filteredFirst);
            progressCallback?.Invoke(new SearchResult
            {
                Items = firstPageItems,
                Keyword = keyword,
                Offset = firstPage.Count,
                HasMore = firstPage.Count == _pageSize && firstPage.Count < _maxResults,
                PageSize = _pageSize,
                MaxResults = _maxResults
            });

            // 异步加载后续页
            await Task.Run(() =>
            {
                int offset = firstPage.Count;
                while (!cancellationToken.IsCancellationRequested && offset < _maxResults)
                {
                    var page = EverythingHelper.SearchFilesPaged(keyword, offset, _pageSize, rangePath);
                    if (page == null || page.Count == 0)
                        break;

                    var filtered = _filterService.ApplyTypeFilter(page, searchOptions.Type);
                    var newPaths = filtered.Where(p => resultPaths.Add(p)).ToList();

                    if (newPaths.Count > 0)
                    {
                        var newItems = _resultBuilder.BuildItemsFromPaths(newPaths);
                        progressCallback?.Invoke(new SearchResult
                        {
                            Items = newItems,
                            Keyword = keyword,
                            Offset = offset + page.Count,
                            HasMore = page.Count == _pageSize && (offset + page.Count) < _maxResults,
                            PageSize = _pageSize,
                            MaxResults = _maxResults
                        });
                    }

                    offset += page.Count;
                }
            }, cancellationToken);

            return resultPaths;
        }

        /// <summary>
        /// 执行单页搜索（用于分页加载）
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="offset">偏移量</param>
        /// <param name="searchOptions">搜索选项</param>
        /// <param name="currentPath">当前路径</param>
        /// <returns>搜索结果页</returns>
        public SearchResultPage ExecutePage(
            string keyword,
            int offset,
            SearchOptions searchOptions,
            string currentPath)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new SearchResultPage
                {
                    Paths = new List<string>(),
                    HasMore = false
                };
            }

            var rangePath = _filterService.GetRangePath(searchOptions.PathRange, currentPath);
            var page = EverythingHelper.SearchFilesPaged(keyword, offset, _pageSize, rangePath);

            if (page == null || page.Count == 0)
            {
                return new SearchResultPage
                {
                    Paths = new List<string>(),
                    HasMore = false
                };
            }

            var filtered = _filterService.ApplyTypeFilter(page, searchOptions.Type).ToList();
            var newOffset = offset + page.Count;
            var hasMore = page.Count == _pageSize && newOffset < _maxResults;

            return new SearchResultPage
            {
                Paths = filtered,
                Offset = newOffset,
                HasMore = hasMore
            };
        }
    }

    /// <summary>
    /// 搜索结果页
    /// </summary>
    public class SearchResultPage
    {
        public List<string> Paths { get; set; } = new List<string>();
        public int Offset { get; set; }
        public bool HasMore { get; set; }
    }
}















