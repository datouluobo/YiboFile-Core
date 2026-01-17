using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;

namespace YiboFile.Services.Search
{
    /// <summary>
    /// 搜索缓存服务
    /// 负责管理搜索结果的缓存
    /// </summary>
    public class SearchCacheService
    {
        private readonly Dictionary<string, SearchCache> _cache = new Dictionary<string, SearchCache>();
        private TimeSpan _cacheTTL = TimeSpan.FromSeconds(30);

        /// <summary>
        /// 缓存TTL（默认30秒）
        /// </summary>
        public TimeSpan CacheTTL
        {
            get => _cacheTTL;
            set => _cacheTTL = value;
        }

        /// <summary>
        /// 获取缓存项
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>缓存项，如果不存在或已过期则返回 null</returns>
        public SearchCache GetCache(string key)
        {
            if (string.IsNullOrEmpty(key) || !_cache.TryGetValue(key, out var cache))
            {
                return null;
            }

            // 检查是否过期
            if (DateTime.UtcNow - cache.LastUpdated > _cacheTTL)
            {
                _cache.Remove(key);
                return null;
            }

            return cache;
        }

        /// <summary>
        /// 更新缓存
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="items">搜索结果项</param>
        /// <param name="offset">当前偏移量</param>
        /// <param name="hasMore">是否有更多结果</param>
        /// <param name="keyword">搜索关键词</param>
        /// <param name="rangePath">路径范围</param>
        /// <param name="type">文件类型过滤器</param>
        /// <param name="pathRange">路径范围过滤器</param>
        public void UpdateCache(string key, List<FileSystemItem> items, int offset, bool hasMore,
            string keyword, string rangePath, FileTypeFilter type, PathRangeFilter pathRange)
        {
            try
            {
                _cache[key] = new SearchCache
                {
                    Keyword = keyword,
                    Items = new List<FileSystemItem>(items),
                    LastUpdated = DateTime.UtcNow,
                    Type = type,
                    PathRange = pathRange,
                    RangePath = rangePath,
                    Offset = offset,
                    HasMore = hasMore
                };
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        /// <param name="key">缓存键，null 表示清除所有缓存</param>
        public void ClearCache(string key = null)
        {
            if (key == null)
            {
                _cache.Clear();
            }
            else
            {
                _cache.Remove(key);
            }
        }

        /// <summary>
        /// 检查缓存是否存在且有效
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>如果缓存存在且有效返回 true</returns>
        public bool IsCacheValid(string key)
        {
            return GetCache(key) != null;
        }
    }
}

























