using System;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;

namespace YiboFile.Services.FullTextSearch.Extractors
{
    /// <summary>
    /// DOCX 文件文本提取器 (使用 OpenXML)
    /// </summary>
    public class DocxExtractor : IContentExtractor
    {
        public string[] SupportedExtensions => new[] { ".docx" };

        public bool CanExtract(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;
            return ext == ".docx";
        }

        public string ExtractText(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return string.Empty;

                var sb = new StringBuilder();

                using (var wordDoc = WordprocessingDocument.Open(filePath, false))
                {
                    var body = wordDoc.MainDocumentPart?.Document?.Body;
                    if (body != null)
                    {
                        foreach (var element in body.Elements())
                        {
                            var text = element.InnerText;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                sb.AppendLine(text);
                            }
                        }
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocxExtractor] Error extracting {filePath}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}

