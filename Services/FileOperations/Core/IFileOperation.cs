using System;
using System.Threading;
using System.Threading.Tasks;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 文件操作接口 - 所有文件操作的统一抽象
    /// </summary>
    public interface IFileOperation
    {
        /// <summary>
        /// 异步执行操作
        /// </summary>
        Task<FileOperationResult> ExecuteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 进度变更事件 (当前数量, 总数量, 当前文件名)
        /// </summary>
        event Action<int, int, string> ProgressChanged;

        /// <summary>
        /// 是否支持撤销
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// 操作描述（用于撤销历史显示）
        /// </summary>
        string Description { get; }
    }
}

