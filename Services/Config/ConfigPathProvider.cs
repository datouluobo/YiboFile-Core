using System;
using System.IO;

namespace YiboFile.Services.Config
{
    public class ConfigPathProvider : IConfigPathProvider
    {
        private string _baseDirectory;
        private string _backupDirectory;

        public ConfigPathProvider()
        {
            // 初始化逻辑：
            // 1. 优先检查当前运行目录下的 AppData (便携模式)
            // 2. 否则使用 %AppData%/YiboFile (安装模式)

            string localAppData = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");
            if (Directory.Exists(localAppData))
            {
                _baseDirectory = localAppData;
            }
            else
            {
                _baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YiboFile");
            }

            // 确保基础目录存在
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

        public string CustomThemesDirectory => Path.Combine(_baseDirectory, "CustomThemes");

        public string BackupDirectory
        {
            get
            {
                // 如果未显式设置备份目录，默认存放在 BaseDirectory/Backups 下
                return !string.IsNullOrEmpty(_backupDirectory)
                    ? _backupDirectory
                    : Path.Combine(_baseDirectory, "Backups");
            }
        }

        public string CacheDirectory => Path.Combine(_baseDirectory, "Cache");

        public void UpdateBaseDirectory(string newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath)) return;
            _baseDirectory = newPath;

            // 确保新目录存在
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
        }

        public void UpdateBackupDirectory(string newPath)
        {
            _backupDirectory = newPath;
        }
    }
}
