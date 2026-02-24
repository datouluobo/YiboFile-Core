using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using YiboFile.ViewModels.Settings;
using Forms = System.Windows.Forms;

namespace YiboFile.Controls.Settings
{
    public partial class GeneralSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private GeneralSettingsViewModel _generalViewModel;
        private DataSettingsViewModel _dataViewModel;

        public GeneralSettingsPanel()
        {
            InitializeComponent();
            var configService = (YiboFile.Services.Config.IConfigurationService)YiboFile.App.ServiceProvider.GetService(typeof(YiboFile.Services.Config.IConfigurationService));
            _generalViewModel = new GeneralSettingsViewModel(configService);
            _dataViewModel = new DataSettingsViewModel();
            this.DataContext = _generalViewModel;

            _generalViewModel.PropertyChanged += (s, e) => SettingsChanged?.Invoke(this, EventArgs.Empty);
            _dataViewModel.SettingsReloadRequested += (s, e) => _generalViewModel.LoadFromConfig();
            
            Loaded += (s, e) => InitializeState();
        }

        public void LoadSettings()
        {
            _generalViewModel?.LoadFromConfig();
            InitializeState();
        }

        public void SaveSettings()
        {
            // Bindings handle updates automatically
        }

        private void InitializeState()
        {
            if (_generalViewModel.TabWidthMode == TabWidthMode.DynamicWidth)
                TabWidthDynamicRadio.IsChecked = true;
            else
                TabWidthFixedRadio.IsChecked = true;
            UpdatePinnedTabWidthUIState();
        }

        private void TabWidthMode_Checked(object sender, RoutedEventArgs e)
        {
            if (TabWidthDynamicRadio?.IsChecked == true)
                _generalViewModel.TabWidthMode = TabWidthMode.DynamicWidth;
            else if (TabWidthFixedRadio?.IsChecked == true)
                _generalViewModel.TabWidthMode = TabWidthMode.FixedWidth;
            UpdatePinnedTabWidthUIState();
        }

        private void UpdatePinnedTabWidthUIState()
        {
            bool isFixedMode = _generalViewModel.TabWidthMode != TabWidthMode.DynamicWidth;
            if (PinnedTabWidthLabel != null) PinnedTabWidthLabel.Opacity = isFixedMode ? 1.0 : 0.5;
            if (PinnedTabWidthTextBox != null) PinnedTabWidthTextBox.IsEnabled = isFixedMode;
        }

        private void AdjustValue(double current, double delta, double min, double max, Action<double> setter)
        {
            double newValue = Math.Clamp(current + delta, min, max);
            setter(newValue);
        }

        private void PinnedTabWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.PinnedTabWidth, 10, 50, 300, v => _generalViewModel.PinnedTabWidth = v);
        private void PinnedTabWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.PinnedTabWidth, -10, 50, 300, v => _generalViewModel.PinnedTabWidth = v);
        private void UIFontSizeUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.UIFontSize, 1, 10, 48, v => _generalViewModel.UIFontSize = v);
        private void UIFontSizeDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.UIFontSize, -1, 10, 48, v => _generalViewModel.UIFontSize = v);
        private void TagFontSizeUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TagFontSize, 1, 10, 48, v => _generalViewModel.TagFontSize = v);
        private void TagFontSizeDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TagFontSize, -1, 10, 48, v => _generalViewModel.TagFontSize = v);
        private void TagBoxWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TagBoxWidth, 5, 0, 500, v => _generalViewModel.TagBoxWidth = v);
        private void TagBoxWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TagBoxWidth, -10, 0, 500, v => _generalViewModel.TagBoxWidth = v);

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void BrowseBaseDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Forms.FolderBrowserDialog
            {
                Description = "选择配置/数据存储目录（默认 .\\AppData）",
                SelectedPath = _generalViewModel.BaseDirectory
            };
            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                _generalViewModel.ChangeBaseDirectoryCommand.Execute(dialog.SelectedPath);
            }
        }

        private void ExportFileAndExecute(ICommand command, string defaultName)
        {
            var sfd = new SaveFileDialog { FileName = defaultName, Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*" };
            if (sfd.ShowDialog() == true)
            {
                try { command.Execute(sfd.FileName); MessageBox.Show("文件已导出。", "成功", MessageBoxButton.OK, MessageBoxImage.Information); }
                catch (Exception ex) { MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void ImportFileAndExecute(ICommand command)
        {
            var ofd = new OpenFileDialog { Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*" };
            if (ofd.ShowDialog() == true)
            {
                try { command.Execute(ofd.FileName); MessageBox.Show("文件已导入。", "成功", MessageBoxButton.OK, MessageBoxImage.Information); }
                catch (Exception ex) { MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void ExportSettings_Click(object sender, RoutedEventArgs e) => ExportFileAndExecute(_dataViewModel.ExportConfigsCommand, "settings_backup.zip");
        private void ImportSettings_Click(object sender, RoutedEventArgs e) => ImportFileAndExecute(_dataViewModel.ImportConfigsCommand);
        private void ExportData_Click(object sender, RoutedEventArgs e) => ExportFileAndExecute(_dataViewModel.ExportDataCommand, "data_backup.zip");
        private void ImportData_Click(object sender, RoutedEventArgs e) => ImportFileAndExecute(_dataViewModel.ImportDataCommand);
        private void ExportAll_Click(object sender, RoutedEventArgs e) => ExportFileAndExecute(_dataViewModel.ExportAllCommand, "full_backup.zip");
        private void ImportAll_Click(object sender, RoutedEventArgs e) => ImportFileAndExecute(_dataViewModel.ImportAllCommand);
    }
}
