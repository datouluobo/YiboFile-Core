using System;
using System.Windows;
using System.Windows.Controls;

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

        private void LoadTagTrainSettings()
        {
            if (_tagTrainSettingsLoaded) return;

            try
            {
                if (App.IsTagTrainAvailable)
                {
                    // 尝试创建TagTrain的配置窗口并获取其内容
                    // 由于TagTrain.ConfigWindow是一个完整窗口，我们需要采用不同的方法
                    // 方案：在标签页中显示一个按钮，点击后打开TagTrain设置窗口
                    TagSettingsContainer.Children.Clear();
                    
                    var stackPanel = new StackPanel { Margin = new Thickness(10) };
                    
                    var titleText = new TextBlock
                    {
                        Text = "标签训练设置",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    stackPanel.Children.Add(titleText);
                    
                    var infoText = new TextBlock
                    {
                        Text = "点击下方按钮打开TagTrain设置窗口，配置标签训练相关参数。",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 0, 0, 20),
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(100, 100, 100))
                    };
                    stackPanel.Children.Add(infoText);
                    
                    var openSettingsBtn = new Button
                    {
                        Content = "打开TagTrain设置",
                        Padding = new Thickness(20, 10, 20, 10),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        MinWidth = 200,
                        FontSize = 14
                    };
                    openSettingsBtn.Click += OpenTagTrainSettings_Click;
                    stackPanel.Children.Add(openSettingsBtn);
                    
                    TagSettingsContainer.Children.Add(stackPanel);
                }
                else
                {
                    var stackPanel = new StackPanel { Margin = new Thickness(10) };
                    var textBlock = new TextBlock
                    {
                        Text = "TagTrain 不可用，无法使用标签训练功能。",
                        Foreground = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(200, 0, 0)),
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
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(200, 0, 0)),
                    TextWrapping = TextWrapping.Wrap
                };
                errorPanel.Children.Add(errorText);
                TagSettingsContainer.Children.Add(errorPanel);
            }
        }

        private void OpenTagTrainSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (App.IsTagTrainAvailable)
                {
                    // 清理缓存
                    try { TagTrain.Services.SettingsManager.ClearCache(); } catch { }
                    
                    // 打开TagTrain的配置窗口
                    var configWindow = new TagTrain.UI.ConfigWindow();
                    configWindow.Owner = this;
                    var result = configWindow.ShowDialog();
                    
                    // 如果用户保存了设置，可以在这里处理后续操作
                    if (result == true)
                    {
                        try
                        {
                            TagTrain.Services.SettingsManager.ClearCache();
                            TagTrain.Services.DataManager.ClearDatabasePathCache();
                        }
                        catch { }
                        
                        MessageBox.Show("TagTrain设置已保存。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开TagTrain设置失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            // 这里可以添加保存设置的逻辑
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

