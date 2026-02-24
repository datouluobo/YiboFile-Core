using System;
using System.Windows.Controls;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class PathSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        private NavigationSettingsViewModel _viewModel;

        public PathSettingsPanel()
        {
            InitializeComponent();
            var configService = (YiboFile.Services.Config.IConfigurationService)YiboFile.App.ServiceProvider.GetService(typeof(YiboFile.Services.Config.IConfigurationService));
            _viewModel = new NavigationSettingsViewModel(configService);
            this.DataContext = _viewModel;
        }

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
        }

        public void SaveSettings()
        {
        }
    }
}
