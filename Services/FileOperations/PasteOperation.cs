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
        /// <param name="sourcePaths">源路径列表</param>
        /// <param name="isCut">是否为剪切操作</param>
        public void Execute(List<string> sourcePaths, bool isCut)
        {
            if (sourcePaths == null || sourcePaths.Count == 0)
            {
                return;
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
                            File.Move(sourcePath, destPath);
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
                            Directory.Move(sourcePath, destPath);
                        }
                        else
                        {
                            CopyDirectory(sourcePath, destPath);
                        }
                    }
                }

                _context.RefreshAfterOperation();
            }
            catch (Exception ex)
            {
                _context.ShowMessage($"粘贴失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
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
    }
}



