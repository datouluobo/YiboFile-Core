using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Dialogs;
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
            var locService = App.ServiceProvider?.GetService<YiboFile.Services.Localization.ILocalizationService>();

            if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            {
                _context.ShowMessage(locService?["FileOp.NoValidPath"] ?? "当前没有可用的路径", locService?["FileInfo.Error"] ?? "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 使用简单的输入对话框
            string inputPrompt = locService?["FileOp.EnterFolderName"] ?? "请输入文件夹名称：";
            string title = locService?["FileOp.NewFolder"] ?? "新建文件夹";
            string defaultValue = locService?["FileOp.NewFolder"] ?? "新建文件夹";

            string inputName = DialogService.ShowInput(inputPrompt, title, defaultValue, owner: _ownerWindow);

            if (inputName != null)
            {
                try
                {
                    var folderName = inputName.Trim();

                    // 验证文件夹名称
                    if (string.IsNullOrEmpty(folderName))
                    {
                        _context.ShowMessage(locService?["FileOp.NameEmpty"] ?? "文件夹名称不能为空", locService?["FileInfo.Error"] ?? "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    // 检查非法字符
                    char[] invalidChars = Path.GetInvalidFileNameChars();
                    if (folderName.IndexOfAny(invalidChars) >= 0)
                    {
                        _context.ShowMessage(locService?["FileOp.NameInvalidChars"] ?? "文件夹名称包含非法字符", locService?["FileInfo.Error"] ?? "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
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
                    string successMsg = string.Format(locService?["FileOp.CreateSuccessFormat"] ?? "已创建文件夹: {0}", folderName);
                    YiboFile.Services.Core.NotificationService.ShowSuccess(successMsg);
                }
                catch (Exception ex)
                {
                    string errorMsg = string.Format(locService?["FileOp.CreateFailedFormat"] ?? "创建文件夹失败: {0}", ex.Message);
                    _context.ShowMessage(errorMsg, locService?["FileInfo.Error"] ?? "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}



























