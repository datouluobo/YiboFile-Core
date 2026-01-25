using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YiboFile.Models;
using YiboFile.Services.Core.Error;
using YiboFile.Services.FileOperations;
using YiboFile.Services.FileOperations.Undo;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

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

        public override string Name => "FileOperation";

        public FileOperationModule(
            IMessageBus messageBus,
            FileOperationService fileOperationService,
            UndoService undoService = null,
            ErrorService errorService = null)
            : base(messageBus)
        {
            _fileOperationService = fileOperationService ?? throw new ArgumentNullException(nameof(fileOperationService));
            _undoService = undoService;
            _errorService = errorService;
        }

        protected override void OnInitialize()
        {
            Subscribe<CreateFolderRequestMessage>(OnCreateFolder);
            Subscribe<DeleteItemsRequestMessage>(OnDeleteItems);
            Subscribe<CopyItemsRequestMessage>(OnCopyItems);
            Subscribe<CutItemsRequestMessage>(OnCutItems);
            Subscribe<PasteItemsRequestMessage>(OnPasteItems);
            Subscribe<RenameItemRequestMessage>(OnRenameItem);
            Subscribe<UndoRequestMessage>(m => OnUndo());
            Subscribe<RedoRequestMessage>(m => OnRedo());
        }

        #region 消息处理

        private async void OnCreateFolder(CreateFolderRequestMessage message)
        {
            await _fileOperationService.CreateFolderAsync(message.ParentPath, message.FolderName);
        }

        private async void OnDeleteItems(DeleteItemsRequestMessage message)
        {
            await _fileOperationService.DeleteAsync(message.Items, message.Permanent);
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
            await _fileOperationService.PasteAsync();
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
    }
}
