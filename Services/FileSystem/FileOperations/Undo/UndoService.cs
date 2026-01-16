using System;
using System.Collections.Generic;
using YiboFile.Services.Core;

namespace YiboFile.Services.FileOperations.Undo
{
    /// <summary>
    /// 撤销/重做服务
    /// 管理文件操作的撤销栈
    /// </summary>
    public class UndoService
    {
        private readonly Stack<UndoableAction> _undoStack = new Stack<UndoableAction>();
        private readonly Stack<UndoableAction> _redoStack = new Stack<UndoableAction>();
        private readonly int _maxStackSize;

        /// <summary>
        /// 撤销栈变化事件
        /// </summary>
        public event EventHandler StackChanged;

        /// <summary>
        /// 是否可以撤销
        /// </summary>
        public bool CanUndo => _undoStack.Count > 0;

        /// <summary>
        /// 是否可以重做
        /// </summary>
        public bool CanRedo => _redoStack.Count > 0;

        /// <summary>
        /// 下一个撤销操作的描述
        /// </summary>
        public string NextUndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;

        /// <summary>
        /// 下一个重做操作的描述
        /// </summary>
        public string NextRedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

        /// <summary>
        /// 撤销执行后触发
        /// </summary>
        public event EventHandler ActionUndone;

        /// <summary>
        /// 重做执行后触发
        /// </summary>
        public event EventHandler ActionRedone;

        public UndoService(int maxStackSize = 50)
        {
            _maxStackSize = maxStackSize;
        }

        /// <summary>
        /// 记录一个可撤销操作
        /// </summary>
        public void RecordAction(UndoableAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear(); // 新操作清除重做栈

            // 限制栈大小
            if (_undoStack.Count > _maxStackSize)
            {
                // 转换为列表，移除最旧的项
                var list = new List<UndoableAction>(_undoStack);
                list.RemoveAt(list.Count - 1);
                _undoStack.Clear();
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    _undoStack.Push(list[i]);
                }
            }

            StackChanged?.Invoke(this, EventArgs.Empty);
            FileLogger.Log($"[UndoService] 记录操作: {action.Description}");
        }

        /// <summary>
        /// 执行撤销
        /// </summary>
        /// <returns>撤销是否成功</returns>
        public bool Undo()
        {
            if (!CanUndo)
                return false;

            var action = _undoStack.Pop();
            FileLogger.Log($"[UndoService] 撤销: {action.Description}");

            if (action.Undo())
            {
                _redoStack.Push(action);
                StackChanged?.Invoke(this, EventArgs.Empty);
                ActionUndone?.Invoke(this, EventArgs.Empty);
                return true;
            }
            else
            {
                // 撤销失败，放回栈中
                _undoStack.Push(action);
                FileLogger.Log($"[UndoService] 撤销失败: {action.Description}");
                return false;
            }
        }

        /// <summary>
        /// 执行重做
        /// </summary>
        /// <returns>重做是否成功</returns>
        public bool Redo()
        {
            if (!CanRedo)
                return false;

            var action = _redoStack.Pop();
            FileLogger.Log($"[UndoService] 重做: {action.Description}");

            if (action.Redo())
            {
                _undoStack.Push(action);
                StackChanged?.Invoke(this, EventArgs.Empty);
                ActionRedone?.Invoke(this, EventArgs.Empty);
                return true;
            }
            else
            {
                // 重做失败，放回栈中
                _redoStack.Push(action);
                FileLogger.Log($"[UndoService] 重做失败: {action.Description}");
                return false;
            }
        }

        /// <summary>
        /// 清空所有记录
        /// </summary>
        public void Clear()
        {
            // 清理删除操作的备份文件
            foreach (var action in _undoStack)
            {
                if (action is DeleteUndoAction deleteAction)
                {
                    deleteAction.CleanupBackup();
                }
            }

            _undoStack.Clear();
            _redoStack.Clear();
            StackChanged?.Invoke(this, EventArgs.Empty);

            // 移除自动清理，改为由程序退出时调用
            // BackupCleanupService.Cleanup();
        }
    }
}

