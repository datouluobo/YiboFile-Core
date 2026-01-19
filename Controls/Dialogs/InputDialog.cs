using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace YiboFile.Controls.Dialogs
{
    public class InputDialog : Window
    {
        public string InputText { get; private set; }
        private TextBox _textBox;

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 350;
            Height = 160;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = System.Windows.Media.Brushes.White;

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            grid.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10), FontWeight = FontWeights.SemiBold });

            _textBox = new TextBox { Text = defaultValue, Margin = new Thickness(0, 0, 0, 15), Padding = new Thickness(5) };
            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "确定", Width = 80, Height = 30, IsDefault = true, Margin = new Thickness(0, 0, 10, 0) };
            okBtn.Click += (s, e) => { InputText = _textBox.Text; DialogResult = true; };

            var cancelBtn = new Button { Content = "取消", Width = 80, Height = 30, IsCancel = true };

            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            Content = grid;

            _textBox.Focus();
            _textBox.SelectAll();

            // Handle Enter key
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { InputText = _textBox.Text; DialogResult = true; }
                if (e.Key == Key.Escape) { DialogResult = false; }
            };
        }
    }
}
