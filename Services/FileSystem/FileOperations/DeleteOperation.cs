using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YiboFile.Services.FileOperations.Undo;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 删除操作
    /// </summary>
    public class DeleteOperation
    {
        private readonly IFileOperationContext _context;
        private readonly UndoService _undoService;

        public DeleteOperation(IFileOperationContext context, UndoService undoService = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _undoService = undoService;
        }

        /// <summary>
        /// 执行删除操作（同步方法，已废弃，建议使用ExecuteAsync）
        /// </summary>
        /// <param name="items">要删除的文件项列表</param>
        public void Execute(List<FileSystemItem> items)
        {
            ExecuteAsync(items).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 异步执行删除操作
        /// </summary>
        /// <param name="items">要删除的文件项列表</param>
        public async Task ExecuteAsync(List<FileSystemItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            if (!_context.CanPerformOperation("Delete"))
            {
                return;
            }

            var itemCount = items.Count;
            var message = itemCount == 1
                ? $"确定要删除 \"{items[0].Name}\" 吗？"
                : $"确定要删除这 {itemCount} 个项目吗？";

            // 确认对话框必须在调用线程（UI线程）上显示
            if (!_context.ShowConfirm(message, "确认删除"))
            {
                return;
            }

            var failedItems = new List<string>();
            var undoActions = new List<DeleteUndoAction>();

            // 在后台线程中执行删除操作，避免阻塞UI
            await Task.Run(() =>
            {
                foreach (var item in items)
                {
                    try
                    {
                        if (_undoService != null)
                        {
                            // 使用可撤销删除（移动到备份目录）
                            var undoAction = new DeleteUndoAction(item.Path, item.IsDirectory);
                            if (undoAction.Execute())
                            {
                                lock (undoActions)
                                {
                                    undoActions.Add(undoAction);
                                }
                            }
                            else
                            {
                                lock (failedItems)
                                {
                                    failedItems.Add($"{item.Name}: 移动到备份目录失败");
                                }
                            }
                        }
                        else
                        {
                            // 直接删除（不可撤销）
                            if (item.IsDirectory)
                            {
                                Directory.Delete(item.Path, true);
                            }
                            else
                            {
                                File.Delete(item.Path);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (failedItems)
                        {
                            failedItems.Add($"{item.Name}: {ex.Message}");
                        }
                    }
                }
            });

            // 记录撤销操作
            foreach (var action in undoActions)
            {
                _undoService?.RecordAction(action);
            }

            // 刷新
            _context.RefreshAfterOperation();

            // 错误消息
            if (failedItems.Count > 0)
            {
                _context.ShowMessage(
                    $"以下项目删除失败:\n\n{string.Join("\n", failedItems)}",
                    "删除失败",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
    }
}
























