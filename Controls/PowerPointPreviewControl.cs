using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.ViewModels.Previews;
using YiboFile.Services.Core;

namespace YiboFile.Controls
{
    public class PowerPointPreviewControl : UserControl
    {
        private WebView2 _webView;
        private Grid _mainGrid;
        private StackPanel _legacyPanel;
        private PowerPointPreviewViewModel _currentVm;

        public PowerPointPreviewControl()
        {
            InitializeUI();
            this.DataContextChanged += OnDataContextChanged;
        }

        private void InitializeUI()
        {
            this.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240));
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
                DetachVm();
                CleanupTempFile();
                if (_webView != null)
                {
                    try { _webView.Dispose(); } catch { }
                    _webView = null;
                }
            };
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            FileLogger.Log($"[PPT-CTRL-DEBUG] OnDataContextChanged, OldValue={e.OldValue?.GetType().Name ?? "null"}, NewValue={e.NewValue?.GetType().Name ?? "null"}");
            DetachVm();
            if (e.NewValue is PowerPointPreviewViewModel vm)
            {
                _currentVm = vm;
                _currentVm.ReloadRequested += OnReloadRequested;
                _currentVm.PropertyChanged += OnVmPropertyChanged;

                FileLogger.Log($"[PPT-CTRL-DEBUG] VM绑定成功, HtmlContent长度={_currentVm.HtmlContent?.Length ?? 0}, IsLegacy={_currentVm.IsLegacyFormat}, IsLoading={_currentVm.IsLoading}");

                if (!string.IsNullOrEmpty(_currentVm.HtmlContent))
                {
                    FileLogger.Log("[PPT-CTRL-DEBUG] 初始化时HtmlContent已有内容, 调用LoadHtml");
                    LoadHtml(_currentVm.HtmlContent);
                }

                UpdateView(_currentVm);
            }
        }

        private void DetachVm()
        {
            if (_currentVm != null)
            {
                _currentVm.ReloadRequested -= OnReloadRequested;
                _currentVm.PropertyChanged -= OnVmPropertyChanged;
                _currentVm = null;
            }
        }

        private void OnReloadRequested(object sender, EventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => {
                if (_currentVm != null) LoadHtml(_currentVm.HtmlContent);
            }));
        }

        private void OnVmPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PowerPointPreviewViewModel.IsLegacyFormat))
            {
                FileLogger.Log($"[PPT-CTRL-DEBUG] PropertyChanged: IsLegacyFormat={_currentVm?.IsLegacyFormat}");
                Dispatcher.BeginInvoke(new Action(() => {
                    if (_currentVm != null) UpdateView(_currentVm);
                }));
            }
            if (e.PropertyName == nameof(PowerPointPreviewViewModel.HtmlContent))
            {
                FileLogger.Log($"[PPT-CTRL-DEBUG] PropertyChanged: HtmlContent 更新, 长度={_currentVm?.HtmlContent?.Length ?? 0}");
                Dispatcher.BeginInvoke(new Action(() => {
                    if (_currentVm != null) LoadHtml(_currentVm.HtmlContent);
                }));
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

        private bool _isScriptInjected = false;
        private string _tempFilePath;

        // NavigateToString 的安全上限约为 ~1.5MB（WebView2 内部限制约2MB）
        private const int MaxNavigateToStringLength = 1_500_000;

        private async void LoadHtml(string html)
        {
            FileLogger.Log($"[PPT-CTRL-DEBUG] LoadHtml 调用, html长度={html?.Length ?? 0}, _webView is {(_webView == null ? "null" : "valid")}");
            if (_webView != null && !string.IsNullOrEmpty(html))
            {
                try
                {
                    FileLogger.Log("[PPT-CTRL-DEBUG] 开始 EnsureInitializedAsync...");
                    await YiboFile.Helpers.WebView2Helper.EnsureInitializedAsync(_webView);
                    FileLogger.Log($"[PPT-CTRL-DEBUG] EnsureInitializedAsync 完成, CoreWebView2 is {(_webView?.CoreWebView2 == null ? "null" : "ready")}");
                    if (_webView != null && _webView.CoreWebView2 != null)
                    {
                        if (!_isScriptInjected)
                        {
                            _webView.CoreWebView2.DOMContentLoaded += async (s, ev) =>
                            {
                                try { await YiboFile.Helpers.WebView2Helper.InjectThemeScriptAsync(_webView); } catch { }
                            };
                            _isScriptInjected = true;
                        }

                        if (html.Length <= MaxNavigateToStringLength)
                        {
                            // 小HTML直接用 NavigateToString
                            FileLogger.Log($"[PPT-CTRL-DEBUG] 调用 NavigateToString, html长度={html.Length}");
                            CleanupTempFile();
                            _webView.NavigateToString(html);
                        }
                        else
                        {
                            // 大HTML写入临时文件再用 Navigate(uri) 加载
                            FileLogger.Log($"[PPT-CTRL-DEBUG] HTML超出NavigateToString限制({html.Length}>{MaxNavigateToStringLength}), 使用临时文件加载");
                            CleanupTempFile();
                            _tempFilePath = System.IO.Path.Combine(
                                System.IO.Path.GetTempPath(),
                                $"yibofile_ppt_preview_{Guid.NewGuid():N}.html");
                            await System.Threading.Tasks.Task.Run(() =>
                                System.IO.File.WriteAllText(_tempFilePath, html, System.Text.Encoding.UTF8));
                            FileLogger.Log($"[PPT-CTRL-DEBUG] 临时文件已写入: {_tempFilePath}");
                            _webView.CoreWebView2.Navigate(new Uri(_tempFilePath).AbsoluteUri);
                        }
                        FileLogger.Log("[PPT-CTRL-DEBUG] 导航完成");
                    }
                    else
                    {
                        FileLogger.Log("[PPT-CTRL-DEBUG] WebView2未就绪（_webView或CoreWebView2为null）, 无法加载");
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.LogException("[PPT-CTRL-DEBUG] LoadHtml 异常", ex);
                }
            }
            else
            {
                FileLogger.Log($"[PPT-CTRL-DEBUG] LoadHtml 跳过: _webView is {(_webView == null ? "null" : "valid")}, html is {(string.IsNullOrEmpty(html) ? "empty/null" : "valid")}");
            }
        }

        private void CleanupTempFile()
        {
            if (!string.IsNullOrEmpty(_tempFilePath))
            {
                try { System.IO.File.Delete(_tempFilePath); } catch { }
                _tempFilePath = null;
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
