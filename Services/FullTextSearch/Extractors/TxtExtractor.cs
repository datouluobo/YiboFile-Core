using System;
using System.IO;

namespace YiboFile.Services.FullTextSearch.Extractors
{
    /// <summary>
    /// 纯文本文件提取器 (TXT, MD, 代码文件等)
    /// </summary>
    public class TxtExtractor : IContentExtractor
    {
        public string[] SupportedExtensions => new[]
        {
            ".txt", ".md", ".markdown", ".log", ".ini", ".cfg", ".conf",
            ".cs", ".js", ".ts", ".py", ".java", ".c", ".cpp", ".h", ".hpp",
            ".html", ".htm", ".css", ".scss", ".less", ".json", ".xml", ".yaml", ".yml",
            ".sql", ".sh", ".bat", ".ps1", ".cmd", ".vbs",
            ".go", ".rs", ".rb", ".php", ".pl", ".lua", ".swift", ".kt", ".scala"
        };

        public bool CanExtract(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;
            return Array.Exists(SupportedExtensions, e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        public string ExtractText(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return string.Empty;

                // 尝试检测编码
                var bytes = File.ReadAllBytes(filePath);
                if (bytes.Length == 0) return string.Empty;

                // 简单的 UTF-8 BOM 检测
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    return System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
                }

                // 尝试 UTF-8
                try
                {
                    return System.Text.Encoding.UTF8.GetString(bytes);
                }
                catch
                {
                    // 回退到默认编码
                    return System.Text.Encoding.Default.GetString(bytes);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TxtExtractor] Error: {ex.Message}");
                return string.Empty;
            }
        }
    }
}

