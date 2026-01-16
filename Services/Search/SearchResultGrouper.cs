using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace YiboFile.Services.Search
{
    /// <summary>
    /// 搜索结果分组器
    /// 负责将搜索结果按类型分组（备注、文件夹、文件）
    /// </summary>
    public static class SearchResultGrouper
    {
        /// <summary>
        /// 构建分组结果（完整版本，包含备注和文件名匹配信息）
        /// </summary>
        /// <param name="results">搜索结果列表</param>
        /// <param name="notesResultPaths">备注匹配的路径集合</param>
        /// <param name="nameResultPaths">文件名匹配的路径集合（可选）</param>
        /// <returns>分组后的结果字典</returns>
        public static Dictionary<SearchResultType, List<FileSystemItem>> BuildGroupedResults(
            List<FileSystemItem> results,
            HashSet<string> notesResultPaths,
            HashSet<string> nameResultPaths = null)
        {
            var groupedItems = new Dictionary<SearchResultType, List<FileSystemItem>>();
            var notesItems = new List<FileSystemItem>();
            var folderItems = new List<FileSystemItem>();
            var fileItems = new List<FileSystemItem>();

            foreach (var item in results)
            {
                bool isNoteMatch = notesResultPaths != null && 
                    notesResultPaths.Contains(item.Path, StringComparer.OrdinalIgnoreCase);
                bool isNameMatch = nameResultPaths != null && 
                    nameResultPaths.Contains(item.Path, StringComparer.OrdinalIgnoreCase);

                // 备注匹配优先：只要备注匹配就进入备注分组（文件夹仍归"文件夹"）
                if (isNoteMatch)
                {
                    item.IsFromNotesSearch = true;
                    if (item.IsDirectory)
                    {
                        item.SearchResultType = SearchResultType.Folder;
                        folderItems.Add(item);
                    }
                    else
                    {
                        item.SearchResultType = SearchResultType.Notes;
                        notesItems.Add(item);
                    }
                    continue;
                }

                // 名称匹配
                if (item.IsDirectory)
                {
                    item.SearchResultType = SearchResultType.Folder;
                    if (isNameMatch)
                    {
                        item.IsFromNameSearch = true;
                    }
                    folderItems.Add(item);
                }
                else
                {
                    item.SearchResultType = SearchResultType.File;
                    if (isNameMatch)
                    {
                        item.IsFromNameSearch = true;
                    }
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

            Debug.WriteLine($"分组结果: 备注={notesItems.Count}, 文件夹={folderItems.Count}, 文件={fileItems.Count}");

            return groupedItems;
        }

        /// <summary>
        /// 从已有 SearchResultType 的搜索结果构建分组（用于缓存恢复）
        /// </summary>
        /// <param name="results">搜索结果列表（应已设置 SearchResultType）</param>
        /// <returns>分组后的结果字典，如果结果中没有 SearchResultType 则返回 null</returns>
        public static Dictionary<SearchResultType, List<FileSystemItem>> BuildGroupedFromCachedResults(
            List<FileSystemItem> results)
        {
            // 检查是否有任何项设置了 SearchResultType，如果没有则返回 null（使用普通列表显示）
            bool hasSearchResultType = results.Any(item => item.SearchResultType != null);
            if (!hasSearchResultType)
            {
                return null;
            }

            var groupedItems = new Dictionary<SearchResultType, List<FileSystemItem>>();
            var notesItems = new List<FileSystemItem>();
            var folderItems = new List<FileSystemItem>();
            var fileItems = new List<FileSystemItem>();

            foreach (var item in results)
            {
                // 根据已有的 SearchResultType 分组
                if (item.SearchResultType == SearchResultType.Notes)
                {
                    notesItems.Add(item);
                }
                else if (item.SearchResultType == SearchResultType.Folder || item.IsDirectory)
                {
                    // 确保 SearchResultType 正确设置
                    if (item.SearchResultType != SearchResultType.Notes)
                    {
                        item.SearchResultType = SearchResultType.Folder;
                        folderItems.Add(item);
                    }
                }
                else
                {
                    // 文件类型
                    if (item.SearchResultType != SearchResultType.Notes)
                    {
                        item.SearchResultType = SearchResultType.File;
                        fileItems.Add(item);
                    }
                }
            }

            // 添加到分组字典
            if (notesItems.Count > 0)
                groupedItems[SearchResultType.Notes] = notesItems;
            if (folderItems.Count > 0)
                groupedItems[SearchResultType.Folder] = folderItems;
            if (fileItems.Count > 0)
                groupedItems[SearchResultType.File] = fileItems;

            return groupedItems;
        }
    }
}

















