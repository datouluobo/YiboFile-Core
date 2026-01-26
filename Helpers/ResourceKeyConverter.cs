using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace YiboFile.Helpers
{
    /// <summary>
    /// Converts a resource key (string) to the resource value.
    /// Useful for dynamic icon binding using DynamicResource logic manually.
    /// </summary>
    public class ResourceKeyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string resourceKey && !string.IsNullOrEmpty(resourceKey))
            {
                try
                {
                    // Attempt to find the resource. 
                    // Note: This does not support dynamic updates if the resource changes later 
                    // unless the binding itself is refreshed or we use a different mechanism.
                    // However, standard ThemeManager replaces the dictionary, which triggers 
                    // a global update. But FindResource looks up the *current* val.
                    // If the dictionary is swapped, the UI might not update if binding is OneTime.
                    // Using OneWay binding should work if we raise PropertyChanged on the VM,
                    // but the VM property (Key) doesn't change, the Resource changes.
                    // 
                    // To support DynamicResource behavior, typically one would use a DynamicResourceExtension in XAML.
                    // But we are binding a data property to it.
                    //
                    // The standard way to solve this for dynamic themes is to just return the key 
                    // if we were using a custom control that knows how to lookup.
                    // 
                    // But here, we return the content. When theme changes, to get the new content,
                    // we need to re-evaluate.
                    // 
                    // A simple hack: The Binding won't trigger if the key string is same.
                    // So we rely on the View refreshing or standard WPF resource inheritance?
                    // No. FindResource returns the object.

                    return Application.Current.TryFindResource(resourceKey);
                }
                catch
                {
                    // Fallback
                    return null;
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

