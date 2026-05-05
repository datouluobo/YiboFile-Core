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
using YiboFile.Services.FileOperations.RecycleBin;
using YiboFile.Services.FileSystem;
using YiboFile.Interop;

// 使用 Services.UI 命名空间的 ConflictResolution
using ConflictResolution = YiboFile.Services.UI.ConflictResolution;
using YiboFile.Services.FileOperations.TaskQueue;
using TaskStatus = YiboFile.Services.FileOperations.TaskQueue.TaskStatus;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.UI;

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
        private readonly YiboFile.Services.FileOperations.RecycleBin.IRecycleBinService _recycleBinService;
        private readonly IMessageBus _messageBus;
        private readonly IDialogService _dialogService;
        private Func<FileOperationContext> _getContext;

        /// <summary>
        /// 设置上下文提供者。用于在单例服务中延迟绑定主窗口的上下文。
        /// </summary>
        public void SetContextProvider(Func<FileOperationContext> provider)
        {
            _getContext = provider;
        }



        public FileOperationService(
            Func<FileOperationContext> contextProvider = null,
            ErrorService errorService = null,
            UndoService undoService = null,
            TaskQueueService taskQueueService = null,
            IRecycleBinService recycleBinService = null,
            IMessageBus messageBus = null)
        {
            _getContext = contextProvider;
            _clipboard = ClipboardService.Instance;
            _errorService = errorService;
            _undoService = undoService;
            _taskQueueService = taskQueueService;
            _recycleBinService = recycleBinService;
            _messageBus = messageBus ?? App.ServiceProvider?.GetService(typeof(IMessageBus)) as IMessageBus;
            _dialogService = App.ServiceProvider?.GetService<IDialogService>();
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
        public async Task<FileOperationResult> PasteAsync(string overrideTargetPath = null, CancellationToken ct = default)
        {
            var context = _getContext();
            string targetPath = overrideTargetPath;

            // If no explicit path, try to get from context
            if (string.IsNullOrEmpty(targetPath))
            {
                if (context == null || !context.CanPerformOperation())
                {
                    return FileOperationResult.Failed("目标路径无效");
                }
                targetPath = context.GetEffectiveTargetPath();
            }

            if (string.IsNullOrEmpty(targetPath)) return FileOperationResult.Failed("目标路径无效");

            var (sourcePaths, isCut) = await _clipboard.GetPathsFromClipboardAsync();
            if (sourcePaths.Count == 0)
            {
                return FileOperationResult.Failed("剪贴板为空");
            }
            _messageBus?.Publish(new FileOperationStatusMessage(isCut ? "正在移动文件..." : "正在复制文件...", true));

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
                IsSilent = true, // 默认先静默，超过阈值自动暴露
                StartTime = DateTime.Now
            };
            _taskQueueService?.EnqueueTask(task);

            // 预扫总字节数，用于精确进度
            long totalBytes = 0;
            var itemsWithSize = new List<(string Path, long Size, bool Dir)>();
            foreach (var sp in sourcePaths)
            {
                if (File.Exists(sp) || Directory.Exists(sp))
                {
                    bool d = Directory.Exists(sp);
                    long s = d ? NativeFileOperations.CalculateTotalSize(sp) : new System.IO.FileInfo(sp).Length;
                    itemsWithSize.Add((sp, s, d));
                    totalBytes += s;
                }
            }
            if (totalBytes > 0) task.TotalBytes = totalBytes;

            long accumulatedBytes = 0;

            foreach (var (sourcePath, fileBytes, _) in itemsWithSize)
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
                    
                    if (task.IsSilent && (DateTime.Now - task.StartTime).TotalMilliseconds > 300)
                    {
                        task.IsSilent = false;
                    }
                }

                if (string.IsNullOrEmpty(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
                {
                    processedCount++;
                    continue;
                }

                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(targetPath, fileName);
                bool isDir = Directory.Exists(sourcePath);

                _messageBus?.Publish(new FileOperationProgressMessage(processedCount, totalCount, fileName));

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
                    var capturedAccumulated = accumulatedBytes;
                    var capturedFileBytes = fileBytes;
                    await Task.Run(async () =>
                    {
                        FileProgressCallback onProgress = (current, total, name) =>
                        {
                            if (task == null || totalBytes <= 0) return;
                            var p = (double)(capturedAccumulated + current) / totalBytes * 100;
                            task.Progress = Math.Min(p, 99.9);
                            if (!string.IsNullOrEmpty(name)) task.CurrentFile = name;
                        };

                        if (isCut)
                        {
                            bool sameVolume = FileSystemCoreUtils.IsSameVolume(sourcePath, destPath);
                            if (sameVolume)
                            {
                                // 同卷移动（高效重命名）
                                if (isDir) Directory.Move(sourcePath, destPath);
                                else File.Move(sourcePath, destPath, true);
                                onProgress(capturedFileBytes, capturedFileBytes, fileName);
                            }
                            else
                            {
                                // 跨卷移动：使用 Win32 API，带进度
                                if (isDir)
                                    await NativeFileOperations.MoveDirectoryAsync(sourcePath, destPath, onProgress);
                                else
                                    NativeFileOperations.MoveFile(sourcePath, destPath, onProgress);
                            }
                        }
                        else
                        {
                            // 复制
                            if (isDir)
                                await NativeFileOperations.CopyDirectoryAsync(sourcePath, destPath, onProgress);
                            else
                                NativeFileOperations.CopyFile(sourcePath, destPath, onProgress);
                        }
                    }, task?.CancellationTokenSource.Token ?? ct);

                    // 更新累积字节
                    accumulatedBytes += fileBytes;
                    if (task != null && totalBytes > 0 && accumulatedBytes >= totalBytes)
                        task.Progress = 100;

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
                            // 新建/复制撤销：回收站删除
                            if (_recycleBinService != null)
                            {
                                undoActionList.Add(new RecycleBinDeleteUndoAction(_recycleBinService, destPath, false));
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
            }

            // FileOperationCompleteMessage 由 FileOperationModule 统一发布，此处仅发布进度完成信号
            _messageBus?.Publish(new FileOperationProgressMessage(totalCount, totalCount, "完成"));

            if (failedItems.Count > 0)
            {
                string errorSummary = $"操作完成，但有 {failedItems.Count} 个项失败。";
                if (failedItems.Count > 5) errorSummary += "\n详情请查看日志或任务清单。";
                _errorService?.ReportError($"{errorSummary}\n以下项目操作失败:\n{string.Join("\n", failedItems.Take(5))}", ErrorSeverity.Error);
            }

            return result;
        }

        #endregion

        #region Delete

        public async Task<FileOperationResult> DeleteAsync(IEnumerable<FileSystemItem> items, bool permanent = false, CancellationToken ct = default)
        {
            var itemList = items?.ToList();
            if (itemList != null)
            {
                foreach (var item in itemList)
                {
                }
            }
            if (itemList == null || itemList.Count == 0) return FileOperationResult.Failed("没有选中任何项目");

            var message = itemList.Count == 1 ? $"确定要删除 \"{itemList[0].Name}\" 吗？" : $"确定要删除这 {itemList.Count} 个项目吗？";
            if (!ShowConfirmDialog(message, "确认删除")) return FileOperationResult.Cancelled();

            _messageBus?.Publish(new FileOperationStatusMessage("正在删除文件...", true));
            var task = new FileOperationTask
            {
                Description = "删除文件",
                TotalItems = itemList.Count,
                Status = TaskStatus.Running,
                IsSilent = true, // 动态评估
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
                    if (task != null) 
                    { 
                        task.WaitIfPaused(); 
                        task.CurrentFile = item.Name; 
                        
                        if (task.IsSilent && (DateTime.Now - task.StartTime).TotalMilliseconds > 300)
                        {
                            task.IsSilent = false;
                        }
                    }

                    try
                    {
                        if (permanent)
                        {
                            if (item.IsDirectory) Directory.Delete(item.Path, true);
                            else File.Delete(item.Path);
                        }
                        else
                        {
                            if (_recycleBinService != null)
                            {
                                // 回收站删除
                                if (_recycleBinService.Send(item.Path))
                                {
                                    undoActions.Add(new RecycleBinDeleteUndoAction(_recycleBinService, item.Path));
                                }
                                else
                                {
                                    // 回收站失败时降级：直接移到 UndoBackup 目录
                                    var action = new DeleteUndoAction(item.Path, item.IsDirectory);
                                    if (action.Execute()) undoActions.Add(action);
                                    else throw new Exception("无法移动文件到备份目录");
                                }
                            }
                            else
                            {
                                // 无回收站服务时的降级
                                var action = new DeleteUndoAction(item.Path, item.IsDirectory);
                                if (action.Execute()) undoActions.Add(action);
                                else throw new Exception("无法移动文件到备份目录");
                            }
                        }
                        processedCount++;
                    }
                    catch (Exception ex) { 
                        failedItems.Add($"{item.Name}: {ex.Message}"); 
                    }

                    if (task != null) task.Progress = (int)((double)processedCount / task.TotalItems * 100);
                    _messageBus?.Publish(new FileOperationProgressMessage(processedCount, itemList.Count, item.Name));
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
            }
            // FileOperationCompleteMessage 由 FileOperationModule 统一发布

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

        private async Task<(ConflictResolution, bool)> ShowConflictDialogAsync(string fileName, bool isMultiple, CancellationToken ct)
        {
            if (_dialogService == null) return (ConflictResolution.CancelAll, false);
            return await _dialogService.ShowConflictDialogAsync(fileName, isMultiple);
        }

        private bool ShowConfirmDialog(string message, string title)
        {
            if (_dialogService == null) return false;
            return _dialogService.Confirm(message, title);
        }

        #endregion

        public async Task<string> CreateFolderAsync(string parentPath, string name = null)
        {
            var locService = App.ServiceProvider?.GetService<YiboFile.Services.Localization.ILocalizationService>();
            if (string.IsNullOrEmpty(parentPath)) return null;
            try
            {
                string folderName = string.IsNullOrEmpty(name) ? (locService?["FileOp.NewFolder"] ?? "新建文件夹") : name;
                string finalPath = FileSystemCoreUtils.GetUniquePath(Path.Combine(parentPath, folderName));
                await Task.Run(() => Directory.CreateDirectory(finalPath));
                if (_undoService != null)
                {
                    if (_recycleBinService != null)
                    {
                        _undoService.RecordAction(new RecycleBinDeleteUndoAction(_recycleBinService, finalPath, false));
                    }
                    else
                    {
                        _undoService.RecordAction(new NewFileUndoAction(finalPath, true));
                    }
                }
                return finalPath;
            }
            catch (Exception ex)
            {
                string errMsg = string.Format(locService?["FileOp.CreateFailedFormat"] ?? "创建文件夹失败: {0}", ex.Message);
                _errorService?.ReportError(errMsg, Core.Error.ErrorSeverity.Error);
                return null;
            }
        }

        public async Task<string> CreateFileAsync(string parentPath, string name = null, string extension = ".txt")
        {
            var locService = App.ServiceProvider?.GetService<YiboFile.Services.Localization.ILocalizationService>();
            if (string.IsNullOrEmpty(parentPath)) return null;
            try
            {
                string defaultName = GetDefaultFileName(extension);
                string fileName = string.IsNullOrEmpty(name) ? defaultName : name;
                if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    fileName += extension;
                }

                string finalPath = FileSystemCoreUtils.GetUniquePath(Path.Combine(parentPath, fileName));
                await Task.Run(() =>
                {
                    var templateService = App.ServiceProvider?.GetService<YiboFile.Services.FileSystem.FileOperations.IFileTemplateService>();
                    if (templateService != null)
                    {
                        templateService.CreateFileWithProperFormat(finalPath, extension);
                    }
                    else
                    {
                        File.WriteAllBytes(finalPath, Array.Empty<byte>());
                    }
                });

                if (_undoService != null)
                {
                    if (_recycleBinService != null)
                    {
                        _undoService.RecordAction(new RecycleBinDeleteUndoAction(_recycleBinService, finalPath, false));
                    }
                    else
                    {
                        _undoService.RecordAction(new NewFileUndoAction(finalPath, false));
                    }
                }
                return finalPath;
            }
            catch (Exception ex)
            {
                _errorService?.ReportError($"创建文件失败: {ex.Message}", Core.Error.ErrorSeverity.Error);
                return null;
            }
        }

        public void NotifyFileCreated(string filePath, bool isDirectory = false)
        {
            if (_undoService != null)
            {
                if (_recycleBinService != null)
                {
                    _undoService.RecordAction(new RecycleBinDeleteUndoAction(_recycleBinService, filePath, false));
                }
                else
                {
                    _undoService.RecordAction(new NewFileUndoAction(filePath, isDirectory));
                }
            }
        }

        private static string GetDefaultFileName(string extension)
        {
            return extension?.ToLowerInvariant() switch
            {
                ".txt" => "新建文本文档",
                ".md" => "新建 Markdown 文件",
                ".html" or ".htm" => "新建网页",
                ".js" => "新建 JavaScript 文件",
                ".ts" => "新建 TypeScript 文件",
                ".py" => "新建 Python 文件",
                ".json" => "新建 JSON 文件",
                ".xml" => "新建 XML 文件",
                ".css" => "新建样式表",
                ".java" => "新建 Java 文件",
                ".bat" => "新建批处理文件",
                ".ps1" => "新建 PowerShell 脚本",
                ".ini" or ".cfg" or ".conf" => "新建配置文件",
                ".png" => "新建图片",
                ".jpg" or ".jpeg" => "新建图片",
                ".svg" => "新建 SVG 图片",
                ".docx" => "新建 Word 文档",
                ".xlsx" => "新建 Excel 工作簿",
                ".pptx" => "新建 PowerPoint 演示文稿",
                ".cs" => "新建 C# 文件",
                ".cpp" or ".c" => "新建 C++ 文件",
                ".h" => "新建头文件",
                ".sql" => "新建 SQL 文件",
                ".sh" => "新建 Shell 脚本",
                ".yaml" or ".yml" => "新建 YAML 文件",
                ".log" => "新建日志文件",
                ".csv" => "新建 CSV 文件",
                _ => "新建文件"
            };
        }
    }
}
