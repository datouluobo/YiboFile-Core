using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.ViewModels.Previews;

namespace YiboFile.Controls
{
    public class HtmlPreviewControl : UserControl
    {
        private WebView2 _webView;
        private TextBox _textBox;

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register("FilePath", typeof(string), typeof(HtmlPreviewControl), new PropertyMetadata(null, OnFilePathChanged));

        public string FilePath
        {
            get { return (string)GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HtmlPreviewControl control && e.NewValue is string path)
            {
                control.LoadUrl(path);
            }
        }

        public HtmlPreviewControl()
        {
            InitializeUI();
            this.Unloaded += (s, e) =>
            {
                if (_webView != null)
                {
                    _webView.Dispose();
                    _webView = null;
                }
            };
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is HtmlPreviewViewModel vm)
            {
                vm.ReloadRequested += (s, args) =>
                {
                    if (_webView != null && _webView.CoreWebView2 != null)
                        _webView.Reload();
                };
            }
        }

        private void InitializeUI()
        {
            var grid = new Grid();

            _webView = new WebView2
            {
                Visibility = Visibility.Visible
            };

            _textBox = new TextBox
            {
                Visibility = Visibility.Collapsed,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                AcceptsReturn = true,
                AcceptsTab = true,
                Padding = new Thickness(5),
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent
            };
            _textBox.SetResourceReference(TextBox.ForegroundProperty, "ForegroundPrimaryBrush");

            // Bindings
            var visibilityConverter = new BooleanToVisibilityConverter();

            _textBox.SetBinding(UIElement.VisibilityProperty, new Binding("IsSourceView") { Converter = visibilityConverter });
            _webView.SetBinding(UIElement.VisibilityProperty, new Binding("IsSourceView") { 
                Converter = new InlineInverseVisibilityConverter() 
            });

            _textBox.SetBinding(TextBox.TextProperty, new Binding("SourceContent") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            // IsReadOnly binding (Inverse of IsEditMode)
            _textBox.SetBinding(TextBox.IsReadOnlyProperty, new Binding("IsEditMode") { Converter = new YiboFile.Converters.InverseBooleanConverter() });

            // WordWrap Binding
            _textBox.SetBinding(TextBox.TextWrappingProperty, new Binding("IsWordWrap") { Converter = new YiboFile.Converters.BooleanToTextWrappingConverter() });

            grid.Children.Add(_webView);
            grid.Children.Add(_textBox);

            this.Content = grid;
        }

        private async void LoadUrl(string path)
        {
            if (_webView != null && !string.IsNullOrEmpty(path))
            {
                try
                {
                    await YiboFile.Helpers.WebView2Helper.EnsureInitializedAsync(_webView);

                    if (_webView == null || _webView.CoreWebView2 == null) return;

                    // Inject theme and viewport script
                    _webView.CoreWebView2.DOMContentLoaded += async (s, ev) =>
                    {
                        await YiboFile.Helpers.WebView2Helper.InjectThemeScriptAsync(_webView);
                        try
                        {
                            string viewportScript = @"
                                 (function() {
                                     var viewport = document.querySelector('meta[name=""viewport""]');
                                     if (!viewport) {
                                         viewport = document.createElement('meta');
                                         viewport.name = 'viewport';
                                         viewport.content = 'width=device-width, initial-scale=1.0';
                                         document.head.appendChild(viewport);
                                     }
                                 })();
                             ";
                            await _webView.CoreWebView2.ExecuteScriptAsync(viewportScript);
                        }
                        catch { }
                    };

                    _webView.Source = new Uri(path);
                }
                catch { }
            }
        }

        // Local Converters
        private class InlineInverseVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is bool b && b) return Visibility.Collapsed;
                return Visibility.Visible;
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
        }
    }
}
