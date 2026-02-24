using System;
using System.Windows.Controls;
using YiboFile.Services.Theming;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class AppearanceSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        private AppearanceSettingsViewModel _appearanceViewModel;

        public AppearanceSettingsPanel()
        {
            InitializeComponent();
            var themeService = YiboFile.App.ServiceProvider?.GetService(typeof(IThemeService)) as IThemeService;
            var configService = YiboFile.App.ServiceProvider?.GetService(typeof(YiboFile.Services.Config.IConfigurationService)) as YiboFile.Services.Config.IConfigurationService;
            _appearanceViewModel = new AppearanceSettingsViewModel(themeService, configService);
            _appearanceViewModel.PropertyChanged += (s, e) => SettingsChanged?.Invoke(this, EventArgs.Empty);
            DataContext = _appearanceViewModel;
            
            // 延迟刷新 ViewModel 选中项通知
            Dispatcher.BeginInvoke(new Action(() => _appearanceViewModel.RefreshBindings()),
                System.Windows.Threading.DispatcherPriority.Loaded);

            LoadSettings();
        }

        public void LoadSettings()
        {
            _appearanceViewModel?.LoadFromConfig();
        }

        public void SaveSettings()
        {
            // Bindings handle updates automatically
        }
    }
}
