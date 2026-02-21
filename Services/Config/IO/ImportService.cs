using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace YiboFile.Services.Config.IO
{
    /// <summary>
    /// Service for handling configuration import operations (Implementation).
    /// </summary>
    public class ImportService : IImportService
    {
        private readonly IConfigPathProvider _pathProvider;
        private const string ManifestFileName = "manifest.json";
        private const string SettingsFileName = "settings.json";
        private const string StateFileName = "state.json";
        private const string StructureFileName = "structure.json";
        private const string FileDataFileName = "filedata.json";
        private const string ThemesDirectoryName = "themes";

        public ImportService(IConfigPathProvider pathProvider)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        }

        public async Task<ExportManifest> ReadManifestAsync(string zipPath)
        {
            if (!File.Exists(zipPath)) throw new FileNotFoundException("Import file not found", zipPath);

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var manifestEntry = archive.GetEntry(ManifestFileName);
                if (manifestEntry != null)
                {
                    using (var stream = manifestEntry.Open())
                    using (var reader = new StreamReader(stream))
                    {
                        var json = await reader.ReadToEndAsync();
                        return JsonSerializer.Deserialize<ExportManifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                }
            }
            return null;
        }

        public Task<ImportResult> SimulateImportAsync(string zipPath, IEnumerable<ExportModuleType> selectedModules)
        {
            // For now, return a dummy success result
            return Task.FromResult(new ImportResult { Success = true });
        }

        public async Task ExecuteImportAsync(string zipPath, IEnumerable<ExportModuleType> selectedModules, IEnumerable<PathMismatch> resolutions = null)
        {
            if (!File.Exists(zipPath)) throw new FileNotFoundException("Import file not found", zipPath);

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                foreach (var module in selectedModules)
                {
                    switch (module)
                    {
                        case ExportModuleType.Settings:
                            await ImportSettingsAsync(archive);
                            break;
                        case ExportModuleType.Structure:
                            await ImportStructureAsync(archive);
                            break;
                        case ExportModuleType.FileData:
                            await ImportFileDataAsync(archive);
                            break;
                        case ExportModuleType.Themes:
                            await ImportThemesAsync(archive);
                            break;
                    }
                }
            }
        }

        private async Task ImportSettingsAsync(ZipArchive archive)
        {
            var entry = archive.GetEntry(SettingsFileName);
            if (entry != null)
            {
                // Create backup of current settings
                if (File.Exists(_pathProvider.SettingsFilePath))
                {
                    File.Copy(_pathProvider.SettingsFilePath, _pathProvider.SettingsFilePath + ".bak", true);
                }

                // Extract new settings
                entry.ExtractToFile(_pathProvider.SettingsFilePath, overwrite: true);
            }
            await Task.CompletedTask;
        }

        private async Task ImportStructureAsync(ZipArchive archive)
        {
            // Identify if we have structure.json or legacy db
            var dbEntry = archive.GetEntry("yibofile_data.db");
            if (dbEntry != null)
            {
                // Close DB connections to release file lock
                try
                {
                    YiboFile.DatabaseManager.Shutdown();
                    // Brief pause to allow file system to release locks
                    await Task.Delay(200);
                }
                catch { }

                // Fallback: Copy to DB path
                if (File.Exists(_pathProvider.DatabaseFilePath))
                    File.Copy(_pathProvider.DatabaseFilePath, _pathProvider.DatabaseFilePath + ".bak", true);

                // This might fail if file is still locked
                try
                {
                    dbEntry.ExtractToFile(_pathProvider.DatabaseFilePath, overwrite: true);

                    // Re-initialize DB? Or ask for restart.
                    // Ideally, after import, we should restart the app.
                }
                catch (IOException)
                {
                    // If locked, maybe fail or schedule on restart?
                    throw new Exception("Cannot overwrite database file because it is in use. Please restart the application to apply changes.");
                }
            }
            await Task.CompletedTask;
        }

        private async Task ImportFileDataAsync(ZipArchive archive)
        {
            // Similar to Structure, currently bundled in DB
            await Task.CompletedTask;
        }

        private async Task ImportThemesAsync(ZipArchive archive)
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.StartsWith(ThemesDirectoryName + "/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(entry.Name))
                {
                    var destPath = Path.Combine(_pathProvider.CustomThemesDirectory, entry.Name);
                    Directory.CreateDirectory(_pathProvider.CustomThemesDirectory);
                    entry.ExtractToFile(destPath, overwrite: true);
                }
            }
            await Task.CompletedTask;
        }
    }
}
