using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Interfaces.Plugins;

namespace YiboFile.Services.Tabs.Content
{
    /// <summary>
    /// 回收站管理标签页内容
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
        public string Title => "回收站";
        public string IconKey => "Icon_Recycle";
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
        }

        public void OnDeactivated()
        {
        }

        public void OnClosed()
        {
            _cachedView = null;
        }
    }
}
