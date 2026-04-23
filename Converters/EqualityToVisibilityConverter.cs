using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YiboFile.Converters
{
    public class EqualityToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return Visibility.Collapsed;

            bool result = value.ToString().Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
            return result ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
