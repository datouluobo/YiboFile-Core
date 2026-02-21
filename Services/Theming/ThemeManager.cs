using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using YiboFile.Models;

namespace YiboFile.Services.Theming
{
    public class ThemeManager : IThemeService
    {
        private readonly Dictionary<string, ThemeMetadata> _themes = new();
        private readonly Dictionary<string, UIStyleMetadata> _uiStyles = new();
        private readonly Dictionary<string, IconStyleMetadata> _iconStyles = new();
        
        private ThemeMetadata _currentTheme;
        private UIStyleMetadata _currentUIStyle;
        private IconStyleMetadata _currentIconStyle;
        private bool _isFollowingSystemTheme = false;

        public bool AnimationsEnabled { get; set; } = true;

        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;
        public event EventHandler<UIStyleChangedEventArgs> UIStyleChanged;
        public event EventHandler<IconStyleChangedEventArgs> IconStyleChanged;

        public ThemeMetadata CurrentTheme => _currentTheme;
        public IReadOnlyList<ThemeMetadata> AvailableThemes => _themes.Values.ToList();
        
        public UIStyleMetadata CurrentUIStyle => _currentUIStyle;
        public IReadOnlyList<UIStyleMetadata> AvailableUIStyles => _uiStyles.Values.ToList();
        
        public IconStyleMetadata CurrentIconStyle => _currentIconStyle;
        public IReadOnlyList<IconStyleMetadata> AvailableIconStyles => _iconStyles.Values.ToList();
        
        public bool IsFollowingSystemTheme => _isFollowingSystemTheme;
        
        public IReadOnlyList<CustomTheme> CustomThemes => CustomThemeManager.LoadAll();

        public ThemeManager()
        {
            DiscoverResources();
        }

        private void DiscoverResources()
        {
            var themes = new[] { "Light", "Dark", "Ocean", "Forest", "Sunset", "Purple", "Nordic" };
            foreach (var t in themes)
            {
                var uri = new Uri($"pack://application:,,,/YiboFile-Core;component/Styles/Themes/{t}.xaml", UriKind.Absolute);
                var meta = LoadThemeMetadata(uri, t);
                if (meta != null) _themes[t] = meta;
            }

            var uiStyles = new[] { "Original", "Fluent", "MacOS", "Geek" };
            foreach (var u in uiStyles)
            {
                var uri = new Uri($"pack://application:,,,/YiboFile-Core;component/Styles/UIStyles/{u}.xaml", UriKind.Absolute);
                var meta = LoadUIStyleMetadata(uri, u);
                if (meta != null) _uiStyles[u] = meta;
            }

            var icons = new[] { "Emoji", "Fluent", "Material", "Remix" };
            foreach (var i in icons)
            {
                var uri = new Uri($"pack://application:,,,/YiboFile-Core;component/Styles/Icons/{i}.xaml", UriKind.Absolute);
                var meta = LoadIconStyleMetadata(uri, i);
                if (meta != null) _iconStyles[i] = meta;
            }
        }

        private ThemeMetadata LoadThemeMetadata(Uri source, string fallbackId)
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
                    Version = dict.Contains("ThemeVersion") ? Version.Parse(dict["ThemeVersion"] as string) : new Version("1.0.0"),
                    Source = source,
                    IsBuiltIn = true,
                    CreatedAt = DateTime.Now,
                    PreviewColors = new ThemePreviewColors
                    {
                        Primary = dict.Contains("PreviewPrimaryColor") ? dict["PreviewPrimaryColor"] as string : "#000000",
                        Background = dict.Contains("BackgroundPrimaryBrush") ? ((System.Windows.Media.SolidColorBrush)dict["BackgroundPrimaryBrush"]).Color.ToString() : "#FFFFFF",
                        Surface = dict.Contains("BackgroundSecondaryBrush") ? ((System.Windows.Media.SolidColorBrush)dict["BackgroundSecondaryBrush"]).Color.ToString() : "#F5F5F5",
                        TextPrimary = dict.Contains("ForegroundPrimaryColor") ? dict["ForegroundPrimaryColor"].ToString() : "#000000"
                    }
                };
            }
            catch { return null; }
        }

        private UIStyleMetadata LoadUIStyleMetadata(Uri source, string fallbackId)
        {
            try
            {
                var dict = new ResourceDictionary { Source = source };
                return new UIStyleMetadata
                {
                    Id = dict.Contains("UIStyleId") ? dict["UIStyleId"] as string : fallbackId,
                    DisplayName = dict.Contains("UIStyleDisplayName") ? dict["UIStyleDisplayName"] as string : fallbackId,
                    Description = dict.Contains("UIStyleDescription") ? dict["UIStyleDescription"] as string : ""
                };
            }
            catch { return null; }
        }

        private IconStyleMetadata LoadIconStyleMetadata(Uri source, string fallbackId)
        {
            try
            {
                var dict = new ResourceDictionary { Source = source };
                return new IconStyleMetadata
                {
                    Id = dict.Contains("IconStyleId") ? dict["IconStyleId"] as string : fallbackId,
                    DisplayName = dict.Contains("IconStyleDisplayName") ? dict["IconStyleDisplayName"] as string : fallbackId,
                    Description = dict.Contains("IconStyleDescription") ? dict["IconStyleDescription"] as string : ""
                };
            }
            catch { return null; }
        }

        public void SetTheme(string themeId, bool animate = true)
        {
            if (_themes.ContainsKey(themeId))
            {
                CustomThemeManager.ClearOverrides();
                var newTheme = _themes[themeId];
                if (animate && AnimationsEnabled && _currentTheme != null)
                {
                    AnimateTransition(() => ApplyDictionary(newTheme.Source, "/Styles/Themes/"));
                }
                else
                {
                    ApplyDictionary(newTheme.Source, "/Styles/Themes/");
                }
                var old = _currentTheme;
                _currentTheme = newTheme;
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(old, newTheme, false));
                return;
            }

            var customTheme = CustomThemeManager.GetTheme(themeId);
            if (customTheme != null)
            {
                if (_themes.ContainsKey(customTheme.BaseTheme))
                {
                    SetTheme(customTheme.BaseTheme, false);
                }
                CustomThemeManager.Apply(customTheme);
                
                var oldTheme = _currentTheme;
                var customThemeMetadata = new ThemeMetadata
                {
                    Id = customTheme.Id,
                    DisplayName = customTheme.Name,
                    Description = "用户自定义主题",
                    IsBuiltIn = false,
                    Source = null,
                    CreatedAt = customTheme.CreatedAt,
                    PreviewColors = new ThemePreviewColors
                    {
                        Primary = customTheme.Colors.ContainsKey("AccentDefaultBrush") ? customTheme.Colors["AccentDefaultBrush"] : "#000000",
                        Background = customTheme.Colors.ContainsKey("BackgroundPrimaryBrush") ? customTheme.Colors["BackgroundPrimaryBrush"] : "#FFFFFF",
                        Surface = customTheme.Colors.ContainsKey("BackgroundSecondaryBrush") ? customTheme.Colors["BackgroundSecondaryBrush"] : "#F5F5F5",
                        TextPrimary = customTheme.Colors.ContainsKey("ForegroundPrimaryBrush") ? customTheme.Colors["ForegroundPrimaryBrush"] : "#000000"
                    }
                };
                _currentTheme = customThemeMetadata;
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(oldTheme, customThemeMetadata, true));
                return;
            }
            throw new ArgumentException($"Theme '{themeId}' not found.");
        }

        public void ToggleTheme()
        {
            if (_currentTheme == null) return;
            var newThemeId = _currentTheme.Id == "Light" ? "Dark" : "Light";
            SetTheme(newThemeId, true);
        }

        public void SetUIStyle(string styleId)
        {
            if (_uiStyles.ContainsKey(styleId))
            {
                var source = new Uri($"pack://application:,,,/YiboFile-Core;component/Styles/UIStyles/{styleId}.xaml", UriKind.Absolute);
                ApplyDictionary(source, "/Styles/UIStyles/");
                var old = _currentUIStyle;
                _currentUIStyle = _uiStyles[styleId];
                UIStyleChanged?.Invoke(this, new UIStyleChangedEventArgs(old, _currentUIStyle));
            }
        }

        public void SetIconStyle(string styleId)
        {
            if (_iconStyles.ContainsKey(styleId))
            {
                var source = new Uri($"pack://application:,,,/YiboFile-Core;component/Styles/Icons/{styleId}.xaml", UriKind.Absolute);
                ApplyDictionary(source, "/Styles/Icons/");
                var old = _currentIconStyle;
                _currentIconStyle = _iconStyles[styleId];
                IconStyleChanged?.Invoke(this, new IconStyleChangedEventArgs(old, _currentIconStyle));
            }
        }

        private void ApplyDictionary(Uri source, string identifierToReplace)
        {
            try {
                var newDict = new ResourceDictionary { Source = source };
                var appDictionaries = Application.Current.Resources.MergedDictionaries;
                var existingDicts = appDictionaries.Where(d => d.Source != null && d.Source.OriginalString.Contains(identifierToReplace)).ToList();
                
                int indexToInsert = 0;
                if (existingDicts.Any())
                {
                    indexToInsert = appDictionaries.IndexOf(existingDicts.First());
                }
                
                appDictionaries.Insert(indexToInsert, newDict);
                foreach (var dict in existingDicts)
                {
                    appDictionaries.Remove(dict);
                }
            } catch (Exception ex) {
                YiboFile.Services.Core.FileLogger.LogException($"Failed to load {source}", ex);
            }
        }

        private void AnimateTransition(Action applyAction)
        {
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow == null)
            {
                applyAction();
                return;
            }
            var fadeOut = new DoubleAnimation { From = 1.0, To = 0.0, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fadeOut.Completed += (s, e) =>
            {
                applyAction();
                var fadeIn = new DoubleAnimation { From = 0.0, To = 1.0, Duration = TimeSpan.FromMilliseconds(150), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                mainWindow.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };
            mainWindow.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        public void EnableSystemThemeFollowing()
        {
            if (_isFollowingSystemTheme) return;
            _isFollowingSystemTheme = true;
            try { SetTheme(DetectSystemTheme(), false); } catch { }
            SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
        }

        public void DisableSystemThemeFollowing()
        {
            if (!_isFollowingSystemTheme) return;
            _isFollowingSystemTheme = false;
            SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
        }

        private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General)
            {
                try
                {
                    var newTheme = DetectSystemTheme();
                    if (newTheme != _currentTheme?.Id)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action(() => SetTheme(newTheme, true)));
                    }
                }
                catch { }
            }
        }

        public string DetectSystemTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key?.GetValue("AppsUseLightTheme") is int val) return val == 0 ? "Dark" : "Light";
            }
            catch { }
            return "Light";
        }

        public CustomTheme CreateCustomTheme(string name, string baseTheme) => CustomThemeManager.CreateFromCurrent(name, baseTheme);
        public void SaveCustomTheme(CustomTheme theme) => CustomThemeManager.Save(theme);
        public void DeleteCustomTheme(string themeId) => CustomThemeManager.Delete(themeId);
        public void ApplyCustomTheme(CustomTheme theme) => SetTheme(theme.Id, false);

        public ContractValidationResult ValidateAll()
        {
            var allMissing = new List<string>();
            foreach(var t in _themes.Values.Where(x => x.Source!=null))
            {
                try {
                var dict = new ResourceDictionary{Source = t.Source};
                var r = ContractValidator.Validate(dict, ContractValidator.ThemeRequiredKeys);
                allMissing.AddRange(r.MissingKeys.Select(k=> t.Id + "." + k));
                } catch { }
            }
            return new ContractValidationResult { IsValid = allMissing.Count == 0, MissingKeys = allMissing };
        }
    }
}
