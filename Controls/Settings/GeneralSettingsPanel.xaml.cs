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
            
            // 触发绑定更新（如果当前还在输入中）
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            if (binding != null)
            {
                binding.UpdateSource();
                
                // 获取绑定的属性名
                string propertyName = binding.ParentBinding.Path.Path;
                if (!string.IsNullOrEmpty(propertyName) && _generalViewModel != null)
                {
                    // 根据 Input 属性名找到对应的底层属性 Reset Action
                    Action resetAction = propertyName switch
                    {
                        "TabFixedWidthInput" => () => _generalViewModel.TabFixedWidth = _generalViewModel.TabFixedWidth,
                        "TabMaxWidthInput" => () => _generalViewModel.TabMaxWidth = _generalViewModel.TabMaxWidth,
                        "TabMinWidthInput" => () => _generalViewModel.TabMinWidth = _generalViewModel.TabMinWidth,
                        "UIFontSizeInput" => () => _generalViewModel.UIFontSize = _generalViewModel.UIFontSize,
                        "TagFontSizeInput" => () => _generalViewModel.TagFontSize = _generalViewModel.TagFontSize,
                        "TagBoxWidthInput" => () => _generalViewModel.TagBoxWidth = _generalViewModel.TagBoxWidth,
                        _ => null
                    };

                    if (resetAction != null)
                    {
                        _generalViewModel.InvalidateInputProxy(propertyName, resetAction);
                    }
                }
            }

            // 移动焦点以完全“确认”并隐藏光标
            this.Focus();
        }

        private void InitializeState()
        {
            // 宽度策略
            switch (_generalViewModel.TabWidthStrategy)
            {
                case TabWidthStrategy.Fixed:
                    WidthStrategyFixed.IsChecked = true;
                    break;
                case TabWidthStrategy.Adaptive:
                    WidthStrategyAdaptive.IsChecked = true;
                    break;
                case TabWidthStrategy.Elastic:
                    WidthStrategyElastic.IsChecked = true;
                    break;
            }

            // 溢出策略
            if (_generalViewModel.TabOverflowStrategy == TabOverflowStrategy.Compress)
                OverflowStrategyCompress.IsChecked = true;
            else
                OverflowStrategyScroll.IsChecked = true;

            UpdateUI();
        }

        private void WidthStrategy_Checked(object sender, RoutedEventArgs e)
        {
            if (WidthStrategyFixed?.IsChecked == true)
                _generalViewModel.TabWidthStrategy = TabWidthStrategy.Fixed;
            else if (WidthStrategyAdaptive?.IsChecked == true)
                _generalViewModel.TabWidthStrategy = TabWidthStrategy.Adaptive;
            else if (WidthStrategyElastic?.IsChecked == true)
                _generalViewModel.TabWidthStrategy = TabWidthStrategy.Elastic;

            UpdateUI();
        }

        private void OverflowStrategy_Checked(object sender, RoutedEventArgs e)
        {
            if (OverflowStrategyCompress?.IsChecked == true)
                _generalViewModel.TabOverflowStrategy = TabOverflowStrategy.Compress;
            else if (OverflowStrategyScroll?.IsChecked == true)
                _generalViewModel.TabOverflowStrategy = TabOverflowStrategy.Scroll;

            UpdateUI();
        }

        /// <summary>
        /// 根据当前选择的策略组合更新 UI 状态（子面板和提示信息）
        /// </summary>
        private void UpdateUI()
        {
            bool isFixed = _generalViewModel.TabWidthStrategy == TabWidthStrategy.Fixed;
            bool isAdaptive = _generalViewModel.TabWidthStrategy == TabWidthStrategy.Adaptive;
            bool isElastic = _generalViewModel.TabWidthStrategy == TabWidthStrategy.Elastic;

            // 子面板可见性
            if (FixedWidthPanel != null) FixedWidthPanel.Opacity = isFixed ? 1.0 : 0.4;
            if (AdaptiveWidthPanel != null) AdaptiveWidthPanel.Opacity = isAdaptive ? 1.0 : 0.4;
            if (ElasticWidthPanel != null) ElasticWidthPanel.Opacity = isElastic ? 1.0 : 0.4;

            if (TabFixedWidthTextBox != null) TabFixedWidthTextBox.IsEnabled = isFixed;
            if (TabMaxWidthTextBox != null) TabMaxWidthTextBox.IsEnabled = isAdaptive;

            // Elastic 模式下溢出策略被强制为 Compress
            if (isElastic)
            {
                OverflowStrategyScroll.IsEnabled = false;
                OverflowStrategyCompress.IsChecked = true;
                if (OverflowHintText != null)
                    OverflowHintText.Text = "弹性宽度下溢出策略自动设为压缩模式";
            }
            else
            {
                OverflowStrategyScroll.IsEnabled = true;
                if (OverflowHintText != null)
                    OverflowHintText.Text = "";
            }
        }

        private void AdjustValue(double current, double delta, double min, double max, Action<double> setter)
        {
            double newValue = Math.Clamp(current + delta, min, max);
            setter(newValue);
        }

        // 标签页宽度调节
        private void TabFixedWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TabFixedWidth, 10, 80, 250, v => _generalViewModel.TabFixedWidth = v);
        private void TabFixedWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TabFixedWidth, -10, 80, 250, v => _generalViewModel.TabFixedWidth = v);
        private void TabMaxWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TabMaxWidth, 10, 100, 300, v => _generalViewModel.TabMaxWidth = v);
        private void TabMaxWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TabMaxWidth, -10, 100, 300, v => _generalViewModel.TabMaxWidth = v);
        private void TabMinWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TabMinWidth, 5, 30, 100, v => _generalViewModel.TabMinWidth = v);
        private void TabMinWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TabMinWidth, -5, 30, 100, v => _generalViewModel.TabMinWidth = v);

        // 字体调节
        private void UIFontSizeUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.UIFontSize, 1, 10, 48, v => _generalViewModel.UIFontSize = v);
        private void UIFontSizeDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.UIFontSize, -1, 10, 48, v => _generalViewModel.UIFontSize = v);
        private void TagFontSizeUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TagFontSize, 1, 10, 48, v => _generalViewModel.TagFontSize = v);
        private void TagFontSizeDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TagFontSize, -1, 10, 48, v => _generalViewModel.TagFontSize = v);
        private void TagBoxWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TagBoxWidth, 5, 0, 500, v => _generalViewModel.TagBoxWidth = v);
        private void TagBoxWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_generalViewModel.TagBoxWidth, -10, 0, 500, v => _generalViewModel.TagBoxWidth = v);

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
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
