using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OoiMRR.Previews
{
    /// <summary>
    /// CAD 文件预览（DWG、DXF）
    /// </summary>
    public class CadPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            
            if (extension == ".dwg" || extension == ".dxf")
            {
                return CreateCadPreview(filePath);
            }
            
            return PreviewHelper.CreateErrorPreview("不支持的 CAD 文件格式");
        }

        private UIElement CreateCadPreview(string filePath)
        {
            try
            {
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var ext = Path.GetExtension(filePath).ToLower();
                var buttons = new List<Button>();
                
                // 如果是 DWG 文件，添加"转换为DXF格式"按钮
                if (ext == ".dwg")
                {
                    var convertButton = new Button
                    {
                        Content = "🔄 转换为DXF格式",
                        Padding = new Thickness(12, 6, 12, 6),
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        FontSize = 13,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    convertButton.Click += (s, e) => ConvertDwgToDxf(filePath, convertButton);
                    buttons.Add(convertButton);
                }
                
                buttons.Add(PreviewHelper.CreateOpenButton(filePath));
                
                var titlePanel = PreviewHelper.CreateTitlePanel("📐", $"CAD 文件: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);
                
                // Main content container
                var main = new Grid { Margin = new Thickness(0) };
                
                // Loading indicator
                var loadingPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Visibility = Visibility.Visible
                };
                var loadingText = new TextBlock
                {
                    Text = "正在准备预览...",
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                var progressBar = new ProgressBar
                {
                    Width = 200,
                    Height = 20,
                    IsIndeterminate = true
                };
                loadingPanel.Children.Add(progressBar);
                loadingPanel.Children.Add(loadingText);
                main.Children.Add(loadingPanel);

                // WebView2 for SVG rendering
                var webView = new WebView2 
                { 
                    Visibility = Visibility.Collapsed,
                    DefaultBackgroundColor = System.Drawing.Color.White
                };
                main.Children.Add(webView);

                Grid.SetRow(main, 1);
                grid.Children.Add(main);

                // Initialize and Render
                InitializeAndRender(webView, loadingPanel, loadingText, filePath, ext);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"CAD 预览初始化失败: {ex.Message}");
            }
        }

        private async void InitializeAndRender(WebView2 webView, StackPanel loadingPanel, TextBlock loadingText, string filePath, string ext)
        {
            try
            {
                // Ensure WebView2 is initialized
                await webView.EnsureCoreWebView2Async();

                string dxfFilePath = filePath;

                // 1. Convert DWG to DXF if needed
                if (ext == ".dwg")
                {
                    // 检查 ODA File Converter 是否已安装
                    if (!OoiMRR.Services.OdaDownloader.IsInstalled())
                    {
                        // 显示下载界面
                        ShowOdaDownloadUI(webView, loadingPanel, filePath);
                        return;
                    }
                    
                    loadingText.Text = "正在转换 DWG 到 DXF...";
                    
                    var convertTask = Task.Run(async () => 
                    {
                        return await OoiMRR.Services.DwgConverter.ConvertToDxfAsync(filePath);
                    });

                    if (await Task.WhenAny(convertTask, Task.Delay(TimeSpan.FromSeconds(30))) == convertTask)
                    {
                        dxfFilePath = convertTask.Result;
                    }
                    else
                    {
                        throw new TimeoutException("DWG转换超时，请检查 ODA File Converter 是否安装正确");
                    }
                }

                // 2. Convert DXF to SVG
                loadingText.Text = "正在解析图纸...";
                
                var svgContent = await Task.Run(() => 
                {
                    return OoiMRR.Rendering.DxfSvgConverter.ConvertToSvg(dxfFilePath);
                });

                // 3. Wrap SVG in HTML for better viewing experience (zoom/pan)
                var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ margin: 0; padding: 0; overflow: hidden; background-color: white; display: flex; justify-content: center; align-items: center; height: 100vh; }}
        #svg-container {{ width: 100%; height: 100%; display: flex; justify-content: center; align-items: center; }}
        svg {{ max-width: 100%; max-height: 100%; }}
    </style>
    <script>
        // Basic Pan and Zoom logic
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
                
                loadingPanel.Visibility = Visibility.Collapsed;
                webView.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                loadingPanel.Visibility = Visibility.Collapsed;
                webView.Visibility = Visibility.Visible;
                webView.NavigateToString($"<html><body style='font-family:Segoe UI;color:#c00;padding:20px'><h3>预览失败</h3><p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p></body></html>");
            }
        }

        /// <summary>
        /// 显示 ODA File Converter 下载界面
        /// </summary>
        private void ShowOdaDownloadUI(WebView2 webView, StackPanel loadingPanel, string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var fileSize = PreviewHelper.FormatFileSize(new FileInfo(filePath).Length);
            
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: 'Segoe UI', 'Microsoft YaHei', Arial, sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            display: flex;
            justify-content: center;
            align-items: center;
            min-height: 100vh;
            padding: 20px;
        }}
        .container {{
            background: white;
            border-radius: 12px;
            padding: 30px;
            box-shadow: 0 10px 40px rgba(0,0,0,0.2);
            max-width: 550px;
            width: 100%;
        }}
        .header {{
            text-align: center;
            margin-bottom: 20px;
        }}
        .icon {{
            font-size: 48px;
            margin-bottom: 10px;
        }}
        h2 {{
            color: #333;
            font-size: 22px;
            margin-bottom: 8px;
        }}
        .subtitle {{
            color: #666;
            font-size: 13px;
        }}
        .file-info {{
            background: #f8f9fa;
            border-radius: 8px;
            padding: 12px 16px;
            margin: 15px 0;
            font-size: 13px;
        }}
        .file-info-item {{
            display: flex;
            justify-content: space-between;
            padding: 4px 0;
        }}
        .file-info-label {{
            font-weight: 600;
            color: #555;
        }}
        .file-info-value {{
            color: #666;
        }}
        .message {{
            background: #fff3cd;
            border-left: 3px solid #ffc107;
            padding: 12px 15px;
            margin: 15px 0;
            border-radius: 6px;
            font-size: 13px;
            line-height: 1.6;
            color: #856404;
        }}
        .steps {{
            background: #e3f2fd;
            border-radius: 8px;
            padding: 15px;
            margin: 15px 0;
        }}
        .steps-title {{
            font-weight: 600;
            color: #1976d2;
            margin-bottom: 10px;
            font-size: 14px;
        }}
        .step {{
            margin: 6px 0;
            padding-left: 22px;
            position: relative;
            color: #555;
            font-size: 13px;
            line-height: 1.5;
        }}
        .step:before {{
            content: '✓';
            position: absolute;
            left: 0;
            color: #4caf50;
            font-weight: bold;
        }}
        .buttons {{
            display: flex;
            gap: 10px;
            margin-top: 20px;
        }}
        .btn {{
            flex: 1;
            padding: 12px 20px;
            border: none;
            border-radius: 6px;
            font-size: 14px;
            font-weight: 600;
            cursor: pointer;
            transition: all 0.2s ease;
            text-decoration: none;
            color: white;
            text-align: center;
            display: inline-block;
        }}
        .btn-primary {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }}
        .btn-primary:hover {{
            transform: translateY(-1px);
            box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
        }}
        .btn-secondary {{
            background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%);
        }}
        .btn-secondary:hover {{
            transform: translateY(-1px);
            box-shadow: 0 4px 12px rgba(245, 87, 108, 0.4);
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='icon'>📐</div>
            <h2>需要 DWG 转换工具</h2>
            <div class='subtitle'>DWG 文件需要转换后才能预览</div>
        </div>
        
        <div class='file-info'>
            <div class='file-info-item'>
                <span class='file-info-label'>文件名</span>
                <span class='file-info-value'>{System.Net.WebUtility.HtmlEncode(fileName)}</span>
            </div>
            <div class='file-info-item'>
                <span class='file-info-label'>大小</span>
                <span class='file-info-value'>{fileSize}</span>
            </div>
        </div>
        
        <div class='message'>
            需要使用 <strong>ODA File Converter</strong> (免费工具，约100MB) 将 DWG 转换为可预览的格式。
        </div>
        
        <div class='steps'>
            <div class='steps-title'>📋 安装步骤</div>
            <div class='step'>访问 ODA 官网下载工具</div>
            <div class='step'>下载 Windows 版本的 ZIP 文件</div>
            <div class='step'>解压到: Dependencies\ODAFileConverter\</div>
            <div class='step'>刷新此页面即可预览</div>
        </div>
        
        <div class='buttons'>
            <a href='https://www.opendesign.com/guestfiles/oda_file_converter' 
               class='btn btn-primary' target='_blank'>
                🌐 前往下载
            </a>
            <button class='btn btn-secondary' onclick='window.location.reload()'>
                🔄 刷新预览
            </button>
        </div>
    </div>
</body>
</html>";

            loadingPanel.Visibility = Visibility.Collapsed;
            webView.Visibility = Visibility.Visible;
            webView.NavigateToString(html);
        }

        /// <summary>
        /// 将 DWG 转换为 DXF 并保存到当前目录
        /// </summary>
        private async void ConvertDwgToDxf(string dwgFilePath, Button convertButton)
        {
            try
            {
                if (!File.Exists(dwgFilePath))
                {
                    MessageBox.Show("源文件不存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 禁用按钮并显示转换中状态
                convertButton.IsEnabled = false;
                convertButton.Content = "⏳ 转换中...";

                try
                {
                    // 获取缓存的 DXF 文件路径
                    var cachedDxfPath = OoiMRR.Services.DwgConverter.GetConvertedDxfPath(dwgFilePath);
                    
                    // 如果缓存不存在，先转换
                    if (!File.Exists(cachedDxfPath))
                    {
                        try
                        {
                            cachedDxfPath = await OoiMRR.Services.DwgConverter.ConvertToDxfAsync(dwgFilePath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            convertButton.IsEnabled = true;
                            convertButton.Content = "🔄 转换为DXF格式";
                            return;
                        }
                    }

                    // 确定目标文件路径（参考 doc 转 docx 的逻辑）
                    var sourceDir = Path.GetDirectoryName(dwgFilePath);
                    var baseFileName = Path.GetFileNameWithoutExtension(dwgFilePath);
                    var targetFileName = baseFileName + ".dxf";
                    var targetPath = Path.Combine(sourceDir, targetFileName);

                    // 如果目标文件已存在，添加序号
                    int counter = 1;
                    while (File.Exists(targetPath))
                    {
                        targetFileName = $"{baseFileName}({counter}).dxf";
                        targetPath = Path.Combine(sourceDir, targetFileName);
                        counter++;
                    }

                    // 复制文件到目标位置
                    File.Copy(cachedDxfPath, targetPath, false);

                    // 更新按钮状态为成功
                    convertButton.Content = "✅ 转换成功";

                    var result = MessageBox.Show(
                        $"转换成功！\n\n文件已保存到:\n{targetPath}\n\n是否在文件资源管理器中显示？",
                        "成功",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        // 在资源管理器中选中文件
                        Process.Start("explorer.exe", $"/select,\"{targetPath}\"");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    convertButton.IsEnabled = true;
                    convertButton.Content = "🔄 转换为DXF格式";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                convertButton.IsEnabled = true;
                convertButton.Content = "🔄 转换为DXF格式";
            }
        }
        
    }
}

















