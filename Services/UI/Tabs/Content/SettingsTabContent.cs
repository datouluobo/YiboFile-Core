using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Interfaces.Plugins;

namespace YiboFile.Services.Tabs.Content
{
    /// <summary>
    /// 设置页标签页内容。
    /// 将现有的 SettingsPanelControl 包装为标签页内容。
    /// AllowMultiple = false：应用中只允许打开一个设置标签页。
    /// </summary>
    public class SettingsTabContent : ITabContent
    {
        private SettingsPanelControl _cachedView;
        private readonly YiboFile.Services.Localization.ILocalizationService _loc;

        public SettingsTabContent(YiboFile.Services.Localization.ILocalizationService loc = null)
        {
            _loc = loc ?? Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<YiboFile.Services.Localization.ILocalizationService>(App.ServiceProvider);
        }

        public string Id => TabContentTypes.Settings;
        public string Title => _loc?["TabContent.Settings"] ?? "设置";
        public string IconKey => "Icon_Window_Settings";
        public bool AllowMultiple => false;
        public bool SupportsSecondaryPane => false; // 设置页在窄副栏中体验不佳

        public UserControl CreateView()
        {
            if (_cachedView == null)
            {
                _cachedView = new SettingsPanelControl();
            }
            return _cachedView;
        }

        public void OnActivated()
        {
            // 设置页激活时可刷新当前配置状态
            // 具体逻辑在阶段 4 迁移时补充
        }

        public void OnDeactivated()
        {
            // 切离设置页时暂不需要特殊处理
            // 配置变更是即时生效的
        }

        public void OnClosed()
        {
            // 关闭设置页时触发配置保存
            // 原 SettingsOverlayController.ApplyAndClose 的保存逻辑
            // 将在阶段 4 迁移时从 SettingsOverlayController 中提取
            _cachedView = null; // 释放引用，下次打开重建
        }
    }
}
