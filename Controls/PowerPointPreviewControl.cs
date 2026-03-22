using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.ViewModels.Previews;

namespace YiboFile.Controls
{
    public class PowerPointPreviewControl : UserControl
    {
        private WebView2 _webView;
        private Grid _mainGrid;
        private StackPanel _legacyPanel;

        public PowerPointPreviewControl()
        {
            InitializeUI();
            this.DataContextChanged += OnDataContextChanged;
        }

        private void InitializeUI()
        {
            _mainGrid = new Grid();

            // WebView for PPTX HTML
            _webView = new WebView2();
            _mainGrid.Children.Add(_webView);

            // Legacy PPT UI
            _legacyPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _legacyPanel.SetResourceReference(StackPanel.BackgroundProperty, "BackgroundPrimaryBrush");

            var icon = new TextBlock { Text = "📊", FontSize = 48, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 20) };
            var title = new TextBlock { Text = "旧版 PowerPoint 格式", FontSize = 18, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
            title.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundPrimaryBrush");

            var desc = new TextBlock
            {
                Text = "该文件为旧版 PPT 格式，由于二进制限制无法直接预览。\n您可以尝试将其转换为 PPTX 格式。",
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            desc.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundSecondaryBrush");

            var convertButton = new Button
            {
                Padding = new Thickness(20, 10, 20, 10),
                FontSize = 14,
                BorderThickness = new Thickness(0)
            };
            convertButton.SetResourceReference(Button.BackgroundProperty, "AccentDefaultBrush");
            convertButton.SetResourceReference(Button.ForegroundProperty, "ForegroundOnAccentBrush");
            convertButton.SetBinding(Button.CommandProperty, new System.Windows.Data.Binding("ConvertCommand"));
            convertButton.SetBinding(Button.ContentProperty, new System.Windows.Data.Binding("ConvertStatusText"));
            convertButton.SetBinding(Button.IsEnabledProperty, new System.Windows.Data.Binding("IsConverting") { Converter = new InverseBooleanConverter() });

            _legacyPanel.Children.Add(icon);
            _legacyPanel.Children.Add(title);
            _legacyPanel.Children.Add(desc);
            _legacyPanel.Children.Add(convertButton);

            _mainGrid.Children.Add(_legacyPanel);
            this.Content = _mainGrid;

            this.Unloaded += (s, e) =>
            {
                if (_webView != null)
                {
                    _webView.Dispose();
                    _webView = null;
                }
            };
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is PowerPointPreviewViewModel vm)
            {
                vm.ReloadRequested += (s, args) => LoadHtml(vm.HtmlContent);
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(PowerPointPreviewViewModel.IsLegacyFormat))
                        UpdateView(vm);
                    if (args.PropertyName == nameof(PowerPointPreviewViewModel.HtmlContent))
                        LoadHtml(vm.HtmlContent);
                };

                if (!string.IsNullOrEmpty(vm.HtmlContent))
                    LoadHtml(vm.HtmlContent);

                UpdateView(vm);
            }
        }

        private void UpdateView(PowerPointPreviewViewModel vm)
        {
            if (vm.IsLegacyFormat)
            {
                _legacyPanel.Visibility = Visibility.Visible;
                _webView.Visibility = Visibility.Collapsed;
            }
            else
            {
                _legacyPanel.Visibility = Visibility.Collapsed;
                _webView.Visibility = Visibility.Visible;
            }
        }

        private async void LoadHtml(string html)
        {
            if (_webView != null && !string.IsNullOrEmpty(html))
            {
                try
                {
                    await YiboFile.Helpers.WebView2Helper.EnsureInitializedAsync(_webView);
                    _webView.CoreWebView2.DOMContentLoaded += async (s, ev) =>
                    {
                        await YiboFile.Helpers.WebView2Helper.InjectThemeScriptAsync(_webView);
                    };
                    _webView.NavigateToString(html);
                }
                catch { }
            }
        }

        private class InverseBooleanConverter : System.Windows.Data.IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                if (value is bool b) return !b;
                return false;
            }
            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
        }
    }
}
