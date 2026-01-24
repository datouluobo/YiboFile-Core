using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using YiboFile.Services.Core.Error;
using YiboFile.Services.FileSystem;

namespace YiboFile.Services.Backup
{
    public class BackupService : IBackupService
    {
        private readonly string _backupRoot;
        private readonly ErrorService _errorService;
        private const string ManifestFileName = "manifest.json";

        public BackupService(ErrorService errorService = null)
        {
            _errorService = errorService;
            _backupRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YiboFile", "Backup");

            if (!Directory.Exists(_backupRoot))
            {
                Directory.CreateDirectory(_backupRoot);
            }
        }

        public async Task<BackupRecord> CreateBackupAsync(string sourcePath)
        {
            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                return null;

            try
            {
                var today = DateTime.Now.ToString("yyyyMMdd");
                var backupDir = Path.Combine(_backupRoot, today);
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                var record = new BackupRecord
                {
                    OriginalPath = sourcePath,
                    DeletionTime = DateTime.Now,
                    IsDirectory = Directory.Exists(sourcePath)
                };

                // Determine backup filename (use ID to avoid conflicts)
                var ext = record.IsDirectory ? "" : Path.GetExtension(sourcePath);
                var backupFileName = $"{record.Id}{ext}";
                var backupPath = Path.Combine(backupDir, backupFileName);
                record.BackupPath = backupPath;

                // Move file/folder
                await Task.Run(async () =>
                {
                    bool sameVolume = FileSystemCoreUtils.IsSameVolume(sourcePath, backupPath);

                    if (record.IsDirectory)
                    {
                        if (sameVolume)
                        {
                            Directory.Move(sourcePath, backupPath);
                        }
                        else
                        {
                            FileSystemCoreUtils.CopyDirectory(sourcePath, backupPath);
                            await FileSystemCoreUtils.SafeDeleteDirectoryAsync(sourcePath);
                        }
                    }
                    else
                    {
                        if (sameVolume)
                        {
                            File.Move(sourcePath, backupPath, true);
                        }
                        else
                        {
                            File.Copy(sourcePath, backupPath, true);
                            await FileSystemCoreUtils.SafeDeleteFileAsync(sourcePath);
                        }
                    }

                    if (record.IsDirectory)
                        record.Size = FileSystemCoreUtils.GetDirectorySize(backupPath);
                    else
                        record.Size = new System.IO.FileInfo(backupPath).Length;
                });

                // Update Manifest
                await UpdateManifestAsync(today, manifest =>
                {
                    manifest.Records.Add(record);
                });

                return record;
            }
            catch (Exception ex)
            {
                _errorService?.ReportError($"Failed to create backup for {sourcePath}", ErrorSeverity.Error, ex);
                return null;
            }
        }

        public async Task<string> RestoreAsync(BackupRecord record, string targetPath = null)
        {
            var sourcePath = record.BackupPath;
            var destPath = targetPath ?? record.OriginalPath;

            if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                throw new FileNotFoundException("Backup file not found", sourcePath);

            // Ensure parent dir exists
            var destDir = Path.GetDirectoryName(destPath);
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            // Check conflict
            if (File.Exists(destPath) || Directory.Exists(destPath))
            {
                destPath = FileSystemCoreUtils.GetUniquePath(destPath);
            }

            await Task.Run(() =>
            {
                if (record.IsDirectory)
                {
                    Directory.Move(sourcePath, destPath);
                }
                else
                {
                    File.Move(sourcePath, destPath);
                }
            });

            // Remove from manifest
            var dateDir = Path.GetFileName(Path.GetDirectoryName(record.BackupPath));
            await UpdateManifestAsync(dateDir, manifest =>
            {
                var r = manifest.Records.FirstOrDefault(x => x.Id == record.Id);
                if (r != null) manifest.Records.Remove(r);
            });

            return destPath;
        }

        public async Task DeleteBackupAsync(BackupRecord record)
        {
            if (File.Exists(record.BackupPath)) File.Delete(record.BackupPath);
            else if (Directory.Exists(record.BackupPath)) Directory.Delete(record.BackupPath, true);

            var dateDir = Path.GetFileName(Path.GetDirectoryName(record.BackupPath));
            await UpdateManifestAsync(dateDir, manifest =>
            {
                var r = manifest.Records.FirstOrDefault(x => x.Id == record.Id);
                if (r != null) manifest.Records.Remove(r);
            });
        }

        public async Task<List<BackupManifest>> GetAllBackupsAsync()
        {
            var result = new List<BackupManifest>();
            var dirs = Directory.GetDirectories(_backupRoot);

            foreach (var dir in dirs)
            {
                var date = Path.GetFileName(dir);
                var manifestPath = Path.Combine(dir, ManifestFileName);
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(manifestPath);
                        var manifest = JsonSerializer.Deserialize<BackupManifest>(json);
                        if (manifest != null)
                        {
                            if (string.IsNullOrEmpty(manifest.Date)) manifest.Date = date;
                            result.Add(manifest);
                        }
                    }
                    catch { }
                }
            }

            return result.OrderByDescending(x => x.Date).ToList();
        }

        public async Task CleanOldBackupsAsync(int daysToKeep)
        {
            if (daysToKeep <= 0) return;

            var dirs = Directory.GetDirectories(_backupRoot);
            var threshold = DateTime.Now.Date.AddDays(-daysToKeep);

            await Task.Run(() =>
            {
                foreach (var dir in dirs)
                {
                    var dateStr = Path.GetFileName(dir);
                    if (DateTime.TryParseExact(dateStr, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
                    {
                        if (date < threshold)
                        {
                            try
                            {
                                Directory.Delete(dir, true);
                            }
                            catch { }
                        }
                    }
                }
            });
        }

        private async Task UpdateManifestAsync(string date, Action<BackupManifest> specificUpdate)
        {
            var dir = Path.Combine(_backupRoot, date);
            var manifestPath = Path.Combine(dir, ManifestFileName);
            BackupManifest manifest = null;

            if (File.Exists(manifestPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(manifestPath);
                    manifest = JsonSerializer.Deserialize<BackupManifest>(json);
                }
                catch { }
            }

            if (manifest == null)
            {
                manifest = new BackupManifest { Date = date };
            }

            specificUpdate(manifest);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var newJson = JsonSerializer.Serialize(manifest, options);
            await File.WriteAllTextAsync(manifestPath, newJson);
        }
    }
}
