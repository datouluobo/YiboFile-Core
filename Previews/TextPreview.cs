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
                string content = null;
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

                        // 成功读取，退出循环
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

                var textBlock = new TextBlock
                {
                    Text = content,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(5),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12
                };

                var scrollViewer = new ScrollViewer
                {
                    Content = textBlock,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                return scrollViewer;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取文本文件: {ex.Message}");
            }
        }
    }
}

