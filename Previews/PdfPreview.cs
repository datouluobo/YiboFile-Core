using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using System.Collections.Generic;
using YiboFile.Controls;

namespace YiboFile.Previews
{
    /// <summary>
    /// PDF文件预览提供者 - 使用PDF.js实现完整的阅读器功能
    /// </summary>
    public class PdfPreview : IPreviewProvider
    {
        private const string PDF_VIEWER_HTML = "Resources/PdfViewer.html";

        public UIElement CreatePreview(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    return CreateErrorPreview($"PDF文件不存在: {filePath}");
                }

                // 主容器：Grid布局 - 标题栏 + PDF查看器
                var mainGrid = new Grid
                {
                    Background = Brushes.White
                };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // PDF内容

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
                mainGrid.Children.Add(toolbar);

                // PDF显示区域 - WebView2
                var webViewContainer = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromRgb(82, 86, 89))
                };

                var webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                webViewContainer.Children.Add(webView);
                Grid.SetRow(webViewContainer, 1);
                mainGrid.Children.Add(webViewContainer);

                // 异步加载PDF查看器
                _ = InitializePdfViewerAsync(webView, filePath, toolbar);

                return mainGrid;
            }
            catch (Exception ex)
            {
                return CreateErrorPreview($"PDF预览初始化失败: {ex.Message}", filePath);
            }
        }

        /// <summary>
        /// 初始化PDF查看器
        /// </summary>
        private async Task InitializePdfViewerAsync(WebView2 webView, string pdfFilePath, UIElement titlePanel)
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
                    webView.CoreWebView2.Settings.IsWebMessageEnabled = true;

                    // 获取HTML查看器路径
                    string htmlViewerPath = GetPdfViewerHtmlPath();

                    if (!File.Exists(htmlViewerPath))
                    {
                        throw new FileNotFoundException($"PDF查看器HTML文件不存在: {htmlViewerPath}");
                    }

                    // 设置虚拟主机映射 - 解决file://协议跨文件访问限制
                    // 将Resources目录映射到虚拟域名,以便HTML能访问PDF.js库和PDF文件
                    string resourcesDir = Path.GetDirectoryName(htmlViewerPath);
                    webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "pdfviewer.local",
                        resourcesDir,
                        Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

                    // 同样映射PDF文件所在目录
                    string pdfDir = Path.GetDirectoryName(pdfFilePath);
                    webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "pdffiles.local",
                        pdfDir,
                        Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

                    // 使用虚拟主机URL导航到HTML查看器
                    string htmlFileName = Path.GetFileName(htmlViewerPath);
                    webView.CoreWebView2.Navigate($"https://pdfviewer.local/{htmlFileName}");

                    // 等待页面加载完成后注入PDF路径
                    webView.CoreWebView2.NavigationCompleted += async (sender, e) =>
                    {
                        if (e.IsSuccess)
                        {
                            try
                            {
                                // 使用虚拟主机URL加载PDF
                                string pdfFileName = Path.GetFileName(pdfFilePath);
                                string pdfUrl = $"https://pdffiles.local/{pdfFileName}";

                                // 调用JavaScript函数加载PDF
                                await webView.CoreWebView2.ExecuteScriptAsync(
                                    $"window.loadPdfFromPath('{pdfUrl.Replace("'", "\\'")}');");
                            }
                            catch (Exception ex)
                            {
                                await ShowErrorInWebView(webView, $"加载PDF失败: {ex.Message}");
                            }
                        }
                        else
                        {
                            await ShowErrorInWebView(webView, $"HTML查看器加载失败: {e.WebErrorStatus}");
                        }
                    };

                    // 监听来自JavaScript的消息(可选,用于调试或状态同步)
                    webView.CoreWebView2.WebMessageReceived += (sender, e) =>
                    {
                        try
                        {
                            string message = e.TryGetWebMessageAsString();
                        }
                        catch { }
                    };
                }
            }
            catch (Exception ex)
            {
                await ShowErrorInWebView(webView, $"初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取PDF查看器HTML文件的完整路径
        /// </summary>
        private string GetPdfViewerHtmlPath()
        {
            // 首先尝试相对于应用程序目录的路径
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string htmlPath = Path.Combine(appDir, PDF_VIEWER_HTML);

            if (File.Exists(htmlPath))
            {
                return htmlPath;
            }

            // 如果不存在,尝试相对于当前工作目录
            htmlPath = Path.Combine(Directory.GetCurrentDirectory(), PDF_VIEWER_HTML);

            if (File.Exists(htmlPath))
            {
                return htmlPath;
            }

            // 如果还是不存在,尝试向上查找项目根目录
            string currentDir = appDir;
            for (int i = 0; i < 5; i++) // 最多向上查找5级
            {
                htmlPath = Path.Combine(currentDir, PDF_VIEWER_HTML);
                if (File.Exists(htmlPath))
                {
                    return htmlPath;
                }

                var parentDir = Directory.GetParent(currentDir);
                if (parentDir == null) break;
                currentDir = parentDir.FullName;
            }

            // 如果都找不到,返回默认路径(会在后续检查中报错)
            return Path.Combine(appDir, PDF_VIEWER_HTML);
        }

        /// <summary>
        /// 在WebView中显示错误信息
        /// </summary>
        private async Task ShowErrorInWebView(WebView2 webView, string errorMessage)
        {
            try
            {
                var escapedMessage = System.Security.SecurityElement.Escape(errorMessage);
                var errorScript = $@"
                    document.getElementById('loading').style.display = 'none';
                    document.getElementById('error-message').textContent = '{escapedMessage.Replace("'", "\\'")}';
                    document.getElementById('error').style.display = 'block';
                ";

                await webView.CoreWebView2.ExecuteScriptAsync(errorScript);
            }
            catch (Exception)
            { }
        }

        /// <summary>
        /// 创建错误预览UI
        /// </summary>
        private UIElement CreateErrorPreview(string message, string filePath = null)
        {
            var panel = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(10)
            };

            // 统一工具栏
            var toolbar = new TextPreviewToolbar
            {
                FileName = string.IsNullOrEmpty(filePath) ? "错误" : Path.GetFileName(filePath),
                FileIcon = "❌",
                ShowSearch = false,
                ShowWordWrap = false,
                ShowEncoding = false,
                ShowViewToggle = false,
                ShowFormat = false
            };

            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);
            }

            panel.Children.Add(toolbar);

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

            // 添加建议信息
            var suggestionBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(227, 242, 253)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(10)
            };

            var suggestionPanel = new StackPanel();
            suggestionPanel.Children.Add(new TextBlock
            {
                Text = "💡 可能的解决方案:",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Margin = new Thickness(0, 0, 0, 10)
            });

            var suggestions = new[]
            {
                "确认PDF文件完整且未损坏",
                "检查文件路径是否包含特殊字符",
                "验证文件权限是否允许读取",
                "确保Resources/PdfViewer.html文件存在",
                "尝试使用外部PDF阅读器打开"
            };

            foreach (var suggestion in suggestions)
            {
                var bulletPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin = new Thickness(0, 3, 0, 3)
                };

                bulletPanel.Children.Add(new TextBlock
                {
                    Text = "✓ ",
                    Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 5, 0)
                });

                bulletPanel.Children.Add(new TextBlock
                {
                    Text = suggestion,
                    Foreground = Brushes.Gray,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                });

                suggestionPanel.Children.Add(bulletPanel);
            }

            suggestionBorder.Child = suggestionPanel;
            panel.Children.Add(suggestionBorder);

            return panel;
        }
    }
}



