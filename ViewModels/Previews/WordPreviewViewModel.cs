using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using YiboFile.ViewModels;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class WordPreviewViewModel : BasePreviewViewModel
    {
        private string _htmlContent;
        public string HtmlContent
        {
            get => _htmlContent;
            set => SetProperty(ref _htmlContent, value);
        }

        private bool _needsConversion;
        public bool NeedsConversion
        {
            get => _needsConversion;
            set => SetProperty(ref _needsConversion, value);
        }

        private string _conversionMessage;
        public string ConversionMessage
        {
            get => _conversionMessage;
            set => SetProperty(ref _conversionMessage, value);
        }

        private bool _isConverting;
        public bool IsConverting
        {
            get => _isConverting;
            set => SetProperty(ref _isConverting, value);
        }

        public ICommand ConvertCommand { get; }
        public ICommand ReloadCommand { get; }

        public WordPreviewViewModel()
        {
            ConvertCommand = new RelayCommand(async () => await ConvertToDocxAsync());
            ReloadCommand = new RelayCommand(async () => await LoadAsync(FilePath));
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public async Task LoadAsync(string filePath, System.Threading.CancellationToken token = default)
        {
            if (token.IsCancellationRequested) return;
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "📝";
            IsLoading = true;
            NeedsConversion = false;
            ConversionMessage = null;

            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                // Explicitly check for OpenXML formats that we can handle natively
                if (extension == ".docx" || extension == ".docm" || extension == ".dotx" || extension == ".dotm")
                {
                    await HandleDocxFile(filePath);
                }
                else if (extension == ".doc" || extension == ".rtf" || extension == ".odt")
                {
                    // For legacy formats (.doc, .rtf) or others (.odt), try using Word COM
                    await HandleDocFile(filePath);
                }
                else
                {
                    HtmlContent = "<html><body style='font-family:Segoe UI;padding:20px;color:#666'>不支持的文件格式</body></html>";
                }
            }
            catch (Exception ex)
            {
                HtmlContent = $"<html><body style='font-family:Segoe UI;color:#c00;padding:20px'>加载失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task HandleDocFile(string filePath)
        {
            // For .doc, we need to convert to .docx first using Word COM
            string tempDocx = Path.Combine(Path.GetTempPath(), $"word_preview_{Guid.NewGuid()}.docx");

            string error = null;
            bool success = await Task.Run(() => ConvertDocToDocx(filePath, tempDocx, out error));

            if (success)
            {
                await HandleDocxFile(tempDocx);
                // Clean up temp file
                try { File.Delete(tempDocx); } catch { }
            }
            else
            {
                NeedsConversion = true;
                ConversionMessage = error ?? "该文件格式需要 Microsoft Word 才能预览。";
                HtmlContent = "<html><body style='background:#f5f5f5;display:flex;align-items:center;justify-content:center;height:100vh;margin:0'><div style='text-align:center;color:#666'><h3>需要转换格式</h3><p>此格式需要安装 Microsoft Word 才能预览</p></div></body></html>";
            }
        }

        private async Task HandleDocxFile(string filePath)
        {
            string html = await Task.Run(() => GenerateHtmlFromDocx(filePath));
            HtmlContent = html;
        }

        private async Task ConvertToDocxAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsConverting = true;
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                string baseName = Path.GetFileNameWithoutExtension(FilePath);
                string outputPath = Path.Combine(directory, baseName + ".docx");

                // Ensure unique name
                int counter = 1;
                while (File.Exists(outputPath))
                {
                    outputPath = Path.Combine(directory, $"{baseName}({counter++}).docx");
                }

                string error = null;
                bool success = await Task.Run(() => ConvertDocToDocx(FilePath, outputPath, out error));
                if (success)
                {
                    // Refresh current view
                    await LoadAsync(outputPath);
                    // Trigger file list refresh if possible
                    var messageBus = YiboFile.App.ServiceProvider?.GetService(typeof(YiboFile.ViewModels.Messaging.IMessageBus)) as YiboFile.ViewModels.Messaging.IMessageBus;
                    messageBus?.Publish(new YiboFile.ViewModels.Messaging.Messages.RefreshFileListMessage());
                }
                else
                {
                    ConversionMessage = error ?? "转换失败";
                }
            }
            catch (Exception ex)
            {
                ConversionMessage = $"转换过程中出错: {ex.Message}";
            }
            finally
            {
                IsConverting = false;
            }
        }

        private bool ConvertDocToDocx(string docPath, string docxPath, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                Type wordType = Type.GetTypeFromProgID("Word.Application");
                if (wordType == null)
                {
                    errorMessage = "未检测到 Microsoft Word。预览此文件格式需要安装 Microsoft Word。";
                    return false;
                }

                dynamic wordApp = Activator.CreateInstance(wordType);
                try
                {
                    try { wordApp.Visible = false; } catch { }
                    wordApp.DisplayAlerts = 0; // wdAlertsNone
                    // Use ConfirmConversions: false to avoid dialogs
                    dynamic document = wordApp.Documents.Open(FileName: docPath, ConfirmConversions: false, ReadOnly: true, AddToRecentFiles: false, Visible: false);
                    document.SaveAs2(docxPath, 12); // wdFormatXMLDocument = 12
                    document.Close(false);
                    return true;
                }
                finally
                {
                    try { wordApp.Quit(false); } catch { }
                    try { Marshal.ReleaseComObject(wordApp); } catch { }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"转换失败: {ex.Message}";
                return false;
            }
        }

        // --- Logic from DocxPreviewHandler ---
        private string GenerateHtmlFromDocx(string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
                sb.Append("<style>body { font-family: 'Segoe UI', sans-serif; padding: 40px; line-height: 1.6; max-width: 800px; margin: 0 auto; } p { margin: 1em 0; } img { max-width: 100%; height: auto; display: block; margin: 1em auto; } table { border-collapse: collapse; width: 100%; margin: 1em 0; } td, th { border: 1px solid #ddd; padding: 8px; }</style></head><body>");

                // Use FileStream with Share ReadWrite to avoid locking issues
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var wordDoc = WordprocessingDocument.Open(fs, false))
                {
                    var mainPart = wordDoc.MainDocumentPart;
                    if (mainPart == null || mainPart.Document == null) return "<html><body><p>文档内容为空</p></body></html>";

                    var body = mainPart.Document.Body;
                    var imageMap = ExtractImages(mainPart);

                    foreach (var element in body.Elements())
                    {
                        ProcessElement(element, sb, imageMap, mainPart);
                    }
                }

                sb.Append("</body></html>");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"<html><body><p style='color:red'>预览渲染失败: {WebUtility.HtmlEncode(ex.Message)}</p></body></html>";
            }
        }

        private Dictionary<string, string> ExtractImages(MainDocumentPart mainPart)
        {
            var map = new Dictionary<string, string>();
            foreach (var part in mainPart.ImageParts)
            {
                try
                {
                    string rId = mainPart.GetIdOfPart(part);
                    using var stream = part.GetStream();
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    string base64 = Convert.ToBase64String(ms.ToArray());
                    string mimeType = "image/png";
                    if (part.Uri.ToString().EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)) mimeType = "image/jpeg";
                    map[rId] = $"data:{mimeType};base64,{base64}";
                }
                catch { }
            }
            return map;
        }

        private void ProcessElement(OpenXmlElement element, StringBuilder sb, Dictionary<string, string> imageMap, MainDocumentPart mainPart)
        {
            if (element is Paragraph p)
            {
                sb.Append("<p>");
                foreach (var run in p.Elements<Run>())
                {
                    foreach (var text in run.Elements<Text>()) sb.Append(WebUtility.HtmlEncode(text.Text));
                    foreach (var drawing in run.Elements<Drawing>())
                    {
                        try
                        {
                            var blip = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Blip>().FirstOrDefault();
                            if (blip != null && imageMap.TryGetValue(blip.Embed?.Value ?? "", out string data))
                                sb.Append($"<img src=\"{data}\" />");
                        }
                        catch { }
                    }
                }
                sb.Append("</p>");
            }
            else if (element is Table t)
            {
                sb.Append("<table>");
                foreach (var row in t.Elements<TableRow>())
                {
                    sb.Append("<tr>");
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        sb.Append("<td>");
                        foreach (var cp in cell.Elements<Paragraph>()) sb.Append(WebUtility.HtmlEncode(cp.InnerText));
                        sb.Append("</td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</table>");
            }
        }
    }
}
