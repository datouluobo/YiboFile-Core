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
            TaskQueueService taskQueueService = null)
        {
            _getContext = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            _clipboard = ClipboardService.Instance;
            _errorService = errorService;
            _undoService = undoService;
            _taskQueueService = taskQueueService;
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
            {                return FileOperationResult.Failed("目标路径无效");
            }            var (sourcePaths, isCut) = await _clipboard.GetPathsFromClipboardAsync();
            if (sourcePaths.Count == 0)
            {                return FileOperationResult.Failed("剪贴板为空");
            }

            var targetPath = context.GetEffectiveTargetPath();            OperationStarted?.Invoke(isCut ? "正在移动文件..." : "正在复制文件...");

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
                CurrentFile = "准备中..."
            };
            _taskQueueService?.EnqueueTask(task);

            foreach (var sourcePath in sourcePaths)
            {
                // 更新任务进度
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

                // 防止递归复制/移动 (源文件夹不能包含目标文件夹)
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
                    // 防止自我复制/移动
                    var srcFullPath = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var destFullPath = Path.GetFullPath(destPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (string.Equals(srcFullPath, destFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!isCut)
                        {
                            destPath = GetUniquePath(destPath);
                        }
                        else
                        {
                            processedCount++;
                            continue;
                        }
                    }

                    // 检查冲突
                    bool hasConflict = File.Exists(destPath) || Directory.Exists(destPath);

                    if (hasConflict)
                    {
                        var sourceDir = Path.GetDirectoryName(sourcePath);
                        bool isSameFolder = string.Equals(sourceDir, targetPath, StringComparison.OrdinalIgnoreCase);

                        if (isSameFolder)
                        {
                            destPath = GetUniquePath(destPath);
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
                                    destPath = GetUniquePath(destPath);
                                    break;
                                case ConflictResolution.Overwrite:
                                    if (File.Exists(destPath)) File.Delete(destPath);
                                    else if (Directory.Exists(destPath)) Directory.Delete(destPath, true);
                                    break;
                            }
                        }
                    }

                    // 执行复制/移动
                    await Task.Run(() =>
                    {
                        if (File.Exists(sourcePath))
                        {
                            if (isCut) SafeMoveFile(sourcePath, destPath);
                            else File.Copy(sourcePath, destPath, true);
                        }
                        else if (Directory.Exists(sourcePath))
                        {
                            if (isCut) SafeMoveDirectory(sourcePath, destPath, task);
                            else CopyDirectory(sourcePath, destPath, task);
                        }
                    }, task?.CancellationTokenSource.Token ?? ct);

                    // 记录撤销操作 (仅当成功时)
                    // 注意：如果是移动操作，源路径可能已经不存在，但在UndoAction中需要记录源路径以便恢复
                    if (isCut)
                    {
                        undoActionList.Add(new MoveUndoAction(sourcePath, destPath, isDir));
                    }
                    else
                    {
                        undoActionList.Add(new NewFileUndoAction(destPath, isDir));
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

            // 注册批量撤销
            if (undoActionList.Count > 0 && _undoService != null)
            {
                var compositeAction = new CompositeUndoAction(isCut ? "移动文件" : "复制文件");
                foreach (var action in undoActionList)
                {
                    compositeAction.AddAction(action);
                }
                _undoService.RecordAction(compositeAction);
            }

            // 刷新
            context.Refresh();

            if (isCut && !ct.IsCancellationRequested)
            {
                await _clipboard.ClearCutStateAsync();
            }

            var result = new FileOperationResult
            {
                Success = failedItems.Count == 0,
                ProcessedCount = processedCount,
                FailedCount = failedItems.Count,
                FailedItems = failedItems
            };

            if (task != null && task.Status != TaskStatus.Canceled && task.Status != TaskStatus.Failed)
            {
                task.Status = TaskStatus.Completed;
                task.Progress = 100;

                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    _taskQueueService?.ClearCompleted();
                });
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

        /// <summary>
        /// 删除文件
        /// </summary>
        /// <param name="items">要删除的项目</param>
        /// <param name="permanent">是否永久删除（不移至回收站/备份）</param>
        public async Task<FileOperationResult> DeleteAsync(IEnumerable<FileSystemItem> items, bool permanent = false, CancellationToken ct = default)
        {
            var itemList = items?.ToList();
            if (itemList == null || itemList.Count == 0)
                return FileOperationResult.Failed("没有选中任何项目");

            var context = _getContext();

            // 确认对话框
            var message = itemList.Count == 1
                ? $"确定要删除 \"{itemList[0].Name}\" 吗？"
                : $"确定要删除这 {itemList.Count} 个项目吗？";

            if (!ShowConfirmDialog(message, "确认删除"))
            {
                return FileOperationResult.Cancelled();
            }

            OperationStarted?.Invoke("正在删除文件...");

            var task = new FileOperationTask
            {
                Description = "删除文件",
                TotalItems = itemList.Count,
                Status = TaskStatus.Running
            };
            _taskQueueService?.EnqueueTask(task);

            var failedItems = new List<string>();
            int processedCount = 0;

            await Task.Run(() =>
            {
                foreach (var item in itemList)
                {
                    if (ct.IsCancellationRequested || (task != null && task.Status == TaskStatus.Canceling))
                    {
                        if (task != null) task.Status = TaskStatus.Canceled;
                        break;
                    }

                    if (task != null)
                    {
                        task.WaitIfPaused();
                        task.CurrentFile = item.Name;
                    }

                    try
                    {
                        if (permanent)
                        {
                            // 永久删除
                            if (item.IsDirectory) Directory.Delete(item.Path, true);
                            else File.Delete(item.Path);
                        }
                        else
                        {
                            // 移至备份目录（可撤销）
                            // 使用 UndoService 管理备份和撤销
                            if (_undoService != null)
                            {
                                var action = new DeleteUndoAction(item.Path, item.IsDirectory);
                                if (action.Execute())
                                {
                                    _undoService.RecordAction(action);
                                }
                                else
                                {
                                    throw new Exception("无法移动文件到备份目录");
                                }
                            }
                            else
                            {
                                // Fallback if no UndoService
                                var backupPath = GetBackupPath(item.Path);
                                if (item.IsDirectory) SafeMoveDirectory(item.Path, backupPath);
                                else SafeMoveFile(item.Path, backupPath);
                            }
                        }
                        processedCount++;
                    }
                    catch (Exception ex)
                    {
                        failedItems.Add($"{item.Name}: {ex.Message}");
                    }

                    // 更新进度
                    if (task != null)
                    {
                        task.Progress = (int)((double)processedCount / task.TotalItems * 100);
                    }
                    ProgressChanged?.Invoke(processedCount, task?.TotalItems ?? itemList.Count, item.Name);
                }
            }, ct);

            context?.Refresh();

            var result = new FileOperationResult
            {
                Success = failedItems.Count == 0,
                ProcessedCount = processedCount,
                FailedCount = failedItems.Count,
                FailedItems = failedItems
            };

            if (task != null && task.Status != TaskStatus.Canceled && task.Status != TaskStatus.Failed)
            {
                task.Status = TaskStatus.Completed;
                task.Progress = 100;
            }

            OperationCompleted?.Invoke(result);

            if (failedItems.Count > 0)
            {
                _errorService?.ReportError($"删除失败:\n{string.Join("\n", failedItems.Take(5))}", ErrorSeverity.Error);
            }

            return result;
        }

        #endregion

        #region Rename

        /// <summary>
        /// 重命名文件
        /// </summary>
        public async Task<FileOperationResult> RenameAsync(FileSystemItem item, string newName)
        {
            if (item == null || string.IsNullOrWhiteSpace(newName))
            {                return FileOperationResult.Failed("参数无效");
            }

            var context = _getContext();
            var directory = Path.GetDirectoryName(item.Path);
            var newPath = Path.Combine(directory, newName);
            // 检查是否仅并在且只是大小写不同
            bool isCaseChangeOnly = string.Equals(item.Path, newPath, StringComparison.OrdinalIgnoreCase);

            if (!isCaseChangeOnly && (File.Exists(newPath) || Directory.Exists(newPath)))
            {                _errorService?.ReportError($"已存在同名文件: {newName}", ErrorSeverity.Warning);
                return FileOperationResult.Failed("已存在同名文件");
            }

            try
            {                await Task.Run(() =>
                {
                    if (item.IsDirectory) Directory.Move(item.Path, newPath);
                    else File.Move(item.Path, newPath);
                });
                // 记录撤销操作
                _undoService?.RecordAction(new RenameUndoAction(item.Path, newPath, item.IsDirectory));

                context?.Refresh();
                return FileOperationResult.Succeeded(1);
            }
            catch (Exception ex)
            {                _errorService?.ReportError($"重命名失败: {ex.Message}", ErrorSeverity.Error, ex);
                return FileOperationResult.Failed(ex.Message, ex);
            }
        }

        #endregion

        #region Helpers

        private string GetUniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;

            var dir = Path.GetDirectoryName(path);
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            int counter = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
                counter++;
            } while (File.Exists(newPath) || Directory.Exists(newPath));

            return newPath;
        }

        private string GetBackupPath(string path)
        {
            var backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "YiboFile", "Backup", DateTime.Now.ToString("yyyyMMdd"));
            Directory.CreateDirectory(backupDir);

            var fileName = Path.GetFileName(path);
            return GetUniquePath(Path.Combine(backupDir, fileName));
        }

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
                var owner = Application.Current.MainWindow;
                var dialog = new ConflictResolutionDialog { Owner = owner };
                dialog.SetFileName(fileName);
                dialog.SetMultipleMode(isMultiple);

                using (ct.Register(() =>
                {
                    try { dialog.Dispatcher.Invoke(dialog.Close); } catch { }
                }))
                {
                    if (dialog.ShowDialog() == true)
                    {
                        return (dialog.Resolution, dialog.ApplyToAll);
                    }
                }

                return (ConflictResolution.CancelAll, false);
            });
        }

        private bool ShowConfirmDialog(string message, string title)
        {
            if (Application.Current?.Dispatcher?.CheckAccess() == false)
            {
                return Application.Current.Dispatcher.Invoke(() => ShowConfirmDialog(message, title));
            }

            return ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Question, Application.Current.MainWindow);
        }

        #endregion
        /// <summary>
        /// 创建新文件夹
        /// </summary>
        public async Task<string> CreateFolderAsync(string parentPath, string name)
        {
            if (string.IsNullOrEmpty(parentPath) || string.IsNullOrEmpty(name)) return null;

            try
            {
                string rawPath = Path.Combine(parentPath, name);
                string finalPath = GetUniquePath(rawPath);

                await Task.Run(() => Directory.CreateDirectory(finalPath));

                // Record Undo
                if (_undoService != null)
                {
                    _undoService.RecordAction(new NewFileUndoAction(finalPath, true));
                }

                return finalPath;
            }
            catch (Exception ex)
            {
                _errorService?.ReportError($"创建文件夹失败: {ex.Message}", Core.Error.ErrorSeverity.Error);
                return null;
            }
        }

        /// <summary>
        /// 通知服务已创建新文件（用于Undo支持）
        /// </summary>
        public void NotifyFileCreated(string filePath, bool isDirectory = false)
        {
            if (_undoService != null)
            {
                _undoService.RecordAction(new NewFileUndoAction(filePath, isDirectory));
            }
        }
    }
}

