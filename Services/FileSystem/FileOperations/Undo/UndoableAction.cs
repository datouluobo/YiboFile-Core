using System;

namespace YiboFile.Services.FileOperations.Undo
{
    /// <summary>
    /// 可撤销操作的类型
    /// </summary>
    public enum UndoableActionType
    {
        Delete,
        Move,
        Rename,
        Copy,
        NewFile,
        None
    }

    /// <summary>
    /// 可撤销操作的基类
    /// </summary>
    public abstract class UndoableAction
    {
        /// <summary>
        /// 操作类型
        /// </summary>
        public abstract UndoableActionType ActionType { get; }

        /// <summary>
        /// 操作描述（用于显示）
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// 操作时间
        /// </summary>
        public DateTime Timestamp { get; } = DateTime.Now;

        /// <summary>
        /// 执行撤销操作
        /// </summary>
        /// <returns>是否成功</returns>
        public abstract bool Undo();

        /// <summary>
        /// 执行重做操作
        /// </summary>
        /// <returns>是否成功</returns>
        public abstract bool Redo();
    }
}

