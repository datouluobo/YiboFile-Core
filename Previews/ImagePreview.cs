using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                if (!File.Exists(filePath))
                {
                    return PreviewHelper.CreateErrorPreview($"图片文件不存在: {filePath}");
                }
                
                // 确保使用绝对路径
                if (!Path.IsPathRooted(filePath))
                {
                    filePath = Path.GetFullPath(filePath);
                }
                
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
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载图片: {ex.Message}");
            }
        }
    }
}

