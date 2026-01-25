using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using YiboFile.Services;
using YiboFile.Services.Tabs;
using YiboFile.Models.UI;
using YiboFile.Services.Search;
using YiboFile.Services.FileNotes; // For FileNotesService

namespace YiboFile
{
    public partial class MainWindow
    {
        #region 标签页管理

        /// <summary>
        /// 创建新标签页
        /// </summary>
        internal void CreateTab(string path, bool forceNewTab = false, bool? activate = null)
        {
            // Determine activation behavior (default to Config if null, or true if Config unavailable)
            bool shouldActivate = activate ?? _configService?.Config?.ActivateNewTabOnMiddleClick ?? true;

            // 在双列表模式下，根据焦点判断在哪个列表创建标签
            if (_isDualListMode && _isSecondPaneFocused && _secondTabService != null)
            {
                _secondTabService.CreatePathTab(path, forceNewTab, shouldActivate);
            }
            else
            {
                _tabService?.CreatePathTab(path, forceNewTab, shouldActivate);
            }
        }

        /// <summary>
        /// 在标签页中打开库
        /// </summary>
        internal void OpenLibraryInTab(Library library, bool forceNewTab = false, bool? activate = null)
        {
            // Determine activation behavior (default to Config if null, or true if Config unavailable)
            bool shouldActivate = activate ?? _configService?.Config?.ActivateNewTabOnMiddleClick ?? true;

            if (_isDualListMode && _isSecondPaneFocused && _secondTabService != null)
            {
                _secondTabService.OpenLibraryTab(library, forceNewTab, shouldActivate);
            }
            else
            {
                _tabService?.OpenLibraryTab(library, forceNewTab, shouldActivate);
            }
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

                string keyword = null;
                bool isContentSearch = false;

                if (searchTabPath.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
                {
                    keyword = searchTabPath.Substring("content://".Length);
                    isContentSearch = true;
                }
                else if (searchTabPath.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
                {
                    keyword = searchTabPath.Substring("search://".Length);
                }
                else
                {
                    return;
                }

                if (string.IsNullOrEmpty(keyword)) return;

                var cacheKey = searchTabPath;
                var cache = _searchCacheService.GetCache(cacheKey);

                if (cache == null || !_searchCacheService.IsCacheValid(cacheKey))
                {
                    // 无缓存或缓存过期，触发刷新
                    await RefreshActiveSearchTab(keyword, isContentSearch);
                    return;
                }

                // 缓存有效则直接使用
                _currentFiles = new List<FileSystemItem>(cache.Items);

                // 从缓存恢复时，如果结果项已有 SearchResultType，则构建分组显示
                // 否则使用普通列表显示
                var groupedItems = SearchResultGrouper.BuildGroupedFromCachedResults(_currentFiles);

                if (FileBrowser != null)
                {
                    // ... (UI update logic logic similar to PerformSearch)
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

                    if (_currentFiles.Count == 0)
                    {
                        FileBrowser.ShowEmptyState("未找到匹配项");
                        FileBrowser.SetSearchStatus(false);
                    }
                    else
                    {
                        FileBrowser.HideEmptyState();
                        FileBrowser.SetSearchStatus(true, $"找到 {_currentFiles.Count} 个结果");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查搜索缓存失败: {ex.Message}");
            }
        }

        private async Task RefreshActiveSearchTab(string keyword, bool isContentSearch = false)
        {
            try
            {
                FileBrowser?.ShowEmptyState("正在刷新...");

                if (isContentSearch)
                {
                    // 全文搜索刷新
                    // 注意：FullTextSearchService.Instance.SearchContent 是同步的还是支持异步？
                    // 它是 CPU 密集型，应该在 Task.Run 中运行
                    var results = await Task.Run(() => YiboFile.Services.FullTextSearch.FullTextSearchService.Instance.SearchContent(keyword));

                    if (results == null) results = new List<FileSystemItem>();
                    _currentFiles = results;

                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = _currentFiles;
                        if (_currentFiles.Count == 0)
                        {
                            FileBrowser.ShowEmptyState("未找到包含该内容的文件");
                            FileBrowser.SetSearchStatus(false);
                        }
                        else
                        {
                            FileBrowser.HideEmptyState();
                            // 更新地址栏和面包屑
                            FileBrowser.SetSearchBreadcrumb($"内容: {keyword}");
                            FileBrowser.AddressText = $"content://{keyword}";
                            FileBrowser.SetSearchStatus(true, $"找到 {_currentFiles.Count} 个结果");
                        }
                        FileBrowser.LoadMoreVisible = false;
                    }
                    return;
                }

                // 常规搜索 (Everything)
                // 规范化关键词
                var normalizedKeyword = SearchService.NormalizeKeyword(keyword);

                // 使用 SearchService 刷新搜索
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

                        // 统一空状态处理
                        if (_currentFiles.Count == 0)
                        {
                            FileBrowser.ShowEmptyState("未找到匹配项");
                            FileBrowser.SetSearchStatus(false);
                        }
                        else
                        {
                            FileBrowser.HideEmptyState();
                            FileBrowser.SetSearchStatus(true, $"找到 {_currentFiles.Count} 个结果");
                        }
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

