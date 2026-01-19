using System;
using System.Globalization;
using System.Windows.Data;

namespace YiboFile.Converters
{
    public class MathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double dValue && double.TryParse(parameter?.ToString(), out double dParam))
            {
                return dValue + dParam;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
