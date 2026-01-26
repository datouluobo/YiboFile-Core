using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace YiboFile.Previews
{
    /// <summary>
    /// DOCX 到 HTML 转换器
    /// </summary>
    internal static class DocxToHtmlConverter
    {
        /// <summary>
        /// 将 DOCX 文件转换为 HTML
        /// </summary>
        public static string Convert(string filePath)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(filePath))
                {
                    var documentEntry = archive.GetEntry("word/document.xml");
                    if (documentEntry == null)
                    {
                        return "<html><body><p>无法找到文档内容</p></body></html>";
                    }

                    // 读取XML内容
                    string xmlContent;
                    using (var stream = documentEntry.Open())
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        xmlContent = reader.ReadToEnd();
                    }

                    // 解析XML并提取内容
                    var doc = XDocument.Parse(xmlContent);
                    XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

                    var body = doc.Descendants(w + "body").FirstOrDefault();
                    if (body == null) return "<html><body><p>无法找到文档内容</p></body></html>";
                    
                    var htmlContent = new StringBuilder();
                    
                    // 遍历body的所有子元素（段落、表格）
                    foreach (var element in body.Elements())
                    {
                        if (element.Name == w + "p")
                        {
                            // 处理段落
                            var paragraph = element;
                            var runs = paragraph.Descendants(w + "r").ToList();
                            var paragraphText = new StringBuilder();
                            bool hasContent = false;

                            foreach (var run in runs)
                            {
                                var texts = run.Descendants(w + "t").Where(t => t.Value != null);
                                foreach (var text in texts)
                                {
                                    var textValue = System.Security.SecurityElement.Escape(text.Value);
                                    
                                    // 检查格式
                                    var isBold = run.Descendants(w + "b").Any();
                                    var isItalic = run.Descendants(w + "i").Any();
                                    var isUnderline = run.Descendants(w + "u").Any();
                                    
                                    // 检查字体大小
                                    var fontSize = run.Descendants(w + "sz").FirstOrDefault();
                                    string fontSizeStyle = "";
                                    if (fontSize != null && fontSize.Attribute(w + "val") != null)
                                    {
                                        var sizeVal = fontSize.Attribute(w + "val").Value;
                                        if (int.TryParse(sizeVal, out int size))
                                        {
                                            double ptSize = size / 2.0;
                                            fontSizeStyle = $"font-size: {ptSize}pt;";
                                        }
                                    }
                                    
                                    // 检查颜色
                                    var color = run.Descendants(w + "color").FirstOrDefault();
                                    string colorStyle = "";
                                    if (color != null && color.Attribute(w + "val") != null)
                                    {
                                        var colorVal = color.Attribute(w + "val").Value;
                                        colorStyle = $"color: #{colorVal};";
                                    }
                                    
                                    var styles = new List<string>();
                                    if (isBold) styles.Add("font-weight: bold;");
                                    if (isItalic) styles.Add("font-style: italic;");
                                    if (isUnderline) styles.Add("text-decoration: underline;");
                                    if (!string.IsNullOrEmpty(fontSizeStyle)) styles.Add(fontSizeStyle);
                                    if (!string.IsNullOrEmpty(colorStyle)) styles.Add(colorStyle);
                                    
                                    if (styles.Count > 0)
                                    {
                                        paragraphText.Append($"<span style=\"{string.Join(" ", styles)}\">").Append(textValue).Append("</span>");
                                    }
                                    else
                                    {
                                        paragraphText.Append(textValue);
                                    }
                                    hasContent = true;
                                }
                            }

                            if (hasContent)
                            {
                                var jc = paragraph.Descendants(w + "jc").FirstOrDefault();
                                string paraAlign = "";
                                if (jc != null && jc.Attribute(w + "val") != null)
                                {
                                    var alignVal = jc.Attribute(w + "val").Value;
                                    switch (alignVal)
                                    {
                                        case "center": paraAlign = "text-align: center;"; break;
                                        case "right": paraAlign = "text-align: right;"; break;
                                        case "both": paraAlign = "text-align: justify;"; break;
                                    }
                                }
                                
                                var indent = paragraph.Descendants(w + "ind").FirstOrDefault();
                                string indentStyle = "";
                                if (indent != null && indent.Attribute(w + "firstLine") != null)
                                {
                                    var firstLine = indent.Attribute(w + "firstLine").Value;
                                    if (int.TryParse(firstLine, out int indentPx))
                                    {
                                        double indentEm = indentPx / 20.0;
                                        indentStyle = $"text-indent: {indentEm}em;";
                                    }
                                }
                                
                                string paraStyle = "";
                                if (!string.IsNullOrEmpty(paraAlign)) paraStyle += paraAlign;
                                if (!string.IsNullOrEmpty(indentStyle)) paraStyle += indentStyle;
                                
                                if (!string.IsNullOrEmpty(paraStyle))
                                {
                                    htmlContent.Append($"<p style=\"{paraStyle}\">").Append(paragraphText.ToString()).Append("</p>\n");
                                }
                                else
                                {
                                    htmlContent.Append("<p>").Append(paragraphText.ToString()).Append("</p>\n");
                                }
                            }
                        }
                        else if (element.Name == w + "tbl")
                        {
                            // 处理表格
                            htmlContent.Append("<table>\n");
                            
                            var rows = element.Descendants(w + "tr").ToList();
                            bool isFirstRow = true;
                            
                            foreach (var row in rows)
                            {
                                htmlContent.Append("<tr>\n");
                                var cells = row.Descendants(w + "tc").ToList();
                                
                                foreach (var cell in cells)
                                {
                                    var cellText = new StringBuilder();
                                    var cellRuns = cell.Descendants(w + "r").ToList();
                                    
                                    foreach (var run in cellRuns)
                                    {
                                        var texts = run.Descendants(w + "t").Where(t => t.Value != null);
                                        foreach (var text in texts)
                                        {
                                            var textValue = System.Security.SecurityElement.Escape(text.Value);
                                            var isBold = run.Descendants(w + "b").Any();
                                            if (isBold)
                                            {
                                                cellText.Append("<strong>").Append(textValue).Append("</strong>");
                                            }
                                            else
                                            {
                                                cellText.Append(textValue);
                                            }
                                        }
                                    }
                                    
                                    // 第一行使用th标签
                                    if (isFirstRow)
                                    {
                                        htmlContent.Append("<th>").Append(cellText.ToString()).Append("</th>\n");
                                    }
                                    else
                                    {
                                        htmlContent.Append("<td>").Append(cellText.ToString()).Append("</td>\n");
                                    }
                                }
                                
                                htmlContent.Append("</tr>\n");
                                isFirstRow = false;
                            }
                            
                            htmlContent.Append("</table>\n");
                        }
                    }

                    // 构建完整的 HTML
                    var html = new StringBuilder();
                    html.AppendLine("<!DOCTYPE html>");
                    html.AppendLine("<html>");
                    html.AppendLine("<head>");
                    html.AppendLine("    <meta charset=\"UTF-8\">");
                    html.AppendLine("    <style>");
                    html.AppendLine("        body { font-family: 'Microsoft YaHei', 'SimSun', 'Segoe UI', Arial, sans-serif; font-size: 14px; line-height: 1.8; padding: 30px 40px; margin: 0; background: #ffffff; color: #333333; }");
                    html.AppendLine("        p { margin: 8px 0; padding: 0; }");
                    html.AppendLine("        span { white-space: pre-wrap; }");
                    html.AppendLine("        table { border-collapse: collapse; margin: 10px 0; width: 100%; }");
                    html.AppendLine("        td { border: 1px solid #cccccc; padding: 8px; vertical-align: top; }");
                    html.AppendLine("        th { border: 1px solid #cccccc; padding: 8px; background-color: #f0f0f0; font-weight: bold; vertical-align: top; }");
                    html.AppendLine("    </style>");
                    html.AppendLine("</head>");
                    html.AppendLine("<body>");
                    html.Append(htmlContent.ToString());
                    html.AppendLine("</body>");
                    html.AppendLine("</html>");

                    return html.ToString();
                }
            }
            catch (Exception ex)
            {
                return $"<html><body><p>转换失败: {ex.Message}</p></body></html>";
            }
        }
    }
}


