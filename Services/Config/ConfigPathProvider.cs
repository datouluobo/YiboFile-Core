using System;
using System.IO;

namespace YiboFile.Services.Config
{
    public class ConfigPathProvider : IConfigPathProvider
    {
        private string _baseDirectory;

        public ConfigPathProvider()
        {
            string localAppData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");
            if (Directory.Exists(localAppData))
            {
                _baseDirectory = localAppData;
            }
            else
            {
                _baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YiboFile");
            }

            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
        }

        public string BaseDirectory => _baseDirectory;

        public string SettingsFilePath => Path.Combine(_baseDirectory, "yibofile_settings.json");

        public string StateFilePath => Path.Combine(_baseDirectory, "yibofile_state.json");

        public string DatabaseFilePath => Path.Combine(_baseDirectory, "yibofile_data.db");

        public string HistoryFilePath => Path.Combine(_baseDirectory, "yibofile_history.json");

        public string LogDirectory => Path.Combine(_baseDirectory, "Logs");

        public string CustomThemesDirectory => Path.Combine(_baseDirectory, "CustomThemes");

        public string CacheDirectory => Path.Combine(_baseDirectory, "Cache");

        public void UpdateBaseDirectory(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath)) return;
            _baseDirectory = newPath;
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
        }
    }
}
