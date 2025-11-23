using System;
using System.IO;
using System.Windows;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 预览工厂 - 根据文件类型创建相应的预览
    /// </summary>
    public static class PreviewFactory
    {
        /// <summary>
        /// 文件列表刷新请求回调
        /// </summary>
        public static Action OnFileListRefreshRequested { get; set; }
        
        /// <summary>
        /// 在新标签页中打开文件夹回调
        /// </summary>
        public static Action<string> OnOpenFolderInNewTab { get; set; }

        /// <summary>
        /// 创建文件预览
        /// </summary>
        public static UIElement CreatePreview(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) && !Directory.Exists(filePath))
                {
                    return PreviewHelper.CreateErrorPreview("文件不存在");
                }

                // 处理文件夹
                if (Directory.Exists(filePath))
                {
                    return new FolderPreview().CreatePreview(filePath);
                }

                var extension = Path.GetExtension(filePath)?.ToLower();
                
                if (string.IsNullOrEmpty(extension))
                {
                    return PreviewHelper.CreateNoPreview(filePath);
                }
                
                // 特殊处理快捷方式文件
                if (extension == ".lnk")
                {
                    return new LnkPreview().CreatePreview(filePath);
                }
                
                // 注意：GetFileTypeInfo需要完整的文件路径，它会内部处理扩展名
                var fileTypeInfo = FileTypeManager.GetFileTypeInfo(filePath);

                if (fileTypeInfo == null || !fileTypeInfo.CanPreview)
                {
                    return PreviewHelper.CreateNoPreview(filePath);
                }

                // 根据文件类型选择预览提供者
                IPreviewProvider provider = fileTypeInfo.PreviewType switch
                {
                    PreviewType.Image => new ImagePreview(),
                    PreviewType.Text => new TextPreview(),
                    PreviewType.Video => new VideoPreview(),
                    PreviewType.Audio => new AudioPreview(),
                    PreviewType.Archive => new ArchivePreview(),
                    PreviewType.Document => GetDocumentProvider(extension),
                    PreviewType.Html => extension == ".xml" ? new XmlPreview() : new HtmlPreview(),
                    _ => null
                };

                if (provider != null)
                {
                    return provider.CreatePreview(filePath);
                }

                return PreviewHelper.CreateNoPreview(filePath);
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"预览失败: {ex.Message}");
            }
        }

        private static IPreviewProvider GetDocumentProvider(string extension)
        {
            return extension switch
            {
                ".docx" => new DocumentPreview(),
                ".doc" => new DocumentPreview(),
                ".pdf" => new PdfPreview(),
                ".rtf" => new DocumentPreview(),
                ".chm" => new DocumentPreview(),
                ".xlsx" => new ExcelPreview(),
                ".xlsm" => new ExcelPreview(),
                ".xls" => new ExcelPreview(),
                ".pptx" => new PowerPointPreview(),
                ".ppt" => new PowerPointPreview(),
                ".pptm" => new PowerPointPreview(),
                _ => null
            };
        }
    }
}
