using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.ViewModels.Previews;

namespace YiboFile.Controls
{
    public class WordPreviewControl : UserControl
    {
        private WebView2 _webView;

        public WordPreviewControl()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            _webView = new WebView2();
            this.Content = _webView;
            this.DataContextChanged += (s, e) =>
            {
                if (e.NewValue is WordPreviewViewModel vm)
                {
                    vm.PropertyChanged += (ps, pe) =>
                    {
                        if (pe.PropertyName == nameof(vm.HtmlContent))
                            UpdateHtml(vm.HtmlContent);
                    };
                    UpdateHtml(vm.HtmlContent);
                }
            };
        }

        private async void UpdateHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return;
            try
            {
                await _webView.EnsureCoreWebView2Async();
                _webView.NavigateToString(html);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Word Preview WebView2 error: {ex.Message}");
            }
        }
    }
}
