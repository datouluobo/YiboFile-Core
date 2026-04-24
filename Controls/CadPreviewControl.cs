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
                BorderThickness = new Thickness(1)
            };
            loadVectorBtn.SetResourceReference(Button.BackgroundProperty, "BackgroundPrimaryBrush");
            loadVectorBtn.SetResourceReference(Button.ForegroundProperty, "ForegroundPrimaryBrush");
            loadVectorBtn.SetResourceReference(Button.BorderBrushProperty, "BorderBrush");

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
                Visibility = Visibility.Collapsed
            };
            _odaGrid.SetResourceReference(Grid.BackgroundProperty, "BackgroundPrimaryBrush");

            var odaView = new WebView2();
            _odaGrid.Children.Add(odaView);
            _mainGrid.Children.Add(_odaGrid);

            this.Content = _mainGrid;

            this.Unloaded += (s, ev) =>
            {
                if (_webView != null)
                {
                    _webView.Dispose();
                    _webView = null;
                }
                
                if (_odaGrid != null && _odaGrid.Children.Count > 0 && _odaGrid.Children[0] is WebView2 odaWebViewRef)
                {
                    odaWebViewRef.Dispose();
                }
            };
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
                    await YiboFile.Helpers.WebView2Helper.EnsureInitializedAsync(_webView);

                    if (_webView == null || _webView.CoreWebView2 == null) return;

                    _webView.CoreWebView2.DOMContentLoaded += async (s, ev) =>
                    {
                        await YiboFile.Helpers.WebView2Helper.InjectThemeScriptAsync(_webView);
                    };

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
            <a href='https://www.opendesign.com/guestfiles/oda_file_converter' class='btn btn-primary' target='_blank' style='padding: 10px 20px; text-decoration: none; display: inline-block; border-radius: 6px;'>🌐 前往下载</a>
            <button class='btn btn-secondary' onclick='window.chrome.webview.postMessage(""refresh"")' style='padding: 10px 20px; border-radius: 6px; cursor: pointer; border: none; margin-left:10px;'>🔄 刷新预览</button>
        </div>
    </div>
</body>
</html>";
            await YiboFile.Helpers.WebView2Helper.EnsureInitializedAsync(odaWebView);
            if (odaWebView != null && odaWebView.CoreWebView2 != null)
            {
                odaWebView.CoreWebView2.DOMContentLoaded += async (s, ev) =>
                {
                    await YiboFile.Helpers.WebView2Helper.InjectThemeScriptAsync(odaWebView);
                };
                odaWebView.NavigateToString(html);
            }
        }
    }
}
