using System.Windows;

namespace YiboFile.Services.UI
{
    public static class DragAttachedProperties
    {
        public static readonly DependencyProperty IsDragTargetProperty =
            DependencyProperty.RegisterAttached(
                "IsDragTarget",
                typeof(bool),
                typeof(DragAttachedProperties),
                new PropertyMetadata(false));

        public static bool GetIsDragTarget(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsDragTargetProperty);
        }

        public static void SetIsDragTarget(DependencyObject obj, bool value)
        {
            obj.SetValue(IsDragTargetProperty, value);
        }
    }
}

