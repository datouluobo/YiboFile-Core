using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using YiboFile.Services.Search;
using YiboFile.Services.FileNotes;
using YiboFile.Services.Tabs;
using YiboFile.Services.FullTextSearch;

namespace YiboFile
{
    public partial class MainWindow
    {
        private bool _isProcessingSearch = false;

        internal async void PerformSearch(string searchText, bool searchNames, bool searchNotes)
        {
            if (_isProcessingSearch) return;
            _isProcessingSearch = true;

            try
            {
                if (string.IsNullOrEmpty(searchText))
                {
                    return;
                }

                // 确保至少有一个搜索选项
                if (!searchNames && !searchNotes)
                {
                    DialogService.Info("请至少选择一个搜索选项", owner: this);
                    return;
                }

                var normalizedKeyword = SearchService.NormalizeKeyword(searchText);

                // 检查是否为全文搜索 (content:xxx 或 content://xxx)
                var (isContentSearch, contentKeyword) = FullTextSearchService.ParseSearchQuery(normalizedKeyword);
                if (isContentSearch)
                {
                    Debug.WriteLine($"[PerformSearch] Detected content search: {contentKeyword}");
                    FileBrowser?.SetSearchStatus(true, "正在搜索文件内容..."); // 初始状态

                    try
                    {
                        // 准备搜索结果 Tab 的路径
                        // 若 normalizedKeyword 本身是 content://... 则直接用，否则构造 content://...
                        string searchTabPath = normalizedKeyword.StartsWith("content://", StringComparison.OrdinalIgnoreCase)
                                             ? normalizedKeyword
                                             : $"content://{contentKeyword}";

                        // 检查标签页是否存在
                        if (_tabService != null)
                        {
                            var existingTab = _tabService.FindTabByPath(searchTabPath);
                            if (existingTab != null)
                            {
                                _tabService.SwitchToTab(existingTab);
                                // 切换 Tab 后，应该已经有数据了（如果是之前加载过的）。
                                // 但如果是空的或者需要刷新呢？暂时假设切换即可。
                                return;
                            }
                        }

                        // 准备 UI
                        if (FileBrowser != null)
                        {
                            FileBrowser.TabsVisible = true;
                        }

                        // 创建新 Tab
                        // 注意：CreatePathTab 会触发 PathChanged，但被我们的 Handler 拦截了 (因为是 content://)
                        // 所以我们需要手动执行搜索并填充数据
                        _tabService?.CreatePathTab(searchTabPath, forceNewTab: true);

                        // 显示搜索状态 (CreatePathTab 可能会重置它，所以再次设置)
                        FileBrowser?.SetSearchStatus(true, "正在搜索内容...");

                        // 异步执行全文搜索
                        var results = await Task.Run(() => FullTextSearchService.Instance.SearchContent(contentKeyword));

                        // 确保 results 不为 null
                        if (results == null) results = new List<FileSystemItem>();

                        string statusMsg = results.Count == 0
                            ? "未找到包含该内容的文件"
                            : $"找到 {results.Count} 个匹配文件";

                        Debug.WriteLine($"[PerformSearch] Content search result: {statusMsg}");

                        // 更新 UI
                        if (FileBrowser != null)
                        {
                            FileBrowser.FilesItemsSource = results;

                            if (results.Count == 0)
                            {
                                // 结果为空：显示列表内空状态，隐藏顶栏状态
                                FileBrowser.ShowEmptyState("未找到包含该内容的文件");
                                FileBrowser.SetSearchStatus(false);
                            }
                            else
                            {
                                // 结果不为空：隐藏空状态，显示顶栏结果数量
                                FileBrowser.HideEmptyState();
                                FileBrowser.SetSearchStatus(true, statusMsg);
                            }

                            // 确保相关 UI 状态正确
                            FileBrowser.SetSearchBreadcrumb($"内容: {contentKeyword}");
                            FileBrowser.AddressText = searchTabPath;
                            FileBrowser.NavUpEnabled = false;
                            FileBrowser.LoadMoreVisible = false;
                        }

                        return; // 全文搜索处理完毕，直接返回
                    }
                    catch (Exception ex)
                    {
                        DialogService.Error($"全文搜索出错: {ex.Message}", owner: this);
                        return;
                    }
                }

                // --- 2. 普通文件搜索 (Everything) ---

                // 如果执行到这里，说明不是全文搜索，或者是无法识别的格式
                // 显示搜索状态
                FileBrowser?.SetSearchStatus(true, "搜索中...");

                // 防御性检查：防止 content: 或者是搜素协议头漏到 Everything 导致卡死 (Everything 对 content: 的处理极慢)
                // 如果 normalizedKeyword 仍然包含 search:// (说明 NormalizeKeyword 失败?) 或者以 content: 开头 (但未被识别为 ContentSearch?)
                // 则直接拦截
                if (normalizedKeyword.Trim().StartsWith("content:", StringComparison.OrdinalIgnoreCase) ||
                    normalizedKeyword.IndexOf("search://", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalizedKeyword.IndexOf("content://", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.WriteLine($"[PerformSearch] Blocked potential content/protocol search from reaching Everything executor: {normalizedKeyword}");
                    FileBrowser?.SetSearchStatus(true, "搜索被拦截：无效的搜索格式");
                    return;
                }

                try
                {
                    // 执行搜索
                    var searchResult = await _searchService.PerformSearchAsync(
                        keyword: normalizedKeyword,
                        searchOptions: _searchOptions,
                        currentPath: _currentPath,
                        searchNames: searchNames,
                        searchNotes: searchNotes,
                        getNotesFromDb: searchNotes ? (Func<string, List<string>>)(keyword => FileNotesService.SearchFilesByNotes(keyword)) : null
                    );

                    var results = (searchResult?.Items) ?? new List<FileSystemItem>();
                    var groupedItems = searchResult?.GroupedItems;

                    if (results.Count == 0)
                    {
                        Debug.WriteLine("搜索结果为空");
                        // Can show a toast or status, but we should proceed to open the tab to show "0 results"
                    }

                    Debug.WriteLine($"搜索完成，共找到 {results.Count} 个结果");
                    FileBrowser?.SetSearchStatus(true, $"找到 {results.Count} 个结果");

                    // 在列2打开新标签页显示搜索结果（即使结果为空也要创建标签页）
                    if (FileBrowser != null) FileBrowser.TabsVisible = true;
                    string searchTabTitle = $"搜索: {normalizedKeyword}";
                    string searchTabPath = $"search://{normalizedKeyword}"; // 使用规范化关键词

                    // 检查是否已存在相同的搜索标签页
                    var existingTab = _tabService.FindTabByPath(searchTabPath);
                    if (existingTab != null)
                    {
                        // 切换到现有标签页
                        SwitchToTab(existingTab);
                        // 更新搜索结果（即使切换到现有标签页，也要刷新结果）
                        _currentFiles = results;
                        if (FileBrowser != null)
                        {
                            // 使用分组显示
                            if (groupedItems != null && groupedItems.Count > 0)
                            {
                                FileBrowser.SetGroupedSearchResults(groupedItems);
                            }
                            else
                            {
                                FileBrowser.FilesItemsSource = results;
                                if (results.Count == 0) FileBrowser.ShowEmptyState("未找到匹配项");
                                else FileBrowser.HideEmptyState();
                            }
                            // 确保地址栏和面包屑显示规范化关键词
                            FileBrowser.SetSearchBreadcrumb(normalizedKeyword);
                            FileBrowser.AddressText = normalizedKeyword;
                            FileBrowser.NavUpEnabled = false;
                            FileBrowser.LoadMoreVisible = searchResult.HasMore;
                        }
                        Debug.WriteLine($"切换到现有搜索标签页: {searchTabTitle}");
                    }
                    else
                    {

                        // 显示搜索结果
                        _currentFiles = results;
                        Debug.WriteLine($"[PerformSearch] 设置 _currentFiles，数量: {_currentFiles.Count}");

                        if (FileBrowser != null)
                        {
                            // 使用分组显示
                            if (groupedItems != null && groupedItems.Count > 0)
                            {
                                Debug.WriteLine($"[PerformSearch] 设置分组搜索结果，分组数: {groupedItems.Count}");
                                FileBrowser.SetGroupedSearchResults(groupedItems);
                            }
                            else
                            {
                                Debug.WriteLine($"[PerformSearch] 设置 FilesItemsSource，结果数: {results.Count}");
                                FileBrowser.FilesItemsSource = results;
                                if (results.Count == 0) FileBrowser.ShowEmptyState("未找到匹配项");
                                else FileBrowser.HideEmptyState();
                            }
                            FileBrowser.SetSearchBreadcrumb(normalizedKeyword);
                            FileBrowser.AddressText = normalizedKeyword;
                            FileBrowser.NavUpEnabled = false;
                            FileBrowser.LoadMoreVisible = searchResult.HasMore;
                            Debug.WriteLine($"[PerformSearch] FileBrowser.FilesItemsSource 当前数量: {(FileBrowser.FilesItemsSource as System.Collections.IList)?.Count ?? 0}");
                        }

                        // 现在创建新标签页（文件列表已准备就绪）
                        Debug.WriteLine($"[PerformSearch] 准备创建标签页: {searchTabPath}");
                        _tabService?.CreatePathTab(searchTabPath, forceNewTab: true);
                        Debug.WriteLine($"[PerformSearch] 标签页创建完成");

                        // 立即检查文件列表状态
                        if (FileBrowser != null)
                        {
                            var currentCount = (FileBrowser.FilesItemsSource as System.Collections.IList)?.Count ?? 0;
                            Debug.WriteLine($"[PerformSearch] 创建后 FileBrowser.FilesItemsSource 数量: {currentCount}");

                            if (currentCount == 0)
                            {
                                Debug.WriteLine($"[PerformSearch] ⚠️ 警告！文件列表被清空了！正在恢复...");
                                // 恢复文件列表
                                if (groupedItems != null && groupedItems.Count > 0)
                                {
                                    FileBrowser.SetGroupedSearchResults(groupedItems);
                                }
                                else
                                {
                                    FileBrowser.FilesItemsSource = results;
                                }
                                Debug.WriteLine($"[PerformSearch] 恢复后数量: {(FileBrowser.FilesItemsSource as System.Collections.IList)?.Count ?? 0}");
                            }
                        }

                        Debug.WriteLine($"创建新搜索标签页: {searchTabTitle}，结果数: {results.Count}");
                    }
                }
                catch (Exception ex)
                {
                    DialogService.Error($"搜索时发生错误: {ex.Message}", owner: this);
                    Debug.WriteLine($"搜索失败: {ex.Message}");
                }
            }
            finally
            {
                _isProcessingSearch = false;
            }

        }
    }
}

