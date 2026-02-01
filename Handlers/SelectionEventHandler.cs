using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YiboFile.Services.Preview;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services;
using YiboFile.Services.FileList;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 选择事件处理器
    /// 处理文件列表的选中变更，协调预览、信息面板和各种状态通知
    /// </summary>
    public class SelectionEventHandler
    {
        private readonly PreviewService _filePreviewService;
        private readonly IMessageBus _messageBus;
        private readonly FileListService _fileListService;
        private readonly Func<List<FileSystemItem>> _getCurrentFiles;
        private readonly Func<string> _getCurrentPath;
        private readonly Func<Library> _getCurrentLibrary;
        private readonly Func<bool> _isDualMode;
        private readonly Func<string, Task> _calculateFolderSize;
        private readonly Action<FileSystemItem> _showFileInfo;
        private readonly Action<Library> _showLibraryInfo;

        private CancellationTokenSource _folderSizeCts;

        public SelectionEventHandler(
            PreviewService previewService,
            IMessageBus messageBus,
            FileListService fileListService,
            Func<List<FileSystemItem>> getCurrentFiles,
            Func<string> getCurrentPath,
            Func<Library> getCurrentLibrary,
            Func<bool> isDualMode,
            Func<string, Task> calculateFolderSize,
            Action<FileSystemItem> showFileInfo,
            Action<Library> showLibraryInfo)
        {
            _filePreviewService = previewService;
            _messageBus = messageBus;
            _fileListService = fileListService;
            _getCurrentFiles = getCurrentFiles;
            _getCurrentPath = getCurrentPath;
            _getCurrentLibrary = getCurrentLibrary;
            _isDualMode = isDualMode;
            _calculateFolderSize = calculateFolderSize;
            _showFileInfo = showFileInfo;
            _showLibraryInfo = showLibraryInfo;
        }

        public void HandleSelectionChanged(System.Collections.IList selectedItems)
        {
            if (selectedItems != null && selectedItems.Count > 0)
            {
                var selectedItem = selectedItems[0] as FileSystemItem;
                if (selectedItem != null)
                {
                    // 1. 发送选择变更消息 (MVVM 订阅者会收到此消息，更新预览)
                    _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { selectedItem }, RequestPreview: true));

                    // 2. 更新信息面板 (Legacy UI 操作)
                    _showFileInfo?.Invoke(selectedItem);

                    // 3. 如果选中的是文件夹，检查是否需要计算大小
                    if (selectedItem.IsDirectory)
                    {
                        if (string.IsNullOrEmpty(selectedItem.Size) ||
                            selectedItem.Size == "-" ||
                            selectedItem.Size == "计算中...")
                        {
                            CalculateFolderSizeImmediately(selectedItem.Path);
                        }
                    }
                }
            }
            else
            {
                HandleNoSelection();
            }
        }

        public void HandleNoSelection()
        {
            // 清除旧的预览状态
            _filePreviewService?.ClearPreview();

            // 检查当前是否在库模式
            Library currentLib = _getCurrentLibrary?.Invoke();
            if (currentLib != null)
            {
                // 发送库选择消息
                _messageBus.Publish(new LibrarySelectedMessage(currentLib));

                // 更新库信息面板
                _showLibraryInfo?.Invoke(currentLib);

                // 在库模式下，直接返回，防止后续逻辑清空面板
                return;
            }

            // 获取并显示当前文件夹信息
            try
            {
                string currentPath = _getCurrentPath?.Invoke();
                if (string.IsNullOrEmpty(currentPath))
                {
                    _messageBus.Publish(new FileSelectionChangedMessage(null));
                    _showFileInfo?.Invoke(null);
                    return;
                }

                // 处理标签虚拟路径
                if (currentPath.StartsWith("tag://", StringComparison.OrdinalIgnoreCase))
                {
                    var tagName = currentPath.Substring(6);
                    var tagItem = new FileSystemItem
                    {
                        Name = tagName,
                        Path = currentPath,
                        Type = "标签", // Or "Tag" depending on localization
                        IsDirectory = true, // Treat as container
                        Size = "-",
                        ModifiedDate = "-",
                        Tags = tagName
                    };

                    // Publish message with no preview request
                    _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { tagItem }, RequestPreview: false));
                    _showFileInfo?.Invoke(tagItem);
                    return;
                }

                bool isVirtual = YiboFile.Services.Core.ProtocolManager.IsVirtual(currentPath);
                bool exists = !isVirtual && System.IO.Directory.Exists(currentPath);

                if (exists)
                {
                    var dirInfo = new System.IO.DirectoryInfo(currentPath);
                    var item = new FileSystemItem
                    {
                        Name = dirInfo.Name,
                        Path = dirInfo.FullName,
                        Type = "文件夹",
                        IsDirectory = true,
                        ModifiedDateTime = dirInfo.LastWriteTime,
                        ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy/M/d HH:mm"),
                        CreatedDateTime = dirInfo.CreationTime,
                        CreatedTime = dirInfo.CreationTime.ToString("yyyy/M/d HH:mm"),
                        Size = "-",
                        Tags = ""
                    };

                    // 发布文件夹选择消息，RequestPreview: false 确保不启动预览加载
                    _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { item }, RequestPreview: false));

                    // 显示文件夹信息
                    _showFileInfo?.Invoke(item);

                    // 同时也尝试计算一次当前路径的大小（如果需要）
                    CalculateFolderSizeImmediately(item.Path);
                }
                else
                {
                    _messageBus.Publish(new FileSelectionChangedMessage(null));
                    _showFileInfo?.Invoke(null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SelectionEventHandler] HandleNoSelection Error: {ex.Message}");
                _messageBus.Publish(new FileSelectionChangedMessage(null));
                _showFileInfo?.Invoke(null);
            }
        }

        private void CalculateFolderSizeImmediately(string path)
        {
            _folderSizeCts?.Cancel();
            _folderSizeCts = new CancellationTokenSource();
            var token = _folderSizeCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;
                    if (_calculateFolderSize != null)
                    {
                        await _calculateFolderSize(path);
                    }
                }
                catch { }
            }, token);
        }

        public void UpdatePaneStats()
        {
            var files = _getCurrentFiles?.Invoke();
            if (files != null)
            {
                // _mainWindow.UpdateStatusBarTotal(files.Count, _fileListService.GetTotalSize(files));
                // 统一交由消息订阅者处理
            }
        }
    }
}
