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
            var locService = YiboFile.App.ServiceProvider?.GetService(typeof(YiboFile.Services.Localization.ILocalizationService)) as YiboFile.Services.Localization.ILocalizationService;
            _appearanceViewModel = new AppearanceSettingsViewModel(themeService, configService, locService);
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

        private void AdjustValue(double currentValue, double delta, double min, double max, Action<double> updateAction)
        {
            double newValue = Math.Clamp(currentValue + delta, min, max);
            updateAction(newValue);
        }

        private void UIFontSizeUp_Click(object sender, System.Windows.RoutedEventArgs e) => AdjustValue(_appearanceViewModel.UIFontSize, 1, 10, 48, v => _appearanceViewModel.UIFontSize = v);
        private void UIFontSizeDown_Click(object sender, System.Windows.RoutedEventArgs e) => AdjustValue(_appearanceViewModel.UIFontSize, -1, 10, 48, v => _appearanceViewModel.UIFontSize = v);

        private void NumericTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                CommitNumericInput(sender as TextBox);
                e.Handled = true;
            }
        }

        private void NumericTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            CommitNumericInput(sender as TextBox);
        }

        private void CommitNumericInput(TextBox textBox)
        {
            if (textBox == null) return;

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            string propertyName = binding?.ParentBinding?.Path?.Path;
            if (string.IsNullOrEmpty(propertyName) || _appearanceViewModel == null) { this.Focus(); return; }

            binding.UpdateSource();

            if (double.TryParse(textBox.Text, out double value))
            {
                switch (propertyName)
                {
                    case "UIFontSizeInput":
                        _appearanceViewModel.UIFontSize = value;
                        break;
                }
            }
            _appearanceViewModel.InvalidateInputProxy(propertyName);

            this.Focus();
        }

        private void NumericTextBox_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
        }
    }
}
