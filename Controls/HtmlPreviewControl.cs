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
                BorderThickness = new Thickness(0)
            };

            // Bindings
            var visibilityConverter = new BooleanToVisibilityConverter();
            var inverseVisibilityConverter = new InverseBooleanToVisibilityConverter();

            var sourceViewBinding = new Binding("IsSourceView");
            _textBox.SetBinding(UIElement.VisibilityProperty, new Binding("IsSourceView") { Converter = visibilityConverter });
            _webView.SetBinding(UIElement.VisibilityProperty, new Binding("IsSourceView") { Converter = inverseVisibilityConverter });

            _textBox.SetBinding(TextBox.TextProperty, new Binding("SourceContent") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            // IsReadOnly binding (Inverse of IsEditMode)
            var editBinding = new Binding("IsEditMode");
            editBinding.Converter = new LocalInverseBooleanConverter();
            // Easier: just listen to property change or use a local converter instance if we can reference it.
            // Since we are in code, we can reference YiboFile.Converters.InverseBooleanConverter if public.
            // But let's check if we can easily instantiate it.
            // "YiboFile.Converters.InverseBooleanConverter" was seen in XAML.
            // Start by assuming we can bind it.

            // Actually, let's create a simple internal converter or use the resource if available.
            // But code-behind binding to resource is annoying. I will handle IsEditMode manually via DataContext property changed or just bind to existing converter class.

            // Let's rely on XAML resource or namespace? No, namespace.
            // YiboFile.Converters.InverseBooleanConverter

            try
            {
                // Assuming YiboFile.Converters namespace exists based on previous interaction context
                // The "InverseBooleanConverter" class was edited/viewed in previous turns.
                // It is in "YiboFile.Converters" namespace (deduced from file path).
                // Actually the file path was YiboFile-Core\Converters\InverseBooleanConverter.cs
                // So namespace is likely YiboFile.Converters.

                // However, I need to instantiate it. I can just create one here if I had access.
                // Better: Use a simple inline converter or just use the one created before.
            }
            catch { }

            // We'll skip dynamic binding of converter instance for now and rely on VM property if possible,
            // or just use logic in OnDataContextChanged to hook up property changes?
            // "InverseBooleanConverter" is simple. I can't easily rely on external types without full qualification and knowing they are public.
            // Let's implement a private converter class here to be safe and self-contained.

            _textBox.SetBinding(TextBox.IsReadOnlyProperty, new Binding("IsEditMode") { Converter = new LocalInverseBooleanConverter() });

            // WordWrap Binding
            _textBox.SetBinding(TextBox.TextWrappingProperty, new Binding("IsWordWrap") { Converter = new BooleanToTextWrappingConverter() });

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
                    await _webView.EnsureCoreWebView2Async();

                    // Inject viewport script
                    _webView.CoreWebView2.DOMContentLoaded += async (s, e) =>
                   {
                       try
                       {
                           string script = @"
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
                           await _webView.CoreWebView2.ExecuteScriptAsync(script);
                       }
                       catch { }
                   };

                    _webView.Source = new Uri(path);
                }
                catch { }
            }
        }

        // Local Converters
        private class LocalInverseBooleanConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is bool b) return !b;
                return false;
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
        }

        private class InverseBooleanToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is bool b && b) return Visibility.Collapsed;
                return Visibility.Visible;
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
        }

        private class BooleanToTextWrappingConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is bool b && b) return TextWrapping.Wrap;
                return TextWrapping.NoWrap;
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
        }
    }
}
