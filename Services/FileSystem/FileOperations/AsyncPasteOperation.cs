using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Dialogs;
using YiboFile.Services.Core.Error;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 异步粘贴操作 - 支持进度显示和冲突处理
    /// </summary>
    public class AsyncPasteOperation
    {
        private readonly IFileOperationContext _context;
        private readonly ErrorService _errorService;

        /// <summary>
        /// 进度更新事件
        /// </summary>
        public event Action<int, int, string> ProgressChanged;

        public AsyncPasteOperation(IFileOperationContext context, ErrorService errorService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _errorService = errorService;
        }

        /// <summary>
        /// 异步执行粘贴操作
        /// </summary>
        public async Task ExecuteAsync(
            List<string> sourcePaths,
            bool isCut,
            CancellationToken cancellationToken = default)
        {
            // 如果内部剪贴板为空，尝试从Windows系统剪贴板获取
            if (sourcePaths == null || sourcePaths.Count == 0)
            {
                sourcePaths = GetPathsFromWindowsClipboard(out isCut);
                if (sourcePaths == null || sourcePaths.Count == 0)
                {
                    return;
                }
            }

            if (!_context.CanPerformOperation("Paste"))
            {
                _errorService?.ReportError("无法执行粘贴操作", ErrorSeverity.Warning);
                return;
            }

            string targetPath = _context.GetTargetPath();
            if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            {
                _errorService?.ReportError("目标路径无效", ErrorSeverity.Warning);
                return;
            }

            var failedItems = new List<string>();
            int totalItems = sourcePaths.Count;
            int processedItems = 0;

            // 冲突处理缓存（用于"应用到全部"）
            ConflictResolution? cachedResolution = null;

            foreach (var sourcePath in sourcePaths)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (string.IsNullOrEmpty(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
                {
                    processedItems++;
                    continue;
                }

                var fileName = Path.GetFileName(sourcePath);
                var destPath = Path.Combine(targetPath, fileName);

                // 报告进度
                ProgressChanged?.Invoke(processedItems, totalItems, fileName);

                try
                {
                    // 检查是否存在冲突
                    bool hasConflict = File.Exists(destPath) || Directory.Exists(destPath);

                    if (hasConflict)
                    {
                        // 关键修复：检测同文件夹粘贴（源和目标在同一目录）
                        // 同文件夹粘贴时，覆盖没有意义，应该自动重命名
                        var sourceDir = Path.GetDirectoryName(sourcePath);
                        bool isSameFolder = string.Equals(sourceDir, targetPath, StringComparison.OrdinalIgnoreCase);

                        if (isSameFolder)
                        {
                            // 同文件夹粘贴：直接自动重命名，不显示对话框
                            destPath = GetUniquePath(destPath);
                        }
                        else
                        {
                            var resolution = cachedResolution;

                            // 如果没有缓存的解决方式，询问用户
                            if (!resolution.HasValue)
                            {
                                var (userResolution, applyToAll) = await ShowConflictDialogAsync(fileName, totalItems > 1, cancellationToken);
                                resolution = userResolution;

                                if (applyToAll)
                                {
                                    cachedResolution = resolution;
                                }
                            }

                            switch (resolution.Value)
                            {
                                case ConflictResolution.CancelAll:
                                    return; // 取消整个操作

                                case ConflictResolution.Skip:
                                    processedItems++;
                                    continue;

                                case ConflictResolution.Rename:
                                    destPath = GetUniquePath(destPath);
                                    break;

                                case ConflictResolution.Overwrite:
                                    // 删除现有目标
                                    if (File.Exists(destPath))
                                        File.Delete(destPath);
                                    else if (Directory.Exists(destPath))
                                        Directory.Delete(destPath, true);
                                    break;
                            }
                        }
                    }

                    // 执行复制/移动
                    await Task.Run(() =>
                    {
                        if (File.Exists(sourcePath))
                        {
                            if (isCut)
                                SafeMoveFile(sourcePath, destPath);
                            else
                                File.Copy(sourcePath, destPath, true);
                        }
                        else if (Directory.Exists(sourcePath))
                        {
                            if (isCut)
                                SafeMoveDirectory(sourcePath, destPath);
                            else
                                CopyDirectory(sourcePath, destPath, cancellationToken);
                        }
                    }, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    failedItems.Add($"{fileName}: {ex.Message}");
                }

                processedItems++;
            }

            // 刷新
            _context.RefreshAfterOperation();

            // 如果是剪切操作，清除剪贴板
            if (isCut && !cancellationToken.IsCancellationRequested)
            {
                ClearClipboard();
            }

            // 报告错误
            if (failedItems.Count > 0)
            {
                _errorService?.ReportError(
                    $"以下项目操作失败:\n{string.Join("\n", failedItems.Take(5))}",
                    ErrorSeverity.Error);
            }

            // 最终进度
            ProgressChanged?.Invoke(totalItems, totalItems, "完成");
        }

        private async Task<(ConflictResolution, bool)> ShowConflictDialogAsync(string fileName, bool isMultiple, CancellationToken ct)
        {
            return await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var ownerWindow = Application.Current.MainWindow;
                var dialog = new ConflictResolutionDialog { Owner = ownerWindow };
                dialog.SetFileName(fileName);
                dialog.SetMultipleMode(isMultiple);

                // 如果任务取消，关闭对话框
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

        private string GetUniquePath(string destPath)
        {
            var directory = Path.GetDirectoryName(destPath);
            var fileName = Path.GetFileNameWithoutExtension(destPath);
            var extension = Path.GetExtension(destPath);
            int counter = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileName} ({counter}){extension}");
                counter++;
            }
            while (File.Exists(newPath) || Directory.Exists(newPath));

            return newPath;
        }

        private void CopyDirectory(string sourceDir, string destDir, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destDir, fileName), true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var dirName = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(destDir, dirName), cancellationToken);
            }
        }

        private void SafeMoveFile(string sourcePath, string destPath)
        {
            try
            {
                File.Move(sourcePath, destPath);
            }
            catch (IOException)
            {
                File.Copy(sourcePath, destPath);
                File.Delete(sourcePath);
            }
        }

        private void SafeMoveDirectory(string sourcePath, string destPath)
        {
            try
            {
                Directory.Move(sourcePath, destPath);
            }
            catch (IOException)
            {
                CopyDirectory(sourcePath, destPath, CancellationToken.None);
                Directory.Delete(sourcePath, true);
            }
        }

        private void ClearClipboard()
        {
            try
            {
                if (Application.Current?.Dispatcher?.CheckAccess() == false)
                {
                    Application.Current.Dispatcher.Invoke(() => Clipboard.Clear());
                }
                else
                {
                    Clipboard.Clear();
                }
            }
            catch
            {
                // 忽略剪贴板清除失败
            }
        }

        private List<string> GetPathsFromWindowsClipboard(out bool isCut)
        {
            isCut = false;
            try
            {
                if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    bool cutFlag = false;
                    var result = Application.Current.Dispatcher.Invoke(() => GetPathsFromWindowsClipboard(out cutFlag));
                    isCut = cutFlag;
                    return result;
                }

                if (Clipboard.ContainsFileDropList())
                {
                    var fileDropList = Clipboard.GetFileDropList();
                    var paths = new List<string>();
                    foreach (string path in fileDropList)
                    {
                        paths.Add(path);
                    }

                    try
                    {
                        if (Clipboard.ContainsData("Preferred DropEffect"))
                        {
                            var data = Clipboard.GetData("Preferred DropEffect");
                            if (data is MemoryStream ms)
                            {
                                var bytes = ms.ToArray();
                                if (bytes.Length >= 4)
                                {
                                    int effect = BitConverter.ToInt32(bytes, 0);
                                    isCut = (effect == 2);
                                }
                            }
                        }
                    }
                    catch
                    {
                        isCut = false;
                    }

                    return paths;
                }
            }
            catch
            {
                // 忽略
            }

            return new List<string>();
        }
    }
}

