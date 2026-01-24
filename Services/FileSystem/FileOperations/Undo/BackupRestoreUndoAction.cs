using System;
using System.Threading.Tasks;
using YiboFile.Services.Backup;

namespace YiboFile.Services.FileOperations.Undo
{
    /// <summary>
    /// 基于备份服务的通用撤销操作
    /// 支持两种模式：
    /// 1. 撤销删除（初始状态在备份库，Undo时还原，Redo时重新备份）
    /// 2. 撤销新建（初始状态在磁盘，Undo时备份，Redo时还原）
    /// </summary>
    public class BackupRestoreUndoAction : UndoableAction
    {
        private readonly IBackupService _backupService;
        private string _filePath;
        private BackupRecord _record;

        /// <summary>
        /// 构造函数用于撤销删除（文件已在备份库中）
        /// </summary>
        public BackupRestoreUndoAction(IBackupService backupService, BackupRecord record)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _record = record ?? throw new ArgumentNullException(nameof(record));
            _filePath = record.OriginalPath;
        }

        /// <summary>
        /// 构造函数用于撤销新建/粘贴（文件目前在磁盘上）
        /// </summary>
        public BackupRestoreUndoAction(IBackupService backupService, string filePath)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _record = null; // 初始状态在磁盘
        }

        public override UndoableActionType ActionType => _record != null ? UndoableActionType.Delete : UndoableActionType.NewFile;

        public override string Description => _record != null
            ? $"恢复文件: {System.IO.Path.GetFileName(_record.OriginalPath)}"
            : $"撤销粘贴/新建: {System.IO.Path.GetFileName(_filePath)}";

        public override bool Undo()
        {
            try
            {
                return ToggleAsync().GetAwaiter().GetResult();
            }
            catch { return false; }
        }

        public override bool Redo()
        {
            try
            {
                return ToggleAsync().GetAwaiter().GetResult();
            }
            catch { return false; }
        }

        private async Task<bool> ToggleAsync()
        {
            if (_record == null)
            {
                // 状态 A: 文件在磁盘上 -> 动作: 备份它 (逻辑上的“删除”)
                _record = await _backupService.CreateBackupAsync(_filePath);
                return _record != null;
            }
            else
            {
                // 状态 B: 文件在备份库中 -> 动作: 还原它
                _filePath = await _backupService.RestoreAsync(_record);
                _record = null;
                return true;
            }
        }
    }
}
