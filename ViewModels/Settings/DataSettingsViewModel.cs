using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using YiboFile.Services.Config.IO;
using YiboFile.Services.Config;

namespace YiboFile.ViewModels.Settings
{
    public class DataSettingsViewModel : BaseViewModel
    {
        private readonly IExportService _exportService;
        private readonly IImportService _importService;
        private readonly IConfigurationService _configService;

        public ICommand ExportConfigsCommand { get; }
        public ICommand ImportConfigsCommand { get; }
        public ICommand ExportDataCommand { get; }
        public ICommand ImportDataCommand { get; }
        public ICommand ExportAllCommand { get; }
        public ICommand ImportAllCommand { get; }

        public event EventHandler SettingsReloadRequested;

        // Default constructor for design-time support or legacy instantiation
        public DataSettingsViewModel() : this(
            App.ServiceProvider?.GetService(typeof(IExportService)) as IExportService ?? new ExportService(new YiboFile.Services.Config.ConfigPathProvider()),
            App.ServiceProvider?.GetService(typeof(IImportService)) as IImportService ?? new ImportService(new YiboFile.Services.Config.ConfigPathProvider()),
            (IConfigurationService)App.ServiceProvider?.GetService(typeof(IConfigurationService)))
        {
        }

        public DataSettingsViewModel(IExportService exportService, IImportService importService, IConfigurationService configService)
        {
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _importService = importService ?? throw new ArgumentNullException(nameof(importService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));

            ExportConfigsCommand = new RelayCommand<string>(async (f) => await ExportConfigsAsync(f));
            ImportConfigsCommand = new RelayCommand<string>(async (f) => await ImportConfigsAsync(f));
            ExportDataCommand = new RelayCommand<string>(async (f) => await ExportDataAsync(f));
            ImportDataCommand = new RelayCommand<string>(async (f) => await ImportDataAsync(f));
            ExportAllCommand = new RelayCommand<string>(async (f) => await ExportAllAsync(f));
            ImportAllCommand = new RelayCommand<string>(async (f) => await ImportAllAsync(f));
        }

        private async Task ExportConfigsAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                await _exportService.ExportAsync(fileName, new[] { ExportModuleType.Settings });
            }
            catch (Exception ex) { throw new Exception($"导出配置失败: {ex.Message}"); }
        }

        private async Task ImportConfigsAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                // In a real scenario, we would show the Import Dialog here.
                // For now, assume direct import of settings.
                await _importService.ExecuteImportAsync(fileName, new[] { ExportModuleType.Settings });

                // Reload configuration in memory
                _configService.Reload();

                SettingsReloadRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { throw new Exception($"导入配置失败: {ex.Message}"); }
        }

        private async Task ExportDataAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                await _exportService.ExportAsync(fileName, new[] { ExportModuleType.Structure, ExportModuleType.FileData });
            }
            catch (Exception ex) { throw new Exception($"导出数据失败: {ex.Message}"); }
        }

        private async Task ImportDataAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                await _importService.ExecuteImportAsync(fileName, new[] { ExportModuleType.Structure, ExportModuleType.FileData });
                SettingsReloadRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { throw new Exception($"导入数据失败: {ex.Message}"); }
        }

        private async Task ExportAllAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                await _exportService.ExportAsync(fileName, new[] { ExportModuleType.Settings, ExportModuleType.Structure, ExportModuleType.FileData, ExportModuleType.Themes });
            }
            catch (Exception ex) { throw new Exception($"导出全部失败: {ex.Message}"); }
        }

        private async Task ImportAllAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                await _importService.ExecuteImportAsync(fileName, new[] { ExportModuleType.Settings, ExportModuleType.Structure, ExportModuleType.FileData, ExportModuleType.Themes });

                // Reload configuration in memory
                _configService.Reload();

                SettingsReloadRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { throw new Exception($"导入全部失败: {ex.Message}"); }
        }
    }
}
