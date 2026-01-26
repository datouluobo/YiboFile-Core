using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using YiboFile.Models;

namespace YiboFile.Services.Theming
{
    /// <summary>
    /// 自定义主题管理器
    /// 负责自定义主题的创建、保存、加载和删除
    /// </summary>
    public static class CustomThemeManager
    {
        private static readonly string CustomThemesDirectory;
        private static List<CustomTheme> _cachedThemes;

        static CustomThemeManager()
        {
            CustomThemesDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YiboFile",
                "CustomThemes"
            );

            // 确保目录存在
            if (!Directory.Exists(CustomThemesDirectory))
            {
                Directory.CreateDirectory(CustomThemesDirectory);
            }
        }

        /// <summary>
        /// 加载所有自定义主题
        /// </summary>
        public static List<CustomTheme> LoadAll()
        {
            if (_cachedThemes != null)
            {
                return _cachedThemes;
            }

            _cachedThemes = new List<CustomTheme>();

            try
            {
                var files = Directory.GetFiles(CustomThemesDirectory, "*.json");
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
        public static void Save(CustomTheme theme)
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
                var filePath = Path.Combine(CustomThemesDirectory, fileName);

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
        public static void Delete(string themeId)
        {
            if (string.IsNullOrWhiteSpace(themeId))
                throw new ArgumentException("Theme ID cannot be empty");

            try
            {
                var fileName = $"{themeId}.json";
                var filePath = Path.Combine(CustomThemesDirectory, fileName);

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
        public static CustomTheme CreateFromCurrent(string name, string baseTheme)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Theme name cannot be empty");

            var colors = new Dictionary<string, string>();

            // 从当前应用程序资源中提取颜色
            var resourceDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString?.Contains($"{baseTheme}.xaml") == true);

            if (resourceDict != null)
            {
                // 提取28个核心颜色
                var colorKeys = GetCoreColorKeys();
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
        public static void Apply(CustomTheme theme)
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
                        var brush = new SolidColorBrush(color);
                        brush.Freeze(); // 冻结以提高性能

                        appResources[kvp.Key] = brush;
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
        public static CustomTheme GetTheme(string themeId)
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
                var filePath = Path.Combine(CustomThemesDirectory, fileName);

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
        public static void ClearOverrides()
        {
            try
            {
                var appResources = Application.Current.Resources;
                var keys = GetCoreColorKeys();

                foreach (var key in keys)
                {
                    if (appResources.Contains(key))
                    {
                        // 只有通过索引器赋值的本地值才能被Remove移除以恢复DynamicResource的查找链
                        // 注意：如果ResourceDictionary里也有这个key，Remove只会移除本地覆盖的值
                        appResources.Remove(key);
                    }
                }
            }
            catch (Exception)
            { }
        }

        /// <summary>
        /// 验证主题数据
        /// </summary>
        private static bool ValidateTheme(CustomTheme theme)
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
        /// 获取28个核心颜色键
        /// </summary>
        public static List<string> GetCoreColorKeys()
        {
            return new List<string>
            {
                // 1. 背景类 (4个)
                "BackgroundPrimaryBrush",
                "BackgroundSecondaryBrush",
                "BackgroundTertiaryBrush",
                "BackgroundElevatedBrush",
                
                // 2. 文本类 (4个)
                "ForegroundPrimaryBrush",
                "ForegroundSecondaryBrush",
                "ForegroundDisabledBrush",
                "ForegroundOnAccentBrush",
                
                // 3. 边框类 (3个)
                "BorderDefaultBrush",
                "BorderSubtleBrush",
                "BorderFocusBrush",
                
                // 4. 强调色/交互 (5个)
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
                
                // 6. 语义颜色 (4个)
                "SuccessBrush",
                "WarningBrush",
                "ErrorBrush",
                "InfoBrush",
                
                // 7. 特殊用途 (4个)
                "ShadowBrush",
                "OverlayBrush",
                "DividerBrush",
                "AppBackgroundBrush"
            };
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            _cachedThemes = null;
        }
    }
}

