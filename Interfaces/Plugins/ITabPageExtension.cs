namespace YiboFile.Interfaces.Plugins
{
    /// <summary>
    /// 标签页扩展接口。
    /// 插件通过实现此接口向应用注册自定义标签页类型。
    /// 与 <see cref="IMenuExtension"/>、<see cref="IPreviewExtension"/> 并列的第三种插件扩展点。
    /// 
    /// 使用方式：
    /// 1. 在插件 DLL 中实现此接口
    /// 2. PluginManager 在加载时自动扫描并注册到 TabContentRegistry
    /// 3. 用户通过菜单或快捷键打开对应标签页
    /// </summary>
    public interface ITabPageExtension
    {
        /// <summary>
        /// 要注册的内容类型 ID。
        /// 建议使用 "vendor.name" 格式以避免冲突（如 "mycompany.cad-manager"）。
        /// </summary>
        string ContentTypeId { get; }

        /// <summary>
        /// 创建 <see cref="ITabContent"/> 实例。
        /// 每次调用可返回新实例或缓存实例，取决于实现者的策略。
        /// </summary>
        /// <returns>标签页内容实例。</returns>
        ITabContent CreateContent();
    }
}
