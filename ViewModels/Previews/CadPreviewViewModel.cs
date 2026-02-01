using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using YiboFile.Previews;
using YiboFile.Services;

namespace YiboFile.ViewModels.Previews
{
    public class CadPreviewViewModel : BasePreviewViewModel
    {
        private string _htmlContent;
        public string HtmlContent
        {
            get => _htmlContent;
            set => SetProperty(ref _htmlContent, value);
        }

        private System.Windows.Media.ImageSource _imageSource;
        public System.Windows.Media.ImageSource ImageSource
        {
            get => _imageSource;
            set => SetProperty(ref _imageSource, value);
        }

        private bool _isShowingThumbnail;
        public bool IsShowingThumbnail
        {
            get => _isShowingThumbnail;
            set => SetProperty(ref _isShowingThumbnail, value);
        }

        private bool _needsOda;
        public bool NeedsOda
        {
            get => _needsOda;
            set => SetProperty(ref _needsOda, value);
        }

        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _convertStatusText = "🔄 转换为DXF格式";
        public string ConvertStatusText
        {
            get => _convertStatusText;
            set => SetProperty(ref _convertStatusText, value);
        }

        private bool _isDwg;
        public bool IsDwg
        {
            get => _isDwg;
            set => SetProperty(ref _isDwg, value);
        }

        private bool _isConverting;
        public bool IsConverting
        {
            get => _isConverting;
            set => SetProperty(ref _isConverting, value);
        }

        public RelayCommand ConvertCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand LoadVectorCommand { get; }

        public event EventHandler ReloadRequested;

        public CadPreviewViewModel()
        {
            ConvertCommand = new RelayCommand(async () => await ConvertDwgToDxfAsync());
            RefreshCommand = new RelayCommand(async () => await LoadAsync(FilePath));
            LoadVectorCommand = new RelayCommand(async () => await LoadVectorViewAsync());
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
            Icon = "📐";
        }

        public async Task LoadAsync(string filePath)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            IsLoading = true;
            NeedsOda = false;
            IsShowingThumbnail = false;
            StatusText = "正在准备预览...";

            try
            {
                var ext = Path.GetExtension(filePath).ToLower();
                IsDwg = ext == ".dwg";

                if (IsDwg)
                {
                    // Try to extract thumbnail first for fast preview
                    var thumbnail = await Task.Run(() => Services.DwgThumbnailExtractor.ExtractThumbnail(filePath));
                    if (thumbnail != null)
                    {
                        ImageSource = thumbnail;
                        IsShowingThumbnail = true;
                        IsLoading = false;
                        return;
                    }
                }

                await LoadVectorViewAsync();
            }
            catch (Exception ex)
            {
                // Fallback to error view
                HtmlContent = $"<html><body style='font-family:Segoe UI;color:#c00;padding:20px'><h3>预览失败</h3><p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p></body></html>";
                ReloadRequested?.Invoke(this, EventArgs.Empty);
                IsLoading = false;
            }
        }

        private async Task LoadVectorViewAsync()
        {
            IsLoading = true;
            IsShowingThumbnail = false;

            try
            {
                string dxfPath = FilePath;

                if (IsDwg)
                {
                    if (!OdaDownloader.IsInstalled())
                    {
                        NeedsOda = true;
                        IsLoading = false;
                        return;
                    }

                    StatusText = "正在转换 DWG 到 DXF...";
                    dxfPath = await DwgConverter.ConvertToDxfAsync(FilePath);
                }

                StatusText = "正在解析图纸...";
                var svgContent = await Task.Run(() => YiboFile.Rendering.DxfSvgConverter.ConvertToSvg(dxfPath));
                HtmlContent = WrapSvgInHtml(svgContent);
                ReloadRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                HtmlContent = $"<html><body style='font-family:Segoe UI;color:#c00;padding:20px'><h3>解析失败</h3><p>{System.Net.WebUtility.HtmlEncode(ex.Message)}</p></body></html>";
                ReloadRequested?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private string WrapSvgInHtml(string svgContent)
        {
            return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ margin: 0; padding: 0; overflow: hidden; background-color: white; display: flex; justify-content: center; align-items: center; height: 100vh; }}
        #svg-container {{ width: 100%; height: 100%; display: flex; justify-content: center; align-items: center; }}
        svg {{ max-width: 100%; max-height: 100%; }}
    </style>
    <script>
        let scale = 1;
        let panning = false;
        let pointX = 0;
        let pointY = 0;
        let start = {{ x: 0, y: 0 }};
        
        function setTransform() {{
            const container = document.getElementById('svg-container');
            container.style.transform = `translate(${{pointX}}px, ${{pointY}}px) scale(${{scale}})`;
        }}

        document.onmousedown = function (e) {{
            e.preventDefault();
            start = {{ x: e.clientX - pointX, y: e.clientY - pointY }};
            panning = true;
        }}

        document.onmouseup = function (e) {{
            panning = false;
        }}

        document.onmousemove = function (e) {{
            e.preventDefault();
            if (!panning) return;
            pointX = (e.clientX - start.x);
            pointY = (e.clientY - start.y);
            setTransform();
        }}

        document.onwheel = function (e) {{
            e.preventDefault();
            var xs = (e.clientX - pointX) / scale,
                ys = (e.clientY - pointY) / scale,
                delta = (e.wheelDelta ? e.wheelDelta : -e.deltaY);
            (delta > 0) ? (scale *= 1.2) : (scale /= 1.2);
            pointX = e.clientX - xs * scale;
            pointY = e.clientY - ys * scale;
            setTransform();
        }}
    </script>
</head>
<body>
    <div id='svg-container'>
        {svgContent}
    </div>
</body>
</html>";
        }

        private async Task ConvertDwgToDxfAsync()
        {
            if (IsConverting) return;
            IsConverting = true;
            ConvertStatusText = "⏳ 转换中...";

            try
            {
                await Task.Run(async () =>
                {
                    var cachedDxfPath = DwgConverter.GetConvertedDxfPath(FilePath);
                    if (!File.Exists(cachedDxfPath))
                    {
                        cachedDxfPath = await DwgConverter.ConvertToDxfAsync(FilePath);
                    }

                    var sourceDir = Path.GetDirectoryName(FilePath);
                    var baseFileName = Path.GetFileNameWithoutExtension(FilePath);
                    var targetPath = Path.Combine(sourceDir, baseFileName + ".dxf");
                    targetPath = ExcelParser.GetUniqueFilePath(targetPath); // Reuse unique file logic

                    File.Copy(cachedDxfPath, targetPath, false);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ConvertStatusText = "✅ 转换成功";
                        Services.Core.NotificationService.ShowSuccess($"转换成功！已保存到: {targetPath}");
                    });
                });
            }
            catch (Exception ex)
            {
                ConvertStatusText = "🔄 转换为DXF格式";
                Services.Core.NotificationService.ShowError($"转换失败: {ex.Message}");
            }
            finally
            {
                IsConverting = false;
            }
        }
    }
}
