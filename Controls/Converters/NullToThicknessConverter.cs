using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YiboFile.Controls.Converters
{
    /// <summary>
    /// Converts null/empty string to border thickness (1 if null/empty, 0 if has value)
    /// Used for showing border on ungrouped tags that have no color
    /// </summary>
    public class NullToThicknessConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var str = value as string;
            // If color is null or empty, show border (thickness 1)
            // If color has value, no border needed (thickness 0)
            return string.IsNullOrEmpty(str) ? new Thickness(1) : new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
