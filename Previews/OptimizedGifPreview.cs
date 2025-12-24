using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 优化的GIF动画预览（使用WpfAnimatedGif库）
    /// </summary>
    public class OptimizedGifPreview
    {
        public static UIElement CreatePreview(string filePath)
        {
            // 创建主容器
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 工具栏
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 内容区

            // 标题栏
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("🎞️", $"GIF 动画: {System.IO.Path.GetFileName(filePath)}", buttons);
            Grid.SetRow(titlePanel, 0);
            grid.Children.Add(titlePanel);

            // Transform配置
            var scaleTransform = new ScaleTransform(1.0, 1.0);
            var rotateTransform = new RotateTransform(0);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(rotateTransform);

            try
            {
                // 使用WpfAnimatedGif库显示GIF动画
                var image = new Image
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransform = transformGroup,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                };

                // 设置动画源
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                // 使用WpfAnimatedGif设置动画
                WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, bitmapImage);
                WpfAnimatedGif.ImageBehavior.SetRepeatBehavior(image, System.Windows.Media.Animation.RepeatBehavior.Forever);

                // 添加ScrollViewer
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
                Grid.SetRow(errorText, 2);
                grid.Children.Add(errorText);
            }

            return grid;
        }
    }
}
