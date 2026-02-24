using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class LibrarySettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        private LibrarySettingsViewModel _viewModel;

        public LibrarySettingsPanel()
        {
            InitializeComponent();
            _viewModel = new LibrarySettingsViewModel();
            this.DataContext = _viewModel;
        }

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "导入库配置"
            };
            if (dialog.ShowDialog() == true && _viewModel.ImportLibrariesCommand.CanExecute(dialog.FileName))
            {
                _viewModel.ImportLibrariesCommand.Execute(dialog.FileName);
            }
        }

        private void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                FileName = $"Libraries_Backup_{DateTime.Now:yyyyMMdd}.json",
                Title = "导出库配置"
            };
            if (dialog.ShowDialog() == true && _viewModel.ExportLibrariesCommand.CanExecute(dialog.FileName))
            {
                _viewModel.ExportLibrariesCommand.Execute(dialog.FileName);
                MessageBox.Show("库配置已导出", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
            _viewModel?.RefreshLibraries();
        }

        public void SaveSettings() { }
    }
}
