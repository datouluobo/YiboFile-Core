using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace YiboFile.Controls.Converters
{
    /// <summary>
    /// 判断是否应该显示文件格式图标
    /// 文件夹不显示，Office文档不显示，非图片/视频文件不显示
    /// </summary>
    public class ShouldShowFileFormatIconConverter : IValueConverter
    {
        // 图片格式列表
        private static readonly string[] ImageExtensions = {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp",
            ".tiff", ".tif", ".ico", ".svg"
        };

        // 视频格式列表
        private static readonly string[] VideoExtensions = {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv",
            ".webm", ".m4v", ".3gp", ".asf", ".rm", ".rmvb",
            ".mpg", ".mpeg", ".m2v", ".vob", ".ogv", ".ts",
            ".mts", ".m2ts"
        };

        // Office文档格式列表（不显示左下角图标）
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
                    return System.Windows.Visibility.Collapsed;

                // 文件夹不显示
                if (Directory.Exists(path))
                    return System.Windows.Visibility.Collapsed;

                // 文件不存在，不显示
                if (!File.Exists(path))
                    return System.Windows.Visibility.Collapsed;

                // 检查文件扩展名
                var ext = Path.GetExtension(path)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(ext))
                    return System.Windows.Visibility.Collapsed;

                // Office文档不显示左下角图标
                if (Array.IndexOf(OfficeExtensions, ext) >= 0)
                    return System.Windows.Visibility.Collapsed;

                // 如果是图片或视频格式，显示图标
                if (Array.IndexOf(ImageExtensions, ext) >= 0 ||
                    Array.IndexOf(VideoExtensions, ext) >= 0)
                {
                    return System.Windows.Visibility.Visible;
                }

                // 其他文件类型不显示
                return System.Windows.Visibility.Collapsed;
            }
            catch
            {
                return System.Windows.Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}



