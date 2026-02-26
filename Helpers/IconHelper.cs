using System.Windows;
using System.Windows.Controls;

namespace YiboFile.Helpers
{
    public static class IconHelper
    {
        public static readonly DependencyProperty IconKeyProperty =
            DependencyProperty.RegisterAttached(
                "IconKey",
                typeof(string),
                typeof(IconHelper),
                new PropertyMetadata(null, OnIconKeyChanged));

        public static string GetIconKey(DependencyObject obj)
        {
            return (string)obj.GetValue(IconKeyProperty);
        }

        public static void SetIconKey(DependencyObject obj, string value)
        {
            obj.SetValue(IconKeyProperty, value);
        }

        private static void OnIconKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Image image)
            {
                if (e.NewValue is string key && !string.IsNullOrEmpty(key))
                {
                    image.SetResourceReference(Image.SourceProperty, key);
                }
                else
                {
                    image.ClearValue(Image.SourceProperty);
                }
            }
        }
    }
}