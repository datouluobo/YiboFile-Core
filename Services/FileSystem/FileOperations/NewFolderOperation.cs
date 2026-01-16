using System;
using System.IO;
using YiboFile;

namespace YiboFile.Services.FileOperations
{
    /// <summary>
    /// 新建文件夹操作
    /// </summary>
    public class NewFolderOperation
    {
        private readonly IFileOperationContext _context;
        private readonly System.Windows.Window _ownerWindow;

        public NewFolderOperation(IFileOperationContext context, System.Windows.Window ownerWindow)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
        }

        /// <summary>
        /// 执行新建文件夹操作
        /// </summary>
        public void Execute()
        {
            if (!_context.CanPerformOperation("NewFolder"))
            {
                return;
            }

            string targetPath = _context.GetTargetPath();
            if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            {
                _context.ShowMessage("当前没有可用的路径", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 使用简单的输入对话框
            var dialog = new PathInputDialog
            {
                Title = "新建文件夹",
                PromptText = "请输入文件夹名称：",
                InputText = "新建文件夹",
                Owner = _ownerWindow
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var folderName = dialog.InputText.Trim();

                    // 验证文件夹名称
                    if (string.IsNullOrEmpty(folderName))
                    {
                        _context.ShowMessage("文件夹名称不能为空", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    // 检查非法字符
                    char[] invalidChars = Path.GetInvalidFileNameChars();
                    if (folderName.IndexOfAny(invalidChars) >= 0)
                    {
                        _context.ShowMessage("文件夹名称包含非法字符", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    var folderPath = Path.Combine(targetPath, folderName);

                    // 如果已存在，自动添加序号
                    if (Directory.Exists(folderPath))
                    {
                        int counter = 2;
                        string newFolderName;
                        do
                        {
                            newFolderName = $"{folderName} ({counter})";
                            folderPath = Path.Combine(targetPath, newFolderName);
                            counter++;
                        }
                        while (Directory.Exists(folderPath));
                    }

                    // 创建文件夹
                    Directory.CreateDirectory(folderPath);

                    // 刷新显示
                    _context.RefreshAfterOperation();
                }
                catch (Exception ex)
                {
                    _context.ShowMessage($"创建文件夹失败: {ex.Message}", "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}



























