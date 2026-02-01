using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YiboFile.Converters
{
    /// <summary>
    /// Converts a Double to a GridLength and vice versa.
    /// Useful for binding RowDefinition.Height or ColumnDefinition.Width to a ViewModel double property.
    /// </summary>
    [ValueConversion(typeof(double), typeof(GridLength))]
    public class DoubleToGridLengthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                return new GridLength(d);
            }
            return new GridLength(1, GridUnitType.Star); // Default fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is GridLength gl)
            {
                return gl.Value;
            }
            return 0.0;
        }
    }
}
