using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Services.Config;

namespace YiboFile.Services.Settings
{
    /// <summary>
    /// Handles showing, hiding and applying settings overlay logic.
    /// </summary>
    public class SettingsOverlayController
    {
        private readonly Grid _overlay;
        private readonly SettingsPanelControl _panel;
        private readonly UIElement _rightPanel;
        private readonly Action<AppConfig> _applyConfig;
        private bool _isInitialized;
        private Visibility _previousRightPanelVisibility;

        public SettingsOverlayController(Grid overlay, SettingsPanelControl panel, UIElement rightPanel, Action<AppConfig> applyConfig)
        {
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
            _rightPanel = rightPanel; // 可以为null
            _applyConfig = applyConfig;

            AttachEvents();
        }

        public void Toggle()
        {
            if (_overlay.Visibility == Visibility.Visible)
            {
                Hide();
            }
            else
            {
                Show();
            }
        }

        public void Show(string category = null)
        {
            _panel.LoadAllSettings();

            if (!string.IsNullOrEmpty(category))
            {
                _panel.SelectCategory(category);
            }

            // 隐藏右侧面板
            if (_rightPanel != null)
            {
                _previousRightPanelVisibility = _rightPanel.Visibility;
                _rightPanel.Visibility = Visibility.Collapsed;
            }

            _overlay.Visibility = Visibility.Visible;
            _isInitialized = true;
        }

        public void Hide()
        {
            if (!_isInitialized)
            {
                _overlay.Visibility = Visibility.Collapsed;
                return;
            }

            _panel.SaveAllSettings();

            // 强制立即保存配置，跳过去抖等待
            // 确保设置在关闭面板时立即写入磁盘
            ConfigurationService.Instance.SaveNow();

            // 移除ApplyLatestConfig - 刚保存的设置不应该被立即重新加载
            // 实时预览已经通过SettingsChanged事件在修改时应用了

            _overlay.Visibility = Visibility.Collapsed;

            // 恢复右侧面板为之前的状态，而不是强制 Visible
            if (_rightPanel != null)
            {
                _rightPanel.Visibility = _previousRightPanelVisibility;
            }
        }

        public void Dispose()
        {
            _panel.CloseRequested -= OnCloseRequested;
            _panel.SettingsChanged -= OnSettingsChanged;
        }

        private void AttachEvents()
        {
            _panel.CloseRequested += OnCloseRequested;
            _panel.SettingsChanged += OnSettingsChanged;
        }

        private void OnCloseRequested(object sender, EventArgs e)
        {
            Hide();
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            ApplyLatestConfig();
        }

        private void ApplyLatestConfig()
        {
            if (_applyConfig == null) return;

            // 使用ConfigurationService获取即时的内存配置快照，而不是读取磁盘上的文件
            // 这样可以确保未保存到磁盘的实时预览修改也能被应用
            var config = ConfigurationService.Instance.GetSnapshot();
            _applyConfig.Invoke(config);
        }
    }
}

