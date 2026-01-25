using System;
using YiboFile.Services.Config;

namespace YiboFile.ViewModels
{
    public partial class SettingsViewModel
    {
        #region Data & Config Management
        private void ChangeBaseDirectory(string newDir)
        {
            if (string.IsNullOrWhiteSpace(newDir)) return;

            var oldDir = ConfigManager.GetBaseDirectory();
            try
            {
                if (string.Equals(System.IO.Path.GetFullPath(oldDir.Trim()), System.IO.Path.GetFullPath(newDir.Trim()), StringComparison.OrdinalIgnoreCase))
                    return;
            }
            catch { return; }

            ConfigManager.SetBaseDirectory(newDir, copyMissingFromOld: true);

            try { DatabaseManager.Initialize(); } catch { }

            LoadFromConfig();
            OnPropertyChanged(nameof(BaseDirectory));
        }

        private void ExportConfigs(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try { ConfigManager.ExportConfigsZip(fileName); } catch (Exception ex) { throw new Exception($"导出配置失败: {ex.Message}"); }
        }

        private void ImportConfigs(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                ConfigManager.ImportConfigsZip(fileName);
                LoadFromConfig();
            }
            catch (Exception ex) { throw new Exception($"导入配置失败: {ex.Message}"); }
        }

        private void ExportData(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try { ConfigManager.ExportDataZip(fileName); } catch (Exception ex) { throw new Exception($"导出数据失败: {ex.Message}"); }
        }

        private void ImportData(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                ConfigManager.ImportDataZip(fileName);
                LoadFromConfig();
            }
            catch (Exception ex) { throw new Exception($"导入数据失败: {ex.Message}"); }
        }

        private void ExportAll(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try { ConfigManager.ExportAllZip(fileName); } catch (Exception ex) { throw new Exception($"导出全部失败: {ex.Message}"); }
        }

        private void ImportAll(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                ConfigManager.ImportAllZip(fileName);
                LoadFromConfig();
            }
            catch (Exception ex) { throw new Exception($"导入全部失败: {ex.Message}"); }
        }
        #endregion
    }
}
