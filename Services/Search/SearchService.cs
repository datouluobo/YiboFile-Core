using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OoiMRR.Services;

namespace OoiMRR.Services.Search
{
    /// <summary>
    /// 搜索服务（编排器）
    /// 负责协调各个搜索执行器，组合搜索结果
    /// </summary>
    public class SearchService
    {
        private readonly SearchFilterService _filterService;
        private readonly SearchCacheService _cacheService;
        private readonly SearchResultBuilder _resultBuilder;
        private readonly EverythingSearchExecutor _everythingExecutor;
        private readonly NotesSearchExecutor _notesExecutor;
        private readonly SearchPaginationService _paginationService;

        // 搜索配置
        private int _pageSize = 1000;
        private int _maxResults = 5000;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 页面大小（默认1000）
        /// </summary>
        public int PageSize
        {
            get => _pageSize;
            set
            {
                _pageSize = value > 0 ? value : 1000;
                // 更新执行器的页面大小
                if (_everythingExecutor != null)
                {
                    // 注意：EverythingSearchExecutor 在构造时接收 pageSize，这里需要重新创建
                    // 或者可以添加一个 UpdatePageSize 方法，但为了简化，暂时保持现状
                }
            }
        }

        /// <summary>
        /// 最大结果数（默认5000）
        /// </summary>
        public int MaxResults
        {
            get => _maxResults;
            set => _maxResults = value > 0 ? value : 5000;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="filterService">过滤器服务</param>
        /// <param name="cacheService">缓存服务</param>
        /// <param name="resultBuilder">结果构建器</param>
        /// <param name="pageSize">页面大小（默认1000）</param>
        /// <param name="maxResults">最大结果数（默认5000）</param>
        public SearchService(
            SearchFilterService filterService,
            SearchCacheService cacheService,
            SearchResultBuilder resultBuilder,
            int pageSize = 1000,
            int maxResults = 5000)
        {
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _resultBuilder = resultBuilder ?? throw new ArgumentNullException(nameof(resultBuilder));
            _pageSize = pageSize > 0 ? pageSize : 1000;
            _maxResults = maxResults > 0 ? maxResults : 5000;

            // 初始化各个执行器
            _everythingExecutor = new EverythingSearchExecutor(
                _filterService,
                _resultBuilder,
                _pageSize,
                _maxResults);
            _notesExecutor = new NotesSearchExecutor();
            _paginationService = new SearchPaginationService(
                _filterService,
                _resultBuilder,
                _cacheService,
                _everythingExecutor,
                _pageSize,
                _maxResults);
        }

        /// <summary>
        /// 规范化搜索关键词（去除"搜索:"前缀）
        /// </summary>
        /// <param name="searchText">原始搜索文本</param>
        /// <returns>规范化后的关键词</returns>
        public static string NormalizeKeyword(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return searchText;

            var normalized = searchText.Trim();
            while (normalized.StartsWith("搜索:"))
            {
                normalized = normalized.Substring("搜索:".Length).Trim();
            }
            return normalized;
        }

        /// <summary>
        /// 执行搜索
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="searchOptions">搜索选项</param>
        /// <param name="currentPath">当前路径（用于路径范围过滤）</param>
        /// <param name="searchNames">是否搜索文件名</param>
        /// <param name="searchNotes">是否搜索备注</param>
        /// <param name="getNotesFromDb">从数据库获取备注搜索结果的函数</param>
        /// <param name="progressCallback">进度回调（每页加载完成后调用）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>搜索结果</returns>
        public async Task<SearchResult> PerformSearchAsync(
            string keyword,
            SearchOptions searchOptions,
            string currentPath,
            bool searchNames = true,
            bool searchNotes = false,
            Func<string, List<string>> getNotesFromDb = null,
            Action<SearchResult> progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                return new SearchResult { Keyword = keyword };
            }

            // 强制依赖 Everything，未运行则不回退到系统枚举，避免卡顿
            var everythingReady = await EverythingHelper.InitializeAsync();
            if (!everythingReady || !EverythingHelper.IsEverythingRunning())
            {
                return new SearchResult { Keyword = keyword };
            }

            var normalizedKeyword = NormalizeKeyword(keyword);
            var resultPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            // 分别收集不同类型的搜索结果
            var notesResultPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nameResultPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 取消之前的搜索
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            try
            {
                // 确保至少有一个搜索选项
                if (!searchNames && !searchNotes)
                {
                    throw new ArgumentException("请至少选择一个搜索选项");
                }

                // 名称搜索（强制使用 Everything）
                if (searchNames)
                {
                    await _everythingExecutor.ExecuteAsync(
                        normalizedKeyword,
                        searchOptions,
                        currentPath,
                        resultPaths,
                        progressCallback,
                        _cancellationTokenSource.Token);
                    
                    // 记录文件名搜索结果
                    foreach (var path in resultPaths)
                    {
                        nameResultPaths.Add(path);
                    }
                }

                // 备注搜索
                if (searchNotes && getNotesFromDb != null)
                {
                    notesResultPaths = _notesExecutor.Execute(
                        normalizedKeyword,
                        getNotesFromDb,
                        resultPaths);
                }

                // 应用类型过滤
                var filteredPaths = _filterService.ApplyTypeFilter(resultPaths, searchOptions.Type).ToList();

                // 按相关性排序
                var sortedPaths = _resultBuilder.SortByRelevance(filteredPaths, normalizedKeyword).ToList();

                // 构建结果项
                var results = _resultBuilder.BuildItemsFromPaths(sortedPaths);

                // 限制展示：备注与文件夹全部保留，文件仅取前100条
                const int maxFiles = 100;
                var limited = new List<FileSystemItem>();
                var noteItemsLimited = results.Where(r => r.SearchResultType == SearchResultType.Notes).ToList();
                var folderItemsLimited = results.Where(r => r.IsDirectory).ToList();
                var fileItemsLimited = results
                    .Where(r => !r.IsDirectory && (r.SearchResultType == null || r.SearchResultType == SearchResultType.File))
                    .Take(maxFiles)
                    .ToList();

                limited.AddRange(noteItemsLimited);
                limited.AddRange(folderItemsLimited);
                limited.AddRange(fileItemsLimited);
                results = limited;
                
                // 构建分组结果
                var groupedItems = SearchResultGrouper.BuildGroupedResults(
                    results, 
                    notesResultPaths, 
                    nameResultPaths);

                Debug.WriteLine($"搜索完成，共找到 {results.Count} 个结果");

                var searchResult = new SearchResult
                {
                    Items = results,
                    Keyword = normalizedKeyword,
                    Offset = results.Count,
                    HasMore = false, // 已截断文件数量，不再分页
                    PageSize = _pageSize,
                    MaxResults = _maxResults,
                    GroupedItems = groupedItems
                };

                // 将完整搜索结果写入缓存，避免重复触发搜索导致卡顿
                try
                {
                    var cacheKey = $"search://{normalizedKeyword}";
                    var rangePath = _filterService.GetRangePath(searchOptions.PathRange, currentPath);
                    _cacheService.UpdateCache(
                        cacheKey,
                        searchResult.Items?.ToList() ?? new List<FileSystemItem>(),
                        searchResult.Offset,
                        searchResult.HasMore,
                        normalizedKeyword,
                        rangePath,
                        searchOptions.Type,
                        searchOptions.PathRange);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"搜索结果写入缓存失败: {ex.Message}");
                }

                return searchResult;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("搜索已取消");
                return new SearchResult { Keyword = normalizedKeyword };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"搜索时发生错误: {ex.Message}");
                throw;
            }
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
            return _paginationService.LoadMore(keyword, offset, searchOptions, currentPath, cancellationToken);
        }

        /// <summary>
        /// 刷新搜索（重新执行第一页）
        /// </summary>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="searchOptions">搜索选项</param>
        /// <param name="currentPath">当前路径</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>搜索结果</returns>
        public SearchResult RefreshSearch(
            string keyword,
            SearchOptions searchOptions,
            string currentPath,
            CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            return _paginationService.Refresh(keyword, searchOptions, currentPath, cancellationToken);
        }

    }
}

