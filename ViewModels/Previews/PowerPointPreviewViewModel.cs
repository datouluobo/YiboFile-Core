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
using YiboFile.Previews;
using YiboFile.ViewModels;

namespace YiboFile.ViewModels.Previews
{
    public class PowerPointPreviewViewModel : BasePreviewViewModel
    {
        private string _htmlContent;
        public string HtmlContent
        {
            get => _htmlContent;
            set => SetProperty(ref _htmlContent, value);
        }

        private bool _isLegacyFormat;
        public bool IsLegacyFormat
        {
            get => _isLegacyFormat;
            set => SetProperty(ref _isLegacyFormat, value);
        }

        private bool _isConverting;
        public bool IsConverting
        {
            get => _isConverting;
            set => SetProperty(ref _isConverting, value);
        }

        private string _convertStatusText = "🔄 转换为PPTX格式";
        public string ConvertStatusText
        {
            get => _convertStatusText;
            set => SetProperty(ref _convertStatusText, value);
        }

        public ICommand ConvertCommand { get; }
        public ICommand ReloadCommand { get; }
        public event EventHandler ReloadRequested;

        public PowerPointPreviewViewModel()
        {
            ConvertCommand = new RelayCommand(async () => await ConvertToPptxAsync());
            ReloadCommand = new RelayCommand(async () => await LoadAsync(FilePath));
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
            Icon = "📊";
        }

        public async Task LoadAsync(string filePath, System.Threading.CancellationToken token = default)
        {
            if (token.IsCancellationRequested) return;
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            IsLoading = true;
            IsLegacyFormat = false;

            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                if (extension == ".pptx" || extension == ".pptm" || extension == ".potx" || extension == ".potm")
                {
                    await HandlePptxFile(filePath);
                }
                else if (extension == ".ppt" || extension == ".pps" || extension == ".pot")
                {
                    IsLegacyFormat = true;
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

        private async Task HandlePptxFile(string filePath)
        {
            // Placeholder: PowerPoint parsing logic would generate slide images or thumbnails and wrap them in HTML
            // Currently using generic logic, this could be expanded to use OpenXml or Spire.Presentation
            string html = await Task.Run(() => GenerateHtmlFromPptx(filePath));
            HtmlContent = html;
        }

        private string GenerateHtmlFromPptx(string filePath)
        {
            string themeCss = GetThemeCss();
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.Append("<style>");
            sb.Append("html, body { margin: 0; padding: 0; background: transparent; font-family: 'Segoe UI', sans-serif; height: 100%; display: flex; align-items: center; justify-content: center; overflow: hidden; } ");
            sb.Append(".placeholder { text-align: center; color: #666; padding: 40px; } ");
            sb.Append(themeCss);
            sb.Append("</style></head><body>");
            sb.Append("<div class='placeholder'><h3>PPTX 预览</h3><p>此版本支持通过 Office 转换引擎进行显示控制</p></div>");
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private async Task ConvertToPptxAsync()
        {
            if (string.IsNullOrEmpty(FilePath)) return;

            IsConverting = true;
            ConvertStatusText = "⏳ 转换中...";

            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                string baseName = Path.GetFileNameWithoutExtension(FilePath);
                string outputPath = Path.Combine(directory, baseName + ".pptx");

                // Ensure unique name
                int counter = 1;
                while (File.Exists(outputPath))
                {
                    outputPath = Path.Combine(directory, $"{baseName}({counter++}).pptx");
                }

                string error = null;
                bool success = await Task.Run(() => ConvertPptToPptx(FilePath, outputPath, out error));
                if (success)
                {
                    ConvertStatusText = "✅ 转换成功";
                    await LoadAsync(outputPath);
                    // Notify list refresh
                    var messageBus = YiboFile.App.ServiceProvider?.GetService(typeof(YiboFile.ViewModels.Messaging.IMessageBus)) as YiboFile.ViewModels.Messaging.IMessageBus;
                    messageBus?.Publish(new YiboFile.ViewModels.Messaging.Messages.RefreshFileListMessage());
                }
                else
                {
                    ConvertStatusText = "❌ 转换失败";
                    Services.Core.NotificationService.ShowError(error ?? "无法转换 PPT 文件");
                }
            }
            catch (Exception ex)
            {
                ConvertStatusText = "❌ 转换出错";
                Services.Core.NotificationService.ShowError($"转换过程中出错: {ex.Message}");
            }
            finally
            {
                IsConverting = false;
            }
        }

        private bool ConvertPptToPptx(string pptPath, string pptxPath, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                Type pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                if (pptType == null)
                {
                    errorMessage = "未检测到 Microsoft PowerPoint。需要安装 PowerPoint 才能预览或转换此格式。";
                    return false;
                }

                dynamic pptApp = Activator.CreateInstance(pptType);
                try
                {
                    // Use ReadOnly: true, WithWindow: false
                    dynamic presentation = pptApp.Presentations.Open(pptPath, 1, 0, 0); // 1 = msoTrue (ReadOnly), 0 = msoFalse (WithWindow)
                    presentation.SaveAs(pptxPath, 24); // 24 = ppSaveAsOpenXMLPresentation
                    presentation.Close();
                    return true;
                }
                finally
                {
                    try { pptApp.Quit(); } catch { }
                    try { Marshal.ReleaseComObject(pptApp); } catch { }
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"转换失败: {ex.Message}";
                return false;
            }
        }
    }
}
