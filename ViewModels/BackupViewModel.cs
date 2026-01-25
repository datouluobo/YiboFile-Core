using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YiboFile.Services.Backup;
using Microsoft.Extensions.DependencyInjection;


namespace YiboFile.ViewModels
{
    public class BackupViewModel : BaseViewModel
    {
        private readonly IBackupService _backupService;
        private ObservableCollection<BackupManifest> _backups;
        private BackupManifest _selectedBackup;
        private ObservableCollection<BackupRecord> _selectedFiles;
        private bool _isLoading;
        private BackupRecord _currentPreviewRecord;
        private UIElement _previewElement;
        private AppConfig _config;

        public BackupViewModel(IBackupService backupService)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

            LoadCommand = new RelayCommand(async () => await LoadBackupsAsync());
            RestoreCommand = new RelayCommand<BackupRecord>(async (record) => await RestoreFileAsync(record));
            DeleteCommand = new RelayCommand<BackupRecord>(async (record) => await DeleteFileAsync(record));
            CleanCommand = new RelayCommand(async () => await CleanBackupsAsync());
            RestoreSelectedCommand = new RelayCommand(async () => await RestoreSelectedFilesAsync());
            DeleteSelectedCommand = new RelayCommand(async () => await DeleteSelectedFilesAsync());

            _config = App.ServiceProvider?.GetService<AppConfig>();
        }

        public ObservableCollection<BackupManifest> Backups
        {
            get => _backups;
            set => SetProperty(ref _backups, value);
        }

        public BackupManifest SelectedBackup
        {
            get => _selectedBackup;
            set
            {
                if (SetProperty(ref _selectedBackup, value))
                {
                    // Update Detail View
                }
            }
        }

        public ObservableCollection<BackupRecord> SelectedFiles
        {
            get => _selectedFiles;
            set => SetProperty(ref _selectedFiles, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public BackupRecord CurrentPreviewRecord
        {
            get => _currentPreviewRecord;
            set
            {
                if (SetProperty(ref _currentPreviewRecord, value))
                {
                    UpdatePreview();
                }
            }
        }

        public UIElement PreviewElement
        {
            get => _previewElement;
            set => SetProperty(ref _previewElement, value);
        }

        // Commands
        public ICommand LoadCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CleanCommand { get; }
        public ICommand RestoreSelectedCommand { get; }
        public ICommand DeleteSelectedCommand { get; }


        public async Task LoadBackupsAsync()
        {
            IsLoading = true;
            try
            {
                // Auto cleanup if enabled
                if (_config != null && _config.BackupRetentionDays > 0)
                {
                    await _backupService.CleanOldBackupsAsync(_config.BackupRetentionDays);
                }

                var list = await _backupService.GetAllBackupsAsync();
                Backups = new ObservableCollection<BackupManifest>(list);
                if (Backups.Count > 0) SelectedBackup = Backups[0];
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RestoreFileAsync(BackupRecord record)
        {
            if (record == null) return;
            try
            {
                await _backupService.RestoreAsync(record);
                // Refresh or remove from list? 
                // Restore removes from manifest, so we should refresh UI
                RemoveRecordFromUI(record);
                YiboFile.DialogService.Info($"已恢复: {System.IO.Path.GetFileName(record.OriginalPath)}");
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"恢复失败: {ex.Message}");
            }
        }

        private async Task DeleteFileAsync(BackupRecord record)
        {
            if (record == null) return;
            if (!YiboFile.DialogService.Ask($"确定要永久删除备份文件 \"{System.IO.Path.GetFileName(record.OriginalPath)}\" 吗?", "确认删除"))
                return;

            try
            {
                await _backupService.DeleteBackupAsync(record);
                RemoveRecordFromUI(record);
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"删除失败: {ex.Message}");
            }
        }

        private async Task CleanBackupsAsync()
        {
            int days = _config?.BackupRetentionDays ?? 30;
            if (days <= 0) days = 30;

            if (YiboFile.DialogService.Ask($"确定要清理超过 {days} 天的旧备份吗?", "清理确认"))
            {
                await _backupService.CleanOldBackupsAsync(days);
                await LoadBackupsAsync();
            }
        }

        private async Task RestoreSelectedFilesAsync()
        {
            if (SelectedFiles == null || SelectedFiles.Count == 0 || SelectedBackup == null) return;

            IsLoading = true;
            try
            {
                var filesToRestore = SelectedFiles.ToList();
                foreach (var record in filesToRestore)
                {
                    await _backupService.RestoreAsync(record);
                    RemoveRecordFromUI(record);
                }
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"还原失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task DeleteSelectedFilesAsync()
        {
            if (SelectedFiles == null || SelectedFiles.Count == 0 || SelectedBackup == null) return;

            if (!YiboFile.DialogService.Ask($"确定要永久删除这 {SelectedFiles.Count} 个备份文件吗？", "确认删除")) return;

            IsLoading = true;
            try
            {
                var filesToDelete = SelectedFiles.ToList();
                foreach (var record in filesToDelete)
                {
                    await _backupService.DeleteBackupAsync(record);
                    RemoveRecordFromUI(record);
                }
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"删除失败: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void RemoveRecordFromUI(BackupRecord record)
        {
            if (record == CurrentPreviewRecord)
            {
                CurrentPreviewRecord = null;
            }

            if (SelectedBackup != null && SelectedBackup.Records.Contains(record))
            {
                SelectedBackup.Records.Remove(record);
                OnPropertyChanged(nameof(SelectedBackup));
            }
        }

        private void UpdatePreview()
        {
            if (CurrentPreviewRecord != null && !CurrentPreviewRecord.IsDirectory && System.IO.File.Exists(CurrentPreviewRecord.BackupPath))
            {
                PreviewElement = YiboFile.Previews.PreviewFactory.CreatePreview(CurrentPreviewRecord.BackupPath);
            }
            else
            {
                PreviewElement = null;
            }
        }
    }
}
