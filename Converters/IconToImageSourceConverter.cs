using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace YiboFile.Converters
{
    /// <summary>
    /// 将 Icon 属性对象（可能是 string, ImageSource, Drawing 等）转换为 Image.Source 可用的 ImageSource。
    /// 如果是 string (Emoji)，则返回 null，由 ContentPresenter 渲染。
    /// </summary>
    public class IconToImageSourceConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return null;

            // Ensure we handle ImageSource directly
            if (value is ImageSource imageSource) return imageSource;

            // Handle Drawing icons
            if (value is Drawing drawing) return new DrawingImage(drawing);

            // Special logic for string Icon value (e.g. Emoji or text shorthand)
            if (value is string str)
            {
                // Emoji or short labels should not be used as Image.Source
                if (str.Length <= 2) return null;
                
                // Potential image URIs/paths can be returned as-is
                return value; 
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
