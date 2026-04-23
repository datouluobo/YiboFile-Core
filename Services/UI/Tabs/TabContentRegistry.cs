using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using YiboFile.Interfaces.Plugins;
using YiboFile.Services.Core;

namespace YiboFile.Services.Tabs
{
    /// <summary>
    /// 标签页内容类型的元数据，用于构建"新建标签页"菜单等 UI。
    /// </summary>
    public class TabContentMetadata
    {
        /// <summary>内容类型 ID</summary>
        public string Id { get; set; }

        /// <summary>显示标题</summary>
        public string Title { get; set; }

        /// <summary>图标资源键</summary>
        public string IconKey { get; set; }

        /// <summary>是否允许多实例</summary>
        public bool AllowMultiple { get; set; }

        /// <summary>是否支持副栏</summary>
        public bool SupportsSecondaryPane { get; set; }
    }

    /// <summary>
    /// 标签页内容注册中心。
    /// 管理所有 ITabContent 工厂的注册与解析。
    /// 
    /// 注册来源：
    /// - Core Bootstrapper：内置 10 种类型
    /// - ProApp.ConfigureServices：Pro 扩展类型
    /// - UltraApp.ConfigureServices：Ultra 扩展类型
    /// - PluginManager：第三方插件通过 ITabPageExtension 自动注册
    /// 
    /// 线程安全：内部使用 ConcurrentDictionary，支持并发注册和解析。
    /// </summary>
    public class TabContentRegistry
    {
        private readonly ConcurrentDictionary<string, Func<ITabContent>> _factories = new();
        private readonly ConcurrentDictionary<string, TabContentMetadata> _metadata = new();

        /// <summary>
        /// 注册一个内容类型工厂。
        /// 如果同一 ID 已存在，将被覆盖（后注册优先，支持插件覆盖内置类型）。
        /// </summary>
        /// <param name="contentTypeId">内容类型 ID</param>
        /// <param name="factory">ITabContent 工厂方法</param>
        public void Register(string contentTypeId, Func<ITabContent> factory)
        {
            if (string.IsNullOrWhiteSpace(contentTypeId))
                throw new ArgumentNullException(nameof(contentTypeId));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factories[contentTypeId] = factory;

            // 尝试创建一次以提取元数据（懒加载策略：仅在需要时创建）
            // 这里不立即创建实例，元数据在首次 Resolve 时填充
            FileLogger.Log($"TabContentRegistry: Registered content type '{contentTypeId}'");
        }

        /// <summary>
        /// 注册一个内容类型工厂，并同时提供元数据。
        /// 避免了为获取元数据而创建实例的开销。
        /// </summary>
        /// <param name="contentTypeId">内容类型 ID</param>
        /// <param name="factory">ITabContent 工厂方法</param>
        /// <param name="metadata">内容类型元数据</param>
        public void Register(string contentTypeId, Func<ITabContent> factory, TabContentMetadata metadata)
        {
            Register(contentTypeId, factory);

            if (metadata != null)
            {
                metadata.Id = contentTypeId; // 确保 ID 一致
                _metadata[contentTypeId] = metadata;
            }
        }

        /// <summary>
        /// 解析并创建指定类型的 ITabContent 实例。
        /// </summary>
        /// <param name="contentTypeId">内容类型 ID</param>
        /// <returns>ITabContent 实例，如果类型未注册则返回 null。</returns>
        public ITabContent Resolve(string contentTypeId)
        {
            if (string.IsNullOrWhiteSpace(contentTypeId))
                return null;

            if (_factories.TryGetValue(contentTypeId, out var factory))
            {
                try
                {
                    var content = factory();

                    // 首次解析时自动填充元数据（如果尚未提供）
                    if (content != null && !_metadata.ContainsKey(contentTypeId))
                    {
                        _metadata[contentTypeId] = new TabContentMetadata
                        {
                            Id = contentTypeId,
                            Title = content.Title,
                            IconKey = content.IconKey,
                            AllowMultiple = content.AllowMultiple,
                            SupportsSecondaryPane = content.SupportsSecondaryPane
                        };
                    }

                    return content;
                }
                catch (Exception ex)
                {
                    FileLogger.LogException($"TabContentRegistry: Failed to resolve '{contentTypeId}'", ex);
                    return null;
                }
            }

            FileLogger.Log($"TabContentRegistry: Content type '{contentTypeId}' is not registered");
            return null;
        }

        /// <summary>
        /// 检查指定类型是否已注册。
        /// </summary>
        /// <param name="contentTypeId">内容类型 ID</param>
        /// <returns>如果已注册返回 true。</returns>
        public bool IsRegistered(string contentTypeId)
        {
            return !string.IsNullOrWhiteSpace(contentTypeId) && _factories.ContainsKey(contentTypeId);
        }

        /// <summary>
        /// 获取所有已注册内容类型的元数据。
        /// 用于构建"新建标签页"菜单、设置界面中的可用标签页列表等。
        /// 注意：仅返回已提供元数据或已被 Resolve 过的类型。
        /// </summary>
        /// <returns>所有已知的内容类型元数据列表。</returns>
        public IReadOnlyList<TabContentMetadata> GetAll()
        {
            return _metadata.Values.ToList().AsReadOnly();
        }

        /// <summary>
        /// 获取所有已注册的内容类型 ID。
        /// </summary>
        /// <returns>所有已注册的内容类型 ID 列表。</returns>
        public IReadOnlyList<string> GetRegisteredIds()
        {
            return _factories.Keys.ToList().AsReadOnly();
        }
    }
}
