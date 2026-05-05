using System;
using System.IO;
using YiboFile.Services.FileOperations.RecycleBin;

namespace YiboFile.Services.FileOperations.Undo
{
    /// <summary>
    /// 基于回收站的删除撤销操作
    /// 支持两种状态间的切换：
    ///   状态 A: 文件在磁盘 → Undo = 发送到回收站，Redo = 从回收站恢复
    ///   状态 B: 文件在回收站 → Undo = 从回收站恢复，Redo = 再次发送到回收站
    /// </summary>
    public class RecycleBinDeleteUndoAction : UndoableAction
    {
        private readonly IRecycleBinService _recycleBinService;
        private string _path;
        private bool _inRecycleBin;

        public override UndoableActionType ActionType => UndoableActionType.Delete;
        public override string Description => string.Format("删除 {0}", Path.GetFileName(_path));

        /// <summary>
        /// 构造函数：文件已在回收站中（删除后创建此Action）
        /// </summary>
        public RecycleBinDeleteUndoAction(IRecycleBinService recycleBinService, string originalPath)
        {
            _recycleBinService = recycleBinService ?? throw new ArgumentNullException(nameof(recycleBinService));
            _path = originalPath ?? throw new ArgumentNullException(nameof(originalPath));
            _inRecycleBin = true;
        }

        /// <summary>
        /// 构造函数：文件在磁盘上（复制/新建后创建此Action，撤销时发送到回收站）
        /// </summary>
        public RecycleBinDeleteUndoAction(IRecycleBinService recycleBinService, string path, bool inRecycleBin)
        {
            _recycleBinService = recycleBinService ?? throw new ArgumentNullException(nameof(recycleBinService));
            _path = path ?? throw new ArgumentNullException(nameof(path));
            _inRecycleBin = inRecycleBin;
        }

        public override bool Undo()
        {
            return Toggle();
        }

        public override bool Redo()
        {
            return Toggle();
        }

        private bool Toggle()
        {
            if (!_inRecycleBin)
            {
                // 状态 A: 文件在磁盘 → 发送到回收站
                if (_recycleBinService.Send(_path))
                {
                    _inRecycleBin = true;
                    return true;
                }
                return false;
            }
            else
            {
                // 状态 B: 文件在回收站 → 还原
                string restored = _recycleBinService.Restore(_path);
                if (restored != null)
                {
                    _path = restored;
                    _inRecycleBin = false;
                    return true;
                }
                return false;
            }
        }
    }
}
