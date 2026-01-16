using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 文件操作基类 - 提供通用功能
    /// </summary>
    public abstract class FileOperationBase : IFileOperation
    {
        protected readonly FileOperationContext Context;
        protected readonly List<string> FailedItems = new List<string>();
        protected int ProcessedCount;
        protected int TotalCount;

        public event Action<int, int, string> ProgressChanged;

        public virtual bool CanUndo => false;
        public abstract string Description { get; }

        protected FileOperationBase(FileOperationContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public abstract Task<FileOperationResult> ExecuteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 报告进度
        /// </summary>
        protected void ReportProgress(string currentItem)
        {
            ProgressChanged?.Invoke(ProcessedCount, TotalCount, currentItem);
        }

        /// <summary>
        /// 安全移动文件（跨卷回退到复制+删除）
        /// </summary>
        protected void SafeMoveFile(string source, string dest)
        {
            try
            {
                File.Move(source, dest);
            }
            catch (IOException)
            {
                File.Copy(source, dest);
                File.Delete(source);
            }
        }

        /// <summary>
        /// 安全移动目录
        /// </summary>
        protected void SafeMoveDirectory(string source, string dest)
        {
            try
            {
                Directory.Move(source, dest);
            }
            catch (IOException)
            {
                CopyDirectoryRecursive(source, dest, CancellationToken.None);
                Directory.Delete(source, true);
            }
        }

        /// <summary>
        /// 递归复制目录
        /// </summary>
        protected void CopyDirectoryRecursive(string source, string dest, CancellationToken ct)
        {
            Directory.CreateDirectory(dest);

            foreach (var file in Directory.GetFiles(source))
            {
                ct.ThrowIfCancellationRequested();
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(dest, fileName), true);
            }

            foreach (var dir in Directory.GetDirectories(source))
            {
                ct.ThrowIfCancellationRequested();
                var dirName = Path.GetFileName(dir);
                CopyDirectoryRecursive(dir, Path.Combine(dest, dirName), ct);
            }
        }

        /// <summary>
        /// 获取唯一路径（重命名避免冲突）
        /// </summary>
        protected string GetUniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                return path;

            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
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

        /// <summary>
        /// 构建操作结果
        /// </summary>
        protected FileOperationResult BuildResult()
        {
            if (FailedItems.Count == 0)
            {
                return FileOperationResult.Succeeded(ProcessedCount);
            }

            return new FileOperationResult
            {
                Success = ProcessedCount > 0,
                ProcessedCount = ProcessedCount,
                FailedCount = FailedItems.Count,
                FailedItems = new List<string>(FailedItems),
                Message = $"{FailedItems.Count} 项操作失败"
            };
        }
    }
}

