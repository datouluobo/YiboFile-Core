using System;
using System.Collections.Generic;
using System.Linq;

namespace YiboFile.Previews.DocumentHandlers
{
    /// <summary>
    /// 文档预览处理器工厂
    /// 根据文件扩展名返回对应的预览处理器
    /// </summary>
    public static class DocumentPreviewFactory
    {
        private static readonly List<IDocumentPreviewHandler> _handlers = new List<IDocumentPreviewHandler>();

        static DocumentPreviewFactory()
        {
            // 注册所有支持的处理器
            RegisterHandler(new DocxPreviewHandler());
            RegisterHandler(new PdfPreviewHandler());
            RegisterHandler(new RtfPreviewHandler());
            RegisterHandler(new DocPreviewHandler());
            RegisterHandler(new ChmPreviewHandler());
        }

        /// <summary>
        /// 注册处理器
        /// </summary>
        public static void RegisterHandler(IDocumentPreviewHandler handler)
        {
            if (handler != null && !_handlers.Contains(handler))
            {
                _handlers.Add(handler);
            }
        }

        /// <summary>
        /// 根据文件扩展名获取处理器
        /// </summary>
        /// <param name="extension">文件扩展名（含点，如 ".docx"）</param>
        /// <returns>处理器实例，如果没有找到则返回 null</returns>
        public static IDocumentPreviewHandler GetHandler(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return null;

            extension = extension.ToLower();
            return _handlers.FirstOrDefault(h => h.CanHandle(extension));
        }

        /// <summary>
        /// 判断是否支持指定的文件扩展名
        /// </summary>
        public static bool IsSupported(string extension)
        {
            return GetHandler(extension) != null;
        }
    }
}

