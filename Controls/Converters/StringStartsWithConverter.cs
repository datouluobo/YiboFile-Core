using System;
using System.Globalization;
using System.Windows.Data;

namespace YiboFile.Controls.Converters
{
    public class StringStartsWithConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            string strValue = value.ToString();
            string strParam = parameter.ToString();
            return strValue.StartsWith(strParam, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
