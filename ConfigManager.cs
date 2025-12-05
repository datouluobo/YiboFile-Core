using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OoiMRR
{
    public class AppConfig
    {
        public string LastPath { get; set; } = string.Empty;
        public string LastNavigationMode { get; set; } = "Path"; // Path, Library, Tag, Search
        public int LastLibraryId { get; set; } = 0; // 最后选中的库ID
        public string TagTrainDataDirectory { get; set; } = string.Empty; // 持久化 TT 数据目录
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public double WindowTop { get; set; } = double.NaN;
        public double WindowLeft { get; set; } = double.NaN;
        public bool IsMaximized { get; set; } = true;
        
        // 主布局列宽度（列1和列2）
        public double ColLeftWidth { get; set; } = 220; // 列1（左侧导航区）宽度
        public double ColCenterWidth { get; set; } = 0; // 列2（中间文件浏览器）宽度，0表示使用Star模式
        
        // 兼容旧版本的属性名
        public double LeftPanelWidth { get => ColLeftWidth; set => ColLeftWidth = value; }
        public double MiddlePanelWidth { get => ColCenterWidth; set => ColCenterWidth = value; }
        
        // 列头宽度
        public double ColNameWidth { get; set; } = 200;
        public double ColSizeWidth { get; set; } = 100;
        public double ColTypeWidth { get; set; } = 100;
        public double ColModifiedDateWidth { get; set; } = 150;
        public double ColCreatedTimeWidth { get; set; } = 50;
        public double ColTagsWidth { get; set; } = 150;
        public double ColNotesWidth { get; set; } = 200;
        
        // 列头顺序
        public string ColumnOrder { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";

        // 按模式存储可见列（CSV）
        public string VisibleColumns_Path { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";
        public string VisibleColumns_Library { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";
        public string VisibleColumns_Tag { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";

        public System.Collections.Generic.Dictionary<string, string> TabTitleOverrides { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
        public System.Collections.Generic.List<string> PinnedTabs { get; set; } = new System.Collections.Generic.List<string>();
        public double PinnedTabWidth { get; set; } = 90;
        
        // 字体设置
        public double UIFontSize { get; set; } = 16; // 界面字体大小（默认16）
        public double TagFontSize { get; set; } = 16; // Tag字体大小（默认16）
        public double TagBoxWidth { get; set; } = 0; // Tag框宽度（0表示自动计算，>0表示固定宽度）
        public double TagWidth { get; set; } = 120; // Tag框宽度（默认120）
    }

    public class AllSettingsConfig
    {
        public AppConfig OoiMRRConfig { get; set; } = new AppConfig();
        public Dictionary<string, string> TagTrainSettings { get; set; } = new Dictionary<string, string>();
    }

    public static class ConfigManager
    {
        private static readonly string ConfigDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OoiMRR");
        private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "config.json");

        public static string GetConfigDirectory()
        {
            return ConfigDirectory;
        }

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    return cfg ?? new AppConfig();
                }
            }
            catch
            {
                // ignore and return defaults
            }
            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            try
            {
                if (!Directory.Exists(ConfigDirectory)) Directory.CreateDirectory(ConfigDirectory);
                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // ignore disk errors for now
            }
        }

        public static void Export(string targetFile)
        {
            var current = Load();
            var json = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(targetFile, json);
        }

        public static void Import(string sourceFile)
        {
            if (!File.Exists(sourceFile)) return;
            var json = File.ReadAllText(sourceFile);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            Save(cfg);
        }

        public static void ExportAllSettings(string targetFile)
        {
            try
            {
                var allSettings = new AllSettingsConfig
                {
                    OoiMRRConfig = Load()
                };
                
                // 导出TagTrain设置
                if (App.IsTagTrainAvailable)
                {
                    try
                    {
                        TagTrain.Services.SettingsManager.ClearCache();
                        var settingsPath = TagTrain.Services.SettingsManager.GetSettingsFilePath();
                        if (File.Exists(settingsPath))
                        {
                            var lines = File.ReadAllLines(settingsPath);
                            foreach (var line in lines)
                            {
                                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                                    continue;
                                var parts = line.Split(new[] { '=' }, 2);
                                if (parts.Length == 2)
                                {
                                    allSettings.TagTrainSettings[parts[0].Trim()] = parts[1].Trim();
                                }
                            }
                        }
                    }
                    catch { }
                }
                
                var json = JsonSerializer.Serialize(allSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(targetFile, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"导出设置失败: {ex.Message}", ex);
            }
        }

        public static void ImportAllSettings(string sourceFile)
        {
            try
            {
                if (!File.Exists(sourceFile)) return;
                
                var json = File.ReadAllText(sourceFile);
                var allSettings = JsonSerializer.Deserialize<AllSettingsConfig>(json);
                if (allSettings == null) return;
                
                // 导入OoiMRR配置
                if (allSettings.OoiMRRConfig != null)
                {
                    Save(allSettings.OoiMRRConfig);
                }
                
                // 导入TagTrain设置
                if (allSettings.TagTrainSettings != null && allSettings.TagTrainSettings.Count > 0)
                {
                    if (App.IsTagTrainAvailable)
                    {
                        try
                        {
                            TagTrain.Services.SettingsManager.ClearCache();
                            var settingsPath = TagTrain.Services.SettingsManager.GetSettingsFilePath();
                            var settingsDir = Path.GetDirectoryName(settingsPath);
                            if (!string.IsNullOrEmpty(settingsDir))
                            {
                                Directory.CreateDirectory(settingsDir);
                            }
                            
                            var lines = allSettings.TagTrainSettings.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList();
                            File.WriteAllLines(settingsPath, lines);
                            TagTrain.Services.SettingsManager.ClearCache();
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"导入设置失败: {ex.Message}", ex);
            }
        }
    }
}

