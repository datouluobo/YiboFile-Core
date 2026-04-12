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
        private ExcelPreviewViewModel _currentVm;

        public ExcelPreviewControl()
        {
            InitializeUI();
            this.DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            DetachVm();
            if (e.NewValue is ExcelPreviewViewModel vm)
            {
                _currentVm = vm;
                _currentVm.PropertyChanged += OnVmPropertyChanged;
                UpdateView(_currentVm);
                if (!string.IsNullOrEmpty(_currentVm.GeneratedHtml))
                    LoadHtml(_currentVm.GeneratedHtml);
            }
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
            if (e.PropertyName == nameof(ExcelPreviewViewModel.IsLegacyFormat))
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    if (_currentVm != null) UpdateView(_currentVm);
                }));
            }
            if (e.PropertyName == nameof(ExcelPreviewViewModel.GeneratedHtml))
            {
                Dispatcher.BeginInvoke(new Action(() => {
                    if (_currentVm != null) LoadHtml(_currentVm.GeneratedHtml);
                }));
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
            // WebView
            _webView = new WebView2();
            _xlsxGrid.Children.Add(_webView);

            // 2. Legacy View
            _legacyPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _legacyPanel.SetResourceReference(StackPanel.BackgroundProperty, "BackgroundPrimaryBrush");

            var legacyIcon = new TextBlock { Text = "📊", FontSize = 48, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 20) };
            var legacyTitle = new TextBlock { Text = "需要转换格式", FontSize = 18, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
            var legacyDesc = new TextBlock
            {
                Text = "该文件为旧版 Excel 格式 (XLS)，无法直接预览。\n请将其转换为 XLSX 格式以查看。",
                FontSize = 14,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };
            legacyDesc.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundSecondaryBrush");
            legacyTitle.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundPrimaryBrush");
            legacyIcon.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundPrimaryBrush");

            var convertButton = new Button
            {
                Padding = new Thickness(20, 10, 20, 10),
                FontSize = 14,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            convertButton.SetResourceReference(Button.BackgroundProperty, "AccentDefaultBrush");
            convertButton.SetResourceReference(Button.ForegroundProperty, "ForegroundOnAccentBrush");
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

            this.Unloaded += (s, e) =>
            {
                DetachVm();
                if (_webView != null)
                {
                    try { _webView.Dispose(); } catch { }
                    _webView = null;
                }
            };
        }

        private async void LoadHtml(string html)
        {
            if (_webView != null && !string.IsNullOrEmpty(html))
            {
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
