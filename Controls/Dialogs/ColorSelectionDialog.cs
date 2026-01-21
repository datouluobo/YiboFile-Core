using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YiboFile.Controls.Dialogs
{
    public class ColorSelectionDialog : Window
    {
        public string SelectedColor { get; private set; }

        private TextBox _colorCodeInput;
        private readonly string[] _presetColors = new[]
        {
             "#FFCDD2", "#F8BBD0", "#E1BEE7", "#D1C4E9", "#C5CAE9",
             "#BBDEFB", "#B3E5FC", "#B2EBF2", "#B2DFDB", "#C8E6C9",
             "#DCEDC8", "#F0F4C3", "#FFF9C4", "#FFECB3", "#FFE0B2",
             "#FFCCBC", "#D7CCC8", "#F5F5F5", "#CFD8DC", "#FFFFFF",
             "#E0E0E0", "#9E9E9E", "#616161", "#212121", "#F44336",
             "#E91E63", "#9C27B0", "#673AB7", "#3F51B5", "#2196F3",
             "#00BCD4", "#009688", "#4CAF50", "#8BC34A", "#FFEB3B",
             "#FFC107", "#FF9800", "#FF5722", "#795548", "#607D8B"
        };

        public ColorSelectionDialog(string initialColor = null)
        {
            SelectedColor = initialColor;
            InitializeUI();
        }

        private void InitializeUI()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Title = "选择颜色";
            Width = 400;
            Height = 450;
            ResizeMode = ResizeMode.NoResize;
            // WindowStyle = WindowStyle.ToolWindow; // Removed to fix close button position
            Background = (Brush)Application.Current.TryFindResource("WindowBackgroundBrush") ?? Brushes.White;

            var mainGrid = new Grid { Margin = new Thickness(20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Preset Title
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Presets
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Input
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // 1. Preset Title
            var title = new TextBlock
            {
                Text = "预设颜色",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            mainGrid.Children.Add(title);

            // 2. Presets Grid
            var presetScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var presetPanel = new WrapPanel { Orientation = Orientation.Horizontal };

            foreach (var colorCode in _presetColors)
            {
                var btn = new Button
                {
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 10),
                    Background = (Brush)new BrushConverter().ConvertFrom(colorCode),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Gray,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = colorCode
                };
                btn.Click += (s, e) =>
                {
                    _colorCodeInput.Text = colorCode;
                    SelectedColor = colorCode;
                };
                presetPanel.Children.Add(btn);
            }
            presetScroll.Content = presetPanel;
            Grid.SetRow(presetScroll, 1);
            mainGrid.Children.Add(presetScroll);

            // 3. Input
            var inputStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 15, 0, 20) };
            var label = new TextBlock { Text = "颜色代码 (Hex):", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");

            _colorCodeInput = new TextBox { Width = 150, Text = SelectedColor ?? "" };
            _colorCodeInput.TextChanged += (s, e) => SelectedColor = _colorCodeInput.Text;

            inputStack.Children.Add(label);
            inputStack.Children.Add(_colorCodeInput);
            Grid.SetRow(inputStack, 2);
            mainGrid.Children.Add(inputStack);

            // 4. Buttons
            var btnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okBtn = new Button
            {
                Content = "确定",
                IsDefault = true,
                Width = 80,
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(5)
            };
            okBtn.Click += (s, e) => { DialogResult = true; Close(); };

            var cancelBtn = new Button
            {
                Content = "取消",
                IsCancel = true,
                Width = 80,
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(5)
            };

            btnStack.Children.Add(cancelBtn);
            btnStack.Children.Add(okBtn);

            Grid.SetRow(btnStack, 3);
            mainGrid.Children.Add(btnStack);

            Content = mainGrid;
        }
    }
}
