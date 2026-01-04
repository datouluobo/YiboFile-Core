using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using OoiMRR.Controls;

namespace OoiMRR.Previews.DocumentHandlers
{
    /// <summary>
    /// RTF 文档预览处理器
    /// </summary>
    public class RtfPreviewHandler : IDocumentPreviewHandler
    {
        public bool CanHandle(string extension)
        {
            return extension?.ToLower() == ".rtf";
        }

        public UIElement CreatePreview(string filePath)
        {
            try
            {
                // 使用Grid布局：标题栏 + 内容区域
                var grid = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Background = new SolidColorBrush(Color.FromRgb(255, 255, 255))
                };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 统一工具栏
                var toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "📄",
                    ShowSearch = false,
                    ShowWordWrap = false,
                    ShowEncoding = false,
                    ShowViewToggle = false,
                    ShowFormat = false
                };
                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                Grid.SetRow(toolbar, 0);
                grid.Children.Add(toolbar);

                var rtfBox = new RichTextBox
                {
                    IsReadOnly = true,
                    Margin = new Thickness(10),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Gray,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                // 读取RTF内容到MemoryStream
                byte[] rtfBytes = File.ReadAllBytes(filePath);
                using (var memStream = new MemoryStream(rtfBytes))
                {
                    var textRange = new TextRange(rtfBox.Document.ContentStart, rtfBox.Document.ContentEnd);
                    textRange.Load(memStream, DataFormats.Rtf);
                }

                Grid.SetRow(rtfBox, 1);
                grid.Children.Add(rtfBox);

                return grid;
            }
            catch (Exception ex)
            {
                return CreateDocumentErrorPanel($"RTF 预览失败: {ex.Message}");
            }
        }

        private UIElement CreateDocumentErrorPanel(string message)
        {
            var panel = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(20)
            };

            var errorText = new TextBlock
            {
                Text = message,
                Foreground = Brushes.Red,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            };
            panel.Children.Add(errorText);

            return panel;
        }
    }
}
