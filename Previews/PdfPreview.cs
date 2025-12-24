using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace OoiMRR.Previews
{
    /// <summary>
    /// PDF文件预览提供者
    /// </summary>
    public class PdfPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return CreateErrorPreview($"PDF文件不存在: {filePath}");
                }

                // 主容器：Grid布局 - 标题栏 + 内容区域
                var mainGrid = new Grid
                {
                    Background = Brushes.White
                };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // PDF内容

                // 标题栏
                var buttons = new List<Button>
                {
                    PreviewHelper.CreateOpenButton(filePath)
                };
                var title = PreviewHelper.CreateTitlePanel("📄",
                    $"PDF文档: {Path.GetFileName(filePath)}",
                    buttons);
                Grid.SetRow(title, 0);
                mainGrid.Children.Add(title);

                // PDF显示区域 - WebView2
                var webViewContainer = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    MinHeight = 400
                };

                var webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                // 加载状态指示器
                var loadingPanel = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = Brushes.White,
                    Visibility = Visibility.Visible
                };
                loadingPanel.Children.Add(new TextBlock
                {
                    Text = "⏳",
                    FontSize = 48,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 10)
                });
                loadingPanel.Children.Add(new TextBlock
                {
                    Text = "正在加载PDF...",
                    FontSize = 14,
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                webViewContainer.Children.Add(loadingPanel);
                webViewContainer.Children.Add(webView);

                Grid.SetRow(webViewContainer, 1);
                mainGrid.Children.Add(webViewContainer);

                // 异步加载PDF
                _ = LoadPdfAsync(webView, filePath, loadingPanel);

                return mainGrid;
            }
            catch (Exception ex)
            {
                return CreateErrorPreview($"PDF预览初始化失败: {ex.Message}", filePath);
            }
        }


        private async Task LoadPdfAsync(WebView2 webView, string filePath, UIElement loadingPanel)
        {
            try
            {
                // 初始化WebView2
                await webView.EnsureCoreWebView2Async(null);

                if (webView.CoreWebView2 != null)
                {
                    // 配置WebView2设置
                    webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                    webView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                    webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                    // 隐藏加载状态
                    await webView.Dispatcher.InvokeAsync(() =>
                    {
                        if (loadingPanel != null)
                            loadingPanel.Visibility = Visibility.Collapsed;
                    });

                    // 确保使用绝对路径
                    if (!Path.IsPathRooted(filePath))
                    {
                        filePath = Path.GetFullPath(filePath);
                    }

                    // 转换为file:// URI格式
                    // 处理路径中的空格和特殊字符
                    var uri = new Uri(filePath).AbsoluteUri;
                    // 导航到PDF文件
                    webView.CoreWebView2.Navigate(uri);

                    // 监听导航完成事件
                    webView.CoreWebView2.NavigationCompleted += (sender, e) =>
                    {
                        if (!e.IsSuccess)
                        {
                            ShowErrorInWebView(webView, $"PDF加载失败: {e.WebErrorStatus}");
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                // 隐藏加载状态
                await webView.Dispatcher.InvokeAsync(() =>
                {
                    if (loadingPanel != null)
                        loadingPanel.Visibility = Visibility.Collapsed;
                });

                // 显示错误页面
                ShowErrorInWebView(webView, ex.Message, filePath);
            }
        }

        private void ShowErrorInWebView(WebView2 webView, string errorMessage, string filePath = null)
        {
            try
            {
                var escapedMessage = System.Security.SecurityElement.Escape(errorMessage);
                var errorHtml = string.Format(@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>PDF加载错误</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: 'Segoe UI', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }}
        .container {{
            background: white;
            border-radius: 16px;
            padding: 40px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
            max-width: 600px;
            width: 100%;
            text-align: center;
        }}
        .error-icon {{
            font-size: 72px;
            margin-bottom: 20px;
            animation: bounce 2s infinite;
        }}
        @keyframes bounce {{
            0%, 100% {{ transform: translateY(0); }}
            50% {{ transform: translateY(-10px); }}
        }}
        h1 {{
            color: #d32f2f;
            margin: 20px 0;
            font-size: 28px;
            font-weight: 600;
        }}
        .error-message {{
            color: #d32f2f;
            margin: 20px 0;
            font-size: 14px;
            line-height: 1.8;
            background: #ffebee;
            padding: 15px;
            border-radius: 8px;
            border-left: 4px solid #d32f2f;
            text-align: left;
            word-break: break-word;
        }}
        .info-list {{
            text-align: left;
            margin: 20px 0;
            padding: 0 20px;
        }}
        .info-list li {{
            color: #666;
            margin: 10px 0;
            font-size: 14px;
            line-height: 1.6;
        }}
        .info-list li::before {{
            content: '✓ ';
            color: #4caf50;
            font-weight: bold;
            margin-right: 8px;
        }}
        .suggestion {{
            color: #2196F3;
            margin-top: 25px;
            padding: 15px;
            background: #e3f2fd;
            border-radius: 8px;
            font-size: 14px;
            line-height: 1.6;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='error-icon'>📄❌</div>
        <h1>PDF加载失败</h1>
        <div class='error-message'>{0}</div>
        <ul class='info-list'>
            <li>检查PDF文件是否完整且未损坏</li>
            <li>确认文件路径不包含特殊字符</li>
            <li>验证文件权限是否允许读取</li>
            <li>尝试使用外部程序打开PDF文件</li>
        </ul>
        <div class='suggestion'>
            💡 <strong>建议：</strong>您可以使用工具栏上的外部打开按钮使用系统默认PDF阅读器打开此文件。
        </div>
    </div>
</body>
</html>", escapedMessage);

                webView.CoreWebView2?.NavigateToString(errorHtml);
            }
            catch
            {
            }
        }

        private UIElement CreateErrorPreview(string message, string filePath = null)
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
            }

            var title = PreviewHelper.CreateTitlePanel("❌", "PDF预览错误", buttons);
            panel.Children.Add(title);

            var errorBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(211, 47, 47)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(10)
            };

            var errorText = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(211, 47, 47)),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap
            };

            errorBorder.Child = errorText;
            panel.Children.Add(errorBorder);

            return panel;
        }
    }
}
