using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YiboFile.Controls.Converters
{
    /// <summary>
    /// 当容器宽度小于阈值时隐藏元素的转换器
    /// 用于驱动器别名在空间不足时完全隐藏
    /// </summary>
    public class WidthToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 默认最小宽度阈值（像素），低于此值时隐藏元素
        /// </summary>
        public static double DefaultThreshold { get; set; } = 200;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width)
            {
                // 获取阈值：优先使用参数，否则使用默认值
                double threshold = DefaultThreshold;
                if (parameter != null && double.TryParse(parameter.ToString(), out double paramThreshold))
                {
                    threshold = paramThreshold;
                }

                // 如果宽度小于阈值，隐藏元素
                return width >= threshold ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

