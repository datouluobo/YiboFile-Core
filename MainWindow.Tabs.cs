using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using OoiMRR.Services;
using OoiMRR.Services.Tabs;
using OoiMRR.Models.UI;
using OoiMRR.Services.Search;
using OoiMRR.Services.FileNotes; // For FileNotesService

namespace OoiMRR
{
    public partial class MainWindow
    {
        #region 标签页管理

        /// <summary>
        /// 创建新标签页
        /// </summary>
        internal void CreateTab(string path, bool forceNewTab = false)
        {
            _tabService?.CreatePathTab(path, forceNewTab);
        }

        /// <summary>
        /// 在标签页中打开库
        /// </summary>
        internal void OpenLibraryInTab(Library library, bool forceNewTab = false)
        {
            _tabService?.OpenLibraryTab(library, forceNewTab);
        }

        private void OpenTagInTab(Tag tag, bool forceNewTab = false)
        {
            // _tabService?.OpenTagTab(tag, forceNewTab); // Phase 2
        }

        /// <summary>
        /// 切换到指定标签页（统一处理库和路径）
        /// </summary>
        internal void SwitchToTab(PathTab tab)
        {
            _tabService?.SwitchToTab(tab);
        }

        private async void CheckAndRefreshSearchTab(string searchTabPath)
        {
            try
            {
                if (string.IsNullOrEmpty(searchTabPath)) return;

                var keyword = searchTabPath.Substring("search://".Length);
                if (string.IsNullOrEmpty(keyword)) return;

                var cacheKey = searchTabPath;
                var cache = _searchCacheService.GetCache(cacheKey);

                if (cache == null || !_searchCacheService.IsCacheValid(cacheKey))
                {
                    // 无缓存或缓存过期，触发刷新
                    await RefreshActiveSearchTab(keyword);
                    return;
                }

                // 缓存有效则直接使用
                _currentFiles = new List<FileSystemItem>(cache.Items);

                // 从缓存恢复时，如果结果项已有 SearchResultType，则构建分组显示
                // 否则使用普通列表显示
                var groupedItems = SearchResultGrouper.BuildGroupedFromCachedResults(_currentFiles);

                if (FileBrowser != null)
                {
                    if (groupedItems != null && groupedItems.Count > 0)
                    {
                        FileBrowser.SetGroupedSearchResults(groupedItems);
                    }
                    else
                    {
                        FileBrowser.FilesItemsSource = null;
                        FileBrowser.FilesItemsSource = _currentFiles;
                    }
                    FileBrowser.LoadMoreVisible = cache.HasMore;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查搜索缓存失败: {ex.Message}");
            }
        }

        private async Task RefreshActiveSearchTab(string keyword)
        {
            try
            {
                // 规范化关键词（确保使用规范化后的关键词）
                var normalizedKeyword = SearchService.NormalizeKeyword(keyword);

                FileBrowser?.ShowEmptyState("正在刷新...");

                // 使用 SearchService 刷新搜索（重新执行完整搜索，包含备注搜索以支持分组显示）
                var searchResult = await _searchService.PerformSearchAsync(
                    keyword: normalizedKeyword,
                    searchOptions: _searchOptions,
                    currentPath: _currentPath,
                    searchNames: true,
                    searchNotes: true,
                    getNotesFromDb: keywordParam => FileNotesService.SearchFilesByNotes(keywordParam)
                );

                if (searchResult != null && searchResult.Items != null)
                {
                    _currentFiles = searchResult.Items;
                    var groupedItems = searchResult.GroupedItems;

                    if (FileBrowser != null)
                    {
                        // 使用分组显示
                        if (groupedItems != null && groupedItems.Count > 0)
                        {
                            FileBrowser.SetGroupedSearchResults(groupedItems);
                        }
                        else
                        {
                            FileBrowser.FilesItemsSource = null;
                            FileBrowser.FilesItemsSource = _currentFiles;
                        }
                        // 更新地址栏和面包屑，确保显示规范化关键词
                        FileBrowser.SetSearchBreadcrumb(normalizedKeyword);
                        FileBrowser.AddressText = normalizedKeyword;
                        FileBrowser.LoadMoreVisible = searchResult.HasMore;
                        FileBrowser.HideEmptyState();
                    }
                }
                else
                {
                    FileBrowser?.ShowEmptyState("刷新失败，点击搜索重试");
                }
            }
            catch (Exception ex)
            {
                FileBrowser?.ShowEmptyState("刷新失败，点击搜索重试");
                Debug.WriteLine($"刷新搜索标签页失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新所有标签页样式（高亮当前标签）
        /// </summary>
        private void UpdateTabStyles()
        {
            _tabService?.UpdateTabStyles();
        }
        /// <summary>
        /// 在库模式下设置标签页（为库的每个路径创建标签页）
        /// </summary>
        private void SetupLibraryTabs(Library library)
        {
            _tabService?.SetupLibraryTabs(library);
        }

        /// <summary>
        /// 清空库模式下的标签页
        /// </summary>
        private void ClearTabsInLibraryMode()
        {
            _tabService?.ClearTabsInLibraryMode();
        }

        /// <summary>
        /// 关闭标签页
        /// </summary>
        internal void CloseTab(PathTab tab)
        {
            _tabService?.CloseTab(tab);
        }

        /// <summary>
        /// 更新标签页标题
        /// </summary>
        private void UpdateTabTitle(PathTab tab, string path)
        {
            _tabService?.UpdateTabTitle(tab, path);
        }

        private void TabButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PathTab tab)
            {
                _tabService?.SwitchToTab(tab);
            }
        }

        #endregion
    }
}
