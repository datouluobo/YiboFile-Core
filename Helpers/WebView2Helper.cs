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
        private static readonly object _lock = new object();

        public static async Task EnsureInitializedAsync(WebView2 webView)
        {
            if (webView == null) return;

            if (_environment == null)
            {
                lock (_lock)
                {
                    if (_environment == null)
                    {
                        string userDataFolder = Path.Combine(ConfigManager.GetBaseDirectory(), "WebView2");
                        try
                        {
                            if (!Directory.Exists(userDataFolder))
                            {
                                Directory.CreateDirectory(userDataFolder);
                            }
                            _environment = CoreWebView2Environment.CreateAsync(null, userDataFolder).GetAwaiter().GetResult();
                        }
                        catch (Exception ex)
                        {
                            YiboFile.Services.Core.FileLogger.LogException("WebView2Environment creation failed", ex);
                        }
                    }
                }
            }

            try
            {
                await webView.EnsureCoreWebView2Async(_environment);
            }
            catch (Exception ex)
            {
                YiboFile.Services.Core.FileLogger.LogException("WebView2 initialization failed", ex);
            }
        }
    }
}
