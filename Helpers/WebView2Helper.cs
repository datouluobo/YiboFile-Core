using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.Services.Core;

namespace YiboFile.Helpers
{
    public static class WebView2Helper
    {
        private static CoreWebView2Environment _environment;
        private static Task<CoreWebView2Environment> _initTask;
        private static readonly object _initLock = new object();

        public static async Task EnsureInitializedAsync(WebView2 webView)
        {
            if (webView == null) return;

            if (_environment == null)
            {
                Task<CoreWebView2Environment> localTask;
                lock (_initLock)
                {
                    if (_initTask == null)
                    {
                        string userDataFolder = Path.Combine(ConfigManager.GetBaseDirectory(), "WebView2");
                        try
                        {
                            if (!Directory.Exists(userDataFolder)) Directory.CreateDirectory(userDataFolder);
                            _initTask = CoreWebView2Environment.CreateAsync(null, userDataFolder);
                        }
                        catch (Exception ex)
                        {
                            YiboFile.Services.Core.FileLogger.LogException("WebView2Environment task creation failed", ex);
                            return;
                        }
                    }
                    localTask = _initTask;
                }
                
                try
                {
                    _environment = await localTask;
                }
                catch (Exception ex)
                {
                    YiboFile.Services.Core.FileLogger.LogException("WebView2Environment await failed", ex);
                    return;
                }
            }

            try
            {
                await webView.EnsureCoreWebView2Async(_environment);
                SetThemedBackground(webView);
            }
            catch (Exception ex)
            {
                YiboFile.Services.Core.FileLogger.LogException("WebView2 initialization failed", ex);
            }
        }

        public static void SetThemedBackground(WebView2 webView)
        {
            if (webView == null) return;
            try
            {
                webView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            }
            catch { }
        }

        public static string GetThemeCss()
        {
            try
            {
                var bgBrush = System.Windows.Application.Current.FindResource("BackgroundPrimaryBrush") as System.Windows.Media.SolidColorBrush;
                var fgBrush = System.Windows.Application.Current.FindResource("ForegroundPrimaryBrush") as System.Windows.Media.SolidColorBrush;

                if (bgBrush != null && fgBrush != null)
                {
                    var bg = bgBrush.Color;
                    var fg = fgBrush.Color;

                    return $@"
        body {{
            background-color: transparent;
            color: rgb({fg.R}, {fg.G}, {fg.B});
        }}
        pre, code, .hdr, th, .slide, .container, .file-info, .steps {{
            background-color: rgba({fg.R}, {fg.G}, {fg.B}, 0.05) !important;
            border: 1px solid rgba({fg.R}, {fg.G}, {fg.B}, 0.1) !important;
            color: rgb({fg.R}, {fg.G}, {fg.B}) !important;
        }}
        .meta {{ color: rgba({fg.R}, {fg.G}, {fg.B}, 0.6) !important; }}
        blockquote {{
            border-left: 0.25em solid rgba({fg.R}, {fg.G}, {fg.B}, 0.2) !important;
            color: rgba({fg.R}, {fg.G}, {fg.B}, 0.6) !important;
        }}
        table th, table td {{
            border: 1px solid rgba({fg.R}, {fg.G}, {fg.B}, 0.2) !important;
            color: rgb({fg.R}, {fg.G}, {fg.B}) !important;
        }}
        h1, h2, h3, h4 {{
            border-bottom: 1px solid rgba({fg.R}, {fg.G}, {fg.B}, 0.2) !important;
            color: rgb({fg.R}, {fg.G}, {fg.B}) !important;
        }}";
                }
            }
            catch { }
            return "";
        }

        public static async Task InjectThemeScriptAsync(WebView2 webView)
        {
            if (webView?.CoreWebView2 == null) return;
            try
            {
                string themeCss = GetThemeCss().Replace("\r", "").Replace("\n", " ");
                string script = $@"
                    (function() {{
                        var style = document.getElementById('antigravity-theme-style');
                        if (!style) {{
                            style = document.createElement('style');
                            style.id = 'antigravity-theme-style';
                            document.head.appendChild(style);
                        }}
                        style.textContent = `{themeCss}`;
                        if (document.body && !document.body.style.backgroundColor) document.body.style.backgroundColor = 'transparent';
                    }})();
                ";
                await webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch { }
        }
    }
}
