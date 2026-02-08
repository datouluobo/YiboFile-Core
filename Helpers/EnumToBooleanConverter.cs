using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YiboFile.Helpers
{
    /// <summary>
    /// 将 Enum 值转换为布尔值的转换器，用于 RadioButton/ToggleButton 的 IsChecked 绑定
    /// </summary>
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string checkValue = value.ToString();
            string targetValue = parameter.ToString();

            return string.Equals(checkValue, targetValue, StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null || !(bool)value)
                return Binding.DoNothing;

            try
            {
                return Enum.Parse(targetType, parameter.ToString());
            }
            catch
            {
                return Binding.DoNothing;
            }
        }
    }
}
