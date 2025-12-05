using System;
using System.Windows;
using System.Windows.Controls;
using OoiMRR.Controls;

namespace OoiMRR.Controls.Settings
{
#pragma warning disable CS0067 // 事件从未使用，但接口要求
    public partial class TagSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
#pragma warning restore CS0067
        
        public TagSettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0) };
            
            var titleText = new TextBlock
            {
                Text = "标签设置",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(titleText);
            
            var placeholderText = new TextBlock
            {
                Text = "标签相关设置功能即将推出...\n\n注：TagTrain 设置请查看 TagTrain 分类。",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 15),
                TextWrapping = TextWrapping.Wrap
            };
            stackPanel.Children.Add(placeholderText);
            
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel
            };
            
            Content = scrollViewer;
        }

        public void LoadSettings()
        {
            // 加载标签设置
        }

        public void SaveSettings()
        {
            // 保存标签设置
        }
    }
}

