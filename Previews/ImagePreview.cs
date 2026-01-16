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
using YiboFile.Controls;

namespace YiboFile.Previews
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
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏 (unified toolbar)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 工具栏 (image tools)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 图片区

            // 统一工具栏
            var mainToolbar = new TextPreviewToolbar
            {
                FileName = Path.GetFileName(filePath),
                FileIcon = "🖼️",
                ShowSearch = false,
                ShowWordWrap = false,
                ShowEncoding = false,
                ShowViewToggle = false,
                ShowFormat = false
            };
            mainToolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

            Grid.SetRow(mainToolbar, 0);
            grid.Children.Add(mainToolbar);

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

            // 创建图片工具栏
            var imageToolbar = ImageToolbarHelper.CreateToolbar(new ImageToolbarHelper.ToolbarConfig
            {
                TargetImage = image,
                ScaleTransform = scaleTransform,
                RotateTransform = rotateTransform,
                TitlePanel = mainToolbar, // Refactored to accept UIElement
                ParentGrid = grid
            });
            Grid.SetRow(imageToolbar, 1);
            grid.Children.Add(imageToolbar);

            return grid;
        }

        /// <summary>
        /// 使用ImageMagick创建预览（TGA/BLP/HEIC/HEIF/AI/PSD）
        /// </summary>
        private UIElement CreateMagickPreview(string filePath, string extension)
        {
            var grid = new Grid
            {
                Background = Brushes.White // 统一背景色
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏 (unified toolbar)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 工具栏 (image tools)
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 图片区

            // 标题栏
            var formatName = GetFormatDisplayName(extension);

            var mainToolbar = new TextPreviewToolbar
            {
                FileName = $"{formatName}: {Path.GetFileName(filePath)}",
                FileIcon = "🖼️",
                ShowSearch = false,
                ShowWordWrap = false,
                ShowEncoding = false,
                ShowViewToggle = false,
                ShowFormat = false
            };
            mainToolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

            Grid.SetRow(mainToolbar, 0);
            grid.Children.Add(mainToolbar);

            // Transform配置
            var scaleTransform = new ScaleTransform(1.0, 1.0);
            var rotateTransform = new RotateTransform(0);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(rotateTransform);

            // 图片控件 (初始隐藏)
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
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

            // 创建工具栏 (初始隐藏)
            var imageToolbar = ImageToolbarHelper.CreateToolbar(new ImageToolbarHelper.ToolbarConfig
            {
                TargetImage = image,
                ScaleTransform = scaleTransform,
                RotateTransform = rotateTransform,
                TitlePanel = mainToolbar,
                ParentGrid = grid
            });
            imageToolbar.Visibility = Visibility.Collapsed;
            Grid.SetRow(imageToolbar, 1);
            grid.Children.Add(imageToolbar);

            // 加载指示器 (默认显示，跨行覆盖)
            var loadingPanel = PreviewHelper.CreateLoadingPanel($"正在解析 {formatName}...");
            Grid.SetRow(loadingPanel, 1);
            Grid.SetRowSpan(loadingPanel, 2); // 覆盖工具栏和内容区
            grid.Children.Add(loadingPanel);

            // 错误容器 (用于显示错误信息)
            var errorContainer = new Grid { Visibility = Visibility.Collapsed };
            Grid.SetRow(errorContainer, 2);
            grid.Children.Add(errorContainer);

            // 异步加载
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var bitmap = DecodeWithImageMagick(filePath);

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        image.Source = bitmap;
                        // 显示内容
                        scrollViewer.Visibility = Visibility.Visible;
                        imageToolbar.Visibility = Visibility.Visible;
                        // 隐藏加载
                        loadingPanel.Visibility = Visibility.Collapsed;
                    });
                }
                catch (MagickException ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        loadingPanel.Visibility = Visibility.Collapsed;
                        errorContainer.Visibility = Visibility.Visible;
                        errorContainer.Children.Clear();
                        errorContainer.Children.Add(CreateMagickErrorPanel(ex, extension, filePath));
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        loadingPanel.Visibility = Visibility.Collapsed;
                        errorContainer.Visibility = Visibility.Visible;
                        errorContainer.Children.Clear();
                        errorContainer.Children.Add(PreviewHelper.CreateErrorPreview($"加载失败: {ex.Message}"));
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
        /// 创建ImageMagick错误面板
        /// </summary>
        private UIElement CreateMagickErrorPanel(MagickException ex, string extension, string filePath)
        {
            // HEIC/HEIF缺少解码器
            if (ex.Message.Contains("delegate") && (extension == ".heic" || extension == ".heif"))
            {
                return PreviewHelper.CreateErrorPreview("缺少 HEIF 解码器\n请从 Microsoft Store 安装 \"HEIF图像扩展\" 或使用外部程序打开");
            }
            else if (ex.Message.Contains("no decode delegate"))
            {
                return PreviewHelper.CreateNoPreview(filePath); // 使用"不支持预览"的统一UI
            }
            else
            {
                return PreviewHelper.CreateErrorPreview($"解码失败: {ex.Message}");
            }
        }

    }
}



