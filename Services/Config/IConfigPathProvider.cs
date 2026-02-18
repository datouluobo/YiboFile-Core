using System;

namespace YiboFile.Services.Config
{
    public interface IConfigPathProvider
    {
        /// <summary>
        /// 用户可配置的基础目录（默认 ./AppData/ 或 %AppData%/YiboFile/）
        /// </summary>
        string BaseDirectory { get; }

        /// <summary>用户偏好文件路径</summary>
        string SettingsFilePath { get; }

        /// <summary>应用状态文件路径</summary>
        string StateFilePath { get; }

        /// <summary>核心数据库路径</summary>
        string DatabaseFilePath { get; }

        /// <summary>搜索/地址栏历史文件路径</summary>
        string HistoryFilePath { get; }

        /// <summary>自定义主题目录路径</summary>
        string CustomThemesDirectory { get; }

        /// <summary>备份目录路径（可由用户独立配置）</summary>
        string BackupDirectory { get; }

        /// <summary>缓存目录路径（包括 CAD/DWG 缓存等）</summary>
        string CacheDirectory { get; }

        /// <summary>
        /// 更新基础目录（例如用户在设置中修改了数据存储位置）
        /// </summary>
        void UpdateBaseDirectory(string newPath);

        /// <summary>
        /// 更新备份目录（例如用户在设置中自定义了备份位置）
        /// </summary>
        void UpdateBackupDirectory(string newPath);
    }
}
