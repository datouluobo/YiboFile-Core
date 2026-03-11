using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class TagSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        private TagSettingsViewModel _viewModel;

        public TagSettingsPanel()
        {
            InitializeComponent();
            var configService = (YiboFile.Services.Config.IConfigurationService)YiboFile.App.ServiceProvider.GetService(typeof(YiboFile.Services.Config.IConfigurationService));
            _viewModel = new TagSettingsViewModel(configService);
            this.DataContext = _viewModel;

            _viewModel.PropertyChanged += (s, e) =>
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };
        }

        private void AdjustValue(double current, double delta, double min, double max, Action<double> setter)
        {
            setter(Math.Clamp(current + delta, min, max));
        }


        private void TagFontSizeUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.TagFontSize, 1, 10, 48, v => _viewModel.TagFontSize = v);
        private void TagFontSizeDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.TagFontSize, -1, 10, 48, v => _viewModel.TagFontSize = v);
        
        private void TagBoxWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.TagBoxWidth, 5, 0, 500, v => _viewModel.TagBoxWidth = v);
        private void TagBoxWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.TagBoxWidth, -5, 0, 500, v => _viewModel.TagBoxWidth = v);

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        public void LoadSettings() => _viewModel?.LoadFromConfig();
        public void SaveSettings() { }

        private void NumericTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitNumericInput(sender as TextBox);
                e.Handled = true;
            }
        }

        private void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitNumericInput(sender as TextBox);
        }

        private void CommitNumericInput(TextBox textBox)
        {
            if (textBox == null) return;

            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            string propertyName = binding?.ParentBinding?.Path?.Path;
            if (string.IsNullOrEmpty(propertyName) || _viewModel == null) { this.Focus(); return; }

            binding.UpdateSource();

            if (double.TryParse(textBox.Text, out double value))
            {
                switch (propertyName)
                {
                    case "TagFontSizeInput":
                        _viewModel.TagFontSize = value;
                        break;
                    case "TagBoxWidthInput":
                        _viewModel.TagBoxWidth = value;
                        break;
                }
            }
            _viewModel.InvalidateInputProxy(propertyName);

            this.Focus();
        }
    }
}
