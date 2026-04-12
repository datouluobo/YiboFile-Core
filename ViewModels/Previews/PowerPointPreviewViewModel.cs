using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YiboFile.Previews;
using YiboFile.ViewModels;
using YiboFile.Services.Core;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml;

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
            ReloadCommand = new RelayCommand(async () => {
                await LoadAsync(FilePath);
                ReloadRequested?.Invoke(this, EventArgs.Empty);
            });
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
            Icon = "📊";
        }

        public override async Task LoadAsync(string filePath, System.Threading.CancellationToken token = default)
        {
            FileLogger.Log($"[PPT-DEBUG] LoadAsync 开始, filePath={filePath}, ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}, IsBackground={System.Threading.Thread.CurrentThread.IsBackground}");
            if (token.IsCancellationRequested)
            {
                FileLogger.Log("[PPT-DEBUG] LoadAsync 已取消(入口处)");
                return;
            }
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            IsLoading = true;
            IsLegacyFormat = false;
            HtmlContent = "<html><body style='display:flex;align-items:center;justify-content:center;height:100vh;font-family:Segoe UI;color:#666'><div>正在准备预览...</div></body></html>";

            try
            {
                var extension = Path.GetExtension(filePath)?.ToLower();
                FileLogger.Log($"[PPT-DEBUG] 扩展名={extension}");
                bool isModern = extension == ".pptx" || extension == ".pptm" || extension == ".potx" || extension == ".potm";

                if (isModern)
                {
                    FileLogger.Log("[PPT-DEBUG] 进入 HandleModernPptx");
                    await HandleModernPptx(filePath, token);
                    FileLogger.Log("[PPT-DEBUG] HandleModernPptx 完成");
                }
                else if (extension == ".ppt" || extension == ".pps" || extension == ".pot")
                {
                    FileLogger.Log("[PPT-DEBUG] 进入 HandleLegacyPpt");
                    await HandleLegacyPpt(filePath, token);
                }
                else
                {
                    FileLogger.Log($"[PPT-DEBUG] 不支持的扩展名: {extension}");
                    HtmlContent = "<html><body style='font-family:Segoe UI;padding:20px;color:#666'>不支持的文件格式</body></html>";
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException("[PPT-DEBUG] PowerPoint Load Error", ex);
                HtmlContent = $"<html><body style='font-family:Segoe UI;color:#c00;padding:20px'>加载失败: {WebUtility.HtmlEncode(ex.Message)}</body></html>";
            }
            finally
            {
                FileLogger.Log($"[PPT-DEBUG] LoadAsync 完成, IsLoading->false, HtmlContent长度={HtmlContent?.Length ?? 0}");
                IsLoading = false;
            }
        }

        private async Task HandleModernPptx(string filePath, System.Threading.CancellationToken token)
        {
            FileLogger.Log($"[PPT-DEBUG] HandleModernPptx 开始, ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");

            // 1. Try to extract thumbnail.jpeg from zip as the cover image
            FileLogger.Log("[PPT-DEBUG] 步骤1: 提取缩略图...");
            string thumbnailBase64 = await Task.Run(() => ExtractThumbnailFromPptx(filePath));
            FileLogger.Log($"[PPT-DEBUG] 步骤1完成: thumbnailBase64 is {(thumbnailBase64 == null ? "null" : $"有值(长度:{thumbnailBase64.Length})")}" );

            // 2. Get ThemeCss on UI thread to avoid cross-thread exception
            FileLogger.Log($"[PPT-DEBUG] 步骤2: 获取ThemeCss, 当前ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
            string themeCss;
            try
            {
                themeCss = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => GetThemeCss());
                FileLogger.Log($"[PPT-DEBUG] ThemeCss获取成功, 长度={themeCss?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                FileLogger.LogException("[PPT-DEBUG] GetThemeCss 异常", ex);
                themeCss = "";
            }

            // 3. Parse the entire PPTX using OpenXml
            FileLogger.Log("[PPT-DEBUG] 步骤3: OpenXml解析PPTX...");
            string html = await Task.Run(() => GenerateHtmlFromPptxNative(filePath, themeCss, thumbnailBase64));
            FileLogger.Log($"[PPT-DEBUG] 步骤3完成: html is {(html == null ? "null" : $"有值(长度:{html.Length})")}" );

            if (html != null)
            {
                FileLogger.Log("[PPT-DEBUG] 设置 HtmlContent (OpenXml完整解析结果)");
                HtmlContent = html;
            }
            else
            {
                FileLogger.Log("[PPT-DEBUG] OpenXml解析返回null, 回退到缩略图");
                ShowSinglePreview(thumbnailBase64); // Fallback to just cover
            }
        }

        private async Task HandleLegacyPpt(string filePath, System.Threading.CancellationToken token)
        {
            // For legacy .ppt, we block automatic background processing to prevent
            // PowerPoint UI flashing and hangs.
            HtmlContent = "<html><body style='display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;font-family:Segoe UI;color:#666;text-align:center;padding:20px'><h3>暂不支持自动预览安全模式</h3><p>该文件为旧版 PPT 格式。为了保证系统稳定并阻止 PowerPoint 频繁弹窗，旧版格式已被移出后台自动预览支持列表。</p><p>请点击界面上的“打开”或“转换”按钮操作。</p></body></html>";
        }

        private void ShowSinglePreview(string thumbnailBase64)
        {
            if (!string.IsNullOrEmpty(thumbnailBase64))
            {
                HtmlContent = GenerateHtmlFromSlides(new List<string> { thumbnailBase64 }, GetThemeCss(), false);
            }
            else
            {
                HtmlContent = "<html><body style='display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;font-family:Segoe UI;color:#666;text-align:center;padding:20px'><h3>安全预览受限</h3><p>为了保证系统稳定和避免外部弹窗，当前已禁用后台隐式全量加载。</p><p>且由于该文件格式限制，系统未能提取出快速预览图。<br/><br/>请直接点击右上角“打开”按钮查看完整演示文稿。</p></body></html>";
            }
        }


        private string ExtractThumbnailFromPptx(string filePath)
        {
            try
            {
                FileLogger.Log($"[PPT-DEBUG] ExtractThumbnail 开始, filePath={filePath}");
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    FileLogger.Log($"[PPT-DEBUG] ZIP打开成功, 总Entry数={archive.Entries.Count}");
                    // Log all entries for diagnosis
                    foreach (var e in archive.Entries)
                    {
                        if (e.FullName.StartsWith("docProps", StringComparison.OrdinalIgnoreCase))
                            FileLogger.Log($"[PPT-DEBUG]   docProps下的Entry: {e.FullName} ({e.Length} bytes)");
                    }

                    // Try common thumbnail paths
                    var entries = new[] { "docProps/thumbnail.jpeg", "docProps/thumbnail.png", "docProps/thumbnail.jpg" };
                    foreach (var path in entries)
                    {
                        var entry = archive.GetEntry(path);
                        if (entry == null)
                        {
                            // Try case-insensitive search
                            entry = archive.Entries.FirstOrDefault(e => e.FullName.Equals(path, StringComparison.OrdinalIgnoreCase));
                        }

                        if (entry != null)
                        {
                            FileLogger.Log($"[PPT-DEBUG] 找到缩略图: {entry.FullName}, {entry.Length} bytes");
                            using (var stream = entry.Open())
                            using (var ms = new MemoryStream())
                            {
                                stream.CopyTo(ms);
                                string mime = path.EndsWith(".png") ? "image/png" : "image/jpeg";
                                return $"data:{mime};base64,{Convert.ToBase64String(ms.ToArray())}";
                            }
                        }
                    }
                    FileLogger.Log("[PPT-DEBUG] 未找到缩略图文件");
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException("[PPT-DEBUG] ExtractThumbnail 异常", ex);
            }
            return null;
        }


        private string GenerateHtmlFromPptxNative(string filePath, string themeCss, string thumbnailBase64)
        {
            try
            {
                FileLogger.Log($"[PPT-DEBUG] GenerateHtmlFromPptxNative 开始, filePath={filePath}, themeCss长度={themeCss?.Length ?? 0}");
                var sb = new StringBuilder();
                sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
                sb.Append("<style>");
                sb.Append("body { margin: 0; padding: 20px; font-family: 'Segoe UI', -apple-system, sans-serif; display: flex; flex-direction: column; align-items: center; gap: 20px; background: #f0f0f0; } ");
                sb.Append(".slide { background: white; width: 100%; max-width: 900px; min-height: 120px; padding: 30px 40px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); border-radius: 4px; box-sizing: border-box; position: relative; } ");
                sb.Append(".slide-label { position: absolute; top: 10px; left: 10px; background: rgba(0,0,0,0.1); color: #666; padding: 4px 8px; font-size: 11px; border-radius: 4px; z-index: 10; } ");
                sb.Append(".shape-group { margin: 8px 0; } ");
                sb.Append(".shape-title { font-size: 22px; font-weight: 700; margin: 4px 0; line-height: 1.4; color: #1a1a1a; } ");
                sb.Append(".shape-subtitle { font-size: 16px; color: #666; margin: 2px 0; line-height: 1.5; } ");
                sb.Append(".shape-text p { margin: 2px 0; font-size: 15px; line-height: 1.6; color: #333; } ");
                sb.Append("img { max-width: 100%; height: auto; display: block; margin: 10px auto; border-radius: 4px; } ");
                sb.Append(themeCss);
                sb.Append("[data-theme='dark'] body { background: #1e1e1e; } ");
                sb.Append("[data-theme='dark'] .slide { background: #2d2d2d; box-shadow: 0 4px 12px rgba(0,0,0,0.5); } ");
                sb.Append("[data-theme='dark'] .shape-title { color: #e0e0e0; } ");
                sb.Append("[data-theme='dark'] .shape-subtitle { color: #999; } ");
                sb.Append("[data-theme='dark'] .shape-text p { color: #d4d4d4; } ");
                sb.Append("[data-theme='dark'] .slide-label { background: rgba(255,255,255,0.1); color: #aaa; } ");
                sb.Append("</style></head><body>");

                // Always prioritize rendering the exact visual thumbnail as the cover
                if (!string.IsNullOrEmpty(thumbnailBase64))
                {
                    sb.Append("<div class='slide' style='padding:0; overflow:hidden; min-height: 100px;'>");
                    sb.Append("<div class='slide-label' style='background:rgba(0,0,0,0.6); color:white;'>幻灯片外观快照</div>");
                    sb.Append($"<img src='{thumbnailBase64}' style='margin:0; width:100%; display:block; border-radius:4px;' />");
                    sb.Append("</div>");
                }

                FileLogger.Log("[PPT-DEBUG] 尝试用OpenXml打开文档...");
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var doc = PresentationDocument.Open(fs, false))
                {
                    var presentationPart = doc.PresentationPart;
                    if (presentationPart == null)
                    {
                        FileLogger.Log("[PPT-DEBUG] PresentationPart 为 null");
                        return null;
                    }
                    if (presentationPart.Presentation == null)
                    {
                        FileLogger.Log("[PPT-DEBUG] Presentation 为 null");
                        return null;
                    }
                    if (presentationPart.Presentation.SlideIdList == null)
                    {
                        FileLogger.Log("[PPT-DEBUG] SlideIdList 为 null");
                        return null;
                    }

                    int totalSlides = presentationPart.Presentation.SlideIdList.Elements<SlideId>().Count();
                    FileLogger.Log($"[PPT-DEBUG] 文档打开成功, 幻灯片总数={totalSlides}");

                    int slideIndex = 1;
                    long totalEmbedSize = 0;
                    foreach (SlideId slideId in presentationPart.Presentation.SlideIdList.Elements<SlideId>())
                    {
                        try
                        {
                            var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId);
                            FileLogger.Log($"[PPT-DEBUG] 处理第 {slideIndex} 页幻灯片, rId={slideId.RelationshipId}");
                            
                            sb.Append("<div class='slide'>");
                            sb.Append($"<div class='slide-label'>第 {slideIndex} 页 (内嵌资源)</div>");

                            // Preload images for this slide
                            // 限制：单张图片原始大小 ≤ 500KB，总嵌入 base64 累计 ≤ 8MB
                            const int MaxSingleImageBytes = 500 * 1024;
                            const long MaxTotalEmbedBytes = 8L * 1024 * 1024;
                            var imageMap = new Dictionary<string, string>();
                            int imgCount = 0;
                            int imgSkipped = 0;
                            foreach (var imgPart in slidePart.ImageParts)
                            {
                                try
                                {
                                    string rId = slidePart.GetIdOfPart(imgPart);
                                    using var stream = imgPart.GetStream();
                                    using var ms = new MemoryStream();
                                    stream.CopyTo(ms);
                                    byte[] imgBytes = ms.ToArray();

                                    if (imgBytes.Length > MaxSingleImageBytes)
                                    {
                                        FileLogger.Log($"[PPT-DEBUG] 第{slideIndex}页: 跳过大图片 rId={rId}, size={imgBytes.Length}bytes (>{MaxSingleImageBytes})");
                                        imgSkipped++;
                                        continue;
                                    }

                                    // 计算 base64 近似大小 = ceil(size/3)*4
                                    long base64Len = ((long)imgBytes.Length + 2) / 3 * 4;
                                    if (totalEmbedSize + base64Len > MaxTotalEmbedBytes)
                                    {
                                        FileLogger.Log($"[PPT-DEBUG] 第{slideIndex}页: 总嵌入体积已达上限, 跳过剩余图片");
                                        imgSkipped++;
                                        continue;
                                    }

                                    string mime = imgPart.Uri.ToString().EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : "image/png";
                                    string b64 = Convert.ToBase64String(imgBytes);
                                    imageMap[rId] = $"data:{mime};base64,{b64}";
                                    totalEmbedSize += base64Len;
                                    imgCount++;
                                }
                                catch (Exception imgEx)
                                {
                                    FileLogger.LogException($"[PPT-DEBUG] 第{slideIndex}页图片加载失败", imgEx);
                                }
                            }
                            FileLogger.Log($"[PPT-DEBUG] 第 {slideIndex} 页: 图片数={imgCount}, 跳过={imgSkipped}");

                            // 按Shape级别遍历，保持形状内文本/图片的分组关系
                            if (slidePart.Slide?.CommonSlideData?.ShapeTree != null)
                            {
                                RenderShapeTree(sb, slidePart.Slide.CommonSlideData.ShapeTree, imageMap, slidePart);
                            }
                            else if (slidePart.Slide != null)
                            {
                                FileLogger.Log($"[PPT-DEBUG] 第 {slideIndex} 页: ShapeTree 为 null, 回退到扁平提取");
                                // 回退：扁平提取文本
                                foreach (var p in slidePart.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Paragraph>())
                                {
                                    string text = string.Join("", p.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(t => t.Text));
                                    if (!string.IsNullOrWhiteSpace(text))
                                        sb.Append($"<p>{WebUtility.HtmlEncode(text)}</p>");
                                }
                            }
                            else
                            {
                                FileLogger.Log($"[PPT-DEBUG] 第 {slideIndex} 页: Slide 对象为 null");
                            }
                            
                            sb.Append("</div>");
                            slideIndex++;
                        }
                        catch (Exception slideEx)
                        {
                            FileLogger.LogException($"[PPT-DEBUG] 处理第{slideIndex}页异常", slideEx);
                            slideIndex++;
                        }
                    }
                }

                sb.Append("</body></html>");
                string result = sb.ToString();
                FileLogger.Log($"[PPT-DEBUG] GenerateHtmlFromPptxNative 完成, HTML总长度={result.Length}");
                return result;
            }
            catch (Exception ex)
            {
                FileLogger.LogException("[PPT-DEBUG] GenerateHtmlFromPptxNative 异常", ex);
                return null;
            }
        }

        /// <summary>
        /// 递归遍历ShapeTree的子元素，按Shape/Picture/GroupShape级别分组渲染
        /// </summary>
        private void RenderShapeTree(StringBuilder sb, OpenXmlCompositeElement shapeTree, Dictionary<string, string> imageMap, SlidePart slidePart)
        {
            foreach (var child in shapeTree.ChildElements)
            {
                RenderShapeElement(sb, child, imageMap, slidePart);
            }
        }

        private void RenderShapeElement(StringBuilder sb, OpenXmlElement element, Dictionary<string, string> imageMap, SlidePart slidePart)
        {
            // 1. 文本形状 (Shape / sp)
            if (element is DocumentFormat.OpenXml.Presentation.Shape shape)
            {
                var textBody = shape.TextBody;
                if (textBody == null) return;

                // 判断是否为标题/副标题形状
                bool isTitle = false;
                bool isSubtitle = false;
                var nvSpPr = shape.NonVisualShapeProperties;
                if (nvSpPr?.ApplicationNonVisualDrawingProperties != null)
                {
                    var ph = nvSpPr.ApplicationNonVisualDrawingProperties
                        .GetFirstChild<DocumentFormat.OpenXml.Presentation.PlaceholderShape>();
                    if (ph != null)
                    {
                        var phType = ph.Type?.Value;
                        if (phType == DocumentFormat.OpenXml.Presentation.PlaceholderValues.Title ||
                            phType == DocumentFormat.OpenXml.Presentation.PlaceholderValues.CenteredTitle)
                            isTitle = true;
                        else if (phType == DocumentFormat.OpenXml.Presentation.PlaceholderValues.SubTitle)
                            isSubtitle = true;
                    }
                }

                // 收集所有段落文本
                var paragraphs = textBody.Elements<DocumentFormat.OpenXml.Drawing.Paragraph>().ToList();
                var nonEmptyTexts = new List<string>();
                foreach (var para in paragraphs)
                {
                    string text = string.Join("", para.Descendants<DocumentFormat.OpenXml.Drawing.Text>().Select(t => t.Text));
                    if (!string.IsNullOrWhiteSpace(text))
                        nonEmptyTexts.Add(text);
                }

                if (nonEmptyTexts.Count == 0) return;

                // 输出形状容器
                if (isTitle)
                {
                    foreach (var t in nonEmptyTexts)
                        sb.Append($"<div class='shape-title'>{WebUtility.HtmlEncode(t)}</div>");
                }
                else if (isSubtitle)
                {
                    foreach (var t in nonEmptyTexts)
                        sb.Append($"<div class='shape-subtitle'>{WebUtility.HtmlEncode(t)}</div>");
                }
                else
                {
                    sb.Append("<div class='shape-text'>");
                    foreach (var t in nonEmptyTexts)
                        sb.Append($"<p>{WebUtility.HtmlEncode(t)}</p>");
                    sb.Append("</div>");
                }
            }
            // 2. 图片形状 (Picture / pic)
            else if (element is DocumentFormat.OpenXml.Presentation.Picture picture)
            {
                var blipFill = picture.BlipFill;
                var blip = blipFill?.Blip;
                if (blip?.Embed != null && imageMap.TryGetValue(blip.Embed.Value, out string imgData))
                {
                    sb.Append($"<img src='{imgData}' />");
                }
            }
            // 3. 组合形状 (GroupShape) — 递归处理
            else if (element is DocumentFormat.OpenXml.Presentation.GroupShape groupShape)
            {
                sb.Append("<div class='shape-group'>");
                foreach (var child in groupShape.ChildElements)
                {
                    RenderShapeElement(sb, child, imageMap, slidePart);
                }
                sb.Append("</div>");
            }
            // 4. 其他未知元素跳过
        }


        private string GenerateHtmlFromSlides(List<string> slides, string themeCss = "", bool isThumbnailOnly = false)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.Append("<style>");
            sb.Append("body { margin: 0; padding: 20px; background: transparent; font-family: 'Segoe UI', -apple-system, sans-serif; display: flex; flex-direction: column; align-items: center; gap: 20px; } ");
            sb.Append(".slide-container { background: white; box-shadow: 0 4px 12px rgba(0,0,0,0.15); max-width: 90%; position: relative; border-radius: 4px; overflow: hidden; } ");
            sb.Append(".slide-container img { width: 100%; height: auto; display: block; } ");
            sb.Append(".slide-label { position: absolute; top: 10px; left: 10px; background: rgba(0,0,0,0.6); color: white; padding: 2px 8px; font-size: 11px; border-radius: 4px; pointer-events: none; } ");
            sb.Append(".info-tag { position: fixed; bottom: 20px; right: 20px; background: rgba(0,0,0,0.5); color: #fff; padding: 4px 12px; border-radius: 20px; font-size: 12px; backdrop-filter: blur(5px); } ");
            sb.Append(themeCss);
            sb.Append("[data-theme='dark'] .slide-container { background: #333; } ");
            sb.Append("</style></head><body>");

            if (slides == null || slides.Count == 0)
            {
                sb.Append("<div style='height:80vh; display:flex; flex-direction:column; align-items:center; justify-content:center; color:#666'>");
                sb.Append("<h3>无法生成幻灯片预览</h3><p>请确保已安装 PowerPoint 或查看原文件</p></div>");
            }
            else
            {
                for (int i = 0; i < slides.Count; i++)
                {
                    sb.Append("<div class='slide-container'>");
                    if (slides.Count > 1)
                        sb.Append($"<div class='slide-label'>第 {i + 1} 页</div>");
                    sb.Append($"<img src='{slides[i]}' />");
                    sb.Append("</div>");
                }

                if (isThumbnailOnly)
                {
                    sb.Append("<div class='info-tag'>预览演示文稿首图，完整加载已关闭以提升性能</div>");
                }
            }

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
                bool success = await ConvertPptToPptxSTAAsync(FilePath, outputPath);
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

        private Task<bool> ConvertPptToPptxSTAAsync(string pptPath, string pptxPath)
        {
            var tcs = new TaskCompletionSource<bool>();
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    bool result = ConvertPptToPptx(pptPath, pptxPath, out string _);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            return tcs.Task;
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
                    try { pptApp.WindowState = 2; } catch { } // 2 = ppWindowMinimized
                    try { pptApp.DisplayAlerts = 1; } catch { } // 1 = ppAlertsNone

                    // Use ReadOnly: true (-1), Untitled: false (0), WithWindow: true (-1)
                    dynamic presentation = pptApp.Presentations.Open(pptPath, -1, 0, -1); 
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
