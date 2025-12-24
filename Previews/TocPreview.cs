using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace OoiMRR.Previews
{
    /// <summary>
    /// TOC文件预览器 - 魔兽世界插件配置文件（支持预览/编辑/分屏模式）
    /// </summary>
    public class TocPreview
    {
        public static UIElement CreatePreview(string filePath)
        {
            try
            {
                // 解析TOC文件
                var tocData = ParseTocFile(filePath);

                // 使用Grid布局
                var mainGrid = new Grid
                {
                    Background = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 视图模式栏
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 元数据信息栏
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 内容区

                // 标题栏
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("📦", $"WoW 插件配置: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

                // 创建视图模式切换栏
                var viewModePanel = CreateViewModePanel(tocData, filePath, mainGrid);
                Grid.SetRow(viewModePanel, 1);
                mainGrid.Children.Add(viewModePanel);

                // 元数据信息栏（默认显示）
                var infoPanel = CreateInfoPanel(tocData);
                Grid.SetRow(infoPanel, 2);
                mainGrid.Children.Add(infoPanel);

                // 默认显示预览模式（源码内容）
                var sourcePanel = CreateSourcePanel(filePath);
                Grid.SetRow(sourcePanel, 3);
                mainGrid.Children.Add(sourcePanel);

                return mainGrid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载 TOC 文件: {ex.Message}\n\n调用栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 创建视图模式切换面板（预览/编辑/分屏）
        /// </summary>
        private static UIElement CreateViewModePanel(TocFileData tocData, string filePath, Grid parentGrid)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                Margin = new Thickness(10, 5, 10, 5)  // StackPanel使用Margin而非Padding
            };

            // 标签
            var label = new TextBlock
            {
                Text = "视图:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
            };
            panel.Children.Add(label);

            // 单选按钮组
            var previewRadio = new RadioButton
            {
                Content = "预览",
                GroupName = "TocViewMode",
                IsChecked = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };

            var editRadio = new RadioButton
            {
                Content = "编辑",
                GroupName = "TocViewMode",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 15, 0)
            };

            var splitRadio = new RadioButton
            {
                Content = "分屏",
                GroupName = "TocViewMode",
                VerticalAlignment = VerticalAlignment.Center
            };

            // 切换逻辑
            UIElement currentContentView = null;
            UIElement currentEditView = null;

            previewRadio.Checked += (s, e) =>
            {
                // 移除当前内容并显示预览模式（源码）
                if (currentContentView != null && parentGrid.Children.Contains(currentContentView))
                    parentGrid.Children.Remove(currentContentView);
                if (currentEditView != null && parentGrid.Children.Contains(currentEditView))
                    parentGrid.Children.Remove(currentEditView);

                var sourcePanel = CreateSourcePanel(filePath);
                Grid.SetRow(sourcePanel, 3);
                parentGrid.Children.Add(sourcePanel);
                currentContentView = sourcePanel;
            };

            editRadio.Checked += (s, e) =>
            {
                // 移除当前内容并显示编辑模式（可编辑的TextBox）
                if (currentContentView != null && parentGrid.Children.Contains(currentContentView))
                    parentGrid.Children.Remove(currentContentView);
                if (currentEditView != null && parentGrid.Children.Contains(currentEditView))
                    parentGrid.Children.Remove(currentEditView);

                var editPanel = CreateEditPanel(filePath);
                Grid.SetRow(editPanel, 3);
                parentGrid.Children.Add(editPanel);
                currentEditView = editPanel;
            };

            splitRadio.Checked += (s, e) =>
            {
                // 移除当前内容并显示分屏模式（左侧编辑，右侧预览）
                if (currentContentView != null && parentGrid.Children.Contains(currentContentView))
                    parentGrid.Children.Remove(currentContentView);
                if (currentEditView != null && parentGrid.Children.Contains(currentEditView))
                    parentGrid.Children.Remove(currentEditView);

                var splitPanel = CreateSplitPanel(filePath);
                Grid.SetRow(splitPanel, 3);
                parentGrid.Children.Add(splitPanel);
                currentContentView = splitPanel;
            };

            panel.Children.Add(previewRadio);
            panel.Children.Add(editRadio);
            panel.Children.Add(splitRadio);

            return panel;
        }

        /// <summary>
        /// 创建编辑面板（可编辑的TextBox）
        /// </summary>
        private static UIElement CreateEditPanel(string filePath)
        {
            var textBox = new TextBox
            {
                Text = File.ReadAllText(filePath),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10),
                BorderThickness = new Thickness(0)
            };

            // 保存快捷键Ctrl+S
            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.S &&
                    (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
                {
                    try
                    {
                        File.WriteAllText(filePath, textBox.Text);
                        MessageBox.Show("文件已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    e.Handled = true;
                }
            };

            return textBox;
        }

        /// <summary>
        /// 创建分屏面板（左侧编辑，右侧预览）
        /// </summary>
        private static UIElement CreateSplitPanel(string filePath)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 左侧：编辑区
            var textBox = new TextBox
            {
                Text = File.ReadAllText(filePath),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10),
                BorderThickness = new Thickness(0)
            };

            Grid.SetColumn(textBox, 0);
            grid.Children.Add(textBox);

            // 中间分隔符
            var separator = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                Margin = new Thickness(5, 0, 5, 0)
            };
            Grid.SetColumn(separator, 1);
            grid.Children.Add(separator);

            // 右侧：预览区
            var previewPanel = CreateSourcePanel(filePath);
            Grid.SetColumn(previewPanel, 2);
            grid.Children.Add(previewPanel);

            // 保存快捷键
            textBox.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.S &&
                    (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
                {
                    try
                    {
                        File.WriteAllText(filePath, textBox.Text);
                        MessageBox.Show("文件已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);

                        // 更新预览
                        grid.Children.Remove(previewPanel);
                        previewPanel = CreateSourcePanel(filePath);
                        Grid.SetColumn(previewPanel, 2);
                        grid.Children.Add(previewPanel);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    e.Handled = true;
                }
            };

            return grid;
        }

        /// <summary>
        /// 解析TOC文件
        /// </summary>
        private static TocFileData ParseTocFile(string filePath)
        {
            var data = new TocFileData
            {
                Metadata = new Dictionary<string, string>(),
                FileList = new List<string>(),
                Comments = new List<string>()
            };

            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;

                if (trimmedLine.StartsWith("##"))
                {
                    // 元数据行
                    var metadataLine = trimmedLine.Substring(2).Trim();
                    var colonIndex = metadataLine.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var key = metadataLine.Substring(0, colonIndex).Trim();
                        var value = metadataLine.Substring(colonIndex + 1).Trim();
                        data.Metadata[key] = value;
                    }
                }
                else if (trimmedLine.StartsWith("#"))
                {
                    // 注释行
                    data.Comments.Add(trimmedLine.Substring(1).Trim());
                }
                else
                {
                    // 文件路径行
                    data.FileList.Add(trimmedLine);
                }
            }

            return data;
        }

        /// <summary>
        /// 创建简洁的元数据信息栏
        /// </summary>
        private static UIElement CreateInfoPanel(TocFileData data)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                Padding = new Thickness(15, 10, 15, 10),
                Margin = new Thickness(0, 0, 0, 5)
            };

            var stackPanel = new StackPanel();

            // 基本信息行
            var title = data.Metadata.ContainsKey("Title") ? data.Metadata["Title"] : "未知";
            var version = data.Metadata.ContainsKey("Version") ? data.Metadata["Version"] : "-";
            var author = data.Metadata.ContainsKey("Author") ? data.Metadata["Author"] : "未知";
            var interfaceVer = data.Metadata.ContainsKey("Interface") ? data.Metadata["Interface"] : "-";

            var basicInfo = new TextBlock
            {
                Text = $"📦 {title}  |  版本: {version}  |  作者: {author}  |  接口: {interfaceVer}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 60))
            };
            stackPanel.Children.Add(basicInfo);

            // 如果有说明，显示在第二行
            if (data.Metadata.ContainsKey("Notes"))
            {
                var notes = new TextBlock
                {
                    Text = $"💬 {data.Metadata["Notes"]}",
                    FontSize = 11,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 5, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                stackPanel.Children.Add(notes);
            }

            // 文件统计
            var stats = new TextBlock
            {
                Text = $"📄 文件数: {data.FileList.Count}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(0, 5, 0, 0)
            };
            stackPanel.Children.Add(stats);

            border.Child = stackPanel;
            return border;
        }

        /// <summary>
        /// 创建源码面板（带语法高亮）
        /// </summary>
        private static UIElement CreateSourcePanel(string filePath)
        {
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = Brushes.White,
                Padding = new Thickness(10)
            };

            var textBlock = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,  // 增大字体
                TextWrapping = TextWrapping.NoWrap
            };

            var lines = File.ReadAllLines(filePath);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.Trim();

                // 行号
                var lineNumber = new Run($"{i + 1,4} │ ")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                    FontSize = 11
                };
                textBlock.Inlines.Add(lineNumber);

                // 语法高亮
                if (trimmedLine.StartsWith("##"))
                {
                    // 元数据行 - 蓝色粗体
                    var run = new Run(line + "\n")
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(30, 90, 180)),
                        FontWeight = FontWeights.Bold
                    };
                    textBlock.Inlines.Add(run);
                }
                else if (trimmedLine.StartsWith("#"))
                {
                    // 注释行 - 灰色斜体
                    var run = new Run(line + "\n")
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                        FontStyle = FontStyles.Italic
                    };
                    textBlock.Inlines.Add(run);
                }
                else if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    // 文件路径 - 普通文本
                    var run = new Run(line + "\n")
                    {
                        Foreground = Brushes.Black
                    };
                    textBlock.Inlines.Add(run);
                }
                else
                {
                    // 空行
                    textBlock.Inlines.Add(new Run("\n"));
                }
            }

            scrollViewer.Content = textBlock;
            return scrollViewer;
        }

        /// <summary>
        /// TOC文件数据模型
        /// </summary>
        private class TocFileData
        {
            public Dictionary<string, string> Metadata { get; set; }
            public List<string> FileList { get; set; }
            public List<string> Comments { get; set; }
        }
    }
}
