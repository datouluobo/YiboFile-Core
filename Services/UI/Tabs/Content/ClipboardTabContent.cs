using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Interfaces.Plugins;

namespace YiboFile.Services.Tabs.Content
{
    /// <summary>
    /// 剪切板历史标签页内容。
    /// 将现有的 ClipboardHistoryPanel 包装为标签页内容。
    /// 取代原有的 ActiveSpecialPanel="Clipboard" 机制。
    /// 
    /// 特殊处理：
    /// 原有的 ItemPasted 事件需在阶段 4 迁移时改为消息发布。
    /// </summary>
    public class ClipboardTabContent : ITabContent
    {
        private ClipboardHistoryPanel _cachedView;
        private readonly YiboFile.Services.Localization.ILocalizationService _loc;

        public ClipboardTabContent(YiboFile.Services.Localization.ILocalizationService loc = null)
        {
            _loc = loc ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<YiboFile.Services.Localization.ILocalizationService>(App.ServiceProvider);
        }

        public string Id => TabContentTypes.Clipboard;
        public string Title => _loc?["TabContent.Clipboard"] ?? "剪切板历史";
        public string IconKey => "Icon_Copy";
        public bool AllowMultiple => false;
        public bool SupportsSecondaryPane => true;

        public UserControl CreateView()
        {
            if (_cachedView == null)
            {
                _cachedView = new ClipboardHistoryPanel();
            }
            return _cachedView;
        }

        public void OnActivated()
        {
            // 激活时刷新剪切板历史
            // ClipboardHistoryPanel 内部通过数据绑定自动更新
        }

        public void OnDeactivated()
        {
            // 无需特殊处理
        }

        public void OnClosed()
        {
            _cachedView = null;
        }
    }
}
