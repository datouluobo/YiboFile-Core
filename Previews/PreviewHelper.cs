using System;
using System.Collections.Generic;
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
        /// 编辑模式背景色（浅蓝色）
        /// </summary>
        public static readonly Brush EditModeBackground = new SolidColorBrush(Color.FromRgb(230, 240, 255));

        /// <summary>
        /// 只读模式背景色（白色）
        /// </summary>
        public static readonly Brush ReadOnlyBackground = Brushes.White;
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
        /// 创建标题面板（统一样式，支持多个按钮）
        /// </summary>
        public static Border CreateTitlePanel(string icon, string title, IEnumerable<Button> actionButtons = null)
        {
            // 统一的标题栏样式
            var titlePanel = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Padding = new Thickness(15, 10, 15, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch // 确保充满宽度
            };

            // 使用 DockPanel 布局：确保按钮靠右，标题靠左占满剩余
            var dockPanel = new DockPanel { LastChildFill = true };

            // 1. 右侧按钮区域（先添加，Dock.Right）
            if (actionButtons != null && actionButtons.Any())
            {
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };

                foreach (var button in actionButtons)
                {
                    // 统一按钮样式
                    if (button.Margin.Left == 0 && button.Margin.Right == 0) // 避免覆盖已有Margin
                    {
                        button.Margin = new Thickness(5, 0, 0, 0);
                    }
                    button.Padding = new Thickness(12, 6, 12, 6);
                    button.FontSize = 13;
                    button.Cursor = System.Windows.Input.Cursors.Hand;
                    buttonPanel.Children.Add(button);
                }

                DockPanel.SetDock(buttonPanel, Dock.Right);
                dockPanel.Children.Add(buttonPanel);
            }

            // 2. 左侧标题区域（LastChildFill=True，自动填充剩余空间）
            var titleStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var titleIcon = new TextBlock
            {
                Text = icon,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol")
            };

            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI"),
                TextTrimming = TextTrimming.CharacterEllipsis, // 长文本自动省略
                TextWrapping = TextWrapping.NoWrap
            };

            titleStack.Children.Add(titleIcon);
            titleStack.Children.Add(titleText);

            dockPanel.Children.Add(titleStack);

            titlePanel.Child = dockPanel;
            return titlePanel;
        }

        /// <summary>
        /// 创建打开文件按钮（保留用于特殊场景）
        /// </summary>
        public static Button CreateOpenButton(string filePath, string buttonText = "📂 外部程序打开")
        {
            var openButton = new Button
            {
                Content = buttonText,
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13
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

        /// <summary>
        /// 创建编辑按钮（用于可编辑的文件类型）
        /// </summary>
        public static Button CreateEditButton(Action onEditToggle, bool isEditMode = false, string editText = "✏️ 编辑", string saveText = "💾 保存")
        {
            var editButton = new Button
            {
                Content = isEditMode ? saveText : editText,
                Padding = new Thickness(12, 6, 12, 6),
                Background = isEditMode
                    ? new SolidColorBrush(Color.FromRgb(76, 175, 80))  // 保存时绿色
                    : new SolidColorBrush(Color.FromRgb(33, 150, 243)), // 编辑时蓝色
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13
            };

            editButton.Click += (s, e) =>
            {
                try
                {
                    onEditToggle?.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            return editButton;
        }

        /// <summary>
        /// 创建打开文件夹按钮（在本程序的新标签页中打开）
        /// </summary>
        public static Button CreateOpenFolderButton(string folderPath, string buttonText = "📂 打开文件夹")
        {
            var button = new Button
            {
                Content = buttonText,
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13
            };

            button.Click += (s, e) =>
            {
                try
                {
                    // 使用回调在本程序的新标签页中打开文件夹
                    if (PreviewFactory.OnOpenFolderInNewTab != null)
                    {
                        PreviewFactory.OnOpenFolderInNewTab(folderPath);
                    }
                    else
                    {
                        // 如果回调未设置，回退到使用系统默认文件管理器
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = folderPath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件夹: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            return button;
        }

        /// <summary>
        /// 创建DOC转DOCX按钮
        /// </summary>
        public static Button CreateDocToDocxButton(string docPath, Action<string> onConvert = null, string buttonText = "🔄 DOC转DOCX")
        {
            var button = new Button
            {
                Content = buttonText,
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13
            };

            button.Click += (s, e) =>
            {
                try
                {
                    if (onConvert != null)
                    {
                        onConvert(docPath);
                    }
                    else
                    {
                        MessageBox.Show("DOC转DOCX功能需要安装Microsoft Word或兼容组件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            return button;
        }

        /// <summary>
        /// 创建HTML渲染/源码切换按钮
        /// </summary>
        public static Button CreateHtmlViewToggleButton(Action onToggle, string currentText = "📄 源码", string toggleText = "🎨 渲染")
        {
            var button = new Button
            {
                Content = currentText,
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                FontSize = 13
            };

            button.Click += (s, e) =>
            {
                try
                {
                    // 切换按钮文本
                    var temp = button.Content;
                    button.Content = toggleText;
                    toggleText = temp.ToString();
                    onToggle?.Invoke();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"切换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            return button;
        }
    }
}

