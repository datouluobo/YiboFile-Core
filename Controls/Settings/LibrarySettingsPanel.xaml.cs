using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controls;

namespace YiboFile.Controls.Settings
{
#pragma warning disable CS0067 // 事件从未使用，但接口要求
    public partial class LibrarySettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
#pragma warning restore CS0067

        public LibrarySettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
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
            manageBtn.Click += (s, e) =>
            {
                var win = new LibraryManagementWindow();
                win.Owner = Window.GetWindow(this);
                win.ShowDialog();
            };
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
            importBtn.Click += ImportBtn_Click;
            stackPanel.Children.Add(importBtn);

            // 导出库按钮
            var exportBtn = new Button
            {
                Content = "导出库配置",
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(15, 8, 15, 8),
                MinWidth = 120
            };
            exportBtn.Click += ExportBtn_Click;
            stackPanel.Children.Add(exportBtn);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel
            };

            Content = scrollViewer;
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
                try
                {
                    string json = System.IO.File.ReadAllText(dialog.FileName);
                    var libraryService = new YiboFile.Services.LibraryService(Dispatcher, null);
                    libraryService.ImportLibrariesFromJson(json);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"读取文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
                try
                {
                    var libraryService = new YiboFile.Services.LibraryService(Dispatcher, null);
                    string json = libraryService.ExportLibrariesToJson();
                    if (!string.IsNullOrEmpty(json))
                    {
                        System.IO.File.WriteAllText(dialog.FileName, json);
                        MessageBox.Show("库配置已导出", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void LoadSettings()
        {
            // 加载库设置
        }

        public void SaveSettings()
        {
            // 保存库设置
        }
    }
}


