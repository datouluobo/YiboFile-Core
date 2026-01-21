using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.ViewModels;

namespace YiboFile.Controls.Settings
{
    public partial class HotkeySettingsPanel : UserControl, ISettingsPanel
    {
        // Event reserved for future use
#pragma warning disable CS0067
        public event EventHandler SettingsChanged;
#pragma warning restore CS0067

        private SettingsViewModel _viewModel;

        public HotkeySettingsPanel()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            this.DataContext = _viewModel;
            // LoadSettings will be called by Interface or initial binding
        }

        private void InitializeComponent()
        {
            var grid = new Grid { Margin = new Thickness(30) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Title
            var titlePanel = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
            titlePanel.Children.Add(new TextBlock
            {
                Text = "快捷键管理",
                FontSize = 28,
                FontWeight = FontWeights.Bold
            });
            ((TextBlock)titlePanel.Children[0]).SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            titlePanel.Children.Add(new TextBlock
            {
                Text = "查看和自定义应用程序快捷键",
                FontSize = 14,
                Margin = new Thickness(0, 8, 0, 0)
            });
            ((TextBlock)titlePanel.Children[1]).SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            // Action Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            var resetBtn = new Button
            {
                Content = "恢复默认",
                Padding = new Thickness(16, 8, 16, 8),
                MinWidth = 100,
                Cursor = Cursors.Hand
            };
            resetBtn.SetBinding(Button.CommandProperty, new Binding(nameof(SettingsViewModel.ResetHotkeysCommand)));
            btnPanel.Children.Add(resetBtn);
            Grid.SetRow(btnPanel, 1);
            grid.Children.Add(btnPanel);

            // DataGrid
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                CanUserResizeRows = false,
                CanUserSortColumns = true,
                RowHeaderWidth = 0,
                GridLinesVisibility = DataGridGridLinesVisibility.None,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow
            };
            dataGrid.SetBinding(DataGrid.ItemsSourceProperty, new Binding(nameof(SettingsViewModel.Hotkeys)));

            // Columns
            // Description
            var descCol = new DataGridTextColumn
            {
                Header = "功能描述",
                Binding = new Binding(nameof(HotkeyItemViewModel.Description)),
                IsReadOnly = true,
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            };
            dataGrid.Columns.Add(descCol);

            // Key
            var keyCol = new DataGridTextColumn
            {
                Header = "快捷键",
                Binding = new Binding(nameof(HotkeyItemViewModel.KeyCombination)),
                IsReadOnly = true,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };
            dataGrid.Columns.Add(keyCol);

            // Actions
            var actionCol = new DataGridTemplateColumn
            {
                Header = "操作",
                Width = DataGridLength.Auto
            };

            // Create DataTemplate for Actions using XamlReader or FrameworkElementFactory
            // Using FrameworkElementFactory for code-behind template
            var stackFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            // Edit Button
            var editBtnFactory = new FrameworkElementFactory(typeof(Button));
            editBtnFactory.SetValue(Button.ContentProperty, "编辑");
            editBtnFactory.SetValue(Button.PaddingProperty, new Thickness(12, 6, 12, 6));
            editBtnFactory.SetValue(Button.MarginProperty, new Thickness(0, 0, 8, 0));
            editBtnFactory.SetValue(Button.CursorProperty, Cursors.Hand);
            editBtnFactory.SetBinding(Button.TagProperty, new Binding(".")); // Bind whole object
            editBtnFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(EditHotkey_Click));
            stackFactory.AppendChild(editBtnFactory);

            // Reset Single Button
            var resetSingleBtnFactory = new FrameworkElementFactory(typeof(Button));
            resetSingleBtnFactory.SetValue(Button.ContentProperty, "恢复");
            resetSingleBtnFactory.SetValue(Button.PaddingProperty, new Thickness(8, 6, 8, 6));
            resetSingleBtnFactory.SetValue(Button.CursorProperty, Cursors.Hand);
            // resetSingleBtnFactory.SetValue(Button.ForegroundProperty, Brushes.Red); // Optional simplified styling
            resetSingleBtnFactory.SetBinding(Button.CommandProperty, new Binding("DataContext.ResetSingleHotkeyCommand") { Source = this }); // Access ViewModel command
            // Binding CommandParameter to the Item
            resetSingleBtnFactory.SetBinding(Button.CommandParameterProperty, new Binding("."));

            // Visibility binding for Reset button
            var visibilityBinding = new Binding(nameof(HotkeyItemViewModel.IsModified));
            visibilityBinding.Converter = new BooleanToVisibilityConverter();
            resetSingleBtnFactory.SetBinding(Button.VisibilityProperty, visibilityBinding);

            stackFactory.AppendChild(resetSingleBtnFactory);

            actionCol.CellTemplate = new DataTemplate { VisualTree = stackFactory };
            dataGrid.Columns.Add(actionCol);

            // Container for DataGrid to simulate Border
            var border = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = dataGrid
            };
            border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            border.SetResourceReference(Border.BackgroundProperty, "CardBackgroundBrush");
            Grid.SetRow(border, 2);
            grid.Children.Add(border);

            // Footer
            var footer = new TextBlock
            {
                Text = "点击编辑按钮可自定义快捷键。修改会立即保存。",
                FontSize = 12,
                Margin = new Thickness(0, 15, 0, 0)
            };
            footer.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            Grid.SetRow(footer, 3);
            grid.Children.Add(footer);

            this.Content = grid;
        }

        private void EditHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HotkeyItemViewModel item)
            {
                // Create input dialog (similar to original code)
                var dialog = new Window
                {
                    Title = "编辑快捷键",
                    Width = 350,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    ResizeMode = ResizeMode.NoResize
                };

                var grid = new Grid { Margin = new Thickness(20) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var descText = new TextBlock
                {
                    Text = $"功能: {item.Description}",
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(descText, 0);
                grid.Children.Add(descText);

                var label = new TextBlock
                {
                    Text = "新快捷键:",
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                Grid.SetRow(label, 1);
                grid.Children.Add(label);

                var textBox = new TextBox
                {
                    Text = item.KeyCombination,
                    FontSize = 13,
                    Padding = new Thickness(8),
                    IsReadOnly = true,
                    Background = Brushes.WhiteSmoke
                };
                Grid.SetRow(textBox, 2);
                grid.Children.Add(textBox);

                // Keyboard listener
                textBox.PreviewKeyDown += (s, args) =>
                {
                    args.Handled = true;
                    var key = args.Key;
                    var modifiers = Keyboard.Modifiers;

                    if (key == Key.System) key = args.SystemKey;

                    // Build string
                    var keys = new System.Collections.Generic.List<string>();
                    if (modifiers.HasFlag(ModifierKeys.Control)) keys.Add("Ctrl");
                    if (modifiers.HasFlag(ModifierKeys.Alt)) keys.Add("Alt");
                    if (modifiers.HasFlag(ModifierKeys.Shift)) keys.Add("Shift");
                    if (modifiers.HasFlag(ModifierKeys.Windows)) keys.Add("Win");

                    // Add main key
                    if (key != Key.LeftCtrl && key != Key.RightCtrl &&
                        key != Key.LeftAlt && key != Key.RightAlt &&
                        key != Key.LeftShift && key != Key.RightShift &&
                        key != Key.LWin && key != Key.RWin)
                    {
                        keys.Add(key.ToString());
                    }

                    if (keys.Count > 0)
                    {
                        textBox.Text = string.Join("+", keys);
                    }
                };

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 15, 0, 0)
                };
                Grid.SetRow(buttonPanel, 3);
                grid.Children.Add(buttonPanel);

                var saveBtn = new Button
                {
                    Content = "保存",
                    Padding = new Thickness(20, 8, 20, 8),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = Cursors.Hand
                };
                saveBtn.Click += (s, args) =>
                {
                    // Update ViewModel
                    item.KeyCombination = textBox.Text;
                    dialog.DialogResult = true;
                };
                buttonPanel.Children.Add(saveBtn);

                var cancelBtn = new Button
                {
                    Content = "取消",
                    Padding = new Thickness(20, 8, 20, 8),
                    Cursor = Cursors.Hand
                };
                cancelBtn.Click += (s, args) =>
                {
                    dialog.DialogResult = false;
                };
                buttonPanel.Children.Add(cancelBtn);

                dialog.Content = grid;
                dialog.ShowDialog();
            }
        }

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
        }

        public void SaveSettings()
        {
            // Auto-saved by command interactions or property changes
        }
    }
}
