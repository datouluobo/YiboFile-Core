using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.Controls;

namespace YiboFile.Previews
{
    /// <summary>
    /// SVG预览 - 支持渲染和源码两种视图
    /// </summary>
    public class SvgPreview
    {
        public static UIElement CreatePreview(string filePath)
        {
            try
            {
                string svgContent = "";
                try { svgContent = File.ReadAllText(filePath); } catch { }

                // 创建主容器
                var grid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = Brushes.White,
                    Name = "SvgPreviewGrid"
                };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 内容区

                // 内容容器
                var contentGrid = new Grid();
                Grid.SetRow(contentGrid, 1);
                grid.Children.Add(contentGrid);

                // 状态
                int currentViewIndex = 0; // 0=Render, 1=Source
                bool isEditMode = false;

                // 控件
                TextPreviewToolbar _toolbar = null;
                WebView2 webView = null;
                TextBox sourceTextBox = null;
                Grid loadingPanel = null;

                _toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "🖼️",
                    ShowSearch = false,
                    ShowWordWrap = true,
                    ShowEncoding = false,
                    ShowViewToggle = true,
                    ShowFormat = true,
                    IsWordWrapEnabled = true
                };

                _toolbar.SetViewToggleText("📄 源码");

                // 加载指示器
                loadingPanel = PreviewHelper.CreateLoadingPanel("正在解析 SVG...");

                // WebView2 控件
                webView = new WebView2
                {
                    Visibility = Visibility.Collapsed, // 初始隐藏，等加载完显示
                    DefaultBackgroundColor = System.Drawing.Color.White,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                // Source TextBox
                sourceTextBox = new TextBox
                {
                    Text = svgContent,
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

                contentGrid.Children.Add(webView);
                contentGrid.Children.Add(sourceTextBox);
                contentGrid.Children.Add(loadingPanel);

                // 事件绑定
                _toolbar.ViewToggleRequested += (s, e) =>
                {
                    currentViewIndex = currentViewIndex == 0 ? 1 : 0;
                    if (currentViewIndex == 0) // Render
                    {
                        webView.Visibility = Visibility.Visible;
                        sourceTextBox.Visibility = Visibility.Collapsed;
                        _toolbar.SetViewToggleText("📄 源码");
                        // 重新加载渲染
                        _ = InitializeAndRender(webView, loadingPanel, filePath, contentGrid);
                    }
                    else // Source
                    {
                        webView.Visibility = Visibility.Collapsed;
                        sourceTextBox.Visibility = Visibility.Visible;
                        loadingPanel.Visibility = Visibility.Collapsed; // 源码不需要加载动画
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

                        // Switch to source to see result
                        if (currentViewIndex == 0)
                        {
                            // Trigger toggle logic manually
                            currentViewIndex = 1;
                            webView.Visibility = Visibility.Collapsed;
                            sourceTextBox.Visibility = Visibility.Visible;
                            loadingPanel.Visibility = Visibility.Collapsed;
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

                            if (currentViewIndex == 0)
                                _ = InitializeAndRender(webView, loadingPanel, filePath, contentGrid);

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

                        // Force source view
                        if (currentViewIndex == 0)
                        {
                            currentViewIndex = 1;
                            webView.Visibility = Visibility.Collapsed;
                            sourceTextBox.Visibility = Visibility.Visible;
                            loadingPanel.Visibility = Visibility.Collapsed;
                            _toolbar.SetViewToggleText("🎨 渲染");
                        }

                        sourceTextBox.IsReadOnly = false;
                        sourceTextBox.Background = new SolidColorBrush(Color.FromRgb(240, 248, 255));
                        _toolbar.SetEditMode(true);
                    }
                };

                _toolbar.CopyRequested += (s, e) =>
               {
                   if (currentViewIndex == 1)
                   {
                       if (!string.IsNullOrEmpty(sourceTextBox.SelectedText)) Clipboard.SetText(sourceTextBox.SelectedText);
                       else Clipboard.SetText(sourceTextBox.Text);
                   }
               };

                _toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                Grid.SetRow(_toolbar, 0);
                grid.Children.Add(_toolbar);

                // 初始渲染
                _ = InitializeAndRender(webView, loadingPanel, filePath, contentGrid);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"SVG预览初始化失败: {ex.Message}");
            }
        }

        private static async Task InitializeAndRender(WebView2 webView, Grid loadingPanel, string filePath, Grid parentGrid)
        {
            try
            {
                // 初始化 WebView2
                await webView.EnsureCoreWebView2Async();

                // 读取 SVG 内容 (每次重新读取以获取最新)
                string svgContent = await File.ReadAllTextAsync(filePath);

                // 包装在 HTML 中，支持缩放和平移
                // 使用与 CadPreview 类似的交互逻辑
                string htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ margin: 0; padding: 0; overflow: hidden; background-color: white; display: flex; justify-content: center; align-items: center; height: 100vh; }}
        #svg-container {{ width: 100%; height: 100%; display: flex; justify-content: center; align-items: center; }}
        svg {{ max-width: 100%; max-height: 100%; }}
    </style>
    <script>
        // 基础平移和缩放逻辑
        let scale = 1;
        let panning = false;
        let pointX = 0;
        let pointY = 0;
        let start = {{ x: 0, y: 0 }};
        
        function setTransform() {{
            const container = document.getElementById('svg-container');
            container.style.transform = `translate(${{pointX}}px, ${{pointY}}px) scale(${{scale}})`;
        }}

        document.onmousedown = function (e) {{
            e.preventDefault();
            start = {{ x: e.clientX - pointX, y: e.clientY - pointY }};
            panning = true;
        }}

        document.onmouseup = function (e) {{
            panning = false;
        }}

        document.onmousemove = function (e) {{
            e.preventDefault();
            if (!panning) return;
            pointX = (e.clientX - start.x);
            pointY = (e.clientY - start.y);
            setTransform();
        }}

        document.onwheel = function (e) {{
            e.preventDefault();
            var xs = (e.clientX - pointX) / scale,
                ys = (e.clientY - pointY) / scale,
                delta = (e.wheelDelta ? e.wheelDelta : -e.deltaY);
            (delta > 0) ? (scale *= 1.2) : (scale /= 1.2);
            pointX = e.clientX - xs * scale;
            pointY = e.clientY - ys * scale;
            setTransform();
        }}
    </script>
</head>
<body>
    <div id='svg-container'>
        {svgContent}
    </div>
</body>
</html>";

                webView.NavigateToString(htmlContent);

                // 导航完成后显示
                webView.NavigationCompleted += (s, e) =>
                {
                    loadingPanel.Visibility = Visibility.Collapsed;
                    webView.Visibility = Visibility.Visible;
                };
            }
            catch (Exception ex)
            {
                loadingPanel.Visibility = Visibility.Collapsed;
                webView.Visibility = Visibility.Collapsed;

                var errorPanel = PreviewHelper.CreateErrorPreview($"无法加载 SVG: {ex.Message}");
                // 防止重复添加
                if (parentGrid.Children.Contains(errorPanel as UIElement)) return;
                parentGrid.Children.Add(errorPanel as UIElement);
            }
        }
    }
}

