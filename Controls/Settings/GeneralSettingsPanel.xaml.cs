using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using Microsoft.Win32;
using YiboFile.Controls;
using Forms = System.Windows.Forms;
using YiboFile.Services;
using YiboFile.Services.Config;
using YiboFile.ViewModels;

namespace YiboFile.Controls.Settings
{
    public partial class GeneralSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private SettingsViewModel _viewModel;

        private CheckBox _rememberWindowPositionCheckBox;
        private CheckBox _startMaximizedCheckBox;
        private CheckBox _enableMultiWindowCheckBox;
        private TextBox _baseDirectoryTextBox;
        private TextBox _uiFontSizeTextBox;
        private TextBox _tagFontSizeTextBox;
        private TextBox _tagBoxWidthTextBox;
        private RadioButton _tabWidthFixedRadio;
        private RadioButton _tabWidthDynamicRadio;
        private TextBox _pinnedTabWidthTextBox;
        private Button _pinnedTabWidthUpButton;
        private Button _pinnedTabWidthDownButton;
        private TextBlock _pinnedTabWidthLabel;

        public GeneralSettingsPanel()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            this.DataContext = _viewModel;

            // Bridge ViewModel changes to SettingsChanged event for legacy support
            _viewModel.PropertyChanged += (s, e) => SettingsChanged?.Invoke(this, EventArgs.Empty);

            InitializeBindings();
            InitializeState(); // Initial state for non-bound controls like RadioButtons
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
                IsChecked = true // UI Placeholder, logic not implemented in VM yet
            };
            stackPanel.Children.Add(_rememberWindowPositionCheckBox);

            _startMaximizedCheckBox = new CheckBox
            {
                Content = "启动时最大化窗口",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 24)
            };
            stackPanel.Children.Add(_startMaximizedCheckBox);

            _enableMultiWindowCheckBox = new CheckBox
            {
                Content = "启用多窗口支持 (Ctrl+Shift+N)",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 24)
            };
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
                VerticalAlignment = VerticalAlignment.Center
            };
            _tabWidthFixedRadio.Checked += TabWidthMode_Changed;
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
                Height = 36,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            _pinnedTabWidthTextBox.PreviewTextInput += NumericOnly_PreviewTextInput;
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
            _pinnedTabWidthUpButton.Click += (s, e) => AdjustValue(_viewModel.PinnedTabWidth, 10, 50, 300, v => _viewModel.PinnedTabWidth = v);
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
            _pinnedTabWidthDownButton.Click += (s, e) => AdjustValue(_viewModel.PinnedTabWidth, -10, 50, 300, v => _viewModel.PinnedTabWidth = v);
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
            _tabWidthDynamicRadio.Checked += TabWidthMode_Changed;
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
            _uiFontSizeTextBox.PreviewTextInput += NumericOnly_PreviewTextInput;
            Grid.SetColumn(_uiFontSizeTextBox, 1);
            fontGrid.Children.Add(_uiFontSizeTextBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 0)
            };

            var fontSizeUpButton = new Button
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
            fontSizeUpButton.Click += (s, e) => AdjustValue(_viewModel.UIFontSize, 1, 10, 48, v => _viewModel.UIFontSize = v);

            var fontSizeDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            fontSizeDownButton.Click += (s, e) => AdjustValue(_viewModel.UIFontSize, -1, 10, 48, v => _viewModel.UIFontSize = v);

            buttonPanel.Children.Add(fontSizeUpButton);
            buttonPanel.Children.Add(fontSizeDownButton);
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
            _tagFontSizeTextBox.PreviewTextInput += NumericOnly_PreviewTextInput;
            Grid.SetColumn(_tagFontSizeTextBox, 1);
            tagFontGrid.Children.Add(_tagFontSizeTextBox);

            var tagButtonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 0)
            };

            var tagFontSizeUpButton = new Button
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
            tagFontSizeUpButton.Click += (s, e) => AdjustValue(_viewModel.TagFontSize, 1, 10, 48, v => _viewModel.TagFontSize = v);

            var tagFontSizeDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            tagFontSizeDownButton.Click += (s, e) => AdjustValue(_viewModel.TagFontSize, -1, 10, 48, v => _viewModel.TagFontSize = v);

            tagButtonPanel.Children.Add(tagFontSizeUpButton);
            tagButtonPanel.Children.Add(tagFontSizeDownButton);
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
            _tagBoxWidthTextBox.PreviewTextInput += NumericOnly_PreviewTextInput;
            Grid.SetColumn(_tagBoxWidthTextBox, 1);
            tagBoxWidthGrid.Children.Add(_tagBoxWidthTextBox);

            var tagBoxWidthButtonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 0)
            };

            var tagBoxWidthUpButton = new Button
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
            tagBoxWidthUpButton.Click += (s, e) => AdjustValue(_viewModel.TagBoxWidth, 5, 0, 500, v => _viewModel.TagBoxWidth = v);

            var tagBoxWidthDownButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["ModernButtonStyle"]
            };
            tagBoxWidthDownButton.Click += (s, e) => AdjustValue(_viewModel.TagBoxWidth, -10, 50, 500, v => _viewModel.TagBoxWidth = v);

            tagBoxWidthButtonPanel.Children.Add(tagBoxWidthUpButton);
            tagBoxWidthButtonPanel.Children.Add(tagBoxWidthDownButton);
            Grid.SetColumn(tagBoxWidthButtonPanel, 2);
            tagBoxWidthGrid.Children.Add(tagBoxWidthButtonPanel);

            stackPanel.Children.Add(tagBoxWidthGrid);

            // 添加提示文本
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

            stackPanel.Children.Add(CreateImportExportRow("仅配置（ooi_config.json + tt_settings.txt）",
                (s, e) => ExportFileAndExecute(_viewModel.ExportConfigsCommand, "configs.zip"),
                (s, e) => ImportFileAndExecute(_viewModel.ImportConfigsCommand)));

            stackPanel.Children.Add(CreateImportExportRow("仅数据（ooi_data.db + tt_training.db + tt_model.zip）",
                (s, e) => ExportFileAndExecute(_viewModel.ExportDataCommand, "data.zip"),
                (s, e) => ImportFileAndExecute(_viewModel.ImportDataCommand)));

            stackPanel.Children.Add(CreateImportExportRow("全部（配置 + 数据）",
                (s, e) => ExportFileAndExecute(_viewModel.ExportAllCommand, "all.zip"),
                (s, e) => ImportFileAndExecute(_viewModel.ImportAllCommand)));

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel
            };

            Content = scrollViewer;
        }

        private void InitializeBindings()
        {
            _startMaximizedCheckBox.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(SettingsViewModel.IsMaximized)) { Mode = BindingMode.TwoWay });
            _enableMultiWindowCheckBox.SetBinding(CheckBox.IsCheckedProperty, new Binding(nameof(SettingsViewModel.EnableMultiWindow)) { Mode = BindingMode.TwoWay });

            _uiFontSizeTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.UIFontSize)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            _tagFontSizeTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.TagFontSize)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            _tagBoxWidthTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.TagBoxWidth)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            _pinnedTabWidthTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.PinnedTabWidth)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            _baseDirectoryTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.BaseDirectory)) { Mode = BindingMode.OneWay });
        }

        private void InitializeState()
        {
            if (_viewModel.TabWidthMode == TabWidthMode.DynamicWidth)
                _tabWidthDynamicRadio.IsChecked = true;
            else
                _tabWidthFixedRadio.IsChecked = true;

            UpdatePinnedTabWidthUIState();
        }

        private void TabWidthMode_Changed(object sender, RoutedEventArgs e)
        {
            if (_tabWidthDynamicRadio.IsChecked == true)
                _viewModel.TabWidthMode = TabWidthMode.DynamicWidth;
            else
                _viewModel.TabWidthMode = TabWidthMode.FixedWidth;

            UpdatePinnedTabWidthUIState();
        }

        private void UpdatePinnedTabWidthUIState()
        {
            bool isFixedMode = _viewModel.TabWidthMode != TabWidthMode.DynamicWidth;
            if (_pinnedTabWidthLabel != null) _pinnedTabWidthLabel.Opacity = isFixedMode ? 1.0 : 0.5;
            if (_pinnedTabWidthTextBox != null) _pinnedTabWidthTextBox.IsEnabled = isFixedMode;
            if (_pinnedTabWidthUpButton != null) _pinnedTabWidthUpButton.IsEnabled = isFixedMode;
            if (_pinnedTabWidthDownButton != null) _pinnedTabWidthDownButton.IsEnabled = isFixedMode;
        }

        private void AdjustValue(double current, double delta, double min, double max, Action<double> setter)
        {
            double newValue = Math.Clamp(current + delta, min, max);
            setter(newValue);
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
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

        private void BrowseBaseDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Forms.FolderBrowserDialog
            {
                Description = "选择配置/数据存储目录（默认 .\\AppData）",
                SelectedPath = _viewModel.BaseDirectory // Use VM property
            };

            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                _viewModel.ChangeBaseDirectoryCommand.Execute(dialog.SelectedPath);
            }
        }

        private void ExportFileAndExecute(ICommand command, string defaultName)
        {
            var sfd = new SaveFileDialog
            {
                FileName = defaultName,
                Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    command.Execute(sfd.FileName);
                    MessageBox.Show("文件已导出。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportFileAndExecute(ICommand command)
        {
            var ofd = new OpenFileDialog
            {
                Filter = "ZIP文件 (*.zip)|*.zip|所有文件 (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    command.Execute(ofd.FileName);
                    MessageBox.Show("文件已导入。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
            InitializeState();
        }

        public void SaveSettings()
        {
            // Auto-saved by bindings and VM logic
        }
    }
}
