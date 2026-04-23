using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using YiboFile.Services.Core;
using YiboFile.ViewModels.Previews;
using YiboFile.Services.Preview;

namespace YiboFile.Previews
{
    /// <summary>
    /// 预览工厂 - 根据文件类型创建相应的预览
    /// </summary>
    public static class PreviewFactory
    {
        /// <summary>
        /// 创建文件预览 ViewModel (同步返回实例，异步加载在内部或外部触发)
        /// </summary>
        public static IPreviewViewModel CreateViewModel(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return null;

                var protocolInfo = ProtocolManager.Parse(filePath);
                if (protocolInfo.Type == ProtocolType.Archive && !string.IsNullOrEmpty(protocolInfo.ExtraData))
                    return new ErrorPreviewViewModel { ErrorMessage = "压缩包内文件暂不支持直接预览" };

                if (Directory.Exists(filePath)) return new FolderPreviewViewModel();

                if (!File.Exists(filePath)) return new ErrorPreviewViewModel { ErrorMessage = "文件不存在" };

                var extension = Path.GetExtension(filePath)?.ToLower();
                var fileTypeInfo = FileTypeManager.GetFileTypeInfo(filePath);

                if (fileTypeInfo == null || !fileTypeInfo.CanPreview)
                    return new ErrorPreviewViewModel { ErrorMessage = "暂不支持此文件类型的预览" };

                switch (fileTypeInfo.PreviewType)
                {
                    case PreviewType.Image: return new ImagePreviewViewModel();
                    case PreviewType.Text:
                        if (extension == ".html" || extension == ".htm" || extension == ".xhtml") return new HtmlPreviewViewModel();
                        if (extension == ".md" || extension == ".markdown") return new MarkdownPreviewViewModel();
                        return new TextPreviewViewModel();
                    case PreviewType.Video:
                    case PreviewType.Audio: return new MediaPreviewViewModel { IsVideo = fileTypeInfo.PreviewType == PreviewType.Video };
                    case PreviewType.Archive: return new ArchivePreviewViewModel();
                    case PreviewType.Document:
                        if (extension == ".pdf") return new PdfPreviewViewModel();
                        if (extension == ".xls" || extension == ".xlsx" || extension == ".xlsm") return new ExcelPreviewViewModel();
                        if (extension == ".dwg" || extension == ".dxf") return new CadPreviewViewModel();
                        if (extension == ".ppt" || extension == ".pptx" || extension == ".pptm") return new PowerPointPreviewViewModel();
                        if (extension == ".doc" || extension == ".docx" || extension == ".docm" || extension == ".rtf") return new WordPreviewViewModel();
                        if (extension == ".chm") return new ChmPreviewViewModel();
                        return new WordPreviewViewModel();
                    default: return new ErrorPreviewViewModel { ErrorMessage = "无法为该文件创建预览" };
                }
            }
            catch (Exception ex)
            {
                return new ErrorPreviewViewModel { ErrorMessage = $"预览创建失败: {ex.Message}" };
            }
        }
    }
}
