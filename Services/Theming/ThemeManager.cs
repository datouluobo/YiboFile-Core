using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32; // SystemEvents 和 Registry

using OoiMRR.Models;

namespace OoiMRR.Services.Theming
{
    /// <summary>
    /// 主题管理器 - 负责主题发现、加载和切换
    /// </summary>
    public class ThemeManager
    {
        private static readonly Dictionary<string, ThemeMetadata> _themes = new();
        private static ThemeMetadata _currentTheme;
        private static bool _isFollowingSystemTheme = false;

        /// <summary>
        /// 动画效果启用状态（从配置读取）
        /// </summary>
        public static bool AnimationsEnabled { get; set; } = true;

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
            try
            {
                // 尝试获取Themes目录下所有主题文件
                var themesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");

                // 自动扫描所有.xaml文件
                string[] themeFiles = null;
                if (Directory.Exists(themesPath))
                {
                    themeFiles = Directory.GetFiles(themesPath, "*.xaml");
                }
                else
                {
                    // 如果目录不存在，回退到已知主题列表
                    themeFiles = new[] { "Light", "Dark", "Ocean", "Forest", "Sunset", "Purple", "Nordic" }
                        .Select(id => $"Themes\\{id}.xaml").ToArray();
                }

                foreach (var filePath in themeFiles)
                {
                    try
                    {
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var uri = new Uri($"pack://application:,,,/Themes/{fileName}.xaml", UriKind.Absolute);
                        var metadata = LoadThemeMetadata(uri, fileName);
                        if (metadata != null)
                        {
                            _themes[fileName] = metadata;
                        }
                    }
                    catch (Exception ex)
                    {                    }
                }

                // 确保至少有Light和Dark主题
                if (!_themes.ContainsKey("Light"))
                {
                    var uri = new Uri("pack://application:,,,/Themes/Light.xaml", UriKind.Absolute);
                    var metadata = LoadThemeMetadata(uri, "Light");
                    if (metadata != null) _themes["Light"] = metadata;
                }
                if (!_themes.ContainsKey("Dark"))
                {
                    var uri = new Uri("pack://application:,,,/Themes/Dark.xaml", UriKind.Absolute);
                    var metadata = LoadThemeMetadata(uri, "Dark");
                    if (metadata != null) _themes["Dark"] = metadata;
                }
            }
            catch (Exception ex)
            {
                // 最小化回退：只加载Light和Dark
                var fallbackThemes = new[] { "Light", "Dark" };
                foreach (var themeId in fallbackThemes)
                {
                    try
                    {
                        var uri = new Uri($"pack://application:,,,/Themes/{themeId}.xaml", UriKind.Absolute);
                        var metadata = LoadThemeMetadata(uri, themeId);
                        if (metadata != null) _themes[themeId] = metadata;
                    }
                    catch { }
                }
            }
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
            {                return null;
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
        /// 根据ID自动判断是内置主题还是自定义主题
        /// </summary>
        /// <param name="themeId">主题ID</param>
        /// <param name="animate">是否使用动画过渡</param>
        public static void SetTheme(string themeId, bool animate = true)
        {
            // 1. 尝试内置主题
            if (_themes.ContainsKey(themeId))
            {
                // 在应用内置主题前，必须清除可能的自定义颜色覆盖
                // 确保从自定义主题切回内置主题时能恢复原样
                CustomThemeManager.ClearOverrides();

                var newTheme = _themes[themeId];

                // 考虑全局动画设置
                bool shouldAnimate = animate && AnimationsEnabled;

                if (shouldAnimate && _currentTheme != null)
                {
                    AnimateThemeTransition(() => ApplyTheme(newTheme));
                }
                else
                {
                    ApplyTheme(newTheme);
                }
                return;
            }

            // 2. 尝试自定义主题
            // 委托给CustomThemeManager处理
            var customTheme = CustomThemeManager.GetTheme(themeId);
            if (customTheme != null)
            {
                // 先应用基础主题（无动画），确保底层资源正确（如不可定制的画笔）
                // 递归调用自身会进入上面的内置主题分支，从而触发 ClearOverrides 和 ApplyTheme
                // 这为您提供了一个干净的底板，然后再应用自定义覆盖
                if (_themes.ContainsKey(customTheme.BaseTheme))
                {
                    SetTheme(customTheme.BaseTheme, animate: false);
                }

                // 应用自定义颜色覆盖 (直接修改 Application.Resources)
                CustomThemeManager.Apply(customTheme);

                // 构造一个临时的 Metadata 以更新 CurrentTheme
                var oldTheme = _currentTheme;
                var customThemeMetadata = new ThemeMetadata
                {
                    Id = customTheme.Id,
                    DisplayName = customTheme.Name,
                    Description = "用户自定义主题",
                    IsBuiltIn = false,
                    Source = null, // 自定义主题没有单一的Source文件
                    CreatedAt = customTheme.CreatedAt,
                    // 预览颜色可以从customTheme.Colors中提取，这里简化处理
                    PreviewColors = new ThemePreviewColors
                    {
                        Primary = customTheme.Colors.ContainsKey("AccentDefaultBrush") ? customTheme.Colors["AccentDefaultBrush"] : "#000000",
                        Background = customTheme.Colors.ContainsKey("BackgroundPrimaryBrush") ? customTheme.Colors["BackgroundPrimaryBrush"] : "#FFFFFF",
                        Surface = customTheme.Colors.ContainsKey("BackgroundSecondaryBrush") ? customTheme.Colors["BackgroundSecondaryBrush"] : "#F5F5F5",
                        TextPrimary = customTheme.Colors.ContainsKey("ForegroundPrimaryBrush") ? customTheme.Colors["ForegroundPrimaryBrush"] : "#000000"
                    }
                };

                _currentTheme = customThemeMetadata;
                ThemeChanged?.Invoke(null, new ThemeChangedEventArgs(oldTheme, customThemeMetadata));                return;
            }

            throw new ArgumentException($"Theme '{themeId}' not found.");
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

                ThemeChanged?.Invoke(null, new ThemeChangedEventArgs(oldTheme, theme));            }
            catch (Exception ex)
            {                throw;
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

        /// <summary>
        /// 切换图标风格
        /// </summary>
        /// <param name="styleName">E.g., "Emoji", "Remix", "Fluent"</param>
        public static void ChangeIconStyle(string styleName)
        {
            try
            {
                // Use Relative URI to ensure consistent BAML loading behavior with App.xaml
                var uri = new Uri($"/Resources/Icons/Icons.{styleName}.xaml", UriKind.Relative);
                var newDict = new ResourceDictionary { Source = uri };

                var appDictionaries = Application.Current.Resources.MergedDictionaries;

                // 查找并替换旧的图标资源字典
                // 我们通过查找是否包含 "IconFontFamily" 键来识别图标字典
                var existingDict = appDictionaries.FirstOrDefault(d => d.Contains("IconFontFamily"));

                if (existingDict != null)
                {
                    appDictionaries.Remove(existingDict);
                }

                appDictionaries.Add(newDict);            }
            catch (Exception ex)
            {            }
        }

        #region 系统主题跟随

        /// <summary>
        /// 启用系统主题跟随
        /// </summary>
        public static void EnableSystemThemeFollowing()
        {
            if (_isFollowingSystemTheme)
            {                return;
            }

            _isFollowingSystemTheme = true;

            // 立即应用当前系统主题
            try
            {
                var systemTheme = DetectSystemTheme();
                SetTheme(systemTheme, animate: false);            }
            catch (Exception ex)
            {            }

            // 监听系统主题变化
            SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        }

        /// <summary>
        /// 禁用系统主题跟随
        /// </summary>
        public static void DisableSystemThemeFollowing()
        {
            if (!_isFollowingSystemTheme)
            {
                return;
            }

            _isFollowingSystemTheme = false;
            SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;        }

        /// <summary>
        /// 系统偏好设置变化事件处理
        /// </summary>
        private static void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // 只响应主题相关的变化
            if (e.Category == UserPreferenceCategory.General)
            {
                try
                {
                    var newTheme = DetectSystemTheme();
                    var currentThemeId = _currentTheme?.Id;

                    if (newTheme != currentThemeId)
                    {
                        // 在UI线程上切换主题
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            SetTheme(newTheme, animate: true);
                        }));
                    }
                }
                catch (Exception ex)
                {                }
            }
        }

        /// <summary>
        /// 检测Windows系统主题
        /// </summary>
        /// <returns>主题ID（"Light" 或 "Dark"）</returns>
        public static string DetectSystemTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("AppsUseLightTheme");
                        if (value is int intValue)
                        {
                            // 0 = Dark, 1 = Light
                            var theme = intValue == 0 ? "Dark" : "Light";                            return theme;
                        }
                    }
                }
            }
            catch (Exception ex)
            {            }

            // 默认返回浅色主题
            return "Light";
        }

        #endregion
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
