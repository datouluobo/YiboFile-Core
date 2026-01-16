using System;
using System.IO;
using System.Text;
using UglyToad.PdfPig;

namespace YiboFile.Services.FullTextSearch.Extractors
{
    /// <summary>
    /// PDF 文件文本提取器 (使用 PdfPig)
    /// </summary>
    public class PdfExtractor : IContentExtractor
    {
        public string[] SupportedExtensions => new[] { ".pdf" };

        public bool CanExtract(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;
            return ext == ".pdf";
        }

        public string ExtractText(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return string.Empty;

                var sb = new StringBuilder();

                using (var document = PdfDocument.Open(filePath))
                {
                    foreach (var page in document.GetPages())
                    {
                        try
                        {
                            var text = page.Text;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                sb.AppendLine(text);
                            }
                        }
                        catch (Exception)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PdfExtractor] Error extracting page from {filePath}: Error");
                        }
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PdfExtractor] Error extracting {filePath}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}

