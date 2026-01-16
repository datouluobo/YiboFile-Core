using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controls;

namespace YiboFile.Controls.Settings
{
#pragma warning disable CS0067 // 事件从未使用，但接口要求
    public partial class PathSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
#pragma warning restore CS0067
        
        public PathSettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0) };
            
            var titleText = new TextBlock
            {
                Text = "路径设置",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(titleText);
            
            var placeholderText = new TextBlock
            {
                Text = "路径相关设置功能即将推出...",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 0, 0, 15)
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
            // 加载路径设置
        }

        public void SaveSettings()
        {
            // 保存路径设置
        }
    }
}


