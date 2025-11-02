using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 音频文件预览
    /// </summary>
    public class AudioPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = "🎵 音频文件",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            });

            panel.Children.Add(new TextBlock
            {
                Text = Path.GetFileName(filePath),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            });

            var button = new Button
            {
                Content = "🎵 打开音频文件",
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(10),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            button.Click += (s, e) =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            panel.Children.Add(button);

            return panel;
        }
    }
}

