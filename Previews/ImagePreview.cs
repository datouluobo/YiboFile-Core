using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using ImageMagick;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 图片文件预览 - 支持多种图像格式
    /// 支持格式: bmp, jpeg, jpg, png, gif, tif, tiff, ico, svg, psd
    /// </summary>
    public class ImagePreview : IPreviewProvider
    {
        // 需要ImageMagick处理的格式
        private static readonly HashSet<string> _imageMagickFormats = new()
        {
            ".tga",   // Targa游戏纹理
            ".blp",   // Blizzard游戏纹理
            ".heic",  // iOS高效图像
            ".heif",  // iOS高效图像
            ".ai",    // Adobe Illustrator
            ".psd",   // Photoshop
            ".svg"    // SVG矢量图
        };
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

                // 特殊处理 SVG 格式 - 使用WebBrowser直接渲染
                if (extension == ".svg")
                {
                    return SvgPreview.CreatePreview(filePath);
                }

                // 特殊处理 GIF 格式（支持动画）
                if (extension == ".gif")
                {
                    return OptimizedGifPreview.CreatePreview(filePath);
                }

                // ImageMagick处理的格式（TGA/BLP/HEIC/HEIF/AI/PSD）
                if (_imageMagickFormats.Contains(extension))
                {
                    return CreateMagickPreview(filePath, extension);
                }

                // WPF原生支持的格式（bmp, jpeg, jpg, png, tif, tiff, ico）
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
        /// <summary>
        /// 创建位图预览 (BMP/JPEG/PNG/ICO/TIFF等)
        /// </summary>
        private UIElement CreateBitmapPreview(string filePath)
        {
            // 加载位图
            BitmapImage bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            // 创建主容器
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 工具栏
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 图片区

            // 标题栏
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"图片文件: {Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            // Transform配置
            var scaleTransform = new ScaleTransform(1.0, 1.0);
            var rotateTransform = new RotateTransform(0);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(rotateTransform);

            // 创建Image
            var image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform, // 默认适应窗口
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransform = transformGroup,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            // ScrollViewer
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = image
            };
            Grid.SetRow(scrollViewer, 2);
            grid.Children.Add(scrollViewer);

            // 创建工具栏
            var toolbar = ImageToolbarHelper.CreateToolbar(new ImageToolbarHelper.ToolbarConfig
            {
                TargetImage = image,
                ScaleTransform = scaleTransform,
                RotateTransform = rotateTransform,
                TitlePanel = titlePanel,
                ParentGrid = grid
            });
            Grid.SetRow(toolbar, 1);
            grid.Children.Add(toolbar);

            return grid;
        }

        /// <summary>

        /// <summary>
        /// 使用ImageMagick创建预览（TGA/BLP/HEIC/HEIF/AI/PSD）
        /// </summary>
        private UIElement CreateMagickPreview(string filePath, string extension)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 工具栏
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 图片区

            // 标题栏
            var formatName = GetFormatDisplayName(extension);
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"{formatName}: {Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            // Transform配置
            var scaleTransform = new ScaleTransform(1.0, 1.0);
            var rotateTransform = new RotateTransform(0);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(rotateTransform);

            // 加载指示器
            var loadingText = new TextBlock
            {
                Text = $"正在加载 {formatName}...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Gray,
                FontSize = 14
            };
            Grid.SetRow(loadingText, 2);
            grid.Children.Add(loadingText);

            // 图片控件
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                RenderTransform = transformGroup,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = image,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(scrollViewer, 2);
            grid.Children.Add(scrollViewer);

            // 创建工具栏
            var toolbar = ImageToolbarHelper.CreateToolbar(new ImageToolbarHelper.ToolbarConfig
            {
                TargetImage = image,
                ScaleTransform = scaleTransform,
                RotateTransform = rotateTransform,
                TitlePanel = titlePanel,
                ParentGrid = grid
            });
            toolbar.Visibility = Visibility.Collapsed;
            Grid.SetRow(toolbar, 1);
            grid.Children.Add(toolbar);

            // 异步加载
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var bitmap = DecodeWithImageMagick(filePath);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        image.Source = bitmap;
                        image.Visibility = Visibility.Visible;
                        scrollViewer.Visibility = Visibility.Visible;
                        toolbar.Visibility = Visibility.Visible;
                        loadingText.Visibility = Visibility.Collapsed;
                    });
                }
                catch (MagickException ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        HandleMagickError(loadingText, ex, extension, filePath);
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        loadingText.Text = $"加载失败: {ex.Message}";
                        loadingText.Foreground = Brushes.Red;
                    });
                }
            });

            return grid;
        }

        /// <summary>
        /// 使用ImageMagick解码图像
        /// </summary>
        private BitmapSource DecodeWithImageMagick(string filePath)
        {
            using var magickImage = new MagickImage(filePath);

            // 限制尺寸避免内存问题，与DecodePixelWidth=800保持一致
            const int maxDim = 800;
            if (magickImage.Width > maxDim || magickImage.Height > maxDim)
            {
                magickImage.Resize(new MagickGeometry((uint)maxDim, (uint)maxDim)
                {
                    IgnoreAspectRatio = false
                });
            }

            // 转换为PNG
            var bytes = magickImage.ToByteArray(MagickFormat.Png);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = new MemoryStream(bytes);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }

        /// <summary>
        /// 获取格式显示名称
        /// </summary>
        private string GetFormatDisplayName(string extension)
        {
            return extension switch
            {
                ".tga" => "TGA 图像",
                ".blp" => "BLP 纹理",
                ".heic" => "HEIC 图片",
                ".heif" => "HEIF 图片",
                ".ai" => "AI 矢量图",
                ".psd" => "Photoshop 文件",
                ".svg" => "SVG 矢量图",
                _ => "图片文件"
            };
        }

        /// <summary>
        /// 处理ImageMagick错误
        /// </summary>
        private void HandleMagickError(TextBlock loadingText, MagickException ex, string extension, string filePath)
        {
            // HEIC/HEIF缺少解码器
            if (ex.Message.Contains("delegate") && (extension == ".heic" || extension == ".heif"))
            {
                loadingText.Text = "❌ 缺少HEIF解码器\n\n" +
                                  "请从 Microsoft Store 安装 \"HEIF图像扩展\"\n" +
                                  "或使用其他应用打开此文件";
                loadingText.FontSize = 13;
            }
            else if (ex.Message.Contains("no decode delegate"))
            {
                loadingText.Text = $"❌ 不支持此{extension}文件\n\n{ex.Message}";
            }
            else
            {
                loadingText.Text = $"解码失败: {ex.Message}";
            }

            loadingText.Foreground = Brushes.Red;
        }

    }
}

