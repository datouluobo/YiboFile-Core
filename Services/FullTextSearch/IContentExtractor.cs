using System;

namespace YiboFile.Services.FullTextSearch
{
    /// <summary>
    /// 文件内容提取器接口
    /// </summary>
    public interface IContentExtractor
    {
        /// <summary>
        /// 判断是否支持指定扩展名
        /// </summary>
        bool CanExtract(string extension);

        /// <summary>
        /// 提取文件文本内容
        /// </summary>
        string ExtractText(string filePath);

        /// <summary>
        /// 支持的文件扩展名列表
        /// </summary>
        string[] SupportedExtensions { get; }
    }
}

