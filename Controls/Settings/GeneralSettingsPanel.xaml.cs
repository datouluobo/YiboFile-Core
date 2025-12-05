using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using OoiMRR.Controls;

namespace OoiMRR.Controls.Settings
{
    public partial class GeneralSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        
        private CheckBox _rememberWindowPositionCheckBox;
        private CheckBox _startMaximizedCheckBox;
        private TextBox _configDirectoryTextBox;
        private TextBox _dataDirectoryTextBox;
        private TextBox _databasePathTextBox;
        private TextBox _uiFontSizeTextBox;
        private Button _fontSizeUpButton;
        private Button _fontSizeDownButton;
        private TextBox _tagFontSizeTextBox;
        private Button _tagFontSizeUpButton;
        private Button _tagFontSizeDownButton;
        private TextBox _tagBoxWidthTextBox;
        private Button _tagBoxWidthUpButton;
        private Button _tagBoxWidthDownButton;
        
        private bool _isLoadingSettings = false;

        public GeneralSettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0) };
            
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
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(152, -8, 0, 0)
            };
            stackPanel.Children.Add(tagBoxWidthHint);
            
            // 路径设置
            var pathTitle = new TextBlock 
            { 
                Text = "路径设置", 
                FontSize = 18, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 24, 0, 16) 
            };
            stackPanel.Children.Add(pathTitle);
            
            _configDirectoryTextBox = CreatePathPanel(stackPanel, "配置文件目录:");
            _dataDirectoryTextBox = CreatePathPanel(stackPanel, "数据目录:");
            _databasePathTextBox = CreatePathPanel(stackPanel, "数据库路径:", isReadOnly: true);
            
            // 导入导出
            var separator = new Separator { Margin = new Thickness(0, 20, 0, 12) };
            stackPanel.Children.Add(separator);
            
            var configTitle = new TextBlock 
            { 
                Text = "配置管理", 
                FontSize = 18, 
                FontWeight = FontWeights.Bold, 
                Margin = new Thickness(0, 0, 0, 16) 
            };
            stackPanel.Children.Add(configTitle);
            
            var configButtonPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var exportButton = new Button 
            { 
                Content = "导出所有设置", 
                FontSize = 14,
                Padding = new Thickness(18, 8, 18, 8), 
                MinHeight = 40,
                Margin = new Thickness(0, 0, 12, 0)
            };
            exportButton.Click += ExportAllSettings_Click;
            var importButton = new Button 
            { 
                Content = "导入所有设置", 
                FontSize = 14,
                Padding = new Thickness(18, 8, 18, 8),
                MinHeight = 40
            };
            importButton.Click += ImportAllSettings_Click;
            configButtonPanel.Children.Add(exportButton);
            configButtonPanel.Children.Add(importButton);
            stackPanel.Children.Add(configButtonPanel);
            
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

        public void LoadSettings()
        {
            _isLoadingSettings = true;
            try
            {
                var config = ConfigManager.Load();
                
                if (_startMaximizedCheckBox != null)
                    _startMaximizedCheckBox.IsChecked = config.IsMaximized;
                
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
                
                if (_configDirectoryTextBox != null)
                    _configDirectoryTextBox.Text = ConfigManager.GetConfigDirectory();
                
                if (_dataDirectoryTextBox != null && App.IsTagTrainAvailable)
                {
                    TagTrain.Services.SettingsManager.ClearCache();
                    _dataDirectoryTextBox.Text = TagTrain.Services.SettingsManager.GetDataStorageDirectory();
                    if (_databasePathTextBox != null)
                        _databasePathTextBox.Text = TagTrain.Services.SettingsManager.GetDatabasePath();
                }
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

        private void ExportAllSettings_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                FileName = "OoiMRR_Settings.json",
                Filter = "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };
            
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.ExportAllSettings(sfd.FileName);
                    MessageBox.Show("所有设置已成功导出。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportAllSettings_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "配置文件 (*.json)|*.json|所有文件 (*.*)|*.*"
            };
            
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    ConfigManager.ImportAllSettings(ofd.FileName);
                    MessageBox.Show("所有设置已成功导入。\n\n部分设置需要重启程序后生效。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    LoadSettings();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}

