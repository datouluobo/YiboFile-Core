using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OoiMRR.Services;

namespace OoiMRR.Services.Search
{
    /// <summary>
    /// 搜索服务
    /// 负责执行文件搜索操作
    /// </summary>
    public class SearchService
    {
        private readonly SearchFilterService _filterService;
        private readonly SearchCacheService _cacheService;
        private readonly SearchResultBuilder _resultBuilder;

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
            set => _pageSize = value > 0 ? value : 1000;
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
        public SearchService(
            SearchFilterService filterService,
            SearchCacheService cacheService,
            SearchResultBuilder resultBuilder)
        {
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _resultBuilder = resultBuilder ?? throw new ArgumentNullException(nameof(resultBuilder));
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

            var normalizedKeyword = NormalizeKeyword(keyword);
            var results = new List<FileSystemItem>();
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

                // 名称搜索（优先使用 Everything）
                if (searchNames)
                {
                    if (EverythingHelper.IsEverythingRunning())
                    {
                        try
                        {
                            await PerformEverythingSearchAsync(
                                normalizedKeyword,
                                searchOptions,
                                currentPath,
                                resultPaths,
                                progressCallback,
                                _cancellationTokenSource.Token);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Everything搜索失败: {ex.Message}，回退默认搜索");
                            PerformDefaultNameSearch(normalizedKeyword, resultPaths);
                        }
                    }
                    else
                    {
                        PerformDefaultNameSearch(normalizedKeyword, resultPaths);
                    }
                    
                    // 记录文件名搜索结果
                    foreach (var path in resultPaths)
                    {
                        nameResultPaths.Add(path);
                    }
                }

                // 备注搜索
                if (searchNotes && getNotesFromDb != null)
                {
                    Debug.WriteLine($"开始备注搜索，关键词: '{normalizedKeyword}'");
                    try
                    {
                        var notesResults = getNotesFromDb(normalizedKeyword);
                        Debug.WriteLine($"备注搜索返回结果: {notesResults?.Count ?? 0} 个");
                        
                        if (notesResults != null && notesResults.Count > 0)
                        {
                            Debug.WriteLine($"备注搜索完成，找到 {notesResults.Count} 个文件");
                            
                            foreach (var path in notesResults)
                            {
                                if (!string.IsNullOrEmpty(path))
                                {
                                    Debug.WriteLine($"备注搜索结果文件: {path}");
                                    notesResultPaths.Add(path);
                                    resultPaths.Add(path);
                                }
                            }
                            Debug.WriteLine($"备注搜索后，总结果数: {resultPaths.Count}");
                        }
                        else
                        {
                            Debug.WriteLine($"备注搜索未找到匹配结果（关键词: '{normalizedKeyword}'）");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"备注搜索失败: {ex.Message}\n{ex.StackTrace}");
                        // 备注搜索失败不影响文件名搜索，继续执行
                    }
                }
                else
                {
                    Debug.WriteLine($"备注搜索未启用: searchNotes={searchNotes}, getNotesFromDb={getNotesFromDb != null}");
                }

                // 应用类型过滤
                var filteredPaths = _filterService.ApplyTypeFilter(resultPaths, searchOptions.Type).ToList();

                // 按相关性排序
                var sortedPaths = _resultBuilder.SortByRelevance(filteredPaths, normalizedKeyword).ToList();

                // 构建结果项
                results = _resultBuilder.BuildItemsFromPaths(sortedPaths);
                
                // 构建分组结果
                var groupedItems = new Dictionary<SearchResultType, List<FileSystemItem>>();
                
                // 分离备注匹配结果
                var notesItems = new List<FileSystemItem>();
                var folderItems = new List<FileSystemItem>();
                var fileItems = new List<FileSystemItem>();
                
                foreach (var item in results)
                {
                    // 检查是否来自备注搜索（优先级最高）
                    if (notesResultPaths.Contains(item.Path, StringComparer.OrdinalIgnoreCase))
                    {
                        item.SearchResultType = SearchResultType.Notes;
                        item.IsFromNotesSearch = true;
                        notesItems.Add(item);
                    }
                    // 检查是否为文件夹（且不是备注匹配）
                    else if (item.IsDirectory)
                    {
                        item.SearchResultType = SearchResultType.Folder;
                        item.IsFromNameSearch = true;
                        folderItems.Add(item);
                    }
                    // 其他为文件（且不是备注匹配）
                    else
                    {
                        item.SearchResultType = SearchResultType.File;
                        item.IsFromNameSearch = nameResultPaths.Contains(item.Path, StringComparer.OrdinalIgnoreCase);
                        fileItems.Add(item);
                    }
                }
                
                // 添加到分组字典
                if (notesItems.Count > 0)
                    groupedItems[SearchResultType.Notes] = notesItems;
                if (folderItems.Count > 0)
                    groupedItems[SearchResultType.Folder] = folderItems;
                if (fileItems.Count > 0)
                    groupedItems[SearchResultType.File] = fileItems;

                Debug.WriteLine($"搜索完成，共找到 {results.Count} 个结果");
                Debug.WriteLine($"分组结果: 备注={notesItems.Count}, 文件夹={folderItems.Count}, 文件={fileItems.Count}");

                return new SearchResult
                {
                    Items = results,
                    Keyword = normalizedKeyword,
                    Offset = Math.Min(_pageSize, results.Count),
                    HasMore = results.Count >= _pageSize && results.Count < _maxResults,
                    PageSize = _pageSize,
                    MaxResults = _maxResults,
                    GroupedItems = groupedItems
                };
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
            if (string.IsNullOrEmpty(keyword) || !EverythingHelper.IsEverythingRunning())
            {
                return new SearchResult { Keyword = keyword };
            }

            try
            {
                var rangePath = _filterService.GetRangePath(searchOptions.PathRange, currentPath);
                var page = EverythingHelper.SearchFilesPaged(keyword, offset, _pageSize, rangePath);
                
                if (page == null || page.Count == 0)
                {
                    return new SearchResult
                    {
                        Keyword = keyword,
                        Offset = offset,
                        HasMore = false,
                        PageSize = _pageSize
                    };
                }

                var filtered = _filterService.ApplyTypeFilter(page, searchOptions.Type).ToList();
                var newItems = _resultBuilder.BuildItemsFromPaths(filtered);

                var newOffset = offset + page.Count;
                var hasMore = page.Count == _pageSize && newOffset < _maxResults;

                // 更新缓存（合并结果）
                var cacheKey = $"search://{keyword}";
                var existingCache = _cacheService.GetCache(cacheKey);
                if (existingCache != null)
                {
                    var allItems = new List<FileSystemItem>(existingCache.Items);
                    allItems.AddRange(newItems);
                    _cacheService.UpdateCache(cacheKey, allItems, newOffset, hasMore, keyword, rangePath, searchOptions.Type, searchOptions.PathRange);
                }

                return new SearchResult
                {
                    Items = newItems,
                    Keyword = keyword,
                    Offset = newOffset,
                    HasMore = hasMore,
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
        public SearchResult RefreshSearch(
            string keyword,
            SearchOptions searchOptions,
            string currentPath,
            CancellationToken cancellationToken = default)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            if (string.IsNullOrEmpty(keyword) || !EverythingHelper.IsEverythingRunning())
            {
                return new SearchResult { Keyword = keyword };
            }

            try
            {
                var rangePath = _filterService.GetRangePath(searchOptions.PathRange, currentPath);
                var firstPage = EverythingHelper.SearchFilesPaged(keyword, 0, _pageSize, rangePath);
                var filtered = _filterService.ApplyTypeFilter(firstPage, searchOptions.Type).ToList();
                var items = _resultBuilder.BuildItemsFromPaths(filtered);

                var offset = firstPage.Count;
                var hasMore = firstPage.Count == _pageSize && offset < _maxResults;

                // 更新缓存
                var cacheKey = $"search://{keyword}";
                _cacheService.UpdateCache(cacheKey, items, offset, hasMore, keyword, rangePath, searchOptions.Type, searchOptions.PathRange);

                return new SearchResult
                {
                    Items = items,
                    Keyword = keyword,
                    Offset = offset,
                    HasMore = hasMore,
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

        #region 私有方法

        /// <summary>
        /// 执行 Everything 搜索（异步分页加载）
        /// </summary>
        private async Task PerformEverythingSearchAsync(
            string keyword,
            SearchOptions searchOptions,
            string currentPath,
            HashSet<string> resultPaths,
            Action<SearchResult> progressCallback,
            CancellationToken cancellationToken)
        {
            var rangePath = _filterService.GetRangePath(searchOptions.PathRange, currentPath);
            
            // 加载第一页
            var firstPage = EverythingHelper.SearchFilesPaged(keyword, 0, _pageSize, rangePath);
            if (firstPage == null || firstPage.Count == 0)
                return;

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
        }

        /// <summary>
        /// 执行默认名称搜索（回退方案）
        /// </summary>
        private void PerformDefaultNameSearch(string keyword, HashSet<string> resultPaths)
        {
            try
            {
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

                foreach (var drive in drives)
                {
                    try
                    {
                        var root = drive.RootDirectory.FullName;
                        
                        // 搜索文件
                        var files = Directory.GetFiles(root, "*" + keyword + "*", SearchOption.TopDirectoryOnly)
                            .Take(1000);
                        foreach (var file in files)
                        {
                            resultPaths.Add(file);
                        }

                        // 搜索文件夹
                        var dirs = Directory.GetDirectories(root, "*" + keyword + "*", SearchOption.TopDirectoryOnly)
                            .Take(1000);
                        foreach (var dir in dirs)
                        {
                            resultPaths.Add(dir);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"搜索驱动器 {drive.Name} 失败: {ex.Message}");
                    }
                }

                Debug.WriteLine($"默认搜索完成，聚合结果数: {resultPaths.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"默认搜索失败: {ex.Message}");
                throw;
            }
        }

        #endregion
    }
}

