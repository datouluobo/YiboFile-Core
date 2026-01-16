using System;
using System.Windows.Data;

namespace YiboFile
{
    /// <summary>
    /// 时间单位转换器 - 提取时间字符串中的单位
    /// </summary>
    public class TimeUnitConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null) return null;
            
            string timeStr = value.ToString();
            if (string.IsNullOrEmpty(timeStr)) return null;

            // 提取单位（最后的字母）
            if (timeStr.EndsWith("s"))
                return "s";
            else if (timeStr.EndsWith("m"))
                return "m";
            else if (timeStr.EndsWith("h"))
                return "h";
            else if (timeStr.EndsWith("d"))
                return "d";
            else if (timeStr.EndsWith("mo"))
                return "mo";
            else if (timeStr.EndsWith("y"))
                return "y";
            
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

