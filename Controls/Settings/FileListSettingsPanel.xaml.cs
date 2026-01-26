using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile;
using YiboFile.Services.Config;

namespace YiboFile.Controls.Settings
{
    public partial class FileListSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private TextBox _tagsWidthTextBox;
        private Button _tagsWidthUpButton;
        private Button _tagsWidthDownButton;
        private TextBox _notesWidthTextBox;
        private Button _notesWidthUpButton;
        private Button _notesWidthDownButton;

        private bool _isLoadingSettings = false;

        public FileListSettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0) };

            // 列宽设置标题
            var title = new TextBlock
            {
                Text = "列宽设置",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(title);

            // Tags列宽设置
            var tagsGrid = CreateNumberInputRow("标签列宽度:", ref _tagsWidthTextBox, ref _tagsWidthUpButton, ref _tagsWidthDownButton);
            stackPanel.Children.Add(tagsGrid);

            _tagsWidthTextBox.TextChanged += TagsWidthTextBox_TextChanged;
            _tagsWidthTextBox.LostFocus += TagsWidthTextBox_LostFocus;
            _tagsWidthTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;
            _tagsWidthUpButton.Click += TagsWidthUpButton_Click;
            _tagsWidthDownButton.Click += TagsWidthDownButton_Click;

            // 添加提示
            var tagsHint = new TextBlock
            {
                Text = "（范围：50-500）",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(152, -8, 0, 0)
            };
            stackPanel.Children.Add(tagsHint);

            // Notes列宽设置
            var notesGrid = CreateNumberInputRow("备注列宽度:", ref _notesWidthTextBox, ref _notesWidthUpButton, ref _notesWidthDownButton);
            stackPanel.Children.Add(notesGrid);

            _notesWidthTextBox.TextChanged += NotesWidthTextBox_TextChanged;
            _notesWidthTextBox.LostFocus += NotesWidthTextBox_LostFocus;
            _notesWidthTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;
            _notesWidthUpButton.Click += NotesWidthUpButton_Click;
            _notesWidthDownButton.Click += NotesWidthDownButton_Click;

            // 添加提示
            var notesHint = new TextBlock
            {
                Text = "（范围：100-800）",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray,
                Margin = new Thickness(152, -8, 0, 0)
            };
            stackPanel.Children.Add(notesHint);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel
            };

            Content = scrollViewer;
        }

        private Grid CreateNumberInputRow(string label, ref TextBox textBox, ref Button upButton, ref Button downButton)
        {
            var grid = new Grid { Margin = new Thickness(0, 12, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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

            textBox = new TextBox
            {
                FontSize = 14,
                Width = 60,
                Height = 32,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(5, 0, 5, 0),
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 0)
            };

            upButton = new Button
            {
                Content = "▲",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                Margin = new Thickness(0, 0, 0, 2),
                VerticalAlignment = VerticalAlignment.Center
            };

            downButton = new Button
            {
                Content = "▼",
                Width = 24,
                Height = 16,
                FontSize = 10,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };

            buttonPanel.Children.Add(upButton);
            buttonPanel.Children.Add(downButton);
            Grid.SetColumn(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            return grid;
        }

        public void LoadSettings()
        {
            _isLoadingSettings = true;
            try
            {
                // 使用统一配置服务获取配置快照
                var config = ConfigurationService.Instance.GetSnapshot();

                if (_tagsWidthTextBox != null)
                {
                    _tagsWidthTextBox.Text = ((int)config.ColTagsWidth).ToString();
                }

                if (_notesWidthTextBox != null)
                {
                    _notesWidthTextBox.Text = ((int)config.ColNotesWidth).ToString();
                }
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        public void SaveSettings()
        {
            // 使用统一配置服务批量更新列宽设置
            ConfigurationService.Instance.Update(config =>
            {
                if (_tagsWidthTextBox != null && int.TryParse(_tagsWidthTextBox.Text, out int tagsWidth))
                {
                    config.ColTagsWidth = tagsWidth;
                }

                if (_notesWidthTextBox != null && int.TryParse(_notesWidthTextBox.Text, out int notesWidth))
                {
                    config.ColNotesWidth = notesWidth;
                }
            });
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void TagsWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings) return;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 只验证是否是有效数字，不限制范围（允许中间输入状态）
            if (int.TryParse(textBox.Text, out int value))
            {
                // 实时更新预览（可选）
                // 这里可以添加实时预览逻辑
            }
        }

        private void TagsWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (int.TryParse(textBox.Text, out int value))
            {
                // 限制范围（失去焦点时）
                if (value < 50) value = 50;
                if (value > 500) value = 500;

                // 更新文本框为有效值
                textBox.Text = value.ToString();
                ApplyTagsWidth(value); // Apply the corrected value
            }
            else
            {
                // 无效输入，恢复默认值
                textBox.Text = "150";
                ApplyTagsWidth(150); // Apply default value
            }
        }

        private void TagsWidthUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_tagsWidthTextBox.Text, out int value))
            {
                value = Math.Min(500, value + 5);
                _tagsWidthTextBox.Text = value.ToString();
                ApplyTagsWidth(value);
            }
        }

        private void TagsWidthDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_tagsWidthTextBox.Text, out int value))
            {
                value = Math.Max(50, value - 5);
                _tagsWidthTextBox.Text = value.ToString();
                ApplyTagsWidth(value);
            }
        }

        private void ApplyTagsWidth(double width)
        {
            // 使用统一配置服务更新
            ConfigurationService.Instance.Set(cfg => cfg.ColTagsWidth, width);

            // 直接刷新 FileListControl 的列宽度
            RefreshFileListColumns();

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void NotesWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingSettings) return;

            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 只验证是否是有效数字，不限制范围
            if (int.TryParse(textBox.Text, out int value))
            {
                // 实时更新预览（可选）
            }
        }

        private void NotesWidthTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            if (int.TryParse(textBox.Text, out int value))
            {
                // 限制范围（失去焦点时）
                if (value < 100) value = 100;
                if (value > 800) value = 800;

                // 更新文本框为有效值
                textBox.Text = value.ToString();
                ApplyNotesWidth(value); // Apply the corrected value
            }
            else
            {
                // 无效输入，恢复默认值
                textBox.Text = "200";
                ApplyNotesWidth(200); // Apply default value
            }
        }

        private void NotesWidthUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_notesWidthTextBox.Text, out int value))
            {
                value = Math.Min(800, value + 5);
                _notesWidthTextBox.Text = value.ToString();
                ApplyNotesWidth(value);
            }
        }

        private void NotesWidthDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(_notesWidthTextBox.Text, out int value))
            {
                value = Math.Max(100, value - 5);
                _notesWidthTextBox.Text = value.ToString();
                ApplyNotesWidth(value);
            }
        }

        private void ApplyNotesWidth(double width)
        {
            // 使用统一配置服务更新
            ConfigurationService.Instance.Set(cfg => cfg.ColNotesWidth, width);

            // 直接刷新 FileListControl 的列宽度
            RefreshFileListColumns();

            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 刷新所有 FileListControl 的列宽度
        /// </summary>
        private void RefreshFileListColumns()
        {
            try
            {
                // 查找主窗口
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow == null)
                {
                    return;
                }

                // 查找 FileBrowser
                var fileBrowser = mainWindow.FindName("FileBrowser") as FileBrowserControl;
                if (fileBrowser == null)
                {
                    return;
                }

                // 调用 FileListControl 的公共方法刷新列宽度
                var fileListControl = fileBrowser.GetFileListControl();
                if (fileListControl == null)
                {
                    return;
                }
                fileListControl.LoadColumnWidths();
            }
            catch (Exception)
            {
            }
        }
    }
}

