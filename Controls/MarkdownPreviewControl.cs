using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.ViewModels.Previews;

namespace YiboFile.Controls
{
    public class MarkdownPreviewControl : UserControl
    {
        private WebView2 _webView;
        private TextBox _textBox;

        public MarkdownPreviewControl()
        {
            InitializeUI();
        }

        private void InitializeUI()
        {
            var grid = new Grid();

            _webView = new WebView2();
            _textBox = new TextBox
            {
                AcceptsReturn = true,
                IsReadOnly = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10)
            };

            grid.Children.Add(_webView);
            grid.Children.Add(_textBox);

            this.Content = grid;

            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is MarkdownPreviewViewModel vm)
            {
                // Unbind old
                BindingOperations.ClearAllBindings(_textBox);

                // Bind SourceContent
                var contentBinding = new Binding("SourceContent") { Source = vm };
                _textBox.SetBinding(TextBox.TextProperty, contentBinding);

                _textBox.TextWrapping = vm.IsWordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(vm.IsWordWrap))
                        _textBox.TextWrapping = vm.IsWordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;

                    if (args.PropertyName == nameof(vm.IsSourceView))
                        UpdateVisibility(vm.IsSourceView);

                    if (args.PropertyName == nameof(vm.HtmlContent))
                        UpdateHtml(vm.HtmlContent);
                };

                UpdateVisibility(vm.IsSourceView);
                UpdateHtml(vm.HtmlContent);
            }
        }

        private void UpdateVisibility(bool isSourceView)
        {
            _textBox.Visibility = isSourceView ? Visibility.Visible : Visibility.Collapsed;
            _webView.Visibility = isSourceView ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void UpdateHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return;
            try
            {
                await _webView.EnsureCoreWebView2Async();
                _webView.NavigateToString(html);
            }
            catch { }
        }
    }
}
