using System;
using System.Threading;
using System.Threading.Tasks;

namespace YiboFile.Services.FileList
{
    /// <summary>
    /// Folder Size Calculation Service (Facade/Compatibility Layer)
    /// Delegates to FolderSizeService for actual computation, preserving old interface compatibility
    /// </summary>
    public class FolderSizeCalculationService : IDisposable
    {
        private readonly FolderSizeService _folderSizeService;

        public FolderSizeCalculationService(FolderSizeService folderSizeService)
        {
            _folderSizeService = folderSizeService ?? throw new ArgumentNullException(nameof(folderSizeService));
        }

        public FolderSizeService Service => _folderSizeService;

        public Task CalculateAndUpdateFolderSizeAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            _folderSizeService.EnqueueSingle(folderPath, cancellationToken);
            return Task.CompletedTask;
        }

        public Task CalculateAndUpdateFolderSizeIfChangedAsync(string folderPath, long cachedSize, CancellationToken cancellationToken = default)
        {
            _folderSizeService.EnqueueSingle(folderPath, cancellationToken);
            return Task.CompletedTask;
        }

        public Task CalculateAllSubfolderSizesOnFirstOpenAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void CleanupFolderSizeCacheOnStartup()
        {
            _folderSizeService.CleanupFolderSizeCacheOnStartup();
        }

        public void Cancel()
        {
        }

        public void Dispose()
        {
        }
    }
}