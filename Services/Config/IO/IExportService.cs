using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace YiboFile.Services.Config.IO
{
    /// <summary>
    /// Supported export module types.
    /// </summary>
    public enum ExportModuleType
    {
        Settings,
        Structure,
        FileData,
        Themes
    }

    /// <summary>
    /// Metadata for an export package.
    /// </summary>
    public class ExportManifest
    {
        public string Version { get; set; } = "1.0";
        public DateTime ExportTime { get; set; }
        public List<ExportModuleType> Modules { get; set; } = new List<ExportModuleType>();
        public string AppVersion { get; set; }
    }

    /// <summary>
    /// Service for handling configuration export operations.
    /// </summary>
    public interface IExportService
    {
        /// <summary>
        /// Estimates the size of the export package for the selected modules.
        /// </summary>
        Task<long> EstimateSizeAsync(IEnumerable<ExportModuleType> modules);

        /// <summary>
        /// Exports the selected modules to a ZIP file.
        /// </summary>
        Task ExportAsync(string targetZipPath, IEnumerable<ExportModuleType> modules);
    }
}
