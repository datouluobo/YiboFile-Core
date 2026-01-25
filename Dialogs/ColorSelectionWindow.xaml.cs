using System;
using System.Windows;
using System.Windows.Media;

namespace YiboFile.Dialogs
{
    /// <summary>
    /// ColorSelectionWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ColorSelectionWindow : Window
    {
        public string SelectedColor { get; private set; } = "#FF0000";

        public ColorSelectionWindow()
        {
            InitializeComponent();
            this.KeyDown += ColorSelectionWindow_KeyDown;
        }

        private void ColorSelectionWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
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
            if (sender is System.Windows.Controls.Button button)
            {
                var brush = button.Background as SolidColorBrush;
                if (brush != null)
                {
                    SelectedColor = brush.Color.ToString();
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
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

