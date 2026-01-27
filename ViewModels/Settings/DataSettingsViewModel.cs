using System;
using System.Windows.Input;
using YiboFile.Services.Config;

namespace YiboFile.ViewModels.Settings
{
    public class DataSettingsViewModel : BaseViewModel
    {
        public ICommand ExportConfigsCommand { get; }
        public ICommand ImportConfigsCommand { get; }
        public ICommand ExportDataCommand { get; }
        public ICommand ImportDataCommand { get; }
        public ICommand ExportAllCommand { get; }
        public ICommand ImportAllCommand { get; }

        // Event to notify parent to reload settings if needed
        public event EventHandler SettingsReloadRequested;

        public DataSettingsViewModel()
        {
            ExportConfigsCommand = new RelayCommand<string>(ExportConfigs);
            ImportConfigsCommand = new RelayCommand<string>(ImportConfigs);
            ExportDataCommand = new RelayCommand<string>(ExportData);
            ImportDataCommand = new RelayCommand<string>(ImportData);
            ExportAllCommand = new RelayCommand<string>(ExportAll);
            ImportAllCommand = new RelayCommand<string>(ImportAll);
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
                SettingsReloadRequested?.Invoke(this, EventArgs.Empty);
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
                SettingsReloadRequested?.Invoke(this, EventArgs.Empty);
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
                SettingsReloadRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { throw new Exception($"导入全部失败: {ex.Message}"); }
        }
    }
}
