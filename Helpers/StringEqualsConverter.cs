using System;
using System.Globalization;
using System.Windows.Data;

namespace YiboFile.Helpers
{
    /// <summary>
    /// 字符串相等转换器
    /// 用于将字符串比较结果转换为布尔值或字符串（如 "Active"）
    /// </summary>
    public class StringEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return targetType == typeof(bool) ? (object)false : null;

            bool isEqual = string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);

            // 如果目标类型是 bool，直接返回相等结果
            if (targetType == typeof(bool))
                return isEqual;

            // 如果目标类型是 string（例如用于 Tag），返回 "Active" 或 null
            return isEqual ? "Active" : null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 用于双向绑定（如 RadioButton.IsChecked）
            if (value is bool boolValue && boolValue)
                return parameter?.ToString();

            return Binding.DoNothing;
        }
    }
}
