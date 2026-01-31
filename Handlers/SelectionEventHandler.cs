using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Models;
using YiboFile.Models.UI;
using YiboFile.Services;
using YiboFile.Services.FileNotes;
using YiboFile.Services.FileOperations;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 处理文件列表的选择事件
    /// </summary>
    public class SelectionEventHandler
    {
        private readonly MainWindow _mainWindow;
        private readonly YiboFile.Services.Preview.PreviewService _filePreviewService;
        private readonly IMessageBus _messageBus;

        private readonly YiboFile.Services.FileList.FileListService _fileListService;
        private readonly Func<List<FileSystemItem>> _getCurrentFiles;
        private readonly Func<string> _getCurrentPath;
        private readonly Func<Library> _getCurrentLibrary;
        private readonly Func<bool> _isDualMode;
        private readonly Action<FileSystemItem> _updateInfoPanel;
        private System.Threading.CancellationTokenSource _folderSizeCts;

        public SelectionEventHandler(
            MainWindow mainWindow,
            YiboFile.Services.Preview.PreviewService filePreviewService,
            IMessageBus messageBus,
            YiboFile.Services.FileList.FileListService fileListService,
            Func<List<FileSystemItem>> getCurrentFiles,
            Func<string> getCurrentPath,
            Action<FileSystemItem> updateInfoPanel = null,
            Func<Library> getCurrentLibrary = null,
            Action<Library> showLibraryInfo = null,
            Func<bool> isDualMode = null)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _filePreviewService = filePreviewService ?? throw new ArgumentNullException(nameof(filePreviewService));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            _fileListService = fileListService ?? throw new ArgumentNullException(nameof(fileListService));
            _getCurrentFiles = getCurrentFiles ?? throw new ArgumentNullException(nameof(getCurrentFiles));
            _getCurrentPath = getCurrentPath ?? throw new ArgumentNullException(nameof(getCurrentPath));
            _updateInfoPanel = updateInfoPanel;
            _getCurrentLibrary = getCurrentLibrary;
            _isDualMode = isDualMode;
        }

        public void HandleSelectionChanged(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;

            if (selectedItems.Count > 0)
            {
                // 1. 发送消息通知所有依赖项（MVVM 模式）
                _messageBus.Publish(new FileSelectionChangedMessage(selectedItems));

                var selectedItem = selectedItems[0] as FileSystemItem;
                if (selectedItem != null)
                {
                    // 2. 更新信息面板 (如果提供了回调)
                    _updateInfoPanel?.Invoke(selectedItem);

                    // 3. 加载预览 (仅在非双栏模式下)
                    bool isPreviewEnabled = _isDualMode == null || !_isDualMode();
                    if (isPreviewEnabled)
                    {
                        _filePreviewService?.LoadFilePreview(selectedItem);
                    }
                    else
                    {
                        // 确保清除旧的预览
                        _filePreviewService?.ClearPreview();
                    }

                    // 4. 更新底部信息栏 (已通过消息由 MVVM 订阅)


                    // 5. 检查剪贴板状态（如果是剪切，调整透明度）
                    try
                    {
                        var clipboardService = YiboFile.Services.FileOperations.ClipboardService.Instance;
                        bool isCut = clipboardService.IsCutOperation && clipboardService.CutPaths.Contains(selectedItem.Path);
                        if (_mainWindow.FileBrowser?.FilesList != null)
                        {
                            var container = _mainWindow.FileBrowser.FilesList.ItemContainerGenerator.ContainerFromItem(selectedItem) as ListViewItem;
                            if (container != null)
                            {
                                container.Opacity = isCut ? 0.5 : 1.0;
                            }
                        }
                    }
                    catch { }

                    // 7. 文件夹大小计算
                    if (selectedItem.IsDirectory)
                    {
                        // 检查大小是否已计算
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
            // 8. 没有选择文件时：
            _messageBus.Publish(new FileSelectionChangedMessage(null));

            // 清除预览区
            _filePreviewService?.ClearPreview();

            // 优先检查库信息
            Library currentLib = _getCurrentLibrary?.Invoke();
            if (currentLib != null)
            {
                // (库信息显示已迁移至消息订阅)
                _messageBus.Publish(new LibrarySelectedMessage(currentLib));

                // 库模式下也应该清空消息中的Item（或发送库Item，暂时保持为了null）
                _messageBus.Publish(new FileSelectionChangedMessage(null));
                return;
            }


            // 显示当前文件夹信息
            try
            {
                string currentPath = _getCurrentPath();
                if (!string.IsNullOrEmpty(currentPath) && System.IO.Directory.Exists(currentPath))
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

                    // 通过消息发送
                    _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { item }));

                    // (底部信息栏更新已由消息订阅驱动)
                }
            }
            catch
            {
                // Ignore
            }
        }

        private void CalculateFolderSizeImmediately(string folderPath)
        {
            if (_folderSizeCts != null)
            {
                _folderSizeCts.Cancel();
                _folderSizeCts = null;
            }

            _folderSizeCts = new System.Threading.CancellationTokenSource();
            var token = _folderSizeCts.Token;

            _mainWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (token.IsCancellationRequested) return;

                var currentFiles = _getCurrentFiles();
                if (currentFiles == null) return;

                var item = currentFiles.FirstOrDefault(f => f.Path == folderPath);
                if (item != null && (string.IsNullOrEmpty(item.Size) || item.Size == "-" || item.Size == "计算?中..."))
                {
                    item.Size = "计算中...";
                    _ = _fileListService.CalculateFolderSizeAsync(item, token);
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }
    }
}

