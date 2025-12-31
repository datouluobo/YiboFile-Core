using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input; // Added for ModifierKeys, Key, Cursors

namespace OoiMRR.Controls.Settings
{
    public class HotkeyItem
    {
        public string Description { get; set; }
        public string KeyCombination { get; set; }
    }

    public partial class HotkeySettingsPanel : UserControl
    {
        private ObservableCollection<HotkeyItem> _hotkeys;
        private ObservableCollection<HotkeyItem> _defaultHotkeys; // Added this line

        public HotkeySettingsPanel()
        {
            InitializeComponent();
            LoadHotkeys();
        }

        private void LoadHotkeys()
        {
            // 默认快捷键列表
            _defaultHotkeys = new ObservableCollection<HotkeyItem>
            {
                new HotkeyItem { Description = "新建标签页", KeyCombination = "Ctrl+T" },
                new HotkeyItem { Description = "关闭标签页", KeyCombination = "Ctrl+W" },
                new HotkeyItem { Description = "下一个标签", KeyCombination = "Ctrl+Tab" },
                new HotkeyItem { Description = "上一个标签", KeyCombination = "Ctrl+Shift+Tab" },
                new HotkeyItem { Description = "刷新", KeyCombination = "F5" },
                new HotkeyItem { Description = "复制", KeyCombination = "Ctrl+C" },
                new HotkeyItem { Description = "剪切", KeyCombination = "Ctrl+X" },
                new HotkeyItem { Description = "粘贴", KeyCombination = "Ctrl+V" },
                new HotkeyItem { Description = "删除", KeyCombination = "Delete" },
                new HotkeyItem { Description = "重命名", KeyCombination = "F2" },
                new HotkeyItem { Description = "全选", KeyCombination = "Ctrl+A" },
                new HotkeyItem { Description = "打开设置", KeyCombination = "Ctrl+," },
                new HotkeyItem { Description = "搜索文件", KeyCombination = "Ctrl+F" },
                new HotkeyItem { Description = "返回上级目录", KeyCombination = "Backspace" },
                new HotkeyItem { Description = "后退", KeyCombination = "Alt+←" },
                new HotkeyItem { Description = "前进", KeyCombination = "Alt+→" },
            };

            // 复制到当前快捷键列表
            _hotkeys = new ObservableCollection<HotkeyItem>();
            foreach (var item in _defaultHotkeys)
            {
                _hotkeys.Add(new HotkeyItem
                {
                    Description = item.Description,
                    KeyCombination = item.KeyCombination
                });
            }

            HotkeyGrid.ItemsSource = _hotkeys;
        }

        /// <summary>
        /// 编辑快捷键按钮点击事件
        /// </summary>
        private void EditHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HotkeyItem item)
            {
                // 创建输入对话框
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

                var label = new TextBlock
                {
                    Text = "新快捷键:",
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 8) // Corrected 'M' to 'Margin'
                };
                Grid.SetRow(label, 1);

                var textBox = new TextBox
                {
                    Text = item.KeyCombination,
                    FontSize = 13,
                    Padding = new Thickness(8),
                    IsReadOnly = true,
                    Background = System.Windows.Media.Brushes.WhiteSmoke
                };
                Grid.SetRow(textBox, 2);

                // 监听键盘输入
                textBox.PreviewKeyDown += (s, args) =>
                {
                    args.Handled = true;
                    var key = args.Key;
                    var modifiers = args.KeyboardDevice.Modifiers;

                    // 构建快捷键字符串
                    var keys = new System.Collections.Generic.List<string>();
                    if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) keys.Add("Ctrl");
                    if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) keys.Add("Alt");
                    if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) keys.Add("Shift");

                    // 添加主键
                    if (key != System.Windows.Input.Key.LeftCtrl &&
                        key != System.Windows.Input.Key.RightCtrl &&
                        key != System.Windows.Input.Key.LeftAlt &&
                        key != System.Windows.Input.Key.RightAlt &&
                        key != System.Windows.Input.Key.LeftShift &&
                        key != System.Windows.Input.Key.RightShift)
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

                var saveBtn = new Button
                {
                    Content = "保存",
                    Padding = new Thickness(20, 8, 20, 8),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                saveBtn.Click += (s, args) =>
                {
                    item.KeyCombination = textBox.Text;
                    HotkeyGrid.Items.Refresh();
                    dialog.DialogResult = true;
                };

                var cancelBtn = new Button
                {
                    Content = "取消",
                    Padding = new Thickness(20, 8, 20, 8),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                cancelBtn.Click += (s, args) =>
                {
                    dialog.DialogResult = false;
                };

                buttonPanel.Children.Add(saveBtn);
                buttonPanel.Children.Add(cancelBtn);

                grid.Children.Add(descText);
                grid.Children.Add(label);
                grid.Children.Add(textBox);
                grid.Children.Add(buttonPanel);

                dialog.Content = grid;
                dialog.ShowDialog();
            }
        }

        /// <summary>
        /// 恢复默认按钮点击事件
        /// </summary>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要恢复所有快捷键为默认设置吗?",
                "确认恢复",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                LoadHotkeys();
                MessageBox.Show("快捷键已恢复为默认设置", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
