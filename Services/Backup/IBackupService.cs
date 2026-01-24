using System.Collections.Generic;
using System.Threading.Tasks;

namespace YiboFile.Services.Backup
{
    public interface IBackupService
    {
        Task<BackupRecord> CreateBackupAsync(string sourcePath);
        Task<string> RestoreAsync(BackupRecord record, string targetPath = null);
        Task DeleteBackupAsync(BackupRecord record);
        Task<List<BackupManifest>> GetAllBackupsAsync();
        Task CleanOldBackupsAsync(int daysToKeep);
    }
}
