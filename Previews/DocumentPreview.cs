using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 文档文件预览（DOCX、DOC、PDF）
    /// </summary>
    public class DocumentPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            
            return extension switch
            {
                ".docx" => CreateDocxPreview(filePath),
                ".doc" => CreateDocPreview(filePath),
                ".pdf" => CreatePdfPreview(filePath),
                _ => PreviewHelper.CreateErrorPreview("不支持的文档格式")
            };
        }

        #region DOCX 预览

        private UIElement CreateDocxPreview(string filePath)
        {
            try
            {
                // 转换 DOCX 到 HTML
                string htmlContent = DocxToHtmlConverter.Convert(filePath);

                // 创建 WebView2 控件
                var webView = new WebView2
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                // 异步加载 HTML 内容
                webView.Loaded += async (s, e) =>
                {
                    try
                    {
                        await webView.EnsureCoreWebView2Async();
                        webView.NavigateToString(htmlContent);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WebView2 加载错误: {ex.Message}");
                    }
                };

                return webView;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取Word文档: {ex.Message}");
            }
        }

        #endregion

        #region DOC 预览

        private UIElement CreateDocPreview(string filePath)
        {
            try
            {
                // DOC文件是旧的二进制格式，需要使用COM对象来读取
                // 为了简化，这里提供一个提示信息
                var mainPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Background = Brushes.White
                };

                // 标题区域
                var titlePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Background = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                    Margin = new Thickness(0, 0, 0, 15)
                };

                var titleIcon = new TextBlock
                {
                    Text = "📋",
                    FontSize = 18,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 10, 5, 10)
                };

                var titleText = new TextBlock
                {
                    Text = "Word 文档（旧格式）",
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51))
                };

                var fileInfo = new TextBlock
                {
                    Text = $"· {Path.GetFileName(filePath)}",
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128)),
                    Margin = new Thickness(10, 10, 10, 10)
                };

                titlePanel.Children.Add(titleIcon);
                titlePanel.Children.Add(titleText);
                titlePanel.Children.Add(fileInfo);

                mainPanel.Children.Add(titlePanel);

                // 内容区域
                var contentPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(20)
                };

                var warningIcon = new TextBlock
                {
                    Text = "⚠️",
                    FontSize = 48,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 20)
                };

                var infoText = new TextBlock
                {
                    Text = "该文档为旧的 DOC 格式（Microsoft Word 97-2003）\n\n由于 DOC 文件使用二进制格式，无法直接预览内容。\n建议将文件转换为 DOCX 格式以获得更好的预览体验。",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    Margin = new Thickness(0, 0, 0, 20)
                };

                contentPanel.Children.Add(warningIcon);
                contentPanel.Children.Add(infoText);

                // 按钮容器
                var buttonContainer = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // 转换按钮
                var convertButton = new Button
                {
                    Content = "🔄 转换为DOCX格式",
                    Padding = new Thickness(15, 8, 15, 8),
                    Margin = new Thickness(5),
                    Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };

                convertButton.Click += async (s, e) =>
                {
                    try
                    {
                        convertButton.IsEnabled = false;
                        convertButton.Content = "⏳ 转换中...";

                        var outputPath = Path.ChangeExtension(filePath, ".docx");

                        // 检查目标文件是否已存在
                        if (File.Exists(outputPath))
                        {
                            var result = MessageBox.Show(
                                $"文件 {Path.GetFileName(outputPath)} 已存在，是否覆盖？",
                                "确认覆盖",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (result != MessageBoxResult.Yes)
                            {
                                convertButton.IsEnabled = true;
                                convertButton.Content = "🔄 转换为DOCX格式";
                                return;
                            }
                        }

                        // 在后台线程执行转换
                        bool success = await System.Threading.Tasks.Task.Run(() => ConvertDocToDocx(filePath, outputPath));

                        if (success)
                        {
                            MessageBox.Show(
                                $"转换成功！\n新文件: {Path.GetFileName(outputPath)}",
                                "转换完成",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            
                            convertButton.Content = "✅ 转换成功";
                            
                            // 请求刷新文件列表
                            PreviewFactory.OnFileListRefreshRequested?.Invoke();
                        }
                        else
                        {
                            convertButton.IsEnabled = true;
                            convertButton.Content = "🔄 转换为DOCX格式";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        convertButton.IsEnabled = true;
                        convertButton.Content = "🔄 转换为DOCX格式";
                    }
                };

                // 打开按钮
                var openButton = PreviewHelper.CreateOpenButton(filePath, "📂 打开文件");
                openButton.Margin = new Thickness(5);

                buttonContainer.Children.Add(convertButton);
                buttonContainer.Children.Add(openButton);

                contentPanel.Children.Add(buttonContainer);

                mainPanel.Children.Add(contentPanel);

                return mainPanel;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取Word文档: {ex.Message}");
            }
        }

        /// <summary>
        /// 将 DOC 文件转换为 DOCX 文件
        /// </summary>
        private bool ConvertDocToDocx(string docPath, string docxPath)
        {
            try
            {
                // 尝试使用 Word COM 自动化
                Type wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType == null)
                {
                    MessageBox.Show(
                        "未检测到 Microsoft Word。\n\n转换 DOC 到 DOCX 需要安装 Microsoft Word。",
                        "需要 Microsoft Word",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                dynamic wordApp = Activator.CreateInstance(wordType);
                try
                {
                    wordApp.Visible = false;
                    wordApp.DisplayAlerts = 0; // wdAlertsNone

                    dynamic doc = wordApp.Documents.Open(docPath);

                    // 保存为 DOCX 格式
                    // wdFormatXMLDocument = 12
                    doc.SaveAs2(docxPath, 12);
                    doc.Close(false);

                    return true;
                }
                finally
                {
                    wordApp.Quit(false);
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(wordApp);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"转换失败: {ex.Message}\n\n请确保：\n1. 已安装 Microsoft Word\n2. 文件未被其他程序占用\n3. 有足够的磁盘空间",
                    "转换错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

        #region PDF 预览

        private UIElement CreatePdfPreview(string filePath)
        {
            try
            {
                // 使用 Grid 布局
                var mainGrid = new Grid();

                // 定义行：标题行（自动高度）+ WebView2行（占满剩余空间）+ 按钮行（自动高度）
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 标题栏
                var titlePanel = PreviewHelper.CreateTitlePanel("📄", $"PDF 文件: {Path.GetFileName(filePath)}");
                Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

                // 创建 WebView2 控件来显示 PDF - 自动填充可用空间
                var webView = new WebView2
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0)
                };

                Grid.SetRow(webView, 1);
                mainGrid.Children.Add(webView);

                // 异步加载 PDF
                webView.Loaded += async (s, e) =>
                {
                    try
                    {
                        await webView.EnsureCoreWebView2Async();
                        webView.Source = new Uri(filePath);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"WebView2 加载错误: {ex.Message}");
                    }
                };

                // 底部按钮栏
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 5)
                };

                var openButton = PreviewHelper.CreateOpenButton(filePath);
                buttonPanel.Children.Add(openButton);

                Grid.SetRow(buttonPanel, 2);
                mainGrid.Children.Add(buttonPanel);

                return mainGrid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载PDF: {ex.Message}");
            }
        }

        #endregion
    }
}

