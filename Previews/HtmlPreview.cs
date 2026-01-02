using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Net;
using Microsoft.Web.WebView2.Wpf;
using OoiMRR.Controls;

namespace OoiMRR.Previews
{
    /// <summary>
    /// HTML 文件预览 - 支持源码和渲染两种视图
    /// </summary>
    public class HtmlPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                // 读取HTML内容
                string htmlContent = null;
                var encodings = new List<Encoding>
                {
                    Encoding.UTF8,
                    Encoding.Default
                };

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                try { encodings.Add(Encoding.GetEncoding("GB2312")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("GBK")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("GB18030")); } catch { }

                Exception lastException = null;
                foreach (var encoding in encodings)
                {
                    try
                    {
                        htmlContent = File.ReadAllText(filePath, encoding);
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                    }
                }

                if (string.IsNullOrEmpty(htmlContent))
                {
                    if (lastException != null)
                        return PreviewHelper.CreateErrorPreview($"无法读取HTML文件: {lastException.Message}");
                    return PreviewHelper.CreateErrorPreview("无法读取HTML文件内容");
                }

                // 创建主容器
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 内容容器
                var contentGrid = new Grid { Background = Brushes.White };
                Grid.SetRow(contentGrid, 1);
                grid.Children.Add(contentGrid);

                // 状态
                int currentViewIndex = 0; // 0=Render, 1=Source
                bool isEditMode = false;

                // 控件
                TextPreviewToolbar _toolbar = null;
                WebView2 webView = null;
                TextBox sourceTextBox = null;

                _toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "🌐",
                    ShowSearch = false,
                    ShowWordWrap = true,
                    ShowEncoding = false,
                    ShowViewToggle = true,
                    ShowFormat = false, // HTML一般不需要格式化，或者过于复杂
                    IsWordWrapEnabled = true
                };

                _toolbar.SetViewToggleText("📄 源码");

                // 渲染视图
                webView = new WebView2
                {
                    Visibility = Visibility.Visible,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };

                webView.Loaded += async (s, e) =>
                {
                    try
                    {
                        await webView.EnsureCoreWebView2Async();
                        // 注入脚本以优化视口
                        webView.CoreWebView2.DOMContentLoaded += async (sender, args) =>
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
                                await webView.CoreWebView2.ExecuteScriptAsync(script);
                            }
                            catch { }
                        };

                        webView.Source = new Uri(filePath);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            await webView.EnsureCoreWebView2Async();
                            webView.NavigateToString($"<html><body>渲染失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>");
                        }
                        catch { }
                    }
                };

                // 源码视图
                sourceTextBox = new TextBox
                {
                    Text = htmlContent,
                    TextWrapping = TextWrapping.Wrap,
                    IsReadOnly = true,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    BorderThickness = new Thickness(0),
                    Background = Brushes.White,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    AcceptsReturn = true,
                    AcceptsTab = true,
                    Visibility = Visibility.Collapsed
                };

                contentGrid.Children.Add(webView);
                contentGrid.Children.Add(sourceTextBox);

                // 事件绑定
                _toolbar.ViewToggleRequested += (s, e) =>
                {
                    currentViewIndex = currentViewIndex == 0 ? 1 : 0;
                    if (currentViewIndex == 0) // Render
                    {
                        webView.Visibility = Visibility.Visible;
                        sourceTextBox.Visibility = Visibility.Collapsed;
                        _toolbar.SetViewToggleText("📄 源码");
                        // 重新加载以显示最新更改
                        if (webView.CoreWebView2 != null) webView.Reload();
                    }
                    else // Source
                    {
                        webView.Visibility = Visibility.Collapsed;
                        sourceTextBox.Visibility = Visibility.Visible;
                        _toolbar.SetViewToggleText("🎨 渲染");
                    }
                };

                _toolbar.WordWrapChanged += (s, enabled) =>
                {
                    sourceTextBox.TextWrapping = enabled ? TextWrapping.Wrap : TextWrapping.NoWrap;
                };

                _toolbar.EditRequested += (s, e) =>
                {
                    if (isEditMode)
                    {
                        // Save
                        try
                        {
                            // 保持原有编码或使用UTF8
                            File.WriteAllText(filePath, sourceTextBox.Text);
                            isEditMode = false;

                            sourceTextBox.IsReadOnly = true;
                            sourceTextBox.Background = Brushes.White;
                            _toolbar.SetEditMode(false);

                            if (webView.CoreWebView2 != null) webView.Reload();

                            // MessageBox.Show("文件已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            Services.Core.NotificationService.ShowSuccess("文件已保存");
                        }
                        catch (Exception ex)
                        {
                            // MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            Services.Core.NotificationService.ShowError($"保存失败: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Edit
                        isEditMode = true;

                        // Force source view
                        if (currentViewIndex == 0)
                        {
                            currentViewIndex = 1;
                            webView.Visibility = Visibility.Collapsed;
                            sourceTextBox.Visibility = Visibility.Visible;
                            _toolbar.SetViewToggleText("🎨 渲染");
                        }

                        sourceTextBox.IsReadOnly = false;
                        sourceTextBox.Background = new SolidColorBrush(Color.FromRgb(240, 248, 255));
                        _toolbar.SetEditMode(true);
                    }
                };

                _toolbar.CopyRequested += (s, e) =>
                {
                    if (currentViewIndex == 1)
                    {
                        if (!string.IsNullOrEmpty(sourceTextBox.SelectedText)) Clipboard.SetText(sourceTextBox.SelectedText);
                        else Clipboard.SetText(sourceTextBox.Text);
                    }
                };

                _toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                Grid.SetRow(_toolbar, 0);
                grid.Children.Add(_toolbar);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"HTML预览失败: {ex.Message}");
            }
        }
    }
}

