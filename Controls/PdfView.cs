using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace YiboFile.Controls
{
    public partial class PdfView : UserControl
    {
        private WebView2 _webView;
        private string _pendingFilePath;
        private const string PDF_VIEWER_HTML = "Resources/PdfViewer.html";

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register("FilePath", typeof(string), typeof(PdfView), new PropertyMetadata(null, OnFilePathChanged));

        public string FilePath
        {
            get { return (string)GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }

        public PdfView()
        {
            _webView = new WebView2();
            var grid = new Grid();
            grid.Children.Add(_webView);
            this.Content = grid;

            InitializeWebView();
        }

        private async void InitializeWebView()
        {
            try
            {
                await _webView.EnsureCoreWebView2Async(null);

                if (_webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                    _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

                    // Setup virtual host mapping
                    string htmlViewerPath = GetPdfViewerHtmlPath();
                    if (File.Exists(htmlViewerPath))
                    {
                        string resourcesDir = Path.GetDirectoryName(htmlViewerPath);
                        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                            "pdfviewer.local",
                            resourcesDir,
                            CoreWebView2HostResourceAccessKind.Allow);

                        _webView.CoreWebView2.Navigate($"http://pdfviewer.local/{Path.GetFileName(htmlViewerPath)}");

                        _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
            }
        }

        private async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && !string.IsNullOrEmpty(_pendingFilePath))
            {
                // Wait a bit for JS to be ready
                await Task.Delay(200);
                await LoadPdf(_pendingFilePath);
            }
        }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (PdfView)d;
            var path = (string)e.NewValue;

            if (string.IsNullOrEmpty(path)) return;

            if (control._webView != null && control._webView.CoreWebView2 != null)
            {
                _ = control.LoadPdf(path);
            }
            else
            {
                control._pendingFilePath = path;
            }
        }

        private async Task LoadPdf(string path)
        {
            if (!File.Exists(path)) return;

            try
            {
                // Asynchronously read file to bytes, handling potential locks
                byte[] bytes = await Task.Run(() =>
                {
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        byte[] buffer = new byte[fs.Length];
                        fs.Read(buffer, 0, buffer.Length);
                        return buffer;
                    }
                });

                string base64 = Convert.ToBase64String(bytes);

                // Use Base64 to load PDF, bypassing CORS and local file access restrictions
                await _webView.CoreWebView2.ExecuteScriptAsync(
                    $@"
                    (function() {{
                        const tryLoad = () => {{
                            if (window.loadPdfFromBase64) {{
                                window.loadPdfFromBase64('{base64}');
                            }} else {{
                                setTimeout(tryLoad, 100);
                            }}
                        }};
                        tryLoad();
                    }})();");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load PDF failed: {ex.Message}");
            }
        }

        private string GetPdfViewerHtmlPath()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDir, PDF_VIEWER_HTML);
        }
    }
}
