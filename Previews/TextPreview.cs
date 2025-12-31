using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 文本文件预览
    /// </summary>
    public class TextPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();

                // 特殊处理TOC文件（魔兽世界插件配置）
                if (extension == ".toc")
                {
                    return TocPreview.CreatePreview(filePath);
                }

                string content = null;
                Encoding currentEncoding = Encoding.UTF8;
                string currentEncodingName = "UTF-8";

                var encodings = new List<Encoding>
                {
                    Encoding.UTF8,
                    Encoding.Default
                };

                // 尝试添加中文字符编码，如果系统支持
                try { encodings.Add(Encoding.GetEncoding("GB2312")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("GBK")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("GB18030")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("UTF-16LE")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("UTF-16BE")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("UTF-32LE")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("UTF-32BE")); } catch { }

                Exception lastException = null;
                foreach (var encoding in encodings)
                {
                    try
                    {
                        // 先读取字节判断是否为文本文件
                        byte[] bytes;

                        // 检查文件大小，如果太大只读取前一部分
                        var fileInfo = new FileInfo(filePath);
                        int maxBytes = 100 * 1024; // 最多读取100KB
                        if (fileInfo.Length > maxBytes)
                        {
                            bytes = new byte[maxBytes];
                            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                fs.Read(bytes, 0, maxBytes);
                            }
                        }
                        else
                        {
                            bytes = File.ReadAllBytes(filePath);
                        }

                        // 尝试用当前编码解码
                        content = encoding.GetString(bytes);

                        // 检查是否包含大量无效字符（可能是二进制文件）
                        int nullCount = 0;
                        int controlCount = 0;
                        foreach (char c in content)
                        {
                            if (c == '\0') nullCount++;
                            if (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t') controlCount++;
                        }

                        // 如果包含过多空字符或控制字符，可能是二进制文件
                        if (nullCount > content.Length * 0.01 || controlCount > content.Length * 0.1)
                        {
                            content = null;
                            continue;
                        }

                        // 成功读取,记录编码
                        currentEncoding = encoding;
                        currentEncodingName = GetEncodingDisplayName(encoding);
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        content = null;
                    }
                }

                if (string.IsNullOrEmpty(content))
                {
                    if (lastException != null)
                    {
                        return PreviewHelper.CreateErrorPreview($"无法读取文本文件: {lastException.Message}");
                    }
                    return PreviewHelper.CreateErrorPreview("文件可能不是文本文件或编码无法识别");
                }

                var maxLength = 2000;
                bool isTruncated = false;
                if (content.Length > maxLength)
                {
                    content = content.Substring(0, maxLength) + "\n\n... (文件内容过长，仅显示前2000个字符)";
                    isTruncated = true;
                }

                // 创建主容器
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 使用可编辑的 TextBox
                bool isWrapEnabled = true;
                var textBox = new TextBox
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    IsReadOnly = true,
                    Margin = new Thickness(5),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.White,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    AcceptsReturn = true,
                    AcceptsTab = true
                };

                bool isEditMode = false;
                string originalContent = content;

                // 先声明按钮变量
                Button editButton = null;
                Button wrapButton = null;
                ComboBox encodingComboBox = null;

                // 编辑/保存按钮
                editButton = PreviewHelper.CreateEditButton(
                    () =>
                    {
                        if (isEditMode)
                        {
                            // 保存模式
                            try
                            {
                                // 确定编码（优先使用UTF-8）
                                Encoding encoding = Encoding.UTF8;
                                try
                                {
                                    // 尝试检测原始编码
                                    var originalBytes = File.ReadAllBytes(filePath);
                                    if (originalBytes.Length > 0)
                                    {
                                        // 简单检测：如果前3个字节是UTF-8 BOM
                                        if (originalBytes.Length >= 3 && originalBytes[0] == 0xEF && originalBytes[1] == 0xBB && originalBytes[2] == 0xBF)
                                        {
                                            encoding = new UTF8Encoding(true);
                                        }
                                        else
                                        {
                                            // 尝试用UTF-8解码，如果失败则使用默认编码
                                            try
                                            {
                                                Encoding.UTF8.GetString(originalBytes);
                                                encoding = Encoding.UTF8;
                                            }
                                            catch
                                            {
                                                encoding = Encoding.Default;
                                            }
                                        }
                                    }
                                }
                                catch { }

                                // 使用当前选择的编码保存文件
                                File.WriteAllText(filePath, textBox.Text, currentEncoding);

                                // 更新原始内容
                                originalContent = textBox.Text;

                                // 切换为只读模式
                                textBox.IsReadOnly = true;
                                textBox.Background = PreviewHelper.ReadOnlyBackground;
                                isEditMode = false;

                                // 更新按钮
                                if (editButton != null)
                                {
                                    editButton.Content = "✏️ 编辑";
                                    editButton.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                                }

                                MessageBox.Show("文件已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            // 编辑模式
                            textBox.IsReadOnly = false;
                            textBox.Background = PreviewHelper.EditModeBackground; // 浅蓝色背景表示可编辑
                            isEditMode = true;

                            // 更新按钮
                            if (editButton != null)
                            {
                                editButton.Content = "💾 保存";
                                editButton.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                            }
                        }
                    },
                    false
                );

                // 创建自动换行切换按钮
                wrapButton = new Button
                {
                    Content = "📃 自动换行",
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = new SolidColorBrush(Color.FromRgb(156, 39, 176)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontSize = 13
                };

                wrapButton.Click += (s, e) =>
                {
                    isWrapEnabled = !isWrapEnabled;
                    textBox.TextWrapping = isWrapEnabled ? TextWrapping.Wrap : TextWrapping.NoWrap;
                    wrapButton.Content = isWrapEnabled ? "📃 自动换行" : "📄 不换行";
                };

                // 创建编码选择ComboBox
                encodingComboBox = new ComboBox
                {
                    Width = 120,
                    FontSize = 12,
                    Margin = new Thickness(5, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };

                // 添加常用编码选项
                var encodingOptions = new[]
                {
                    "UTF-8",
                    "UTF-8 (BOM)",
                    "GBK",
                    "GB2312",
                    "GB18030",
                    "UTF-16 LE",
                    "UTF-16 BE",
                    "ASCII",
                    "系统默认"
                };

                foreach (var option in encodingOptions)
                {
                    encodingComboBox.Items.Add(option);
                }

                encodingComboBox.SelectedItem = currentEncodingName;

                encodingComboBox.SelectionChanged += (s, e) =>
                {
                    if (encodingComboBox.SelectedItem == null) return;

                    try
                    {
                        var selectedEncoding = GetEncodingFromName(encodingComboBox.SelectedItem.ToString());
                        if (selectedEncoding == null) return;

                        // 重新读取文件
                        byte[] bytes;
                        var fileInfo = new FileInfo(filePath);
                        int maxBytes = 100 * 1024;

                        if (fileInfo.Length > maxBytes)
                        {
                            bytes = new byte[maxBytes];
                            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                fs.Read(bytes, 0, maxBytes);
                            }
                        }
                        else
                        {
                            bytes = File.ReadAllBytes(filePath);
                        }

                        string newContent = selectedEncoding.GetString(bytes);

                        var maxLength = 2000;
                        if (newContent.Length > maxLength)
                        {
                            newContent = newContent.Substring(0, maxLength) + "\n\n... (文件内容过长,仅显示前2000个字符)";
                        }

                        textBox.Text = newContent;
                        originalContent = newContent;
                        currentEncoding = selectedEncoding;
                        currentEncodingName = GetEncodingDisplayName(selectedEncoding);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"使用所选编码重新加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                // 标题栏 - 创建自定义布局以包含ComboBox
                var titlePanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                    Padding = new Thickness(15, 10, 15, 10),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var dockPanel = new DockPanel { LastChildFill = true };

                // 右侧按钮区域
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };

                buttonPanel.Children.Add(editButton);
                buttonPanel.Children.Add(wrapButton);
                buttonPanel.Children.Add(encodingComboBox);
                buttonPanel.Children.Add(PreviewHelper.CreateOpenButton(filePath));

                DockPanel.SetDock(buttonPanel, Dock.Right);
                dockPanel.Children.Add(buttonPanel);

                // 左侧标题
                var titleStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };

                titleStack.Children.Add(new TextBlock
                {
                    Text = "📄",
                    FontSize = 18,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol")
                });

                titleStack.Children.Add(new TextBlock
                {
                    Text = $"文本文件: {Path.GetFileName(filePath)}",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Segoe UI"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    TextWrapping = TextWrapping.NoWrap
                });

                dockPanel.Children.Add(titleStack);
                titlePanel.Child = dockPanel;

                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);

                // 设置自定义右键菜单，只包含复制（去掉剪切和粘贴）
                var contextMenu = new ContextMenu();
                var copyItem = new MenuItem
                {
                    Header = "复制",
                    InputGestureText = "Ctrl+C"
                };
                copyItem.Click += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(textBox.SelectedText))
                    {
                        Clipboard.SetText(textBox.SelectedText);
                    }
                    else
                    {
                        Clipboard.SetText(textBox.Text);
                    }
                };
                contextMenu.Items.Add(copyItem);
                textBox.ContextMenu = contextMenu;

                // 如果是截断的内容，确保可以滚动
                if (isTruncated)
                {
                    textBox.TextWrapping = TextWrapping.Wrap;
                }

                Grid.SetRow(textBox, 1);
                grid.Children.Add(textBox);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取文本文件: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取编码的显示名称
        /// </summary>
        private static string GetEncodingDisplayName(Encoding encoding)
        {
            if (encoding == null) return "UTF-8";

            if (encoding is UTF8Encoding utf8)
            {
                return utf8.GetPreamble().Length > 0 ? "UTF-8 (BOM)" : "UTF-8";
            }

            switch (encoding.CodePage)
            {
                case 936: return "GBK";
                case 20936: return "GB2312";
                case 54936: return "GB18030";
                case 1200: return "UTF-16 LE";
                case 1201: return "UTF-16 BE";
                case 20127: return "ASCII";
                default:
                    if (encoding == Encoding.Default)
                        return "系统默认";
                    return encoding.EncodingName;
            }
        }

        /// <summary>
        /// 从名称获取编码对象
        /// </summary>
        private static Encoding GetEncodingFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return Encoding.UTF8;

            try
            {
                switch (name)
                {
                    case "UTF-8":
                        return new UTF8Encoding(false);
                    case "UTF-8 (BOM)":
                        return new UTF8Encoding(true);
                    case "GBK":
                        return Encoding.GetEncoding("GBK");
                    case "GB2312":
                        return Encoding.GetEncoding("GB2312");
                    case "GB18030":
                        return Encoding.GetEncoding("GB18030");
                    case "UTF-16 LE":
                        return Encoding.Unicode;
                    case "UTF-16 BE":
                        return Encoding.BigEndianUnicode;
                    case "ASCII":
                        return Encoding.ASCII;
                    case "系统默认":
                        return Encoding.Default;
                    default:
                        return Encoding.UTF8;
                }
            }
            catch
            {
                return Encoding.UTF8;
            }
        }
    }
}

