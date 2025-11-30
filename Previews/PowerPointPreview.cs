using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Wpf;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Drawing;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace OoiMRR.Previews
{
    /// <summary>
    /// PowerPoint 文件预览（PPT、PPTX）
    /// </summary>
    public class PowerPointPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            var extension = System.IO.Path.GetExtension(filePath).ToLower();

            if (extension == ".ppt")
            {
                return CreatePptPreview(filePath);
            }
            else if (extension == ".pptx")
            {
                return CreatePptxPreview(filePath);
            }

            return PreviewHelper.CreateErrorPreview("不支持的文件格式");
        }

        #region PPTX 预览

        private UIElement CreatePptxPreview(string filePath)
        {
            try
            {
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("📊", $"PowerPoint 演示文稿: {System.IO.Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);

                var webView = new WebView2
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    MinHeight = 400
                };
                Grid.SetRow(webView, 1);
                grid.Children.Add(webView);

                // 异步加载文档内容
                webView.Loaded += async (s, e) =>
                {
                    try
                    {
                        await webView.EnsureCoreWebView2Async();

                        // 在后台线程提取内容
                        string html = await Task.Run(() =>
                        {
                            try
                            {
                                return GenerateHtmlFromPptx(filePath);
                            }
                            catch (Exception ex)
                            {
                                                                return $"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>预览失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>";
                            }
                        });

                        // 如果HTML太大，保存到临时文件然后导航
                        if (html.Length > 1.5 * 1024 * 1024) // 1.5MB
                        {
                            try
                            {
                                var tempHtmlFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pptx_preview_{Guid.NewGuid()}.html");
                                File.WriteAllText(tempHtmlFile, html, Encoding.UTF8);
                                var fileUri = new Uri(tempHtmlFile).ToString();

                                await webView.EnsureCoreWebView2Async();
                                webView.CoreWebView2.Navigate(fileUri);

                                // 清理：在WebView关闭时删除临时文件
                                webView.CoreWebView2.NavigationCompleted += (s, e) =>
                                {
                                    try
                                    {
                                        Task.Delay(5000).ContinueWith(_ =>
                                        {
                                            try { File.Delete(tempHtmlFile); }
                                            catch { }
                                        });
                                    }
                                    catch { }
                                };
                            }
                            catch (Exception)
            {webView.NavigateToString(html);}
                        }
                        else
                        {
                            webView.NavigateToString(html);
                        }
                    }
                    catch (Exception ex)
                    {
                        webView.NavigateToString($"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>预览失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>");
                    }
                };

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载PPTX: {ex.Message}");
            }
        }

        private string GenerateHtmlFromPptx(string filePath)
        {
            try
            {
                using var presentationDoc = PresentationDocument.Open(filePath, false);

                if (presentationDoc?.PresentationPart?.Presentation == null)
                {
                    throw new Exception("无法读取PPTX文档结构");
                }

                var presentationPart = presentationDoc.PresentationPart;
                var slideIdList = presentationPart.Presentation.SlideIdList;

                if (slideIdList == null)
                {
                    throw new Exception("未找到幻灯片列表");
                }

                var slides = new List<SlideContent>();
                var allImageMap = new Dictionary<string, Dictionary<string, string>>(); // slideId -> imageMap

                // 首先提取所有幻灯片的图片映射
                foreach (var slideId in slideIdList.Elements<SlideId>())
                {
                    var slidePart = presentationPart.GetPartById(slideId.RelationshipId) as SlidePart;
                    if (slidePart != null)
                    {
                        var imageMap = ExtractImagesFromSlidePart(slidePart);
                        allImageMap[slideId.RelationshipId] = imageMap;
                                            }
                }

                // 处理每张幻灯片
                int slideIndex = 0;
                foreach (var slideId in slideIdList.Elements<SlideId>())
                {
                    slideIndex++;
                    try
                    {
                        var slidePart = presentationPart.GetPartById(slideId.RelationshipId) as SlidePart;
                        if (slidePart?.Slide == null)
                            continue;

                        var slideContent = new SlideContent();
                        var contentBuilder = new StringBuilder();
                        var imageMap = allImageMap.ContainsKey(slideId.RelationshipId)
                            ? allImageMap[slideId.RelationshipId]
                            : new Dictionary<string, string>();

                        // 处理幻灯片中的所有形状
                        ProcessSlideShapes(slidePart.Slide, contentBuilder, imageMap);

                        slideContent.Content = contentBuilder.ToString();
                        slides.Add(slideContent);
                    }
                    catch (Exception ex)
                    {
                                                slides.Add(new SlideContent { Content = $"解析错误: {ex.Message}" });
                    }
                }

                var slideCount = slides.Count;
                var sb = new StringBuilder();
                sb.Append("<html><head><meta charset='utf-8'><style>");
                sb.Append("body{font-family:'Microsoft YaHei',Segoe UI,Arial,sans-serif;margin:0;padding:20px;background:#f5f5f5}");
                sb.Append(".header{background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);color:white;padding:20px;border-radius:8px;margin-bottom:20px}");
                sb.Append(".slide{background:white;margin:20px 0;padding:30px;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);min-height:400px}");
                sb.Append(".slide-number{color:#667eea;font-weight:bold;font-size:14px;margin-bottom:15px;border-bottom:2px solid #667eea;padding-bottom:8px}");
                sb.Append(".slide-content{line-height:1.8;color:#333}");
                sb.Append(".slide-content p{margin:12px 0;font-size:14px}");
                sb.Append(".slide-content h1,.slide-content h2,.slide-content h3{margin:15px 0 10px 0;color:#333}");
                sb.Append(".slide-content h1{font-size:24px;font-weight:bold}");
                sb.Append(".slide-content h2{font-size:20px;font-weight:bold}");
                sb.Append(".slide-content h3{font-size:16px;font-weight:bold}");
                sb.Append(".slide-content strong{font-weight:600;color:#222}");
                sb.Append(".slide-content em{font-style:italic}");
                sb.Append(".slide-content ul,.slide-content ol{margin:10px 0;padding-left:30px}");
                sb.Append(".slide-content li{margin:5px 0}");
                sb.Append(".slide-content img{max-width:100%;height:auto;margin:10px 0;border-radius:4px;box-shadow:0 2px 4px rgba(0,0,0,0.1);display:block}");
                sb.Append(".slide-image{text-align:center;margin:15px 0}");
                sb.Append(".slide-image img{max-width:90%;max-height:400px;border-radius:6px;box-shadow:0 4px 8px rgba(0,0,0,0.15)}");
                sb.Append(".empty-slide{color:#999;font-style:italic;text-align:center;padding:40px}");
                sb.Append("</style></head><body>");

                sb.Append($"<div class='header'><h1 style='margin:0'>📊 PowerPoint 演示文稿</h1><p style='margin:5px 0 0 0'>共 {slideCount} 张幻灯片</p></div>");

                for (int i = 0; i < slides.Count; i++)
                {
                    var slide = slides[i];
                    sb.Append($"<div class='slide'>");
                    sb.Append($"<div class='slide-number'>幻灯片 {i + 1} / {slideCount}</div>");

                    if (string.IsNullOrWhiteSpace(slide.Content))
                    {
                        sb.Append("<div class='empty-slide'>（空白幻灯片）</div>");
                    }
                    else
                    {
                        sb.Append($"<div class='slide-content'>{slide.Content}</div>");
                    }

                    sb.Append("</div>");
                }

                sb.Append("</body></html>");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>解析失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>";
            }
        }

        /// <summary>
        /// 从SlidePart提取图片到Base64映射（参考docx的处理方式）
        /// </summary>
        private Dictionary<string, string> ExtractImagesFromSlidePart(SlidePart slidePart)
        {
            var imageMap = new Dictionary<string, string>();

            try
            {
                if (slidePart == null)
                    return imageMap;

                int imageCount = 0;
                foreach (var imagePart in slidePart.ImageParts)
                {
                    try
                    {
                        if (imagePart == null)
                            continue;

                        // 获取关系ID
                        string relationshipId = null;
                        try
                        {
                            relationshipId = slidePart.GetIdOfPart(imagePart);
                        }
                        catch (Exception)
            {continue;}

                        if (string.IsNullOrEmpty(relationshipId))
                        {
                                                        continue;
                        }

                        using (var stream = imagePart.GetStream())
                        {
                            if (stream == null)
                            {
                                                                continue;
                            }

                            using (var memoryStream = new MemoryStream())
                            {
                                stream.CopyTo(memoryStream);

                                if (memoryStream.Length == 0 || memoryStream.Length > 50 * 1024 * 1024) // 限制50MB
                                {
                                                                        continue;
                                }

                                byte[] imageBytes = memoryStream.ToArray();

                                // 确定MIME类型
                                string mimeType = "image/png";
                                try
                                {
                                    var uri = imagePart.Uri?.ToString() ?? "";
                                    var extension = System.IO.Path.GetExtension(uri).ToLower();
                                    if (extension == ".jpg" || extension == ".jpeg")
                                        mimeType = "image/jpeg";
                                    else if (extension == ".gif")
                                        mimeType = "image/gif";
                                    else if (extension == ".bmp")
                                        mimeType = "image/bmp";
                                    else if (extension == ".png")
                                        mimeType = "image/png";

                                                                    }
                                catch
                                {
                                    // 使用默认PNG类型
                                }

                                string base64 = Convert.ToBase64String(imageBytes);
                                string imageData = $"data:{mimeType};base64,{base64}";

                                // 存储关系ID
                                imageMap[relationshipId] = imageData;

                                // 同时存储其他格式以确保匹配
                                if (!relationshipId.StartsWith("rId", StringComparison.OrdinalIgnoreCase))
                                {
                                    var match = System.Text.RegularExpressions.Regex.Match(relationshipId, @"(\d+)");
                                    if (match.Success)
                                    {
                                        var numericPart = match.Groups[1].Value;
                                        var rIdFormat = $"rId{numericPart}";
                                        if (!imageMap.ContainsKey(rIdFormat))
                                        {
                                            imageMap[rIdFormat] = imageData;
                                        }
                                    }
                                }

                                imageCount++;
                                                            }
                        }
                    }
                    catch{
                                            }
                }

                            }
            catch{
                            }

            return imageMap;
        }

        /// <summary>
        /// 处理幻灯片中的形状（文本、图片等）
        /// </summary>
        private void ProcessSlideShapes(Slide slide, StringBuilder sb, Dictionary<string, string> imageMap)
        {
            if (slide == null || slide.CommonSlideData == null)
                return;

            var shapeTree = slide.CommonSlideData.ShapeTree;
            if (shapeTree == null)
                return;

            foreach (var shape in shapeTree.Elements<DocumentFormat.OpenXml.Presentation.Shape>())
            {
                ProcessShape(shape, sb, imageMap);
            }

            foreach (var picture in shapeTree.Elements<DocumentFormat.OpenXml.Presentation.Picture>())
            {
                ProcessPicture(picture, sb, imageMap);
            }

            foreach (var groupShape in shapeTree.Elements<DocumentFormat.OpenXml.Presentation.GroupShape>())
            {
                ProcessGroupShape(groupShape, sb, imageMap);
            }
        }

        /// <summary>
        /// 处理形状（文本）
        /// </summary>
        private void ProcessShape(DocumentFormat.OpenXml.Presentation.Shape shape, StringBuilder sb, Dictionary<string, string> imageMap)
        {
            if (shape?.TextBody == null)
                return;

            var paragraphs = shape.TextBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>();
            foreach (var para in paragraphs)
            {
                var paraText = new StringBuilder();
                var runs = para.Elements<DocumentFormat.OpenXml.Drawing.Run>();

                foreach (var run in runs)
                {
                    // 处理文本
                    var texts = run.Elements<DocumentFormat.OpenXml.Drawing.Text>();
                    foreach (var text in texts)
                    {
                        if (!string.IsNullOrWhiteSpace(text.Text))
                        {
                            var textValue = WebUtility.HtmlEncode(text.Text);

                            // 检查格式
                            var isBold = run.RunProperties?.Bold != null;
                            var isItalic = run.RunProperties?.Italic != null;

                            if (isBold && isItalic)
                                paraText.Append($"<strong><em>{textValue}</em></strong>");
                            else if (isBold)
                                paraText.Append($"<strong>{textValue}</strong>");
                            else if (isItalic)
                                paraText.Append($"<em>{textValue}</em>");
                            else
                                paraText.Append(textValue);
                        }
                    }
                }

                if (paraText.Length > 0)
                {
                    var level = para.ParagraphProperties?.Level ?? 0;
                    if (level == 0)
                        sb.Append($"<p>{paraText.ToString()}</p>");
                    else if (level == 1)
                        sb.Append($"<h1>{paraText.ToString()}</h1>");
                    else if (level == 2)
                        sb.Append($"<h2>{paraText.ToString()}</h2>");
                    else
                        sb.Append($"<h3>{paraText.ToString()}</h3>");
                }
            }
        }

        /// <summary>
        /// 处理图片
        /// </summary>
        private void ProcessPicture(DocumentFormat.OpenXml.Presentation.Picture picture, StringBuilder sb, Dictionary<string, string> imageMap)
        {
            if (picture?.BlipFill == null || imageMap == null || imageMap.Count == 0)
                return;

            var blip = picture.BlipFill.Blip;
            if (blip == null)
                return;

            var embed = blip.Embed;
            if (embed == null || string.IsNullOrEmpty(embed.Value))
                return;

            var imageData = FindImageInMap(embed.Value, imageMap);
            if (!string.IsNullOrEmpty(imageData))
            {
                sb.Append($"<div class='slide-image'><img src=\"{imageData}\" alt=\"图片\" /></div>");
            }
        }

        /// <summary>
        /// 处理组合形状
        /// </summary>
        private void ProcessGroupShape(DocumentFormat.OpenXml.Presentation.GroupShape groupShape, StringBuilder sb, Dictionary<string, string> imageMap)
        {
            if (groupShape == null)
                return;

            foreach (var shape in groupShape.Elements<DocumentFormat.OpenXml.Presentation.Shape>())
            {
                ProcessShape(shape, sb, imageMap);
            }

            foreach (var picture in groupShape.Elements<DocumentFormat.OpenXml.Presentation.Picture>())
            {
                ProcessPicture(picture, sb, imageMap);
            }
        }

        /// <summary>
        /// 从映射表中查找图片数据
        /// </summary>
        private string FindImageInMap(string relationshipId, Dictionary<string, string> imageMap)
        {
            if (string.IsNullOrEmpty(relationshipId) || imageMap == null || imageMap.Count == 0)
                return null;

            // 尝试直接匹配
            if (imageMap.ContainsKey(relationshipId))
            {
                return imageMap[relationshipId];
            }

            // 尝试不区分大小写匹配
            var matchedKey = imageMap.Keys.FirstOrDefault(k =>
                string.Equals(k, relationshipId, StringComparison.OrdinalIgnoreCase));
            if (matchedKey != null)
            {
                return imageMap[matchedKey];
            }

            // 尝试rId格式转换
            if (!relationshipId.StartsWith("rId", StringComparison.OrdinalIgnoreCase))
            {
                var match = System.Text.RegularExpressions.Regex.Match(relationshipId, @"(\d+)");
                if (match.Success)
                {
                    var numericPart = match.Groups[1].Value;
                    var rIdFormat = $"rId{numericPart}";
                    if (imageMap.ContainsKey(rIdFormat))
                    {
                        return imageMap[rIdFormat];
                    }
                }
            }

            return null;
        }


        private class SlideContent
        {
            public string Content { get; set; } = "";
        }

        #endregion

        #region PPT 预览（旧格式）

        private UIElement CreatePptPreview(string filePath)
        {
            try
            {
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 转换按钮
                var convertButton = new Button
                {
                    Content = "🔄 转换为PPTX格式",
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontSize = 13
                };

                convertButton.Click += async (s, e) =>
                {
                    try
                    {
                        convertButton.IsEnabled = false;
                        convertButton.Content = "⏳ 转换中...";

                        try
                        {
                            // 生成输出路径（同目录，同名）
                            string directory = System.IO.Path.GetDirectoryName(filePath);
                            string baseName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                            string outputPath = System.IO.Path.Combine(directory, baseName + ".pptx");

                            // 如果文件已存在，添加序号
                            outputPath = GetUniqueFilePath(outputPath);

                            // 在后台线程执行转换
                            string errorMessage = null;
                            bool success = await System.Threading.Tasks.Task.Run(() =>
                            {
                                bool result = ConvertPptToPptx(filePath, outputPath, out errorMessage);
                                return result;
                            });

                            if (success)
                            {
                                convertButton.Content = "✅ 转换成功";
                            }
                            else
                            {
                                string errorTitle = errorMessage?.Contains("未检测到") == true ? "需要 Microsoft PowerPoint" : "转换错误";
                                MessageBox.Show(
                                    errorMessage ?? "转换失败",
                                    errorTitle,
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                                convertButton.IsEnabled = true;
                                convertButton.Content = "🔄 转换为PPTX格式";
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            convertButton.IsEnabled = true;
                            convertButton.Content = "🔄 转换为PPTX格式";
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        convertButton.IsEnabled = true;
                        convertButton.Content = "🔄 转换为PPTX格式";
                    }
                };

                var buttons = new List<Button>
                {
                    convertButton,
                    PreviewHelper.CreateOpenButton(filePath)
                };
                var titlePanel = PreviewHelper.CreateTitlePanel("📊", $"PowerPoint 演示文稿（旧格式）: {System.IO.Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);

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
                    Text = "该文件为旧的 PPT 格式（Microsoft PowerPoint 97-2003）\n\n由于 PPT 文件使用二进制格式，无法直接预览内容。\n建议将文件转换为 PPTX 格式以获得更好的预览体验。",
                    FontSize = 12,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    LineHeight = 24
                };

                contentPanel.Children.Add(warningIcon);
                contentPanel.Children.Add(infoText);

                Grid.SetRow(contentPanel, 1);
                grid.Children.Add(contentPanel);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取PPT文档: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成唯一文件名（如果文件已存在，添加序号）
        /// </summary>
        private string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            string directory = System.IO.Path.GetDirectoryName(filePath);
            string fileNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(filePath);
            string extension = System.IO.Path.GetExtension(filePath);

            int counter = 1;
            string newFilePath;
            do
            {
                newFilePath = System.IO.Path.Combine(directory, $"{fileNameWithoutExtension}({counter}){extension}");
                counter++;
            }
            while (File.Exists(newFilePath));

            return newFilePath;
        }

        /// <summary>
        /// 将 PPT 文件转换为 PPTX 文件
        /// </summary>
        private bool ConvertPptToPptx(string pptPath, string pptxPath, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                // 尝试使用 PowerPoint COM 自动化
                Type pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                if (pptType == null)
                {
                    errorMessage = "未检测到 Microsoft PowerPoint。\n\n转换 PPT 到 PPTX 需要安装 Microsoft PowerPoint。";
                    return false;
                }

                dynamic pptApp = Activator.CreateInstance(pptType);
                try
                {
                    // 尝试设置Visible=false，如果失败则忽略（某些版本不允许隐藏）
                    try
                    {
                        pptApp.Visible = false;
                    }
                    catch
                    {
                        // 某些版本的PowerPoint不允许隐藏窗口，忽略此错误
                    }

                    pptApp.DisplayAlerts = 0; // ppAlertsNone

                    dynamic presentation = pptApp.Presentations.Open(pptPath, ReadOnly: true, Untitled: false, WithWindow: false);

                    // 保存为 PPTX 格式
                    // ppSaveAsOpenXMLPresentation = 24
                    presentation.SaveAs(pptxPath, 24);
                    presentation.Close();

                    return true;
                }
                finally
                {
                    try
                    {
                        pptApp.Quit();
                    }
                    catch (COMException)
                    {
                        // 忽略退出时的 COM 异常
                    }
                    catch
                    {
                        // 忽略退出时的错误
                    }
                    try
                    {
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(pptApp);
                    }
                    catch (COMException)
                    {
                        // 忽略释放时的 COM 异常
                    }
                    catch
                    {
                        // 忽略释放时的错误
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"转换失败: {ex.Message}\n\n请确保：\n1. 已安装 Microsoft PowerPoint\n2. 文件未被其他程序占用\n3. 有足够的磁盘空间";
                return false;
            }
        }

        #endregion
    }
}

