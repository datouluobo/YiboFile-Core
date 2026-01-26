using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YiboFile.Services.FullTextSearch.Extractors;

namespace YiboFile.Services.FullTextSearch
{
    /// <summary>
    /// 内容提取器管理器 - 统一管理所有文件格式的文本提取
    /// </summary>
    public class ContentExtractorManager
    {
        private readonly List<IContentExtractor> _extractors;

        public ContentExtractorManager()
        {
            _extractors = new List<IContentExtractor>
            {
                new TxtExtractor(),
                new PdfExtractor(),
                new DocxExtractor(),
                new DocExtractor(),
                new XlsxExtractor(),
                new XlsExtractor()
            };
        }

        /// <summary>
        /// 获取所有支持的扩展名
        /// </summary>
        public IEnumerable<string> SupportedExtensions =>
            _extractors.SelectMany(e => e.SupportedExtensions).Distinct();

        /// <summary>
        /// 判断是否支持指定文件
        /// </summary>
        public bool CanExtract(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return false;
            var ext = Path.GetExtension(filePath);
            return _extractors.Any(e => e.CanExtract(ext));
        }

        /// <summary>
        /// 提取文件文本内容
        /// </summary>
        public string ExtractText(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return string.Empty;

            var ext = Path.GetExtension(filePath);
            var extractor = _extractors.FirstOrDefault(e => e.CanExtract(ext));

            if (extractor == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ContentExtractorManager] No extractor for: {ext}");
                return string.Empty;
            }

            try
            {
                return extractor.ExtractText(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ContentExtractorManager] Error extracting {filePath}: {ex.Message}");
                return string.Empty;
            }
        }
    }
}

