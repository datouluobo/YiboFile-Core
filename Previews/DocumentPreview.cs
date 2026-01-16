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
using System.Runtime.InteropServices;
using System.Xml.Linq;
using YiboFile.Controls;

namespace YiboFile.Previews
{
    /// <summary>
    /// 文档文件预览（DOCX、DOC、PDF、RTF、CHM）
    /// </summary>
    public class DocumentPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();

            // 使用工厂模式的处理器
            var handler = DocumentHandlers.DocumentPreviewFactory.GetHandler(extension);
            if (handler != null)
            {
                return handler.CreatePreview(filePath);
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

                // 统一工具栏
                var toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "📄",
                    ShowSearch = false,
                    ShowWordWrap = false,
                    ShowEncoding = false,
                    ShowViewToggle = false,
                    ShowFormat = false
                };
                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                Grid.SetRow(toolbar, 0);
                grid.Children.Add(toolbar);

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
                            catch (Exception)
                            {
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
                            catch (Exception)
                            {// 回退到NavigateToString
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
            var mainContainer = new Grid();
            mainContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 初始标题栏
            // 统一工具栏
            var toolbar = new TextPreviewToolbar
            {
                FileName = Path.GetFileName(filePath),
                FileIcon = "📄",
                ShowSearch = false,
                ShowWordWrap = false,
                ShowEncoding = false,
                ShowViewToggle = false,
                ShowFormat = false
            };
            toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

            Grid.SetRow(toolbar, 0);
            mainContainer.Children.Add(toolbar);

            // 加载提示
            var loadingPanel = PreviewHelper.CreateLoadingPanel("⏳ 正在检测文档预览...");
            Grid.SetRow(loadingPanel, 1);
            mainContainer.Children.Add(loadingPanel);

            // 异步检查和转换
            Task.Run(() =>
            {
                try
                {
                    var tempDocx = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(filePath) + ".docx");
                    string errorMsg;
                    bool canPreview = ConvertDocToDocx(filePath, tempDocx, out errorMsg);
                    bool hasWord = errorMsg == null || !errorMsg.Contains("未检测到");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        mainContainer.Children.Remove(loadingPanel);

                        // 转换按钮
                        var convertButton = PreviewHelper.CreateConvertButton(
                            "🔄 转换为DOCX格式",
                            async (s, e) =>
                            {
                                var btn = s as Button;
                                try
                                {
                                    btn.IsEnabled = false;
                                    btn.Content = "⏳ 转换中...";

                                    string directory = Path.GetDirectoryName(filePath);
                                    string baseName = Path.GetFileNameWithoutExtension(filePath);
                                    string outputPath = Path.Combine(directory, baseName + ".docx");
                                    outputPath = GetUniqueFilePath(outputPath);

                                    string convertError = null;
                                    bool success = await Task.Run(() => ConvertDocToDocx(filePath, outputPath, out convertError));

                                    if (success)
                                    {
                                        btn.Content = "✅ 转换成功";
                                        MessageBox.Show($"文件已成功转换为DOCX格式：\n{outputPath}", "转换成功", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        string errorTitle = convertError?.Contains("未检测到") == true ? "需要 Microsoft Word" : "转换错误";
                                        MessageBox.Show(convertError ?? "转换失败", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                                        btn.IsEnabled = true;
                                        btn.Content = "🔄 转换为DOCX格式";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                    btn.IsEnabled = true;
                                    btn.Content = "🔄 转换为DOCX格式";
                                }
                            }
                        );

                        if (!hasWord)
                        {
                            convertButton.IsEnabled = false;
                            convertButton.ToolTip = "未检测到 Microsoft Word";
                        }

                        // 更新工具栏的操作内容
                        toolbar.CustomActionContent = convertButton;

                        if (canPreview)
                        {
                            var webView = new WebView2
                            {
                                VerticalAlignment = VerticalAlignment.Stretch,
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                MinHeight = 400
                            };
                            Grid.SetRow(webView, 1);
                            mainContainer.Children.Add(webView);

                            webView.Loaded += async (s, e) =>
                            {
                                try
                                {
                                    await webView.EnsureCoreWebView2Async();
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
                                                    var text = element.InnerText ?? "";
                                                    if (!string.IsNullOrWhiteSpace(text))
                                                        paragraphs.Add(WebUtility.HtmlEncode(text));
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

                            // 清理临时文件
                            Task.Delay(5000).ContinueWith(_ =>
                            {
                                try { if (File.Exists(tempDocx)) File.Delete(tempDocx); } catch { }
                            });
                        }
                        else
                        {
                            var infoPanel = PreviewHelper.CreateLegacyFormatPanel(
                                "DOC",
                                "该文件为旧的 Word 格式（.doc）。\n需安装 Microsoft Word 才能预览或转换。",
                                hasWord,
                                "转换为DOCX格式"
                            );
                            Grid.SetRow(infoPanel, 1);
                            mainContainer.Children.Add(infoPanel);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        mainContainer.Children.Remove(loadingPanel);
                        mainContainer.Children.Remove(toolbar); // 错误时移除工具栏，显示纯错误面板? 或者保留?
                        // 原始逻辑不仅 remove loadingPanel 还 remove titlePanel?
                        // 原始逻辑 line 339: mainContainer.Children.Remove(titlePanel);
                        // 如果出错，下面会添加 CreateErrorPreview 到 Row 1.
                        // 但是 toolbar 在 Row 0.
                        // 如果我不移除 toolbar，那么 Row 0 是 toolbar， Row 1 是 error panel. 
                        // 这样其实更好。
                        // 但是 PreviewHelper.CreateErrorPreview 返回的是一个Grid，通常包含了 reset/retry button etc? 
                        // 不，CreateErrorPreview 只是个 StackPanel showing text.
                        // 所以保留 toolbar 是合理的。
                        // 但为了保持原样，我先注释掉 remove toolbar.
                        // mainContainer.Children.Remove(toolbar);

                        var errorPanel = PreviewHelper.CreateErrorPreview($"DOC 预览检测失败: {ex.Message}");
                        Grid.SetRow(errorPanel, 1);
                        mainContainer.Children.Add(errorPanel);
                    });
                }
            });

            return mainContainer;
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
            // Note: This method is called inside Task.Run by the caller, so we can keep it synchronous here.
            // But to be safe, we verify it is not on the UI thread if possible, or just trust the caller.
            // The caller (CreateDocPreview -> convertButton.Click) already does Task.Run.
            // However, CreateDocPreview ALSO calls ConvertDocToDocx synchronously at the start to check if it can preview!
            // Line 192: bool canPreview = ConvertDocToDocx(filePath, tempDocx, out errorMsg);
            // This IS the bottleneck!

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
                    catch (COMException)
                    {
                        // 忽略退出时的 COM 异常
                    }
                    catch
                    {
                        // 忽略退出时的错误
                    }
                    try
                    {
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(wordApp);
                    }
                    catch (COMException)
                    {
                        // 忽略释放时的 COM 异常
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
                // 统一工具栏
                var toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "📄",
                    ShowSearch = false,
                    ShowWordWrap = false,
                    ShowEncoding = false,
                    ShowViewToggle = false,
                    ShowFormat = false
                };
                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                Grid.SetRow(toolbar, 0);
                grid.Children.Add(toolbar);

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
                    // 直接导航到PDF文件
                    webView.CoreWebView2.Navigate(uri);
                }
            }
            catch (Exception ex)
            {
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
                var grid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)), // 白色背景
                    Name = "RtfPreviewGrid"
                };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 统一工具栏
                var toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "📄",
                    ShowSearch = false,
                    ShowWordWrap = false,
                    ShowEncoding = false,
                    ShowViewToggle = false,
                    ShowFormat = false
                };
                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                Grid.SetRow(toolbar, 0);
                grid.Children.Add(toolbar);

                var rtfBox = new RichTextBox
                {
                    IsReadOnly = true,
                    Margin = new Thickness(10),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Gray,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                // 读取RTF内容到MemoryStream，避免文件流过早关闭
                byte[] rtfBytes = File.ReadAllBytes(filePath);
                using (var memStream = new MemoryStream(rtfBytes))
                {
                    var textRange = new TextRange(rtfBox.Document.ContentStart, rtfBox.Document.ContentEnd);
                    textRange.Load(memStream, DataFormats.Rtf);
                }


                Grid.SetRow(rtfBox, 1);
                grid.Children.Add(rtfBox);

                // 检查内容是否加载成功
                //TextRange checkRange = new TextRange(rtfBox.Document.ContentStart, rtfBox.Document.ContentEnd);
                //if (string.IsNullOrWhiteSpace(checkRange.Text))
                //{
                //    // 不要作为纯文本加载，这会显示原始RTF代码
                //    // rtfBox.Document.Blocks.Clear();
                //    // rtfBox.AppendText(File.ReadAllText(filePath));
                //}


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
                    // 移除外边距，改为统一的布局
                };

                // 定义列：标题行、内容行
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 标题
                // 统一工具栏
                var toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "📖",
                    ShowSearch = false,
                    ShowWordWrap = false,
                    ShowEncoding = false,
                    ShowViewToggle = false,
                    ShowFormat = false
                };
                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                Grid.SetRow(toolbar, 0);
                mainGrid.Children.Add(toolbar);

                // 内容容器
                var contentContainer = new Grid();
                Grid.SetRow(contentContainer, 1);
                mainGrid.Children.Add(contentContainer);

                // 1. 实际内容区域
                var contentGrid = new Grid
                {
                    Margin = new Thickness(0) // 贴边显示
                };

                // 定义列：目录树（250px） + 分割线（5px） + WebView2（剩余空间）
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250, GridUnitType.Pixel) });
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Pixel) });
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 目录树
                var treeView = new TreeView
                {
                    Background = Brushes.White,
                    BorderThickness = new Thickness(0, 0, 1, 0), // 只有右边框
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

                contentContainer.Children.Add(contentGrid);

                // 2. 加载遮罩 (默认显示)
                var loadingPanel = PreviewHelper.CreateLoadingPanel("正在解析 CHM 文件...");
                contentContainer.Children.Add(loadingPanel);

                // 异步初始化并加载 CHM（传递目录树引用 和 loadingPanel）
                _ = LoadChmInWebViewAsync(webView, filePath, treeView, loadingPanel);

                return mainGrid;
            }
            catch (Exception ex)
            {
                return CreateDocumentErrorPanel($"CHM 预览初始化失败: {ex.Message}");
            }
        }

        private async Task LoadChmInWebViewAsync(WebView2 webView, string filePath, TreeView treeView, UIElement loadingPanel)
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

                    // 查找 7-Zip 可执行文件
                    var sevenZipPath = FindSevenZipPath();

                    // 使用缓存机制：基于文件路径和修改时间生成唯一标识
                    var fileInfo = new FileInfo(filePath);
                    var fileHash = $"{filePath.GetHashCode():X8}_{fileInfo.LastWriteTime.Ticks:X16}";
                    var cacheDir = Path.Combine(Path.GetTempPath(), "MRR_CHM_Cache", fileHash);

                    // 检查缓存是否存在
                    string tempDir = cacheDir;
                    bool needExtract = true;

                    if (Directory.Exists(cacheDir))
                    {
                        // 检查缓存是否完整（至少包含一些 HTML 文件）
                        var htmlFiles = Directory.GetFiles(cacheDir, "*.html", SearchOption.AllDirectories)
                            .Concat(Directory.GetFiles(cacheDir, "*.htm", SearchOption.AllDirectories));

                        if (htmlFiles.Any())
                        {
                            needExtract = false;
                        }
                        else
                        {
                            // 缓存不完整，删除并重新解压
                            try { Directory.Delete(cacheDir, true); } catch { }
                            Directory.CreateDirectory(tempDir);
                        }
                    }
                    else
                    {
                        Directory.CreateDirectory(tempDir);
                    }

                    try
                    {
                        // 只有在需要时才解压
                        if (needExtract)
                        {
                            bool extracted = false;
                            string lastError = "";

                            // 1. 尝试使用 7-Zip 解压
                            if (!string.IsNullOrEmpty(sevenZipPath))
                            {
                                try
                                {
                                    var arguments = $"x \"{filePath}\" -o\"{tempDir}\" -y";
                                    var processInfo = new ProcessStartInfo
                                    {
                                        FileName = sevenZipPath,
                                        Arguments = arguments,
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        WindowStyle = ProcessWindowStyle.Hidden,
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        StandardOutputEncoding = Encoding.UTF8,
                                        StandardErrorEncoding = Encoding.UTF8,
                                        WorkingDirectory = Path.GetDirectoryName(sevenZipPath)
                                    };

                                    using (var process = Process.Start(processInfo))
                                    {
                                        if (process != null)
                                        {
                                            var output = await process.StandardOutput.ReadToEndAsync();
                                            var error = await process.StandardError.ReadToEndAsync();
                                            await process.WaitForExitAsync();

                                            // 检查是否解压了文件
                                            if (Directory.Exists(tempDir) && Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Length > 0)
                                            {
                                                extracted = true;
                                            }
                                            else
                                            {
                                                lastError = $"7-Zip 退出代码: {process.ExitCode}. {error}";
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    lastError = ex.Message;
                                }
                            }
                            else
                            {
                            }

                            // 2. 如果 7-Zip 失败，尝试使用 hh.exe 解压
                            if (!extracted)
                            {
                                extracted = await TryExtractWithHhExe(filePath, tempDir);

                                if (!extracted)
                                {
                                    // 如果 hh.exe 也失败，且之前是因为没找到 7-Zip，则报告未找到 7-Zip
                                    if (string.IsNullOrEmpty(sevenZipPath))
                                    {
                                        webView.CoreWebView2.NavigateToString(GenerateChmErrorHtml(ChmErrorType.SevenZipNotFound, "未找到 7-Zip 工具", filePath));
                                        Application.Current.Dispatcher.Invoke(() => loadingPanel.Visibility = Visibility.Collapsed);
                                        return;
                                    }

                                    // 否则报告解压失败（可能是 LZX 问题）
                                    var errorType = lastError.Contains("LZX") ? ChmErrorType.LzxCompressionIssue : ChmErrorType.ExtractionFailed;
                                    webView.CoreWebView2.NavigateToString(GenerateChmErrorHtml(
                                        lastError.Contains("LZX") ? ChmErrorType.LzxCompressionIssue : ChmErrorType.ExtractionFailed,
                                        lastError,
                                        filePath));

                                    // 清理空目录
                                    try { Directory.Delete(tempDir, true); } catch { }
                                    Application.Current.Dispatcher.Invoke(() => loadingPanel.Visibility = Visibility.Collapsed);
                                    return;
                                }
                            }
                        }
                        else
                        {
                        }

                        // 3. 验证解压结果
                        if (!Directory.Exists(tempDir) || Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Length == 0)
                        {
                            webView.CoreWebView2.NavigateToString(GenerateChmErrorHtml(ChmErrorType.NoHtmlFilesFound, "解压后未找到文件", filePath));
                            // 清理空目录
                            try { Directory.Delete(tempDir, true); } catch { }
                            Application.Current.Dispatcher.Invoke(() => loadingPanel.Visibility = Visibility.Collapsed);
                            return;
                        }

                        // 4. 解析并构建目录树
                        await webView.Dispatcher.InvokeAsync(() =>
                        {
                            BuildChmTreeView(treeView, tempDir, webView);
                            loadingPanel.Visibility = Visibility.Collapsed;
                        });

                        // 5. 查找主 HTML 文件
                        var mainHtmlFile = FindMainHtmlFile(tempDir);
                        if (string.IsNullOrEmpty(mainHtmlFile))
                        {
                            webView.CoreWebView2.NavigateToString(GenerateChmErrorHtml(ChmErrorType.NoHtmlFilesFound, "无法找到 CHM 主页面文件", filePath));
                            Application.Current.Dispatcher.Invoke(() => loadingPanel.Visibility = Visibility.Collapsed);
                            return;
                        }

                        // 6. 导航到主页面
                        var uri = new Uri(mainHtmlFile).AbsoluteUri;
                        webView.CoreWebView2.Navigate(uri);
                    }
                    catch (Exception ex)
                    {
                        // 清理临时目录
                        try
                        {
                            if (Directory.Exists(tempDir) && needExtract) // 仅在尝试解压失败时清理
                            {
                                Directory.Delete(tempDir, true);
                            }
                        }
                        catch { }

                        webView.CoreWebView2.NavigateToString(GenerateChmErrorHtml(ChmErrorType.Unknown, ex.Message, filePath));
                        Application.Current.Dispatcher.Invoke(() => loadingPanel.Visibility = Visibility.Collapsed);
                    }
                }
            }
            catch
            {
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
            }
            else
            {
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
                                return defaultPath;
                            }

                            // 尝试添加 .html 扩展名
                            if (!defaultPage.EndsWith(".html", StringComparison.OrdinalIgnoreCase) &&
                                !defaultPage.EndsWith(".htm", StringComparison.OrdinalIgnoreCase))
                            {
                                defaultPath = Path.Combine(directory, defaultPage + ".html");
                                if (File.Exists(defaultPath))
                                {
                                    return defaultPath;
                                }
                            }
                        }
                    }
                }
                catch
                {
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
                return selected;
            }

            return null;
        }

        private void BuildChmTreeView(TreeView treeView, string tempDir, WebView2 webView)
        {
            // 存储 webView 引用到 TreeView 的 Tag 中，以便在事件处理中使用
            treeView.Tag = webView;

            try
            {
                treeView.Items.Clear();

                // 尝试查找并解析 .hhc 文件（CHM目录文件）
                var hhcFile = FindHhcFile(tempDir);

                if (!string.IsNullOrEmpty(hhcFile) && File.Exists(hhcFile))
                {
                    // 从 .hhc 文件构建真实的CHM目录结构
                    BuildTreeFromHhc(treeView, hhcFile, tempDir, webView);
                }
                else
                {
                    // 退化模式：直接列出所有 HTML 页面
                    BuildSimpleTreeFromHtmlFiles(treeView, tempDir, webView);
                }
            }
            catch
            {
                // 出错时使用简单模式
                BuildSimpleTreeFromHtmlFiles(treeView, tempDir, webView);
            }
        }

        /// <summary>
        /// 查找CHM目录文件(.hhc)
        /// </summary>
        private string FindHhcFile(string directory)
        {
            try
            {
                // 查找所有.hhc文件
                var hhcFiles = Directory.GetFiles(directory, "*.hhc", SearchOption.AllDirectories);

                if (hhcFiles.Length > 0)
                {
                    // 优先选择根目录的.hhc文件
                    var rootHhc = hhcFiles.FirstOrDefault(f =>
                        Path.GetDirectoryName(f).Equals(directory, StringComparison.OrdinalIgnoreCase));

                    return rootHhc ?? hhcFiles[0];
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 从.hhc文件构建目录树
        /// </summary>
        private void BuildTreeFromHhc(TreeView treeView, string hhcFile, string baseDir, WebView2 webView)
        {
            try
            {
                // 读取HHC文件内容
                // CHM文件通常使用ANSI编码(中文环境为GBK), Encoding.Default在.NET Core中可能是UTF-8
                // 需要注册编码提供程序以支持GBK
                string htmlContent;
                try
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    htmlContent = File.ReadAllText(hhcFile, Encoding.GetEncoding("GB18030"));
                }
                catch
                {
                    // 如果失败，回退到默认编码
                    htmlContent = File.ReadAllText(hhcFile, Encoding.Default);
                }

                // 简化的HHC解析：查找所有<OBJECT>标签
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    htmlContent,
                    @"<OBJECT[^>]*>.*?</OBJECT>",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                    System.Text.RegularExpressions.RegexOptions.Singleline);

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var objectContent = match.Value;

                    // 提取 Name 参数（标题）
                    var nameMatch = System.Text.RegularExpressions.Regex.Match(
                        objectContent,
                        @"<param\s+name=""Name""\s+value=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    // 提取 Local 参数（HTML文件路径）
                    var localMatch = System.Text.RegularExpressions.Regex.Match(
                        objectContent,
                        @"<param\s+name=""Local""\s+value=""([^""]+)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                    if (nameMatch.Success)
                    {
                        var title = System.Net.WebUtility.HtmlDecode(nameMatch.Groups[1].Value);
                        var localPath = localMatch.Success ? localMatch.Groups[1].Value : null;

                        // 创建TreeViewItem
                        var item = new TreeViewItem
                        {
                            Header = title,
                            Tag = localPath
                        };

                        // 如果有对应的HTML文件，添加点击事件
                        if (!string.IsNullOrEmpty(localPath))
                        {
                            item.MouseDoubleClick += (s, e) =>
                            {
                                if (e.Source == item) // 确保事件来自当前项
                                {
                                    LoadHtmlInWebView(webView, baseDir, localPath);
                                    e.Handled = true;
                                }
                            };
                        }

                        // 添加到树中
                        treeView.Items.Add(item);
                    }
                }

                // 如果没有解析到任何项，退化到简单模式
                if (treeView.Items.Count == 0)
                {
                    BuildSimpleTreeFromHtmlFiles(treeView, baseDir, webView);
                }
            }
            catch
            {
                // 解析失败，使用简单模式
                BuildSimpleTreeFromHtmlFiles(treeView, baseDir, webView);
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
            catch
            {
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
            catch
            {
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
                                }
                            }
                        }
                    }
                }
                catch
                {
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
                        }

                        // 方法2: 通过GetIdOfPart获取
                        if (string.IsNullOrEmpty(relationshipId))
                        {
                            try
                            {
                                relationshipId = mainPart.GetIdOfPart(imagePart);
                            }
                            catch
                            {
                            }
                        }

                        if (string.IsNullOrEmpty(relationshipId))
                        {
                            continue;
                        }

                        using (var stream = imagePart.GetStream())
                        {
                            if (stream == null)
                            {
                                continue;
                            }

                            // ZIP流不支持Seek，所以使用MemoryStream来缓冲
                            using (var memoryStream = new MemoryStream())
                            {
                                stream.CopyTo(memoryStream);

                                if (memoryStream.Length == 0)
                                {
                                    continue;
                                }

                                if (memoryStream.Length > 50 * 1024 * 1024) // 限制50MB
                                {
                                    continue;
                                }

                                byte[] imageBytes = memoryStream.ToArray();

                                if (imageBytes.Length == 0)
                                {
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
                                        }
                                    }
                                }

                                // 也存储URI作为键（以防万一）
                                if (!string.IsNullOrEmpty(uri) && !imageMap.ContainsKey(uri))
                                {
                                    imageMap[uri] = imageData;
                                }

                                imageCount++;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // 忽略单个图片的提取错误，继续处理其他图片
                    }
                }
            }
            catch (Exception)
            {
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
                        var imageData = ExtractImageFromDrawing(drawing, imageMap, mainPart);
                        if (!string.IsNullOrEmpty(imageData))
                        {
                            // 确保Base64数据正确（截取前50个字符用于调试）
                            var preview = imageData.Length > 50 ? imageData.Substring(0, 50) + "..." : imageData;
                            paraSb.Append($"<img src=\"{imageData}\" alt=\"图片\" style=\"max-width: 100%; height: auto; display: block; margin: 12px auto;\" />");
                            hasContent = true;
                        }
                        else
                        {
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
                            var text = para.InnerText ?? "";
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
                var text = element.InnerText ?? "";
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
                return null;
            }

            if (imageMap == null || imageMap.Count == 0)
            {
                return null;
            }

            try
            {
                // 使用XML方式查找（最可靠）
                var xml = drawing.OuterXml;
                if (string.IsNullOrEmpty(xml))
                {
                    return null;
                }

                var doc = XDocument.Parse(xml);

                // 查找图片关系ID - 搜索所有命名空间
                XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
                XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";

                // 输出所有可用的关系ID
                // 方法1: 查找所有embed属性（最直接的方法）
                var allEmbedAttrs = doc.Descendants()
                    .SelectMany(e => e.Attributes())
                    .Where(attr => attr.Name.LocalName == "embed");
                foreach (var embed in allEmbedAttrs)
                {
                    var embedId = embed.Value?.Trim();
                    if (string.IsNullOrEmpty(embedId))
                        continue;
                    // 尝试直接匹配
                    if (imageMap.ContainsKey(embedId))
                    {
                        return imageMap[embedId];
                    }

                    // 尝试不区分大小写匹配
                    var matchedKey = imageMap.Keys.FirstOrDefault(k =>
                        string.Equals(k, embedId, StringComparison.OrdinalIgnoreCase));
                    if (matchedKey != null)
                    {
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
                                return imageMap[rIdFormat];
                            }
                        }
                    }
                }

                // 方法2: 查找带命名空间的blip元素
                var blipElements = doc.Descendants()
                    .Where(e => e.Name.LocalName == "blip");
                foreach (var blip in blipElements)
                {
                    // 查找embed属性（可能在不同的命名空间）
                    var embedAttrs = blip.Attributes()
                        .Where(attr => attr.Name.LocalName == "embed");

                    foreach (var embed in embedAttrs)
                    {
                        var embedId = embed.Value;
                        if (!string.IsNullOrEmpty(embedId) && imageMap.ContainsKey(embedId))
                        {
                            return imageMap[embedId];
                        }
                    }
                }
            }
            catch
            {
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
                    var text = element.InnerText ?? "";
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

        /// <summary>
        /// 尝试使用 hh.exe 解压 CHM 文件
        /// </summary>
        private async Task<bool> TryExtractWithHhExe(string chmPath, string outputDir)
        {
            var hhPath = FindHhExePath();
            if (string.IsNullOrEmpty(hhPath))
            {
                return false;
            }

            try
            {
                // hh.exe -decompile <outputDir> <chmPath>
                var arguments = $"-decompile \"{outputDir}\" \"{chmPath}\"";
                var processInfo = new ProcessStartInfo
                {
                    FileName = hhPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                        return false;

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (!string.IsNullOrEmpty(output))
                    {
                        // Output processed
                    }
                    if (!string.IsNullOrEmpty(error))
                    {
                        // Error processed
                    }
                }

                // 检查是否成功解压（存在 HTML 文件）
                if (Directory.Exists(outputDir))
                {
                    var htmlFiles = Directory.GetFiles(outputDir, "*.htm*", SearchOption.AllDirectories);
                    if (htmlFiles.Length > 0)
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 查找 hh.exe 路径
        /// </summary>
        private string FindHhExePath()
        {
            var paths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "hh.exe"),
                @"C:\Windows\hh.exe",
                @"C:\Windows\System32\hh.exe"
            };

            var foundPath = Array.Find(paths, File.Exists);
            if (foundPath != null)
            {
            }
            return foundPath;
        }

        /// <summary>
        /// CHM 错误类型
        /// </summary>
        private enum ChmErrorType
        {
            SevenZipNotFound,
            ExtractionFailed,
            LzxCompressionIssue,
            NoHtmlFilesFound,
            Unknown
        }

        /// <summary>
        /// 生成友好的错误 HTML
        /// </summary>
        private string GenerateChmErrorHtml(ChmErrorType errorType, string errorMessage, string filePath)
        {
            var fileName = Path.GetFileName(filePath);

            var (title, description, solutions) = errorType switch
            {
                ChmErrorType.SevenZipNotFound => (
                    "未找到 7-Zip",
                    "CHM 文件预览需要 7-Zip 工具来解压文件内容。",
                    new[]
                    {
                        "安装 7-Zip 到默认位置（C:\\Program Files\\7-Zip）",
                        "或将 7-Zip 复制到程序的 Dependencies/7-Zip 目录",
                        "点击上方按钮使用系统查看器打开"
                    }
                ),
                ChmErrorType.LzxCompressionIssue => (
                    "CHM 格式兼容性问题",
                    "此 CHM 文件使用 LZX 压缩格式，7-Zip 可能不完全支持。已尝试使用 hh.exe 解压但也失败了。",
                    new[]
                    {
                        "使用上方按钮通过系统查看器打开（推荐）",
                        "尝试使用其他 CHM 阅读器",
                        "检查文件是否完整或损坏"
                    }
                ),
                ChmErrorType.ExtractionFailed => (
                    "CHM 解压失败",
                    $"无法解压 CHM 文件。{errorMessage}",
                    new[]
                    {
                        "检查文件是否完整且未损坏",
                        "尝试使用外部程序打开",
                        "确保有足够的磁盘空间"
                    }
                ),
                ChmErrorType.NoHtmlFilesFound => (
                    "未找到内容",
                    "CHM 文件已解压，但未找到任何 HTML 内容文件。",
                    new[]
                    {
                        "文件可能已损坏",
                        "使用外部程序打开查看"
                    }
                ),
                _ => (
                    "CHM 加载失败",
                    errorMessage,
                    new[] { "使用上方按钮通过外部程序打开此文件" }
                )
            };

            var solutionsHtml = string.Join("", solutions.Select(s => $"<div class='solution'>{s}</div>"));

            return $@"
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: 'Segoe UI', 'Microsoft YaHei', Arial, sans-serif;
            padding: 30px;
            background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
            margin: 0;
        }}
        .container {{
            background: white;
            border-radius: 12px;
            padding: 30px;
            max-width: 650px;
            margin: 0 auto;
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }}
        .icon {{
            font-size: 56px;
            text-align: center;
            margin-bottom: 20px;
        }}
        h2 {{
            color: #d32f2f;
            margin: 0 0 15px 0;
            font-size: 24px;
            text-align: center;
        }}
        .description {{
            color: #555;
            margin: 15px 0 20px 0;
            line-height: 1.7;
            text-align: center;
        }}
        .solutions {{
            background: #e3f2fd;
            border-left: 4px solid #2196f3;
            padding: 20px;
            margin: 25px 0;
            border-radius: 6px;
        }}
        .solutions-title {{
            font-weight: 600;
            color: #1976d2;
            margin-bottom: 12px;
            font-size: 16px;
        }}
        .solution {{
            margin: 10px 0;
            padding-left: 24px;
            position: relative;
            line-height: 1.6;
        }}
        .solution:before {{
            content: '→';
            position: absolute;
            left: 0;
            color: #2196f3;
            font-weight: bold;
        }}
        .error-detail {{
            background: #fafafa;
            padding: 12px;
            border-radius: 6px;
            font-size: 13px;
            color: #666;
            margin-top: 20px;
            border: 1px solid #e0e0e0;
        }}
        .error-detail strong {{
            color: #333;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>📖❌</div>
        <h2>{title}</h2>
        <div class='description'>{description}</div>
        <div class='solutions'>
            <div class='solutions-title'>💡 解决方案</div>
            {solutionsHtml}
        </div>
        <div class='error-detail'>
            <strong>文件：</strong>{fileName}<br>
            <strong>位置：</strong>{filePath}
        </div>
    </div>
</body>
</html>";
        }

    }
}



