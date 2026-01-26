using System;
using System.Linq;
using System.Windows;

namespace YiboFile.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static class ThemeService
    {
        private const string ThemeDictionaryUrl = "pack://application:,,,/Themes/{0}.xaml";

        public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

        public static void SetTheme(AppTheme theme)
        {
            var themeName = theme.ToString();
            var newDictUri = new Uri(string.Format(ThemeDictionaryUrl, themeName), UriKind.Absolute);

            try
            {
                var newDict = new ResourceDictionary { Source = newDictUri };

                // Find existing theme dictionary to replace
                // We assume the theme dictionary is the one that contains "AppBackgroundBrush"
                var appDictionaries = Application.Current.Resources.MergedDictionaries;
                var existingDict = appDictionaries.FirstOrDefault(d => d.Contains("AppBackgroundBrush"));

                if (existingDict != null)
                {
                    appDictionaries.Remove(existingDict);
                }

                appDictionaries.Add(newDict);
                CurrentTheme = theme;
            }
            catch (Exception)
            { }
        }

        public static void ToggleTheme()
        {
            SetTheme(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        }
    }
}

