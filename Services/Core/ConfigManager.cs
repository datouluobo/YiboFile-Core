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
        public string TagTrainDataDirectory { get; set; } = string.Empty; // 持久化 TT 数据目录
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public double WindowTop { get; set; } = double.NaN;
        public double WindowLeft { get; set; } = double.NaN;
        public bool IsMaximized { get; set; } = true;
        public string Theme { get; set; } = "Light"; // Light, Dark (保留兼容性)

        // 外观设置
        public string ThemeMode { get; set; } = "FollowSystem"; // Light, Dark, FollowSystem
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

        // 自定义快捷键 (Description -> KeyCombination)
        public System.Collections.Generic.Dictionary<string, string> CustomHotkeys { get; set; } = new System.Collections.Generic.Dictionary<string, string>();

        // 中键打开标签页行为
        public bool ActivateNewTabOnMiddleClick { get; set; } = true;
    }

    public class AllSettingsConfig
    {
        public AppConfig YiboFileConfig { get; set; } = new AppConfig();
        public Dictionary<string, string> TagTrainSettings { get; set; } = new Dictionary<string, string>();
    }

    public static class ConfigManager
    {
        private const string ConfigFileName = "ooi_config.json";
        private const string DataFileName = "ooi_data.db";
        private const string TagTrainSettingsFileName = "tt_settings.txt";
        private const string TagTrainDbFileName = "tt_training.db";
        private const string TagTrainModelFileName = "tt_model.zip";
        private const string BasePathMarkerFileName = "basepath.txt";

        private static readonly string DefaultBaseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");
        private static readonly string LegacyBaseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YiboFile");

        private static string _baseDirectory;

        public static string GetConfigDirectory()
        {
            return GetBaseDirectory();
        }

        public static string GetConfigFilePath() => Path.Combine(GetBaseDirectory(), ConfigFileName);
        public static string GetDataFilePath() => Path.Combine(GetBaseDirectory(), DataFileName);
        public static string GetTagTrainSettingsPath() => Path.Combine(GetBaseDirectory(), TagTrainSettingsFileName);
        public static string GetTagTrainDatabasePath() => Path.Combine(GetBaseDirectory(), TagTrainDbFileName);
        public static string GetTagTrainModelPath() => Path.Combine(GetBaseDirectory(), TagTrainModelFileName);

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
                new { NewName = DataFileName, Legacy = new [] { "data.db", DataFileName } },
                new { NewName = TagTrainSettingsFileName, Legacy = new [] { "settings.txt", TagTrainSettingsFileName } },
                new { NewName = TagTrainDbFileName, Legacy = new [] { "training.db", TagTrainDbFileName } },
                new { NewName = TagTrainModelFileName, Legacy = new [] { "model.zip", TagTrainModelFileName } }
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

        public static AppConfig Load()
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
                        // Debug Logging
                        try
                        {
                            string msg = $"{DateTime.Now:O} [ConfigManager.Load] Loaded IsMaximized={cfg.IsMaximized}, W={cfg.WindowWidth}";
                            System.Diagnostics.Debug.WriteLine(msg);
                            File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_debug.log"), msg + "\n");
                        }
                        catch { }

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

                // #region agent log
                var logPath = @"f:\Download\GitHub\YiboFile\.cursor\debug.log";
                try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)); System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "ConfigManager.cs:310", message = "ConfigManager.Save开始", data = new { windowWidth = config.WindowWidth, windowHeight = config.WindowHeight, windowTop = config.WindowTop, windowLeft = config.WindowLeft, isMaximized = config.IsMaximized, colLeftWidth = config.ColLeftWidth, colCenterWidth = config.ColCenterWidth, openTabsCount = config.OpenTabs?.Count ?? 0, activeTabKey = config.ActiveTabKey }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion

                // Debug Logging
                try
                {
                    string msg = $"{DateTime.Now:O} [ConfigManager.Save] IsMaximized={config.IsMaximized}, W={config.WindowWidth}, H={config.WindowHeight}";
                    System.Diagnostics.Debug.WriteLine(msg);
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_debug.log"), msg + "\n");
                }
                catch { }

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

                // #region agent log
                try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "ConfigManager.cs:322", message = "ConfigManager.Save完成", data = new { configPath = configPath, jsonLength = json.Length, savedWindowWidth = config.WindowWidth, savedWindowHeight = config.WindowHeight, savedColLeftWidth = config.ColLeftWidth, savedColCenterWidth = config.ColCenterWidth, savedOpenTabsCount = config.OpenTabs?.Count ?? 0 }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
            }
            catch (Exception ex)
            {
                // Debug Logging
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_debug.log"),
                        $"{DateTime.Now:O} [ConfigManager.Save] EXCEPTION: {ex.Message}\n");
                }
                catch { }
                // #region agent log
                try { var logPath = @"f:\Download\GitHub\YiboFile\.cursor\debug.log"; System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "ConfigManager.cs:327", message = "ConfigManager.Save异常", data = new { error = ex.Message, stackTrace = ex.StackTrace }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
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

        public static void ExportConfigsZip(string targetZip)
        {
            ExportZip(targetZip, new[]
            {
                GetConfigFilePath(),
                GetTagTrainSettingsPath()
            });
        }

        public static void ExportDataZip(string targetZip)
        {
            ExportZip(targetZip, new[]
            {
                GetDataFilePath(),
                GetTagTrainDatabasePath(),
                GetTagTrainModelPath()
            });
        }

        public static void ExportAllZip(string targetZip)
        {
            ExportZip(targetZip, new[]
            {
                GetConfigFilePath(),
                GetTagTrainSettingsPath(),
                GetDataFilePath(),
                GetTagTrainDatabasePath(),
                GetTagTrainModelPath()
            });
        }

        public static void ImportConfigsZip(string sourceZip)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ConfigFileName, GetConfigFilePath() },
                { TagTrainSettingsFileName, GetTagTrainSettingsPath() }
            };
            ImportZip(sourceZip, map);
        }

        public static void ImportDataZip(string sourceZip)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { DataFileName, GetDataFilePath() },
                { TagTrainDbFileName, GetTagTrainDatabasePath() },
                { TagTrainModelFileName, GetTagTrainModelPath() }
            };
            ImportZip(sourceZip, map);
        }

        public static void ImportAllZip(string sourceZip)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { ConfigFileName, GetConfigFilePath() },
                { TagTrainSettingsFileName, GetTagTrainSettingsPath() },
                { DataFileName, GetDataFilePath() },
                { TagTrainDbFileName, GetTagTrainDatabasePath() },
                { TagTrainModelFileName, GetTagTrainModelPath() }
            };
            ImportZip(sourceZip, map);
        }

        private static void ExportZip(string targetZip, IEnumerable<string> filePaths)
        {
            var baseDir = Path.GetDirectoryName(targetZip);
            if (!string.IsNullOrEmpty(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            if (File.Exists(targetZip))
            {
                File.Delete(targetZip);
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "YiboFile_Export_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                using var archive = ZipFile.Open(targetZip, ZipArchiveMode.Create);
                foreach (var file in filePaths.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (!File.Exists(file)) continue;

                    var entryName = Path.GetFileName(file);
                    var tempFile = Path.Combine(tempDir, entryName);

                    try
                    {
                        // 尝试在共享读写模式下复制，避免被 SQLite 占用导致导出失败
                        using (var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var dest = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            source.CopyTo(dest);
                        }

                        archive.CreateEntryFromFile(tempFile, entryName, CompressionLevel.Optimal);
                    }
                    catch
                    {
                        // 忽略单个文件的复制/压缩错误，继续处理其它文件
                    }
                }
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static void ImportZip(string sourceZip, Dictionary<string, string> targetMap)
        {
            if (!File.Exists(sourceZip))
            {
                throw new FileNotFoundException("未找到导入包", sourceZip);
            }

            // 尝试关闭所有数据库连接以释放文件锁
            try
            {
                DatabaseManager.Shutdown();

                // 给一点时间让文件系统释放锁
                System.Threading.Thread.Sleep(200);
            }
            catch { }

            using var archive = ZipFile.OpenRead(sourceZip);
            foreach (var entry in archive.Entries)
            {
                if (targetMap.TryGetValue(entry.Name, out var targetPath))
                {
                    var targetDir = Path.GetDirectoryName(targetPath);
                    if (!string.IsNullOrEmpty(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }
                    entry.ExtractToFile(targetPath, overwrite: true);
                }
            }
        }
    }
}


