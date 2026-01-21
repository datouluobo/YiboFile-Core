using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YiboFile.Services.FileOperations.Undo
{
    /// <summary>
    /// 删除操作的撤销支持
    /// 实际上是将文件移动到临时目录，撤销时移回
    /// </summary>
    public class DeleteUndoAction : UndoableAction
    {
        private readonly string _originalPath;
        private readonly string _backupPath;
        private readonly bool _isDirectory;

        public override UndoableActionType ActionType => UndoableActionType.Delete;
        public override string Description => $"删除 {Path.GetFileName(_originalPath)}";

        /// <summary>
        /// 获取撤销备份目录
        /// </summary>
        public static string BackupDirectory
        {
            get
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "YiboFile", "UndoBackup");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        public DeleteUndoAction(string originalPath, bool isDirectory)
        {
            _originalPath = originalPath;
            _isDirectory = isDirectory;
            _backupPath = Path.Combine(BackupDirectory, Guid.NewGuid().ToString());
        }

        /// <summary>
        /// 执行删除（移动到备份目录）
        /// </summary>
        public bool Execute()
        {
            try
            {
                // 确保备份目录存在
                var backupDir = Path.GetDirectoryName(_backupPath);
                if (!Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);

                if (_isDirectory)
                {
                    try
                    {
                        Directory.Move(_originalPath, _backupPath);
                    }
                    catch (IOException)
                    {
                        // 跨磁盘移动失败，使用复制+删除
                        CopyDirectory(_originalPath, _backupPath);
                        Directory.Delete(_originalPath, true);
                    }
                }
                else
                {
                    try
                    {
                        File.Move(_originalPath, _backupPath);
                    }
                    catch (IOException)
                    {
                        // 跨磁盘移动失败，使用复制+删除
                        File.Copy(_originalPath, _backupPath, true);
                        File.Delete(_originalPath);
                    }
                }
                return true;
            }
            catch (Exception)
            {
                // Fallback for any error (e.g. UnauthorizedAccessException)
                try
                {
                    if (_isDirectory) return false; // Directory fallback complex

                    // Attempt to remove ReadOnly attribute
                    if (File.Exists(_originalPath))
                    {
                        var attributes = File.GetAttributes(_originalPath);
                        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(_originalPath, attributes & ~FileAttributes.ReadOnly);
                        }
                    }

                    // Try Copy+Delete again
                    File.Copy(_originalPath, _backupPath, true);
                    File.Delete(_originalPath);
                    return true;
                }
                catch
                {
                    // Last resort failed
                    return false;
                }
            }
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
            }
        }

        public override bool Undo()
        {
            try
            {
                // 确保目标目录存在
                var targetDir = Path.GetDirectoryName(_originalPath);
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                // 恢复到原位置
                if (_isDirectory)
                {
                    try
                    {
                        Directory.Move(_backupPath, _originalPath);
                    }
                    catch (IOException)
                    {
                        // 跨磁盘移动失败，使用复制+删除
                        CopyDirectory(_backupPath, _originalPath);
                        Directory.Delete(_backupPath, true);
                    }
                }
                else
                {
                    try
                    {
                        File.Move(_backupPath, _originalPath);
                    }
                    catch (IOException)
                    {
                        // 跨磁盘移动失败，使用复制+删除
                        File.Copy(_backupPath, _originalPath, true);
                        File.Delete(_backupPath);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Redo()
        {
            return Execute();
        }

        /// <summary>
        /// 清理备份（确认删除后调用）
        /// </summary>
        public void CleanupBackup()
        {
            try
            {
                if (_isDirectory && Directory.Exists(_backupPath))
                    Directory.Delete(_backupPath, true);
                else if (File.Exists(_backupPath))
                    File.Delete(_backupPath);
            }
            catch
            {
                // 忽略清理错误
            }
        }
    }

    /// <summary>
    /// 移动操作的撤销支持
    /// </summary>
    /// <summary>
    /// 移动操作的撤销支持
    /// </summary>
    public class MoveUndoAction : UndoableAction
    {
        private readonly string _sourcePath;
        private readonly string _destinationPath;
        private readonly bool _isDirectory;

        public override UndoableActionType ActionType => UndoableActionType.Move;
        public override string Description => $"移动 {Path.GetFileName(_sourcePath)}";

        public MoveUndoAction(string sourcePath, string destinationPath, bool isDirectory)
        {
            _sourcePath = sourcePath;
            _destinationPath = destinationPath;
            _isDirectory = isDirectory;
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(subDir, Path.Combine(destDir, Path.GetFileName(subDir)));
            }
        }

        private bool SafeMove(string src, string dest)
        {
            try
            {
                if (_isDirectory)
                {
                    if (Directory.Exists(src))
                    {
                        try
                        {
                            Directory.Move(src, dest);
                        }
                        catch (IOException) // Span across volumes or other error
                        {
                            CopyDirectory(src, dest);
                            Directory.Delete(src, true);
                        }
                    }
                }
                else
                {
                    if (File.Exists(src))
                    {
                        if (File.Exists(dest)) File.Delete(dest); // Overwrite protection
                        File.Move(src, dest);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Undo()
        {
            // 移回原位置: Dest -> Source
            return SafeMove(_destinationPath, _sourcePath);
        }

        public override bool Redo()
        {
            // 重做: Source -> Dest
            return SafeMove(_sourcePath, _destinationPath);
        }
    }

    /// <summary>
    /// 重命名操作的撤销支持
    /// </summary>
    public class RenameUndoAction : UndoableAction
    {
        private readonly string _oldPath;
        private readonly string _newPath;
        private readonly bool _isDirectory;

        public override UndoableActionType ActionType => UndoableActionType.Rename;
        public override string Description => $"重命名 {Path.GetFileName(_oldPath)} → {Path.GetFileName(_newPath)}";

        public RenameUndoAction(string oldPath, string newPath, bool isDirectory)
        {
            _oldPath = oldPath;
            _newPath = newPath;
            _isDirectory = isDirectory;
        }

        public override bool Undo()
        {
            try
            {
                if (_isDirectory)
                {
                    Directory.Move(_newPath, _oldPath);
                }
                else
                {
                    File.Move(_newPath, _oldPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public override bool Redo()
        {
            try
            {
                if (_isDirectory)
                {
                    Directory.Move(_oldPath, _newPath);
                }
                else
                {
                    File.Move(_oldPath, _newPath);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
    /// <summary>
    /// 组合撤销操作（支持事务性撤销多个操作）
    /// </summary>
    public class CompositeUndoAction : UndoableAction
    {
        private readonly List<UndoableAction> _actions = new List<UndoableAction>();
        private readonly string _description;

        public override UndoableActionType ActionType => _actions.Count > 0 ? _actions[0].ActionType : UndoableActionType.None;
        public override string Description => _description;

        public CompositeUndoAction(string description)
        {
            _description = description;
        }

        public void AddAction(UndoableAction action)
        {
            _actions.Add(action);
        }

        public override bool Undo()
        {
            bool success = true;
            // 按相反顺序撤销
            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                if (!_actions[i].Undo())
                {
                    success = false;
                }
            }
            return success;
        }

        public override bool Redo()
        {
            bool success = true;
            foreach (var action in _actions)
            {
                if (!action.Redo())
                {
                    success = false;
                }
            }
            return success;
        }
    }

    /// <summary>
    /// 新建文件/复制文件的撤销支持（撤销时删除文件）
    /// </summary>
    /// <summary>
    /// 新建文件/复制文件的撤销支持
    /// 撤销时：将文件移至临时备份（像删除一样），以便Redo时恢复
    /// </summary>
    public class NewFileUndoAction : UndoableAction
    {
        private readonly string _filePath;
        private readonly bool _isDirectory;
        private readonly string _backupPath;

        public override UndoableActionType ActionType => UndoableActionType.NewFile;
        public override string Description => $"新建/复制 {Path.GetFileName(_filePath)}";

        // 复用DeleteUndoAction的备份目录逻辑
        private string BackupDirectory
        {
            get
            {
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "YiboFile", "UndoBackup");
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
                return path;
            }
        }

        public NewFileUndoAction(string filePath, bool isDirectory)
        {
            _filePath = filePath;
            _isDirectory = isDirectory;
            _backupPath = Path.Combine(BackupDirectory, Guid.NewGuid().ToString());
        }

        public override bool Undo()
        {
            try
            {
                // 撤销新建 = 删除。但为了支持Redo，我们将其移至备份
                if (_isDirectory)
                {
                    if (Directory.Exists(_filePath))
                    {
                        // 确保备份目录父级存在
                        var backupDir = Path.GetDirectoryName(_backupPath);
                        if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                        Directory.Move(_filePath, _backupPath);
                    }
                }
                else
                {
                    if (File.Exists(_filePath))
                    {
                        var backupDir = Path.GetDirectoryName(_backupPath);
                        if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                        File.Move(_filePath, _backupPath);
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool Redo()
        {
            try
            {
                // Redo = 恢复新建 = 从备份移回
                if (_isDirectory)
                {
                    if (Directory.Exists(_backupPath))
                    {
                        // 确保目标父目录存在
                        var targetDir = Path.GetDirectoryName(_filePath);
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                        Directory.Move(_backupPath, _filePath);
                    }
                }
                else
                {
                    if (File.Exists(_backupPath))
                    {
                        var targetDir = Path.GetDirectoryName(_filePath);
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                        File.Move(_backupPath, _filePath);
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void CleanupBackup()
        {
            try
            {
                if (_isDirectory && Directory.Exists(_backupPath))
                    Directory.Delete(_backupPath, true);
                else if (File.Exists(_backupPath))
                    File.Delete(_backupPath);
            }
            catch { }
        }
    }
}

