using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OoiMRR.Services.FileOperations
{
    /// <summary>
    /// 删除操作
    /// </summary>
    public class DeleteOperation
    {
        private readonly IFileOperationContext _context;

        public DeleteOperation(IFileOperationContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 执行删除操作
        /// </summary>
        /// <param name="items">要删除的文件项列表</param>
        public void Execute(List<FileSystemItem> items)
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

            if (!_context.ShowConfirm(message, "确认删除"))
            {
                return;
            }

            var failedItems = new List<string>();

            foreach (var item in items)
            {
                try
                {
                    if (item.IsDirectory)
                    {
                        Directory.Delete(item.Path, true);
                    }
                    else
                    {
                        File.Delete(item.Path);
                    }
                }
                catch (Exception ex)
                {
                    failedItems.Add($"{item.Name}: {ex.Message}");
                }
            }

            _context.RefreshAfterOperation();

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













