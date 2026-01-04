using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.Web.WebView2.Wpf;
using OoiMRR.Controls;

namespace OoiMRR.Previews.DocumentHandlers
{
    /// <summary>
    /// CHM (Compiled HTML Help) 文档预览处理器
    /// 需要 7-Zip 或 hh.exe 来解压 CHM 文件
    /// </summary>
    public class ChmPreviewHandler : IDocumentPreviewHandler
    {
        public bool CanHandle(string extension)
        {
            return extension?.ToLower() == ".chm";
        }

        public UIElement CreatePreview(string filePath)
        {
            try
            {
                var mainGrid = new Grid { Background = Brushes.White };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

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

                // 实际内容区域
                var contentGrid = new Grid { Margin = new Thickness(0) };
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250, GridUnitType.Pixel) });
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Pixel) });
                contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // 目录树
                var treeView = new TreeView
                {
                    Background = Brushes.White,
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    BorderBrush = Brushes.LightGray,
                    Padding = new Thickness(5)
                };
                Grid.SetColumn(treeView, 0);
                contentGrid.Children.Add(treeView);

                // 分割线
                var splitter = new Border
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

                // 加载遮罩
                var loadingPanel = PreviewHelper.CreateLoadingPanel("正在解析 CHM 文件...");
                contentContainer.Children.Add(loadingPanel);

                // 异步加载
                _ = LoadChmInWebViewAsync(webView, filePath, treeView, loadingPanel);

                return mainGrid;
            }
            catch (Exception ex)
            {
                return CreateDocumentErrorPanel($"CHM 预览初始化失败: {ex.Message}");
            }
        }

        private async Task LoadChmInWebViewAsync(WebView2 webView, string filePath, TreeView treeView, FrameworkElement loadingPanel)
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

                    // 查找 7-Zip
                    var sevenZipPath = FindSevenZipPath();

                    // 使用缓存机制
                    var fileInfo = new FileInfo(filePath);
                    var fileHash = $"{filePath.GetHashCode():X8}_{fileInfo.LastWriteTime.Ticks:X16}";
                    var cacheDir = Path.Combine(Path.GetTempPath(), "MRR_CHM_Cache", fileHash);

                    string tempDir = cacheDir;
                    bool needExtract = true;

                    if (Directory.Exists(cacheDir))
                    {
                        var htmlFiles = Directory.GetFiles(cacheDir, "*.html", SearchOption.AllDirectories)
                            .Concat(Directory.GetFiles(cacheDir, "*.htm", SearchOption.AllDirectories));

                        if (htmlFiles.Any())
                        {
                            needExtract = false;
                        }
                        else
                        {
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
                        if (needExtract)
                        {
                            bool extracted = false;

                            // 尝试使用 7-Zip 解压
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
                                        RedirectStandardError = true
                                    };

                                    using (var process = Process.Start(processInfo))
                                    {
                                        if (process != null)
                                        {
                                            await process.WaitForExitAsync();

                                            if (Directory.Exists(tempDir) && Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories).Length > 0)
                                            {
                                                extracted = true;
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }

                            // 如果 7-Zip 失败，尝试 hh.exe
                            if (!extracted)
                            {
                                extracted = await TryExtractWithHhExe(filePath, tempDir);

                                if (!extracted)
                                {
                                    throw new Exception("无法解压 CHM 文件。请安装 7-Zip 或确保系统中有 hh.exe。");
                                }
                            }
                        }

                        // 查找主 HTML 文件
                        var mainHtmlFile = FindMainHtmlFile(tempDir);

                        if (string.IsNullOrEmpty(mainHtmlFile) || !File.Exists(mainHtmlFile))
                        {
                            throw new Exception("无法找到 CHM 的主 HTML 文件");
                        }

                        // 构建目录树
                        await BuildChmTreeView(treeView, tempDir, mainHtmlFile, webView);

                        // 加载主页面
                        await LoadHtmlInWebView(webView, mainHtmlFile);

                        // 隐藏加载遮罩
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (loadingPanel.Parent is Panel panel)
                            {
                                panel.Children.Remove(loadingPanel);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        await webView.EnsureCoreWebView2Async();
                        var errorHtml = GenerateChmErrorHtml(ex.Message, filePath);
                        webView.CoreWebView2.NavigateToString(errorHtml);

                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (loadingPanel.Parent is Panel panel)
                            {
                                panel.Children.Remove(loadingPanel);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await webView.Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await webView.EnsureCoreWebView2Async();
                        var errorHtml = GenerateChmErrorHtml(ex.Message, filePath);
                        webView.CoreWebView2.NavigateToString(errorHtml);
                    }
                    catch { }
                });
            }
        }

        private string FindSevenZipPath()
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7-Zip", "7zG.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7zG.exe"),
                @"C:\Program Files\7-Zip\7zG.exe",
                @"C:\Program Files (x86)\7-Zip\7zG.exe",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7-Zip", "7z.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe"),
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe"
            };

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        private string FindMainHtmlFile(string directory)
        {
            // 优先级列表
            var priorityFiles = new[] { "index.html", "index.htm", "default.html", "default.htm", "main.html", "main.htm" };

            foreach (var fileName in priorityFiles)
            {
                var filePath = Path.Combine(directory, fileName);
                if (File.Exists(filePath))
                    return filePath;
            }

            // 递归查找
            var allHtmlFiles = Directory.GetFiles(directory, "*.html", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(directory, "*.htm", SearchOption.AllDirectories))
                .OrderBy(f => f.Length)
                .ToList();

            return allHtmlFiles.FirstOrDefault();
        }

        private async Task BuildChmTreeView(TreeView treeView, string chmDir, string mainHtmlFile, WebView2 webView)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // 尝试查找 .hhc 目录文件
                    var hhcFile = FindHhcFile(chmDir);

                    if (!string.IsNullOrEmpty(hhcFile) && File.Exists(hhcFile))
                    {
                        // 从 .hhc 构建目录树
                        BuildTreeFromHhc(treeView, hhcFile, chmDir, webView);
                    }
                    else
                    {
                        // 简单树结构
                        BuildSimpleTreeFromHtmlFiles(treeView, chmDir, webView);
                    }
                }
                catch
                {
                    BuildSimpleTreeFromHtmlFiles(treeView, chmDir, webView);
                }
            });
        }

        private string FindHhcFile(string directory)
        {
            var hhcFiles = Directory.GetFiles(directory, "*.hhc", SearchOption.AllDirectories);
            return hhcFiles.FirstOrDefault();
        }

        private void BuildTreeFromHhc(TreeView treeView, string hhcFile, string chmDir, WebView2 webView)
        {
            try
            {
                var content = File.ReadAllText(hhcFile, Encoding.Default);

                // 简化：创建一个根节点
                var rootItem = new TreeViewItem
                {
                    Header = "目录",
                    IsExpanded = true
                };
                treeView.Items.Add(rootItem);

                // 从 HTML 文件列表构建简单树
                var htmlFiles = Directory.GetFiles(chmDir, "*.htm*", SearchOption.AllDirectories).Take(50);
                foreach (var file in htmlFiles)
                {
                    var item = new TreeViewItem
                    {
                        Header = Path.GetFileNameWithoutExtension(file),
                        Tag = file
                    };
                    item.Selected += (s, e) =>
                    {
                        if (item.Tag is string path)
                        {
                            _ = LoadHtmlInWebView(webView, path);
                        }
                    };
                    rootItem.Items.Add(item);
                }
            }
            catch
            {
                BuildSimpleTreeFromHtmlFiles(treeView, chmDir, webView);
            }
        }

        private void BuildSimpleTreeFromHtmlFiles(TreeView treeView, string chmDir, WebView2 webView)
        {
            var rootItem = new TreeViewItem
            {
                Header = "文件列表",
                IsExpanded = true
            };
            treeView.Items.Add(rootItem);

            var htmlFiles = Directory.GetFiles(chmDir, "*.htm*", SearchOption.AllDirectories).Take(100);
            foreach (var file in htmlFiles)
            {
                var item = new TreeViewItem
                {
                    Header = Path.GetFileNameWithoutExtension(file),
                    Tag = file
                };
                item.Selected += (s, e) =>
                {
                    if (item.Tag is string path)
                    {
                        _ = LoadHtmlInWebView(webView, path);
                    }
                };
                rootItem.Items.Add(item);
            }
        }

        private async Task LoadHtmlInWebView(WebView2 webView, string htmlFilePath)
        {
            try
            {
                await webView.EnsureCoreWebView2Async();
                var uri = new Uri(htmlFilePath).AbsoluteUri;
                webView.CoreWebView2.Navigate(uri);
            }
            catch { }
        }

        private async Task<bool> TryExtractWithHhExe(string chmPath, string outputDir)
        {
            try
            {
                var hhPath = FindHhExePath();
                if (string.IsNullOrEmpty(hhPath))
                    return false;

                var arguments = $"-decompile \"{outputDir}\" \"{chmPath}\"";
                var processInfo = new ProcessStartInfo
                {
                    FileName = hhPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        await process.WaitForExitAsync();
                        return Directory.Exists(outputDir) && Directory.GetFiles(outputDir, "*", SearchOption.AllDirectories).Length > 0;
                    }
                }
            }
            catch { }

            return false;
        }

        private string FindHhExePath()
        {
            var possiblePaths = new[]
            {
                @"C:\Windows\hh.exe",
                @"C:\Windows\System32\hh.exe",
                @"C:\Windows\SysWOW64\hh.exe"
            };

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        private string GenerateChmErrorHtml(string errorMessage, string filePath)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: 'Segoe UI', Arial, sans-serif;
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
        .error-icon {{ font-size: 64px; margin-bottom: 20px; }}
        h2 {{ color: #d32f2f; margin: 20px 0; }}
        .error {{ color: #d32f2f; margin: 20px 0; font-size: 14px; line-height: 1.6; }}
        .info {{ color: #666; margin: 10px 0; font-size: 13px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='error-icon'>📖❌</div>
        <h2>CHM 加载失败</h2>
        <div class='error'>{System.Security.SecurityElement.Escape(errorMessage)}</div>
        <div class='info'>请检查：</div>
        <div class='info'>1. 是否安装了 7-Zip</div>
        <div class='info'>2. CHM 文件是否完整</div>
        <div class='info'>3. 文件路径是否包含特殊字符</div>
    </div>
</body>
</html>";
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
