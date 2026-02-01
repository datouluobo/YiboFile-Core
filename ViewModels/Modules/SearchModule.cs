using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using YiboFile.Models;
using YiboFile.Services;
using YiboFile.Services.Search;
using YiboFile.Services.FileNotes;
using YiboFile.Services.Tabs;
using YiboFile.Services.FullTextSearch;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 搜索模块
    /// 处理文件搜索、全文搜索及搜索结果分发
    /// </summary>
    public class SearchModule : ModuleBase
    {
        private readonly SearchService _searchService;
        private readonly SearchCacheService _searchCacheService;
        private readonly TabService _tabService;
        private readonly TabService _secondTabService;
        private readonly Func<bool> _isDualListMode;
        private readonly Func<bool> _isSecondPaneFocused;
        private bool _isProcessingSearch = false;

        public override string Name => "Search";

        public SearchModule(
            IMessageBus messageBus,
            SearchService searchService,
            SearchCacheService searchCacheService,
            TabService tabService,
            TabService secondTabService = null,
            Func<bool> isDualListMode = null,
            Func<bool> isSecondPaneFocused = null)
            : base(messageBus)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _searchCacheService = searchCacheService ?? throw new ArgumentNullException(nameof(searchCacheService));
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _secondTabService = secondTabService;
            _isDualListMode = isDualListMode ?? (() => false);
            _isSecondPaneFocused = isSecondPaneFocused ?? (() => false);
        }

        protected override void OnInitialize()
        {
            // 订阅搜索执行消息
            Subscribe<ExecuteSearchMessage>(OnExecuteSearch);
        }

        private async void OnExecuteSearch(ExecuteSearchMessage message)
        {
            if (_isProcessingSearch) return;
            _isProcessingSearch = true;

            // 立即清除预览区，防止在搜索过程中显示过时或不相关的预览
            Publish(new PreviewRequestMessage(null));

            try
            {
                if (string.IsNullOrEmpty(message.SearchText))
                {
                    _isProcessingSearch = false;
                    return;
                }

                // 确保至少有一个搜索选项
                if (!message.SearchNames && !message.SearchNotes)
                {
                    // 这里可以发布一个通知消息
                    _isProcessingSearch = false;
                    return;
                }

                var normalizedKeyword = SearchService.NormalizeKeyword(message.SearchText);

                // 检查是否为全文搜索 (content:xxx 或 content://xxx)
                var (isContentSearch, contentKeyword) = FullTextSearchService.ParseSearchQuery(normalizedKeyword);
                if (isContentSearch)
                {
                    await PerformContentSearch(contentKeyword, normalizedKeyword, message.TargetPaneId);
                }
                else
                {
                    await PerformFileSearch(normalizedKeyword, message.SearchNames, message.SearchNotes, message.TargetPaneId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SearchModule] Search failed: {ex.Message}");
            }
            finally
            {
                _isProcessingSearch = false;
            }
        }

        private async Task PerformContentSearch(string contentKeyword, string normalizedKeyword, string targetPaneId)
        {
            string statusMsg = "正在搜索文件内容...";
            Publish(new SearchResultUpdatedMessage(null, statusMsg, true, targetPaneId));

            try
            {
                string searchTabPath = normalizedKeyword.StartsWith("content://", StringComparison.OrdinalIgnoreCase)
                                     ? normalizedKeyword
                                     : $"content://{contentKeyword}";

                // 处理标签页创建/切换
                EnsureSearchTab(searchTabPath, targetPaneId);

                // 异步执行全文搜索
                var results = await Task.Run(() => FullTextSearchService.Instance.SearchContent(contentKeyword));
                if (results == null) results = new List<FileSystemItem>();

                statusMsg = results.Count == 0
                    ? "未找到包含该内容的文件"
                    : $"找到 {results.Count} 个匹配文件";

                Publish(new SearchResultUpdatedMessage(
                    results,
                    statusMsg,
                    false,
                    targetPaneId,
                    SearchTabPath: searchTabPath,
                    NormalizedKeyword: $"内容: {contentKeyword}"));
            }
            catch (Exception ex)
            {
                Publish(new SearchResultUpdatedMessage(new List<FileSystemItem>(), $"全文搜索出错: {ex.Message}", false, targetPaneId));
            }
        }

        private async Task PerformFileSearch(string normalizedKeyword, bool searchNames, bool searchNotes, string targetPaneId)
        {
            Publish(new SearchResultUpdatedMessage(null, "搜索中...", true, targetPaneId));

            // 防御性检查
            if (normalizedKeyword.Trim().StartsWith("content:", StringComparison.OrdinalIgnoreCase) ||
                normalizedKeyword.IndexOf("search://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalizedKeyword.IndexOf("content://", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Publish(new SearchResultUpdatedMessage(new List<FileSystemItem>(), "搜索被拦截：无效的搜索格式", false, targetPaneId));
                return;
            }

            try
            {
                // 获取当前路径用于搜索上下文 (TODO: 需要一种方式获取当前 VM 的路径)
                // 这里暂时假设搜索是基于全局上下文的，或者后续在 ExecuteSearchMessage 中传递
                string currentPath = "";

                var searchOptions = new SearchOptions(); // 临时创建，实际应从 VM 获取
                var searchResult = await _searchService.PerformSearchAsync(
                    keyword: normalizedKeyword,
                    searchOptions: searchOptions,
                    currentPath: currentPath,
                    searchNames: searchNames,
                    searchNotes: searchNotes,
                    getNotesFromDb: searchNotes ? (Func<string, List<string>>)(keyword => FileNotesService.SearchFilesByNotes(keyword)) : null
                );

                var results = (searchResult?.Items) ?? new List<FileSystemItem>();
                var groupedItems = searchResult?.GroupedItems;

                string searchTabPath = $"search://{normalizedKeyword}";
                EnsureSearchTab(searchTabPath, targetPaneId);

                List<FileSystemItem> finalResults = results;
                if (groupedItems != null && groupedItems.Count > 0)
                {
                    finalResults = FlattenGroupedItems(groupedItems);
                }

                string statusMsg = $"找到 {finalResults.Count} 个结果";

                Publish(new SearchResultUpdatedMessage(
                    finalResults,
                    statusMsg,
                    false,
                    targetPaneId,
                    HasMore: searchResult?.HasMore ?? false,
                    SearchTabPath: searchTabPath,
                    NormalizedKeyword: normalizedKeyword));
            }
            catch (Exception ex)
            {
                Publish(new SearchResultUpdatedMessage(new List<FileSystemItem>(), $"搜索出错: {ex.Message}", false, targetPaneId));
            }
        }

        private void EnsureSearchTab(string path, string targetPaneId)
        {
            var tabService = (targetPaneId == "Secondary" && _secondTabService != null) ? _secondTabService : _tabService;

            var existingTab = tabService.FindTabByPath(path);
            if (existingTab != null)
            {
                tabService.SwitchToTab(existingTab);
            }
            else
            {
                tabService.CreatePathTab(path, forceNewTab: true);
            }
        }

        private List<FileSystemItem> FlattenGroupedItems(Dictionary<SearchResultType, List<FileSystemItem>> groupedItems)
        {
            var flatList = new List<FileSystemItem>();

            var displayOrder = new[]
            {
                SearchResultType.Notes,
                SearchResultType.Folder,
                SearchResultType.File,
                SearchResultType.Tag,
                SearchResultType.Date,
                SearchResultType.Other
            };

            foreach (var type in displayOrder)
            {
                if (groupedItems.ContainsKey(type) && groupedItems[type].Count > 0)
                {
                    string groupName = GetGroupName(type);
                    foreach (var item in groupedItems[type])
                    {
                        item.GroupingKey = groupName;
                        flatList.Add(item);
                    }
                }
            }
            return flatList;
        }

        private string GetGroupName(SearchResultType type)
        {
            return type switch
            {
                SearchResultType.Notes => "备注匹配",
                SearchResultType.Folder => "文件夹匹配",
                SearchResultType.File => "文件匹配",
                SearchResultType.Tag => "标签匹配",
                SearchResultType.Date => "日期匹配",
                _ => "其他"
            };
        }
    }
}
