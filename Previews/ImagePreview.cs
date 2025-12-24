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
        /// 创建位图预览 - 旧版本（待删除）
        /// </summary>
        private UIElement CreateBitmapPreview_Old(string filePath)
        {
            BitmapImage bitmap;

            // 优先尝试使用UriSource（性能更好），如果失败则使用StreamSource
            try
            {
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            }
            catch
            {
                // 如果UriSource失败（可能包含特殊字符），使用StreamSource
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
            }

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

            // 工具栏
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = (Brush)Application.Current.FindResource("PreviewPanelBackgroundBrush"),
                Margin = new Thickness(0, 5, 0, 5),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(toolbar, 1);

            // Transform配置
            var scaleTransform = new ScaleTransform(1.0, 1.0);
            var rotateTransform = new RotateTransform(0);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(rotateTransform);

            double currentScale = 1.0;
            double currentRotation = 0;
            bool isFitToWindow = true;

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

            // 按钮样式
            var buttonStyle = Application.Current.TryFindResource("ModernButtonStyle") as Style;

            // 放大按钮
            var zoomInBtn = new Button
            {
                Content = "🔍+ 放大",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Style = buttonStyle,
                ToolTip = "放大 (Ctrl+Plus)"
            };
            zoomInBtn.Click += (s, e) =>
            {
                if (isFitToWindow)
                {
                    image.Stretch = Stretch.None;
                    isFitToWindow = false;
                }
                currentScale *= 1.2;
                scaleTransform.ScaleX = currentScale;
                scaleTransform.ScaleY = currentScale;
            };
            toolbar.Children.Add(zoomInBtn);

            // 缩小按钮
            var zoomOutBtn = new Button
            {
                Content = "🔍- 缩小",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Style = buttonStyle,
                ToolTip = "缩小 (Ctrl+Minus)"
            };
            zoomOutBtn.Click += (s, e) =>
            {
                if (isFitToWindow)
                {
                    image.Stretch = Stretch.None;
                    isFitToWindow = false;
                }
                currentScale /= 1.2;
                if (currentScale < 0.1) currentScale = 0.1;
                scaleTransform.ScaleX = currentScale;
                scaleTransform.ScaleY = currentScale;
            };
            toolbar.Children.Add(zoomOutBtn);

            // 100%按钮
            var resetBtn = new Button
            {
                Content = "1:1 原始",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Style = buttonStyle,
                ToolTip = "原始大小 (Ctrl+0)"
            };
            resetBtn.Click += (s, e) =>
            {
                image.Stretch = Stretch.None;
                currentScale = 1.0;
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
                isFitToWindow = false;
            };
            toolbar.Children.Add(resetBtn);

            // 适应窗口按钮
            var fitBtn = new Button
            {
                Content = "⊡ 适应",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Style = buttonStyle,
                ToolTip = "适应窗口 (Ctrl+F)"
            };
            fitBtn.Click += (s, e) =>
            {
                image.Stretch = Stretch.Uniform;
                currentScale = 1.0;
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
                isFitToWindow = true;
            };
            toolbar.Children.Add(fitBtn);

            // 旋转按钮
            var rotateBtn = new Button
            {
                Content = "🔄 旋转",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Style = buttonStyle,
                ToolTip = "顺时针旋转90° (R)"
            };
            rotateBtn.Click += (s, e) =>
            {
                currentRotation = (currentRotation + 90) % 360;
                rotateTransform.Angle = currentRotation;
            };
            toolbar.Children.Add(rotateBtn);

            // 全屏按钮
            bool isFullscreen = false;
            var fullscreenBtn = new Button
            {
                Content = "⛶ 全屏",
                Margin = new Thickness(5),
                Padding = new Thickness(10, 5, 10, 5),
                Style = buttonStyle,
                ToolTip = "全屏查看 (F11)"
            };
            fullscreenBtn.Click += (s, e) =>
            {
                var window = Window.GetWindow(grid);
                if (window == null) return;

                isFullscreen = !isFullscreen;
                if (isFullscreen)
                {
                    titlePanel.Visibility = Visibility.Collapsed;
                    toolbar.Visibility = Visibility.Collapsed;
                    window.WindowStyle = WindowStyle.None;
                    window.WindowState = WindowState.Maximized;
                    window.Topmost = true;
                    fullscreenBtn.Content = "⛶ 退出";
                }
                else
                {
                    titlePanel.Visibility = Visibility.Visible;
                    toolbar.Visibility = Visibility.Visible;
                    window.WindowStyle = WindowStyle.SingleBorderWindow;
                    window.WindowState = WindowState.Normal;
                    window.Topmost = false;
                    fullscreenBtn.Content = "⛶ 全屏";
                }
            };
            toolbar.Children.Add(fullscreenBtn);

            grid.Children.Add(toolbar);

            // 快捷键支持
            grid.Focusable = true;
            grid.PreviewKeyDown += (s, e) =>
            {
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    if (e.Key == Key.OemPlus || e.Key == Key.Add)
                    {
                        zoomInBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                    else if (e.Key == Key.OemMinus || e.Key == Key.Subtract)
                    {
                        zoomOutBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                    else if (e.Key == Key.D0 || e.Key == Key.NumPad0)
                    {
                        resetBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                    else if (e.Key == Key.F)
                    {
                        fitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.R)
                {
                    rotateBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.F11)
                {
                    fullscreenBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape && isFullscreen)
                {
                    fullscreenBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }
            };

            return grid;
        }

        /// <summary>
        /// 创建 SVG 预览（使用 WebBrowser 直接渲染）
        /// </summary>
        private UIElement CreateSvgWebBrowserPreview(string filePath)
        {
            // 创建主容器
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 标题栏
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"SVG 矢量图: {Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            try
            {
                // 创建WebBrowser并加载SVG
                var webBrowser = new System.Windows.Controls.WebBrowser();

                // 读取SVG文件内容
                string svgContent = File.ReadAllText(filePath);
                webBrowser.NavigateToString(svgContent);

                Grid.SetRow(webBrowser, 1);
                grid.Children.Add(webBrowser);
            }
            catch (Exception ex)
            {
                var errorText = new TextBlock
                {
                    Text = $"无法加载 SVG: {ex.Message}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.Red,
                    FontSize = 14
                };
                Grid.SetRow(errorText, 1);
                grid.Children.Add(errorText);
            }

            return grid;
        }

        /// <summary>
        /// 创建 GIF 动画预览（使用 GifBitmapDecoder 手动播放帧）
        /// </summary>
        private UIElement CreateGifPreview(string filePath)
        {
            // 创建主容器
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 标题栏
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🎞️", $"GIF 动画: {Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            try
            {
                // 使用Image控件显示GIF
                var image = new Image
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // 加载GIF并启动动画
                using (var stream = File.OpenRead(filePath))
                {
                    var decoder = new GifBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);

                    if (decoder.Frames.Count > 0)
                    {
                        // 如果只有一帧，直接显示
                        if (decoder.Frames.Count == 1)
                        {
                            image.Source = decoder.Frames[0];
                        }
                        else
                        {
                            // 多帧动画 - 启动动画控制器
                            var frames = new List<BitmapFrame>();
                            var delays = new List<int>();

                            foreach (var frame in decoder.Frames)
                            {
                                frames.Add(frame);

                                // 尝试读取帧延迟（毫秒）
                                int delay = 100; // 默认延迟
                                try
                                {
                                    var metadata = frame.Metadata as BitmapMetadata;
                                    if (metadata != null && metadata.ContainsQuery("/grctlext/Delay"))
                                    {
                                        var delayValue = metadata.GetQuery("/grctlext/Delay");
                                        if (delayValue is ushort delayUShort)
                                        {
                                            delay = delayUShort * 10; // GIF延迟单位是1/100秒
                                            if (delay < 10) delay = 100; // 最小延迟
                                        }
                                    }
                                }
                                catch { }

                                delays.Add(delay);
                            }

                            // 设置第一帧
                            image.Source = frames[0];

                            // 创建动画控制器 - 使用Background优先级避免阻塞UI
                            int currentFrame = 0;
                            var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background);
                            timer.Interval = TimeSpan.FromMilliseconds(delays[0]);
                            timer.Tick += (s, e) =>
                            {
                                currentFrame = (currentFrame + 1) % frames.Count;
                                image.Source = frames[currentFrame];
                                timer.Interval = TimeSpan.FromMilliseconds(delays[currentFrame]);
                            };
                            timer.Start();

                            // 当控件卸载时停止timer
                            grid.Unloaded += (s, e) => timer.Stop();
                        }
                    }
                }

                // 添加ScrollViewer
                var scrollViewer = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = image
                };
                Grid.SetRow(scrollViewer, 2);
                grid.Children.Add(scrollViewer);
            }
            catch (Exception ex)
            {
                var errorText = new TextBlock
                {
                    Text = $"无法加载 GIF: {ex.Message}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.Red,
                    FontSize = 14
                };
                Grid.SetRow(errorText, 1);
                grid.Children.Add(errorText);
            }

            return grid;
        }

        /// <summary>
        /// 创建 SVG 预览（使用 Magick.NET 渲染为位图）
        /// </summary>
        private UIElement CreateSvgPreview(string filePath)
        {
            // 创建主容器
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 标题栏
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"SVG 矢量图: {Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            // 加载指示器
            var loadingText = new TextBlock
            {
                Text = "正在渲染 SVG...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 14
            };
            Grid.SetRow(loadingText, 2);
            grid.Children.Add(loadingText);

            // 图片控件（初始隐藏）
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            // 添加ScrollViewer
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = image,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(scrollViewer, 2);
            grid.Children.Add(scrollViewer);

            // 异步加载
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    BitmapImage bitmap = null;
                    using (var magickImage = new MagickImage(filePath))
                    {
                        var maxDim = 2048;
                        if (magickImage.Width > maxDim || magickImage.Height > maxDim)
                        {
                            magickImage.Resize(new MagickGeometry((uint)maxDim, (uint)maxDim) { IgnoreAspectRatio = false });
                        }

                        var bytes = magickImage.ToByteArray(MagickFormat.Png);

                        bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = new MemoryStream(bytes);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }

                    // UI 更新
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        image.Source = bitmap;
                        image.Visibility = Visibility.Visible;
                        scrollViewer.Visibility = Visibility.Visible;
                        loadingText.Visibility = Visibility.Collapsed;
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        loadingText.Text = $"SVG 渲染失败: {ex.Message}";
                        loadingText.Foreground = System.Windows.Media.Brushes.Red;
                    });
                }
            });

            return grid;
        }

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

        /// <summary>
        /// 创建 PSD 预览（使用 Magick.NET 显示实际图像内容）
        /// </summary>
        private UIElement CreatePsdPreview(string filePath)
        {
            // 创建主容器
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 标题栏
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🖼️", $"Photoshop 文件: {Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            // 加载指示器
            var loadingText = new TextBlock
            {
                Text = "正在解析 PSD...",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 14
            };
            Grid.SetRow(loadingText, 2);
            grid.Children.Add(loadingText);

            // 图片控件（初始隐藏）
            var image = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            // 添加ScrollViewer
            var scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = image,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(scrollViewer, 2);
            grid.Children.Add(scrollViewer);

            // 异步加载
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    BitmapImage bitmap = null;
                    using (var magickImage = new MagickImage(filePath))
                    {
                        var maxDim = 2048;
                        if (magickImage.Width > maxDim || magickImage.Height > maxDim)
                        {
                            magickImage.Resize(new MagickGeometry((uint)maxDim, (uint)maxDim) { IgnoreAspectRatio = false });
                        }

                        var bytes = magickImage.ToByteArray(MagickFormat.Png);

                        bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = new MemoryStream(bytes);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                    }

                    // UI 更新
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        image.Source = bitmap;
                        image.Visibility = Visibility.Visible;
                        scrollViewer.Visibility = Visibility.Visible;
                        loadingText.Visibility = Visibility.Collapsed;
                    });
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        loadingText.Text = $"PSD 解析失败: {ex.Message}";
                        loadingText.Foreground = System.Windows.Media.Brushes.Red;
                    });
                }
            });

            return grid;
        }
    }
}

