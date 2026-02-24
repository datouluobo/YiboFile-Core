using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace YiboFile
{
    /// <summary>
    /// 标签页宽度模式
    /// </summary>
    public enum TabWidthMode
    {
        FixedWidth,      // 固定宽度：所有标签统一宽度
        DynamicWidth     // 动态宽度：根据文本长度自适应
    }

    public class AppConfig
    {
        public string LastPath { get; set; } = string.Empty;
        public string LastNavigationMode { get; set; } = "Path"; // Path, Library, Tag, Search
        public int LastLibraryId { get; set; } = 0; // 最后选中的库ID
        // Removed TagTrainDataDirectory
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public double? WindowTop { get; set; } = null;
        public double? WindowLeft { get; set; } = null;
        public bool IsMaximized { get; set; } = true;
        public string Theme { get; set; } = "Light"; // Light, Dark (保留兼容性)

        // 外观设置
        public string ThemeMode { get; set; } = "FollowSystem"; // Light, Dark, FollowSystem
        public string UIStyle { get; set; } = "Original"; // Original, Fluent, MacOS, Geek
        public string LayoutMode { get; set; } = "Full"; // Focus, Work, Full
        public bool IsDualListMode { get; set; } = false; // 双列表模式
        public double WindowOpacity { get; set; } = 1.0; // 窗口透明度 (0.5-1.0)
        public bool AnimationsEnabled { get; set; } = true; // 动画效果启用
        public string IconStyle { get; set; } = "Emoji"; // 图标风格 (Emoji, Remix, Fluent)

        // 主布局列宽度（列1和列2）
        public double ColLeftWidth { get; set; } = 220; // 列1（左侧导航区）宽度
        public double ColCenterWidth { get; set; } = 0; // 列2（中间文件浏览器）宽度，0表示使用Star模式
        // 新增：列3（右侧预览区）宽度 - 默认360
        public double ColRightWidth { get; set; } = 360;

        // 兼容旧版本的属性名
        public double LeftPanelWidth { get => ColLeftWidth; set => ColLeftWidth = value; }
        public double MiddlePanelWidth { get => ColCenterWidth; set => ColCenterWidth = value; }
        public double RightPanelWidth { get => ColRightWidth; set => ColRightWidth = value; }

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
        public double PinnedTabWidth { get; set; } = 120;

        // 标签页状态保存（所有打开的标签页和活动标签页）
        public System.Collections.Generic.List<string> OpenTabs { get; set; } = new System.Collections.Generic.List<string>(); // 所有打开的标签页键值列表（按顺序）
        public string ActiveTabKey { get; set; } = string.Empty; // 活动标签页的键值

        // 副列表（双栏模式）标签页状态保存
        public System.Collections.Generic.List<string> OpenTabsSecondary { get; set; } = new System.Collections.Generic.List<string>();
        public string ActiveTabKeySecondary { get; set; } = string.Empty;

        // 字体设置
        public double UIFontSize { get; set; } = 16; // 界面字体大小（默认16）
        public double TagFontSize { get; set; } = 16; // Tag字体大小（默认16）
        public double TagBoxWidth { get; set; } = 0; // Tag框宽度（0表示自动计算，>0表示固定宽度）
        public double TagWidth { get; set; } = 120; // Tag框宽度（默认120）

        // 新增：持久化状态字段
        public bool IsRightPanelVisible { get; set; } = true; // 右侧面板可见性
        public double RightPanelNotesHeight { get; set; } = 200; // 右侧备注区高度
        public double CenterPanelInfoHeight { get; set; } = 180; // 中间底部详情区高度
        public string FileViewMode { get; set; } = "List"; // 视图模式：List 或 Thumbnail
        public string SortColumn { get; set; } = "Name"; // 排序字段
        public string SortDirection { get; set; } = "Ascending"; // 排序方向

        // 标签页复用策略配置
        public int ReuseTabTimeWindow { get; set; } = 10; // 复用标签页的时间窗口（秒），默认10秒
        public bool AlwaysReuseTab { get; set; } = false; // 总是复用标签页（忽略时间窗口）
        public bool NeverReuseTab { get; set; } = false; // 从不复用标签页（总是创建新的）

        // 标签页宽度模式
        public TabWidthMode TabWidthMode { get; set; } = TabWidthMode.FixedWidth;

        // 布局状态持久化
        public bool IsSidebarCollapsed { get; set; } = false;
        public bool IsPreviewCollapsed { get; set; } = true;

        // 搜索设置
        public bool IsEnableFullTextSearch { get; set; } = true;
        public System.Collections.Generic.List<string> FullTextIndexPaths { get; set; } = new System.Collections.Generic.List<string>(); // 启用全文搜索
        public string FullTextIndexDbPath { get; set; } = string.Empty; // 索引数据库路径

        // 多窗口支持
        public bool EnableMultiWindow { get; set; } = true;

        // 搜索历史设置
        public int HistoryMaxCount { get; set; } = 20;
        public bool AutoExpandHistory { get; set; } = false;

        // 备份设置
        public string BackupDirectory { get; set; } = string.Empty; // 为空则使用默认路径
        public int BackupRetentionDays { get; set; } = 30; // 默认保留30天，0表示永久
        public double BackupBrowserWidth { get; set; } = 1000;
        public double BackupBrowserHeight { get; set; } = 650;

        // 导航栏项目顺序
        public System.Collections.Generic.List<string> NavigationSectionsOrder { get; set; } = new System.Collections.Generic.List<string>
        {
            "QuickAccess",
            "Drives",
            "FolderFavorites",
            "FileFavorites",
            "Libraries",
            "Tags"
        };

        // 侧边栏折叠状态持久化
        public System.Collections.Generic.Dictionary<string, bool> SidebarExpanderStates { get; set; } = new System.Collections.Generic.Dictionary<string, bool>();

        // 自定义快捷键 (Description -> KeyCombination)
        public System.Collections.Generic.Dictionary<string, string> CustomHotkeys { get; set; } = new System.Collections.Generic.Dictionary<string, string>();

        // 中键打开标签页行为
        public bool ActivateNewTabOnMiddleClick { get; set; } = true;
    }

    public class AllSettingsConfig
    {
        public AppConfig YiboFileConfig { get; set; } = new AppConfig();
        // Removed TagTrainSettings
    }

    public static class ConfigManager
    {
        private const string ConfigFileName = "ooi_config.json";
        private const string OldDataFileName = "ooi_data.db";
        private const string DataFileName = "yibofile_data.db";
        private const string BasePathMarkerFileName = "basepath.txt";

        private static readonly string DefaultBaseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");
        private static readonly string LegacyBaseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YiboFile");

        private static string _baseDirectory;

        public static string GetConfigDirectory()
        {
            return GetBaseDirectory();
        }

        public static string GetConfigFilePath() => Path.Combine(GetBaseDirectory(), ConfigFileName);
        
        public static string GetDataFilePath() 
        {
            var baseDir = GetBaseDirectory();
            var newDbPath = Path.Combine(baseDir, DataFileName);
            var oldDbPath = Path.Combine(baseDir, OldDataFileName);
            
            if (File.Exists(oldDbPath) && !File.Exists(newDbPath))
            {
                try { File.Move(oldDbPath, newDbPath); } catch { }
            }
            
            return newDbPath;
        }

        public static string GetBaseDirectory()
        {
            if (!string.IsNullOrEmpty(_baseDirectory))
            {
                return _baseDirectory;
            }

            var candidates = new List<string>();
            var markerPaths = new[]
            {
                Path.Combine(DefaultBaseDirectory, BasePathMarkerFileName),
                Path.Combine(LegacyBaseDirectory, BasePathMarkerFileName)
            };

            foreach (var marker in markerPaths)
            {
                if (File.Exists(marker))
                {
                    try
                    {
                        var path = File.ReadAllText(marker).Trim();
                        if (!string.IsNullOrEmpty(path))
                        {
                            candidates.Add(path);
                        }
                    }
                    catch { }
                }
            }

            candidates.Add(DefaultBaseDirectory);
            candidates.Add(LegacyBaseDirectory);

            string PickExisting(IEnumerable<string> paths)
            {
                foreach (var p in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (Directory.Exists(p) || File.Exists(Path.Combine(p, ConfigFileName)))
                        {
                            return p;
                        }
                    }
                    catch { }
                }
                return null;
            }

            var selected = PickExisting(candidates) ?? DefaultBaseDirectory;
            try
            {
                Directory.CreateDirectory(selected);
            }
            catch { }

            _baseDirectory = selected;
            WriteBasePathMarker(_baseDirectory);

            // 如果使用默认目录且存在旧目录中的文件，自动补齐
            if (!string.Equals(_baseDirectory, LegacyBaseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                TryCopyMissingFiles(LegacyBaseDirectory, _baseDirectory);
            }

            return _baseDirectory;
        }

        public static void SetBaseDirectory(string newBaseDirectory, bool copyMissingFromOld = true)
        {
            if (string.IsNullOrWhiteSpace(newBaseDirectory))
            {
                return;
            }

            var oldBase = GetBaseDirectory();
            _baseDirectory = newBaseDirectory;

            try
            {
                Directory.CreateDirectory(_baseDirectory);
            }
            catch { }

            WriteBasePathMarker(_baseDirectory);

            if (copyMissingFromOld && !string.Equals(oldBase, _baseDirectory, StringComparison.OrdinalIgnoreCase))
            {
                TryCopyMissingFiles(oldBase, _baseDirectory);
            }
        }

        private static void WriteBasePathMarker(string baseDirectory)
        {
            try
            {
                Directory.CreateDirectory(DefaultBaseDirectory);
                File.WriteAllText(Path.Combine(DefaultBaseDirectory, BasePathMarkerFileName), baseDirectory);
            }
            catch { }

            try
            {
                Directory.CreateDirectory(LegacyBaseDirectory);
                File.WriteAllText(Path.Combine(LegacyBaseDirectory, BasePathMarkerFileName), baseDirectory);
            }
            catch { }
        }

        private static void TryCopyMissingFiles(string sourceDir, string targetDir)
        {
            if (string.IsNullOrWhiteSpace(sourceDir) || string.IsNullOrWhiteSpace(targetDir) ||
                string.Equals(sourceDir, targetDir, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileMappings = new[]
            {
                new { NewName = ConfigFileName, Legacy = new [] { "config.json", ConfigFileName } },
                new { NewName = DataFileName, Legacy = new [] { "data.db", DataFileName, OldDataFileName } }
            };

            foreach (var mapping in fileMappings)
            {
                try
                {
                    var targetPath = Path.Combine(targetDir, mapping.NewName);
                    if (File.Exists(targetPath)) continue;

                    string sourcePath = null;
                    foreach (var legacy in mapping.Legacy)
                    {
                        var candidate = Path.Combine(sourceDir, legacy);
                        if (File.Exists(candidate))
                        {
                            sourcePath = candidate;
                            break;
                        }
                    }

                    if (sourcePath != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? targetDir);
                        File.Copy(sourcePath, targetPath, overwrite: false);
                    }
                }
                catch { }
            }
        }

        [Obsolete("Use ConfigurationService.Instance.Config instead.")]
        public static AppConfig Load()
        {
            // Delegate to the new unified ConfigurationService
            // This ensures all callers get the consistent, in-memory config object
            // managed by the service (settings.json + state.json)
            var service = YiboFile.Services.Config.ConfigurationService.Instance;
            return service.Config;
        }

        /// <summary>
        /// 加载旧版配置文件 (internal usage or migration)
        /// </summary>
        public static AppConfig LoadLegacy()
        {
            try
            {
                var path = GetConfigFilePath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var options = new JsonSerializerOptions
                    {
                        Converters = { new JsonStringEnumConverter() },
                        PropertyNameCaseInsensitive = true
                    };
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json, options);
                    if (cfg != null)
                    {
                        // 迁移配置：清理旧字段，确保新字段有值
                        MigrateConfig(cfg);
                        return cfg;
                    }
                }
            }
            catch
            {
                // ignore and return defaults
            }
            return new AppConfig();
        }

        /// <summary>
        /// 迁移配置：清理旧字段，确保新字段正确
        /// </summary>
        public static void MigrateConfig(AppConfig config)
        {
            if (config == null) return;

            // 确保 OpenTabs 和 ActiveTabKey 已初始化
            if (config.OpenTabs == null)
            {
                config.OpenTabs = new List<string>();
            }
            if (string.IsNullOrEmpty(config.ActiveTabKey))
            {
                config.ActiveTabKey = string.Empty;
            }
            if (config.OpenTabsSecondary == null)
            {
                config.OpenTabsSecondary = new List<string>();
            }
            if (string.IsNullOrEmpty(config.ActiveTabKeySecondary))
            {
                config.ActiveTabKeySecondary = string.Empty;
            }

            // 如果 ColLeftWidth 和 ColCenterWidth 为 0，但 LeftPanelWidth 和 MiddlePanelWidth 有值，则迁移
            if (config.ColLeftWidth <= 0 && config.LeftPanelWidth > 0)
            {
                config.ColLeftWidth = config.LeftPanelWidth;
            }
            if (config.ColCenterWidth <= 0 && config.MiddlePanelWidth > 0)
            {
                config.ColCenterWidth = config.MiddlePanelWidth;
            }

            // 确保导航模式有默认值
            if (string.IsNullOrEmpty(config.LastNavigationMode))
            {
                config.LastNavigationMode = "Path";
            }

            // 确保窗口尺寸有效
            if (config.WindowWidth <= 0) config.WindowWidth = 1200;
            if (config.WindowHeight <= 0) config.WindowHeight = 800;
            if (config.ColLeftWidth <= 0) config.ColLeftWidth = 220;
            if (config.ColRightWidth <= 0) config.ColRightWidth = 360;
            if (config.BackupBrowserWidth <= 0) config.BackupBrowserWidth = 1000;
            if (config.BackupBrowserHeight <= 0) config.BackupBrowserHeight = 650;

            // 根据 LayoutMode 初始化折叠状态（如果是新配置或旧版本升级）
            if (config.LayoutMode == "Focus")
            {
                config.IsSidebarCollapsed = true;
                config.IsPreviewCollapsed = true;
            }
            else if (config.LayoutMode == "Work")
            {
                config.IsSidebarCollapsed = false;
                config.IsPreviewCollapsed = true;
            }
            else if (config.LayoutMode == "Full")
            {
                config.IsSidebarCollapsed = false;
                config.IsPreviewCollapsed = false;
            }
        }

        public static void Save(AppConfig config)
        {
            try
            {
                if (config == null) return;

                // Delegate to ConfigurationService (New System)
                try
                {
                    var service = YiboFile.Services.Config.ConfigurationService.Instance;
                    if (service != null)
                    {
                        service.ManualSave(config);
                        return; // Successfully handled by new system
                    }
                }
                catch (Exception)
                {

                }

                // Legacy Fallback (should rarely be reached)
                MigrateConfig(config);

                var baseDir = GetBaseDirectory();
                Directory.CreateDirectory(baseDir);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() }
                };
                var json = JsonSerializer.Serialize(config, options);
                var configPath = GetConfigFilePath();
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                // ignore disk errors for now
            }
        }

        [Obsolete("Use IExportService instead.")]
        public static void Export(string targetFile)
        {
            var current = YiboFile.Services.Config.ConfigurationService.Instance.Config;
            var json = JsonSerializer.Serialize(current, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(targetFile, json);
        }

        [Obsolete("Use IImportService instead.")]
        public static void Import(string sourceFile)
        {
            if (!File.Exists(sourceFile)) return;
            var json = File.ReadAllText(sourceFile);
            var cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            Save(cfg);
        }

        // --- Legacy Export/Import Logic (Zip) ---
        // These methods rely on specific file paths which might have changed.
        // We mark them as obsolete. They might be broken if they only export ooi_config.json.

        [Obsolete("Use IExportService instead.")]
        public static void ExportConfigsZip(string targetZip)
        {
            // Redirect to new system if possible? No, static context.
            // Just warn user.
            throw new NotSupportedException("This method is deprecated. Please use IExportService.");
        }

        [Obsolete("Use IImportService instead.")]
        public static void ImportConfigsZip(string sourceZip)
        {
            throw new NotSupportedException("This method is deprecated. Please use IImportService.");
        }

        [Obsolete("Use IImportService instead.")]
        public static void ImportDataZip(string sourceZip)
        {
            throw new NotSupportedException("This method is deprecated. Please use IImportService.");
        }

        [Obsolete("Use IExportService instead.")]
        public static void ExportDataZip(string targetZip)
        {
            throw new NotSupportedException("This method is deprecated. Please use IExportService.");
        }

        [Obsolete("Use IExportService instead.")]
        public static void ExportAllZip(string targetZip)
        {
            throw new NotSupportedException("This method is deprecated. Please use IExportService.");
        }

        [Obsolete("Use IImportService instead.")]
        public static void ImportAllZip(string sourceZip)
        {
            throw new NotSupportedException("This method is deprecated. Please use IImportService.");
        }

    }
}
