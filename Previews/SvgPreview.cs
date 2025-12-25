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
    /// SVG预览（使用WebView2渲染）
    /// </summary>
    public class SvgPreview
    {
        public static UIElement CreatePreview(string filePath)
        {
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

            // 标题栏
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"SVG 矢量图: {Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            // 内容容器
            var contentGrid = new Grid();
            Grid.SetRow(contentGrid, 1);
            grid.Children.Add(contentGrid);

            // 加载指示器
            var loadingPanel = PreviewHelper.CreateLoadingPanel("正在解析 SVG...");
            contentGrid.Children.Add(loadingPanel);

            // WebView2 控件
            var webView = new WebView2
            {
                Visibility = Visibility.Collapsed,
                DefaultBackgroundColor = System.Drawing.Color.White
            };
            contentGrid.Children.Add(webView);

            // 异步初始化并加载
            _ = InitializeAndRender(webView, loadingPanel, filePath, contentGrid);

            return grid;
        }

        private static async Task InitializeAndRender(WebView2 webView, Grid loadingPanel, string filePath, Grid parentGrid)
        {
            try
            {
                // 初始化 WebView2
                await webView.EnsureCoreWebView2Async();

                // 读取 SVG 内容
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
