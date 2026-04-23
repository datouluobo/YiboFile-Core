using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Interfaces.Plugins;

namespace YiboFile.Services.Tabs.Content
{
    /// <summary>
    /// 备份管理标签页内容。
    /// 将现有的 BackupBrowserControl 包装为标签页内容。
    /// 取代原有的 ActiveSpecialPanel="Backup" 机制。
    /// </summary>
    public class BackupTabContent : ITabContent
    {
        private BackupBrowserControl _cachedView;
        private readonly YiboFile.Services.Localization.ILocalizationService _loc;

        public BackupTabContent(YiboFile.Services.Localization.ILocalizationService loc = null)
        {
            _loc = loc ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<YiboFile.Services.Localization.ILocalizationService>(App.ServiceProvider);
        }

        public string Id => TabContentTypes.Backup;
        public string Title => _loc?["TabContent.Backup"] ?? "备份管理";
        public string IconKey => "Icon_Folder";
        public bool AllowMultiple => false;
        public bool SupportsSecondaryPane => true;

        public UserControl CreateView()
        {
            if (_cachedView == null)
            {
                _cachedView = new BackupBrowserControl();
            }
            return _cachedView;
        }

        public void OnActivated()
        {
            // 激活时可刷新备份列表
            // BackupBrowserControl 内部在 Loaded 事件中自动加载
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
