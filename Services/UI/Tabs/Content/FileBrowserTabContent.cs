using System.Windows.Controls;
using YiboFile.Interfaces.Plugins;

namespace YiboFile.Services.Tabs.Content
{
    /// <summary>
    /// 文件浏览类标签页内容实现。
    /// 对应 ContentTypeId: "path", "library", "tag", "search"。
    /// 
    /// 设计说明：
    /// 文件浏览类标签是最核心、最高频的标签页类型。
    /// 与其他特殊面板不同，FileBrowserControl 实例由 PaneContentHost 直接持有，
    /// 而非通过此类的 CreateView() 创建。这样做的原因：
    /// 1. FileBrowserControl 是重量级控件（395行XAML，构造函数80行事件订阅），
    ///    需要始终保持实例化以避免切换标签时的重建开销。
    /// 2. FileBrowserControl 的 DataContext（PaneViewModel）由标签切换逻辑管理，
    ///    不适合通过 ITabContent 的 CreateView() 生命周期来控制。
    /// 
    /// 此类主要用于：
    /// - 向 TabContentRegistry 注册文件浏览类型的元数据
    /// - 在 PaneContentHost 中通过 TabContentTypes.IsFileBrowserType() 判断
    ///   是显示内置 FileBrowserControl 还是自定义面板
    /// </summary>
    public class FileBrowserTabContent : ITabContent
    {
        private readonly string _contentTypeId;
        private readonly YiboFile.Services.Localization.ILocalizationService _loc;

        /// <summary>
        /// 创建文件浏览类标签页内容。
        /// </summary>
        /// <param name="contentTypeId">
        /// 文件浏览子类型：
        /// <see cref="TabContentTypes.Path"/>、
        /// <see cref="TabContentTypes.Library"/>、
        /// <see cref="TabContentTypes.Tag"/>、
        /// <see cref="TabContentTypes.Search"/>
        /// </param>
        /// <param name="loc">本地化服务</param>
        public FileBrowserTabContent(string contentTypeId, YiboFile.Services.Localization.ILocalizationService loc = null)
        {
            _contentTypeId = contentTypeId;
            _loc = loc ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<YiboFile.Services.Localization.ILocalizationService>(App.ServiceProvider);
        }

        public string Id => _contentTypeId;

        public string Title => _contentTypeId switch
        {
            TabContentTypes.Path => _loc?["TabContent.FileBrowser"] ?? "文件浏览",
            TabContentTypes.Library => _loc?["TabContent.Library"] ?? "库",
            TabContentTypes.Tag => _loc?["TabContent.Tag"] ?? "标签",
            TabContentTypes.Search => _loc?["TabContent.Search"] ?? "搜索",
            _ => _loc?["TabContent.FileBrowser"] ?? "浏览"
        };

        public string IconKey => _contentTypeId switch
        {
            TabContentTypes.Path => null,     // 图标由文件夹类型推导
            TabContentTypes.Library => "Icon_Nav_Library",
            TabContentTypes.Tag => "Icon_Nav_Tag",
            TabContentTypes.Search => "Icon_Nav_Search",
            _ => null
        };

        public bool AllowMultiple => true;

        public bool SupportsSecondaryPane => true;

        /// <summary>
        /// 文件浏览类标签不通过此方法创建 View。
        /// PaneContentHost 直接使用内置的 FileBrowserControl 实例。
        /// 此方法返回 null，调用者应通过 TabContentTypes.IsFileBrowserType() 判断。
        /// </summary>
        public UserControl CreateView() => null;

        public void OnActivated()
        {
            // 文件浏览标签的激活由 TabService.SetActiveTab → PaneViewModel 数据绑定驱动
        }

        public void OnDeactivated()
        {
            // 文件浏览标签的停用由 TabService 管理
        }

        public void OnClosed()
        {
            // 文件浏览标签的关闭由 TabService.CloseTab 处理
        }
    }
}
