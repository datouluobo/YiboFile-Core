using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using System.IO.Compression;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Text.RegularExpressions;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 文档文件预览（DOCX、DOC、PDF、RTF、CHM）
    /// </summary>
    public class DocumentPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            if (extension == ".docx")
            {
                return CreateDocxPreview(filePath);
            }
            else if (extension == ".doc")
            {
                return CreateDocPreview(filePath);
            }
            else if (extension == ".pdf")
            {
                return CreatePdfPreview(filePath);
            }
            else if (extension == ".rtf")
            {
                return CreateRtfPreview(filePath);
            }
            else if (extension == ".chm")
            {
                return CreateChmPreview(filePath);
            }

            // 未知文档类型
            return CreateGenericDocumentPreview(filePath);
        }

        private UIElement CreateGenericDocumentPreview(string filePath)
        {
            var panel = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(10)
            };

            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var title = PreviewHelper.CreateTitlePanel("📄", $"文档: {Path.GetFileName(filePath)}", buttons);
            panel.Children.Add(title);

            var info = new TextBlock
            {
                Text = $"文件大小: {PreviewHelper.FormatFileSize(new FileInfo(filePath).Length)}",
                Foreground = Brushes.Gray,
                Margin = new Thickness(10),
                TextAlignment = System.Windows.TextAlignment.Center
            };
            panel.Children.Add(info);

            return panel;
        }

        private UIElement CreateDocxPreview(string filePath)
        {
            try
            {
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("📄", $"Word 文档: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);

                var webView = new WebView2
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    MinHeight = 400
                };
                Grid.SetRow(webView, 1);
                grid.Children.Add(webView);

                // 异步加载文档内容
                webView.Loaded += async (s, e) =>
                {
                    try
                    {
                        await webView.EnsureCoreWebView2Async();
                        
                        // 在后台线程提取文本和图片
                        string html = await Task.Run(() =>
                        {
                            try
                            {
                                return GenerateHtmlFromDocx(filePath);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"DOCX预览失败: {ex.Message}\n{ex.StackTrace}");
                                // 如果完整预览失败，尝试仅提取文本
                                try
                                {
                                    return GenerateSimpleTextPreview(filePath);
                                }
                                catch (Exception ex2)
                                {
                                    return $"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>预览失败: {WebUtility.HtmlEncode(ex2.Message)}</body></html>";
                                }
                            }
                        });

                        // 如果HTML太大，保存到临时文件然后导航
                        // NavigateToString有大小限制（约2MB），包含大量Base64图片时可能超出
                        if (html.Length > 1.5 * 1024 * 1024) // 1.5MB
                        {
                            try
                            {
                                var tempHtmlFile = Path.Combine(Path.GetTempPath(), $"docx_preview_{Guid.NewGuid()}.html");
                                File.WriteAllText(tempHtmlFile, html, Encoding.UTF8);
                                var fileUri = new Uri(tempHtmlFile).ToString();
                                System.Diagnostics.Debug.WriteLine($"HTML太大 ({html.Length} 字节)，保存到临时文件: {tempHtmlFile}");
                                
                                await webView.EnsureCoreWebView2Async();
                                webView.CoreWebView2.Navigate(fileUri);
                                
                                // 清理：在WebView关闭时删除临时文件
                                webView.CoreWebView2.NavigationCompleted += (s, e) =>
                                {
                                    try
                                    {
                                        // 延迟删除，确保WebView已加载
                                        Task.Delay(5000).ContinueWith(_ =>
                                        {
                                            try { File.Delete(tempHtmlFile); }
                                            catch { }
                                        });
                                    }
                                    catch { }
                                };
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"保存临时HTML文件失败: {ex.Message}");
                                // 回退到NavigateToString
                                webView.NavigateToString(html);
                            }
                        }
                        else
                        {
                            webView.NavigateToString(html);
                        }
                    }
                    catch (Exception ex)
                    {
                        webView.NavigateToString($"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>预览失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>");
                    }
                };

                return grid;
            }
            catch (Exception ex)
            {
                return CreateDocumentErrorPanel($"DOCX 预览失败: {ex.Message}");
            }
        }

        private UIElement CreateDocPreview(string filePath)
        {
            try
            {
                var tempDocx = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(filePath) + ".docx");
                string errorMsg;
                bool canPreview = ConvertDocToDocx(filePath, tempDocx, out errorMsg);
                
                // 无论能否预览，都显示转换按钮
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 转换按钮
                var convertButton = new Button
                {
                    Content = "🔄 转换为DOCX格式",
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    FontSize = 13
                };

                convertButton.Click += async (s, e) =>
                {
                    try
                    {
                        convertButton.IsEnabled = false;
                        convertButton.Content = "⏳ 转换中...";

                        try
                        {
                            // 生成输出路径（同目录，同名）
                            string directory = Path.GetDirectoryName(filePath);
                            string baseName = Path.GetFileNameWithoutExtension(filePath);
                            string outputPath = Path.Combine(directory, baseName + ".docx");
                            
                            // 如果文件已存在，添加序号
                            outputPath = GetUniqueFilePath(outputPath);
                            
                            // 在后台线程执行转换
                            string errorMessage = null;
                            bool success = await Task.Run(() => 
                            {
                                bool result = ConvertDocToDocx(filePath, outputPath, out errorMessage);
                                return result;
                            });

                            if (success)
                            {
                                convertButton.Content = "✅ 转换成功";
                            }
                            else
                            {
                                string errorTitle = errorMessage?.Contains("未检测到") == true ? "需要 Microsoft Word" : "转换错误";
                                MessageBox.Show(
                                    errorMessage ?? "转换失败",
                                    errorTitle,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                convertButton.IsEnabled = true;
                                convertButton.Content = "🔄 转换为DOCX格式";
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            convertButton.IsEnabled = true;
                            convertButton.Content = "🔄 转换为DOCX格式";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        convertButton.IsEnabled = true;
                        convertButton.Content = "🔄 转换为DOCX格式";
                    }
                };

                var buttons = new List<Button> 
                { 
                    convertButton,
                    PreviewHelper.CreateOpenButton(filePath)
                };
                var titlePanel = PreviewHelper.CreateTitlePanel("📄", $"Word 文档: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);

                // 如果能预览，显示预览内容
                if (canPreview)
                {

                    var webView = new WebView2
                    {
                        VerticalAlignment = VerticalAlignment.Stretch,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        MinHeight = 400
                    };
                    Grid.SetRow(webView, 1);
                    grid.Children.Add(webView);

                    // 异步加载文档内容
                    webView.Loaded += async (s, e) =>
                    {
                        try
                        {
                            await webView.EnsureCoreWebView2Async();
                            
                            // 在后台线程提取文本
                            string html = await Task.Run(() =>
                            {
                                try
                                {
                                    using (var wordDoc = WordprocessingDocument.Open(tempDocx, false))
                                    {
                                        var body = wordDoc.MainDocumentPart.Document.Body;
                                        var paragraphs = new List<string>();
                                        
                                        foreach (var element in body.Elements())
                                        {
                                            var text = element.InnerText;
                                            if (!string.IsNullOrWhiteSpace(text))
                                            {
                                                paragraphs.Add(WebUtility.HtmlEncode(text));
                                            }
                                        }

                                        return GenerateHtmlFromText(paragraphs, Path.GetFileName(filePath));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    return $"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>预览失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>";
                                }
                            });

                            webView.NavigateToString(html);
                        }
                        catch (Exception ex)
                        {
                            webView.NavigateToString($"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>预览失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>");
                        }
                    };

                    // 延迟删除临时文件，确保WebView加载完成
                    Task.Delay(5000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (File.Exists(tempDocx))
                                File.Delete(tempDocx);
                        }
                        catch { }
                    });
                }
                else
                {
                    // 如果不能预览，显示错误信息
                    var errorPanel = new StackPanel
                    {
                        Background = Brushes.White,
                        Margin = new Thickness(15, 10, 15, 5)
                    };
                    
                    var errorText = new TextBlock
                    {
                        Text = "DOC 预览需要安装 Microsoft Word 或兼容组件，或者转换为 DOCX 格式。",
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                        LineHeight = 24
                    };
                    errorPanel.Children.Add(errorText);
                    
                    Grid.SetRow(errorPanel, 1);
                    grid.Children.Add(errorPanel);
                }

                return grid;
            }
            catch (Exception ex)
            {
                // 如果出错，显示包含转换按钮的界面
                return CreateDocPreviewWithConvertButton(filePath, $"DOC 预览失败: {ex.Message}");
            }
        }

        private UIElement CreateDocPreviewWithConvertButton(string filePath, string message)
        {
            var panel = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(10)
            };

            // 转换按钮
            var convertButton = new Button
            {
                Content = "🔄 转换为DOCX格式",
                Padding = new Thickness(12, 6, 12, 6),
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13
            };

            convertButton.Click += async (s, e) =>
            {
                try
                {
                    convertButton.IsEnabled = false;
                    convertButton.Content = "⏳ 转换中...";

                    try
                    {
                        // 生成输出路径（同目录，同名）
                        string directory = Path.GetDirectoryName(filePath);
                        string baseName = Path.GetFileNameWithoutExtension(filePath);
                        string outputPath = Path.Combine(directory, baseName + ".docx");
                        
                        // 如果文件已存在，添加序号
                        outputPath = GetUniqueFilePath(outputPath);
                        
                        // 在后台线程执行转换
                        string errorMsg = null;
                        bool success = await Task.Run(() => 
                        {
                            bool result = ConvertDocToDocx(filePath, outputPath, out errorMsg);
                            return result;
                        });

                        if (success)
                        {
                            convertButton.Content = "✅ 转换成功";
                        }
                        else
                        {
                            string errorTitle = errorMsg?.Contains("未检测到") == true ? "需要 Microsoft Word" : "转换错误";
                            MessageBox.Show(
                                errorMsg ?? "转换失败",
                                errorTitle,
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            convertButton.IsEnabled = true;
                            convertButton.Content = "🔄 转换为DOCX格式";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        convertButton.IsEnabled = true;
                        convertButton.Content = "🔄 转换为DOCX格式";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    convertButton.IsEnabled = true;
                    convertButton.Content = "🔄 转换为DOCX格式";
                }
            };

            var buttons = new List<Button>
            {
                convertButton,
                PreviewHelper.CreateOpenButton(filePath)
            };
            var title = PreviewHelper.CreateTitlePanel("📄", $"DOC 文档: {Path.GetFileName(filePath)}", buttons);
            panel.Children.Add(title);

            var errorPanel = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(15, 10, 15, 5)
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 24
            };
            errorPanel.Child = textBlock;
            panel.Children.Add(errorPanel);

            return panel;
        }

        /// <summary>
        /// 生成唯一文件名（如果文件已存在，添加序号）
        /// </summary>
        private string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            string directory = Path.GetDirectoryName(filePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int counter = 1;
            string newFilePath;
            do
            {
                newFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}({counter}){extension}");
                counter++;
            }
            while (File.Exists(newFilePath));

            return newFilePath;
        }

        private bool ConvertDocToDocx(string docPath, string docxPath, out string errorMessage)
        {
            errorMessage = null;
            
            try
            {
                // 尝试使用 Word COM 自动化
                Type wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType == null)
                {
                    errorMessage = "未检测到 Microsoft Word。\n\n转换 DOC 到 DOCX 需要安装 Microsoft Word。";
                    return false;
                }

                dynamic wordApp = Activator.CreateInstance(wordType);
                try
                {
                    // 尝试设置Visible=false，如果失败则忽略（某些版本不允许隐藏）
                    try
                    {
                        wordApp.Visible = false;
                    }
                    catch
                    {
                        // 某些版本的Word不允许隐藏窗口，忽略此错误
                    }
                    
                    wordApp.DisplayAlerts = 0; // wdAlertsNone

                    dynamic document = wordApp.Documents.Open(docPath, ReadOnly: true);

                    // 保存为 DOCX 格式
                    // wdFormatXMLDocument = 12 (DOCX格式)
                    document.SaveAs2(docxPath, 12);
                    document.Close(false);

                    return true;
                }
                finally
                {
                    try
                    {
                        wordApp.Quit(false);
                    }
                    catch
                    {
                        // 忽略退出时的错误
                    }
                    try
                    {
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(wordApp);
                    }
                    catch
                    {
                        // 忽略释放时的错误
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"转换失败: {ex.Message}\n\n请确保：\n1. 已安装 Microsoft Word\n2. 文件未被其他程序占用\n3. 有足够的磁盘空间";
                return false;
            }
        }

        private UIElement CreatePdfPreview(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return CreateDocumentErrorPanel($"PDF 文件不存在: {filePath}");
                }

                // 使用Grid布局：标题栏 + 内容区域
                var grid = new Grid
                {
                    Background = Brushes.White
                };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 标题栏
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath, "📂 外部程序打开") };
                var title = PreviewHelper.CreateTitlePanel("📄", $"PDF 文档: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(title, 0);
                grid.Children.Add(title);

                // WebView2 用于显示PDF
                var webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 400
                };
                Grid.SetRow(webView, 1);
                grid.Children.Add(webView);

                // 异步初始化并加载PDF
                _ = LoadPdfInWebViewAsync(webView, filePath);

                return grid;
            }
            catch (Exception ex)
            {
                return CreateDocumentErrorPanel($"PDF 预览初始化失败: {ex.Message}", filePath);
            }
        }

        private async Task LoadPdfInWebViewAsync(WebView2 webView, string filePath)
        {
            try
            {
                await webView.EnsureCoreWebView2Async(null);

                if (webView.CoreWebView2 != null)
                {
                    // 设置安全策略
                    webView.CoreWebView2.Settings.IsScriptEnabled = true; // PDF需要JavaScript支持
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                    webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                    // 确保使用绝对路径
                    if (!Path.IsPathRooted(filePath))
                    {
                        filePath = Path.GetFullPath(filePath);
                    }

                    // 转换为 file:// URI
                    var uri = new Uri(filePath).AbsoluteUri;
                    System.Diagnostics.Debug.WriteLine($"加载PDF: {uri}");

                    // 直接导航到PDF文件
                    webView.CoreWebView2.Navigate(uri);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PDF WebView2 加载失败: {ex.Message}");
                
                // 如果加载失败，显示错误信息
                await webView.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await webView.EnsureCoreWebView2Async(null);
                        if (webView.CoreWebView2 != null)
                        {
                            var errorHtml = $@"
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ 
            font-family: Arial, sans-serif; 
            padding: 40px; 
            text-align: center; 
            background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
        }}
        .container {{
            background: white;
            border-radius: 10px;
            padding: 30px;
            box-shadow: 0 4px 6px rgba(0,0,0,0.1);
            max-width: 600px;
            margin: 0 auto;
        }}
        .error-icon {{
            font-size: 64px;
            margin-bottom: 20px;
        }}
        h2 {{ color: #d32f2f; margin: 20px 0; }}
        .error {{ color: #d32f2f; margin: 20px 0; font-size: 14px; line-height: 1.6; }}
        .info {{ color: #666; margin: 10px 0; font-size: 13px; }}
        .button {{
            display: inline-block;
            margin-top: 20px;
            padding: 10px 20px;
            background: #2196F3;
            color: white;
            text-decoration: none;
            border-radius: 5px;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='error-icon'>📄❌</div>
        <h2>PDF 加载失败</h2>
        <div class='error'>{System.Security.SecurityElement.Escape(ex.Message)}</div>
        <div class='info'>请检查：</div>
        <div class='info'>1. PDF 文件是否完整且未损坏</div>
        <div class='info'>2. 文件路径是否包含特殊字符</div>
        <div class='info'>3. 系统是否安装了 PDF 阅读器</div>
        <div class='info'>您可以使用上方按钮使用外部程序打开此文件。</div>
    </div>
</body>
</html>";
                            webView.CoreWebView2.NavigateToString(errorHtml);
                        }
                    }
                    catch
                    {
                        // 如果连错误页面都无法显示，忽略异常
                    }
                });
            }
        }

        private UIElement CreateRtfPreview(string filePath)
        {
            try
            {
                // 使用Grid布局：标题栏 + 内容区域
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var title = PreviewHelper.CreateTitlePanel("📄", $"RTF 文档: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(title, 0);
                grid.Children.Add(title);

                var rtfBox = new RichTextBox
                {
                    IsReadOnly = true,
                    Margin = new Thickness(10),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Gray,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                using (var stream = File.OpenRead(filePath))
                {
                    rtfBox.Selection.Load(stream, DataFormats.Rtf);
                }

                Grid.SetRow(rtfBox, 1);
                grid.Children.Add(rtfBox);

                return grid;
            }
            catch (Exception ex)
            {
                return CreateDocumentErrorPanel($"RTF 预览失败: {ex.Message}");
            }
        }

        private UIElement CreateChmPreview(string filePath)
        {
            try
            {
                var mainGrid = new Grid
                {
                    Background = Brushes.White,
                    Margin = new Thickness(10)
                };

                // 定义列：标题行、内容行
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 标题
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var title = PreviewHelper.CreateTitlePanel("📖", $"CHM 帮助文件: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(title, 0);
                mainGrid.Children.Add(title);

                // 按钮面板（用于回退方案）
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(10, 0, 10, 10)
                };

                var openButton = new Button
                {
                    Content = "🌐 使用系统查看器打开",
                    FontSize = 14,
                    Padding = new Thickness(20, 10, 20, 10),
                    Cursor = Cursors.Hand,
                    Visibility = Visibility.Collapsed // 默认隐藏，解压失败时显示
                };

                openButton.Click += (s, e) =>
                {
                    try
                    {
                        var hhPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "hh.exe");
                        if (File.Exists(hhPath))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = hhPath,
                                Arguments = $"\"{filePath}\"",
                                UseShellExecute = true
                            });
                        }
                        else
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = filePath,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch (Exception openEx)
                    {
                        MessageBox.Show($"无法打开 CHM 文件: {openEx.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                buttonPanel.Children.Add(openButton);
                Grid.SetRow(buttonPanel, 0);
                mainGrid.Children.Add(buttonPanel);

                // 内容区域：左侧目录树 + 右侧 WebView2
                var contentGrid = new Grid
                {
                    Margin = new Thickness(10, 0, 10, 10)
                };

                // 定义列：目录树（250px） + 分割线（5px） + WebView2（剩余空间）
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250, GridUnitType.Pixel) });
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Pixel) });
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 目录树
                var treeView = new TreeView
                {
                    Background = Brushes.White,
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.LightGray,
                    Padding = new Thickness(5)
                };
                Grid.SetColumn(treeView, 0);
                contentGrid.Children.Add(treeView);

                // 分割线
                var splitter = new System.Windows.Controls.Border
                {
                    Background = Brushes.LightGray,
                    Width = 5,
                    Cursor = Cursors.SizeWE
                };
                Grid.SetColumn(splitter, 1);
                contentGrid.Children.Add(splitter);

                // WebView2
                var webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 400
                };
                Grid.SetColumn(webView, 2);
                contentGrid.Children.Add(webView);

                Grid.SetRow(contentGrid, 1);
                mainGrid.Children.Add(contentGrid);

                // 异步初始化并加载 CHM（传递目录树和按钮引用）
                _ = LoadChmInWebViewAsync(webView, filePath, openButton, treeView);

                return mainGrid;
            }
            catch (Exception ex)
            {
                return CreateDocumentErrorPanel($"CHM 预览初始化失败: {ex.Message}");
            }
        }

        private async Task LoadChmInWebViewAsync(WebView2 webView, string filePath, Button fallbackButton, TreeView treeView)
        {
            try
            {
                await webView.EnsureCoreWebView2Async(null);

                if (webView.CoreWebView2 != null)
                {
                    // 设置安全策略
                    webView.CoreWebView2.Settings.IsScriptEnabled = false;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                    webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                    // 查找 7-Zip 可执行文件（优先使用 7zG.exe，支持 CHM）
                    var sevenZipPath = FindSevenZipPath();
                    if (string.IsNullOrEmpty(sevenZipPath))
                    {
                        // 如果找不到 7-Zip，使用系统查看器
                        webView.CoreWebView2.NavigateToString(
                            "<html><body style='font-family: Arial; padding: 20px; text-align: center;'>" +
                            "<h2>无法找到 7-Zip</h2>" +
                            "<p>CHM 文件预览需要 7-Zip 工具。</p>" +
                            "<p>请安装 7-Zip 或使用系统查看器打开此文件。</p>" +
                            "</body></html>");
                        return;
                    }

                    // 使用缓存机制：基于文件路径和修改时间生成唯一标识
                    var fileInfo = new FileInfo(filePath);
                    var fileHash = $"{filePath.GetHashCode():X8}_{fileInfo.LastWriteTime.Ticks:X16}";
                    var cacheDir = Path.Combine(Path.GetTempPath(), "MRR_CHM_Cache", fileHash);
                    
                    // 检查缓存是否存在
                    string tempDir;
                    bool needExtract = true;
                    
                    if (Directory.Exists(cacheDir))
                    {
                        // 检查缓存是否完整（至少包含一些 HTML 文件）
                        var htmlFiles = Directory.GetFiles(cacheDir, "*.html", SearchOption.AllDirectories)
                            .Concat(Directory.GetFiles(cacheDir, "*.htm", SearchOption.AllDirectories));
                        
                        if (htmlFiles.Any())
                        {
                            tempDir = cacheDir;
                            needExtract = false;
                            System.Diagnostics.Debug.WriteLine($"使用缓存目录: {tempDir}");
                        }
                        else
                        {
                            // 缓存不完整，删除并重新解压
                            try { Directory.Delete(cacheDir, true); } catch { }
                            tempDir = cacheDir;
                            Directory.CreateDirectory(tempDir);
                        }
                    }
                    else
                    {
                        tempDir = cacheDir;
                        Directory.CreateDirectory(tempDir);
                    }

                    try
                    {
                        // 只有在需要时才解压
                        if (needExtract)
                        {
                            // 使用 7-Zip 解压 CHM（完全后台，无窗口闪烁）
                            var arguments = $"x \"{filePath}\" -o\"{tempDir}\" -y";
                            System.Diagnostics.Debug.WriteLine($"执行命令: {sevenZipPath} {arguments}");
                            
                            var processInfo = new ProcessStartInfo
                            {
                                FileName = sevenZipPath,
                                Arguments = arguments,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden, // 完全隐藏窗口
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                StandardOutputEncoding = Encoding.UTF8,
                                StandardErrorEncoding = Encoding.UTF8,
                                WorkingDirectory = Path.GetDirectoryName(sevenZipPath) // 设置工作目录
                            };

                            string output = "";
                            string error = "";

                            using (var process = Process.Start(processInfo))
                            {
                                if (process != null)
                                {
                                    output = await process.StandardOutput.ReadToEndAsync();
                                    error = await process.StandardError.ReadToEndAsync();
                                    await process.WaitForExitAsync();
                                    
                                    // 调试信息：记录输出和错误
                                    System.Diagnostics.Debug.WriteLine($"7-Zip 退出代码: {process.ExitCode}");
                                    if (!string.IsNullOrEmpty(output))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"7-Zip 输出: {output}");
                                    }
                                    if (!string.IsNullOrEmpty(error))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"7-Zip 错误: {error}");
                                    }

                                    // 7-Zip 退出代码说明：
                                    // 0 = 成功
                                    // 1 = 警告（某些文件无法解压，但部分成功）
                                    // 2 = 致命错误
                                    // 7 = 命令行错误
                                    // 8 = 内存不足
                                    // 255 = 用户中断
                                    
                                    // 即使退出代码不为 0，也检查是否解压了文件
                                    if (process.ExitCode != 0 && process.ExitCode != 1)
                                    {
                                        var errorMsg = $"7-Zip 解压失败，退出代码: {process.ExitCode}";
                                        if (!string.IsNullOrEmpty(error))
                                        {
                                            errorMsg += $"\n错误信息: {error}";
                                        }
                                        throw new Exception(errorMsg);
                                    }
                                }
                            }

                            // 检查是否解压了文件
                            if (!Directory.Exists(tempDir) || Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Length == 0)
                            {
                                var errorMsg = "7-Zip 解压后未找到任何文件";
                                if (!string.IsNullOrEmpty(error))
                                {
                                    errorMsg += $"\n错误信息: {error}";
                                }
                                throw new Exception(errorMsg);
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("使用缓存，跳过解压");
                        }

                        // 解析并构建目录树
                        await webView.Dispatcher.InvokeAsync(() =>
                        {
                            BuildChmTreeView(treeView, tempDir, webView);
                        });

                        // 查找主 HTML 文件
                        var mainHtmlFile = FindMainHtmlFile(tempDir);
                        if (string.IsNullOrEmpty(mainHtmlFile))
                        {
                            throw new Exception("无法找到 CHM 主页面文件（index.html、default.html 等）");
                        }

                        // 转换为 file:// URI
                        var uri = new Uri(mainHtmlFile).AbsoluteUri;
                        webView.CoreWebView2.Navigate(uri);

                        // 缓存目录不自动清理，保留供后续使用
                        // 可以定期清理旧缓存（例如在应用启动时）
                    }
                    catch (Exception ex)
                    {
                        // 清理临时目录
                        try
                        {
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }
                        catch { }

                        // 如果解压失败，显示错误并提供回退按钮
                        var fallbackHtml = $@"
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; padding: 20px; text-align: center; }}
        .error {{ color: #d32f2f; margin: 20px 0; font-size: 14px; }}
        .info {{ color: #666; margin: 10px 0; font-size: 13px; }}
    </style>
</head>
<body>
    <h2>CHM 加载失败</h2>
    <div class='error'>{System.Security.SecurityElement.Escape(ex.Message)}</div>
    <div class='info'>7-Zip 可能不完全支持此 CHM 文件格式（LZX 压缩）。</div>
    <div class='info'>请点击上方按钮使用系统查看器打开此文件。</div>
</body>
</html>";
                        
                        webView.CoreWebView2.NavigateToString(fallbackHtml);
                        
                        // 显示回退按钮
                        await webView.Dispatcher.InvokeAsync(() =>
                        {
                            fallbackButton.Visibility = Visibility.Visible;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CHM WebView2 初始化失败: {ex.Message}");
            }
        }

        private string FindSevenZipPath()
        {
            // 查找 7-Zip 可执行文件（优先查找 7zG.exe，因为它支持 CHM 格式）
            var possiblePaths = new[]
            {
                // 优先查找 7zG.exe（GUI 版本，支持 CHM，支持命令行参数）
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7-Zip", "7zG.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7zG.exe"),
                @"C:\Program Files\7-Zip\7zG.exe",
                @"C:\Program Files (x86)\7-Zip\7zG.exe",
                // 其次查找 7z.exe（命令行版本）
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7-Zip", "7z.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe"),
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe"
            };

            var foundPath = possiblePaths.FirstOrDefault(File.Exists);
            
            // 调试信息：记录找到的 7-Zip 路径
            if (foundPath != null)
            {
                System.Diagnostics.Debug.WriteLine($"找到 7-Zip: {foundPath}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("未找到 7-Zip 可执行文件");
            }
            
            return foundPath;
        }

        private string FindMainHtmlFile(string directory)
        {
            // 方法 1: 尝试从 #SYSTEM 文件读取默认页面（CHM 标准方式）
            var systemFile = Path.Combine(directory, "#SYSTEM");
            if (File.Exists(systemFile))
            {
                try
                {
                    var bytes = File.ReadAllBytes(systemFile);
                    var content = Encoding.ASCII.GetString(bytes);
                    
                    // 查找默认主题（DEFTOPIC），格式：DEFTOPIC + 字符串
                    var defTopicPattern = new System.Text.RegularExpressions.Regex(
                        @"DEFTOPIC\s+([^\x00\r\n]+)", 
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    var match = defTopicPattern.Match(content);
                    
                    if (match.Success)
                    {
                        var defaultPage = match.Groups[1].Value.Trim('\0', ' ', '\t', '\r', '\n');
                        if (!string.IsNullOrEmpty(defaultPage))
                        {
                            // 尝试直接路径
                            var defaultPath = Path.Combine(directory, defaultPage);
                            if (File.Exists(defaultPath))
                            {
                                System.Diagnostics.Debug.WriteLine($"从 #SYSTEM 找到默认页面: {defaultPath}");
                                return defaultPath;
                            }
                            
                            // 尝试添加 .html 扩展名
                            if (!defaultPage.EndsWith(".html", StringComparison.OrdinalIgnoreCase) && 
                                !defaultPage.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                            {
                                defaultPath = Path.Combine(directory, defaultPage + ".html");
                                if (File.Exists(defaultPath))
                                {
                                    System.Diagnostics.Debug.WriteLine($"从 #SYSTEM 找到默认页面（添加扩展名）: {defaultPath}");
                                    return defaultPath;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"读取 #SYSTEM 文件失败: {ex.Message}");
                }
            }

            // 方法 2: 查找常见的入口文件名（按优先级）
            var htmlFiles = new[] { "index.html", "default.html", "main.html", "contents.html", "welcome.html", "start.html", "home.html" };

            // 先查找根目录
            foreach (var htmlFile in htmlFiles)
            {
                var path = Path.Combine(directory, htmlFile);
                if (File.Exists(path))
                {
                    System.Diagnostics.Debug.WriteLine($"找到常见入口文件: {path}");
                    return path;
                }
            }

            // 方法 3: 查找根目录下的所有 HTML 文件，智能排序
            var rootHtmlFiles = Directory.GetFiles(directory, "*.html", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(directory, "*.htm", SearchOption.TopDirectoryOnly))
                .ToList();
            
            if (rootHtmlFiles.Count > 0)
            {
                // 按文件名智能排序：优先选择可能的主页面
                var sortedFiles = rootHtmlFiles.OrderBy(f =>
                {
                    var name = Path.GetFileNameWithoutExtension(f).ToLower();
                    // 优先级：main/index/start > content/welcome/home > 其他
                    if (name == "main" || name == "index" || name == "start" || name == "home")
                        return 0;
                    if (name.Contains("main") || name.Contains("index") || name.Contains("start"))
                        return 1;
                    if (name.Contains("content") || name.Contains("welcome") || name.Contains("home"))
                        return 2;
                    if (name.Contains("database") && name.Contains("main"))
                        return 3;
                    if (name.Contains("datasource") && name.Contains("main"))
                        return 4;
                    return 10; // 其他文件优先级最低
                }).ThenBy(f => Path.GetFileName(f)); // 相同优先级按文件名排序
                
                var selected = sortedFiles.First();
                System.Diagnostics.Debug.WriteLine($"从根目录选择入口文件: {selected}");
                return selected;
            }

            // 方法 4: 查找所有 HTML 文件（包括子目录），智能排序
            var allHtmlFiles = Directory.GetFiles(directory, "*.html", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(directory, "*.htm", SearchOption.AllDirectories))
                .ToList();
            
            if (allHtmlFiles.Count > 0)
            {
                // 优先选择根目录或浅层目录的文件
                var sortedFiles = allHtmlFiles.OrderBy(f =>
                {
                    var relativePath = Path.GetRelativePath(directory, f);
                    var depth = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length - 1;
                    var name = Path.GetFileNameWithoutExtension(f).ToLower();
                    
                    // 深度优先（根目录优先），然后按文件名优先级
                    int namePriority = 10;
                    if (name == "main" || name == "index" || name == "start" || name == "home")
                        namePriority = 0;
                    else if (name.Contains("main") || name.Contains("index") || name.Contains("start"))
                        namePriority = 1;
                    else if (name.Contains("content") || name.Contains("welcome") || name.Contains("home"))
                        namePriority = 2;
                    
                    return depth * 100 + namePriority; // 深度权重更大
                }).ThenBy(f => Path.GetFileName(f));
                
                var selected = sortedFiles.First();
                System.Diagnostics.Debug.WriteLine($"从所有文件选择入口文件: {selected}");
                return selected;
            }

            System.Diagnostics.Debug.WriteLine("未找到任何 HTML 文件");
            return null;
        }

        private void BuildChmTreeView(TreeView treeView, string tempDir, WebView2 webView)
        {
            // 存储 webView 引用到 TreeView 的 Tag 中，以便在事件处理中使用
            treeView.Tag = webView;
            
            try
            {
                treeView.Items.Clear();
                // 直接列出所有 HTML 页面
                BuildSimpleTreeFromHtmlFiles(treeView, tempDir, webView);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"构建目录树失败: {ex.Message}");
            }
        }


        private void BuildSimpleTreeFromHtmlFiles(TreeView treeView, string tempDir, WebView2 webView)
        {
            try
            {
                // 获取所有 HTML 文件
                var htmlFiles = Directory.GetFiles(tempDir, "*.html", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(tempDir, "*.htm", SearchOption.AllDirectories))
                    .OrderBy(f => f)
                    .ToList();

                foreach (var htmlFile in htmlFiles)
                {
                    var relativePath = Path.GetRelativePath(tempDir, htmlFile);
                    var fileName = Path.GetFileNameWithoutExtension(htmlFile);

                    var item = new TreeViewItem
                    {
                        Header = fileName,
                        Tag = relativePath
                    };

                    item.MouseDoubleClick += (s, e) =>
                    {
                        LoadHtmlInWebView(webView, tempDir, relativePath);
                    };

                    treeView.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"构建简单目录树失败: {ex.Message}");
            }
        }

        private void LoadHtmlInWebView(WebView2 webView, string baseDir, string relativePath)
        {
            try
            {
                var fullPath = Path.Combine(baseDir, relativePath);
                if (File.Exists(fullPath))
                {
                    var uri = new Uri(fullPath).AbsoluteUri;
                    webView.CoreWebView2?.Navigate(uri);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载 HTML 文件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从DOCX文件生成HTML（包含文本和图片）
        /// </summary>
        private string GenerateHtmlFromDocx(string filePath)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html><head>");
            sb.Append("<meta charset='utf-8'>");
            sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("<style>");
            sb.Append("body {");
            sb.Append("  font-family: 'Segoe UI', 'Microsoft YaHei', Arial, sans-serif;");
            sb.Append("  margin: 0;");
            sb.Append("  padding: 20px 40px;");
            sb.Append("  line-height: 1.8;");
            sb.Append("  color: #333;");
            sb.Append("  background: #fff;");
            sb.Append("  max-width: 100%;");
            sb.Append("  word-wrap: break-word;");
            sb.Append("  overflow-wrap: break-word;");
            sb.Append("}");
            sb.Append("p {");
            sb.Append("  margin: 12px 0;");
            sb.Append("  padding: 0;");
            sb.Append("  text-align: justify;");
            sb.Append("}");
            sb.Append("img {");
            sb.Append("  max-width: 100%;");
            sb.Append("  height: auto;");
            sb.Append("  display: block;");
            sb.Append("  margin: 12px auto;");
            sb.Append("}");
            sb.Append("</style>");
            sb.Append("</head><body>");

            using (var wordDoc = WordprocessingDocument.Open(filePath, false))
            {
                var mainPart = wordDoc.MainDocumentPart;
                if (mainPart == null || mainPart.Document == null || mainPart.Document.Body == null)
                {
                    throw new Exception("无法读取DOCX文档结构");
                }
                
                var body = mainPart.Document.Body;
                
                // 提取图片映射（关系ID -> 图片数据）
                var imageMap = ExtractImages(mainPart);
                System.Diagnostics.Debug.WriteLine($"图片映射表创建完成，包含 {imageMap.Count} 个图片");
                
                // 遍历文档元素
                foreach (var element in body.Elements())
                {
                    ProcessElement(element, sb, imageMap, mainPart);
                }
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// 提取图片到base64映射
        /// </summary>
        private Dictionary<string, string> ExtractImages(MainDocumentPart mainPart)
        {
            var imageMap = new Dictionary<string, string>();
            var imagePartUriMap = new Dictionary<string, string>(); // URI -> 关系ID的映射
            
            try
            {
                if (mainPart == null)
                    return imageMap;

                // 首先，通过关系文件建立URI到关系ID的映射
                try
                {
                    if (mainPart.Parts != null)
                    {
                        foreach (var part in mainPart.Parts)
                        {
                            if (part.OpenXmlPart is ImagePart imagePart)
                            {
                                var uri = part.OpenXmlPart.Uri?.ToString() ?? "";
                                var relId = part.RelationshipId ?? "";
                                if (!string.IsNullOrEmpty(uri) && !string.IsNullOrEmpty(relId))
                                {
                                    imagePartUriMap[uri] = relId;
                                    System.Diagnostics.Debug.WriteLine($"映射: URI={uri}, 关系ID={relId}");
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"建立URI映射失败: {ex.Message}");
                }

                // 遍历所有图片部分
                int imageCount = 0;
                foreach (var imagePart in mainPart.ImageParts)
                {
                    try
                    {
                        if (imagePart == null)
                            continue;

                        // 获取关系ID - 尝试多种方式
                        string relationshipId = null;
                        
                        // 方法1: 通过URI映射获取
                        var uri = imagePart.Uri?.ToString() ?? "";
                        if (imagePartUriMap.ContainsKey(uri))
                        {
                            relationshipId = imagePartUriMap[uri];
                            System.Diagnostics.Debug.WriteLine($"通过URI映射获取关系ID: {relationshipId}");
                        }
                        
                        // 方法2: 通过GetIdOfPart获取
                        if (string.IsNullOrEmpty(relationshipId))
                        {
                            try
                            {
                                relationshipId = mainPart.GetIdOfPart(imagePart);
                                System.Diagnostics.Debug.WriteLine($"通过GetIdOfPart获取关系ID: {relationshipId}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"GetIdOfPart失败: {ex.Message}");
                            }
                        }
                        
                        if (string.IsNullOrEmpty(relationshipId))
                        {
                            System.Diagnostics.Debug.WriteLine($"图片关系ID为空，URI: {uri}，跳过");
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"提取图片，关系ID: {relationshipId}, URI: {uri}");

                        using (var stream = imagePart.GetStream())
                        {
                            if (stream == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"图片流为null，跳过");
                                continue;
                            }
                            
                            // ZIP流不支持Seek，所以使用MemoryStream来缓冲
                            using (var memoryStream = new MemoryStream())
                            {
                                stream.CopyTo(memoryStream);
                                
                                if (memoryStream.Length == 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"图片数据为空，跳过");
                                    continue;
                                }
                                
                                if (memoryStream.Length > 50 * 1024 * 1024) // 限制50MB
                                {
                                    System.Diagnostics.Debug.WriteLine($"图片太大 ({memoryStream.Length} 字节)，跳过");
                                    continue;
                                }

                                byte[] imageBytes = memoryStream.ToArray();
                                
                                if (imageBytes.Length == 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"未读取到图片数据，跳过");
                                    continue;
                                }
                            
                            // 确定MIME类型
                            string mimeType = "image/png";
                            try
                            {
                                var uriStr = imagePart.Uri?.ToString() ?? "";
                                var extension = System.IO.Path.GetExtension(uriStr).ToLower();
                                if (extension == ".jpg" || extension == ".jpeg")
                                    mimeType = "image/jpeg";
                                else if (extension == ".gif")
                                    mimeType = "image/gif";
                                else if (extension == ".bmp")
                                    mimeType = "image/bmp";
                                else if (extension == ".png")
                                    mimeType = "image/png";
                                else if (extension == ".wmf" || extension == ".emf")
                                    mimeType = "image/x-wmf"; // Windows图元文件
                                
                                System.Diagnostics.Debug.WriteLine($"图片类型: {mimeType}, 扩展名: {extension}");
                            }
                            catch
                            {
                                // 使用默认PNG类型
                            }
                            
                                string base64 = Convert.ToBase64String(imageBytes);
                                string imageData = $"data:{mimeType};base64,{base64}";
                                
                                // 存储关系ID（使用多种格式以确保匹配）
                                imageMap[relationshipId] = imageData;
                                
                                // 如果ID不是rId格式，也尝试添加rId前缀
                                if (!relationshipId.StartsWith("rId", StringComparison.OrdinalIgnoreCase))
                                {
                                    // 尝试从关系ID中提取数字部分，然后添加rId前缀
                                    var match = System.Text.RegularExpressions.Regex.Match(relationshipId, @"(\d+)");
                                    if (match.Success)
                                    {
                                        var numericPart = match.Groups[1].Value;
                                        var rIdFormat = $"rId{numericPart}";
                                        if (!imageMap.ContainsKey(rIdFormat))
                                        {
                                            imageMap[rIdFormat] = imageData;
                                            System.Diagnostics.Debug.WriteLine($"同时存储rId格式: {rIdFormat}");
                                        }
                                    }
                                }
                                
                                // 也存储URI作为键（以防万一）
                                if (!string.IsNullOrEmpty(uri) && !imageMap.ContainsKey(uri))
                                {
                                    imageMap[uri] = imageData;
                                    System.Diagnostics.Debug.WriteLine($"同时存储URI格式: {uri}");
                                }
                                
                                imageCount++;
                                System.Diagnostics.Debug.WriteLine($"成功提取图片 {imageCount}，关系ID: {relationshipId}, Base64长度: {base64.Length}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"提取单个图片失败: {ex.Message}\n{ex.StackTrace}");
                        // 忽略单个图片的提取错误，继续处理其他图片
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"总共提取了 {imageCount} 张图片，映射表包含 {imageMap.Count} 个条目");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"提取图片列表失败: {ex.Message}\n{ex.StackTrace}");
                // 忽略图片提取错误，至少显示文本内容
            }
            
            return imageMap;
        }

        /// <summary>
        /// 处理文档元素（段落、图片等）
        /// </summary>
        private void ProcessElement(OpenXmlElement element, StringBuilder sb, Dictionary<string, string> imageMap, MainDocumentPart mainPart)
        {
            if (element is DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph)
            {
                var hasContent = false;
                var paraSb = new StringBuilder();
                
                foreach (var run in paragraph.Elements<DocumentFormat.OpenXml.Wordprocessing.Run>())
                {
                    // 处理文本
                    foreach (var text in run.Elements<DocumentFormat.OpenXml.Wordprocessing.Text>())
                    {
                        var textValue = text.Text;
                        if (!string.IsNullOrWhiteSpace(textValue))
                        {
                            paraSb.Append(WebUtility.HtmlEncode(textValue));
                            hasContent = true;
                        }
                    }
                    
                    // 处理图片
                    foreach (var drawing in run.Elements<DocumentFormat.OpenXml.Wordprocessing.Drawing>())
                    {
                        System.Diagnostics.Debug.WriteLine($"处理Drawing元素");
                        var imageData = ExtractImageFromDrawing(drawing, imageMap, mainPart);
                        if (!string.IsNullOrEmpty(imageData))
                        {
                            // 确保Base64数据正确（截取前50个字符用于调试）
                            var preview = imageData.Length > 50 ? imageData.Substring(0, 50) + "..." : imageData;
                            System.Diagnostics.Debug.WriteLine($"找到图片数据，长度: {imageData.Length}, 预览: {preview}");
                            paraSb.Append($"<img src=\"{imageData}\" alt=\"图片\" style=\"max-width: 100%; height: auto; display: block; margin: 12px auto;\" />");
                            hasContent = true;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"未找到图片数据");
                        }
                    }
                }
                
                if (hasContent)
                {
                    sb.Append("<p>");
                    sb.Append(paraSb.ToString());
                    sb.Append("</p>");
                }
            }
            else if (element is DocumentFormat.OpenXml.Wordprocessing.Table table)
            {
                // 处理表格（简化处理）
                sb.Append("<table border='1' cellpadding='5' style='border-collapse: collapse; margin: 12px 0; width: 100%;'>");
                foreach (var row in table.Elements<DocumentFormat.OpenXml.Wordprocessing.TableRow>())
                {
                    sb.Append("<tr>");
                    foreach (var cell in row.Elements<DocumentFormat.OpenXml.Wordprocessing.TableCell>())
                    {
                        sb.Append("<td>");
                        foreach (var para in cell.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                        {
                            var text = para.InnerText;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                sb.Append(WebUtility.HtmlEncode(text));
                            }
                        }
                        sb.Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }
            else
            {
                // 其他元素，提取文本
                var text = element.InnerText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.Append($"<p>{WebUtility.HtmlEncode(text)}</p>");
                }
            }
        }

        /// <summary>
        /// 从Drawing元素提取图片
        /// </summary>
        private string ExtractImageFromDrawing(DocumentFormat.OpenXml.Wordprocessing.Drawing drawing, Dictionary<string, string> imageMap, MainDocumentPart mainPart)
        {
            if (drawing == null)
            {
                System.Diagnostics.Debug.WriteLine("Drawing元素为null");
                return null;
            }
            
            if (imageMap == null || imageMap.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"图片映射表为空或没有图片 (Count: {imageMap?.Count ?? 0})");
                return null;
            }

            try
            {
                // 使用XML方式查找（最可靠）
                var xml = drawing.OuterXml;
                if (string.IsNullOrEmpty(xml))
                {
                    System.Diagnostics.Debug.WriteLine("Drawing的XML为空");
                    return null;
                }

                System.Diagnostics.Debug.WriteLine($"解析Drawing XML，长度: {xml.Length}");
                var doc = XDocument.Parse(xml);
                
                // 查找图片关系ID - 搜索所有命名空间
                XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
                XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";
                
                // 输出所有可用的关系ID
                System.Diagnostics.Debug.WriteLine($"可用的图片关系ID: {string.Join(", ", imageMap.Keys)}");
                
                // 方法1: 查找所有embed属性（最直接的方法）
                var allEmbedAttrs = doc.Descendants()
                    .SelectMany(e => e.Attributes())
                    .Where(attr => attr.Name.LocalName == "embed");
                
                System.Diagnostics.Debug.WriteLine($"找到 {allEmbedAttrs.Count()} 个embed属性");
                
                foreach (var embed in allEmbedAttrs)
                {
                    var embedId = embed.Value?.Trim();
                    if (string.IsNullOrEmpty(embedId))
                        continue;
                        
                    System.Diagnostics.Debug.WriteLine($"检查embed ID: '{embedId}', 命名空间: {embed.Name.NamespaceName}, 是否在映射表中: {imageMap.ContainsKey(embedId)}");
                    
                    // 尝试直接匹配
                    if (imageMap.ContainsKey(embedId))
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ 找到匹配的图片（直接匹配），ID: {embedId}");
                        return imageMap[embedId];
                    }
                    
                    // 尝试不区分大小写匹配
                    var matchedKey = imageMap.Keys.FirstOrDefault(k => 
                        string.Equals(k, embedId, StringComparison.OrdinalIgnoreCase));
                    if (matchedKey != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ 找到匹配的图片（不区分大小写），ID: {matchedKey}");
                        return imageMap[matchedKey];
                    }
                    
                    // 尝试匹配rId格式（如果embedId不是rId格式）
                    if (!embedId.StartsWith("rId", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(embedId, @"(\d+)");
                        if (match.Success)
                        {
                            var numericPart = match.Groups[1].Value;
                            var rIdFormat = $"rId{numericPart}";
                            if (imageMap.ContainsKey(rIdFormat))
                            {
                                System.Diagnostics.Debug.WriteLine($"✓ 找到匹配的图片（rId格式转换），ID: {rIdFormat}");
                                return imageMap[rIdFormat];
                            }
                        }
                    }
                }

                // 方法2: 查找带命名空间的blip元素
                var blipElements = doc.Descendants()
                    .Where(e => e.Name.LocalName == "blip");
                
                System.Diagnostics.Debug.WriteLine($"找到 {blipElements.Count()} 个blip元素");
                
                foreach (var blip in blipElements)
                {
                    // 查找embed属性（可能在不同的命名空间）
                    var embedAttrs = blip.Attributes()
                        .Where(attr => attr.Name.LocalName == "embed");
                    
                    foreach (var embed in embedAttrs)
                    {
                        var embedId = embed.Value;
                        System.Diagnostics.Debug.WriteLine($"Blip中找到embed ID: {embedId}");
                        
                        if (!string.IsNullOrEmpty(embedId) && imageMap.ContainsKey(embedId))
                        {
                            System.Diagnostics.Debug.WriteLine($"找到匹配的图片（通过blip），ID: {embedId}");
                            return imageMap[embedId];
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"未找到匹配的图片。可用ID列表: {string.Join(", ", imageMap.Keys)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从Drawing提取图片失败: {ex.Message}\n{ex.StackTrace}");
            }
            
            return null;
        }

        /// <summary>
        /// 生成简单文本预览（当完整预览失败时使用）
        /// </summary>
        private string GenerateSimpleTextPreview(string filePath)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html><head>");
            sb.Append("<meta charset='utf-8'>");
            sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("<style>");
            sb.Append("body {");
            sb.Append("  font-family: 'Segoe UI', 'Microsoft YaHei', Arial, sans-serif;");
            sb.Append("  margin: 0;");
            sb.Append("  padding: 20px 40px;");
            sb.Append("  line-height: 1.8;");
            sb.Append("  color: #333;");
            sb.Append("  background: #fff;");
            sb.Append("}");
            sb.Append("p {");
            sb.Append("  margin: 12px 0;");
            sb.Append("  padding: 0;");
            sb.Append("  text-align: justify;");
            sb.Append("}");
            sb.Append("</style>");
            sb.Append("</head><body>");

            using (var wordDoc = WordprocessingDocument.Open(filePath, false))
            {
                var body = wordDoc.MainDocumentPart.Document.Body;
                var paragraphs = new List<string>();
                
                foreach (var element in body.Elements())
                {
                    var text = element.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        paragraphs.Add(WebUtility.HtmlEncode(text));
                    }
                }

                foreach (var para in paragraphs)
                {
                    sb.Append($"<p>{para}</p>");
                }
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }

        /// <summary>
        /// 从文本段落生成HTML
        /// </summary>
        private string GenerateHtmlFromText(List<string> paragraphs, string fileName)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html><head>");
            sb.Append("<meta charset='utf-8'>");
            sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("<style>");
            sb.Append("body {");
            sb.Append("  font-family: 'Segoe UI', 'Microsoft YaHei', Arial, sans-serif;");
            sb.Append("  margin: 0;");
            sb.Append("  padding: 20px 40px;");
            sb.Append("  line-height: 1.8;");
            sb.Append("  color: #333;");
            sb.Append("  background: #fff;");
            sb.Append("  max-width: 100%;");
            sb.Append("  word-wrap: break-word;");
            sb.Append("  overflow-wrap: break-word;");
            sb.Append("}");
            sb.Append("p {");
            sb.Append("  margin: 12px 0;");
            sb.Append("  padding: 0;");
            sb.Append("  text-align: justify;");
            sb.Append("}");
            sb.Append("</style>");
            sb.Append("</head><body>");
            
            foreach (var para in paragraphs)
            {
                sb.Append($"<p>{para}</p>");
            }
            
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private UIElement CreateTextPreview(string text, string fileName)
        {
            var textBox = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                Margin = new Thickness(10),
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Gray,
                FontFamily = new System.Windows.Media.FontFamily("Consolas, Monaco, 'Courier New', monospace"),
                FontSize = 12
            };

            var panel = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(10)
            };

            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(fileName) };
            var title = PreviewHelper.CreateTitlePanel("📄", $"文本内容: {fileName}", buttons);
            panel.Children.Add(title);

            panel.Children.Add(textBox);

            return panel;
        }

        private UIElement CreateDocumentErrorPanel(string message, string filePath = null)
        {
            var panel = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(10)
            };

            var buttons = new List<Button>();
            if (!string.IsNullOrEmpty(filePath))
            {
                buttons.Add(PreviewHelper.CreateOpenButton(filePath));
                // 如果是DOC文件，添加转换按钮
                if (Path.GetExtension(filePath).Equals(".doc", StringComparison.OrdinalIgnoreCase))
                {
                    buttons.Add(PreviewHelper.CreateDocToDocxButton(filePath));
                }
            }
            var title = PreviewHelper.CreateTitlePanel("📄", "文档预览", buttons);
            panel.Children.Add(title);

            var errorPanel = new System.Windows.Controls.Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 249, 250)),
                BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 10, 0, 0)
            };

            var textBlock = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 16,
                Margin = new Thickness(0, 10, 0, 20)
            };
            errorPanel.Child = textBlock;

            panel.Children.Add(errorPanel);

            return panel;
        }

    }
}


