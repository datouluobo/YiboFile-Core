using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace OoiMRR
{
    /// <summary>
    /// TagDialog.xaml 的交互逻辑
    /// </summary>
    public partial class TagDialog : Window
    {
        public string TagName { get; private set; }
        public string TagColor { get; private set; } = "#FF0000";

        public TagDialog()
        {
            InitializeComponent();
            TagNameTextBox.Focus();
            this.KeyDown += TagDialog_KeyDown;
        }

        private void TagDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && !string.IsNullOrWhiteSpace(TagNameTextBox.Text))
            {
                OK_Click(null, null);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                Cancel_Click(null, null);
            }
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            var colorDialog = new Microsoft.Win32.OpenFileDialog();
            // 使用简单的颜色选择
            var colorWindow = new ColorSelectionWindow();
            if (colorWindow.ShowDialog() == true)
            {
                TagColor = colorWindow.SelectedColor;
                ColorPreview.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(TagColor));
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TagNameTextBox.Text))
            {
                MessageBox.Show("请输入标签名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TagName = TagNameTextBox.Text.Trim();
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
