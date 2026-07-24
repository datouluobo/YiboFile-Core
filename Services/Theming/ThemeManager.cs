using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;
using Microsoft.Win32;
using YiboFile.Models;

namespace YiboFile.Services.Theming
{
    public class ThemeManager : IThemeService
    {
        private readonly Dictionary<string, ThemeMetadata> _themes = new();
        private readonly Dictionary<string, UIStyleMetadata> _uiStyles = new();
        private readonly Dictionary<string, TabStyleMetadata> _tabStyles = new();
        private readonly Dictionary<string, IconStyleMetadata> _iconStyles = new();
        
        private ThemeMetadata _currentTheme;
        private UIStyleMetadata _currentUIStyle;
        private TabStyleMetadata _currentTabStyle;
        private IconStyleMetadata _currentIconStyle;
        private bool _isFollowingSystemTheme = false;

        public bool AnimationsEnabled { get; set; } = true;

        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;
        public event EventHandler<UIStyleChangedEventArgs> UIStyleChanged;
        public event EventHandler<TabStyleChangedEventArgs> TabStyleChanged;
        public event EventHandler<IconStyleChangedEventArgs> IconStyleChanged;

        public ThemeMetadata CurrentTheme => _currentTheme;
        public IReadOnlyList<ThemeMetadata> AvailableThemes => _themes.Values.ToList();
        
        public UIStyleMetadata CurrentUIStyle => _currentUIStyle;
        public IReadOnlyList<UIStyleMetadata> AvailableUIStyles => _uiStyles.Values.ToList();

        public TabStyleMetadata CurrentTabStyle => _currentTabStyle;
        public IReadOnlyList<TabStyleMetadata> AvailableTabStyles => _tabStyles.Values.ToList();
        
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
            var themes = new[] { "Light", "Dark", "Ocean", "Forest", "Sunset", "Purple", "Nordic", "FluentMica", "Win11Pro", "Tesla", "TeslaDark", "Spotify", "test1" };
            foreach (var t in themes)
            {
                var uri = new Uri($"pack://application:,,,/YiboFile-Core;component/Styles/Themes/{t}.xaml", UriKind.Absolute);
                var meta = LoadThemeMetadata(uri, t);
                if (meta != null) _themes[t] = meta;
            }

            var uiStyles = new[] { "Original", "Fluent", "MacOS", "Geek", "OneCommander", "Antigravity", "Tesla", "Spotify", "test1" };
            foreach (var u in uiStyles)
            {
                var uri = new Uri($"pack://application:,,,/YiboFile-Core;component/Styles/UIStyles/{u}.xaml", UriKind.Absolute);
                var meta = LoadUIStyleMetadata(uri, u);
                if (meta != null) _uiStyles[u] = meta;
            }

            var tabStyles = new[] { "Original", "StrongUnderline", "PagePlate", "TopRail", "PinUnderline", "SegmentSlot", "CompactChip", "RailBlock", "Blueprint", "test1" };
            foreach (var tab in tabStyles)
            {
                var uri = new Uri($"pack://application:,,,/YiboFile-Core;component/Styles/TabStyles/{tab}.xaml", UriKind.Absolute);
                var meta = LoadTabStyleMetadata(uri, tab);
                if (meta != null) _tabStyles[tab] = meta;
            }

            var icons = new[] { "Emoji", "Fluent", "Material", "Remix", "Lucide", "Pixel", "Prism", "Tabler", "Phosphor", "Tesla", "Spotify", "test1" };
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

        private TabStyleMetadata LoadTabStyleMetadata(Uri source, string fallbackId)
        {
            try
            {
                var dict = new ResourceDictionary { Source = source };
                return new TabStyleMetadata
                {
                    Id = dict.Contains("TabStyleId") ? dict["TabStyleId"] as string : fallbackId,
                    DisplayName = dict.Contains("TabStyleDisplayName") ? dict["TabStyleDisplayName"] as string : fallbackId,
                    Description = dict.Contains("TabStyleDescription") ? dict["TabStyleDescription"] as string : "",
                    Source = source,
                    ActiveBackground = GetBrush(dict, "TabStylePreview.ActiveBackground", GetBrush(dict, "UI.TabItem.ActiveBackground", Brushes.Transparent)),
                    ActiveBorderBrush = GetBrush(dict, "TabStylePreview.ActiveBorderBrush", GetBrush(dict, "UI.TabItem.ActiveBorderBrush", Brushes.Transparent)),
                    ActiveIndicatorBrush = GetBrush(dict, "TabStylePreview.ActiveIndicatorBrush", GetBrush(dict, "UI.TabItem.ActiveIndicatorColor", Brushes.Transparent)),
                    ActiveSideIndicatorBrush = GetBrush(dict, "TabStylePreview.ActiveSideIndicatorBrush", GetBrush(dict, "UI.TabItem.ActiveSideIndicatorColor", Brushes.Transparent)),
                    ActiveBorderThickness = GetThickness(dict, "UI.TabItem.ActiveBorderThickness", new Thickness(0)),
                    Margin = GetThickness(dict, "UI.TabItem.Margin", new Thickness(0)),
                    ActiveIndicatorMargin = GetThickness(dict, "UI.TabItem.ActiveIndicatorMargin", new Thickness(0)),
                    ActiveSideIndicatorMargin = GetThickness(dict, "UI.TabItem.ActiveSideIndicatorMargin", new Thickness(0)),
                    CornerRadius = GetCornerRadius(dict, "UI.TabItem.CornerRadius", new CornerRadius(0)),
                    ActiveSideIndicatorRadius = GetCornerRadius(dict, "UI.TabItem.ActiveSideIndicatorRadius", new CornerRadius(0)),
                    Height = GetDouble(dict, "UI.TabItem.Height", 34),
                    ActiveIndicatorHeight = GetDouble(dict, "UI.TabItem.ActiveIndicatorHeight", 2),
                    ActiveIndicatorRadius = GetDouble(dict, "UI.TabItem.ActiveIndicatorRadius", 0),
                    ActiveSideIndicatorWidth = GetDouble(dict, "UI.TabItem.ActiveSideIndicatorWidth", 0),
                    ActiveIndicatorPosition = dict.Contains("UI.TabItem.ActiveIndicatorPosition") && dict["UI.TabItem.ActiveIndicatorPosition"] is VerticalAlignment pos ? pos : VerticalAlignment.Bottom,
                    ActiveIndicatorVisibility = dict.Contains("UI.TabItem.ActiveIndicatorVisibility") && dict["UI.TabItem.ActiveIndicatorVisibility"] is Visibility visibility ? visibility : Visibility.Visible,
                    ActiveSideIndicatorVisibility = dict.Contains("UI.TabItem.ActiveSideIndicatorVisibility") && dict["UI.TabItem.ActiveSideIndicatorVisibility"] is Visibility sideVisibility ? sideVisibility : Visibility.Collapsed,
                    ActiveFontWeight = dict.Contains("UI.TabItem.ActiveFontWeight") && dict["UI.TabItem.ActiveFontWeight"] is FontWeight weight ? weight : FontWeights.SemiBold
                };
            }
            catch { return null; }
        }

        private static Brush GetBrush(ResourceDictionary dict, string key, Brush fallback)
        {
            return dict.Contains(key) && dict[key] is Brush brush ? brush : fallback;
        }

        private static double GetDouble(ResourceDictionary dict, string key, double fallback)
        {
            return dict.Contains(key) && dict[key] is double value ? value : fallback;
        }

        private static Thickness GetThickness(ResourceDictionary dict, string key, Thickness fallback)
        {
            return dict.Contains(key) && dict[key] is Thickness value ? value : fallback;
        }

        private static CornerRadius GetCornerRadius(ResourceDictionary dict, string key, CornerRadius fallback)
        {
            return dict.Contains(key) && dict[key] is CornerRadius value ? value : fallback;
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
                ReloadAliases(); // Ensure aliases catch the local custom theme overrides
                
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

        public void SetTabStyle(string styleId)
        {
            if (_tabStyles.ContainsKey(styleId))
            {
                var source = new Uri($"pack://application:,,,/YiboFile-Core;component/Styles/TabStyles/{styleId}.xaml", UriKind.Absolute);
                ApplyDictionary(source, "/Styles/TabStyles/");
                var old = _currentTabStyle;
                _currentTabStyle = LoadTabStyleMetadata(source, styleId) ?? _tabStyles[styleId];
                _tabStyles[styleId] = _currentTabStyle;
                TabStyleChanged?.Invoke(this, new TabStyleChangedEventArgs(old, _currentTabStyle));
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

        public void ReloadAliases()
        {
            try {
                var (parent, aliasDicts) = FindDictionariesRecursive(
                    Application.Current.Resources.MergedDictionaries, "/Styles/Aliases/");
                
                if (parent == null || aliasDicts.Count == 0) return;

                foreach (var alias in aliasDicts)
                {
                    int idx = parent.IndexOf(alias);
                    var newAlias = new ResourceDictionary { Source = alias.Source };
                    parent.Remove(alias);
                    if (idx >= 0 && idx <= parent.Count)
                        parent.Insert(idx, newAlias);
                    else
                        parent.Add(newAlias);
                }
            } catch { } // Ignore errors during alias swap
        }

        private void ApplyDictionary(Uri source, string identifierToReplace)
        {
            try {
                var newDict = new ResourceDictionary { Source = source };

                YiboFile.Services.Core.FileLogger.Log($"[ThemeManager] ApplyDictionary: {source}, identifier={identifierToReplace}");

                // 查找现有的对应字典及其所在的父集合
                var (parentCollection, existingDicts) = FindDictionariesRecursive(
                    Application.Current.Resources.MergedDictionaries, identifierToReplace);

                if (parentCollection != null && existingDicts.Count > 0)
                {
                    // 在找到旧字典的同一个父集合中进行原位替换
                    // 记录第一个匹配字典的索引，新字典将插入到该位置
                    int insertIndex = -1;
                    foreach (var dict in existingDicts)
                    {
                        int idx = parentCollection.IndexOf(dict);
                        if (insertIndex < 0 || (idx >= 0 && idx < insertIndex))
                            insertIndex = idx;
                        YiboFile.Services.Core.FileLogger.Log($"[ThemeManager] Removing: {dict.Source} at index {idx}");
                        parentCollection.Remove(dict);
                    }

                    // 由于 Remove 会导致索引偏移，确保 insertIndex 有效
                    if (insertIndex < 0 || insertIndex > parentCollection.Count)
                        insertIndex = parentCollection.Count;

                    parentCollection.Insert(insertIndex, newDict);
                    YiboFile.Services.Core.FileLogger.Log($"[ThemeManager] Inserted {newDict.Source} at index {insertIndex} in parent collection (count={parentCollection.Count})");
                }
                else
                {
                    // 如果没有找到旧字典，找到最内层的 MergedDictionaries 并添加
                    var target = Application.Current.Resources.MergedDictionaries;
                    // 如果顶层只有一个匿名字典容器（App.xaml 的结构），则深入一层
                    if (target.Count == 1 && target[0].Source == null && target[0].MergedDictionaries.Count > 0)
                    {
                        target = target[0].MergedDictionaries;
                    }
                    target.Add(newDict);
                    YiboFile.Services.Core.FileLogger.Log($"[ThemeManager] No existing dict found. Added {newDict.Source} to inner collection (count={target.Count})");
                }

                if (identifierToReplace.Contains("/Styles/Themes/") || identifierToReplace.Contains("/Styles/Contracts/"))
                {
                    ReloadDependentDictionaries();
                }
            } catch (Exception ex) {
                YiboFile.Services.Core.FileLogger.LogException($"Failed to load {source}", ex);
            }
        }

        /// <summary>
        /// 递归查找嵌套 MergedDictionaries 中匹配 identifier 的字典及其父集合
        /// </summary>
        private (System.Collections.ObjectModel.Collection<ResourceDictionary> parentCollection, List<ResourceDictionary> matches) 
            FindDictionariesRecursive(System.Collections.ObjectModel.Collection<ResourceDictionary> dictionaries, string identifier)
        {
            // 先在当前层级查找
            var matches = dictionaries
                .Where(d => d.Source != null 
                    && d.Source.OriginalString.Contains(identifier)
                    && !d.Source.OriginalString.Contains("Contract", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count > 0)
            {
                return (dictionaries, matches);
            }

            // 递归搜索子级
            foreach (var dict in dictionaries)
            {
                if (dict.MergedDictionaries.Count > 0)
                {
                    var result = FindDictionariesRecursive(dict.MergedDictionaries, identifier);
                    if (result.matches.Count > 0)
                    {
                        return result;
                    }
                }
            }

            return (null, new List<ResourceDictionary>());
        }

        /// <summary>
        /// 主题切换后，强制重新加载依赖主题颜色的字典（Aliases、UIStyles、TabStyles、Icons）
        /// </summary>
        private void ReloadDependentDictionaries()
        {
            var identifiers = new[] { "/Styles/Aliases/", "/Styles/UIStyles/", "/Styles/TabStyles/", "/Styles/Icons/" };
            foreach (var id in identifiers)
            {
                var (parent, targets) = FindDictionariesRecursive(
                    Application.Current.Resources.MergedDictionaries, id);
                
                if (parent == null || targets.Count == 0) continue;

                foreach (var dictToReload in targets)
                {
                    // 跳过合约字典
                    if (dictToReload.Source.OriginalString.Contains("Contract", StringComparison.OrdinalIgnoreCase))
                        continue;

                    int idx = parent.IndexOf(dictToReload);
                    var newDictInstance = new ResourceDictionary { Source = dictToReload.Source };
                    parent.Remove(dictToReload);
                    
                    // 核心修复: 在同一父集合中原位替换，不跨层级
                    if (idx >= 0 && idx <= parent.Count)
                        parent.Insert(idx, newDictInstance);
                    else
                        parent.Add(newDictInstance);
                }
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
