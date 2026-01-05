using System;
using System.IO;

namespace OoiMRR.Services.FileOperations.Undo
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
                    "OoiMRR", "UndoBackup");
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
                if (_isDirectory)
                {
                    Directory.Move(_originalPath, _backupPath);
                }
                else
                {
                    // 确保备份目录存在
                    var backupDir = Path.GetDirectoryName(_backupPath);
                    if (!Directory.Exists(backupDir))
                        Directory.CreateDirectory(backupDir);
                    File.Move(_originalPath, _backupPath);
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
            try
            {
                // 恢复到原位置
                if (_isDirectory)
                {
                    Directory.Move(_backupPath, _originalPath);
                }
                else
                {
                    File.Move(_backupPath, _originalPath);
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

        public override bool Undo()
        {
            try
            {
                // 移回原位置
                if (_isDirectory)
                {
                    Directory.Move(_destinationPath, _sourcePath);
                }
                else
                {
                    File.Move(_destinationPath, _sourcePath);
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
                    Directory.Move(_sourcePath, _destinationPath);
                }
                else
                {
                    File.Move(_sourcePath, _destinationPath);
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
}
