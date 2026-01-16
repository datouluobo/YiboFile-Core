using System.Windows;

namespace YiboFile.Previews.DocumentHandlers
{
    /// <summary>
    /// 文档预览处理器接口
    /// 每种文档格式实现此接口以提供预览功能
    /// </summary>
    public interface IDocumentPreviewHandler
    {
        /// <summary>
        /// 创建文档预览UI
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>预览UI元素</returns>
        UIElement CreatePreview(string filePath);

        /// <summary>
        /// 判断是否可以处理指定的文件扩展名
        /// </summary>
        /// <param name="extension">文件扩展名（含点，如 ".docx"）</param>
        /// <returns>true 表示可以处理</returns>
        bool CanHandle(string extension);
    }
}

