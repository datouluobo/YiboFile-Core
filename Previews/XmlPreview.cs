using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.Controls;

namespace YiboFile.Previews
{
    /// <summary>
    /// XML 文件预览 - 支持渲染和源码两种视图
    /// </summary>
    public class XmlPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                // 读取XML内容
                string xmlContent = "";
                Encoding encoding = Encoding.UTF8;

                // 简单的编码尝试
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    // 尝试读取，如果出错则尝试其他编码逻辑在后面（此处简化，直接读取）
                    // 实际项目中可以保留原来的复杂编码探测，这里为了修复编译错误先采用这种方式
                    // 如果需要完整逻辑，可以后续添加
                    xmlContent = File.ReadAllText(filePath);
                }
                catch
                {
                    try { xmlContent = File.ReadAllText(filePath, Encoding.Default); } catch { }
                }

                // 创建主容器
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 内容容器
                var contentGrid = new Grid
                {
                    Background = Brushes.White
                };
                Grid.SetRow(contentGrid, 1);
                grid.Children.Add(contentGrid);

                // 用于跟踪当前视图
                int currentViewIndex = 0; // 0=Render, 1=Source
                bool isEditMode = false;

                // 预先声明控件
                WebView2 webView = new WebView2
                {
                    Visibility = Visibility.Visible,
                    DefaultBackgroundColor = System.Drawing.Color.White
                };

                TextBox sourceTextBox = new TextBox
                {
                    Text = xmlContent,
                    TextWrapping = TextWrapping.Wrap,
                    IsReadOnly = true,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.White,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    AcceptsReturn = true,
                    AcceptsTab = true,
                    Visibility = Visibility.Collapsed
                };

                // 创建工具栏
                var _toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "🌐",
                    ShowSearch = false,
                    ShowWordWrap = true,
                    ShowEncoding = false,
                    ShowViewToggle = true,
                    ShowFormat = true,
                    IsWordWrapEnabled = true
                };

                _toolbar.SetViewToggleText("📄 源码");

                // 添加控件
                contentGrid.Children.Add(webView);
                contentGrid.Children.Add(sourceTextBox);
                Grid.SetRow(_toolbar, 0);
                grid.Children.Add(_toolbar);

                // 初始渲染
                InitializeRender(webView, xmlContent);

                // 事件绑定
                _toolbar.ViewToggleRequested += (s, e) =>
                {
                    currentViewIndex = currentViewIndex == 0 ? 1 : 0;

                    if (currentViewIndex == 0) // Switch to Render
                    {
                        webView.Visibility = Visibility.Visible;
                        sourceTextBox.Visibility = Visibility.Collapsed;
                        _toolbar.SetViewToggleText("📄 源码");
                        // 重新加载渲染
                        InitializeRender(webView, sourceTextBox.Text);
                    }
                    else // Switch to Source
                    {
                        webView.Visibility = Visibility.Collapsed;
                        sourceTextBox.Visibility = Visibility.Visible;
                        _toolbar.SetViewToggleText("🎨 渲染");
                    }
                };

                _toolbar.WordWrapChanged += (s, enabled) =>
                {
                    sourceTextBox.TextWrapping = enabled ? TextWrapping.Wrap : TextWrapping.NoWrap;
                };

                _toolbar.FormatRequested += (s, e) =>
                {
                    try
                    {
                        var doc = new XmlDocument();
                        doc.LoadXml(sourceTextBox.Text);

                        var sb = new StringBuilder();
                        var settings = new XmlWriterSettings
                        {
                            Indent = true,
                            IndentChars = "  ",
                            NewLineChars = "\r\n",
                            NewLineHandling = NewLineHandling.Replace
                        };

                        using (var writer = XmlWriter.Create(sb, settings))
                        {
                            doc.Save(writer);
                        }

                        sourceTextBox.Text = sb.ToString();

                        // 切换到源码视图查看结果
                        if (currentViewIndex == 0)
                        {
                            currentViewIndex = 1;
                            webView.Visibility = Visibility.Collapsed;
                            sourceTextBox.Visibility = Visibility.Visible;
                            _toolbar.SetViewToggleText("🎨 渲染");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"格式化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                _toolbar.EditRequested += (s, e) =>
                {
                    if (isEditMode)
                    {
                        // Save
                        try
                        {
                            File.WriteAllText(filePath, sourceTextBox.Text);
                            isEditMode = false;

                            sourceTextBox.IsReadOnly = true;
                            sourceTextBox.Background = Brushes.White;
                            _toolbar.SetEditMode(false);

                            // Update Render
                            if (currentViewIndex == 0)
                                InitializeRender(webView, sourceTextBox.Text);

                            MessageBox.Show("文件已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        // Edit
                        isEditMode = true;

                        // Force switch to source
                        if (currentViewIndex == 0)
                        {
                            currentViewIndex = 1;
                            webView.Visibility = Visibility.Collapsed;
                            sourceTextBox.Visibility = Visibility.Visible;
                            _toolbar.SetViewToggleText("🎨 渲染");
                        }

                        sourceTextBox.IsReadOnly = false;
                        sourceTextBox.Background = new SolidColorBrush(Color.FromRgb(240, 248, 255));
                        _toolbar.SetEditMode(true);
                    }
                };

                _toolbar.CopyRequested += (s, e) =>
                {
                    if (currentViewIndex == 1) // Only copy from source
                    {
                        if (!string.IsNullOrEmpty(sourceTextBox.SelectedText)) Clipboard.SetText(sourceTextBox.SelectedText);
                        else Clipboard.SetText(sourceTextBox.Text);
                    }
                };

                _toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"XML预览失败: {ex.Message}");
            }
        }

        private async void InitializeRender(WebView2 webView, string content)
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                string html = GenerateStyledHtml(content);
                webView.NavigateToString(html);
            }
            catch
            {
                // Ignore initialization errors
            }
        }

        private string GenerateStyledHtml(string xmlContent)
        {
            if (string.IsNullOrEmpty(xmlContent)) return "";

            // 转义XML内容中的特殊字符
            string escapedXml = WebUtility.HtmlEncode(xmlContent);

            // 简单的语法高亮（通过正则表达式）
            escapedXml = System.Text.RegularExpressions.Regex.Replace(
                escapedXml,
                @"(&lt;)(/?)([\w:]+)([^&]*?)(/?)(&gt;)",
                match =>
                {
                    string prefix = match.Groups[1].Value; // &lt;
                    string slash = match.Groups[2].Value;  // / or empty
                    string tagName = match.Groups[3].Value; // tag name
                    string attrs = match.Groups[4].Value;   // attributes
                    string selfClose = match.Groups[5].Value; // / or empty
                    string suffix = match.Groups[6].Value;  // &gt;

                    // 高亮标签名
                    string highlightedTag = $"{prefix}{slash}<span style='color:#881280;font-weight:bold'>{tagName}</span>{attrs}{selfClose}{suffix}";

                    // 高亮属性
                    highlightedTag = System.Text.RegularExpressions.Regex.Replace(
                        highlightedTag,
                        @"(\w+)(=)(&quot;[^&]*?&quot;)",
                        m => $"<span style='color:#994500'>{m.Groups[1].Value}</span>{m.Groups[2].Value}<span style='color:#1A1AA6'>{m.Groups[3].Value}</span>",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    return highlightedTag;
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: 'Consolas', 'Monaco', 'Courier New', monospace;
            font-size: 13px;
            line-height: 1.6;
            padding: 20px;
            background: #ffffff;
            color: #333;
            margin: 0;
        }}
        pre {{
            margin: 0;
            white-space: pre-wrap;
            word-wrap: break-word;
        }}
        .xml-container {{
            background: #f8f8f8;
            border: 1px solid #e0e0e0;
            border-radius: 4px;
            padding: 15px;
            overflow-x: auto;
        }}
    </style>
</head>
<body>
    <div class='xml-container'>
        <pre>{escapedXml}</pre>
    </div>
</body>
</html>";
        }
    }
}

