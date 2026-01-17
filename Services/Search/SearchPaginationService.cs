using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using YiboFile.Services;

namespace YiboFile.Services.Search
{
    /// <summary>
    /// 搜索分页服务
    /// 负责处理搜索结果的分页操作（加载更多、刷新）
    /// </summary>
    public class SearchPaginationService
    {
        private readonly SearchFilterService _filterService;
        private readonly SearchResultBuilder _resultBuilder;
        private readonly SearchCacheService _cacheService;
        private readonly EverythingSearchExecutor _everythingExecutor;
        private readonly int _pageSize;
        private readonly int _maxResults;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filterService">过滤器服务</param>
        /// <param name="resultBuilder">结果构建器</param>
        /// <param name="cacheService">缓存服务</param>
        /// <param name="everythingExecutor">Everything 搜索执行器</param>
        /// <param name="pageSize">页面大小</param>
        /// <param name="maxResults">最大结果数</param>
        public SearchPaginationService(
            SearchFilterService filterService,
            SearchResultBuilder resultBuilder,
            SearchCacheService cacheService,
            EverythingSearchExecutor everythingExecutor,
            int pageSize = 1000,
            int maxResults = 5000)
        {
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
            _resultBuilder = resultBuilder ?? throw new ArgumentNullException(nameof(resultBuilder));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _everythingExecutor = everythingExecutor ?? throw new ArgumentNullException(nameof(everythingExecutor));
            _pageSize = pageSize > 0 ? pageSize : 1000;
            _maxResults = maxResults > 0 ? maxResults : 5000;
        }

        /// <summary>
        /// 加载更多搜索结果（分页）
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="offset">当前偏移量</param>
        /// <param name="searchOptions">搜索选项</param>
        /// <param name="currentPath">当前路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>新的搜索结果页</returns>
        public SearchResult LoadMore(
            string keyword,
            int offset,
            SearchOptions searchOptions,
            string currentPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(keyword) || !EverythingHelper.IsEverythingRunning())
            {
                return new SearchResult { Keyword = keyword };
            }

            try
            {
                var page = _everythingExecutor.ExecutePage(keyword, offset, searchOptions, currentPath);

                if (page.Paths.Count == 0)
                {
                    return new SearchResult
                    {
                        Keyword = keyword,
                        Offset = offset,
                        HasMore = false,
                        PageSize = _pageSize
                    };
                }

                var newItems = _resultBuilder.BuildItemsFromPaths(page.Paths);

                // 更新缓存（合并结果）
                var cacheKey = $"search://{keyword}";
                var existingCache = _cacheService.GetCache(cacheKey);
                if (existingCache != null)
                {
                    var allItems = new List<FileSystemItem>(existingCache.Items);
                    allItems.AddRange(newItems);
                    var rangePath = _filterService.GetRangePath(searchOptions.PathRange, currentPath);
                    _cacheService.UpdateCache(
                        cacheKey,
                        allItems,
                        page.Offset,
                        page.HasMore,
                        keyword,
                        rangePath,
                        searchOptions.Type,
                        searchOptions.PathRange);
                }

                return new SearchResult
                {
                    Items = newItems,
                    Keyword = keyword,
                    Offset = page.Offset,
                    HasMore = page.HasMore,
                    PageSize = _pageSize,
                    MaxResults = _maxResults
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"加载更多搜索结果失败: {ex.Message}");
                return new SearchResult { Keyword = keyword };
            }
        }

        /// <summary>
        /// 刷新搜索（重新执行第一页）
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="searchOptions">搜索选项</param>
        /// <param name="currentPath">当前路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>搜索结果</returns>
        public SearchResult Refresh(
            string keyword,
            SearchOptions searchOptions,
            string currentPath,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(keyword) || !EverythingHelper.IsEverythingRunning())
            {
                return new SearchResult { Keyword = keyword };
            }

            try
            {
                var page = _everythingExecutor.ExecutePage(keyword, 0, searchOptions, currentPath);
                var items = _resultBuilder.BuildItemsFromPaths(page.Paths);

                // 更新缓存
                var cacheKey = $"search://{keyword}";
                var rangePath = _filterService.GetRangePath(searchOptions.PathRange, currentPath);
                _cacheService.UpdateCache(
                    cacheKey,
                    items,
                    page.Offset,
                    page.HasMore,
                    keyword,
                    rangePath,
                    searchOptions.Type,
                    searchOptions.PathRange);

                return new SearchResult
                {
                    Items = items,
                    Keyword = keyword,
                    Offset = page.Offset,
                    HasMore = page.HasMore,
                    PageSize = _pageSize,
                    MaxResults = _maxResults
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"刷新搜索失败: {ex.Message}");
                return new SearchResult { Keyword = keyword };
            }
        }
    }
}















