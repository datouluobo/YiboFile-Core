using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using YiboFile;
using YiboFile.Services.Config;
using YiboFile.ViewModels;

namespace YiboFile.Controls.Settings
{
    public partial class FileListSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private SettingsViewModel _viewModel;

        private TextBox _tagsWidthTextBox;
        private Button _tagsWidthUpButton;
        private Button _tagsWidthDownButton;
        private TextBox _notesWidthTextBox;
        private Button _notesWidthUpButton;
        private Button _notesWidthDownButton;

        public FileListSettingsPanel()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            this.DataContext = _viewModel;

            // Bridge ViewModel changes to SettingsChanged event and refresh columns
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsViewModel.ColTagsWidth) ||
                    e.PropertyName == nameof(SettingsViewModel.ColNotesWidth))
                {
                    RefreshFileListColumns();
                }
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };

            InitializeBindings();
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

            _tagsWidthTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;
            _tagsWidthUpButton.Click += (s, e) => AdjustValue(_viewModel.ColTagsWidth, 5, 50, 500, v => _viewModel.ColTagsWidth = v);
            _tagsWidthDownButton.Click += (s, e) => AdjustValue(_viewModel.ColTagsWidth, -5, 50, 500, v => _viewModel.ColTagsWidth = v);

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

            _notesWidthTextBox.PreviewTextInput += NumericTextBox_PreviewTextInput;
            _notesWidthUpButton.Click += (s, e) => AdjustValue(_viewModel.ColNotesWidth, 5, 100, 800, v => _viewModel.ColNotesWidth = v);
            _notesWidthDownButton.Click += (s, e) => AdjustValue(_viewModel.ColNotesWidth, -5, 100, 800, v => _viewModel.ColNotesWidth = v);

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

        private void InitializeBindings()
        {
            _tagsWidthTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.ColTagsWidth))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });

            _notesWidthTextBox.SetBinding(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.ColNotesWidth))
            {
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            });
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

        private void AdjustValue(double current, double delta, double min, double max, Action<double> setter)
        {
            double newValue = Math.Clamp(current + delta, min, max);
            setter(newValue);
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
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

        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
        }

        public void SaveSettings()
        {
            // Auto-saved by bindings
        }
    }
}
