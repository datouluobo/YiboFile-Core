using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace YiboFile.Controls.Converters
{
    /// <summary>
    /// 驱动器使用率颜色转换器
    /// 将使用率百分比转换为对应的颜色：
    /// - 0% - 70%: 蓝色 (正常)
    /// - 70% - 90%: 橙色 (警告)
    /// - 90% - 100%: 红色 (危险)
    /// </summary>
    public class UsagePercentageToColorConverter : IValueConverter
    {
        // 预定义颜色画刷（避免每次转换都创建新实例）
        private static readonly SolidColorBrush NormalBrush = new SolidColorBrush(Color.FromRgb(74, 144, 226));   // #4A90E2 蓝色
        private static readonly SolidColorBrush WarningBrush = new SolidColorBrush(Color.FromRgb(255, 165, 0));   // #FFA500 橙色
        private static readonly SolidColorBrush DangerBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));    // #FF4444 红色

        static UsagePercentageToColorConverter()
        {
            // 冻结画刷以提高性能
            NormalBrush.Freeze();
            WarningBrush.Freeze();
            DangerBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double percentage)
            {
                if (percentage >= 0.9)
                    return DangerBrush;  // 红色：危险
                if (percentage >= 0.7)
                    return WarningBrush; // 橙色：警告
                return NormalBrush;      // 蓝色：正常
            }
            return NormalBrush; // 默认蓝色
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

