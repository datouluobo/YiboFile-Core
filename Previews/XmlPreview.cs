using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using System.Xml.Linq;

namespace OoiMRR.Previews
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
                string xmlContent = null;
                var encodings = new List<Encoding>
                {
                    Encoding.UTF8,
                    Encoding.Default
                };

                // 注册编码提供程序，以支持中文字符编码
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // 尝试添加中文字符编码，如果系统支持
                try { encodings.Add(Encoding.GetEncoding("GB2312")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("GBK")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("GB18030")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("UTF-16LE")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("UTF-16BE")); } catch { }

                Exception lastException = null;
                bool isValidXml = false;
                string tempContent = null;
                
                foreach (var encoding in encodings)
                {
                    try
                    {
                        tempContent = File.ReadAllText(filePath, encoding);
                        // 验证是否为有效的XML
                        try
                        {
                            XDocument.Parse(tempContent);
                            xmlContent = tempContent;
                            isValidXml = true;
                            break; // 成功解析，退出循环
                        }
                        catch
                        {
                            // 不是有效的XML，但保留内容用于源码显示
                            if (string.IsNullOrEmpty(xmlContent))
                            {
                                xmlContent = tempContent; // 保存第一个成功读取的内容
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                    }
                }

                if (string.IsNullOrEmpty(xmlContent))
                {
                    if (lastException != null)
                    {
                        return PreviewHelper.CreateErrorPreview($"无法读取XML文件: {lastException.Message}");
                    }
                    return PreviewHelper.CreateErrorPreview("无法读取XML文件内容");
                }
                
                // 如果之前没有验证成功，再次验证XML格式
                if (!isValidXml)
                {
                    try
                    {
                        XDocument.Parse(xmlContent);
                        isValidXml = true;
                    }
                    catch
                    {
                        isValidXml = false; // XML格式无效，只显示源码
                    }
                }

                // 创建主容器
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 内容区域 - 使用Grid替代TabControl，隐藏标签页
                var contentGrid = new Grid
                {
                    Background = Brushes.White
                };
                Grid.SetRow(contentGrid, 1);
                grid.Children.Add(contentGrid);
                
                // 用于跟踪当前视图（0=渲染，1=源码）
                // 如果XML无效，默认显示源码视图
                int currentViewIndex = isValidXml ? 0 : 1;

                // 先声明所有变量，以便在lambda中使用
                Button toggleButton = null;
                TextBox sourceTextBoxRef = null;
                WebView2 webViewRef = null;
                Button editButton = null;
                bool isEditMode = false;
                string originalXmlContent = xmlContent;

                // 标题栏 - 添加渲染/源码切换按钮和编辑按钮
                var buttons = new List<Button>();
                
                // 如果XML无效，不显示渲染切换按钮，只显示编辑按钮
                if (isValidXml)
                {
                    toggleButton = PreviewHelper.CreateHtmlViewToggleButton(
                        () => 
                        {
                            // 切换视图：如果当前是渲染(0)，切换到源码(1)；如果当前是源码(1)，切换到渲染(0)
                            currentViewIndex = currentViewIndex == 0 ? 1 : 0;
                            
                            // 显示/隐藏对应的视图
                            if (currentViewIndex == 0)
                            {
                                // 显示渲染视图
                                if (webViewRef != null)
                                {
                                    webViewRef.Visibility = Visibility.Visible;
                                    // 重新加载XML内容
                                    LoadXmlToWebView(webViewRef, filePath, xmlContent);
                                }
                                if (sourceTextBoxRef != null)
                                {
                                    sourceTextBoxRef.Visibility = Visibility.Collapsed;
                                }
                                if (toggleButton != null)
                                {
                                    toggleButton.Content = "📄 源码";
                                }
                            }
                            else
                            {
                                // 显示源码视图
                                if (webViewRef != null)
                                {
                                    webViewRef.Visibility = Visibility.Collapsed;
                                }
                                if (sourceTextBoxRef != null)
                                {
                                    sourceTextBoxRef.Visibility = Visibility.Visible;
                                }
                                if (toggleButton != null)
                                {
                                    toggleButton.Content = "🎨 渲染";
                                }
                            }
                        },
                        "📄 源码",  // 当前显示渲染，按钮显示"源码"
                        "🎨 渲染"   // 切换到源码后，按钮显示"渲染"
                    );
                    buttons.Add(toggleButton);
                }
                
                // 编辑/保存按钮
                editButton = PreviewHelper.CreateEditButton(
                    () =>
                    {
                        if (isEditMode)
                        {
                            // 保存模式
                            try
                            {
                                // 从源码视图获取内容
                                if (sourceTextBoxRef != null)
                                {
                                    xmlContent = sourceTextBoxRef.Text;
                                    
                                    // 验证XML格式
                                    try
                                    {
                                        XDocument.Parse(xmlContent);
                                    }
                                    catch (Exception xmlEx)
                                    {
                                        var result = MessageBox.Show(
                                            $"XML格式错误: {xmlEx.Message}\n\n是否仍要保存？",
                                            "XML格式验证失败",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Warning);
                                        if (result == MessageBoxResult.No)
                                        {
                                            return;
                                        }
                                    }
                                }

                                // 确定编码（优先使用UTF-8）
                                Encoding encoding = Encoding.UTF8;
                                try
                                {
                                    var originalBytes = File.ReadAllBytes(filePath);
                                    if (originalBytes.Length >= 3 && originalBytes[0] == 0xEF && originalBytes[1] == 0xBB && originalBytes[2] == 0xBF)
                                    {
                                        encoding = new UTF8Encoding(true);
                                    }
                                }
                                catch { }

                                // 保存文件
                                File.WriteAllText(filePath, xmlContent, encoding);
                                originalXmlContent = xmlContent;
                                
                                // 更新渲染视图
                                if (webViewRef != null)
                                {
                                    LoadXmlToWebView(webViewRef, filePath, xmlContent);
                                }
                                
                                // 切换为只读模式
                                if (sourceTextBoxRef != null)
                                {
                                    sourceTextBoxRef.IsReadOnly = true;
                                    sourceTextBoxRef.Background = PreviewHelper.ReadOnlyBackground;
                                }
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
                            // 编辑模式 - 切换到源码视图
                            currentViewIndex = 1;
                            if (webViewRef != null)
                            {
                                webViewRef.Visibility = Visibility.Collapsed;
                            }
                            if (sourceTextBoxRef != null)
                            {
                                sourceTextBoxRef.Visibility = Visibility.Visible;
                            }
                            if (toggleButton != null)
                            {
                                toggleButton.Content = "🎨 渲染";
                            }
                            
                            if (sourceTextBoxRef != null)
                            {
                                sourceTextBoxRef.IsReadOnly = false;
                                sourceTextBoxRef.Background = PreviewHelper.EditModeBackground; // 浅蓝色背景表示可编辑
                            }
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
                buttons.Add(editButton);
                
                var titlePanel = PreviewHelper.CreateTitlePanel("📋", $"XML 文件: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);

                // 渲染视图（仅在XML有效时创建和显示）
                if (isValidXml)
                {
                    webViewRef = new WebView2
                    {
                        VerticalAlignment = VerticalAlignment.Stretch,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Visibility = Visibility.Visible
                    };

                    // 异步加载XML内容
                    webViewRef.Loaded += async (s, e) =>
                    {
                        try
                        {
                            await webViewRef.EnsureCoreWebView2Async();
                            LoadXmlToWebView(webViewRef, filePath, xmlContent);
                        }
                        catch (Exception ex)
                        {
                                                        try
                            {
                                await webViewRef.EnsureCoreWebView2Async();
                                webViewRef.NavigateToString($"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>渲染失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>");
                            }
                            catch { }
                        }
                    };
                }

                // 源码视图（XML无效时默认显示，有效时默认隐藏）
                sourceTextBoxRef = new TextBox
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
                    Visibility = isValidXml ? Visibility.Collapsed : Visibility.Visible
                };

                // 源码的右键菜单（只包含复制）
                var sourceContextMenu = new ContextMenu();
                var sourceCopyItem = new MenuItem
                {
                    Header = "复制",
                    InputGestureText = "Ctrl+C"
                };
                sourceCopyItem.Click += (s, e) =>
                {
                    if (sourceTextBoxRef != null)
                    {
                        if (!string.IsNullOrEmpty(sourceTextBoxRef.SelectedText))
                        {
                            Clipboard.SetText(sourceTextBoxRef.SelectedText);
                        }
                        else
                        {
                            Clipboard.SetText(sourceTextBoxRef.Text);
                        }
                    }
                };
                sourceContextMenu.Items.Add(sourceCopyItem);
                sourceTextBoxRef.ContextMenu = sourceContextMenu;

                // 将内容添加到Grid中
                if (webViewRef != null)
                {
                    contentGrid.Children.Add(webViewRef);
                }
                contentGrid.Children.Add(sourceTextBoxRef);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"XML预览失败: {ex.Message}");
            }
        }

        private void LoadXmlToWebView(WebView2 webView, string filePath, string xmlContent)
        {
            try
            {
                // 格式化XML
                string formattedXml = FormatXml(xmlContent);
                
                // 生成带样式的HTML
                string html = GenerateStyledHtml(formattedXml);
                
                // 使用data URI加载HTML内容
                var htmlBytes = Encoding.UTF8.GetBytes(html);
                var base64 = Convert.ToBase64String(htmlBytes);
                webView.CoreWebView2?.NavigateToString(html);
            }
            catch (Exception ex)
            {
                webView.CoreWebView2?.NavigateToString(
                    $"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>XML渲染失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>");
            }
        }

        private string FormatXml(string xml)
        {
            try
            {
                var doc = XDocument.Parse(xml);
                return doc.ToString();
            }
            catch
            {
                // 如果解析失败，返回原始内容
                return xml;
            }
        }

        private string GenerateStyledHtml(string xmlContent)
        {
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

