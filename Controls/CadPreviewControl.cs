using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.ViewModels.Previews;

namespace YiboFile.Controls
{
    public class CadPreviewControl : UserControl
    {
        private WebView2 _webView;
        private Grid _mainGrid;
        private Grid _odaGrid;
        private Image _thumbnailImage;
        private Grid _thumbnailOverlay;

        public CadPreviewControl()
        {
            InitializeUI();
            this.DataContextChanged += OnDataContextChanged;
        }

        private void InitializeUI()
        {
            _mainGrid = new Grid();

            // WebView for SVG
            _webView = new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.White,
                Visibility = Visibility.Collapsed
            };
            _mainGrid.Children.Add(_webView);

            // Thumbnail Image
            _thumbnailImage = new Image
            {
                Stretch = System.Windows.Media.Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _mainGrid.Children.Add(_thumbnailImage);

            // Thumbnail Overlay (Button to load vector)
            _thumbnailOverlay = new Grid
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 40),
                Visibility = Visibility.Collapsed
            };
            var loadVectorBtn = new Button
            {
                Content = "加载高清矢量图 (可能需要转换)",
                Padding = new Thickness(15, 8, 15, 8),
                FontSize = 14,
                Background = System.Windows.Media.Brushes.White,
                BorderBrush = System.Windows.Media.Brushes.LightGray,
                BorderThickness = new Thickness(1)
            };
            // Bind button command later or finding ancestor? Better in code behind setup
            loadVectorBtn.Click += (s, e) =>
            {
                if (DataContext is CadPreviewViewModel vm)
                    vm.LoadVectorCommand.Execute(null);
            };

            _thumbnailOverlay.Children.Add(loadVectorBtn);
            _mainGrid.Children.Add(_thumbnailOverlay);

            // ODA Download UI
            _odaGrid = new Grid
            {
                Visibility = Visibility.Collapsed,
                Background = System.Windows.Media.Brushes.White
            };

            var odaView = new WebView2();
            _odaGrid.Children.Add(odaView);
            _mainGrid.Children.Add(_odaGrid);

            this.Content = _mainGrid;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is CadPreviewViewModel vm)
            {
                vm.ReloadRequested += (s, args) =>
                {
                    if (!vm.IsShowingThumbnail)
                        LoadHtml(vm.HtmlContent);
                };

                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(CadPreviewViewModel.NeedsOda))
                    {
                        UpdateVisibility(vm);
                    }
                    else if (args.PropertyName == nameof(CadPreviewViewModel.IsShowingThumbnail))
                    {
                        UpdateVisibility(vm);
                    }
                    else if (args.PropertyName == nameof(CadPreviewViewModel.ImageSource))
                    {
                        _thumbnailImage.Source = vm.ImageSource;
                    }
                };

                _thumbnailImage.Source = vm.ImageSource;
                if (!string.IsNullOrEmpty(vm.HtmlContent) && !vm.IsShowingThumbnail)
                    LoadHtml(vm.HtmlContent);

                UpdateVisibility(vm);
            }
        }

        private void UpdateVisibility(CadPreviewViewModel vm)
        {
            if (vm.IsShowingThumbnail)
            {
                _thumbnailImage.Visibility = Visibility.Visible;
                _thumbnailOverlay.Visibility = Visibility.Visible;
                _webView.Visibility = Visibility.Collapsed;
                _odaGrid.Visibility = Visibility.Collapsed;
            }
            else if (vm.NeedsOda)
            {
                _thumbnailImage.Visibility = Visibility.Collapsed;
                _thumbnailOverlay.Visibility = Visibility.Collapsed;
                _webView.Visibility = Visibility.Collapsed;
                _odaGrid.Visibility = Visibility.Visible;
                LoadOdaHtml(vm);
            }
            else
            {
                _thumbnailImage.Visibility = Visibility.Collapsed;
                _thumbnailOverlay.Visibility = Visibility.Collapsed;
                _webView.Visibility = Visibility.Visible;
                _odaGrid.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadHtml(string html)
        {
            if (_webView != null && !string.IsNullOrEmpty(html))
            {
                try
                {
                    await _webView.EnsureCoreWebView2Async();

                    // WebView2 NavigateToString has a size limit (around 2MB).
                    // If content is large, save to temp file and navigate.
                    if (html.Length > 1024 * 1024)
                    {
                        string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "YiboFile_CadPreview_" + Guid.NewGuid() + ".html");
                        System.IO.File.WriteAllText(tempPath, html);
                        _webView.CoreWebView2.Navigate(tempPath);
                    }
                    else
                    {
                        _webView.NavigateToString(html);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"CAD Preview LoadHtml failed: {ex.Message}");
                }
            }
        }

        private async void LoadOdaHtml(CadPreviewViewModel vm)
        {
            var odaWebView = _odaGrid.Children[0] as WebView2;
            if (odaWebView == null) return;

            var fileName = System.IO.Path.GetFileName(vm.FilePath);
            var fi = new System.IO.FileInfo(vm.FilePath);
            var fileSize = Previews.PreviewHelper.FormatFileSize(fi.Exists ? fi.Length : 0);

            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: 'Segoe UI', sans-serif; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); display: flex; justify-content: center; align-items: center; min-height: 100vh; margin: 0; padding: 20px; box-sizing: border-box; }}
        .container {{ background: white; border-radius: 12px; padding: 30px; box-shadow: 0 10px 40px rgba(0,0,0,0.2); max-width: 550px; width: 100%; }}
        h2 {{ color: #333; margin-top: 0; }}
        .file-info {{ background: #f8f9fa; border-radius: 8px; padding: 12px; margin: 15px 0; font-size: 13px; }}
        .message {{ background: #fff3cd; border-left: 3px solid #ffc107; padding: 12px; margin: 15px 0; border-radius: 6px; font-size: 13px; color: #856404; }}
        .steps {{ background: #e3f2fd; border-radius: 8px; padding: 15px; margin: 15px 0; font-size: 13px; }}
        .btn {{ display: inline-block; padding: 10px 20px; border-radius: 6px; font-weight: 600; text-decoration: none; cursor: pointer; border: none; }}
        .btn-primary {{ background: #667eea; color: white; }}
        .btn-secondary {{ background: #f5576c; color: white; margin-left: 10px; }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>📐 需要 DWG 转换工具</h2>
        <div class='file-info'><strong>文件名:</strong> {System.Net.WebUtility.HtmlEncode(fileName)}<br><strong>大小:</strong> {fileSize}</div>
        <div class='message'>需要使用 <strong>ODA File Converter</strong> 转换 DWG 后才能预览。</div>
        <div class='steps'>
            <strong>安装步骤:</strong><br>
            1. 访问 ODA 官网下载 ZIP<br>
            2. 解压到: Dependencies\ODAFileConverter\<br>
            3. 刷新此页面即可预览
        </div>
        <div style='text-align: right; margin-top: 20px;'>
            <a href='https://www.opendesign.com/guestfiles/oda_file_converter' class='btn btn-primary' target='_blank'>🌐 前往下载</a>
            <button class='btn btn-secondary' onclick='window.chrome.webview.postMessage(""refresh"")'>🔄 刷新预览</button>
        </div>
    </div>
    <script>
        // Handle refresh message if needed, though simpler via VM RefreshCommand
    </script>
</body>
</html>";
            await odaWebView.EnsureCoreWebView2Async();
            odaWebView.NavigateToString(html);
        }
    }
}
