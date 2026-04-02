using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace YiboFile.Controls.Behaviors
{
    /// <summary>
    /// 提供附加属性，用于阻断 ListView 列头上的导航触发。
    /// 解决在预览窗口中调整列宽时误进入文件夹的问题。
    /// </summary>
    public static class ColumnResizeNavigationBlocker
    {
        public static readonly DependencyProperty SuppressHeaderNavigationProperty =
            DependencyProperty.RegisterAttached(
                "SuppressHeaderNavigation",
                typeof(bool),
                typeof(ColumnResizeNavigationBlocker),
                new PropertyMetadata(false, OnSuppressHeaderNavigationChanged));

        public static bool GetSuppressHeaderNavigation(DependencyObject obj) => (bool)obj.GetValue(SuppressHeaderNavigationProperty);
        public static void SetSuppressHeaderNavigation(DependencyObject obj, bool value) => obj.SetValue(SuppressHeaderNavigationProperty, value);

        private static void OnSuppressHeaderNavigationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Control element)
            {
                if ((bool)e.NewValue)
                {
                    element.PreviewMouseDoubleClick += Element_PreviewMouseDoubleClick;
                }
                else
                {
                    element.PreviewMouseDoubleClick -= Element_PreviewMouseDoubleClick;
                }
            }
        }

        private static void Element_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IsOnHeaderOrThumb(e.OriginalSource as DependencyObject))
            {
                e.Handled = true;
            }
        }

        private static bool IsOnHeaderOrThumb(DependencyObject obj)
        {
            while (obj != null)
            {
                if (obj is GridViewColumnHeader || obj is Thumb)
                    return true;
                
                // 某些情况下 OriginalSource 可能是 TextBlock 等，需要向上查找
                obj = VisualTreeHelper.GetParent(obj);
            }
            return false;
        }
    }
}
