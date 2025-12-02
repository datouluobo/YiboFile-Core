using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using Microsoft.Data.Sqlite;

namespace TagTrain.Services
{
    /// <summary>
    /// 统一配置管理器
    /// </summary>
    public static class SettingsManager
    {
        /// <summary>
        /// 获取程序目录下的 data 目录路径（默认数据存储目录）
        /// 注意：由于项目配置了 RuntimeIdentifier>win-x64</RuntimeIdentifier>，
        /// 程序会从 bin\Debug\net8.0-windows\win-x64\ 目录运行，
        /// 因此 data 目录位于 win-x64\data\
        /// </summary>
        private static string GetDefaultDataDirectory()
        {
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(assemblyLocation))
            {
                // 如果无法获取程序集位置，使用当前工作目录
                assemblyLocation = Environment.CurrentDirectory;
            }
            
            var appDir = Path.GetDirectoryName(assemblyLocation);
            if (string.IsNullOrEmpty(appDir))
            {
                // 如果无法获取目录，使用当前工作目录
                appDir = Environment.CurrentDirectory;
            }
            
            // 返回程序目录下的 data 目录
            // 当从 win-x64 目录运行时，会自动指向 win-x64\data\
            return Path.Combine(appDir, "data");
        }

        private static string GetSettingsPath(bool useCache = true)
        {
            // 如果使用缓存且缓存已加载，从缓存读取
            if (useCache && _settingsCache != null)
            {
                if (_settingsCache.TryGetValue("DataStorageDirectory", out var storageDir) && !string.IsNullOrEmpty(storageDir))
                {
                    return Path.Combine(storageDir, "settings.txt");
                }
            }
            
            // 如果没有设置，使用默认路径（程序目录下的 data 目录）
            var defaultDir = GetDefaultDataDirectory();
            return Path.Combine(defaultDir, "settings.txt");
        }

        private static Dictionary<string, string> _settingsCache = null;
        private static readonly object _lockObject = new object();

        /// <summary>
        /// 加载所有配置
        /// </summary>
        private static Dictionary<string, string> LoadSettings()
        {
            lock (_lockObject)
            {
                if (_settingsCache != null)
                {
                    return _settingsCache;
                }

                var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                // 先尝试从新位置读取（不使用缓存，避免循环）
                var settingsPath = GetSettingsPath(useCache: false);
                if (File.Exists(settingsPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(settingsPath);
                        foreach (var line in lines)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                                continue;

                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                var key = parts[0].Trim();
                                var value = parts[1].Trim();
                                settings[key] = value;
                            }
                        }
                    }
                    catch (Exception)
                    {
                                            }
                }
                else
                {
                    // 如果新位置不存在，尝试从旧默认位置读取（兼容旧版本）
                    var oldDefaultPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "TagTrain", "settings.txt");
                    if (File.Exists(oldDefaultPath))
                    {
                        try
                        {
                            var lines = File.ReadAllLines(oldDefaultPath);
                            foreach (var line in lines)
                            {
                                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                                    continue;

                                var parts = line.Split('=');
                                if (parts.Length == 2)
                                {
                                    var key = parts[0].Trim();
                                    var value = parts[1].Trim();
                                    settings[key] = value;
                                }
                            }
                        }
                        catch (Exception)
                        {
                                                    }
                    }
                }

                _settingsCache = settings;
                return settings;
            }
        }

        /// <summary>
        /// 保存所有配置
        /// </summary>
        private static void SaveSettings(Dictionary<string, string> settings)
        {
            lock (_lockObject)
            {
                try
                {
                    var settingsPath = GetSettingsPath();
                    if (string.IsNullOrEmpty(settingsPath))
                    {
                        throw new InvalidOperationException("无法确定设置文件路径");
                    }
                    
                    var settingsDir = Path.GetDirectoryName(settingsPath);
                    if (!string.IsNullOrEmpty(settingsDir))
                    {
                        Directory.CreateDirectory(settingsDir);
                    }
                    else
                    {
                        // 如果无法获取目录，使用默认目录
                        var defaultDir = GetDefaultDataDirectory();
                        Directory.CreateDirectory(defaultDir);
                        settingsPath = Path.Combine(defaultDir, "settings.txt");
                    }

                    var lines = settings.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList();
                    File.WriteAllLines(settingsPath, lines);

                    // 更新缓存
                    _settingsCache = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
                }
                        catch (Exception)
                {
                                    }
            }
        }

        /// <summary>
        /// 获取配置值
        /// </summary>
        public static string GetValue(string key, string defaultValue = "")
        {
            var settings = LoadSettings();
            return settings.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 设置配置值
        /// </summary>
        public static void SetValue(string key, string value)
        {
            lock (_lockObject)
            {
                var settings = LoadSettings();
                settings[key] = value;
                SaveSettings(settings);
            }
        }

        /// <summary>
        /// 获取整数配置值
        /// </summary>
        public static int GetInt(string key, int defaultValue = 0)
        {
            var value = GetValue(key);
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 设置整数配置值
        /// </summary>
        public static void SetInt(string key, int value)
        {
            SetValue(key, value.ToString());
        }

        /// <summary>
        /// 获取双精度浮点数配置值
        /// </summary>
        public static double GetDouble(string key, double defaultValue = 0.0)
        {
            var value = GetValue(key);
            return double.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 设置双精度浮点数配置值
        /// </summary>
        public static void SetDouble(string key, double value)
        {
            SetValue(key, value.ToString());
        }

        /// <summary>
        /// 获取布尔配置值
        /// </summary>
        public static bool GetBool(string key, bool defaultValue = false)
        {
            var value = GetValue(key);
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// 设置布尔配置值
        /// </summary>
        public static void SetBool(string key, bool value)
        {
            SetValue(key, value.ToString());
        }

        /// <summary>
        /// 获取图片目录
        /// </summary>
        public static string GetImageDirectory()
        {
            return GetValue("ImageDirectory", "");
        }

        /// <summary>
        /// 设置图片目录
        /// </summary>
        public static void SetImageDirectory(string directory)
        {
            SetValue("ImageDirectory", directory);
        }

        /// <summary>
        /// 获取数据保存目录（所有程序生成文件的存储目录）
        /// </summary>
        public static string GetDataStorageDirectory()
        {
            var dir = GetValue("DataStorageDirectory", "");
            if (string.IsNullOrEmpty(dir))
            {
                // 默认路径：程序目录下的 data 目录
                dir = GetDefaultDataDirectory();
            }
            return dir;
        }

        /// <summary>
        /// 设置数据保存目录（所有程序生成文件的存储目录）
        /// </summary>
        public static void SetDataStorageDirectory(string directory)
        {
            SetValue("DataStorageDirectory", directory);
        }

        /// <summary>
        /// 获取数据库路径
        /// </summary>
        public static string GetDatabasePath()
        {
            var storageDir = GetDataStorageDirectory();
            return Path.Combine(storageDir, "training.db");
        }

        /// <summary>
        /// 获取模型文件路径
        /// </summary>
        public static string GetModelPath()
        {
            var storageDir = GetDataStorageDirectory();
            return Path.Combine(storageDir, "model.zip");
        }

        /// <summary>
        /// 获取设置文件路径
        /// </summary>
        public static string GetSettingsFilePath()
        {
            var storageDir = GetDataStorageDirectory();
            return Path.Combine(storageDir, "settings.txt");
        }

        /// <summary>
        /// 获取每行标签数
        /// </summary>
        public static int GetTagsPerRow()
        {
            return GetInt("TagsPerRow", 5);
        }

        /// <summary>
        /// 设置每行标签数
        /// </summary>
        public static void SetTagsPerRow(int tagsPerRow)
        {
            SetInt("TagsPerRow", tagsPerRow);
        }

        /// <summary>
        /// 获取预测阈值
        /// </summary>
        public static double GetPredictionThreshold()
        {
            return GetDouble("PredictionThreshold", 50.0);
        }

        /// <summary>
        /// 设置预测阈值
        /// </summary>
        public static void SetPredictionThreshold(double threshold)
        {
            SetDouble("PredictionThreshold", threshold);
        }

        /// <summary>
        /// 获取标签排序模式
        /// </summary>
        public static string GetTagSortMode()
        {
            return GetValue("TagSortMode", "Count");
        }

        /// <summary>
        /// 设置标签排序模式
        /// </summary>
        public static void SetTagSortMode(string mode)
        {
            SetValue("TagSortMode", mode);
        }

        /// <summary>
        /// 保存窗口位置和大小
        /// </summary>
        public static void SaveWindowPosition(string windowName, Window window)
        {
            if (window == null) return;

            lock (_lockObject)
            {
                var settings = LoadSettings();
                
                // 保存窗口位置和大小（仅在非最大化状态下保存）
                if (window.WindowState == WindowState.Normal)
                {
                    settings[$"{windowName}Left"] = window.Left.ToString();
                    settings[$"{windowName}Top"] = window.Top.ToString();
                    settings[$"{windowName}Width"] = window.Width.ToString();
                    settings[$"{windowName}Height"] = window.Height.ToString();
                }
                settings[$"{windowName}WindowState"] = window.WindowState.ToString();
                
                SaveSettings(settings);
            }
        }

        /// <summary>
        /// 加载窗口位置和大小
        /// </summary>
        public static void LoadWindowPosition(string windowName, Window window)
        {
            if (window == null) return;

            try
            {
                var settings = LoadSettings();
                
                double? left = null, top = null, width = null, height = null;
                WindowState? windowState = null;

                if (settings.TryGetValue($"{windowName}Left", out var leftStr) && double.TryParse(leftStr, out var l))
                    left = l;
                if (settings.TryGetValue($"{windowName}Top", out var topStr) && double.TryParse(topStr, out var t))
                    top = t;
                if (settings.TryGetValue($"{windowName}Width", out var widthStr) && double.TryParse(widthStr, out var w) && w >= window.MinWidth)
                    width = w;
                if (settings.TryGetValue($"{windowName}Height", out var heightStr) && double.TryParse(heightStr, out var h) && h >= window.MinHeight)
                    height = h;
                if (settings.TryGetValue($"{windowName}WindowState", out var stateStr) && Enum.TryParse<WindowState>(stateStr, out var state))
                    windowState = state;

                // 验证位置是否在任何屏幕范围内（支持多屏幕）
                if (left.HasValue && top.HasValue && width.HasValue && height.HasValue)
                {
                    bool isPositionValid = false;
                    var allScreens = System.Windows.Forms.Screen.AllScreens;

                    foreach (var screen in allScreens)
                    {
                        var screenBounds = screen.WorkingArea;
                        if (left.Value + width.Value > screenBounds.Left &&
                            left.Value < screenBounds.Right &&
                            top.Value + height.Value > screenBounds.Top &&
                            top.Value < screenBounds.Bottom)
                        {
                            isPositionValid = true;
                            break;
                        }
                    }

                    if (isPositionValid)
                    {
                        window.Left = left.Value;
                        window.Top = top.Value;
                        window.Width = width.Value;
                        window.Height = height.Value;
                    }
                    else
                    {
                        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                }
                else
                {
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                // 设置窗口状态（最后设置，避免影响位置）
                // 使用 Dispatcher.BeginInvoke 确保窗口完全加载后再设置最大化状态
                if (windowState.HasValue)
                {
                    if (windowState.Value == WindowState.Maximized)
                    {
                        // 最大化状态需要在窗口完全加载后设置
                        // 先设置位置和大小，然后在Loaded事件中设置最大化状态
                        window.Loaded += (s, e) =>
                        {
                            if (window.WindowState != WindowState.Maximized)
                            {
                                window.WindowState = WindowState.Maximized;
                            }
                        };
                    }
                    else
                    {
                        window.WindowState = windowState.Value;
                    }
                }
            }
                        catch (Exception)
            {
                                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        /// <summary>
        /// 清除配置缓存（用于重新加载）
        /// </summary>
        public static void ClearCache()
        {
            lock (_lockObject)
            {
                _settingsCache = null;
            }
        }

        /// <summary>
        /// 获取所有设置的副本（用于迁移等场景）
        /// </summary>
        public static Dictionary<string, string> GetAllSettings()
        {
            lock (_lockObject)
            {
                var settings = LoadSettings();
                return new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// 迁移旧配置文件到新格式
        /// </summary>
        public static void MigrateOldSettings()
        {
            try
            {
                var oldSettingsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TagTrain");

                if (!Directory.Exists(oldSettingsDir))
                    return;

                // 获取新的默认数据目录（程序目录下的 data 目录）
                var newDataDir = GetDefaultDataDirectory();
                
                // 如果新目录不存在，创建它
                if (!Directory.Exists(newDataDir))
                {
                    try
                    {
                        Directory.CreateDirectory(newDataDir);
                    }
                    catch (Exception)
                    {
                                            }
                }

                var settings = LoadSettings();
                bool migrated = false;

                // 迁移 config.txt (图片目录)
                var configPath = Path.Combine(oldSettingsDir, "config.txt");
                if (File.Exists(configPath) && !settings.ContainsKey("ImageDirectory"))
                {
                    try
                    {
                        var imageDir = File.ReadAllText(configPath).Trim();
                        if (!string.IsNullOrEmpty(imageDir))
                        {
                            settings["ImageDirectory"] = imageDir;
                            migrated = true;
                        }
                    }
                    catch { }
                }

                // 迁移 dbpath.txt (数据库路径)
                var dbpathPath = Path.Combine(oldSettingsDir, "dbpath.txt");
                if (File.Exists(dbpathPath) && !settings.ContainsKey("DatabasePath"))
                {
                    try
                    {
                        var dbPath = File.ReadAllText(dbpathPath).Trim();
                        if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
                        {
                            settings["DatabasePath"] = dbPath;
                            migrated = true;
                        }
                    }
                    catch { }
                }

                // 迁移 configwindow.txt (配置窗口位置)
                var configWindowPath = Path.Combine(oldSettingsDir, "configwindow.txt");
                if (File.Exists(configWindowPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(configWindowPath);
                        foreach (var line in lines)
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                var key = parts[0].Trim();
                                var value = parts[1].Trim();
                                var newKey = "ConfigWindow" + key;
                                if (!settings.ContainsKey(newKey))
                                {
                                    settings[newKey] = value;
                                    migrated = true;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 迁移 trainingstatuswindow.txt (训练状态窗口位置)
                var trainingStatusWindowPath = Path.Combine(oldSettingsDir, "trainingstatuswindow.txt");
                if (File.Exists(trainingStatusWindowPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(trainingStatusWindowPath);
                        foreach (var line in lines)
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                var key = parts[0].Trim();
                                var value = parts[1].Trim();
                                var newKey = "TrainingStatusWindow" + key;
                                if (!settings.ContainsKey(newKey))
                                {
                                    settings[newKey] = value;
                                    migrated = true;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 迁移旧settings.txt中的TrainingWindow窗口位置（WindowLeft -> TrainingWindowLeft）
                var oldSettingsPath = Path.Combine(oldSettingsDir, "settings.txt");
                if (File.Exists(oldSettingsPath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(oldSettingsPath);
                        foreach (var line in lines)
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                var key = parts[0].Trim();
                                var value = parts[1].Trim();
                                
                                // 迁移窗口位置键（WindowLeft -> TrainingWindowLeft）
                                if (key == "WindowLeft" && !settings.ContainsKey("TrainingWindowLeft"))
                                {
                                    settings["TrainingWindowLeft"] = value;
                                    migrated = true;
                                }
                                else if (key == "WindowTop" && !settings.ContainsKey("TrainingWindowTop"))
                                {
                                    settings["TrainingWindowTop"] = value;
                                    migrated = true;
                                }
                                else if (key == "WindowWidth" && !settings.ContainsKey("TrainingWindowWidth"))
                                {
                                    settings["TrainingWindowWidth"] = value;
                                    migrated = true;
                                }
                                else if (key == "WindowHeight" && !settings.ContainsKey("TrainingWindowHeight"))
                                {
                                    settings["TrainingWindowHeight"] = value;
                                    migrated = true;
                                }
                                else if (key == "WindowState" && !settings.ContainsKey("TrainingWindowWindowState"))
                                {
                                    settings["TrainingWindowWindowState"] = value;
                                    migrated = true;
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 保存迁移后的配置到新位置
                if (migrated)
                {
                    // 确保新目录存在
                    if (!Directory.Exists(newDataDir))
                    {
                        Directory.CreateDirectory(newDataDir);
                    }
                    
                    // 保存到新位置
                    var newSettingsPath = Path.Combine(newDataDir, "settings.txt");
                    var lines = settings.Select(kvp => $"{kvp.Key}={kvp.Value}").ToList();
                    File.WriteAllLines(newSettingsPath, lines);
                    
                    ClearCache();
                    
                    // 清理旧配置文件
                    CleanupOldConfigFiles();
                }
                
                // 如果旧路径存在数据文件且新路径没有，自动迁移数据文件
                if (Directory.Exists(oldSettingsDir) && Directory.Exists(newDataDir))
                {
                    var dataFiles = new[]
                    {
                        new { Name = "settings.txt", Description = "设置文件" },
                        new { Name = "training.db", Description = "训练数据库" },
                        new { Name = "model.zip", Description = "模型文件" }
                    };
                    
                    foreach (var fileInfo in dataFiles)
                    {
                        var oldFilePath = Path.Combine(oldSettingsDir, fileInfo.Name);
                        var newFilePath = Path.Combine(newDataDir, fileInfo.Name);
                        
                        // 如果旧文件存在且新文件不存在，则迁移
                        if (File.Exists(oldFilePath) && !File.Exists(newFilePath))
                        {
                            try
                            {
                                if (fileInfo.Name == "training.db")
                                {
                                    // 数据库文件使用备份方式迁移
                                    using (var sourceConnection = new SqliteConnection($"Data Source={oldFilePath}"))
                                    {
                                        sourceConnection.Open();
                                        using (var destConnection = new SqliteConnection($"Data Source={newFilePath}"))
                                        {
                                            destConnection.Open();
                                            sourceConnection.BackupDatabase(destConnection);
                                        }
                                    }
                                }
                                else
                                {
                                    // 其他文件直接复制
                                    File.Copy(oldFilePath, newFilePath);
                                }
                                
                                                            }
                            catch (Exception)
                            {
                                                            }
                        }
                    }
                }
            }
                        catch (Exception)
            {
                            }
        }

        /// <summary>
        /// 清理旧配置文件
        /// </summary>
        private static void CleanupOldConfigFiles()
        {
            try
            {
                var settingsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TagTrain");

                if (!Directory.Exists(settingsDir))
                    return;

                // 删除旧配置文件
                var oldFiles = new[]
                {
                    Path.Combine(settingsDir, "config.txt"),
                    Path.Combine(settingsDir, "dbpath.txt"),
                    Path.Combine(settingsDir, "configwindow.txt"),
                    Path.Combine(settingsDir, "trainingstatuswindow.txt")
                };

                foreach (var file in oldFiles)
                {
                    try
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                                                    }
                    }
                    catch (Exception)
                    {
                                                // 忽略删除失败，可能是文件正在被使用
                    }
                }
            }
                        catch (Exception)
            {
                            }
        }
    }
}

