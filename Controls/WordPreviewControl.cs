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
        private WordPreviewViewModel _currentVm;

        public WordPreviewControl()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            this.Background = System.Windows.Media.Brushes.White; // Default background for Word
            _webView = new WebView2();
            this.Content = _webView;
            
            this.Unloaded += (s, e) =>
            {
                DetachVm();
                if (_webView != null)
                {
                    try { _webView.Dispose(); } catch { }
                    _webView = null;
                }
            };

            this.DataContextChanged += (s, e) =>
            {
                DetachVm();
                if (e.NewValue is WordPreviewViewModel vm)
                {
                    _currentVm = vm;
                    _currentVm.PropertyChanged += OnVmPropertyChanged;
                    UpdateHtml(_currentVm.HtmlContent);
                }
            };
        }

        private void DetachVm()
        {
            if (_currentVm != null)
            {
                _currentVm.PropertyChanged -= OnVmPropertyChanged;
                _currentVm = null;
            }
        }

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WordPreviewViewModel.HtmlContent))
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    if (_currentVm != null) UpdateHtml(_currentVm.HtmlContent);
                }));
            }
        }

        private async void UpdateHtml(string html)
        {
            if (string.IsNullOrEmpty(html) || _webView == null) return;
            try
            {
                await YiboFile.Helpers.WebView2Helper.EnsureInitializedAsync(_webView);
                if (_webView != null && _webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.DOMContentLoaded += async (s, ev) =>
                    {
                        await YiboFile.Helpers.WebView2Helper.InjectThemeScriptAsync(_webView);
                    };
                    _webView.NavigateToString(html);
                }
            }
            catch (Exception ex)
            {
            }
        }
    }
}
