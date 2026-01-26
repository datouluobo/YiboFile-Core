using System.Windows;

namespace YiboFile.Previews
{
    /// <summary>
    /// 预览提供者接口
    /// </summary>
    public interface IPreviewProvider
    {
        /// <summary>
        /// 创建预览UI元素
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>预览UI元素</returns>
        UIElement CreatePreview(string filePath);
    }
}


