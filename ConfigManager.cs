using System;
using System.IO;
using System.Text.Json;

namespace OoiMRR
{
    public class AppConfig
    {
        public string LastPath { get; set; } = string.Empty;
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public double WindowTop { get; set; } = double.NaN;
        public double WindowLeft { get; set; } = double.NaN;
        public bool IsMaximized { get; set; } = true;

        public double LeftPanelWidth { get; set; } = 300;
        public double MiddlePanelWidth { get; set; } = 600;
        public double RightPanelWidth { get; set; } = 350;
    }

    public static class ConfigManager
    {
        private static readonly string ConfigDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OoiMRR");
        private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "config.json");

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
    }
}


