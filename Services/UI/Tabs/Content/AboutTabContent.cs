using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Interfaces.Plugins;

namespace YiboFile.Services.Tabs.Content
{
    /// <summary>
    /// 关于页标签页内容。
    /// 将现有的 AboutPanelControl 包装为标签页内容。
    /// AllowMultiple = false：应用中只允许打开一个关于标签页。
    /// </summary>
    public class AboutTabContent : ITabContent
    {
        private AboutPanelControl _cachedView;
        private readonly YiboFile.Services.Localization.ILocalizationService _loc;

        public AboutTabContent(YiboFile.Services.Localization.ILocalizationService loc = null)
        {
            _loc = loc ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<YiboFile.Services.Localization.ILocalizationService>(App.ServiceProvider);
        }

        public string Id => TabContentTypes.About;
        public string Title => _loc?["TabContent.About"] ?? "关于";
        public string IconKey => "Icon_Window_About";
        public bool AllowMultiple => false;
        public bool SupportsSecondaryPane => true;

        public UserControl CreateView()
        {
            if (_cachedView == null)
            {
                _cachedView = new AboutPanelControl();
            }
            return _cachedView;
        }

        public void OnActivated()
        {
            // 关于页无需特殊激活逻辑
        }

        public void OnDeactivated()
        {
            // 关于页无需特殊停用逻辑
        }

        public void OnClosed()
        {
            _cachedView = null;
        }
    }
}
