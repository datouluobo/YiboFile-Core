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
                        catch
                        {
                            // 静默处理单页提取失败，继续处理其他页面
                        }
                    }
                }

                return sb.ToString();
            }
            catch
            {
                // 静默处理：加密PDF、格式不兼容等情况是预期的，不需要输出错误信息
                return string.Empty;
            }
        }
    }
}

