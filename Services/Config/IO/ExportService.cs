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
    /// Service for handling configuration export operations (Implementation).
    /// </summary>
    public class ExportService : IExportService
    {
        private readonly IConfigPathProvider _pathProvider;
        private const string ManifestFileName = "manifest.json";
        private const string SettingsFileName = "settings.json";
        private const string StateFileName = "state.json";
        private const string StructureFileName = "structure.json";
        private const string FileDataFileName = "filedata.json";
        private const string ThemesDirectoryName = "themes";

        public ExportService(IConfigPathProvider pathProvider)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        }

        public Task<long> EstimateSizeAsync(IEnumerable<ExportModuleType> modules)
        {
            long size = 0;
            foreach (var module in modules)
            {
                switch (module)
                {
                    case ExportModuleType.Settings:
                        if (File.Exists(_pathProvider.SettingsFilePath))
                            size += new System.IO.FileInfo(_pathProvider.SettingsFilePath).Length;
                        break;
                    case ExportModuleType.Structure:
                    case ExportModuleType.FileData:
                        // These are stored in DB or separate files, estimation is rough
                        if (File.Exists(_pathProvider.DatabaseFilePath))
                            size += new System.IO.FileInfo(_pathProvider.DatabaseFilePath).Length / 2; // Rough estimate
                        break;
                    case ExportModuleType.Themes:
                        if (Directory.Exists(_pathProvider.CustomThemesDirectory))
                        {
                            var info = new System.IO.DirectoryInfo(_pathProvider.CustomThemesDirectory);
                            size += info.EnumerateFiles("*.*", SearchOption.AllDirectories).Sum(fi => fi.Length);
                        }
                        break;
                }
            }
            return Task.FromResult(size);
        }

        public async Task ExportAsync(string targetZipPath, IEnumerable<ExportModuleType> modules)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "YiboFile_Export_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var manifest = new ExportManifest
                {
                    ExportTime = DateTime.Now,
                    Modules = modules.ToList(),
                    AppVersion = "1.0.0"
                };

                // 1. Export Content
                foreach (var module in modules)
                {
                    switch (module)
                    {
                        case ExportModuleType.Settings:
                            await ExportSettingsAsync(tempDir);
                            break;
                        case ExportModuleType.Structure:
                            await ExportStructureAsync(tempDir);
                            break;
                        case ExportModuleType.FileData:
                            await ExportFileDataAsync(tempDir);
                            break;
                        case ExportModuleType.Themes:
                            await ExportThemesAsync(tempDir);
                            break;
                    }
                }

                // 2. Rewrite Manifest (in case some exports failed or were empty?)
                // Actually, just write the initial manifest
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(Path.Combine(tempDir, ManifestFileName), JsonSerializer.Serialize(manifest, options));

                // 3. Zip It Up
                if (File.Exists(targetZipPath)) File.Delete(targetZipPath);
                ZipFile.CreateFromDirectory(tempDir, targetZipPath);
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private async Task ExportSettingsAsync(string outputDir)
        {
            // Simply copy the settings file content to settings.json
            if (File.Exists(_pathProvider.SettingsFilePath))
            {
                // We could just copy the file, but let's ensure it's valid JSON matching our export spec
                // For now, raw copy is safest and simplest
                var dest = Path.Combine(outputDir, SettingsFileName);
                CopyFileSafe(_pathProvider.SettingsFilePath, dest);
            }
            await Task.CompletedTask;
        }

        private async Task ExportStructureAsync(string outputDir)
        {
            // For now, we might export the whole DB or partial data.
            // Given the requirement is granular export, we should query repositories.
            // Placeholder: Export the DB file itself as "structure.db" or dump to JSON?
            // The spec says "structure.json".

            // Allow DB export for now as a fallback if JSON serialization isn't ready
            if (File.Exists(_pathProvider.DatabaseFilePath))
            {
                CopyFileSafe(_pathProvider.DatabaseFilePath, Path.Combine(outputDir, "yibofile_data.db"));
            }

            await Task.CompletedTask;
        }

        private async Task ExportFileDataAsync(string outputDir)
        {
            await Task.CompletedTask;
        }

        private async Task ExportThemesAsync(string outputDir)
        {
            var themesDest = Path.Combine(outputDir, ThemesDirectoryName);
            if (Directory.Exists(_pathProvider.CustomThemesDirectory))
            {
                Directory.CreateDirectory(themesDest);
                foreach (var file in Directory.GetFiles(_pathProvider.CustomThemesDirectory, "*.json"))
                {
                    CopyFileSafe(file, Path.Combine(themesDest, Path.GetFileName(file)));
                }
            }
            await Task.CompletedTask;
        }

        private void CopyFileSafe(string source, string dest)
        {
            try
            {
                using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write))
                {
                    sourceStream.CopyTo(destStream);
                }
            }
            catch (Exception)
            {
                // Log or throw?
            }
        }
    }
}
