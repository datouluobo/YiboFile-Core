using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.Controls;

namespace YiboFile.Previews.DocumentHandlers
{
    /// <summary>
    /// DOC (旧版 Word) 文档预览处理器
    /// 需要安装 Microsoft Word 才能转换预览
    /// </summary>
    public class DocPreviewHandler : IDocumentPreviewHandler
    {
        private readonly DocxPreviewHandler _docxHandler = new DocxPreviewHandler();

        public bool CanHandle(string extension)
        {
            return extension?.ToLower() == ".doc";
        }

        public UIElement CreatePreview(string filePath)
        {
            var mainContainer = new Grid();
            mainContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 统一工具栏
            var toolbar = new TextPreviewToolbar
            {
                FileName = Path.GetFileName(filePath),
                FileIcon = "📄",
                ShowSearch = false,
                ShowWordWrap = false,
                ShowEncoding = false,
                ShowViewToggle = false,
                ShowFormat = false
            };
            toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

            Grid.SetRow(toolbar, 0);
            mainContainer.Children.Add(toolbar);

            // 加载提示
            var loadingPanel = PreviewHelper.CreateLoadingPanel("⏳ 正在检测文档预览...");
            Grid.SetRow(loadingPanel, 1);
            mainContainer.Children.Add(loadingPanel);

            // 异步检查和转换
            Task.Run(() =>
            {
                try
                {
                    var tempDocx = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(filePath) + ".docx");
                    string errorMsg;
                    bool canPreview = ConvertDocToDocx(filePath, tempDocx, out errorMsg);
                    bool hasWord = errorMsg == null || !errorMsg.Contains("未检测到");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        mainContainer.Children.Remove(loadingPanel);

                        // 转换按钮
                        var convertButton = PreviewHelper.CreateConvertButton(
                            "🔄 转换为DOCX格式",
                            async (s, e) =>
                            {
                                var btn = s as Button;
                                try
                                {
                                    btn.IsEnabled = false;
                                    btn.Content = "⏳ 转换中...";

                                    string directory = Path.GetDirectoryName(filePath);
                                    string baseName = Path.GetFileNameWithoutExtension(filePath);
                                    string outputPath = Path.Combine(directory, baseName + ".docx");
                                    outputPath = GetUniqueFilePath(outputPath);

                                    string convertError = null;
                                    bool success = await Task.Run(() => ConvertDocToDocx(filePath, outputPath, out convertError));

                                    if (success)
                                    {
                                        btn.Content = "✅ 转换成功";
                                        MessageBox.Show($"文件已成功转换为DOCX格式：\n{outputPath}", "转换成功", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        string errorTitle = convertError?.Contains("未检测到") == true ? "需要 Microsoft Word" : "转换错误";
                                        MessageBox.Show(convertError ?? "转换失败", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                                        btn.IsEnabled = true;
                                        btn.Content = "🔄 转换为DOCX格式";
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                    btn.IsEnabled = true;
                                    btn.Content = "🔄 转换为DOCX格式";
                                }
                            }
                        );

                        if (!hasWord)
                        {
                            convertButton.IsEnabled = false;
                            convertButton.ToolTip = "未检测到 Microsoft Word";
                        }

                        // 更新工具栏
                        toolbar.CustomActionContent = convertButton;

                        if (canPreview)
                        {
                            // 使用 DOCX Handler 预览转换后的文件
                            var previewContent = _docxHandler.CreatePreview(tempDocx);
                            Grid.SetRow(previewContent, 1);
                            mainContainer.Children.Add(previewContent);
                        }
                        else
                        {
                            // 显示错误信息
                            var errorPanel = CreateDocumentErrorPanel(errorMsg ?? "无法预览 DOC 文件");
                            Grid.SetRow(errorPanel, 1);
                            mainContainer.Children.Add(errorPanel);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        mainContainer.Children.Remove(loadingPanel);
                        var errorPanel = CreateDocumentErrorPanel($"DOC 预览失败: {ex.Message}");
                        Grid.SetRow(errorPanel, 1);
                        mainContainer.Children.Add(errorPanel);
                    });
                }
            });

            return mainContainer;
        }

        private bool ConvertDocToDocx(string docPath, string docxPath, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                // 尝试使用 Word COM 自动化
                Type wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType == null)
                {
                    errorMessage = "未检测到 Microsoft Word。\n\n转换 DOC 到 DOCX 需要安装 Microsoft Word。";
                    return false;
                }

                dynamic wordApp = Activator.CreateInstance(wordType);
                try
                {
                    // 尝试设置Visible=false
                    try
                    {
                        wordApp.Visible = false;
                    }
                    catch
                    {
                        // 某些版本的Word不允许隐藏窗口，忽略此错误
                    }

                    wordApp.DisplayAlerts = 0; // wdAlertsNone

                    dynamic document = wordApp.Documents.Open(docPath, ReadOnly: true);

                    // 保存为 DOCX 格式 (wdFormatXMLDocument = 12)
                    document.SaveAs2(docxPath, 12);
                    document.Close(false);

                    return true;
                }
                finally
                {
                    try
                    {
                        wordApp.Quit(false);
                    }
                    catch (COMException) { }
                    catch { }

                    try
                    {
                        Marshal.ReleaseComObject(wordApp);
                    }
                    catch (COMException) { }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"转换失败: {ex.Message}\n\n请确保：\n1. 已安装 Microsoft Word\n2. 文件未被其他程序占用\n3. 有足够的磁盘空间";
                return false;
            }
        }

        private string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
                return filePath;

            string directory = Path.GetDirectoryName(filePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int counter = 1;
            string newFilePath;
            do
            {
                newFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}({counter}){extension}");
                counter++;
            }
            while (File.Exists(newFilePath));

            return newFilePath;
        }

        private UIElement CreateDocumentErrorPanel(string message)
        {
            var panel = new StackPanel
            {
                Background = System.Windows.Media.Brushes.White,
                Margin = new Thickness(20)
            };

            var errorText = new TextBlock
            {
                Text = message,
                Foreground = System.Windows.Media.Brushes.Red,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            };
            panel.Children.Add(errorText);

            return panel;
        }
    }
}

