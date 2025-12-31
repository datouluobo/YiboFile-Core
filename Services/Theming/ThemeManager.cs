using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;

namespace OoiMRR.Services.Theming
{
    /// <summary>
    /// 主题管理器 - 负责主题发现、加载和切换
    /// </summary>
    public class ThemeManager
    {
        private static readonly Dictionary<string, ThemeMetadata> _themes = new();
        private static ThemeMetadata _currentTheme;

        /// <summary>
        /// 主题变更事件
        /// </summary>
        public static event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        static ThemeManager()
        {
            DiscoverThemes();
        }

        /// <summary>
        /// 自动发现所有可用主题
        /// </summary>
        private static void DiscoverThemes()
        {
            // 扫描内置主题
            var builtInThemes = new[] { "Light", "Dark" };
            foreach (var themeId in builtInThemes)
            {
                var uri = new Uri($"pack://application:,,,/Themes/{themeId}.xaml", UriKind.Absolute);
                var metadata = LoadThemeMetadata(uri, themeId);
                if (metadata != null)
                {
                    _themes[themeId] = metadata;
                }
            }

            // TODO: 扫描自定义主题目录（未来扩展）
        }

        /// <summary>
        /// 从 ResourceDictionary 加载主题元数据
        /// </summary>
        private static ThemeMetadata LoadThemeMetadata(Uri source, string fallbackId)
        {
            try
            {
                var dict = new ResourceDictionary { Source = source };

                return new ThemeMetadata
                {
                    Id = dict.Contains("ThemeId") ? dict["ThemeId"] as string : fallbackId,
                    DisplayName = dict.Contains("ThemeDisplayName") ? dict["ThemeDisplayName"] as string : fallbackId,
                    Description = dict.Contains("ThemeDescription") ? dict["ThemeDescription"] as string : "",
                    Author = dict.Contains("ThemeAuthor") ? dict["ThemeAuthor"] as string : "Unknown",
                    Version = dict.Contains("ThemeVersion")
                        ? Version.Parse(dict["ThemeVersion"] as string)
                        : new Version("1.0.0"),
                    Source = source,
                    IsBuiltIn = true,
                    CreatedAt = DateTime.Now,
                    PreviewColors = new ThemePreviewColors
                    {
                        Primary = dict.Contains("PreviewPrimaryColor") ? dict["PreviewPrimaryColor"] as string : "#000000",
                        Background = dict.Contains("PreviewBackgroundColor") ? dict["PreviewBackgroundColor"] as string : "#FFFFFF",
                        Surface = dict.Contains("PreviewSurfaceColor") ? dict["PreviewSurfaceColor"] as string : "#F5F5F5",
                        TextPrimary = dict.Contains("PreviewTextColor") ? dict["PreviewTextColor"] as string : "#000000"
                    }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load theme metadata from {source}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有可用主题
        /// </summary>
        public static IEnumerable<ThemeMetadata> GetAvailableThemes()
        {
            return _themes.Values;
        }

        /// <summary>
        /// 获取当前主题
        /// </summary>
        public static ThemeMetadata GetCurrentTheme()
        {
            return _currentTheme;
        }

        /// <summary>
        /// 设置主题（支持动画）
        /// </summary>
        /// <param name="themeId">主题ID</param>
        /// <param name="animate">是否使用动画过渡</param>
        public static void SetTheme(string themeId, bool animate = true)
        {
            if (!_themes.ContainsKey(themeId))
            {
                throw new ArgumentException($"Theme '{themeId}' not found.");
            }

            var newTheme = _themes[themeId];

            if (animate && _currentTheme != null)
            {
                AnimateThemeTransition(() => ApplyTheme(newTheme));
            }
            else
            {
                ApplyTheme(newTheme);
            }
        }

        /// <summary>
        /// 应用主题
        /// </summary>
        private static void ApplyTheme(ThemeMetadata theme)
        {
            try
            {
                var newDict = new ResourceDictionary { Source = theme.Source };

                var appDictionaries = Application.Current.Resources.MergedDictionaries;
                var existingDict = appDictionaries.FirstOrDefault(d => d.Contains("AppBackgroundBrush"));

                if (existingDict != null)
                {
                    appDictionaries.Remove(existingDict);
                }

                appDictionaries.Add(newDict);

                var oldTheme = _currentTheme;
                _currentTheme = theme;

                ThemeChanged?.Invoke(null, new ThemeChangedEventArgs(oldTheme, theme));

                System.Diagnostics.Debug.WriteLine($"Theme applied: {theme.DisplayName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply theme '{theme.Id}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 主题切换动画（淡入淡出效果）
        /// </summary>
        private static void AnimateThemeTransition(Action applyAction)
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                applyAction();
                return;
            }

            // 淡出动画
            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                // 应用主题
                applyAction();

                // 淡入动画
                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                mainWindow.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            mainWindow.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        /// <summary>
        /// 切换主题（Light ↔ Dark）
        /// </summary>
        public static void ToggleTheme()
        {
            if (_currentTheme == null) return;

            var newThemeId = _currentTheme.Id == "Light" ? "Dark" : "Light";
            SetTheme(newThemeId, animate: true);
        }
    }

    /// <summary>
    /// 主题变更事件参数
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public ThemeMetadata OldTheme { get; }
        public ThemeMetadata NewTheme { get; }

        public ThemeChangedEventArgs(ThemeMetadata oldTheme, ThemeMetadata newTheme)
        {
            OldTheme = oldTheme;
            NewTheme = newTheme;
        }
    }
}
