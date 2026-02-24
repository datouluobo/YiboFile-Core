using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Settings;
using YiboFile.Windows;

namespace YiboFile.Controls.Settings
{
    public partial class HotkeySettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        private HotkeySettingsViewModel _viewModel;

        public HotkeySettingsPanel()
        {
            InitializeComponent();
            var configService = (YiboFile.Services.Config.IConfigurationService)YiboFile.App.ServiceProvider.GetService(typeof(YiboFile.Services.Config.IConfigurationService));
            _viewModel = new HotkeySettingsViewModel(configService);
            this.DataContext = _viewModel;
        }

        private void EditHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HotkeyItemViewModel item)
            {
                var dialog = new HotkeyEditWindow(item)
                {
                    Owner = Window.GetWindow(this)
                };
                if (dialog.ShowDialog() == true)
                {
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void ResetHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HotkeyItemViewModel item)
            {
                _viewModel.ResetSingleHotkeyCommand.Execute(item);
            }
        }

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
        }

        public void SaveSettings()
        {
            // Changes to hotkeys are updated dynamically via bind.
        }
    }
}
