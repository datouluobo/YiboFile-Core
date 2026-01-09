using System;
using System.Collections.Generic;
using System.Linq;

namespace OoiMRR.Services.Search
{
    /// <summary>
    /// 标签搜索执行器
    /// 在已加载的文件项中按标签名称进行过滤
    /// </summary>
    public class TagSearchExecutor
    {
        /// <summary>
        /// 在结果集中按标签名称过滤
        /// </summary>
        /// <param name="items">要过滤的文件项列表</param>
        /// <param name="tagKeyword">标签关键词</param>
        /// <returns>匹配标签的文件项</returns>
        public IEnumerable<FileSystemItem> FilterByTag(IEnumerable<FileSystemItem> items, string tagKeyword)
        {
            if (items == null || string.IsNullOrWhiteSpace(tagKeyword))
            {
                return items ?? Enumerable.Empty<FileSystemItem>();
            }

            var keyword = tagKeyword.Trim().ToLower();

            return items.Where(item =>
            {
                if (string.IsNullOrEmpty(item.Tags))
                    return false;

                // 标签通常以逗号分隔
                var tags = item.Tags.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
                return tags.Any(tag => tag.Trim().ToLower().Contains(keyword));
            });
        }

        /// <summary>
        /// 获取所有不重复的标签名称（用于搜索建议）
        /// </summary>
        /// <param name="items">文件项列表</param>
        /// <returns>去重后的标签名称列表</returns>
        public List<string> GetDistinctTags(IEnumerable<FileSystemItem> items)
        {
            if (items == null)
                return new List<string>();

            var allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Tags))
                    continue;

                var tags = item.Tags.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var tag in tags)
                {
                    var trimmed = tag.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        allTags.Add(trimmed);
                    }
                }
            }

            return allTags.OrderBy(t => t).ToList();
        }

        /// <summary>
        /// 精确匹配标签名称
        /// </summary>
        /// <param name="items">要过滤的文件项列表</param>
        /// <param name="tagName">精确的标签名称</param>
        /// <returns>匹配的文件项</returns>
        public IEnumerable<FileSystemItem> FilterByExactTag(IEnumerable<FileSystemItem> items, string tagName)
        {
            if (items == null || string.IsNullOrWhiteSpace(tagName))
            {
                return items ?? Enumerable.Empty<FileSystemItem>();
            }

            var exactTag = tagName.Trim();

            return items.Where(item =>
            {
                if (string.IsNullOrEmpty(item.Tags))
                    return false;

                var tags = item.Tags.Split(new[] { ',', '，', ';', '；' }, StringSplitOptions.RemoveEmptyEntries);
                return tags.Any(tag => string.Equals(tag.Trim(), exactTag, StringComparison.OrdinalIgnoreCase));
            });
        }
    }
}
