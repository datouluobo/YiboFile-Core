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
        /// 文件列表刷新请求回调
        /// </summary>
        public static Action OnFileListRefreshRequested { get; set; }

        /// <summary>
        /// 在新标签页中打开文件夹回调
        /// </summary>
        public static Action<string> OnOpenFolderInNewTab { get; set; }

        /// <summary>
        /// 创建文件预览 ViewModel
        /// </summary>
        public static async System.Threading.Tasks.Task<IPreviewViewModel> CreateViewModelAsync(string filePath, System.Threading.CancellationToken token = default)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    return null;
                }

                if (token.IsCancellationRequested) return null;

                var protocolInfo = ProtocolManager.Parse(filePath);
                if (protocolInfo.Type == ProtocolType.Archive && !string.IsNullOrEmpty(protocolInfo.ExtraData))
                {
                    return new ErrorPreviewViewModel { ErrorMessage = "压缩包内文件暂不支持直接预览" };
                }

                if (Directory.Exists(filePath))
                {
                    var vm = new FolderPreviewViewModel();
                    await vm.LoadAsync(filePath, token);
                    return vm;
                }

                if (!File.Exists(filePath))
                {
                    return new ErrorPreviewViewModel { ErrorMessage = "文件不存在" };
                }

                var extension = Path.GetExtension(filePath)?.ToLower();
                var fileTypeInfo = FileTypeManager.GetFileTypeInfo(filePath);

                if (fileTypeInfo == null || !fileTypeInfo.CanPreview)
                {
                    return new ErrorPreviewViewModel { ErrorMessage = "暂不支持此文件类型的预览" };
                }

                if (token.IsCancellationRequested) return null;

                IPreviewViewModel previewVm = null;

                switch (fileTypeInfo.PreviewType)
                {
                    case PreviewType.Image:
                        var ivm = new ImagePreviewViewModel();
                        await ivm.LoadAsync(filePath, token);
                        previewVm = ivm;
                        break;
                    case PreviewType.Text:
                        if (extension == ".html" || extension == ".htm" || extension == ".xhtml")
                        {
                            var hvm = new HtmlPreviewViewModel();
                            await hvm.LoadAsync(filePath, token);
                            previewVm = hvm;
                        }
                        else if (extension == ".md" || extension == ".markdown")
                        {
                            var mdvm = new MarkdownPreviewViewModel();
                            await mdvm.LoadAsync(filePath, token);
                            previewVm = mdvm;
                        }
                        else
                        {
                            var tvm = new TextPreviewViewModel();
                            await tvm.LoadAsync(filePath, token);
                            previewVm = tvm;
                        }
                        break;
                    case PreviewType.Video:
                    case PreviewType.Audio:
                        var mvm = new MediaPreviewViewModel();
                        mvm.IsVideo = fileTypeInfo.PreviewType == PreviewType.Video;
                        await mvm.LoadAsync(filePath, token);
                        previewVm = mvm;
                        break;
                    case PreviewType.Archive:
                        var avm = new ArchivePreviewViewModel();
                        await avm.LoadAsync(filePath, token);
                        previewVm = avm;
                        break;
                    case PreviewType.Document:
                        if (extension == ".pdf")
                        {
                            var pvm = new PdfPreviewViewModel();
                            await pvm.LoadAsync(filePath, token);
                            previewVm = pvm;
                        }
                        else if (extension == ".xls" || extension == ".xlsx" || extension == ".xlsm")
                        {
                            var evm = new ExcelPreviewViewModel();
                            await evm.LoadAsync(filePath, token);
                            previewVm = evm;
                        }
                        else if (extension == ".dwg" || extension == ".dxf")
                        {
                            var cvm = new CadPreviewViewModel();
                            await cvm.LoadAsync(filePath, token);
                            previewVm = cvm;
                        }
                        else if (extension == ".ppt" || extension == ".pptx" || extension == ".pptm")
                        {
                            var pvm = new PowerPointPreviewViewModel();
                            await pvm.LoadAsync(filePath, token);
                            previewVm = pvm;
                        }
                        else if (extension == ".doc" || extension == ".docx" || extension == ".docm" || extension == ".rtf")
                        {
                            var wvm = new WordPreviewViewModel();
                            await wvm.LoadAsync(filePath, token);
                            previewVm = wvm;
                        }
                        else if (extension == ".chm")
                        {
                            var cvm = new ChmPreviewViewModel();
                            await cvm.LoadAsync(filePath, token);
                            previewVm = cvm;
                        }
                        else
                        {
                            // Fallback to generic document viewmodel or Word if appropriate
                            var dvm = new WordPreviewViewModel();
                            await dvm.LoadAsync(filePath, token);
                            previewVm = dvm;
                        }
                        break;
                    case PreviewType.Shortcut:
                        // Fallback for shortcuts
                        break;
                }

                return previewVm ?? new ErrorPreviewViewModel { ErrorMessage = "无法为该文件创建预览" };
            }
            catch (Exception ex)
            {
                return new ErrorPreviewViewModel { ErrorMessage = $"预览创建失败: {ex.Message}" };
            }
        }

    }
}

