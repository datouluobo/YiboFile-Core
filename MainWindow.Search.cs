using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using OoiMRR.Services.Search;
using OoiMRR.Services.FileNotes;
using OoiMRR.Services.Tabs;

namespace OoiMRR
{
    public partial class MainWindow
    {
        internal async void PerformSearch(string searchText, bool searchNames, bool searchNotes)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                return;
            }

            // 确保至少有一个搜索选项
            if (!searchNames && !searchNotes)
            {
                MessageBox.Show("请至少选择一个搜索选项", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var normalizedKeyword = SearchService.NormalizeKeyword(searchText);

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

                if (searchResult == null || searchResult.Items == null)
                {
                    Debug.WriteLine("搜索结果为空");
                    return;
                }

                var results = searchResult.Items;
                var groupedItems = searchResult.GroupedItems;

                Debug.WriteLine($"搜索完成，共找到 {results.Count} 个结果");

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
                MessageBox.Show($"搜索时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"搜索失败: {ex.Message}");
            }

        }
    }
}
