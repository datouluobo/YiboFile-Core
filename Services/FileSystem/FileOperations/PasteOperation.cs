using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OoiMRR.Services.FileOperations
{
    /// <summary>
    /// 粘贴操作
    /// </summary>
    public class PasteOperation
    {
        private readonly IFileOperationContext _context;

        public PasteOperation(IFileOperationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 执行粘贴操作
        /// </summary>
        /// <param name="sourcePaths">源路径列表（程序内部剪贴板）</param>
        /// <param name="isCut">是否为剪切操作</param>
        public void Execute(List<string> sourcePaths, bool isCut)
        {
            // 如果内部剪贴板为空，尝试从Windows系统剪贴板获取
            if (sourcePaths == null || sourcePaths.Count == 0)
            {
                sourcePaths = GetPathsFromWindowsClipboard(out isCut);

                if (sourcePaths == null || sourcePaths.Count == 0)
                {
                    return; // 两个剪贴板都为空，直接返回
                }
            }

            if (!_context.CanPerformOperation("Paste"))
            {
                _context.ShowMessage("无法执行粘贴操作", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            string targetPath = _context.GetTargetPath();
            if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            {
                _context.ShowMessage("目标路径无效", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            try
            {
                foreach (var sourcePath in sourcePaths)
                {
                    if (string.IsNullOrEmpty(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
                    {
                        continue;
                    }

                    var fileName = Path.GetFileName(sourcePath);
                    var destPath = Path.Combine(targetPath, fileName);

                    // 如果目标已存在，添加序号
                    if (File.Exists(destPath) || Directory.Exists(destPath))
                    {
                        var extension = Path.GetExtension(fileName);
                        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                        int counter = 1;

                        do
                        {
                            fileName = $"{nameWithoutExt} ({counter}){extension}";
                            destPath = Path.Combine(targetPath, fileName);
                            counter++;
                        }
                        while (File.Exists(destPath) || Directory.Exists(destPath));
                    }

                    if (File.Exists(sourcePath))
                    {
                        if (isCut)
                        {
                            SafeMoveFile(sourcePath, destPath);
                        }
                        else
                        {
                            File.Copy(sourcePath, destPath);
                        }
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        if (isCut)
                        {
                            SafeMoveDirectory(sourcePath, destPath);
                        }
                        else
                        {
                            CopyDirectory(sourcePath, destPath);
                        }
                    }
                }

                _context.RefreshAfterOperation();

                // 如果是剪切操作，清除剪贴板
                if (isCut)
                {
                    try
                    {
                        if (System.Windows.Application.Current?.Dispatcher?.CheckAccess() == false)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() => System.Windows.Clipboard.Clear());
                        }
                        else
                        {
                            System.Windows.Clipboard.Clear();
                        }
                    }
                    catch
                    {
                        // 清除剪贴板失败不影响粘贴操作
                    }
                }
            }
            catch (Exception ex)
            {
                _context.ShowMessage($"粘贴失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从Windows系统剪贴板获取文件路径列表并判断是否为剪切操作
        /// 必须在UI线程上调用
        /// </summary>
        private List<string> GetPathsFromWindowsClipboard(out bool isCut)
        {
            isCut = false;
            isCut = false;

            try
            {
                // 确保在UI线程上访问剪贴板
                if (System.Windows.Application.Current != null &&
                    !System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    bool cutFlag = false;
                    var result = System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        return GetPathsFromWindowsClipboard(out cutFlag);
                    });
                    isCut = cutFlag;
                    return result;
                }

                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var fileDropList = System.Windows.Clipboard.GetFileDropList();
                    var paths = new List<string>();
                    foreach (string path in fileDropList)
                    {
                        paths.Add(path);
                    }

                    // 检测是否为剪切操作（通过Preferred DropEffect）
                    try
                    {
                        if (System.Windows.Clipboard.ContainsData("Preferred DropEffect"))
                        {
                            var data = System.Windows.Clipboard.GetData("Preferred DropEffect");
                            if (data is System.IO.MemoryStream ms)
                            {
                                var bytes = ms.ToArray();
                                if (bytes.Length >= 4)
                                {
                                    int effect = BitConverter.ToInt32(bytes, 0);
                                    // DROPEFFECT_MOVE = 2
                                    isCut = (effect == 2);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // 无法检测剪切标记，默认为复制操作
                        isCut = false;
                    }

                    return paths;
                }
            }
            catch
            {
                // 访问剪贴板失败，返回空列表
            }

            return new List<string>();
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destDir, fileName), true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(destDir, dirName));
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
                // 跨卷移动可能会失败，回退到复制删除
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
                // 跨卷移动可能会失败，回退到复制删除
                CopyDirectory(sourcePath, destPath);
                Directory.Delete(sourcePath, true);
            }
        }
    }
}


























