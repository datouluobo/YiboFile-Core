using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using YiboFile.ViewModels;

namespace YiboFile.Controls.Settings
{
    public partial class LibrarySettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private SettingsViewModel _viewModel;

        public LibrarySettingsPanel()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            this.DataContext = _viewModel;

            _viewModel.OpenLibraryManagerRequested += OnOpenLibraryManagerRequested;
        }

        private void InitializeComponent()
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0) };

            var titleText = new TextBlock
            {
                Text = "库设置",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(titleText);

            var infoText = new TextBlock
            {
                Text = "在此处管理您的库配置，可以导入或导出库设置以进行备份或迁移。",
                Foreground = System.Windows.Media.Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };
            stackPanel.Children.Add(infoText);

            // 管理库按钮
            var manageBtn = new Button
            {
                Content = "管理库",
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 0, 10),
                MinWidth = 120
            };
            manageBtn.SetBinding(Button.CommandProperty, new Binding(nameof(SettingsViewModel.OpenLibraryManagerCommand)));
            stackPanel.Children.Add(manageBtn);

            // 导入库按钮
            var importBtn = new Button
            {
                Content = "导入库配置",
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 0, 10),
                MinWidth = 120
            };
            importBtn.Click += ImportBtn_Click; // Handle Click to open dialog, then Command
            stackPanel.Children.Add(importBtn);

            // 导出库按钮
            var exportBtn = new Button
            {
                Content = "导出库配置",
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(15, 8, 15, 8),
                MinWidth = 120
            };
            exportBtn.Click += ExportBtn_Click; // Handle Click to open dialog, then Command
            stackPanel.Children.Add(exportBtn);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel
            };

            Content = scrollViewer;
        }

        private void OnOpenLibraryManagerRequested(object sender, EventArgs e)
        {
            var win = new YiboFile.Windows.NavigationSettingsWindow("Library");
            win.Owner = Window.GetWindow(this);
            win.ShowDialog();
        }

        private void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON 文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                Title = "导入库配置"
            };

            if (dialog.ShowDialog() == true)
            {
                // Call Command
                if (_viewModel.ImportLibrariesCommand.CanExecute(dialog.FileName))
                {
                    _viewModel.ImportLibrariesCommand.Execute(dialog.FileName);
                }
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

            if (dialog.ShowDialog() == true)
            {
                // Call Command
                if (_viewModel.ExportLibrariesCommand.CanExecute(dialog.FileName))
                {
                    _viewModel.ExportLibrariesCommand.Execute(dialog.FileName);
                    MessageBox.Show("库配置已导出", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
        }

        public void SaveSettings()
        {
            // Auto-saved by Command actions
        }
    }
}
