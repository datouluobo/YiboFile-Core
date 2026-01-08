using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    public class NewFileUndoAction : UndoableAction
    {
        private readonly string _filePath;
        private readonly bool _isDirectory;

        public override UndoableActionType ActionType => UndoableActionType.NewFile; // Or Copy
        public override string Description => $"新建/复制 {Path.GetFileName(_filePath)}";

        public NewFileUndoAction(string filePath, bool isDirectory)
        {
            _filePath = filePath;
            _isDirectory = isDirectory;
        }

        public override bool Undo()
        {
            try
            {
                if (_isDirectory)
                {
                    if (Directory.Exists(_filePath))
                        Directory.Delete(_filePath, true);
                }
                else
                {
                    if (File.Exists(_filePath))
                        File.Delete(_filePath);
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
            // Redo creation? Not easily supported unless we backed it up.
            // For Copy/NewFile redo, we ideally need to re-copy or re-create.
            // However, typical Windows Undo behavior: Undo Copy -> Delete. Redo Copy -> Restore (Recopy?).
            // If we don't have the source or backup, Redo is impossible.
            // For now, return false or implement simple restore if deleted to Recycle Bin?
            // Let's assume DeleteUndoAction logic (Move to backup) is better?
            // But NewFileUndoAction deletes permanently?
            // To support Redo, Undo must Move to Backup.

            // Let's reuse DeleteUndoAction logic basically!
            // But inverted.
            return false;
        }
    }
}
