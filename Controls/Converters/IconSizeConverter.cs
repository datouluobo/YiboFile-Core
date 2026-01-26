using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace YiboFile.Controls.Converters
{
    /// <summary>
    /// 图标大小转换器
    /// 根据文件类型动态计算图标大小：
    /// - Office文档（docx, xlsx, pptx等）：使用10%的比例
    /// - 其他文件：使用15%的比例
    /// </summary>
    public class IconSizeConverter : IValueConverter
    {
        // Office文档扩展名列表
        private static readonly string[] OfficeExtensions = {
            ".doc", ".docx", ".docm", ".dot", ".dotx", ".dotm",
            ".xls", ".xlsx", ".xlsm", ".xlt", ".xltx", ".xltm",
            ".ppt", ".pptx", ".pptm", ".pot", ".potx", ".potm",
            ".odt", ".ods", ".odp"
        };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var path = value as string;
                if (string.IsNullOrEmpty(path))
                    return GetDefaultSize(parameter);

                // 获取文件扩展名
                var ext = Path.GetExtension(path)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(ext))
                    return GetDefaultSize(parameter);

                // 检查是否是Office文档
                bool isOfficeDocument = Array.IndexOf(OfficeExtensions, ext) >= 0;

                // 从parameter获取缩略图大小
                double thumbnailSize = 100; // 默认值
                if (parameter != null)
                {
                    if (parameter is double size)
                        thumbnailSize = size;
                    else if (double.TryParse(parameter.ToString(), out double parsedSize))
                        thumbnailSize = parsedSize;
                }

                // 根据文件类型选择比例
                double ratio = isOfficeDocument ? 0.10 : 0.15; // Office文档10%，其他15%

                // 计算图标大小
                int iconSize = (int)(thumbnailSize * ratio);

                // 应用范围限制（测试用：2-30px）
                iconSize = Math.Max(2, Math.Min(30, iconSize));

                return (double)iconSize;
            }
            catch
            {
                return GetDefaultSize(parameter);
            }
        }

        private double GetDefaultSize(object parameter)
        {
            // 默认使用15%的比例
            double thumbnailSize = 100;
            if (parameter != null)
            {
                if (parameter is double size)
                    thumbnailSize = size;
                else if (double.TryParse(parameter.ToString(), out double parsedSize))
                    thumbnailSize = parsedSize;
            }
            int iconSize = (int)(thumbnailSize * 0.15);
            return Math.Max(2, Math.Min(30, iconSize));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}





