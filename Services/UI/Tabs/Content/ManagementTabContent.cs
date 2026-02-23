using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Interfaces.Plugins;

namespace YiboFile.Services.Tabs.Content
{
    /// <summary>
    /// 路径/库/标签管理标签页内容。
    /// 将 ManagementPanelControl 包装为标签页内容。
    /// 取代原有的 NavigationSettingsWindow 独立窗口。
    /// </summary>
    public class ManagementTabContent : ITabContent
    {
        private ManagementPanelControl _cachedView;
        private string _initialTab;

        /// <summary>
        /// 创建管理标签页内容。
        /// </summary>
        /// <param name="initialTab">初始选中的子标签页：Path / Library / Tag。默认 Path。</param>
        public ManagementTabContent(string initialTab = "Path")
        {
            _initialTab = initialTab;
        }

        public string Id => TabContentTypes.Management;
        public string Title => "路径与库管理";
        public string IconKey => "Icon_Nav_Library";
        public bool AllowMultiple => false;
        public bool SupportsSecondaryPane => true;

        public UserControl CreateView()
        {
            if (_cachedView == null)
            {
                _cachedView = new ManagementPanelControl();
            }
            return _cachedView;
        }

        public void OnActivated()
        {
            // 如果有指定初始标签页，选中它
            if (!string.IsNullOrEmpty(_initialTab) && _cachedView != null)
            {
                _cachedView.SelectTab(_initialTab);
                _initialTab = null; // 仅首次生效
            }
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
