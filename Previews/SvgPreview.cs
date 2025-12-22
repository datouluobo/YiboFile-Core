using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;

namespace OoiMRR.Previews
{
    /// <summary>
    /// SVG预览（使用WebBrowser直接渲染）
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
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255)), // 白色背景
                Name = "SvgPreviewGrid"
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 标题栏
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"SVG 矢量图: {Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            try
            {
                // 创建WebBrowser并加载SVG
                var webBrowser = new System.Windows.Controls.WebBrowser();

                // 读取SVG文件内容
                string svgContent = File.ReadAllText(filePath);

                // 包装在HTML中以确保正确渲染 (强制使用IE Edge模式支持SVG)
                string htmlContent = $@"<!DOCTYPE html>
<html>
<head>
    <meta http-equiv='X-UA-Compatible' content='IE=edge'>
    <style>
        body {{ margin: 0; padding: 0; display: flex; justify-content: center; align-items: center; height: 100vh; overflow: auto; background-color: white; }}
        svg {{ max-width: 100%; max-height: 100%; }}
    </style>
</head>
<body>
    {svgContent}
</body>
</html>";
                webBrowser.NavigateToString(htmlContent);

                Grid.SetRow(webBrowser, 1);
                grid.Children.Add(webBrowser);
            }
            catch (Exception ex)
            {
                var errorText = new TextBlock
                {
                    Text = $"无法加载 SVG: {ex.Message}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.Red,
                    FontSize = 14
                };
                Grid.SetRow(errorText, 1);
                grid.Children.Add(errorText);
            }

            return grid;
        }
    }
}
