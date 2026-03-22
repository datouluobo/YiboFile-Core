using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using YiboFile.Services.Core;

namespace YiboFile.Services.Localization
{
    public class LocalizationService : ILocalizationService
    {
        public event PropertyChangedEventHandler PropertyChanged;
        
        private Dictionary<string, string> _currentStrings = new Dictionary<string, string>();
        private Dictionary<string, string> _fallbackStrings = new Dictionary<string, string>();
        private readonly List<LanguageInfo> _availableLanguages = new List<LanguageInfo>();
        
        private readonly string _languagesPath;
        private const string DefaultLanguage = "zh-CN";

        public string CurrentLanguage { get; private set; }
        public IReadOnlyList<LanguageInfo> AvailableLanguages => _availableLanguages;

        public LocalizationService()
        {
            _languagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
            DiscoverLanguages();
            
            // 加载 Fallback 语言（默认 zh-CN）
            _fallbackStrings = LoadLanguageFile(DefaultLanguage);
            
            // 自动检测系统语言作为初始语言
            string systemLang = GetSystemLanguage();
            CurrentLanguage = systemLang;
            _currentStrings = LoadLanguageFile(systemLang);

            if (_currentStrings.Count == 0)
            {
                CurrentLanguage = DefaultLanguage;
                _currentStrings = _fallbackStrings;
            }
        }

        private string GetSystemLanguage()
        {
            try
            {
                var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
                if (culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
                {
                    return "zh-CN";
                }
            }
            catch { }
            return "en-US";
        }

        public string this[string key] => Get(key);

        public string Get(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            string template = null;
            if (_currentStrings.TryGetValue(key, out var val))
            {
                template = val;
            }
            else if (_fallbackStrings.TryGetValue(key, out var fallbackVal))
            {
                template = fallbackVal;
                // FileLogger.Log($"[i18n] Key missing in '{CurrentLanguage}': {key}");
            }
            else
            {
                template = $"[{key}]";
                // FileLogger.Log($"[i18n] Key missing globally: {key}");
            }

            if (args != null && args.Length > 0 && template != $"[{key}]")
            {
                try
                {
                    return string.Format(template, args);
                }
                catch
                {
                    return template;
                }
            }
            return template;
        }

        public void SetLanguage(string cultureCode)
        {
            if (string.IsNullOrEmpty(cultureCode)) return;
            if (cultureCode == CurrentLanguage && _currentStrings.Count > 0) return;

            var newStrings = LoadLanguageFile(cultureCode);
            if (newStrings.Count == 0 && cultureCode != DefaultLanguage)
            {
                FileLogger.Log($"[i18n] Failed to load language '{cultureCode}', falling back to {CurrentLanguage}.");
                return;
            }

            _currentStrings = newStrings;
            CurrentLanguage = cultureCode;
            
            // 通知所有绑定了索引器的UI元素刷新数据
            OnPropertyChanged(System.Windows.Data.Binding.IndexerName);
            OnPropertyChanged(nameof(CurrentLanguage));
        }

        private void DiscoverLanguages()
        {
            _availableLanguages.Clear();
            if (!Directory.Exists(_languagesPath))
            {
                try { Directory.CreateDirectory(_languagesPath); } catch { }
                return;
            }

            var files = Directory.GetFiles(_languagesPath, "*.json");
            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(json);
                    
                    string code = Path.GetFileNameWithoutExtension(file);
                    string displayName = code;
                    string nativeName = code;

                    if (doc.RootElement.TryGetProperty("_meta", out var metaElement))
                    {
                        if (metaElement.TryGetProperty("code", out var codeEle)) code = codeEle.GetString() ?? code;
                        if (metaElement.TryGetProperty("displayName", out var dpEle)) displayName = dpEle.GetString() ?? displayName;
                        if (metaElement.TryGetProperty("nativeName", out var nEle)) nativeName = nEle.GetString() ?? nativeName;
                    }

                    _availableLanguages.Add(new LanguageInfo(code, displayName, nativeName));
                }
                catch (Exception ex)
                {
                    FileLogger.LogException($"[i18n] Error discovering language file {file}", ex);
                }
            }
            
            if (_availableLanguages.Count == 0)
            {
                _availableLanguages.Add(new LanguageInfo(DefaultLanguage, "简体中文", "简体中文"));
            }
        }

        private Dictionary<string, string> LoadLanguageFile(string cultureCode)
        {
            var result = new Dictionary<string, string>();
            string filePath = Path.Combine(_languagesPath, $"{cultureCode}.json");
            
            if (!File.Exists(filePath))
            {
                return result;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(json);
                FlattenJsonElement(doc.RootElement, string.Empty, result);
            }
            catch (Exception ex)
            {
                FileLogger.LogException($"[i18n] Error parsing language file: {filePath}", ex);
            }

            return result;
        }

        private void FlattenJsonElement(JsonElement element, string prefix, Dictionary<string, string> dictionary)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == "_meta" && string.IsNullOrEmpty(prefix)) continue; // 跳过元数据
                    
                    string newPrefix = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    FlattenJsonElement(property.Value, newPrefix, dictionary);
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                dictionary[prefix] = element.GetString() ?? string.Empty;
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.InvokeAsync(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
