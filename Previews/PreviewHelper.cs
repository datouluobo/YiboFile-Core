using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace YiboFile.Previews
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
        /// 创建统一的加载遮罩层
        /// </summary>
        public static Grid CreateLoadingPanel(string message = "加载中...")
        {
            var grid = new Grid
            {
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Visible,
                Name = "LoadingPanel" // 方便查找
            };

            var stackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 如果项目中有预定义的 Loading 动画控件可以使用，暂时使用 ProgressBar 模拟
            var progressBar = new ProgressBar
            {
                IsIndeterminate = true,
                Width = 150,
                Height = 4,
                Margin = new Thickness(0, 0, 0, 15),
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)) // 蓝色
            };

            var textBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stackPanel.Children.Add(progressBar);
            stackPanel.Children.Add(textBlock);
            grid.Children.Add(stackPanel);

            return grid;
        }

        /// <summary>
        /// 创建通用信息面板（用于错误、空状态、不支持预览等）
        /// </summary>
        private static UIElement CreateGenericMessagePanel(string icon, string mainText, string subText = null, Brush color = null)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = icon,
                FontSize = 48, // 更大的图标
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
                Foreground = color ?? Brushes.Gray,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol")
            });

            panel.Children.Add(new TextBlock
            {
                Text = mainText,
                FontSize = 18,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50)),
                Margin = new Thickness(10, 0, 10, 10),
                TextWrapping = TextWrapping.Wrap
            });

            if (!string.IsNullOrEmpty(subText))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = subText,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(20, 0, 20, 0),
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center
                });
            }

            return panel;
        }

        /// <summary>
        /// 创建统一的错误提示
        /// </summary>
        public static UIElement CreateErrorPreview(string errorMessage)
        {
            return CreateGenericMessagePanel("❌", "发生错误", errorMessage, Brushes.Red);
        }

        /// <summary>
        /// 创建空状态提示（如空文件夹、空压缩包）
        /// </summary>
        public static UIElement CreateEmptyPreview(string message = "没有内容")
        {
            return CreateGenericMessagePanel("📭", message);
        }

        /// <summary>
        /// 创建信息提示（用于提示性消息，非错误）
        /// </summary>
        public static UIElement CreateInfoPreview(string title, string message)
        {
            return CreateGenericMessagePanel("📦", title, message, new SolidColorBrush(Color.FromRgb(100, 149, 237))); // CornflowerBlue
        }

        /// <summary>
        /// 创建不支持预览的UI
        /// </summary>
        public static UIElement CreateNoPreview(string filePath)
        {
            string category = FileTypeManager.GetFileCategory(filePath);
            return CreateGenericMessagePanel("❓", "不支持预览", $"{Path.GetFileName(filePath)}\n类型: {category}");
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
        public static Button CreateOpenButton(string filePath, string buttonText = "📂 打开")
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
        /// 使用系统默认程序打开文件
        /// </summary>
        public static void OpenInDefaultApp(string filePath)
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
        }

        /// <summary>
        /// 打开文件夹（优先在新标签页打开，否则使用资源管理器）
        /// </summary>
        public static void OpenFolderInExplorer(string folderPath)
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

        /// <summary>
        /// 创建统一的转换按钮
        /// </summary>
        public static Button CreateConvertButton(string content, RoutedEventHandler onClick)
        {
            var button = new Button
            {
                Content = content,
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13,
                Margin = new Thickness(0, 0, 5, 0)
            };
            button.Click += onClick;
            return button;
        }

        /// <summary>
        /// 创建统一的旧格式/不支持预览提示面板（如 PPT/XLS）
        /// </summary>
        public static StackPanel CreateLegacyFormatPanel(string formatName, string description, bool canConvert, string convertButtonName)
        {
            var contentPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(40)
            };

            var warningIcon = new TextBlock
            {
                Text = "⚠️",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 20)
            };

            var titleText = new TextBlock
            {
                Text = $"{formatName} 格式说明",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var infoText = new TextBlock
            {
                Text = description,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                LineHeight = 22,
                Margin = new Thickness(0, 0, 0, 20)
            };

            contentPanel.Children.Add(warningIcon);
            contentPanel.Children.Add(titleText);
            contentPanel.Children.Add(infoText);

            // 推荐方案
            var solutionsPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(20, 0, 20, 0)
            };

            var solutionsTitle = new TextBlock
            {
                Text = "💡 推荐方案：",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            solutionsPanel.Children.Add(solutionsTitle);

            if (canConvert)
            {
                // 方案1：自动转换
                solutionsPanel.Children.Add(new TextBlock
                {
                    Text = $"✅ 自动转换：点击上方\"{convertButtonName}\"按钮",
                    FontSize = 12,
                    Margin = new Thickness(0, 5, 0, 5),
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                // 无转换器的情况
                solutionsPanel.Children.Add(new TextBlock
                {
                    Text = "❌ 未检测到转换工具，无法自动转换",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 5, 0, 10),
                    TextWrapping = TextWrapping.Wrap
                });
            }

            // 方案2：手动转换
            solutionsPanel.Children.Add(new TextBlock
            {
                Text = $"🔧 手动转换：在 Office 中打开，选择\"另存为\" → 新格式 (如 .xlsx/.pptx/.docx)",
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            });

            // 方案3：在线预览
            solutionsPanel.Children.Add(new TextBlock
            {
                Text = "🌐 在线预览：上传到 OneDrive 后使用 Office Online 打开",
                FontSize = 12,
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            });

            contentPanel.Children.Add(solutionsPanel);
            return contentPanel;
        }
    }
}


