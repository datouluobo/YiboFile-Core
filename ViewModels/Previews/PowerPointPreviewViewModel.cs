using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Drawing;
using YiboFile.Previews;
using YiboFile.Services;

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

        private bool _hasPowerPointInstalled;
        public bool HasPowerPointInstalled
        {
            get => _hasPowerPointInstalled;
            set => SetProperty(ref _hasPowerPointInstalled, value);
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

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public RelayCommand ConvertCommand { get; }
        public RelayCommand ReloadCommand { get; }

        public event EventHandler ReloadRequested;

        public PowerPointPreviewViewModel()
        {
            ConvertCommand = new RelayCommand(async () => await ConvertPptToPptxAsync());
            ReloadCommand = new RelayCommand(async () => await LoadAsync(FilePath));
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
            Icon = "📊";
        }

        public async Task LoadAsync(string filePath)
        {
            FilePath = filePath;
            Title = System.IO.Path.GetFileName(filePath);
            IsLoading = true;
            IsLegacyFormat = false;
            StatusText = "正在加载 PowerPoint 文档...";

            try
            {
                var ext = System.IO.Path.GetExtension(filePath).ToLower();
                if (ext == ".ppt")
                {
                    IsLegacyFormat = true;
                    HasPowerPointInstalled = IsPowerPointInstalled();
                    IsLoading = false;
                    return;
                }

                if (ext == ".pptx")
                {
                    await Task.Run(() =>
                    {
                        var html = GenerateHtmlFromPptx(filePath);
                        HtmlContent = html;
                    });
                    ReloadRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                HtmlContent = $"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'><h3>预览失败</h3><p>{WebUtility.HtmlEncode(ex.Message)}</p></body></html>";
                ReloadRequested?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string GenerateHtmlFromPptx(string filePath)
        {
            try
            {
                using var presentationDoc = PresentationDocument.Open(filePath, false);
                if (presentationDoc?.PresentationPart?.Presentation == null)
                    throw new Exception("无法读取PPTX文档结构");

                var presentationPart = presentationDoc.PresentationPart;
                var slideIdList = presentationPart.Presentation.SlideIdList;
                if (slideIdList == null) throw new Exception("未找到幻灯片列表");

                var sb = new StringBuilder();
                sb.Append("<html><head><meta charset='utf-8'><style>");
                sb.Append("body{font-family:'Microsoft YaHei',Segoe UI,Arial,sans-serif;margin:0;padding:20px;background:#f5f5f5}");
                sb.Append(".header{background:linear-gradient(135deg,#667eea 0%,#764ba2 100%);color:white;padding:20px;border-radius:8px;margin-bottom:20px}");
                sb.Append(".slide{background:white;margin:20px 0;padding:30px;border-radius:8px;box-shadow:0 2px 8px rgba(0,0,0,0.1);min-height:400px}");
                sb.Append(".slide-number{color:#667eea;font-weight:bold;font-size:14px;margin-bottom:15px;border-bottom:2px solid #667eea;padding-bottom:8px}");
                sb.Append(".slide-content{line-height:1.8;color:#333}");
                sb.Append(".slide-image{text-align:center;margin:15px 0} .slide-image img{max-width:90%;max-height:400px;border-radius:6px;box-shadow:0 4px 8px rgba(0,0,0,0.15)}");
                sb.Append("</style></head><body>");

                var slideIds = slideIdList.Elements<SlideId>().ToList();
                sb.Append($"<div class='header'><h1 style='margin:0'>📊 PowerPoint 演示文稿</h1><p style='margin:5px 0 0 0'>共 {slideIds.Count} 张幻灯片</p></div>");

                for (int i = 0; i < slideIds.Count; i++)
                {
                    var slideId = slideIds[i];
                    var slidePart = presentationPart.GetPartById(slideId.RelationshipId) as SlidePart;

                    sb.Append($"<div class='slide'>");
                    sb.Append($"<div class='slide-number'>幻灯片 {i + 1} / {slideIds.Count}</div>");
                    sb.Append("<div class='slide-content'>");

                    if (slidePart?.Slide != null)
                    {
                        var imageMap = ExtractImagesFromSlidePart(slidePart);
                        ProcessSlideShapes(slidePart.Slide, sb, imageMap);
                    }
                    else
                    {
                        sb.Append("（解析错误或幻灯片内容为空）");
                    }

                    sb.Append("</div></div>");
                }

                sb.Append("</body></html>");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"<html><body style='font-family:Segoe UI;color:#c00;padding:16px'>解析失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>";
            }
        }

        private Dictionary<string, string> ExtractImagesFromSlidePart(SlidePart slidePart)
        {
            var imageMap = new Dictionary<string, string>();
            if (slidePart == null) return imageMap;

            foreach (var imagePart in slidePart.ImageParts)
            {
                try
                {
                    string rId = slidePart.GetIdOfPart(imagePart);
                    using var stream = imagePart.GetStream();
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    byte[] bytes = ms.ToArray();
                    string mime = "image/png";
                    var ext = System.IO.Path.GetExtension(imagePart.Uri.ToString()).ToLower();
                    if (ext == ".jpg" || ext == ".jpeg") mime = "image/jpeg";
                    else if (ext == ".gif") mime = "image/gif";

                    imageMap[rId] = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                }
                catch { }
            }
            return imageMap;
        }

        private void ProcessSlideShapes(Slide slide, StringBuilder sb, Dictionary<string, string> imageMap)
        {
            var shapeTree = slide.CommonSlideData?.ShapeTree;
            if (shapeTree == null) return;

            foreach (var element in shapeTree.Elements())
            {
                if (element is DocumentFormat.OpenXml.Presentation.Shape shape)
                {
                    if (shape.TextBody != null)
                    {
                        foreach (var para in shape.TextBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>())
                        {
                            var text = string.Join("", para.Elements<DocumentFormat.OpenXml.Drawing.Run>().Select(r => r.Text?.Text ?? ""));
                            if (!string.IsNullOrWhiteSpace(text)) sb.Append($"<p>{WebUtility.HtmlEncode(text)}</p>");
                        }
                    }
                }
                else if (element is DocumentFormat.OpenXml.Presentation.Picture pic)
                {
                    var rId = pic.BlipFill?.Blip?.Embed?.Value;
                    if (rId != null && imageMap.TryGetValue(rId, out string data))
                    {
                        sb.Append($"<div class='slide-image'><img src=\"{data}\" /></div>");
                    }
                }
            }
        }

        private bool IsPowerPointInstalled()
        {
            try { return Type.GetTypeFromProgID("PowerPoint.Application") != null; }
            catch { return false; }
        }

        private async Task ConvertPptToPptxAsync()
        {
            if (IsConverting) return;
            IsConverting = true;
            ConvertStatusText = "⏳ 转换中...";

            try
            {
                await Task.Run(() =>
                {
                    string outPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(FilePath), System.IO.Path.GetFileNameWithoutExtension(FilePath) + ".pptx");
                    outPath = ExcelParser.GetUniqueFilePath(outPath);

                    Type pptType = Type.GetTypeFromProgID("PowerPoint.Application");
                    dynamic pptApp = Activator.CreateInstance(pptType);
                    try
                    {
                        pptApp.DisplayAlerts = 0;
                        dynamic pres = pptApp.Presentations.Open(FilePath, ReadOnly: true, WithWindow: false);
                        pres.SaveAs(outPath, 24); // ppSaveAsOpenXMLPresentation
                        pres.Close();

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ConvertStatusText = "✅ 转换成功";
                            Services.Core.NotificationService.ShowSuccess($"转换成功: {outPath}");
                        });
                    }
                    finally
                    {
                        pptApp.Quit();
                        System.Runtime.InteropServices.Marshal.ReleaseComObject(pptApp);
                    }
                });
            }
            catch (Exception ex)
            {
                ConvertStatusText = "🔄 转换为PPTX格式";
                Services.Core.NotificationService.ShowError($"转换失败: {ex.Message}");
            }
            finally
            {
                IsConverting = false;
            }
        }
    }
}
