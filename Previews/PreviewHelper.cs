using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 预览辅助类 - 提供通用的预览UI创建方法
    /// </summary>
    public static class PreviewHelper
    {
        /// <summary>
        /// 检测 QuickLook 是否安装
        /// </summary>
        public static bool IsQuickLookInstalled()
        {
            // 检查常见的 QuickLook 安装路径
            var commonPaths = new[]
            {
                @"C:\Program Files\QuickLook\QuickLook.exe",
                @"C:\Program Files (x86)\QuickLook\QuickLook.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\QuickLook\QuickLook.exe")
            };

            return commonPaths.Any(File.Exists);
        }

        /// <summary>
        /// 获取 QuickLook 可执行文件路径
        /// </summary>
        public static string GetQuickLookPath()
        {
            var commonPaths = new[]
            {
                @"C:\Program Files\QuickLook\QuickLook.exe",
                @"C:\Program Files (x86)\QuickLook\QuickLook.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\QuickLook\QuickLook.exe")
            };

            return commonPaths.FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// 创建错误预览
        /// </summary>
        public static UIElement CreateErrorPreview(string errorMessage)
        {
            return new TextBlock
            {
                Text = errorMessage,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Red,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20)
            };
        }

        /// <summary>
        /// 创建不支持预览的UI
        /// </summary>
        public static UIElement CreateNoPreview(string filePath)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = "❓ 不支持预览",
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

            panel.Children.Add(new TextBlock
            {
                Text = $"文件类型: {FileTypeManager.GetFileCategory(filePath)}",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = Brushes.Gray,
                Margin = new Thickness(10)
            });

            return panel;
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 创建标题面板
        /// </summary>
        public static Border CreateTitlePanel(string icon, string title, Button additionalButton = null)
        {
            var titlePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Padding = new Thickness(15, 10, 15, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            var titleIcon = new TextBlock
            {
                Text = icon,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            titleStack.Children.Add(titleIcon);
            titleStack.Children.Add(titleText);

            if (additionalButton != null)
            {
                additionalButton.Margin = new Thickness(15, 0, 0, 0);
                titleStack.Children.Add(additionalButton);
            }

            titlePanel.Child = titleStack;
            return titlePanel;
        }

        /// <summary>
        /// 创建打开文件按钮
        /// </summary>
        public static Button CreateOpenButton(string filePath, string buttonText = "📂 打开")
        {
            var openButton = new Button
            {
                Content = buttonText,
                Padding = new Thickness(12, 5, 12, 5),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            openButton.Click += (s, e) =>
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

            return openButton;
        }
    }
}

