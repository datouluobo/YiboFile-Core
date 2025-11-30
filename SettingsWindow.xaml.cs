using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using TagTrain.Services;

namespace OoiMRR
{
    public partial class SettingsWindow : Window
    {
        private bool _tagTrainSettingsLoaded = false;

        public SettingsWindow()
        {
            InitializeComponent();
            this.KeyDown += SettingsWindow_KeyDown;
            
            // 延迟加载TagTrain设置，确保窗口已完全初始化
            this.Loaded += SettingsWindow_Loaded;
        }

        private void SettingsWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Cancel_Click(null, null);
            }
        }

        private void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 当切换到标签设置页时加载TagTrain设置
            SettingsTabControl.SelectionChanged += SettingsTabControl_SelectionChanged;
            
            // 如果默认选中标签页，立即加载
            if (SettingsTabControl.SelectedItem == TagSettingsTab)
            {
                LoadTagTrainSettings();
            }
        }

        private void SettingsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsTabControl.SelectedItem == TagSettingsTab && !_tagTrainSettingsLoaded)
            {
                LoadTagTrainSettings();
            }
        }

        private System.Windows.Controls.TextBox _tagTrainImageDirectoryTextBox;
        private System.Windows.Controls.TextBox _tagTrainDataStoragePathTextBox;
        private TextBlock _tagTrainStatusText;
        private Slider _tagTrainTagsPerRowSlider;
        private TextBlock _tagTrainTagsPerRowValue;
        private Slider _tagTrainPredictionThresholdSlider;
        private TextBlock _tagTrainPredictionThresholdValue;

        private void LoadTagTrainSettings()
        {
            if (_tagTrainSettingsLoaded) return;

            try
            {
                if (App.IsTagTrainAvailable)
                {
                    TagSettingsContainer.Children.Clear();
                    
                    var scrollViewer = new ScrollViewer
                    {
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Padding = new Thickness(10)
                    };
                    
                    var mainPanel = new StackPanel();
                    
                    // 标题
                    var titleText = new TextBlock
                    {
                        Text = "标签训练设置",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    mainPanel.Children.Add(titleText);
                    
                    // 图片目录
                    var imageDirLabel = new System.Windows.Controls.Label { Content = "图片目录:", FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
                    mainPanel.Children.Add(imageDirLabel);
                    
                    var imageDirGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                    imageDirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    imageDirGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    
                    _tagTrainImageDirectoryTextBox = new System.Windows.Controls.TextBox
                    {
                        MinHeight = 28,
                        FontSize = 13,
                        Padding = new Thickness(8, 4, 8, 4),
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    _tagTrainImageDirectoryTextBox.TextChanged += TagTrainImageDirectoryTextBox_TextChanged;
                    Grid.SetColumn(_tagTrainImageDirectoryTextBox, 0);
                    imageDirGrid.Children.Add(_tagTrainImageDirectoryTextBox);
                    
                    var browseBtn = new System.Windows.Controls.Button
                    {
                        Content = "浏览...",
                        Width = 80,
                        Height = 30
                    };
                    browseBtn.Click += TagTrainBrowseImageDirectory_Click;
                    Grid.SetColumn(browseBtn, 1);
                    imageDirGrid.Children.Add(browseBtn);
                    
                    mainPanel.Children.Add(imageDirGrid);
                    
                    _tagTrainStatusText = new TextBlock
                    {
                        Foreground = System.Windows.Media.Brushes.Gray,
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    mainPanel.Children.Add(_tagTrainStatusText);
                    
                    // 数据保存路径
                    var dataStorageLabel = new System.Windows.Controls.Label { Content = "设置及数据保存路径:", FontSize = 14, Margin = new Thickness(0, 0, 0, 8) };
                    mainPanel.Children.Add(dataStorageLabel);
                    
                    var dataStorageGrid = new Grid { Margin = new Thickness(0, 0, 0, 15) };
                    dataStorageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    dataStorageGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    
                    _tagTrainDataStoragePathTextBox = new System.Windows.Controls.TextBox
                    {
                        MinHeight = 28,
                        FontSize = 13,
                        Padding = new Thickness(8, 4, 8, 4),
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0),
                        IsReadOnly = true
                    };
                    Grid.SetColumn(_tagTrainDataStoragePathTextBox, 0);
                    dataStorageGrid.Children.Add(_tagTrainDataStoragePathTextBox);
                    
                    var selectDataStorageBtn = new System.Windows.Controls.Button
                    {
                        Content = "选择文件夹",
                        Width = 100,
                        Height = 30
                    };
                    selectDataStorageBtn.Click += TagTrainSelectDataStorageFolder_Click;
                    Grid.SetColumn(selectDataStorageBtn, 1);
                    dataStorageGrid.Children.Add(selectDataStorageBtn);
                    
                    mainPanel.Children.Add(dataStorageGrid);
                    
                    var dataStorageInfo = new TextBlock
                    {
                        Text = "程序将从此目录加载数据: settings.txt, training.db, model.zip（不迁移旧数据）",
                        FontSize = 11,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 15)
                    };
                    mainPanel.Children.Add(dataStorageInfo);
                    
                    // 标签显示设置
                    var displaySettingsLabel = new System.Windows.Controls.Label
                    {
                        Content = "标签显示设置:",
                        FontSize = 14,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    mainPanel.Children.Add(displaySettingsLabel);
                    
                    // 每行显示标签数
                    var tagsPerRowGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                    tagsPerRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    tagsPerRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    tagsPerRowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    
                    var tagsPerRowLabel = new System.Windows.Controls.Label
                    {
                        Content = "每行显示标签数:",
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    Grid.SetColumn(tagsPerRowLabel, 0);
                    tagsPerRowGrid.Children.Add(tagsPerRowLabel);
                    
                    _tagTrainTagsPerRowSlider = new Slider
                    {
                        Minimum = 1,
                        Maximum = 10,
                        TickFrequency = 1,
                        IsSnapToTickEnabled = true,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    _tagTrainTagsPerRowSlider.ValueChanged += (s, e) => _tagTrainTagsPerRowValue.Text = ((int)_tagTrainTagsPerRowSlider.Value).ToString();
                    Grid.SetColumn(_tagTrainTagsPerRowSlider, 1);
                    tagsPerRowGrid.Children.Add(_tagTrainTagsPerRowSlider);
                    
                    _tagTrainTagsPerRowValue = new TextBlock
                    {
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        MinWidth = 30
                    };
                    Grid.SetColumn(_tagTrainTagsPerRowValue, 2);
                    tagsPerRowGrid.Children.Add(_tagTrainTagsPerRowValue);
                    
                    mainPanel.Children.Add(tagsPerRowGrid);
                    
                    // 预测阈值
                    var thresholdGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                    thresholdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    thresholdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    thresholdGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    
                    var thresholdLabel = new System.Windows.Controls.Label
                    {
                        Content = "预测阈值（%）:",
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    Grid.SetColumn(thresholdLabel, 0);
                    thresholdGrid.Children.Add(thresholdLabel);
                    
                    _tagTrainPredictionThresholdSlider = new Slider
                    {
                        Minimum = 0,
                        Maximum = 100,
                        TickFrequency = 5,
                        IsSnapToTickEnabled = true,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    _tagTrainPredictionThresholdSlider.ValueChanged += (s, e) => _tagTrainPredictionThresholdValue.Text = ((int)_tagTrainPredictionThresholdSlider.Value).ToString();
                    Grid.SetColumn(_tagTrainPredictionThresholdSlider, 1);
                    thresholdGrid.Children.Add(_tagTrainPredictionThresholdSlider);
                    
                    _tagTrainPredictionThresholdValue = new TextBlock
                    {
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        MinWidth = 40
                    };
                    Grid.SetColumn(_tagTrainPredictionThresholdValue, 2);
                    thresholdGrid.Children.Add(_tagTrainPredictionThresholdValue);
                    
                    mainPanel.Children.Add(thresholdGrid);
                    
                    // 加载设置值
                    LoadTagTrainSettingsValues();
                    
                    scrollViewer.Content = mainPanel;
                    TagSettingsContainer.Children.Add(scrollViewer);
                }
                else
                {
                    var stackPanel = new StackPanel { Margin = new Thickness(10) };
                    var textBlock = new TextBlock
                    {
                        Text = "TagTrain 不可用，无法使用标签训练功能。",
                        Foreground = System.Windows.Media.Brushes.Red,
                        TextWrapping = TextWrapping.Wrap
                    };
                    stackPanel.Children.Add(textBlock);
                    TagSettingsContainer.Children.Add(stackPanel);
                }
                
                _tagTrainSettingsLoaded = true;
            }
            catch (Exception ex)
            {
                var errorPanel = new StackPanel { Margin = new Thickness(10) };
                var errorText = new TextBlock
                {
                    Text = $"加载TagTrain设置失败: {ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                };
                errorPanel.Children.Add(errorText);
                TagSettingsContainer.Children.Add(errorPanel);
            }
        }

        private void LoadTagTrainSettingsValues()
        {
            try
            {
                if (!App.IsTagTrainAvailable) return;
                
                SettingsManager.ClearCache();
                
                // 加载图片目录
                var imageDir = SettingsManager.GetImageDirectory();
                if (_tagTrainImageDirectoryTextBox != null)
                {
                    _tagTrainImageDirectoryTextBox.Text = imageDir ?? "";
                    if (!string.IsNullOrEmpty(imageDir))
                        ValidateTagTrainImageDirectory();
                }
                
                // 加载数据保存目录
                var storageDir = SettingsManager.GetDataStorageDirectory();
                if (_tagTrainDataStoragePathTextBox != null)
                    _tagTrainDataStoragePathTextBox.Text = storageDir ?? "";
                
                // 加载标签显示设置
                var tagsPerRow = SettingsManager.GetTagsPerRow();
                if (_tagTrainTagsPerRowSlider != null)
                {
                    _tagTrainTagsPerRowSlider.Value = tagsPerRow;
                    if (_tagTrainTagsPerRowValue != null)
                        _tagTrainTagsPerRowValue.Text = tagsPerRow.ToString();
                }
                
                var threshold = SettingsManager.GetPredictionThreshold();
                if (_tagTrainPredictionThresholdSlider != null)
                {
                    _tagTrainPredictionThresholdSlider.Value = threshold;
                    if (_tagTrainPredictionThresholdValue != null)
                        _tagTrainPredictionThresholdValue.Text = ((int)threshold).ToString();
                }
            }
            catch { }
        }

        private void ValidateTagTrainImageDirectory()
        {
            if (_tagTrainStatusText == null || _tagTrainImageDirectoryTextBox == null) return;
            
            var path = _tagTrainImageDirectoryTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(path))
            {
                _tagTrainStatusText.Text = "";
                return;
            }

            if (!Directory.Exists(path))
            {
                _tagTrainStatusText.Text = "❌ 目录不存在";
                _tagTrainStatusText.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
            var hasImages = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                .Any(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()));

            if (hasImages)
            {
                _tagTrainStatusText.Text = "✅ 目录有效，包含图片文件";
                _tagTrainStatusText.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                _tagTrainStatusText.Text = "⚠️ 目录中未找到图片文件";
                _tagTrainStatusText.Foreground = System.Windows.Media.Brushes.Orange;
            }
        }

        private void TagTrainImageDirectoryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateTagTrainImageDirectory();
        }

        private void TagTrainBrowseImageDirectory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "选择包含图片的目录"
            };
            
            if (_tagTrainImageDirectoryTextBox != null && !string.IsNullOrEmpty(_tagTrainImageDirectoryTextBox.Text) && Directory.Exists(_tagTrainImageDirectoryTextBox.Text))
            {
                dialog.SelectedPath = _tagTrainImageDirectoryTextBox.Text;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (_tagTrainImageDirectoryTextBox != null)
                    _tagTrainImageDirectoryTextBox.Text = dialog.SelectedPath;
                ValidateTagTrainImageDirectory();
            }
        }

        private void TagTrainSelectDataStorageFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "选择数据目录（程序将从此目录加载数据，不迁移旧数据）"
            };
            
            if (_tagTrainDataStoragePathTextBox != null && !string.IsNullOrEmpty(_tagTrainDataStoragePathTextBox.Text) && Directory.Exists(_tagTrainDataStoragePathTextBox.Text))
            {
                dialog.SelectedPath = _tagTrainDataStoragePathTextBox.Text;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (_tagTrainDataStoragePathTextBox != null)
                    _tagTrainDataStoragePathTextBox.Text = dialog.SelectedPath;
            }
        }


        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // 保存TagTrain设置
            if (App.IsTagTrainAvailable && _tagTrainSettingsLoaded)
            {
                try
                {
                    // 保存图片目录
                    if (_tagTrainImageDirectoryTextBox != null)
                    {
                        var imageDir = _tagTrainImageDirectoryTextBox.Text.Trim();
                        if (!string.IsNullOrEmpty(imageDir))
                        {
                            if (!Directory.Exists(imageDir))
                            {
                                System.Windows.MessageBox.Show("图片目录不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            SettingsManager.SetImageDirectory(imageDir);
                        }
                    }
                    
                    // 保存标签显示设置
                    if (_tagTrainTagsPerRowSlider != null)
                    {
                        SettingsManager.SetTagsPerRow((int)_tagTrainTagsPerRowSlider.Value);
                    }
                    
                    if (_tagTrainPredictionThresholdSlider != null)
                    {
                        SettingsManager.SetPredictionThreshold(_tagTrainPredictionThresholdSlider.Value);
                    }
                    
                    // 保存数据保存目录
                    if (_tagTrainDataStoragePathTextBox != null)
                    {
                        var newStorageDir = _tagTrainDataStoragePathTextBox.Text.Trim();
                        if (!string.IsNullOrEmpty(newStorageDir))
                        {
                            if (!Directory.Exists(newStorageDir))
                            {
                                System.Windows.MessageBox.Show("选择的目录不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                            
                            var currentStorageDir = SettingsManager.GetDataStorageDirectory();
                            var isDirectoryChanged = !Path.GetFullPath(newStorageDir).Equals(Path.GetFullPath(currentStorageDir), StringComparison.OrdinalIgnoreCase);
                            
                            if (isDirectoryChanged)
                            {
                                Directory.CreateDirectory(newStorageDir);
                                SettingsManager.SetDataStorageDirectory(newStorageDir);
                                SettingsManager.ClearCache();
                                DataManager.ClearDatabasePathCache();
                                
                                System.Windows.MessageBox.Show("已切换到新数据目录\n\n程序将加载此目录中的数据（settings.txt, training.db, model.zip）\n\n请重新启动程序以确保所有连接使用新路径。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                    
                    SettingsManager.ClearCache();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"保存TagTrain设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

