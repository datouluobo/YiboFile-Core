using System.Windows;

namespace YiboFile.Services.UI.ColumnManagement
{
    /// <summary>
    /// 列头排序相关的附加属性
    /// </summary>
    public static class ColumnHeaderSortHelper
    {
        /// <summary>
        /// 排序方向附加属性
        /// </summary>
        public static readonly DependencyProperty SortDirectionProperty =
            DependencyProperty.RegisterAttached(
                "SortDirection",
                typeof(string),
                typeof(ColumnHeaderSortHelper),
                new PropertyMetadata(null));

        public static string GetSortDirection(DependencyObject obj)
        {
            return (string)obj.GetValue(SortDirectionProperty);
        }

        public static void SetSortDirection(DependencyObject obj, string value)
        {
            obj.SetValue(SortDirectionProperty, value);
        }
    }
}

