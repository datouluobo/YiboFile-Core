using System;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace YiboFile.Dialogs
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
            this.KeyDown += LibraryDialog_KeyDown;
            this.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed) this.DragMove(); };
        }

        private void LibraryDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && !string.IsNullOrWhiteSpace(LibraryNameTextBox.Text))
            {
                OK_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                Cancel_Click(null, null);
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            // 使用文件夹选择对话框
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择库的文件夹:";
                dialog.ShowNewFolderButton = false;

                // 如果已有路径，从该路径开始浏览
                if (!string.IsNullOrEmpty(LibraryPathTextBox.Text) && Directory.Exists(LibraryPathTextBox.Text))
                {
                    dialog.SelectedPath = LibraryPathTextBox.Text;
                }
                else
                {
                    dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var path = dialog.SelectedPath;
                    LibraryPathTextBox.Text = path;

                    // 如果库名称为空，自动填充文件夹名称
                    if (string.IsNullOrWhiteSpace(LibraryNameTextBox.Text))
                    {
                        LibraryNameTextBox.Text = Path.GetFileName(path) ?? path;
                    }
                }
            }
        }

        public int? LibraryId { get; private set; }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LibraryNameTextBox.Text))
            {
                System.Windows.MessageBox.Show("请输入库名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 路径可选，可以在管理窗口中添加
            if (!string.IsNullOrWhiteSpace(LibraryPathTextBox.Text))
            {
                if (!Directory.Exists(LibraryPathTextBox.Text))
                {
                    System.Windows.MessageBox.Show("指定的路径不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
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

