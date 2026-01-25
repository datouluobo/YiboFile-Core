using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using YiboFile.Services.FileOperations;
using YiboFile.Services.FileOperations.Undo;
using System.Collections.Specialized;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 处理文件操作逻辑（复制、剪切、粘贴、删除）
    /// </summary>
    internal class FileOperationHandler
    {
        private readonly MainWindow _mainWindow;
        private readonly UndoService _undoService;
        private readonly FileOperationService _fileOperationService;

        public FileOperationHandler(MainWindow mainWindow, UndoService undoService, FileOperationService fileOperationService = null)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _undoService = undoService;
            _fileOperationService = fileOperationService;
        }

        public UndoService UndoService => _undoService;

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
            // TagOperationContext removed - Phase 2
            // else if (_mainWindow._currentTagFilter != null)
            // {
            //     return new TagOperationContext(
            //         _mainWindow._currentTagFilter,
            //         _mainWindow.FileBrowser,
            //         _mainWindow,
            //         () => _mainWindow.FilterByTag(_mainWindow._currentTagFilter));
            // }
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
        public async Task PerformCopyOperationAsync()
        {
            try
            {
                var selectedItems = _mainWindow.FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var paths = selectedItems.Select(item => item.Path).ToList();
                await YiboFile.Services.FileOperations.ClipboardService.Instance.SetCopyPathsAsync(paths);
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"复制操作失败: {ex.Message}", owner: _mainWindow);
            }
        }

        /// <summary>
        /// 执行剪切操作
        /// </summary>
        public async Task PerformCutOperationAsync()
        {
            try
            {
                var selectedItems = _mainWindow.FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    return;
                }

                var paths = selectedItems.Select(item => item.Path).ToList();
                await YiboFile.Services.FileOperations.ClipboardService.Instance.SetCutPathsAsync(paths);
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"剪切操作失败: {ex.Message}", owner: _mainWindow);
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

                if (_fileOperationService != null)
                {
                    await _fileOperationService.DeleteAsync(selectedItems, permanent: false);
                }
                else
                {
                    var context = GetCurrentOperationContext();
                    if (context == null) return;
                    var deleteOperation = new YiboFile.Services.FileOperations.DeleteOperation(context, _undoService);
                    await deleteOperation.ExecuteAsync(selectedItems);
                }
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"删除操作失败: {ex.Message}", owner: _mainWindow);
            }
        }

        /// <summary>
        /// 执行撤销
        /// </summary>
        public void PerformUndo()
        {
            if (_undoService.CanUndo)
            {
                _undoService.Undo();
                // 刷新当前视图以反映更改
                _mainWindow.RefreshFileList();
            }
        }

        /// <summary>
        /// 执行重做
        /// </summary>
        public void PerformRedo()
        {
            if (_undoService.CanRedo)
            {
                _undoService.Redo();
                // 刷新当前视图以反映更改
                _mainWindow.RefreshFileList();
            }
        }
    }
}

