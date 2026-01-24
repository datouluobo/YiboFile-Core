using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YiboFile.Controls;

namespace YiboFile.Previews
{
    /// <summary>
    /// 文本文件预览
    /// </summary>
    public class TextPreview : IPreviewProvider
    {
        private TextBox _textBox;
        private TextPreviewToolbar _toolbar;
        private List<int> _searchMatches = new List<int>();
        private int _currentMatchIndex = -1;
        private string _lastSearchText = string.Empty;

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
                            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
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
                if (content.Length > maxLength)
                {
                    content = content.Substring(0, maxLength) + "\n\n... (文件内容过长，仅显示前2000个字符)";
                }

                // 创建主容器
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 创建工具栏
                _toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "📄",
                    ShowSearch = true,
                    ShowWordWrap = true,
                    ShowEncoding = true,
                    ShowViewToggle = false, // 纯文本不需要切换视图
                    IsWordWrapEnabled = true
                };

                // 初始化工具栏状态
                _toolbar.SetSelectedEncoding(currentEncodingName);

                // 使用可编辑的 TextBox
                _textBox = new TextBox
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    IsReadOnly = true,
                    Margin = new Thickness(5),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    BorderThickness = new Thickness(0),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    AcceptsReturn = true,
                    AcceptsTab = true
                };
                _textBox.SetResourceReference(TextBox.BackgroundProperty, "AppBackgroundBrush");
                _textBox.SetResourceReference(TextBox.ForegroundProperty, "ForegroundPrimaryBrush");

                bool isEditMode = false;
                string originalContent = content;

                // 绑定工具栏事件
                _toolbar.WordWrapChanged += (s, enabled) =>
                {
                    _textBox.TextWrapping = enabled ? TextWrapping.Wrap : TextWrapping.NoWrap;
                };

                _toolbar.EncodingChanged += (s, encodingName) =>
                {
                    try
                    {
                        var selectedEncoding = GetEncodingFromName(encodingName);
                        if (selectedEncoding == null) return;

                        // 重新读取文件
                        byte[] bytes;
                        var fileInfo = new FileInfo(filePath);
                        int maxBytes = 100 * 1024;

                        if (fileInfo.Length > maxBytes)
                        {
                            bytes = new byte[maxBytes];
                            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                            {
                                fs.Read(bytes, 0, maxBytes);
                            }
                        }
                        else
                        {
                            bytes = File.ReadAllBytes(filePath);
                        }

                        string newContent = selectedEncoding.GetString(bytes);

                        if (newContent.Length > maxLength)
                        {
                            newContent = newContent.Substring(0, maxLength) + "\n\n... (文件内容过长,仅显示前2000个字符)";
                        }

                        _textBox.Text = newContent;
                        originalContent = newContent;
                        currentEncoding = selectedEncoding;
                        // 重置搜索
                        PerformSearch(_lastSearchText);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"使用所选编码重新加载失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                _toolbar.CopyRequested += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(_textBox.SelectedText))
                    {
                        Clipboard.SetText(_textBox.SelectedText);
                    }
                    else
                    {
                        Clipboard.SetText(_textBox.Text);
                    }
                };

                _toolbar.EditRequested += (s, e) =>
                {
                    if (isEditMode)
                    {
                        // 保存模式
                        try
                        {
                            // 确定编码
                            Encoding encoding = Encoding.UTF8;
                            try
                            {
                                var originalBytes = File.ReadAllBytes(filePath);
                                if (originalBytes.Length >= 3 && originalBytes[0] == 0xEF && originalBytes[1] == 0xBB && originalBytes[2] == 0xBF)
                                {
                                    encoding = new UTF8Encoding(true);
                                }
                                else
                                {
                                    // 尝试保持当前编码
                                    encoding = currentEncoding ?? Encoding.UTF8;
                                }
                            }
                            catch { }

                            // 保存文件
                            File.WriteAllText(filePath, _textBox.Text, encoding);

                            // 更新原始内容
                            originalContent = _textBox.Text;

                            // 切换为只读模式
                            _textBox.IsReadOnly = true;
                            _textBox.SetResourceReference(TextBox.BackgroundProperty, "AppBackgroundBrush");
                            isEditMode = false;
                            _toolbar.SetEditMode(false);

                            // MessageBox.Show("文件已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            Services.Core.NotificationService.ShowSuccess("文件已保存");
                        }
                        catch (Exception ex)
                        {
                            // MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            Services.Core.NotificationService.ShowError($"保存失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        // 编辑模式
                        _textBox.IsReadOnly = false;
                        _textBox.SetResourceReference(TextBox.BackgroundProperty, "AccentLightBrush"); // 使用强调色的浅色背景
                        isEditMode = true;
                        _toolbar.SetEditMode(true);
                    }
                };

                _toolbar.OpenExternalRequested += (s, e) =>
                {
                    PreviewHelper.OpenInDefaultApp(filePath);
                };

                // 搜索功能实现
                _toolbar.SearchRequested += (s, text) => PerformSearch(text);
                _toolbar.SearchNextRequested += (s, e) => NavigateMatch(true);
                _toolbar.SearchPrevRequested += (s, e) => NavigateMatch(false);


                Grid.SetRow(_toolbar, 0);
                grid.Children.Add(_toolbar);

                // 设置右键菜单
                var contextMenu = new ContextMenu();
                var copyItem = new MenuItem { Header = "复制", InputGestureText = "Ctrl+C" };
                copyItem.Click += (s, e) => _toolbar.GetType().GetMethod("CopyButton_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(_toolbar, new object[] { null, null });
                // 既然我们在Toolbar里实现了复制逻辑，这里其实直接调用Toolbar的逻辑或者重新实现简单逻辑皆可
                // 简单起见，重新实现:
                copyItem.Click -= (s, e) => _toolbar.GetType().GetMethod("CopyButton_Click", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(_toolbar, new object[] { null, null });
                copyItem.Click += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(_textBox.SelectedText)) Clipboard.SetText(_textBox.SelectedText);
                    else Clipboard.SetText(_textBox.Text);
                };
                contextMenu.Items.Add(copyItem);
                _textBox.ContextMenu = contextMenu;

                Grid.SetRow(_textBox, 1);
                grid.Children.Add(_textBox);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取文本文件: {ex.Message}");
            }
        }

        private void PerformSearch(string text)
        {
            _lastSearchText = text;
            _searchMatches.Clear();
            _currentMatchIndex = -1;

            if (string.IsNullOrEmpty(text) || _textBox == null)
            {
                _toolbar.SetMatchCount(0, 0);
                return;
            }

            string content = _textBox.Text;
            int index = 0;
            while ((index = content.IndexOf(text, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                _searchMatches.Add(index);
                index += text.Length;
            }

            if (_searchMatches.Count > 0)
            {
                _currentMatchIndex = 0;
                HighlightMatch(0);
            }

            _toolbar.SetMatchCount(_searchMatches.Count > 0 ? 1 : 0, _searchMatches.Count);
        }

        private void NavigateMatch(bool next)
        {
            if (_searchMatches.Count == 0) return;

            if (next)
            {
                _currentMatchIndex++;
                if (_currentMatchIndex >= _searchMatches.Count) _currentMatchIndex = 0; // 循环
            }
            else
            {
                _currentMatchIndex--;
                if (_currentMatchIndex < 0) _currentMatchIndex = _searchMatches.Count - 1; // 循环
            }

            HighlightMatch(_currentMatchIndex);
            _toolbar.SetMatchCount(_currentMatchIndex + 1, _searchMatches.Count);
        }

        private void HighlightMatch(int matchIndex)
        {
            if (matchIndex < 0 || matchIndex >= _searchMatches.Count) return;

            int start = _searchMatches[matchIndex];
            int length = _lastSearchText.Length;

            _textBox.Focus();
            _textBox.Select(start, length);

            // 滚动到可见区域
            var lineIndex = _textBox.GetLineIndexFromCharacterIndex(start);
            _textBox.ScrollToLine(lineIndex);
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


