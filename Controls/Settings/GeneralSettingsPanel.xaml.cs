using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using OoiMRR.Controls;
using Forms = System.Windows.Forms;
using OoiMRR.Services;

namespace OoiMRR.Controls.Settings
{
    public partial class GeneralSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private ComboBox _themeComboBox;
        private CheckBox _rememberWindowPositionCheckBox;
        private CheckBox _startMaximizedCheckBox;
        private TextBox _baseDirectoryTextBox;
        private TextBox _uiFontSizeTextBox;
        private Button _fontSizeUpButton;
        private Button _fontSizeDownButton;
        private TextBox _tagFontSizeTextBox;
        private Button _tagFontSizeUpButton;
        private Button _tagFontSizeDownButton;
        private TextBox _tagBoxWidthTextBox;
        private Button _tagBoxWidthUpButton;
        private Button _tagBoxWidthDownButton;
        private RadioButton _tabWidthFixedRadio;
        private RadioButton _tabWidthDynamicRadio;

        private bool _isLoadingSettings = false;

        public GeneralSettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0) };

            // 外观设置
            var appearanceTitle = new TextBlock
            {
                Text = "外观设置",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(appearanceTitle);

            var themePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 24) };
            var themeLabel = new TextBlock
            {
                Text = "主题模式:",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 140
            };
            themePanel.Children.Add(themeLabel);

            _themeComboBox = new ComboBox { Width = 200, Height = 32, FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(10, 0, 0, 0) };
            _themeComboBox.Items.Add(new ComboBoxItem { Content = "浅色模式 (Light)", Tag = "Light" });
            _themeComboBox.Items.Add(new ComboBoxItem { Content = "深色模式 (Dark)", Tag = "Dark" });
            _themeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
            themePanel.Children.Add(_themeComboBox);
            stackPanel.Children.Add(themePanel);

            // 窗口设置
            var windowTitle = new TextBlock
            {
                Text = "窗口设置",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(windowTitle);

            _rememberWindowPositionCheckBox = new CheckBox
            {
                Content = "记住窗口位置和大小",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 10),
                IsChecked = true
            };
            _rememberWindowPositionCheckBox.Checked += (s, e) => OnSettingChanged();
            _rememberWindowPositionCheckBox.Unchecked += (s, e) => OnSettingChanged();
            stackPanel.Children.Add(_rememberWindowPositionCheckBox);

            _startMaximizedCheckBox = new CheckBox
            {
                Content = "启动时最大化窗口",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 24)
            };
            _startMaximizedCheckBox.Checked += (s, e) => OnSettingChanged();
            _startMaximizedCheckBox.Unchecked += (s, e) => OnSettingChanged();
            stackPanel.Children.Add(_startMaximizedCheckBox);

            // 标签页宽度模式
            var tabWidthModeLabel = new TextBlock
            {
                Text = "标签页宽度模式:",
                FontSize = 14,
                Margin = new Thickness(0, 8, 0, 8)
            };
            stackPanel.Children.Add(tabWidthModeLabel);

            _tabWidthFixedRadio = new RadioButton
            {
                Content = "固定宽度（所有标签统一宽度）",
                GroupName = "TabWidthMode",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(20, 0, 0, 4),
                IsChecked = true
            };
            _tabWidthFixedRadio.Checked += (s, e) => OnSettingChanged();
            stackPanel.Children.Add(_tabWidthFixedRadio);

            _tabWidthDynamicRadio = new RadioButton
            {
                Content = "动态宽度（根据文本长度自适应）",
                GroupName = "TabWidthMode",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(20, 0, 0, 24)
            };
            _tabWidthDynamicRadio.Checked += (s, e) => OnSettingChanged();
            stackPanel.Children.Add(_tabWidthDynamicRadio);

            // 字体设置
            var fontTitle = new TextBlock
            {
                Text = "字体设置",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 24, 0, 16)
            };
            stackPanel.Children.Add(fontTitle);

            // 创建字体大小控件
            var fontGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            fontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var fontLabel = new TextBlock
            {
                Text = "字体大小:",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 140,
                MinHeight = 32
            };
            Grid.SetColumn(fontLabel, 0);
            fontGrid.Children.Add(fontLabel);

            _uiFontSizeTextBox = new TextBox
            {
                FontSize = 14,
                Width = 60,
                Height = 32,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _uiFontSizeTextBox.TextChanged += FontSizeTextBox_TextChanged;
            _uiFontSizeTextBox.LostFocus += FontSizeTextBox_LostFocus;
            _uiFontSizeTextBox.PreviewTextInput += FontSizeTextBox_PreviewTextInput;
            Grid.SetColumn(_uiFontSizeTextBox, 1);
            fontGrid.Children.Add(_uiFontSizeTextBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 0)
            };

            _fontSizeUpButton = new Button
            {
                Content = "▲",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            _fontSizeUpButton.Click += FontSizeUpButton_Click;

            _fontSizeDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _fontSizeDownButton.Click += FontSizeDownButton_Click;

            buttonPanel.Children.Add(_fontSizeUpButton);
            buttonPanel.Children.Add(_fontSizeDownButton);
            Grid.SetColumn(buttonPanel, 2);
            fontGrid.Children.Add(buttonPanel);

            stackPanel.Children.Add(fontGrid);

            // Tag字号设置
            var tagFontGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            tagFontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tagFontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tagFontGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tagFontLabel = new TextBlock
            {
                Text = "Tag字号:",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 140,
                MinHeight = 32
            };
            Grid.SetColumn(tagFontLabel, 0);
            tagFontGrid.Children.Add(tagFontLabel);

            _tagFontSizeTextBox = new TextBox
            {
                FontSize = 14,
                Width = 60,
                Height = 32,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _tagFontSizeTextBox.TextChanged += TagFontSizeTextBox_TextChanged;
            _tagFontSizeTextBox.LostFocus += TagFontSizeTextBox_LostFocus;
            _tagFontSizeTextBox.PreviewTextInput += FontSizeTextBox_PreviewTextInput;
            Grid.SetColumn(_tagFontSizeTextBox, 1);
            tagFontGrid.Children.Add(_tagFontSizeTextBox);

            var tagButtonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 0)
            };

            _tagFontSizeUpButton = new Button
            {
                Content = "▲",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            _tagFontSizeUpButton.Click += TagFontSizeUpButton_Click;

            _tagFontSizeDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _tagFontSizeDownButton.Click += TagFontSizeDownButton_Click;

            tagButtonPanel.Children.Add(_tagFontSizeUpButton);
            tagButtonPanel.Children.Add(_tagFontSizeDownButton);
            Grid.SetColumn(tagButtonPanel, 2);
            tagFontGrid.Children.Add(tagButtonPanel);

            stackPanel.Children.Add(tagFontGrid);

            // Tag框宽度设置
            var tagBoxWidthGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            tagBoxWidthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tagBoxWidthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            tagBoxWidthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var tagBoxWidthLabel = new TextBlock
            {
                Text = "Tag框宽度:",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 140,
                MinHeight = 32
            };
            Grid.SetColumn(tagBoxWidthLabel, 0);
            tagBoxWidthGrid.Children.Add(tagBoxWidthLabel);

            _tagBoxWidthTextBox = new TextBox
            {
                FontSize = 14,
                Width = 60,
                Height = 32,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _tagBoxWidthTextBox.TextChanged += TagBoxWidthTextBox_TextChanged;
            _tagBoxWidthTextBox.LostFocus += TagBoxWidthTextBox_LostFocus;
            _tagBoxWidthTextBox.PreviewTextInput += FontSizeTextBox_PreviewTextInput;
            Grid.SetColumn(_tagBoxWidthTextBox, 1);
            tagBoxWidthGrid.Children.Add(_tagBoxWidthTextBox);

            var tagBoxWidthButtonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 0)
            };

            _tagBoxWidthUpButton = new Button
            {
                Content = "▲",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            _tagBoxWidthUpButton.Click += TagBoxWidthUpButton_Click;

            _tagBoxWidthDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _tagBoxWidthDownButton.Click += TagBoxWidthDownButton_Click;

            tagBoxWidthButtonPanel.Children.Add(_tagBoxWidthUpButton);
            tagBoxWidthButtonPanel.Children.Add(_tagBoxWidthDownButton);
            Grid.SetColumn(tagBoxWidthButtonPanel, 2);
            tagBoxWidthGrid.Children.Add(tagBoxWidthButtonPanel);

            stackPanel.Children.Add(tagBoxWidthGrid);

            // 添加提示文本（单独一行）
            var tagBoxWidthHint = new TextBlock
            {
                Text = "（0表示自动计算，>0表示固定宽度，范围：0-500）",
                FontSize = 12,
                Margin = new Thickness(152, -8, 0, 0)
            };
            tagBoxWidthHint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            stackPanel.Children.Add(tagBoxWidthHint);

            // 路径设置
            var pathTitle = new TextBlock
            {
                Text = "路径设置",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 24, 0, 8)
            };
            stackPanel.Children.Add(pathTitle);

            var pathHint = new TextBlock
            {
                Text = "默认目录：程序根目录下的 .\\AppData\\ （配置、数据、TagTrain 文件全部集中）",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 12),
                TextWrapping = TextWrapping.Wrap
            };
            pathHint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            stackPanel.Children.Add(pathHint);

            _baseDirectoryTextBox = CreatePathPanelWithButton(stackPanel, "当前目录:", BrowseBaseDirectory_Click);

            // 导入导出
            var separator = new Separator { Margin = new Thickness(0, 20, 0, 12) };
            stackPanel.Children.Add(separator);

            var configTitle = new TextBlock
            {
                Text = "导入 / 导出",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(configTitle);

            stackPanel.Children.Add(CreateImportExportRow("仅配置（ooi_config.json + tt_settings.txt）", ExportConfigs_Click, ImportConfigs_Click));
            stackPanel.Children.Add(CreateImportExportRow("仅数据（ooi_data.db + tt_training.db + tt_model.zip）", ExportData_Click, ImportData_Click));
            stackPanel.Children.Add(CreateImportExportRow("全部（配置 + 数据）", ExportAll_Click, ImportAll_Click));

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel
            };

            Content = scrollViewer;
        }

        private Slider CreateSliderPanel(StackPanel parent, string label, double min, double max, double defaultValue, ref TextBlock valueText)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 140,
                MinHeight = 32
            };
            Grid.SetColumn(labelText, 0);
            grid.Children.Add(labelText);

            var slider = new Slider
            {
                Minimum = min,
                Maximum = max,
                Value = defaultValue,
                TickFrequency = 10,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                Height = 24
            };
            Grid.SetColumn(slider, 1);
            grid.Children.Add(slider);

            var valueTextBlock = new TextBlock
            {
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 60,
                MinHeight = 32,
                Text = ((int)defaultValue).ToString()
            };
            valueText = valueTextBlock;
            slider.ValueChanged += (s, e) => valueTextBlock.Text = ((int)slider.Value).ToString();
            Grid.SetColumn(valueTextBlock, 2);
            grid.Children.Add(valueTextBlock);

            parent.Children.Add(grid);
            return slider;
        }

        private TextBox CreatePathPanel(StackPanel parent, string label, bool isReadOnly = false)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 140,
                MinHeight = 36
            };
            Grid.SetColumn(labelText, 0);
            grid.Children.Add(labelText);

            var textBox = new TextBox
            {
                FontSize = 14,
                MinHeight = 36,
                IsReadOnly = isReadOnly,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(10, 6, 10, 6)
            };
            Grid.SetColumn(textBox, 1);
            grid.Children.Add(textBox);

            parent.Children.Add(grid);
            return textBox;
        }

        private TextBox CreatePathPanelWithButton(StackPanel parent, string label, RoutedEventHandler browseHandler)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 140,
                MinHeight = 36
            };
            Grid.SetColumn(labelText, 0);
            grid.Children.Add(labelText);

            var textBox = new TextBox
            {
                FontSize = 14,
                MinHeight = 36,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(10, 6, 10, 6),
                IsReadOnly = true
            };
            Grid.SetColumn(textBox, 1);
            grid.Children.Add(textBox);

            var button = new Button
            {
                Content = "选择文件夹",
                FontSize = 13,
                Padding = new Thickness(12, 6, 12, 6),
                MinHeight = 32,
                Margin = new Thickness(8, 0, 0, 0)
            };
            button.Click += browseHandler;
            Grid.SetColumn(button, 2);
            grid.Children.Add(button);

            parent.Children.Add(grid);
            return textBox;
        }

        private UIElement CreateImportExportRow(string title, RoutedEventHandler exportHandler, RoutedEventHandler importHandler)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 12) };

            var text = new TextBlock
            {
                Text = title,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 6)
            };
            panel.Children.Add(text);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var exportButton = new Button
            {
                Content = "导出",
                FontSize = 13,
                Padding = new Thickness(14, 6, 14, 6),
                MinHeight = 32,
                Margin = new Thickness(0, 0, 8, 0)
            };
            exportButton.Click += exportHandler;
            buttonPanel.Children.Add(exportButton);

            var importButton = new Button
            {
                Content = "导入",
                FontSize = 13,
                Padding = new Thickness(14, 6, 14, 6),
                MinHeight = 32
            };
            importButton.Click += importHandler;
            buttonPanel.Children.Add(importButton);

            panel.Children.Add(buttonPanel);
            return panel;
        }

        public void LoadSettings()
        {
            _isLoadingSettings = true;
            try
            {
                var config = ConfigManager.Load();

                if (_startMaximizedCheckBox != null)
                    _startMaximizedCheckBox.IsChecked = config.IsMaximized;

                if (_themeComboBox != null)
                {
                    string theme = config.Theme ?? "Light";
                    foreach (ComboBoxItem item in _themeComboBox.Items)
                    {
                        if ((item.Tag as string) == theme)
                        {
                            _themeComboBox.SelectedItem = item;
                            break;
                        }
                    }
                }

                if (_uiFontSizeTextBox != null)
                {
                    _uiFontSizeTextBox.Text = ((int)(config.UIFontSize > 0 ? config.UIFontSize : 16)).ToString();
                }

                if (_tagFontSizeTextBox != null)
                {
                    _tagFontSizeTextBox.Text = ((int)(config.TagFontSize > 0 ? config.TagFontSize : 16)).ToString();
                }

                if (_tagBoxWidthTextBox != null)
                {
                    _tagBoxWidthTextBox.Text = ((int)config.TagBoxWidth).ToString();
                }

                // Load tab width mode
                if (_tabWidthFixedRadio != null && _tabWidthDynamicRadio != null)
                {
                    if (config.TabWidthMode == TabWidthMode.DynamicWidth)
                    {
                        _tabWidthDynamicRadio.IsChecked = true;
                    }
                    else
                    {
                        _tabWidthFixedRadio.IsChecked = true;
                    }
                }

                if (_baseDirectoryTextBox != null)
                    _baseDirectoryTextBox.Text = ConfigManager.GetBaseDirectory();
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        private void OnSettingChanged()
        {
            if (_isLoadingSettings) return;

            SaveSettings();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SaveSettings()
        {
            var config = ConfigManager.Load();

            if (_startMaximizedCheckBox != null)
                config.IsMaximized = _startMaximizedCheckBox.IsChecked ?? true;

            if (_themeComboBox != null && _themeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string theme)
            {
                config.Theme = theme;
            }

            // Save tab width mode
            if (_tabWidthDynamicRadio != null && _tabWidthDynamicRadio.IsChecked == true)
            {
                config.TabWidthMode = TabWidthMode.DynamicWidth;
            }
            else
            {
                config.TabWidthMode = TabWidthMode.FixedWidth;
            }

            // 字体大小在 ApplyFontSize 中已保存，这里不需要再保存

            ConfigManager.Save(config);
        }

        private void FontSizeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 只允许输入数字
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void FontSizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings) return;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (int.TryParse(textBox.Text, out int value))
            {
                // 限制范围
                if (value < 10) value = 10;
                if (value > 48) value = 48;

                // 如果值被修正，更新文本框
                if (textBox.Text != value.ToString())
                {
                    var selectionStart = textBox.SelectionStart;
                    textBox.Text = value.ToString();
                    textBox.SelectionStart = Math.Min(selectionStart, textBox.Text.Length);
                }

                // 应用字体
                ApplyFontSize(value);
            }
        }

        private void FontSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 如果输入无效，恢复为当前配置值
            if (!int.TryParse(textBox.Text, out int value) || value < 10 || value > 48)
            {
                var config = ConfigManager.Load();
                textBox.Text = ((int)config.UIFontSize).ToString();
            }
        }

        private void FontSizeUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_uiFontSizeTextBox.Text, out int value))
            {
                value = Math.Min(48, value + 1);
                _uiFontSizeTextBox.Text = value.ToString();
                ApplyFontSize(value);
            }
        }

        private void FontSizeDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_uiFontSizeTextBox.Text, out int value))
            {
                value = Math.Max(10, value - 1);
                _uiFontSizeTextBox.Text = value.ToString();
                ApplyFontSize(value);
            }
        }

        private void ApplyFontSize(double fontSize)
        {
            // 保存配置
            var config = ConfigManager.Load();
            config.UIFontSize = fontSize;
            ConfigManager.Save(config);

            // 触发设置变更事件，让MainWindow应用字体
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TagFontSizeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 只允许输入数字
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void TagFontSizeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings) return;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (int.TryParse(textBox.Text, out int value))
            {
                // 限制范围
                if (value < 10) value = 10;
                if (value > 48) value = 48;

                // 如果值被修正，更新文本框
                if (textBox.Text != value.ToString())
                {
                    var selectionStart = textBox.SelectionStart;
                    textBox.Text = value.ToString();
                    textBox.SelectionStart = Math.Min(selectionStart, textBox.Text.Length);
                }

                // 应用字体
                ApplyTagFontSize(value);
            }
        }

        private void TagFontSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 如果输入无效，恢复为当前配置值
            if (!int.TryParse(textBox.Text, out int value) || value < 10 || value > 48)
            {
                var config = ConfigManager.Load();
                textBox.Text = ((int)config.TagFontSize).ToString();
            }
        }

        private void TagFontSizeUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_tagFontSizeTextBox.Text, out int value))
            {
                value = Math.Min(48, value + 1);
                _tagFontSizeTextBox.Text = value.ToString();
                ApplyTagFontSize(value);
            }
        }

        private void TagFontSizeDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_tagFontSizeTextBox.Text, out int value))
            {
                value = Math.Max(10, value - 1);
                _tagFontSizeTextBox.Text = value.ToString();
                ApplyTagFontSize(value);
            }
        }

        private void ApplyTagFontSize(double fontSize)
        {
            // 保存配置
            var config = ConfigManager.Load();
            config.TagFontSize = fontSize;
            ConfigManager.Save(config);

            // 触发设置变更事件，让MainWindow应用字体
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void TagBoxWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings) return;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (int.TryParse(textBox.Text, out int value))
            {
                // 限制范围
                if (value < 0) value = 0;
                if (value > 500) value = 500;

                // 如果值被修正，更新文本框
                if (textBox.Text != value.ToString())
                {
                    var selectionStart = textBox.SelectionStart;
                    textBox.Text = value.ToString();
                    textBox.SelectionStart = Math.Min(selectionStart, textBox.Text.Length);
                }

                // 应用宽度
                ApplyTagBoxWidth(value);
            }
        }

        private void TagBoxWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 如果输入无效，恢复为当前配置值
            if (!int.TryParse(textBox.Text, out int value) || value < 0 || value > 500)
            {
                var config = ConfigManager.Load();
                textBox.Text = ((int)config.TagBoxWidth).ToString();
            }
        }

        private void TagBoxWidthUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_tagBoxWidthTextBox.Text, out int value))
            {
                value = Math.Min(500, value + 5);
                _tagBoxWidthTextBox.Text = value.ToString();
                ApplyTagBoxWidth(value);
            }
        }

        private void TagBoxWidthDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_tagBoxWidthTextBox.Text, out int value))
            {
                value = Math.Max(0, value - 5);
                _tagBoxWidthTextBox.Text = value.ToString();
                ApplyTagBoxWidth(value);
            }
        }

        private void ApplyTagBoxWidth(double width)
        {
            // 保存配置
            var config = ConfigManager.Load();
            config.TagBoxWidth = width;
            ConfigManager.Save(config);

            // 触发设置变更事件，让MainWindow应用宽度
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;

            if (_themeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string theme)
            {
                // 立即应用主题
                ThemeService.SetTheme(theme == "Dark" ? AppTheme.Dark : AppTheme.Light);

                // 保存设置
                OnSettingChanged();
            }
        }

        private void BrowseBaseDirectory_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Forms.FolderBrowserDialog
                {
                    Description = "选择配置/数据存储目录（默认 .\\AppData）",
                    SelectedPath = ConfigManager.GetBaseDirectory()
                };

                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    ApplyBaseDirectoryChange(dialog.SelectedPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"选择目录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyBaseDirectoryChange(string newDir)
        {
            if (string.IsNullOrWhiteSpace(newDir)) return;

            var oldDir = ConfigManager.GetBaseDirectory();
            if (string.Equals(NormalizePath(oldDir), NormalizePath(newDir), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ConfigManager.SetBaseDirectory(newDir, copyMissingFromOld: true);

            if (App.IsTagTrainAvailable)
            {
                try
                {
                    TagTrain.Services.SettingsManager.ClearCache();
                    TagTrain.Services.SettingsManager.SetDataStorageDirectory(ConfigManager.GetBaseDirectory());
                    TagTrain.Services.SettingsManager.ClearCache();
                }
                catch { }
            }

            try
            {
                DatabaseManager.Initialize();
            }
            catch { }

            LoadSettings();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path;
            }
        }

        private void ExportConfigs_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                FileName = "configs.zip",
                Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.ExportConfigsZip(sfd.FileName);
                    MessageBox.Show("配置已导出。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportConfigs_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.ImportConfigsZip(ofd.FileName);
                    LoadSettings();
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                    MessageBox.Show("配置已导入。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportData_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                FileName = "data.zip",
                Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.ExportDataZip(sfd.FileName);
                    MessageBox.Show("数据已导出。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportData_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.ImportDataZip(ofd.FileName);
                    LoadSettings();
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                    MessageBox.Show("数据已导入。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportAll_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                FileName = "all.zip",
                Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.ExportAllZip(sfd.FileName);
                    MessageBox.Show("全部文件已导出。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportAll_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.ImportAllZip(ofd.FileName);
                    LoadSettings();
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                    MessageBox.Show("全部文件已导入。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}

