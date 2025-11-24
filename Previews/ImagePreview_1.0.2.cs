using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 图片文件预览 - 支持多种图像格式 (v1.0.2)
    /// 支持格式: bmp, jpeg, jpg, png, gif, tif, tiff, ico, svg, psd
    /// </summary>
    public class ImagePreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return PreviewHelper.CreateErrorPreview($"图片文件不存在: {filePath}");
                }
                
                // 确保使用绝对路径
                if (!Path.IsPathRooted(filePath))
                {
                    filePath = Path.GetFullPath(filePath);
                }

                var extension = Path.GetExtension(filePath)?.ToLower();

                // 特殊处理 SVG 格式
                if (extension == ".svg")
                {
                    return CreateSvgPreview(filePath);
                }

                // 特殊处理 PSD 格式
                if (extension == ".psd")
                {
                    return CreatePsdPreview(filePath);
                }

                // 处理其他图像格式（bmp, jpeg, jpg, png, gif, tif, tiff, ico）
                return CreateBitmapPreview(filePath);
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载图片: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建位图预览（bmp, jpeg, jpg, png, gif, tif, tiff, ico）
        /// </summary>
        private UIElement CreateBitmapPreview(string filePath)
        {
            BitmapImage bitmap;
            
            // 优先尝试使用UriSource（性能更好），如果失败则使用StreamSource
            try
            {
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 800; // 提高显示质量
                bitmap.EndInit();
            }
            catch
            {
                // 如果UriSource失败（可能包含特殊字符），使用StreamSource
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 800; // 提高显示质量
                bitmap.EndInit();
            }

            // 创建主容器
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 标题栏
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"图片文件: {Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

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
            Grid.SetRow(scrollViewer, 1);
            grid.Children.Add(scrollViewer);
            
            return grid;
        }

        /// <summary>
        /// 创建 SVG 预览（使用 Magick.NET 渲染为位图）
        /// </summary>
        private UIElement CreateSvgPreview(string filePath)
        {
            try
            {
                // 使用 Magick.NET 加载 SVG 并转换为位图
                BitmapImage bitmap;
                
                using (var magickImage = new MagickImage(filePath))
                {
                    // 设置最大显示尺寸（保持宽高比）
                    if (magickImage.Width > 1600 || magickImage.Height > 1600)
                    {
                        magickImage.Resize(new MagickGeometry(1600, 1600)
                        {
                            IgnoreAspectRatio = false
                        });
                    }
                    
                    // 转换为字节数组
                    var bytes = magickImage.ToByteArray(MagickFormat.Png);
                    
                    // 创建 BitmapImage
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(bytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                // 创建主容器
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 标题栏
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"SVG 矢量图: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);

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
                Grid.SetRow(scrollViewer, 1);
                grid.Children.Add(scrollViewer);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载 SVG 文件: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建 PSD 预览（使用 Magick.NET 显示实际图像内容）
        /// </summary>
        private UIElement CreatePsdPreview(string filePath)
        {
            try
            {
                // 使用 Magick.NET 加载 PSD 并转换为位图
                BitmapImage bitmap;
                
                using (var magickImage = new MagickImage(filePath))
                {
                    // 设置最大显示尺寸（保持宽高比）
                    if (magickImage.Width > 1600 || magickImage.Height > 1600)
                    {
                        magickImage.Resize(new MagickGeometry(1600, 1600)
                        {
                            IgnoreAspectRatio = false
                        });
                    }
                    
                    // 转换为字节数组
                    var bytes = magickImage.ToByteArray(MagickFormat.Png);
                    
                    // 创建 BitmapImage
                    bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(bytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                }

                // 创建主容器
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 标题栏
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"Photoshop 文件: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                grid.Children.Add(titlePanel);

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
                Grid.SetRow(scrollViewer, 1);
                grid.Children.Add(scrollViewer);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载 PSD 文件: {ex.Message}");
            }
        }
    }
}



