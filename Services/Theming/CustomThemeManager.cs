using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Models;
using YiboFile.Services.Config;

namespace YiboFile.Services.Theming
{
    /// <summary>
    /// 自定义主题管理器
    /// 负责自定义主题的创建、保存、加载和删除
    /// </summary>
    public class CustomThemeManager
    {
        // 静态单例访问入口（兼容旧代码）
        public static CustomThemeManager Instance => YiboFile.App.ServiceProvider?.GetService<CustomThemeManager>() ?? new CustomThemeManager(null);

        private readonly string _customThemesDirectory;
        private List<CustomTheme> _cachedThemes;

        public CustomThemeManager(IConfigPathProvider pathProvider)
        {
            if (pathProvider == null)
            {
                pathProvider = new ConfigPathProvider();
            }
            
            _customThemesDirectory = pathProvider.CustomThemesDirectory;

            // 确保目录存在
            if (!Directory.Exists(_customThemesDirectory))
            {
                Directory.CreateDirectory(_customThemesDirectory);
            }
        }

        #region static Proxy Methods (Compatibility)

        public static List<CustomTheme> LoadAll() => Instance.LoadAllThemes();
        public static void Save(CustomTheme theme) => Instance.SaveTheme(theme);
        public static void Delete(string themeId) => Instance.DeleteTheme(themeId);
        public static CustomTheme CreateFromCurrent(string name, string baseTheme) => Instance.CreateThemeFromCurrent(name, baseTheme);
        public static void Apply(CustomTheme theme) => Instance.ApplyTheme(theme);
        public static CustomTheme GetTheme(string themeId) => Instance.GetThemeById(themeId);
        public static void ClearOverrides() => Instance.ClearThemeOverrides();
        public static void ClearCache() => Instance.ClearThemeCache();
        // GetCoreColorKeys 是纯逻辑，可以是静态的，或者也做代理
        public static List<string> GetCoreColorKeys() => Instance.GetCoreColorKeysList();

        #endregion

        #region Instance Methods

        /// <summary>
        /// 加载所有自定义主题
        /// </summary>
        public List<CustomTheme> LoadAllThemes()
        {
            if (_cachedThemes != null)
            {
                return _cachedThemes;
            }

            _cachedThemes = new List<CustomTheme>();

            try
            {
                var files = Directory.GetFiles(_customThemesDirectory, "*.json");
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var theme = JsonSerializer.Deserialize<CustomTheme>(json);
                        if (theme != null && ValidateTheme(theme))
                        {
                            _cachedThemes.Add(theme);
                        }
                    }
                    catch (Exception)
                    { }
                }
            }
            catch (Exception)
            { }

            return _cachedThemes;
        }

        /// <summary>
        /// 保存自定义主题
        /// </summary>
        public void SaveTheme(CustomTheme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            if (string.IsNullOrWhiteSpace(theme.Name))
                throw new ArgumentException("Theme name cannot be empty");

            if (!ValidateTheme(theme))
                throw new ArgumentException("Invalid theme data");

            try
            {
                theme.Touch();
                var fileName = $"{theme.Id}.json";
                var filePath = Path.Combine(_customThemesDirectory, fileName);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                var json = JsonSerializer.Serialize(theme, options);
                File.WriteAllText(filePath, json);

                // 更新缓存
                if (_cachedThemes != null)
                {
                    var existing = _cachedThemes.FirstOrDefault(t => t.Id == theme.Id);
                    if (existing != null)
                    {
                        _cachedThemes.Remove(existing);
                    }
                    _cachedThemes.Add(theme);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 删除自定义主题
        /// </summary>
        public void DeleteTheme(string themeId)
        {
            if (string.IsNullOrWhiteSpace(themeId))
                throw new ArgumentException("Theme ID cannot be empty");

            try
            {
                var fileName = $"{themeId}.json";
                var filePath = Path.Combine(_customThemesDirectory, fileName);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // 更新缓存
                if (_cachedThemes != null)
                {
                    _cachedThemes.RemoveAll(t => t.Id == themeId);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 从当前应用的主题创建自定义主题
        /// </summary>
        public CustomTheme CreateThemeFromCurrent(string name, string baseTheme)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Theme name cannot be empty");

            var colors = new Dictionary<string, string>();

            // 从当前应用程序资源中提取颜色
            var resourceDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString?.Contains($"{baseTheme}.xaml") == true);

            if (resourceDict != null)
            {
                // 提取 37 个全量核心语义令牌
                var colorKeys = GetCoreColorKeysList();
                foreach (var key in colorKeys)
                {
                    if (resourceDict.Contains(key))
                    {
                        var brush = resourceDict[key] as SolidColorBrush;
                        if (brush != null)
                        {
                            colors[key] = brush.Color.ToString();
                        }
                    }
                }
            }

            return CustomTheme.CreateFromBaseTheme(name, baseTheme, colors);
        }

        /// <summary>
        /// 应用自定义主题
        /// </summary>
        public void ApplyTheme(CustomTheme theme)
        {
            if (theme == null)
                throw new ArgumentNullException(nameof(theme));

            try
            {
                var appResources = Application.Current.Resources;

                // 应用每个自定义颜色
                foreach (var kvp in theme.Colors)
                {
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(kvp.Value);
                        
                        // 1. 应用 画刷 (Brush)
                        var brush = new SolidColorBrush(color);
                        brush.Freeze();
                        appResources[kvp.Key] = brush;

                        // 2. 自动应用 颜色 (Color)，用于支持从 BrushAliases.xaml 引用 .Color 属性
                        // 如果键本身就是 Color 类型 (如 AccentColor)，则不重复添加后缀
                        if (kvp.Key.EndsWith("Brush"))
                        {
                            var colorKey = kvp.Key + ".Color";
                            appResources[colorKey] = color;
                        }
                        // 兼容旧的命名方式
                        else if (kvp.Key == "AccentColor")
                        {
                            appResources["AccentDefaultBrush.Color"] = color;
                        }
                        else if (kvp.Key == "ForegroundPrimaryColor")
                        {
                            appResources["ForegroundPrimaryBrush.Color"] = color;
                        }
                    }
                    catch (Exception)
                    { }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 根据ID获取自定义主题
        /// </summary>
        public CustomTheme GetThemeById(string themeId)
        {
            // 1. 尝试从缓存获取
            if (_cachedThemes != null)
            {
                var cached = _cachedThemes.FirstOrDefault(t => t.Id == themeId);
                if (cached != null) return cached;
            }

            // 2. 尝试从磁盘加载
            try
            {
                var fileName = $"{themeId}.json";
                var filePath = Path.Combine(_customThemesDirectory, fileName);

                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var theme = JsonSerializer.Deserialize<CustomTheme>(json);
                    if (ValidateTheme(theme))
                    {
                        // 确保缓存已初始化
                        if (_cachedThemes == null) _cachedThemes = new List<CustomTheme>();
                        if (!_cachedThemes.Any(t => t.Id == theme.Id))
                        {
                            _cachedThemes.Add(theme);
                        }
                        return theme;
                    }
                }
            }
            catch (Exception)
            { }

            return null;
        }

        /// <summary>
        /// 清除所有自定义颜色覆盖（恢复使用ResourceDictionary定义的值）
        /// </summary>
        public void ClearThemeOverrides()
        {
            try
            {
                var appResources = Application.Current.Resources;
                var keys = GetCoreColorKeysList();

                foreach (var key in keys)
                {
                    if (appResources.Contains(key))
                    {
                        appResources.Remove(key);
                        
                        // 同时尝试移除对应的 .Color 键
                        if (key.EndsWith("Brush"))
                        {
                            appResources.Remove(key + ".Color");
                        }
                    }
                }
            }
            catch (Exception)
            { }
        }

        /// <summary>
        /// 验证主题数据
        /// </summary>
        private bool ValidateTheme(CustomTheme theme)
        {
            if (theme == null) return false;
            if (string.IsNullOrWhiteSpace(theme.Id)) return false;
            if (string.IsNullOrWhiteSpace(theme.Name)) return false;
            if (string.IsNullOrWhiteSpace(theme.BaseTheme)) return false;
            if (theme.Colors == null) return false;

            // 验证颜色格式
            foreach (var color in theme.Colors.Values)
            {
                try
                {
                    ColorConverter.ConvertFromString(color);
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取 37 个全量核心语义令牌键 (Semantic Tokens)
        /// </summary>
        public List<string> GetCoreColorKeysList()
        {
            return new List<string>
            {
                // 1. 背景类 (9个)
                "BackgroundPrimaryBrush",
                "BackgroundSecondaryBrush",
                "BackgroundTertiaryBrush",
                "BackgroundElevatedBrush",
                "TitleBarBackgroundBrush",
                "NavigationRegionBrush",
                "SidebarBackgroundBrush",
                "PaneFocusedBackgroundBrush",
                "PaneUnfocusedBackgroundBrush",
                
                // 2. 文本类 (6个)
                "ForegroundPrimaryColor",           // Color
                "ForegroundPrimaryBrush",
                "ForegroundSecondaryBrush",
                "ForegroundTertiaryBrush",
                "ForegroundDisabledBrush",
                "ForegroundOnAccentBrush",
                
                // 3. 边框类 (3个)
                "BorderDefaultBrush",
                "BorderSubtleBrush",
                "BorderFocusBrush",
                
                // 4. 强调色 (7个)
                "AccentColor",                    // Color
                "AccentHoverColor",               // Color
                "AccentDefaultBrush",
                "AccentHoverBrush",
                "AccentPressedBrush",
                "AccentSelectedBrush",
                "AccentLightBrush",
                
                // 5. 控件状态 (4个)
                "ControlDefaultBrush",
                "ControlHoverBrush",
                "ControlPressedBrush",
                "ControlDisabledBrush",
                
                // 6. 语义状态 (4个)
                "StatusSuccessBrush",
                "StatusWarningBrush",
                "StatusErrorBrush",
                "StatusInfoBrush",
                
                // 7. 特殊用途 (5个)
                "TransparentBrush",
                "OverlayBrush",
                "OverlayLightBrush",
                "DividerBrush",
                "ShadowBrush"
            };
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearThemeCache()
        {
            _cachedThemes = null;
        }

        #endregion
    }
}
