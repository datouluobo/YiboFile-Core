using System;
using System.IO;
using System.Windows;

namespace OoiMRR
{
    /// <summary>
    /// LibraryDialog.xaml 的交互逻辑
    /// </summary>
    public partial class LibraryDialog : Window
    {
        public string LibraryName { get; private set; }
        public string LibraryPath { get; private set; }

        public LibraryDialog()
        {
            InitializeComponent();
            LibraryNameTextBox.Focus();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // 使用简单的输入框让用户输入路径
            var pathDialog = new PathInputDialog("请输入库路径:");
            if (pathDialog.ShowDialog() == true)
            {
                var path = pathDialog.InputText.Trim();
                if (Directory.Exists(path))
                {
                    LibraryPathTextBox.Text = path;
                    if (string.IsNullOrEmpty(LibraryNameTextBox.Text))
                    {
                        LibraryNameTextBox.Text = Path.GetFileName(path);
                    }
                }
                else
                {
                    MessageBox.Show("指定的路径不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LibraryNameTextBox.Text))
            {
                MessageBox.Show("请输入库名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(LibraryPathTextBox.Text))
            {
                MessageBox.Show("请输入库路径", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!Directory.Exists(LibraryPathTextBox.Text))
            {
                MessageBox.Show("指定的路径不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LibraryName = LibraryNameTextBox.Text.Trim();
            LibraryPath = LibraryPathTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
