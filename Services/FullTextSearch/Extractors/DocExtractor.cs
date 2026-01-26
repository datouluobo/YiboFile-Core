using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YiboFile.Services.FullTextSearch.Extractors
{
    /// <summary>
    /// DOC (旧版 Word) 文件文本提取器
    /// 使用简化的二进制文本提取方法
    /// </summary>
    public class DocExtractor : IContentExtractor
    {
        public string[] SupportedExtensions => new[] { ".doc" };

        public bool CanExtract(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith(".")) ext = "." + ext;
            return ext == ".doc";
        }

        public string ExtractText(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return string.Empty;

                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return ExtractTextFromBinary(fs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DocExtractor] Error extracting {filePath}: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// 从二进制文件中提取可读文本（简化方法）
        /// DOC 文件中的文本通常是 Unicode LE 编码
        /// </summary>
        private string ExtractTextFromBinary(Stream stream)
        {
            try
            {
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);

                var sb = new StringBuilder();
                var textChars = new List<char>();

                // 遍历字节寻找 Unicode LE 文本
                for (int i = 0; i < bytes.Length - 1; i += 2)
                {
                    int charCode = bytes[i] | (bytes[i + 1] << 8);

                    // 只保留可打印字符和常见控制字符
                    if ((charCode >= 0x20 && charCode <= 0x7E) ||  // ASCII 可打印
                        (charCode >= 0x4E00 && charCode <= 0x9FFF) ||  // CJK 常用汉字
                        (charCode >= 0x3000 && charCode <= 0x303F) ||  // CJK 标点
                        charCode == 0x0A || charCode == 0x0D ||  // 换行
                        charCode == 0x09)  // Tab
                    {
                        textChars.Add((char)charCode);
                    }
                    else if (textChars.Count > 3)
                    {
                        var segment = new string(textChars.ToArray()).Trim();
                        if (segment.Length > 3)
                        {
                            sb.AppendLine(segment);
                        }
                        textChars.Clear();
                    }
                }

                // 处理剩余字符
                if (textChars.Count > 3)
                {
                    var segment = new string(textChars.ToArray()).Trim();
                    if (segment.Length > 3)
                    {
                        sb.AppendLine(segment);
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}

