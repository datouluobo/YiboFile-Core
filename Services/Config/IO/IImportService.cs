using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YiboFile.Services.Config.IO;

namespace YiboFile.Services.Config.IO
{
    /// <summary>
    /// Result of an import operation simulation or execution.
    /// </summary>
    public class ImportResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        // Summary of what will be/was imported
        public int SettingsCount { get; set; }
        public int LibraryCount { get; set; }
        public int FavoriteCount { get; set; }
        public int TagCount { get; set; }
        public int FileMetadataCount { get; set; }
        public int ThemeCount { get; set; }

        // Mismatched paths found during structure import
        public List<PathMismatch> PathMismatches { get; set; } = new List<PathMismatch>();
    }

    /// <summary>
    /// Represents a file path that exists in the imported data but not on the local system.
    /// </summary>
    public class PathMismatch
    {
        public string OriginalPath { get; set; }
        public string Type { get; set; } // "Library", "Favorite", "IndexedPath"
        public string Name { get; set; }

        // Resolution action (to be set by user)
        public PathResolutionAction Action { get; set; } = PathResolutionAction.Ignore;
        public string NewPath { get; set; }
    }

    public enum PathResolutionAction
    {
        Ignore,
        Create,
        Map
    }

    /// <summary>
    /// Service for handling configuration import operations.
    /// </summary>
    public interface IImportService
    {
        /// <summary>
        /// Reads the manifest from a ZIP file to determine available modules.
        /// </summary>
        Task<ExportManifest> ReadManifestAsync(string zipPath);

        /// <summary>
        /// Simulates the import process to identify potential issues (like path mismatches).
        /// </summary>
        Task<ImportResult> SimulateImportAsync(string zipPath, IEnumerable<ExportModuleType> selectedModules);

        /// <summary>
        /// Executes the import process, applying resolutions for path mismatches.
        /// </summary>
        Task ExecuteImportAsync(string zipPath, IEnumerable<ExportModuleType> selectedModules, IEnumerable<PathMismatch> resolutions = null);
    }
}
