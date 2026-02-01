using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.ViewModels.Previews;

namespace YiboFile.Controls
{
    public class ExcelPreviewControl : UserControl
    {
        private Grid _mainGrid;
        private Grid _xlsxGrid;
        private StackPanel _legacyPanel;

        private WebView2 _webView;
        private StackPanel _tabPanel;

        public ExcelPreviewControl()
        {
            InitializeUI();
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ExcelPreviewViewModel vm)
            {
                UpdateView(vm);
                vm.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(ExcelPreviewViewModel.IsLegacyFormat)) UpdateView(vm);
                    if (args.PropertyName == nameof(ExcelPreviewViewModel.Sheets)) RebuildTabs(vm);
                    if (args.PropertyName == nameof(ExcelPreviewViewModel.GeneratedHtml)) LoadHtml(vm.GeneratedHtml);
                };
            }
        }

        private void UpdateView(ExcelPreviewViewModel vm)
        {
            if (vm.IsLegacyFormat)
            {
                _xlsxGrid.Visibility = Visibility.Collapsed;
                _legacyPanel.Visibility = Visibility.Visible;
            }
            else
            {
                _xlsxGrid.Visibility = Visibility.Visible;
                _legacyPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void InitializeUI()
        {
            _mainGrid = new Grid();

            // 1. XLSX View
            _xlsxGrid = new Grid { Visibility = Visibility.Collapsed };
            _xlsxGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Tabs
            _xlsxGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // WebView

            // Tabs
            var tabBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 5, 10, 5)
            };
            var tabScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.HorizontalOnly
            };

            _tabPanel = new StackPanel { Orientation = Orientation.Horizontal };
            tabScroll.Content = _tabPanel;
            tabBorder.Child = tabScroll;

            Grid.SetRow(tabBorder, 0);
            _xlsxGrid.Children.Add(tabBorder);

            // WebView
            _webView = new WebView2();
            Grid.SetRow(_webView, 1);
            _xlsxGrid.Children.Add(_webView);

            // 2. Legacy View
            _legacyPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = Brushes.White,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var legacyIcon = new TextBlock { Text = "📊", FontSize = 48, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 20) };
            var legacyTitle = new TextBlock { Text = "需要转换格式", FontSize = 18, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
            var legacyDesc = new TextBlock
            {
                Text = "该文件为旧版 Excel 格式 (XLS)，无法直接预览。\n请将其转换为 XLSX 格式以查看。",
                FontSize = 14,
                Foreground = Brushes.Gray,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var convertButton = new Button
            {
                Padding = new Thickness(20, 10, 20, 10),
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                Foreground = Brushes.White,
                FontSize = 14,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            convertButton.SetBinding(Button.CommandProperty, new Binding("ConvertCommand"));
            convertButton.SetBinding(Button.ContentProperty, new Binding("ConvertStatusText"));
            convertButton.SetBinding(Button.IsEnabledProperty, new Binding("IsConverting") { Converter = new InverseBooleanConverter() });

            _legacyPanel.Children.Add(legacyIcon);
            _legacyPanel.Children.Add(legacyTitle);
            _legacyPanel.Children.Add(legacyDesc);
            _legacyPanel.Children.Add(convertButton);

            _mainGrid.Children.Add(_xlsxGrid);
            _mainGrid.Children.Add(_legacyPanel);

            this.Content = _mainGrid;
        }

        private void RebuildTabs(ExcelPreviewViewModel vm)
        {
            _tabPanel.Children.Clear();
            if (vm.Sheets == null) return;

            foreach (var sheet in vm.Sheets)
            {
                var btn = new Button
                {
                    Content = sheet.Name,
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(0, 0, 5, 0),
                    FontSize = 13,
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(0),
                    Background = sheet == vm.SelectedSheet ? new SolidColorBrush(Color.FromRgb(33, 150, 243)) : Brushes.Transparent,
                    Foreground = sheet == vm.SelectedSheet ? Brushes.White : Brushes.Black
                };

                btn.Click += (s, e) =>
                {
                    vm.SelectedSheet = sheet;
                    foreach (Button b in _tabPanel.Children)
                    {
                        b.Background = Brushes.Transparent;
                        b.Foreground = Brushes.Black;
                    }
                    btn.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    btn.Foreground = Brushes.White;
                };

                _tabPanel.Children.Add(btn);
            }
        }

        private async void LoadHtml(string html)
        {
            if (_webView != null && !string.IsNullOrEmpty(html))
            {
                try
                {
                    await _webView.EnsureCoreWebView2Async();
                    _webView.NavigateToString(html);
                }
                catch { }
            }
        }

        // Simple internal converter
        private class InverseBooleanConverter : IValueConverter
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
