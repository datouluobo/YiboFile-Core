using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using OoiMRR.Services.FileOperations;

namespace OoiMRR.Handlers
{
    /// <summary>
    /// 处理文件操作逻辑（复制、剪切、粘贴、删除）
    /// </summary>
    internal class FileOperationHandler
    {
        private readonly MainWindow _mainWindow;

        public FileOperationHandler(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        /// <summary>
        /// 获取当前操作上下文
        /// </summary>
        public IFileOperationContext GetCurrentOperationContext()
        {
            if (_mainWindow._currentLibrary != null)
            {
                return new LibraryOperationContext(
                    _mainWindow._currentLibrary,
                    _mainWindow.FileBrowser,
                    _mainWindow,
                    () => _mainWindow.LoadLibraryFiles(_mainWindow._currentLibrary));
            }
            else if (_mainWindow._currentTagFilter != null)
            {
                return new TagOperationContext(
                    _mainWindow._currentTagFilter,
                    _mainWindow.FileBrowser,
                    _mainWindow,
                    () => _mainWindow.FilterByTag(_mainWindow._currentTagFilter));
            }
            else
            {
                return new PathOperationContext(
                    _mainWindow._currentPath,
                    _mainWindow.FileBrowser,
                    _mainWindow,
                    _mainWindow.LoadCurrentDirectory);
            }
        }

        /// <summary>
        /// 执行复制操作
        /// </summary>
        public void PerformCopyOperation()
        {
            try
            {
                var selectedItems = _mainWindow.FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var paths = selectedItems.Select(item => item.Path).ToList();
                FileClipboardManager.SetCopyPaths(paths);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"复制操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行剪切操作
        /// </summary>
        public void PerformCutOperation()
        {
            try
            {
                var selectedItems = _mainWindow.FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var paths = selectedItems.Select(item => item.Path).ToList();
                FileClipboardManager.SetCutPaths(paths);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"剪切操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行粘贴操作
        /// </summary>
        public void PerformPasteOperation()
        {
            try
            {
                var copiedPaths = FileClipboardManager.GetCopiedPaths();
                if (copiedPaths == null || copiedPaths.Count == 0)
                {
                    return;
                }

                var context = GetCurrentOperationContext();
                if (context == null)
                {
                    return;
                }

                var isCut = FileClipboardManager.IsCutOperation;
                var pasteOperation = new PasteOperation(context);
                pasteOperation.Execute(copiedPaths, isCut);

                if (isCut)
                {
                    FileClipboardManager.ClearCutOperation();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"粘贴操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 执行删除操作
        /// </summary>
        public async Task PerformDeleteOperationAsync()
        {
            try
            {
                var selectedItems = _mainWindow.FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var context = GetCurrentOperationContext();
                if (context == null)
                {
                    return;
                }

                var deleteOperation = new DeleteOperation(context);
                await deleteOperation.ExecuteAsync(selectedItems);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
