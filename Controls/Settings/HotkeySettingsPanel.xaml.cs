using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Effects; // Added
using YiboFile.ViewModels;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class HotkeySettingsPanel : UserControl, ISettingsPanel
    {
        // Event reserved for future use
#pragma warning disable CS0067
        public event EventHandler SettingsChanged;
#pragma warning restore CS0067

        private HotkeySettingsViewModel _viewModel;

        public HotkeySettingsPanel()
        {
            InitializeComponent();
            _viewModel = new HotkeySettingsViewModel();
            this.DataContext = _viewModel;
            // LoadSettings will be called by Interface or initial binding
        }

        private void InitializeComponent()
        {
            var grid = new Grid { Margin = new Thickness(0) };
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
            resetBtn.SetBinding(Button.CommandProperty, new Binding(nameof(HotkeySettingsViewModel.ResetHotkeysCommand)));
            btnPanel.Children.Add(resetBtn);
            Grid.SetRow(btnPanel, 1);
            grid.Children.Add(btnPanel);

            // Scrollable ItemsControl for two-column layout
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var itemsControl = new ItemsControl();
            itemsControl.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(HotkeySettingsViewModel.Hotkeys)));

            // Horizontal spacing between columns
            var itemsPanelFactory = new FrameworkElementFactory(typeof(UniformGrid));
            itemsPanelFactory.SetValue(UniformGrid.ColumnsProperty, 2);
            itemsControl.ItemsPanel = new ItemsPanelTemplate(itemsPanelFactory);

            // Item Template
            var itemTemplate = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.MarginProperty, new Thickness(15, 6, 15, 6)); // 进一步增大间距
            factory.SetValue(Border.PaddingProperty, new Thickness(12));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            factory.SetResourceReference(Border.BackgroundProperty, "SurfaceBrush");
            factory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            factory.SetResourceReference(Border.BorderBrushProperty, "BorderLightBrush");

            // 悬停效果模拟 - 通过 Style 定义
            var borderStyle = new Style(typeof(Border));
            var trigger = new Trigger { Property = Border.IsMouseOverProperty, Value = true };
            // 这里使用 DynamicResource 绑定的 Setter 在后台代码中很难直接实现，
            // 故使用固定颜色或在 XAML 中定义，但既然是后台生成，我们尽量找一个通用的方式。
            trigger.Setters.Add(new Setter(Border.BorderBrushProperty, Brushes.SkyBlue));
            borderStyle.Triggers.Add(trigger);
            factory.SetValue(Border.StyleProperty, borderStyle);

            var itemGrid = new FrameworkElementFactory(typeof(Grid));
            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            var col3 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col3.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);

            itemGrid.AppendChild(col1);
            itemGrid.AppendChild(col2);
            itemGrid.AppendChild(col3);

            // Description
            var descText = new FrameworkElementFactory(typeof(TextBlock));
            descText.SetBinding(TextBlock.TextProperty, new Binding("Description"));
            descText.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            descText.SetValue(TextBlock.FontSizeProperty, 14.0);
            descText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            descText.SetValue(Grid.ColumnProperty, 0);
            itemGrid.AppendChild(descText);

            // Key Combination (Badge style)
            var keyBorder = new FrameworkElementFactory(typeof(Border));
            keyBorder.SetResourceReference(Border.BackgroundProperty, "AccentLightBrush");
            keyBorder.SetResourceReference(Border.BorderBrushProperty, "AccentDefaultBrush"); // 增加边框
            keyBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            keyBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            keyBorder.SetValue(Border.PaddingProperty, new Thickness(8, 2, 8, 2));
            keyBorder.SetValue(Border.MarginProperty, new Thickness(10, 0, 10, 0));
            keyBorder.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
            keyBorder.SetValue(Border.SnapsToDevicePixelsProperty, true); // 开启对齐
            keyBorder.SetValue(Grid.ColumnProperty, 1);

            var keyText = new FrameworkElementFactory(typeof(TextBlock));
            keyText.SetBinding(TextBlock.TextProperty, new Binding("KeyCombination"));
            keyText.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Consolas, Segoe UI"));
            keyText.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            keyText.SetResourceReference(TextBlock.ForegroundProperty, "AccentDefaultBrush");
            keyBorder.AppendChild(keyText);
            itemGrid.AppendChild(keyBorder);

            // Button Panel
            var btns = new FrameworkElementFactory(typeof(StackPanel));
            btns.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            btns.SetValue(Grid.ColumnProperty, 2);

            var editBtn = new FrameworkElementFactory(typeof(Button));
            editBtn.SetValue(Button.ContentProperty, "编辑");
            editBtn.SetValue(Button.MarginProperty, new Thickness(5, 0, 0, 0));
            editBtn.SetValue(Button.PaddingProperty, new Thickness(12, 4, 12, 4));
            editBtn.SetBinding(Button.TagProperty, new Binding("."));
            editBtn.AddHandler(Button.ClickEvent, new RoutedEventHandler(EditHotkey_Click));
            btns.AppendChild(editBtn);

            var resetBtnItem = new FrameworkElementFactory(typeof(Button));
            resetBtnItem.SetValue(Button.ContentProperty, "恢复");
            resetBtnItem.SetValue(Button.MarginProperty, new Thickness(8, 0, 0, 0));
            resetBtnItem.SetValue(Button.PaddingProperty, new Thickness(12, 4, 12, 4));
            resetBtnItem.SetBinding(Button.TagProperty, new Binding("."));
            resetBtnItem.AddHandler(Button.ClickEvent, new RoutedEventHandler(ResetHotkey_Click));
            var visBinding = new Binding(nameof(HotkeyItemViewModel.IsModified)) { Converter = new BooleanToVisibilityConverter() };
            resetBtnItem.SetBinding(Button.VisibilityProperty, visBinding);
            btns.AppendChild(resetBtnItem);

            itemGrid.AppendChild(btns);
            factory.AppendChild(itemGrid);
            itemTemplate.VisualTree = factory;
            itemsControl.ItemTemplate = itemTemplate;

            // Wrap ScrollViewer and vertical line in a Grid
            var listContainer = new Grid();
            // 不再使用多列定义，直接在单列中居中放置线，确保其位于 itemsControl 两列的正中间

            scrollViewer.Content = itemsControl;
            listContainer.Children.Add(scrollViewer);

            // Vertical separator line
            var separator = new Border
            {
                Width = 1,
                Background = Brushes.LightGray, // 使用更确定的灰色
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10),
                SnapsToDevicePixels = true,
                IsHitTestVisible = false
            };
            listContainer.Children.Add(separator);

            Grid.SetRow(listContainer, 2);
            grid.Children.Add(listContainer);

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
                var dialog = new Window
                {
                    Title = "编辑快捷键",
                    Width = 380, // 增加宽度
                    Height = 280, // 增加高度
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this),
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ShowInTaskbar = false
                };

                // 主容器，增加内边距以容纳阴影
                var mainBorder = new Border
                {
                    Background = (Brush)Application.Current.TryFindResource("CardBackgroundBrush"),
                    CornerRadius = new CornerRadius(12),
                    BorderThickness = new Thickness(1),
                    BorderBrush = (Brush)Application.Current.TryFindResource("BorderBrush"),
                    Margin = new Thickness(15), // 为阴影预留充足空间
                    UseLayoutRounding = true,   // 开启布局舍入
                    SnapsToDevicePixels = true, // 开启像素对齐
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 12,
                        ShadowDepth = 2,
                        Opacity = 0.25
                    }
                };

                var grid = new Grid { Margin = new Thickness(24) };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var headerText = new TextBlock
                {
                    Text = "设置快捷键",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 15)
                };
                headerText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                Grid.SetRow(headerText, 0);
                grid.Children.Add(headerText);

                var descText = new TextBlock
                {
                    Text = $"当前功能: {item.Description}",
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 20) // 增加底部边距
                };
                descText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                Grid.SetRow(descText, 1);
                grid.Children.Add(descText);

                // Recording Visual
                var displayBorder = new Border
                {
                    BorderThickness = new Thickness(2), // 增加到 2 像素以保证各边一致且醒目
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(15),
                    Background = (Brush)Application.Current.TryFindResource("AppBackgroundBrush"),
                    BorderBrush = (Brush)Application.Current.TryFindResource("AccentDefaultBrush"),
                    SnapsToDevicePixels = true,
                    UseLayoutRounding = true,
                    Height = 70
                };

                var keyDisplayText = new TextBlock
                {
                    Text = string.IsNullOrEmpty(item.KeyCombination) ? "请按下组合键..." : item.KeyCombination,
                    FontSize = 24, // 恢复原来较大的号
                    FontWeight = FontWeights.Bold, // 使用 Bold
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                keyDisplayText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
                displayBorder.Child = keyDisplayText;
                Grid.SetRow(displayBorder, 2);
                grid.Children.Add(displayBorder);

                bool hasMainKey = !string.IsNullOrEmpty(item.KeyCombination) && !item.KeyCombination.EndsWith("...");

                dialog.PreviewKeyDown += (s, args) =>
                {
                    args.Handled = true;
                    var key = args.Key == Key.System ? args.SystemKey : args.Key;
                    var modifiers = Keyboard.Modifiers;

                    // Finish (Enter) or Cancel (Esc)
                    if (modifiers == ModifierKeys.None)
                    {
                        if (key == Key.Enter && hasMainKey)
                        {
                            item.KeyCombination = keyDisplayText.Text;
                            dialog.DialogResult = true;
                            return;
                        }
                        if (key == Key.Escape)
                        {
                            dialog.DialogResult = false;
                            return;
                        }
                    }

                    // Build string
                    var parts = new System.Collections.Generic.List<string>();
                    if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
                    if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
                    if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
                    if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");

                    bool isModifier = key == Key.LeftCtrl || key == Key.RightCtrl ||
                                     key == Key.LeftAlt || key == Key.RightAlt ||
                                     key == Key.LeftShift || key == Key.RightShift ||
                                     key == Key.LWin || key == Key.RWin;

                    if (!isModifier)
                    {
                        var keyStr = key.ToString();

                        // 映射修正
                        if (key >= Key.D0 && key <= Key.D9) keyStr = (key - Key.D0).ToString();
                        else if (key >= Key.NumPad0 && key <= Key.NumPad9) keyStr = (key - Key.NumPad0).ToString();
                        else if (key == Key.OemPlus) keyStr = "=";
                        else if (key == Key.OemMinus) keyStr = "-";
                        else if (key == Key.OemPeriod) keyStr = ".";
                        else if (key == Key.OemComma) keyStr = ",";

                        parts.Add(keyStr);
                        hasMainKey = true;
                    }
                    else
                    {
                        parts.Add("...");
                        hasMainKey = false;
                    }

                    keyDisplayText.Text = string.Join("+", parts);
                };

                // 移除手动文字渲染模式设置，采用默认渲染以获得最佳一致性

                // Focus enforcement
                dialog.Loaded += (s, a) => { dialog.Activate(); dialog.Focus(); Keyboard.Focus(dialog); };
                dialog.ContentRendered += (s, a) => Keyboard.Focus(dialog);
                dialog.MouseDown += (s, a) => dialog.Focus();

                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
                Grid.SetRow(buttonPanel, 3);
                grid.Children.Add(buttonPanel);

                var saveBtn = new Button { Content = "完成", Padding = new Thickness(20, 6, 20, 6), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
                saveBtn.Click += (s, a) => { if (hasMainKey) { item.KeyCombination = keyDisplayText.Text; dialog.DialogResult = true; } };
                buttonPanel.Children.Add(saveBtn);

                var cancelBtn = new Button { Content = "取消", Padding = new Thickness(20, 6, 20, 6), Cursor = Cursors.Hand };
                cancelBtn.Click += (s, a) => dialog.DialogResult = false;
                buttonPanel.Children.Add(cancelBtn);

                mainBorder.Child = grid;
                dialog.Content = mainBorder;
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
        private void ResetHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HotkeyItemViewModel item)
            {
                if (DataContext is HotkeySettingsViewModel viewModel)
                {
                    viewModel.ResetSingleHotkeyCommand.Execute(item);
                }
            }
        }
    }
}
