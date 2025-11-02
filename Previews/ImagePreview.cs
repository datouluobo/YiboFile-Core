using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 图片文件预览
    /// </summary>
    public class ImagePreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                // 使用绝对路径URI
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 800; // 提高显示质量
                bitmap.EndInit();

                var image = new Image 
                { 
                    Source = bitmap, 
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                // 添加ScrollViewer以支持大图片
                var scrollViewer = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = image
                };
                
                return scrollViewer;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载图片: {ex.Message}");
            }
        }
    }
}

