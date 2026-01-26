using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.Controls;

namespace YiboFile.Previews.DocumentHandlers
{
    /// <summary>
    /// PDF 文档预览处理器
    /// </summary>
    public class PdfPreviewHandler : IDocumentPreviewHandler
    {
        public bool CanHandle(string extension)
        {
            return extension?.ToLower() == ".pdf";
        }

        public UIElement CreatePreview(string filePath)
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
                return CreateDocumentErrorPanel($"PDF 预览初始化失败: {ex.Message}");
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

        private UIElement CreateDocumentErrorPanel(string message)
        {
            var panel = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(20)
            };

            var errorText = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Red,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            };
            panel.Children.Add(errorText);

            return panel;
        }
    }
}

