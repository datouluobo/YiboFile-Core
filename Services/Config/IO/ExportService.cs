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
        private readonly IServiceProvider _serviceProvider;
        private const string ManifestFileName = "manifest.json";
        private const string SettingsFileName = "settings.json";
        private const string StateFileName = "state.json";
        private const string StructureFileName = "structure.json";
        private const string FileDataFileName = "filedata.json";
        private const string ThemesDirectoryName = "themes";

        public ExportService(IConfigPathProvider pathProvider, IServiceProvider serviceProvider)
        {
            _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
            var structure = new YiboFile.Services.Config.IO.Models.StructureExportDto();

            // 1. Tag Groups and Tags
            var tagsRepo = (YiboFile.Services.Data.Repositories.ITagsRepository)_serviceProvider.GetService(typeof(YiboFile.Services.Data.Repositories.ITagsRepository));
            if (tagsRepo != null)
            {
                var groups = tagsRepo.GetTagGroups();
                foreach (var g in groups)
                {
                    var groupDto = new YiboFile.Services.Config.IO.Models.TagGroupDto
                    {
                        Id = g.Id,
                        Name = g.Name,
                        Color = g.Color
                    };
                    var tags = tagsRepo.GetTagsByGroup(g.Id);
                    foreach (var t in tags)
                    {
                        groupDto.Tags.Add(new YiboFile.Services.Config.IO.Models.TagDto { Id = t.Id, Name = t.Name, Color = t.Color, GroupId = t.GroupId });
                    }
                    structure.TagGroups.Add(groupDto);
                }
                // Handle ungrouped tags
                var ungrouped = tagsRepo.GetUngroupedTags();
                if (ungrouped.Count > 0)
                {
                    var ungroupedDto = new YiboFile.Services.Config.IO.Models.TagGroupDto { Id = 0, Name = "Ungrouped" };
                    foreach (var t in ungrouped)
                    {
                        ungroupedDto.Tags.Add(new YiboFile.Services.Config.IO.Models.TagDto { Id = t.Id, Name = t.Name, Color = t.Color, GroupId = t.GroupId });
                    }
                    structure.TagGroups.Add(ungroupedDto);
                }
            }

            // 2. Libraries
            var libRepo = (YiboFile.Services.Data.Repositories.ILibraryRepository)_serviceProvider.GetService(typeof(YiboFile.Services.Data.Repositories.ILibraryRepository));
            if (libRepo != null)
            {
                var libs = libRepo.GetAllLibraries();
                foreach (var lib in libs)
                {
                    var libDto = new YiboFile.Services.Config.IO.Models.LibraryDto
                    {
                        Id = lib.Id,
                        Name = lib.Name
                    };
                    var paths = libRepo.GetLibraryPaths(lib.Id);
                    foreach (var p in paths)
                    {
                        libDto.Paths.Add(new YiboFile.Services.Config.IO.Models.LibraryPathDto { Path = p.Path, DisplayName = p.DisplayName });
                    }
                    structure.Libraries.Add(libDto);
                }
            }

            // 3. Favorites
            var favRepo = (YiboFile.Services.Data.Repositories.IFavoriteRepository)_serviceProvider.GetService(typeof(YiboFile.Services.Data.Repositories.IFavoriteRepository));
            if (favRepo != null)
            {
                var groups = favRepo.GetAllGroups();
                var allFavs = favRepo.GetAllFavorites();
                foreach (var g in groups)
                {
                    var groupDto = new YiboFile.Services.Config.IO.Models.FavoriteGroupDto
                    {
                        Id = g.Id,
                        Name = g.Name,
                        SortOrder = g.SortOrder
                    };
                    var favsInGroup = allFavs.Where(f => f.GroupId == g.Id).OrderBy(f => f.SortOrder);
                    foreach (var f in favsInGroup)
                    {
                        groupDto.Favorites.Add(new YiboFile.Services.Config.IO.Models.FavoriteDto
                        {
                            Path = f.Path,
                            IsDirectory = f.IsDirectory,
                            DisplayName = f.DisplayName,
                            SortOrder = f.SortOrder,
                            GroupId = f.GroupId
                        });
                    }
                    structure.FavoriteGroups.Add(groupDto);
                }
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(structure, options);
            File.WriteAllText(Path.Combine(outputDir, StructureFileName), json);

            await Task.CompletedTask;
        }

        private async Task ExportFileDataAsync(string outputDir)
        {
            var fileData = new YiboFile.Services.Config.IO.Models.FileDataExportDto();

            // 1. File Tags
            var tagsRepo = (YiboFile.Services.Data.Repositories.ITagsRepository)_serviceProvider.GetService(typeof(YiboFile.Services.Data.Repositories.ITagsRepository));
            if (tagsRepo != null)
            {
                var allTags = tagsRepo.GetAllTags();
                foreach (var tag in allTags)
                {
                    var files = tagsRepo.GetFilesByTag(tag.Id);
                    foreach (var file in files)
                    {
                        if (!fileData.FileTags.ContainsKey(file))
                            fileData.FileTags[file] = new List<int>();
                        fileData.FileTags[file].Add(tag.Id);
                    }
                }
            }

            // 2. File Notes
            var notesRepo = (YiboFile.Services.Data.Repositories.INotesRepository)_serviceProvider.GetService(typeof(YiboFile.Services.Data.Repositories.INotesRepository));
            if (notesRepo != null)
            {
                var notedFiles = notesRepo.GetAllNotedFiles();
                if (notedFiles != null && notedFiles.Count > 0)
                {
                    var notesBatch = notesRepo.GetNotesBatch(notedFiles);
                    foreach (var kvp in notesBatch)
                    {
                        fileData.FileNotes[kvp.Key] = kvp.Value;
                    }
                }
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(fileData, options);
            File.WriteAllText(Path.Combine(outputDir, FileDataFileName), json);

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
