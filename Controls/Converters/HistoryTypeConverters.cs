using System;
using System.Globalization;
using System.Windows.Data;
using YiboFile.Services.Search;

namespace YiboFile.Controls
{
    /// <summary>
    /// 将 HistoryType 转换为图标字符
    /// </summary>
    public class HistoryTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HistoryType type)
            {
                return type switch
                {
                    HistoryType.LocalPath => "\uE838",
                    HistoryType.Search => "\uE721",
                    HistoryType.FullTextSearch => "\uE890",
                    _ => "\uE838"
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 将 HistoryType 转换为显示文本
    /// </summary>
    public class HistoryTypeToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is HistoryType type)
            {
                return type switch
                {
                    HistoryType.LocalPath => "位置",
                    HistoryType.Search => "搜索",
                    HistoryType.FullTextSearch => "全文",
                    _ => ""
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
