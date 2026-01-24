using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Dialogs;
using YiboFile.Services.Core.Error;
using YiboFile.Services.FileOperations.Undo;
using YiboFile.Services.FileSystem;

// 使用 Dialogs 命名空间的 ConflictResolution
using ConflictResolution = YiboFile.Dialogs.ConflictResolution;
using YiboFile.Services.FileOperations.TaskQueue;
using TaskStatus = YiboFile.Services.FileOperations.TaskQueue.TaskStatus;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 文件操作服务 - 所有文件操作的统一入口
    /// 工具栏、快捷键、右键菜单、拖放都应调用此服务
    /// </summary>
    public class FileOperationService
    {
        private readonly ClipboardService _clipboard;
        private readonly ErrorService _errorService;
        private readonly UndoService _undoService;
        private readonly TaskQueueService _taskQueueService;
        private readonly YiboFile.Services.Backup.IBackupService _backupService;
        private readonly Func<FileOperationContext> _getContext;

        /// <summary>
        /// 进度更新事件
        /// </summary>
        public event Action<int, int, string> ProgressChanged;

        /// <summary>
        /// 操作开始事件
        /// </summary>
        public event Action<string> OperationStarted;

        /// <summary>
        /// 操作完成事件
        /// </summary>
        public event Action<FileOperationResult> OperationCompleted;

        public FileOperationService(
            Func<FileOperationContext> contextProvider,
            ErrorService errorService = null,
            UndoService undoService = null,
            TaskQueueService taskQueueService = null,
            YiboFile.Services.Backup.IBackupService backupService = null)
        {
            _getContext = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            _clipboard = ClipboardService.Instance;
            _errorService = errorService;
            _undoService = undoService;
            _taskQueueService = taskQueueService;
            _backupService = backupService;
        }

        #region Copy / Cut

        /// <summary>
        /// 复制选中的文件到剪贴板
        /// </summary>
        public async Task<bool> CopyAsync(IEnumerable<string> paths)
        {
            var pathList = paths?.ToList();
            if (pathList == null || pathList.Count == 0) return false;

            return await _clipboard.SetCopyPathsAsync(pathList);
        }

        /// <summary>
        /// 剪切选中的文件到剪贴板
        /// </summary>
        public async Task<bool> CutAsync(IEnumerable<string> paths)
        {
            var pathList = paths?.ToList();
            if (pathList == null || pathList.Count == 0) return false;

            return await _clipboard.SetCutPathsAsync(pathList);
        }

        #endregion

        #region Paste

        /// <summary>
        /// 粘贴剪贴板内容到目标路径
        /// </summary>
        public async Task<FileOperationResult> PasteAsync(CancellationToken ct = default)
        {
            var context = _getContext();
            if (context == null || !context.CanPerformOperation())
            {
                return FileOperationResult.Failed("目标路径无效");
            }
            var (sourcePaths, isCut) = await _clipboard.GetPathsFromClipboardAsync();
            if (sourcePaths.Count == 0)
            {
                return FileOperationResult.Failed("剪贴板为空");
            }

            var targetPath = context.GetEffectiveTargetPath();
            OperationStarted?.Invoke(isCut ? "正在移动文件..." : "正在复制文件...");

            var failedItems = new List<string>();
            int processedCount = 0;
            int totalCount = sourcePaths.Count;
            ConflictResolution? cachedResolution = null;

            // 用于撤销操作
            var undoActionList = new List<UndoableAction>();

            // 创建并注册任务
            var task = new FileOperationTask
            {
                Description = isCut ? "移动文件" : "复制文件",
                TotalItems = sourcePaths.Count,
                Status = TaskStatus.Running,
                CurrentFile = "准备中...",
                IsSilent = totalCount <= 5,
                StartTime = DateTime.Now
            };
            _taskQueueService?.EnqueueTask(task);

            foreach (var sourcePath in sourcePaths)
            {
                if (task != null && totalCount > 0)
                {
                    task.Progress = (int)((double)processedCount / totalCount * 100);
                }

                if (ct.IsCancellationRequested || (task != null && task.Status == TaskStatus.Canceling))
                {
                    if (task != null) task.Status = TaskStatus.Canceled;
                    break;
                }

                if (task != null)
                {
                    task.WaitIfPaused();
                    task.CurrentFile = Path.GetFileName(sourcePath);
                }

                if (string.IsNullOrEmpty(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
                {
                    processedCount++;
                    continue;
                }

                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(targetPath, fileName);
                bool isDir = Directory.Exists(sourcePath);

                ProgressChanged?.Invoke(processedCount, totalCount, fileName);

                if (isDir)
                {
                    var srcFull = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var targetFull = Path.GetFullPath(targetPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    bool isRecursive = targetFull.StartsWith(srcFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(targetFull, srcFull, StringComparison.OrdinalIgnoreCase);

                    if (isRecursive)
                    {
                        failedItems.Add($"{fileName}: 目标文件夹是源文件夹的子文件夹");
                        processedCount++;
                        continue;
                    }
                }

                try
                {
                    var srcFullPath = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var destFullPath = Path.GetFullPath(destPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (string.Equals(srcFullPath, destFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isCut)
                        {
                            destPath = FileSystemCoreUtils.GetUniquePath(destPath);
                        }
                        else
                        {
                            processedCount++;
                            continue;
                        }
                    }

                    bool hasConflict = File.Exists(destPath) || Directory.Exists(destPath);
                    if (hasConflict)
                    {
                        var sourceDir = Path.GetDirectoryName(sourcePath);
                        bool isSameFolder = string.Equals(sourceDir, targetPath, StringComparison.OrdinalIgnoreCase);

                        if (isSameFolder)
                        {
                            destPath = FileSystemCoreUtils.GetUniquePath(destPath);
                        }
                        else
                        {
                            var resolution = cachedResolution;
                            if (!resolution.HasValue)
                            {
                                var (userRes, applyAll) = await ShowConflictDialogAsync(fileName, totalCount > 1, task?.CancellationTokenSource.Token ?? ct);
                                resolution = userRes;
                                if (applyAll) cachedResolution = resolution;
                            }

                            switch (resolution.Value)
                            {
                                case ConflictResolution.CancelAll:
                                    if (task != null) task.Status = TaskStatus.Canceled;
                                    return FileOperationResult.Cancelled();
                                case ConflictResolution.Skip:
                                    processedCount++;
                                    continue;
                                case ConflictResolution.Rename:
                                    destPath = FileSystemCoreUtils.GetUniquePath(destPath);
                                    break;
                                case ConflictResolution.Overwrite:
                                    if (File.Exists(destPath)) File.Delete(destPath);
                                    else if (Directory.Exists(destPath)) Directory.Delete(destPath, true);
                                    break;
                            }
                        }
                    }

                    // 执行复制/移动
                    await Task.Run(async () =>
                    {
                        if (isCut)
                        {
                            bool sameVolume = FileSystemCoreUtils.IsSameVolume(sourcePath, destPath);
                            if (sameVolume)
                            {
                                // 同卷移动
                                if (isDir) Directory.Move(sourcePath, destPath);
                                else File.Move(sourcePath, destPath, true);
                            }
                            else
                            {
                                // 跨卷移动：使用备份服务中转，安全第一
                                if (_backupService != null)
                                {
                                    var record = await _backupService.CreateBackupAsync(sourcePath);
                                    if (record != null)
                                    {
                                        destPath = await _backupService.RestoreAsync(record, destPath);
                                    }
                                    else throw new Exception("安全移动：备份失败");
                                }
                                else
                                {
                                    // 无备份服务回退
                                    if (isDir)
                                    {
                                        FileSystemCoreUtils.CopyDirectory(sourcePath, destPath);
                                        await FileSystemCoreUtils.SafeDeleteDirectoryAsync(sourcePath);
                                    }
                                    else
                                    {
                                        File.Copy(sourcePath, destPath, true);
                                        await FileSystemCoreUtils.SafeDeleteFileAsync(sourcePath);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // 复制
                            if (isDir) CopyDirectory(sourcePath, destPath, task);
                            else File.Copy(sourcePath, destPath, true);
                        }
                    }, task?.CancellationTokenSource.Token ?? ct);

                    // 记录撤销操作 (统一使用通用备份撤销)
                    if (_undoService != null)
                    {
                        if (isCut)
                        {
                            // 移动撤销：目前仍保留 MoveUndoAction 处理同目录/同卷的轻量重命名，
                            // 但对于跨卷或复杂操作，建议也逐步统一。此处暂时保留 MoveUndoAction。
                            undoActionList.Add(new MoveUndoAction(sourcePath, destPath, isDir));
                        }
                        else
                        {
                            // 新建/复制撤销：统一接入备份服务，撤销粘贴即“删除入库”
                            if (_backupService != null)
                            {
                                undoActionList.Add(new BackupRestoreUndoAction(_backupService, destPath));
                            }
                            else
                            {
                                undoActionList.Add(new NewFileUndoAction(destPath, isDir));
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    if (task != null) task.Status = TaskStatus.Canceled;
                    break;
                }
                catch (Exception ex)
                {
                    failedItems.Add($"{fileName}: {ex.Message}");
                }

                processedCount++;
            }

            if (undoActionList.Count > 0 && _undoService != null)
            {
                var compositeAction = new CompositeUndoAction(isCut ? "移动文件" : "复制文件");
                foreach (var action in undoActionList) compositeAction.AddAction(action);
                _undoService.RecordAction(compositeAction);
            }

            context.Refresh();
            if (isCut && !ct.IsCancellationRequested) await _clipboard.ClearCutStateAsync();

            var result = new FileOperationResult
            {
                Success = failedItems.Count == 0,
                ProcessedCount = processedCount,
                FailedCount = failedItems.Count,
                FailedItems = failedItems
            };

            if (task != null && task.Status != TaskStatus.Canceled)
            {
                task.Status = TaskStatus.Completed;
                task.Progress = 100;

                // 静默任务完成时显示通知
                if (task.IsSilent)
                {
                    YiboFile.Services.Core.NotificationService.ShowSuccess($"{task.Description} 已完成");
                }
            }

            OperationCompleted?.Invoke(result);
            ProgressChanged?.Invoke(totalCount, totalCount, "完成");

            if (failedItems.Count > 0)
            {
                _errorService?.ReportError($"以下项目操作失败:\n{string.Join("\n", failedItems.Take(5))}", ErrorSeverity.Error);
            }

            return result;
        }

        #endregion

        #region Delete

        public async Task<FileOperationResult> DeleteAsync(IEnumerable<FileSystemItem> items, bool permanent = false, CancellationToken ct = default)
        {
            var itemList = items?.ToList();
            if (itemList == null || itemList.Count == 0) return FileOperationResult.Failed("没有选中任何项目");

            var message = itemList.Count == 1 ? $"确定要删除 \"{itemList[0].Name}\" 吗？" : $"确定要删除这 {itemList.Count} 个项目吗？";
            if (!ShowConfirmDialog(message, "确认删除")) return FileOperationResult.Cancelled();

            OperationStarted?.Invoke("正在删除文件...");
            var task = new FileOperationTask
            {
                Description = "删除文件",
                TotalItems = itemList.Count,
                Status = TaskStatus.Running,
                IsSilent = itemList.Count <= 5,
                StartTime = DateTime.Now
            };
            _taskQueueService?.EnqueueTask(task);

            var failedItems = new List<string>();
            var undoActions = new List<UndoableAction>();
            int processedCount = 0;

            await Task.Run(async () =>
            {
                foreach (var item in itemList)
                {
                    if (ct.IsCancellationRequested || (task != null && task.Status == TaskStatus.Canceling)) break;
                    if (task != null) { task.WaitIfPaused(); task.CurrentFile = item.Name; }

                    try
                    {
                        if (permanent)
                        {
                            if (item.IsDirectory) Directory.Delete(item.Path, true);
                            else File.Delete(item.Path);
                        }
                        else
                        {
                            if (_backupService != null)
                            {
                                var record = await _backupService.CreateBackupAsync(item.Path);
                                if (record != null && _undoService != null)
                                {
                                    undoActions.Add(new BackupRestoreUndoAction(_backupService, record));
                                }
                            }
                            else
                            {
                                // Fallback
                                var action = new DeleteUndoAction(item.Path, item.IsDirectory);
                                if (action.Execute()) undoActions.Add(action);
                                else throw new Exception("无法移动文件到备份目录");
                            }
                        }
                        processedCount++;
                    }
                    catch (Exception ex) { failedItems.Add($"{item.Name}: {ex.Message}"); }

                    if (task != null) task.Progress = (int)((double)processedCount / task.TotalItems * 100);
                    ProgressChanged?.Invoke(processedCount, itemList.Count, item.Name);
                }
            }, ct);

            if (undoActions.Count > 0 && _undoService != null)
            {
                var composite = new CompositeUndoAction("删除文件");
                foreach (var a in undoActions) composite.AddAction(a);
                _undoService.RecordAction(composite);
            }

            _getContext()?.Refresh();
            var result = new FileOperationResult { Success = failedItems.Count == 0, ProcessedCount = processedCount, FailedCount = failedItems.Count, FailedItems = failedItems };
            if (task != null)
            {
                task.Status = TaskStatus.Completed;
                if (task.IsSilent)
                {
                    YiboFile.Services.Core.NotificationService.ShowSuccess("文件删除成功");
                }
            }
            OperationCompleted?.Invoke(result);

            if (failedItems.Count > 0) _errorService?.ReportError($"删除失败:\n{string.Join("\n", failedItems.Take(5))}", ErrorSeverity.Error);
            return result;
        }

        #endregion

        #region Rename

        public async Task<FileOperationResult> RenameAsync(FileSystemItem item, string newName)
        {
            if (item == null || string.IsNullOrWhiteSpace(newName)) return FileOperationResult.Failed("参数无效");

            var directory = Path.GetDirectoryName(item.Path);
            var newPath = Path.Combine(directory, newName);
            bool isCaseChangeOnly = string.Equals(item.Path, newPath, StringComparison.OrdinalIgnoreCase);

            if (!isCaseChangeOnly && (File.Exists(newPath) || Directory.Exists(newPath)))
            {
                _errorService?.ReportError($"已存在同名文件: {newName}", ErrorSeverity.Warning);
                return FileOperationResult.Failed("已存在同名文件");
            }

            try
            {
                await Task.Run(() =>
                {
                    if (item.IsDirectory) Directory.Move(item.Path, newPath);
                    else File.Move(item.Path, newPath);
                });
                _undoService?.RecordAction(new RenameUndoAction(item.Path, newPath, item.IsDirectory));
                _getContext()?.Refresh();
                return FileOperationResult.Succeeded(1);
            }
            catch (Exception ex)
            {
                _errorService?.ReportError($"重命名失败: {ex.Message}", ErrorSeverity.Error, ex);
                return FileOperationResult.Failed(ex.Message, ex);
            }
        }

        #endregion

        #region Helpers

        private void SafeMoveFile(string src, string dest)
        {
            try { File.Move(src, dest); }
            catch (IOException) { File.Copy(src, dest); File.Delete(src); }
        }

        private void SafeMoveDirectory(string src, string dest, FileOperationTask task = null)
        {
            try
            {
                if (task != null) task.WaitIfPaused();
                Directory.Move(src, dest);
            }
            catch (IOException) { CopyDirectory(src, dest, task); Directory.Delete(src, true); }
        }

        private void CopyDirectory(string src, string dest, FileOperationTask task = null)
        {
            Directory.CreateDirectory(dest);
            if (task != null) task.WaitIfPaused();
            var ct = task?.CancellationTokenSource.Token ?? CancellationToken.None;

            foreach (var file in Directory.GetFiles(src))
            {
                ct.ThrowIfCancellationRequested();
                if (task != null) task.WaitIfPaused();
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            }
            foreach (var dir in Directory.GetDirectories(src))
            {
                ct.ThrowIfCancellationRequested();
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)), task);
            }
        }

        private async Task<(ConflictResolution, bool)> ShowConflictDialogAsync(string fileName, bool isMultiple, CancellationToken ct)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new ConflictResolutionDialog { Owner = Application.Current.MainWindow };
                dialog.SetFileName(fileName);
                dialog.SetMultipleMode(isMultiple);
                using (ct.Register(() => { try { dialog.Dispatcher.Invoke(dialog.Close); } catch { } }))
                {
                    if (dialog.ShowDialog() == true) return (dialog.Resolution, dialog.ApplyToAll);
                }
                return (ConflictResolution.CancelAll, false);
            });
        }

        private bool ShowConfirmDialog(string message, string title)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
                return Application.Current.Dispatcher.Invoke(() => ShowConfirmDialog(message, title));
            return ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Question, Application.Current.MainWindow);
        }

        #endregion

        public async Task<string> CreateFolderAsync(string parentPath, string name)
        {
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(name)) return null;
            try
            {
                string finalPath = FileSystemCoreUtils.GetUniquePath(Path.Combine(parentPath, name));
                await Task.Run(() => Directory.CreateDirectory(finalPath));
                if (_undoService != null && _backupService != null)
                {
                    _undoService.RecordAction(new BackupRestoreUndoAction(_backupService, finalPath));
                }
                return finalPath;
            }
            catch (Exception ex)
            {
                _errorService?.ReportError($"创建文件夹失败: {ex.Message}", Core.Error.ErrorSeverity.Error);
                return null;
            }
        }

        public void NotifyFileCreated(string filePath, bool isDirectory = false)
        {
            if (_undoService != null && _backupService != null)
            {
                _undoService.RecordAction(new BackupRestoreUndoAction(_backupService, filePath));
            }
        }
    }
}
