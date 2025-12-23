using System;
using System.Windows;
using System.Windows.Controls;
using OoiMRR.Controls;

namespace OoiMRR.Services.Settings
{
    /// <summary>
    /// Handles showing, hiding and applying settings overlay logic.
    /// </summary>
    public class SettingsOverlayController
    {
        private readonly Grid _overlay;
        private readonly SettingsPanelControl _panel;
        private readonly Action<AppConfig> _applyConfig;
        private bool _isInitialized;

        public SettingsOverlayController(Grid overlay, SettingsPanelControl panel, Action<AppConfig> applyConfig)
        {
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
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

        public void Show()
        {
            _panel.LoadAllSettings();
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

            // 注释掉SaveAllSettings，因为各个设置面板已经在修改时实时保存（如ApplyNotesWidth）
            // 如果在这里保存，会导致当前面板保存整个config对象，覆盖其他面板已保存的值
            // _panel.SaveAllSettings();

            ApplyLatestConfig();
            _overlay.Visibility = Visibility.Collapsed;
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

            var config = ConfigManager.Load();
            _applyConfig.Invoke(config);
        }
    }
}
