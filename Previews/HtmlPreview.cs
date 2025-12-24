using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace OoiMRR.Previews
{
    /// <summary>
    /// HTML 文件预览 - 支持源码和渲染两种视图（Tab标签页）
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

                // 注册编码提供程序，以支持中文字符编码
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                // 尝试添加中文字符编码，如果系统支持
                try { encodings.Add(Encoding.GetEncoding("GB2312")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("GBK")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("GB18030")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("UTF-16LE")); } catch { }
                try { encodings.Add(Encoding.GetEncoding("UTF-16BE")); } catch { }

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
                    {
                        return PreviewHelper.CreateErrorPreview($"无法读取HTML文件: {lastException.Message}");
                    }
                    return PreviewHelper.CreateErrorPreview("无法读取HTML文件内容");
                }

                // 创建主容器
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 内容区域 - 使用Grid替代TabControl，隐藏标签页
                var contentGrid = new Grid
                {
                    Background = Brushes.White
                };
                Grid.SetRow(contentGrid, 1);
                grid.Children.Add(contentGrid);

                // 用于跟踪当前视图（0=渲染，1=源码）
                int currentViewIndex = 0;

                // 先声明所有变量，以便在lambda中使用
                Button toggleButton = null;
                TextBox sourceTextBoxRef = null;
                WebView2 webViewRef = null;
                Button editButton = null;
                bool isEditMode = false;
                string originalHtmlContent = htmlContent;

                // 标题栏 - 添加渲染/源码切换按钮和编辑按钮
                var buttons = new List<Button>();
                toggleButton = PreviewHelper.CreateHtmlViewToggleButton(
                    () =>
                    {
                        // 切换视图：如果当前是渲染(0)，切换到源码(1)；如果当前是源码(1)，切换到渲染(0)
                        currentViewIndex = currentViewIndex == 0 ? 1 : 0;

                        // 显示/隐藏对应的视图
                        if (currentViewIndex == 0)
                        {
                            // 显示渲染视图
                            if (webViewRef != null)
                            {
                                webViewRef.Visibility = Visibility.Visible;
                            }
                            if (sourceTextBoxRef != null)
                            {
                                sourceTextBoxRef.Visibility = Visibility.Collapsed;
                            }
                            if (toggleButton != null)
                            {
                                toggleButton.Content = "📄 源码";
                            }
                        }
                        else
                        {
                            // 显示源码视图
                            if (webViewRef != null)
                            {
                                webViewRef.Visibility = Visibility.Collapsed;
                            }
                            if (sourceTextBoxRef != null)
                            {
                                sourceTextBoxRef.Visibility = Visibility.Visible;
                            }
                            if (toggleButton != null)
                            {
                                toggleButton.Content = "🎨 渲染";
                            }
                        }
                    },
                    "📄 源码",  // 当前显示渲染，按钮显示"源码"
                    "🎨 渲染"   // 切换到源码后，按钮显示"渲染"
                );
                buttons.Add(toggleButton);

                // 编辑/保存按钮
                editButton = PreviewHelper.CreateEditButton(
                    () =>
                    {
                        if (isEditMode)
                        {
                            // 保存模式
                            try
                            {
                                // 从源码Tab获取内容
                                if (sourceTextBoxRef != null)
                                {
                                    htmlContent = sourceTextBoxRef.Text;
                                }

                                // 确定编码（优先使用UTF-8）
                                Encoding encoding = Encoding.UTF8;
                                try
                                {
                                    var originalBytes = File.ReadAllBytes(filePath);
                                    if (originalBytes.Length >= 3 && originalBytes[0] == 0xEF && originalBytes[1] == 0xBB && originalBytes[2] == 0xBF)
                                    {
                                        encoding = new UTF8Encoding(true);
                                    }
                                }
                                catch { }

                                // 保存文件
                                File.WriteAllText(filePath, htmlContent, encoding);
                                originalHtmlContent = htmlContent;

                                // 更新渲染视图
                                if (webViewRef != null)
                                {
                                    var uri = new Uri(filePath);
                                    webViewRef.Source = uri;
                                }

                                // 切换为只读模式
                                if (sourceTextBoxRef != null)
                                {
                                    sourceTextBoxRef.IsReadOnly = true;
                                    sourceTextBoxRef.Background = PreviewHelper.ReadOnlyBackground;
                                }
                                isEditMode = false;

                                // 更新按钮
                                if (editButton != null)
                                {
                                    editButton.Content = "✏️ 编辑";
                                    editButton.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                                }

                                MessageBox.Show("文件已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        else
                        {
                            // 编辑模式 - 切换到源码视图
                            currentViewIndex = 1;
                            if (webViewRef != null)
                            {
                                webViewRef.Visibility = Visibility.Collapsed;
                            }
                            if (sourceTextBoxRef != null)
                            {
                                sourceTextBoxRef.Visibility = Visibility.Visible;
                            }
                            if (toggleButton != null)
                            {
                                toggleButton.Content = "🎨 渲染";
                            }

                            if (sourceTextBoxRef != null)
                            {
                                sourceTextBoxRef.IsReadOnly = false;
                                sourceTextBoxRef.Background = PreviewHelper.EditModeBackground; // 浅蓝色背景表示可编辑
                            }
                            isEditMode = true;

                            // 更新按钮
                            if (editButton != null)
                            {
                                editButton.Content = "💾 保存";
                                editButton.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                            }
                        }
                    },
                    false
                );
                buttons.Add(editButton);
                buttons.Add(PreviewHelper.CreateOpenButton(filePath));

                var titlePanel = PreviewHelper.CreateTitlePanel("🌐", $"HTML 文件: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);

                // 渲染视图（默认显示）
                webViewRef = new WebView2
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Visibility = Visibility.Visible
                };

                // 异步加载HTML内容 - 使用 file:// 协议直接加载文件
                // 这样可以正确加载外部资源（如背景图片URL）和相对路径资源
                webViewRef.Loaded += async (s, e) =>
                {
                    try
                    {
                        await webViewRef.EnsureCoreWebView2Async();

                        // 设置WebView2的视口宽度，确保媒体查询能正确判断
                        // 解决HTML中媒体查询在小宽度时隐藏内容的问题
                        var coreWebView2 = webViewRef.CoreWebView2;
                        if (coreWebView2 != null)
                        {
                            // 在DOMContentLoaded后注入脚本，强制显示内容并设置视口
                            coreWebView2.DOMContentLoaded += async (sender, args) =>
                            {
                                try
                                {
                                    // 强制设置viewport meta标签，确保视口宽度足够大
                                    // 同时强制显示可能被媒体查询隐藏的内容
                                    string script = @"
                                        (function() {
                                            // 设置或更新viewport meta标签
                                            var viewport = document.querySelector('meta[name=""viewport""]');
                                            if (!viewport) {
                                                viewport = document.createElement('meta');
                                                viewport.name = 'viewport';
                                                document.head.appendChild(viewport);
                                            }
                                            // 设置视口宽度为1400px，确保媒体查询不会隐藏内容
                                            viewport.content = 'width=1400, initial-scale=1.0, minimum-scale=0.1, maximum-scale=3.0, user-scalable=yes';

                                            // 强制显示可能被媒体查询隐藏的内容
                                            var banner = document.getElementById('diagram-banner');
                                            if (banner) {
                                                banner.style.display = 'block';
                                                banner.style.visibility = 'visible';
                                            }

                                            // 添加样式来覆盖媒体查询，确保内容始终显示
                                            var style = document.createElement('style');
                                            style.textContent = '@media only screen and (max-width: 1024px) { #diagram-banner { display: block !important; } }';
                                            document.head.appendChild(style);
                                        })();
                                    ";
                                    await coreWebView2.ExecuteScriptAsync(script);
                                }
                                catch
                                {
                                }
                            };
                        }

                        // 使用 file:// 协议直接加载HTML文件
                        // NavigateToString 会将HTML视为 about:blank 源，导致外部资源无法加载
                        var uri = new Uri(filePath);
                        webViewRef.Source = uri;
                    }
                    catch (Exception ex)
                    {
                        // 如果加载失败，回退到 NavigateToString 并显示错误信息
                        try
                        {
                            await webViewRef.EnsureCoreWebView2Async();
                            webViewRef.NavigateToString($"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>渲染失败: {WebUtility.HtmlEncode(ex.Message)}<br/>尝试直接加载文件失败，可能因为文件路径或权限问题。</body></html>");
                        }
                        catch { }
                    }
                };

                // 源码视图（默认隐藏）
                sourceTextBoxRef = new TextBox
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

                // 源码的右键菜单（只包含复制）
                var sourceContextMenu = new ContextMenu();
                var sourceCopyItem = new MenuItem
                {
                    Header = "复制",
                    InputGestureText = "Ctrl+C"
                };
                sourceCopyItem.Click += (s, e) =>
                {
                    if (sourceTextBoxRef != null)
                    {
                        if (!string.IsNullOrEmpty(sourceTextBoxRef.SelectedText))
                        {
                            Clipboard.SetText(sourceTextBoxRef.SelectedText);
                        }
                        else
                        {
                            Clipboard.SetText(sourceTextBoxRef.Text);
                        }
                    }
                };
                sourceContextMenu.Items.Add(sourceCopyItem);
                sourceTextBoxRef.ContextMenu = sourceContextMenu;

                // 将内容添加到Grid中（不使用TabControl，直接添加控件）
                contentGrid.Children.Add(webViewRef);
                contentGrid.Children.Add(sourceTextBoxRef);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"HTML预览失败: {ex.Message}");
            }
        }
    }
}

