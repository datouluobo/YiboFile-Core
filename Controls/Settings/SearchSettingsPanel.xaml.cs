using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Services.Features;
using System.Windows.Input;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class SearchSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        private SearchSettingsViewModel _viewModel;

        public SearchSettingsPanel()
        {
            InitializeComponent();
            var configService = (YiboFile.Services.Config.IConfigurationService)YiboFile.App.ServiceProvider.GetService(typeof(YiboFile.Services.Config.IConfigurationService));
            _viewModel = new SearchSettingsViewModel(configService);
            this.DataContext = _viewModel;

            EverythingVersionText.Text = YiboFile.Services.EverythingHelper.GetVersion();
            LoadSupportedExtensions();
        }

        private void RebuildEverythingButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MessageBox.Show("确定要强制重建 Everything 索引吗？这可能会触发 UAC 提示。", "重建索引", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    YiboFile.Services.EverythingHelper.ForceRebuildIndex();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ChangeIndexLocationButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "选择索引数据库位置",
                Filter = "SQLite 数据库 (*.db)|*.db",
                FileName = "fts_index.db",
                DefaultExt = ".db"
            };

            if (dialog.ShowDialog() == true)
            {
                _viewModel.UpdateIndexLocation(dialog.FileName);
                MessageBox.Show("索引位置已更新。请重启应用以生效。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要删除所有本地搜索和路径历史记录吗？", "清除历史", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _viewModel.ClearHistoryCommand.Execute(null);
                MessageBox.Show("历史记录已清除。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void AddScopeButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = dialog.SelectedPath;
                if (!_viewModel.IndexScopes.Contains(path))
                {
                    _viewModel.IndexScopes.Add(path);
                    _viewModel.UpdateIndexScopes(_viewModel.IndexScopes);
                    _viewModel?.StartBackgroundIndexing();
                }
            }
        }

        private void RemoveScopeButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ScopeListBox.SelectedItems.Cast<string>().ToList();
            if (selectedItems.Count > 0)
            {
                foreach (var item in selectedItems) _viewModel.IndexScopes.Remove(item);
                _viewModel.UpdateIndexScopes(_viewModel.IndexScopes);
            }
        }

        private void RebuildIndexButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.IsIndexing) return;

            if (MessageBox.Show("确信要清空并重建索引吗？此操作不可撤销。", "重建索引", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            _viewModel.RebuildIndexCommand.Execute(null);
            MessageBox.Show("索引已清空。后台任务将自动开始重新扫描您的文件（请确保应用保持运行）。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void LoadSupportedExtensions()
        {
            var formats = new[]
            {
                new { Ext = ".txt", Desc = "纯文本文档" },
                new { Ext = ".md", Desc = "Markdown 文档" },
                new { Ext = ".pdf", Desc = "PDF 文档 (PdfPig)" },
                new { Ext = ".docx", Desc = "Word 文档 (OpenXML)" },
                new { Ext = ".doc", Desc = "Word 97-2003 (NPOI)" },
                new { Ext = ".xlsx", Desc = "Excel 工作簿 (OpenXML)" },
                new { Ext = ".xls", Desc = "Excel 97-2003 (NPOI)" },
                new { Ext = ".cpp/.c/.h/.cs/.java/.py/.js/.ts/.html/.css/.xml/.json/.sql", Desc = "代码源文件" }
            };

            foreach (var fmt in formats)
            {
                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var extBlock = new TextBlock { Text = fmt.Ext, FontWeight = FontWeights.Bold };
                Grid.SetColumn(extBlock, 0);
                row.Children.Add(extBlock);

                var descBlock = new TextBlock { Text = fmt.Desc };
                Grid.SetColumn(descBlock, 1);
                row.Children.Add(descBlock);

                ExtensionsPanel.Children.Add(row);
            }
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

            if (int.TryParse(textBox.Text, out int value))
            {
                switch (propertyName)
                {
                    case "HistoryMaxCountInput":
                        _viewModel.HistoryMaxCount = value;
                        break;
                }
            }
            _viewModel.InvalidateInputProxy(propertyName);

            this.Focus();
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }
    }
}
