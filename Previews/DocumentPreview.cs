using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YiboFile.Previews.DocumentHandlers;

namespace YiboFile.Previews
{
    /// <summary>
    /// 文档文件预览（DOCX、DOC、PDF、RTF、CHM）
    /// NOTE: 具体预览逻辑已拆分至 DocumentHandlers 目录下的各个 Handler 中
    /// </summary>
    public class DocumentPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            var extension = Path.GetExtension(filePath)?.ToLower() ?? "";

            // 使用工厂模式获取对应的处理器
            var handler = DocumentPreviewFactory.GetHandler(extension);
            if (handler != null)
            {
                return handler.CreatePreview(filePath);
            }

            // 未知文档类型，显示通用预览
            return CreateGenericDocumentPreview(filePath);
        }

        private UIElement CreateGenericDocumentPreview(string filePath)
        {
            var panel = new StackPanel
            {
                Background = Brushes.White,
                Margin = new Thickness(10)
            };

            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var title = PreviewHelper.CreateTitlePanel("📄", $"文档: {Path.GetFileName(filePath)}", buttons);
            panel.Children.Add(title);

            long fileSize = 0;
            try
            {
                fileSize = new FileInfo(filePath).Length;
            }
            catch { }

            var info = new TextBlock
            {
                Text = $"文件大小: {PreviewHelper.FormatFileSize(fileSize)}",
                Foreground = Brushes.Gray,
                Margin = new Thickness(10),
                TextAlignment = TextAlignment.Center
            };
            panel.Children.Add(info);

            return panel;
        }
    }
}
