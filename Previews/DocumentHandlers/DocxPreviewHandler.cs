using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Web.WebView2.Wpf;
using YiboFile.Controls;

namespace YiboFile.Previews.DocumentHandlers
{
    /// <summary>
    /// DOCX 文档预览处理器
    /// </summary>
    public class DocxPreviewHandler : IDocumentPreviewHandler
    {
        public bool CanHandle(string extension)
        {
            return extension?.ToLower() == ".docx";
        }

        public UIElement CreatePreview(string filePath)
        {
            try
            {
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

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
                grid.Children.Add(toolbar);

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

                        // 在后台线程提取文本和图片
                        string html = await Task.Run(() =>
                        {
                            try
                            {
                                return GenerateHtmlFromDocx(filePath);
                            }
                            catch (Exception)
                            {
                                // 如果完整预览失败，尝试仅提取文本
                                try
                                {
                                    return GenerateSimpleTextPreview(filePath);
                                }
                                catch (Exception ex2)
                                {
                                    return $"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>预览失败: {WebUtility.HtmlEncode(ex2.Message)}</body></html>";
                                }
                            }
                        });

                        // 如果HTML太大，保存到临时文件然后导航
                        if (html.Length > 1.5 * 1024 * 1024) // 1.5MB
                        {
                            try
                            {
                                var tempHtmlFile = Path.Combine(Path.GetTempPath(), $"docx_preview_{Guid.NewGuid()}.html");
                                File.WriteAllText(tempHtmlFile, html, Encoding.UTF8);
                                var fileUri = new Uri(tempHtmlFile).ToString();
                                await webView.EnsureCoreWebView2Async();
                                webView.CoreWebView2.Navigate(fileUri);

                                // 清理：延迟删除临时文件
                                webView.CoreWebView2.NavigationCompleted += (navS, navE) =>
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
                            {
                                // 回退到NavigateToString
                                webView.NavigateToString(html);
                            }
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
                return CreateDocumentErrorPanel($"DOCX 预览失败: {ex.Message}");
            }
        }

        #region HTML 生成

        private string GenerateHtmlFromDocx(string filePath)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html><head>");
            sb.Append("<meta charset='utf-8'>");
            sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("<style>");
            sb.Append("body {");
            sb.Append("  font-family: 'Segoe UI', 'Microsoft YaHei', Arial, sans-serif;");
            sb.Append("  margin: 0;");
            sb.Append("  padding: 20px 40px;");
            sb.Append("  line-height: 1.8;");
            sb.Append("  color: #333;");
            sb.Append("  background: #fff;");
            sb.Append("  max-width: 100%;");
            sb.Append("  word-wrap: break-word;");
            sb.Append("  overflow-wrap: break-word;");
            sb.Append("}");
            sb.Append("p {");
            sb.Append("  margin: 12px 0;");
            sb.Append("  padding: 0;");
            sb.Append("  text-align: justify;");
            sb.Append("}");
            sb.Append("img {");
            sb.Append("  max-width: 100%;");
            sb.Append("  height: auto;");
            sb.Append("  display: block;");
            sb.Append("  margin: 12px auto;");
            sb.Append("}");
            sb.Append("</style>");
            sb.Append("</head><body>");

            using (var wordDoc = WordprocessingDocument.Open(filePath, false))
            {
                var mainPart = wordDoc.MainDocumentPart;
                if (mainPart == null || mainPart.Document == null || mainPart.Document.Body == null)
                {
                    throw new Exception("无法读取DOCX文档结构");
                }

                var body = mainPart.Document.Body;

                // 提取图片映射
                var imageMap = ExtractImages(mainPart);

                // 遍历文档元素
                foreach (var element in body.Elements())
                {
                    ProcessElement(element, sb, imageMap, mainPart);
                }
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }

        private string GenerateSimpleTextPreview(string filePath)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>");
            sb.Append("<html><head>");
            sb.Append("<meta charset='utf-8'>");
            sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("<style>");
            sb.Append("body {");
            sb.Append("  font-family: 'Segoe UI', 'Microsoft YaHei', Arial, sans-serif;");
            sb.Append("  margin: 0;");
            sb.Append("  padding: 20px 40px;");
            sb.Append("  line-height: 1.8;");
            sb.Append("  color: #333;");
            sb.Append("  background: #fff;");
            sb.Append("}");
            sb.Append("p {");
            sb.Append("  margin: 12px 0;");
            sb.Append("  padding: 0;");
            sb.Append("  text-align: justify;");
            sb.Append("}");
            sb.Append("</style>");
            sb.Append("</head><body>");

            using (var wordDoc = WordprocessingDocument.Open(filePath, false))
            {
                var body = wordDoc.MainDocumentPart.Document.Body;
                var paragraphs = new List<string>();

                foreach (var element in body.Elements())
                {
                    var text = element.InnerText ?? "";
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        paragraphs.Add(WebUtility.HtmlEncode(text));
                    }
                }

                foreach (var para in paragraphs)
                {
                    sb.Append($"<p>{para}</p>");
                }
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }

        #endregion

        #region 图片处理

        private Dictionary<string, string> ExtractImages(MainDocumentPart mainPart)
        {
            var imageMap = new Dictionary<string, string>();
            var imagePartUriMap = new Dictionary<string, string>();

            try
            {
                if (mainPart == null)
                    return imageMap;

                // 建立URI到关系ID的映射
                try
                {
                    if (mainPart.Parts != null)
                    {
                        foreach (var part in mainPart.Parts)
                        {
                            if (part.OpenXmlPart is ImagePart imagePart)
                            {
                                var uri = part.OpenXmlPart.Uri?.ToString() ?? "";
                                var relId = part.RelationshipId ?? "";
                                if (!string.IsNullOrEmpty(uri) && !string.IsNullOrEmpty(relId))
                                {
                                    imagePartUriMap[uri] = relId;
                                }
                            }
                        }
                    }
                }
                catch { }

                // 遍历所有图片部分
                foreach (var imagePart in mainPart.ImageParts)
                {
                    try
                    {
                        if (imagePart == null)
                            continue;

                        string relationshipId = null;

                        // 方法1: 通过URI映射获取
                        var uri = imagePart.Uri?.ToString() ?? "";
                        if (imagePartUriMap.ContainsKey(uri))
                        {
                            relationshipId = imagePartUriMap[uri];
                        }

                        // 方法2: 通过GetIdOfPart获取
                        if (string.IsNullOrEmpty(relationshipId))
                        {
                            try
                            {
                                relationshipId = mainPart.GetIdOfPart(imagePart);
                            }
                            catch { }
                        }

                        if (string.IsNullOrEmpty(relationshipId))
                            continue;

                        using (var stream = imagePart.GetStream())
                        {
                            if (stream == null)
                                continue;

                            using (var memoryStream = new MemoryStream())
                            {
                                stream.CopyTo(memoryStream);

                                if (memoryStream.Length == 0 || memoryStream.Length > 50 * 1024 * 1024)
                                    continue;

                                byte[] imageBytes = memoryStream.ToArray();

                                // 确定MIME类型
                                string mimeType = "image/png";
                                try
                                {
                                    var extension = Path.GetExtension(uri).ToLower();
                                    if (extension == ".jpg" || extension == ".jpeg")
                                        mimeType = "image/jpeg";
                                    else if (extension == ".gif")
                                        mimeType = "image/gif";
                                    else if (extension == ".bmp")
                                        mimeType = "image/bmp";
                                }
                                catch { }

                                string base64 = Convert.ToBase64String(imageBytes);
                                string imageData = $"data:{mimeType};base64,{base64}";

                                imageMap[relationshipId] = imageData;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return imageMap;
        }

        private string ExtractImageFromDrawing(Drawing drawing, Dictionary<string, string> imageMap, MainDocumentPart mainPart)
        {
            if (drawing == null || imageMap == null || imageMap.Count == 0)
                return null;

            try
            {
                var xml = drawing.OuterXml;
                if (string.IsNullOrEmpty(xml))
                    return null;

                var doc = XDocument.Parse(xml);

                // 查找所有embed属性
                var allEmbedAttrs = doc.Descendants()
                    .SelectMany(e => e.Attributes())
                    .Where(attr => attr.Name.LocalName == "embed");

                foreach (var embed in allEmbedAttrs)
                {
                    var embedId = embed.Value?.Trim();
                    if (string.IsNullOrEmpty(embedId))
                        continue;

                    // 直接匹配
                    if (imageMap.ContainsKey(embedId))
                        return imageMap[embedId];

                    // 不区分大小写匹配
                    var matchedKey = imageMap.Keys.FirstOrDefault(k =>
                        string.Equals(k, embedId, StringComparison.OrdinalIgnoreCase));
                    if (matchedKey != null)
                        return imageMap[matchedKey];
                }
            }
            catch { }

            return null;
        }

        #endregion

        #region 元素处理

        private void ProcessElement(OpenXmlElement element, StringBuilder sb, Dictionary<string, string> imageMap, MainDocumentPart mainPart)
        {
            if (element is Paragraph paragraph)
            {
                var hasContent = false;
                var paraSb = new StringBuilder();

                foreach (var run in paragraph.Elements<Run>())
                {
                    // 处理文本
                    foreach (var text in run.Elements<Text>())
                    {
                        var textValue = text.Text;
                        if (!string.IsNullOrWhiteSpace(textValue))
                        {
                            paraSb.Append(WebUtility.HtmlEncode(textValue));
                            hasContent = true;
                        }
                    }

                    // 处理图片
                    foreach (var drawing in run.Elements<Drawing>())
                    {
                        var imageData = ExtractImageFromDrawing(drawing, imageMap, mainPart);
                        if (!string.IsNullOrEmpty(imageData))
                        {
                            paraSb.Append($"<img src=\"{imageData}\" alt=\"图片\" style=\"max-width: 100%; height: auto; display: block; margin: 12px auto;\" />");
                            hasContent = true;
                        }
                    }
                }

                if (hasContent)
                {
                    sb.Append("<p>");
                    sb.Append(paraSb.ToString());
                    sb.Append("</p>");
                }
            }
            else if (element is Table table)
            {
                // 处理表格（简化）
                sb.Append("<table border='1' cellpadding='5' style='border-collapse: collapse; margin: 12px 0; width: 100%;'>");
                foreach (var row in table.Elements<TableRow>())
                {
                    sb.Append("<tr>");
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        sb.Append("<td>");
                        foreach (var para in cell.Elements<Paragraph>())
                        {
                            var text = para.InnerText ?? "";
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                sb.Append(WebUtility.HtmlEncode(text));
                            }
                        }
                        sb.Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }
            else
            {
                // 其他元素，提取文本
                var text = element.InnerText ?? "";
                if (!string.IsNullOrWhiteSpace(text))
                {
                    sb.Append($"<p>{WebUtility.HtmlEncode(text)}</p>");
                }
            }
        }

        #endregion

        #region 错误处理

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

        #endregion
    }
}

