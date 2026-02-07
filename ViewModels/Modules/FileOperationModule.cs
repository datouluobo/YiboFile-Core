using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Collections;
using System.Windows;
using YiboFile.Models;
using YiboFile.Services.Core.Error;
using YiboFile.Services.FileOperations;
using YiboFile.Services.FileOperations.Undo;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 文件操作模块
    /// 处理复制、粘贴、剪切、删除、重命名和创建文件夹等操作
    /// </summary>
    public class FileOperationModule : ModuleBase
    {
        private readonly FileOperationService _fileOperationService;
        private readonly UndoService _undoService;
        private readonly ErrorService _errorService;
        private readonly LibraryService _libraryService;

        public override string Name => "FileOperation";

        #region Commands

        public ICommand CopyCommand { get; }
        public ICommand CutCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand RenameCommand { get; }
        public ICommand NewFolderCommand { get; }
        public ICommand NewFileCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        #endregion

        public FileOperationModule(
            IMessageBus messageBus,
            FileOperationService fileOperationService,
            UndoService undoService = null,
            ErrorService errorService = null,
            LibraryService libraryService = null)
            : base(messageBus)
        {
            _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
            _undoService = undoService;
            _errorService = errorService;
            _libraryService = libraryService ?? App.ServiceProvider?.GetService<LibraryService>();

            CopyCommand = new RelayCommand<IList>(ExecuteCopy, CanExecuteCopy);
            CutCommand = new RelayCommand<IList>(ExecuteCut, CanExecuteCut);
            PasteCommand = new RelayCommand<PaneViewModel>(ExecutePaste, CanExecutePaste);
            DeleteCommand = new RelayCommand<IList>(ExecuteDelete, CanExecuteDelete);
            RenameCommand = new RelayCommand<FileSystemItem>(ExecuteRename, CanExecuteRename);
            NewFolderCommand = new RelayCommand<PaneViewModel>(ExecuteNewFolder, CanExecuteNewFolder);
            NewFileCommand = new RelayCommand<PaneViewModel>(ExecuteNewFile, CanExecuteNewFile);
            UndoCommand = new RelayCommand(OnUndo, () => _undoService?.CanUndo == true);
            RedoCommand = new RelayCommand(OnRedo, () => _undoService?.CanRedo == true);
        }

        protected override void OnInitialize()
        {
            Subscribe<CreateFolderRequestMessage>(OnCreateFolder);
            Subscribe<CreateFileRequestMessage>(OnCreateFile);
            Subscribe<DeleteItemsRequestMessage>(OnDeleteItems);
            Subscribe<CopyItemsRequestMessage>(OnCopyItems);
            Subscribe<CutItemsRequestMessage>(OnCutItems);
            Subscribe<PasteItemsRequestMessage>(OnPasteItems);
            Subscribe<RenameItemRequestMessage>(OnRenameItem);
            Subscribe<UndoRequestMessage>(m => OnUndo());
            Subscribe<RedoRequestMessage>(m => OnRedo());
            Subscribe<ShowPropertiesRequestMessage>(OnShowProperties);
        }

        #region 消息处理

        private async void OnCreateFolder(CreateFolderRequestMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"[FileOperationModule] Creating folder. Parent: {message.ParentPath}, Name: {message.FolderName}");
            await _fileOperationService.CreateFolderAsync(message.ParentPath, message.FolderName);
            System.Diagnostics.Debug.WriteLine($"[FileOperationModule] Folder created. Publishing RefreshFileListMessage for: {message.ParentPath}");
            Publish(new RefreshFileListMessage(message.ParentPath));
        }

        private async void OnCreateFile(CreateFileRequestMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"[FileOperationModule] Creating file. Parent: {message.ParentPath}, Name: {message.FileName}, Ext: {message.Extension}");
            await _fileOperationService.CreateFileAsync(message.ParentPath, message.FileName, message.Extension);
            System.Diagnostics.Debug.WriteLine($"[FileOperationModule] File created. Publishing RefreshFileListMessage for: {message.ParentPath}");
            Publish(new RefreshFileListMessage(message.ParentPath));
        }

        private async void OnDeleteItems(DeleteItemsRequestMessage message)
        {
            await _fileOperationService.DeleteAsync(message.Items, message.Permanent);

            // Refresh specific parents instead of global refresh
            if (message.Items != null && message.Items.Count > 0)
            {
                var parents = message.Items
                    .Select(i => System.IO.Path.GetDirectoryName(i.Path))
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
                    .ToList();

                foreach (var parent in parents)
                {
                    Publish(new RefreshFileListMessage(parent));
                }

                // Fallback if no parents found
                if (parents.Count == 0) Publish(new RefreshFileListMessage());
            }
            else
            {
                Publish(new RefreshFileListMessage());
            }
        }

        private async void OnCopyItems(CopyItemsRequestMessage message)
        {
            await _fileOperationService.CopyAsync(message.Items?.Select(i => i.Path));
        }

        private async void OnCutItems(CutItemsRequestMessage message)
        {
            await _fileOperationService.CutAsync(message.Items?.Select(i => i.Path));
        }

        private async void OnPasteItems(PasteItemsRequestMessage message)
        {
            string targetPath = message.TargetPath;

            // 库路径解析逻辑
            if (!string.IsNullOrEmpty(targetPath) && targetPath.StartsWith("lib://", StringComparison.OrdinalIgnoreCase))
            {
                var parts = targetPath.Substring(6).Split('/');
                var libName = parts[0];
                var lib = _libraryService?.GetAllLibraries()?.FirstOrDefault(l => string.Equals(l.Name, libName, StringComparison.OrdinalIgnoreCase));
                if (lib != null && lib.Paths.Count > 0)
                {
                    targetPath = lib.Paths.FirstOrDefault(p => System.IO.Directory.Exists(p)) ?? targetPath;
                }
            }

            await _fileOperationService.PasteAsync(targetPath);
            // 延迟一点刷新，或者由 FileOperationService 触发刷新回调
            Publish(new RefreshFileListMessage(targetPath));
        }

        private void OnShowProperties(ShowPropertiesRequestMessage message)
        {
            string targetPath = message.Item?.Path;
            if (string.IsNullOrEmpty(targetPath))
            {
                targetPath = message.CurrentPath;
            }

            if (!string.IsNullOrEmpty(targetPath))
            {
                if (YiboFile.Services.Core.ProtocolManager.IsVirtual(targetPath))
                {
                    // 暂时不支持压缩包内文件的系统属性
                    YiboFile.DialogService.Info($"暂不支持查看此类型的系统属性：\n{targetPath}");
                    return;
                }

                YiboFile.Services.Core.ShellNative.ShowFileProperties(targetPath);
            }
        }

        private async void OnRenameItem(RenameItemRequestMessage message)
        {
            if (message.NewName == null)
            {
                // 如果没有提供新名字，通知 UI 进入重命名模式
                if (message.Item != null)
                {
                    message.Item.IsRenaming = true;
                }
            }
            else
            {
                // 执行实际重命名逻辑
                await _fileOperationService.RenameAsync(message.Item, message.NewName);
            }
        }

        private void OnUndo()
        {
            if (_undoService?.CanUndo == true)
            {
                var description = _undoService.NextUndoDescription;
                if (_undoService.Undo())
                {
                    _errorService?.ReportError($"已撤销: {description}", ErrorSeverity.Info);
                    // 广播一个刷新消息
                    Publish(new RefreshFileListMessage());
                }
                else
                {
                    _errorService?.ReportError("撤销失败", ErrorSeverity.Warning);
                }
            }
        }

        private void OnRedo()
        {
            if (_undoService?.CanRedo == true)
            {
                var description = _undoService.NextRedoDescription;
                if (_undoService.Redo())
                {
                    _errorService?.ReportError($"已重做: {description}", ErrorSeverity.Info);
                    Publish(new RefreshFileListMessage());
                }
                else
                {
                    _errorService?.ReportError("重做失败", ErrorSeverity.Warning);
                }
            }
        }

        #endregion

        #region 公开方法 (供直接调用)

        public async Task CreateFolder(string parentPath, string folderName = null)
        {
            await _fileOperationService.CreateFolderAsync(parentPath, folderName);
        }

        public async Task Delete(List<FileSystemItem> items, bool permanent = false)
        {
            await _fileOperationService.DeleteAsync(items, permanent);
        }

        public async Task Copy(List<FileSystemItem> items)
        {
            await _fileOperationService.CopyAsync(items?.Select(i => i.Path));
        }

        public async Task Cut(List<FileSystemItem> items)
        {
            await _fileOperationService.CutAsync(items?.Select(i => i.Path));
        }

        public async Task Paste()
        {
            await _fileOperationService.PasteAsync();
        }

        #endregion
        #region Command Handlers

        private void ExecuteCopy(IList items)
        {
            var fileItems = items?.OfType<FileSystemItem>().ToList();
            if (fileItems != null && fileItems.Any())
            {
                Publish(new CopyItemsRequestMessage(fileItems));
            }
        }

        private bool CanExecuteCopy(IList items)
        {
            return items != null && items.Count > 0;
        }

        private void ExecuteCut(IList items)
        {
            var fileItems = items?.OfType<FileSystemItem>().ToList();
            if (fileItems != null && fileItems.Any())
            {
                Publish(new CutItemsRequestMessage(fileItems));
            }
        }

        private bool CanExecuteCut(IList items)
        {
            return items != null && items.Count > 0;
        }

        private void ExecutePaste(PaneViewModel pane)
        {
            if (pane != null)
            {
                string targetPath = pane.CurrentPath;
                Publish(new PasteItemsRequestMessage(targetPath));
            }
        }

        private bool CanExecutePaste(PaneViewModel pane)
        {
            // 在库模式下，CurrentPath 可能是 lib:// 协议
            return pane != null && !string.IsNullOrEmpty(pane.CurrentPath) && Clipboard.ContainsFileDropList();
        }

        private void ExecuteDelete(IList items)
        {
            var fileItems = items?.OfType<FileSystemItem>().ToList();
            if (fileItems != null && fileItems.Any())
            {
                // Check shift key for permanent delete?
                // CommandParameter usually doesn't capture modifier keys.
                // We might need a separate DeletePermanentCommand or check Keyboard.Modifiers (UI dependency in VM, but acceptable for commands)
                bool permanent = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                Publish(new DeleteItemsRequestMessage(fileItems, permanent));
            }
        }

        private bool CanExecuteDelete(IList items)
        {
            return items != null && items.Count > 0;
        }

        private void ExecuteRename(FileSystemItem item)
        {
            if (item != null)
            {
                Publish(new RenameItemRequestMessage(item, null)); // null triggers edit mode
            }
        }

        private bool CanExecuteRename(FileSystemItem item)
        {
            return item != null;
        }

        private void ExecuteNewFolder(PaneViewModel pane)
        {
            if (pane != null)
            {
                System.Diagnostics.Debug.WriteLine($"[FileOperationModule] ExecuteNewFolder. Mode: {pane.NavigationMode}, Path: {pane.CurrentPath}, Lib: {pane.CurrentLibrary?.Name}");

                string targetPath = pane.CurrentPath;
                if (pane.NavigationMode == "Library" && pane.CurrentLibrary != null)
                {
                    // If in library mode, we need a physical path to create folder
                    // Use the first available path in the library? Or ask user?
                    // Implementation of FileOperationService.CreateFolderAsync might handle lib:// paths or not?
                    // Let's assume we pick the first valid path of the library for now, or check what FileOperationService does.
                    var libPaths = pane.CurrentLibrary.Paths;
                    if (libPaths != null && libPaths.Count > 0)
                    {
                        targetPath = libPaths.FirstOrDefault(p => System.IO.Directory.Exists(p));
                        System.Diagnostics.Debug.WriteLine($"[FileOperationModule] Resolved Library path to: {targetPath}");
                    }
                }

                if (!string.IsNullOrEmpty(targetPath))
                {
                    Publish(new CreateFolderRequestMessage(targetPath));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[FileOperationModule] Cannot create folder: Target path is empty.");
                }
            }
        }

        private bool CanExecuteNewFolder(PaneViewModel pane)
        {
            return pane != null && !string.IsNullOrEmpty(pane.CurrentPath);
        }

        private void ExecuteNewFile(PaneViewModel pane)
        {
            if (pane != null)
            {
                string targetPath = pane.CurrentPath;
                if (pane.NavigationMode == "Library" && pane.CurrentLibrary != null)
                {
                    var libPaths = pane.CurrentLibrary.Paths;
                    if (libPaths != null && libPaths.Count > 0)
                    {
                        targetPath = libPaths.FirstOrDefault(p => System.IO.Directory.Exists(p));
                    }
                }

                if (!string.IsNullOrEmpty(targetPath))
                {
                    Publish(new CreateFileRequestMessage(targetPath));
                }
            }
        }

        private bool CanExecuteNewFile(PaneViewModel pane)
        {
            return pane != null && !string.IsNullOrEmpty(pane.CurrentPath);
        }

        #endregion

    }
}
