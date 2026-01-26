using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using YiboFile.Controls;
using Forms = System.Windows.Forms;
using YiboFile.Services;
using YiboFile.Services.Config;

namespace YiboFile.Controls.Settings
{
    public partial class GeneralSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private CheckBox _rememberWindowPositionCheckBox;
        private CheckBox _startMaximizedCheckBox;
        private CheckBox _enableMultiWindowCheckBox;
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
        private TextBox _pinnedTabWidthTextBox;
        private Button _pinnedTabWidthUpButton;
        private Button _pinnedTabWidthDownButton;
        private TextBlock _pinnedTabWidthLabel;

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

            var appearanceHint = new TextBlock
            {
                Text = "提示：主题、颜色、透明度等外观设置已移至\"外观\"设置面板。",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 24),
                TextWrapping = TextWrapping.Wrap
            };
            appearanceHint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            stackPanel.Children.Add(appearanceHint);

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
            _startMaximizedCheckBox.Unchecked += (s, e) => OnSettingChanged();
            stackPanel.Children.Add(_startMaximizedCheckBox);

            _enableMultiWindowCheckBox = new CheckBox
            {
                Content = "启用多窗口支持 (Ctrl+Shift+N)",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 24)
            };
            _enableMultiWindowCheckBox.Checked += (s, e) => OnSettingChanged();
            _enableMultiWindowCheckBox.Unchecked += (s, e) => OnSettingChanged();
            stackPanel.Children.Add(_enableMultiWindowCheckBox);

            // 标签页宽度模式
            var tabWidthModeLabel = new TextBlock
            {
                Text = "标签页宽度模式:",
                FontSize = 14,
                Margin = new Thickness(0, 8, 0, 8)
            };
            stackPanel.Children.Add(tabWidthModeLabel);

            // 固定宽度选项（包含宽度输入框）
            var fixedWidthPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(20, 0, 0, 4)
            };

            _tabWidthFixedRadio = new RadioButton
            {
                Content = "固定宽度（所有标签统一宽度）",
                GroupName = "TabWidthMode",
                FontSize = 14,
                MinHeight = 32,
                VerticalAlignment = VerticalAlignment.Center,
                IsChecked = true
            };
            _tabWidthFixedRadio.Checked += TabWidthFixedRadio_Checked;
            _tabWidthFixedRadio.Unchecked += TabWidthFixedRadio_Unchecked;
            fixedWidthPanel.Children.Add(_tabWidthFixedRadio);

            // 宽度标签（带范围提示）
            _pinnedTabWidthLabel = new TextBlock
            {
                Text = "宽度(50-300):",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 8, 0)
            };
            fixedWidthPanel.Children.Add(_pinnedTabWidthLabel);

            // 宽度输入框
            _pinnedTabWidthTextBox = new TextBox
            {
                FontSize = 14,
                Width = 60,
                Height = 36,  // 增加高度与单选按钮对齐
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _pinnedTabWidthTextBox.TextChanged += PinnedTabWidthTextBox_TextChanged;
            _pinnedTabWidthTextBox.LostFocus += PinnedTabWidthTextBox_LostFocus;
            _pinnedTabWidthTextBox.PreviewTextInput += FontSizeTextBox_PreviewTextInput;
            _pinnedTabWidthTextBox.KeyDown += NumericTextBox_KeyDown;
            fixedWidthPanel.Children.Add(_pinnedTabWidthTextBox);

            // 上下按钮
            var pinnedWidthButtonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            _pinnedTabWidthUpButton = new Button
            {
                Content = "▲",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 2),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            _pinnedTabWidthUpButton.Click += PinnedTabWidthUpButton_Click;
            pinnedWidthButtonPanel.Children.Add(_pinnedTabWidthUpButton);

            _pinnedTabWidthDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            _pinnedTabWidthDownButton.Click += PinnedTabWidthDownButton_Click;
            pinnedWidthButtonPanel.Children.Add(_pinnedTabWidthDownButton);

            fixedWidthPanel.Children.Add(pinnedWidthButtonPanel);
            stackPanel.Children.Add(fixedWidthPanel);

            _tabWidthDynamicRadio = new RadioButton
            {
                Content = "动态宽度（根据文本长度自适应）",
                GroupName = "TabWidthMode",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(20, 0, 0, 24)
            };
            _tabWidthDynamicRadio.Checked += TabWidthDynamicRadio_Checked;
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
            _uiFontSizeTextBox.KeyDown += NumericTextBox_KeyDown;
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
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            _fontSizeUpButton.Click += FontSizeUpButton_Click;

            _fontSizeDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
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
            _tagFontSizeTextBox.KeyDown += NumericTextBox_KeyDown;
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
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            _tagFontSizeUpButton.Click += TagFontSizeUpButton_Click;

            _tagFontSizeDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
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
            _tagBoxWidthTextBox.KeyDown += NumericTextBox_KeyDown;
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
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            _tagBoxWidthUpButton.Click += TagBoxWidthUpButton_Click;

            _tagBoxWidthDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
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
                Margin = new Thickness(8, 0, 0, 0),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
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
                Margin = new Thickness(0, 0, 8, 0),
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            exportButton.Click += exportHandler;
            buttonPanel.Children.Add(exportButton);

            var importButton = new Button
            {
                Content = "导入",
                FontSize = 13,
                Padding = new Thickness(14, 6, 14, 6),
                MinHeight = 32,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
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
                // 使用统一配置服务获取配置快照
                var config = ConfigurationService.Instance.GetSnapshot();

                if (_startMaximizedCheckBox != null)
                    _startMaximizedCheckBox.IsChecked = config.IsMaximized;

                if (_enableMultiWindowCheckBox != null)
                    _enableMultiWindowCheckBox.IsChecked = config.EnableMultiWindow;

                // 加载固定标签页宽度
                if (_pinnedTabWidthTextBox != null)
                {
                    int width = (int)(config.PinnedTabWidth > 0 ? config.PinnedTabWidth : 120);
                    _pinnedTabWidthTextBox.Text = width.ToString();

                    // 根据当前模式设置启用/禁用状态
                    bool isFixedMode = (config.TabWidthMode != TabWidthMode.DynamicWidth);
                    _pinnedTabWidthLabel.Opacity = isFixedMode ? 1.0 : 0.5;
                    _pinnedTabWidthTextBox.IsEnabled = isFixedMode;
                    _pinnedTabWidthUpButton.IsEnabled = isFixedMode;
                    _pinnedTabWidthDownButton.IsEnabled = isFixedMode;
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
            // 使用统一配置服务批量更新所有设置
            // 注意：字体大小已在ApplyFontSize中实时保存，这里只保存非实时的设置
            ConfigurationService.Instance.Update(config =>
            {
                // 启动时最大化
                if (_startMaximizedCheckBox != null)
                    config.IsMaximized = _startMaximizedCheckBox.IsChecked ?? true;

                // 多窗口支持
                if (_enableMultiWindowCheckBox != null)
                    config.EnableMultiWindow = _enableMultiWindowCheckBox.IsChecked ?? true;

                // 标签页宽度模式
                if (_tabWidthDynamicRadio != null && _tabWidthDynamicRadio.IsChecked == true)
                {
                    config.TabWidthMode = TabWidthMode.DynamicWidth;
                }
                else
                {
                    config.TabWidthMode = TabWidthMode.FixedWidth;
                }

                // 字体大小已在ApplyFontSize/ApplyTagFontSize/ApplyTagBoxWidth中实时保存
                // 这里不需要重复保存，避免从UI读取可能不一致的值
            });
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

            // 只验证是否是有效数字，不限制范围（允许中间输入状态）
            if (int.TryParse(textBox.Text, out int value))
            {
                // 应用字体（即时预览）
                ApplyFontSize(value);
            }
        }

        private void FontSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (int.TryParse(textBox.Text, out int value))
            {
                // 限制范围（失去焦点时）
                if (value < 10) value = 10;
                if (value > 48) value = 48;

                // 更新文本框为有效值
                textBox.Text = value.ToString();

                // 应用字体
                ApplyFontSize(value);
            }
            else
            {
                // 无效输入，恢复默认值
                textBox.Text = "15";
                ApplyFontSize(15);
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
            // 使用统一配置服务更新
            ConfigurationService.Instance.Set(cfg => cfg.UIFontSize, fontSize);

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

            // 只验证是否是有效数字，不限制范围
            if (int.TryParse(textBox.Text, out int value))
            {
                // 应用字体（即时预览）
                ApplyTagFontSize(value);
            }
        }

        private void TagFontSizeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (int.TryParse(textBox.Text, out int value))
            {
                // 限制范围（失去焦点时）
                if (value < 10) value = 10;
                if (value > 48) value = 48;

                // 更新文本框为有效值
                textBox.Text = value.ToString();

                // 应用字体
                ApplyTagFontSize(value);
            }
            else
            {
                // 无效输入，恢复默认值
                textBox.Text = "16";
                ApplyTagFontSize(16);
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

        // 通用数值输入框回车确认处理
        private void NumericTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                // 按回车键时，移动焦点触发LostFocus事件
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    // 移动焦点到父容器，触发LostFocus
                    textBox.MoveFocus(new System.Windows.Input.TraversalRequest(
                        System.Windows.Input.FocusNavigationDirection.Next));
                    e.Handled = true;
                }
            }
        }

        private void ApplyTagFontSize(double fontSize)
        {
            // 使用统一配置服务更新
            ConfigurationService.Instance.Set(cfg => cfg.TagFontSize, fontSize);

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
                value = Math.Max(50, value - 10);
                _tagBoxWidthTextBox.Text = value.ToString();
            }
        }

        #region 固定标签页宽度

        private void PinnedTabWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings) return;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 实时验证并应用宽度（用于预览）
            if (int.TryParse(textBox.Text, out int value))
            {
                // 实时预览：放宽范围限制以便输入时立即生效
                // 即使输入较小的值(如20)也立即应用，让用户有直观反馈
                // 最终的有效性检查由LostFocus处理
                if (value >= 10 && value <= 500)
                {
                    // 实时更新标签页宽度（不保存到配置）
                    ConfigurationService.Instance.Set(cfg => cfg.PinnedTabWidth, value);
                    SettingsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void PinnedTabWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (int.TryParse(textBox.Text, out int value))
            {
                // 限制范围（50-300）
                if (value < 50) value = 50;
                if (value > 300) value = 300;

                // 更新文本框为有效值
                textBox.Text = value.ToString();

                // 保存到配置
                ConfigurationService.Instance.Set(cfg => cfg.PinnedTabWidth, value);

                // 触发事件，让主窗口更新标签页
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                // 无效输入，恢复默认值
                textBox.Text = "120";
                ConfigurationService.Instance.Set(cfg => cfg.PinnedTabWidth, 120);
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void PinnedTabWidthUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_pinnedTabWidthTextBox.Text, out int value))
            {
                value = Math.Min(300, value + 10);
                _pinnedTabWidthTextBox.Text = value.ToString();
            }
        }

        private void PinnedTabWidthDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_pinnedTabWidthTextBox.Text, out int value))
            {
                value = Math.Max(50, value - 10);
                _pinnedTabWidthTextBox.Text = value.ToString();
            }
        }

        private void TabWidthFixedRadio_Checked(object sender, RoutedEventArgs e)
        {
            // 启用宽度输入
            if (_pinnedTabWidthLabel != null)
                _pinnedTabWidthLabel.Opacity = 1.0;
            if (_pinnedTabWidthTextBox != null)
                _pinnedTabWidthTextBox.IsEnabled = true;
            if (_pinnedTabWidthUpButton != null)
                _pinnedTabWidthUpButton.IsEnabled = true;
            if (_pinnedTabWidthDownButton != null)
                _pinnedTabWidthDownButton.IsEnabled = true;

            OnSettingChanged();
        }

        private void TabWidthFixedRadio_Unchecked(object sender, RoutedEventArgs e)
        {
            // 禁用宽度输入并变灰
            if (_pinnedTabWidthLabel != null)
                _pinnedTabWidthLabel.Opacity = 0.5;
            if (_pinnedTabWidthTextBox != null)
                _pinnedTabWidthTextBox.IsEnabled = false;
            if (_pinnedTabWidthUpButton != null)
                _pinnedTabWidthUpButton.IsEnabled = false;
            if (_pinnedTabWidthDownButton != null)
                _pinnedTabWidthDownButton.IsEnabled = false;
        }

        private void TabWidthDynamicRadio_Checked(object sender, RoutedEventArgs e)
        {
            OnSettingChanged();
        }

        #endregion

        private void ApplyTagBoxWidth(double width)
        {
            // 使用统一配置服务更新
            ConfigurationService.Instance.Set(cfg => cfg.TagBoxWidth, width);

            // 触发设置变更事件，让MainWindow应用宽度
            SettingsChanged?.Invoke(this, EventArgs.Empty);
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
                    // TagTrain SettingsManager removed - Phase 2
                    // TagTrain.Services.SettingsManager.ClearCache();
                    // TagTrain.Services.SettingsManager.SetDataStorageDirectory(ConfigManager.GetBaseDirectory());
                    // TagTrain.Services.SettingsManager.ClearCache();
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


