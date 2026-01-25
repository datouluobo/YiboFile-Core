using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YiboFile.Controls.Dialogs
{
    public class InputDialog : Window
    {
        public string InputText { get; private set; }
        private TextBox _textBox;

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            // 应用统一的对话框样式
            Style = (Style)Application.Current.TryFindResource("BaseDialogWindowStyle");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 提示文本
            var promptLabel = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 15),
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };
            promptLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundPrimaryBrush");
            grid.Children.Add(promptLabel);

            // 输入框
            _textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 25),
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 13
            };
            _textBox.SetResourceReference(TextBox.BackgroundProperty, "BackgroundPrimaryBrush");
            _textBox.SetResourceReference(TextBox.ForegroundProperty, "ForegroundPrimaryBrush");
            _textBox.SetResourceReference(TextBox.BorderBrushProperty, "BorderDefaultBrush");
            _textBox.BorderThickness = new Thickness(1);

            Grid.SetRow(_textBox, 1);
            grid.Children.Add(_textBox);

            // 按钮面板
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var cancelBtn = new Button
            {
                Content = "取消",
                IsCancel = true
            };
            cancelBtn.SetResourceReference(Button.StyleProperty, "DialogButtonStyle");
            cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

            var okBtn = new Button
            {
                Content = "确定",
                IsDefault = true
            };
            okBtn.SetResourceReference(Button.StyleProperty, "PrimaryDialogButtonStyle");
            okBtn.Click += (s, e) => { InputText = _textBox.Text; DialogResult = true; Close(); };

            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            // 关键修复：显式设置透明背景，否则 Grid 空白处无法响应点击
            grid.Background = Brushes.Transparent;

            Content = grid;

            _textBox.Focus();
            _textBox.SelectAll();

            // 允许拖动窗口 (在窗口级别监听以确保覆盖所有非交互区域)
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { this.DragMove(); } catch { }
                }
            };

            // Handle keys
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    InputText = _textBox.Text;
                    DialogResult = true;
                    Close();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                    e.Handled = true;
                }
            };

        }
    }
}
