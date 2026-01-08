using System;
using System.Collections.Generic;
using System.IO;
using OoiMRR;

namespace OoiMRR.Services.FileOperations
{
    /// <summary>
    /// 重命名操作
    /// </summary>
    public class RenameOperation
    {
        private readonly IFileOperationContext _context;
        private readonly System.Windows.Window _ownerWindow;
        private readonly FileOperationService _fileOperationService;

        public RenameOperation(IFileOperationContext context, System.Windows.Window ownerWindow, FileOperationService fileOperationService = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _fileOperationService = fileOperationService;
        }

        /// <summary>
        /// 执行重命名操作
        /// </summary>
        /// <param name="item">要重命名的文件项</param>
        public void Execute(FileSystemItem item)
        {
            if (item == null) return;
            if (!_context.CanPerformOperation("Rename")) return;

            var dialog = new PathInputDialog
            {
                Title = "重命名",
                PromptText = "请输入新名称：",
                InputText = item.Name,
                SelectFileNameOnly = true,
                Owner = _ownerWindow
            };

            if (dialog.ShowDialog() == true)
            {
                var newName = dialog.InputText.Trim();
                Execute(item, newName);
            }
        }

        public async void Execute(FileSystemItem item, string newName)
        {
            System.Diagnostics.Debug.WriteLine($"[RenameOp] Execute called - Item: {item?.Name}, NewName: {newName}, Path: {item?.Path}");

            if (item == null)
            {
                System.Diagnostics.Debug.WriteLine("[RenameOp] Item is null, returning");
                return;
            }
            if (string.IsNullOrEmpty(newName))
            {
                System.Diagnostics.Debug.WriteLine("[RenameOp] NewName is empty, showing error");
                _context.ShowMessage("名称不能为空", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return;
            }

            if (_fileOperationService != null)
            {
                System.Diagnostics.Debug.WriteLine($"[RenameOp] Calling RenameAsync for {item.Path} -> {newName}");
                try
                {
                    await _fileOperationService.RenameAsync(item, newName);
                    System.Diagnostics.Debug.WriteLine("[RenameOp] RenameAsync completed");
                    _context.RefreshAfterOperation();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RenameOp] RenameAsync exception: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[RenameOp] _fileOperationService is null, using fallback");
                // Fallback logic
                try
                {
                    var newPath = Path.Combine(Path.GetDirectoryName(item.Path), newName);
                    if (item.IsDirectory) Directory.Move(item.Path, newPath);
                    else File.Move(item.Path, newPath);
                    _context.RefreshAfterOperation();
                }
                catch (Exception ex)
                {
                    _context.ShowMessage($"重命名失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}


























