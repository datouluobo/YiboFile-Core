using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using OoiMRR.Models.UI;
using OoiMRR.Services;
using OoiMRR.Services.FileNotes; // Corrected namespace
using OoiMRR.Services.FileOperations;
// using OoiMRR.Services.TagTrain; // Phase 2

namespace OoiMRR.Handlers
{
    /// <summary>
    /// 处理文件列表的选择事件
    /// </summary>
    public class SelectionEventHandler
    {
        private readonly MainWindow _mainWindow;
        private readonly OoiMRR.Services.Preview.PreviewService _filePreviewService;
        private readonly FileNotesUIHandler _fileNotesUIHandler;
        // private readonly TagTrainEventHandler _tagTrainEventHandler; // Phase 2
        private readonly Action<FileSystemItem> _updateFileInfoPanel;
        private readonly Action _clearPreviewAndInfo;
        private readonly Action<object> _renderPredictionResults; // Phase 2: was List<TagTrain.Services.TagPredictionResult>
        private readonly OoiMRR.Services.FileList.FileListService _fileListService;
        private readonly Func<List<FileSystemItem>> _getCurrentFiles;
        private readonly Func<string> _getCurrentPath;
        private System.Threading.CancellationTokenSource _folderSizeCts;

        public SelectionEventHandler(
            MainWindow mainWindow,
            OoiMRR.Services.Preview.PreviewService filePreviewService,
            FileNotesUIHandler fileNotesUIHandler,
            object tagTrainEventHandler, // Phase 2参数，暂时改为object避免编译错误
            Action<FileSystemItem> updateFileInfoPanel,
            Action clearPreviewAndInfo,
            Action<object> renderPredictionResults, // Phase 2: was List<TagTrain.Services.TagPredictionResult>
            OoiMRR.Services.FileList.FileListService fileListService,
            Func<List<FileSystemItem>> getCurrentFiles,
            Func<string> getCurrentPath)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _filePreviewService = filePreviewService ?? throw new ArgumentNullException(nameof(filePreviewService));
            _fileNotesUIHandler = fileNotesUIHandler ?? throw new ArgumentNullException(nameof(fileNotesUIHandler));
            // _tagTrainEventHandler = tagTrainEventHandler; // Phase 2
            _updateFileInfoPanel = updateFileInfoPanel ?? throw new ArgumentNullException(nameof(updateFileInfoPanel));
            _clearPreviewAndInfo = clearPreviewAndInfo ?? throw new ArgumentNullException(nameof(clearPreviewAndInfo));
            _renderPredictionResults = renderPredictionResults ?? throw new ArgumentNullException(nameof(renderPredictionResults));
            _fileListService = fileListService ?? throw new ArgumentNullException(nameof(_fileListService));
            _getCurrentFiles = getCurrentFiles ?? throw new ArgumentNullException(nameof(getCurrentFiles));
            _getCurrentPath = getCurrentPath ?? throw new ArgumentNullException(nameof(getCurrentPath));
        }

        public void HandleSelectionChanged(System.Collections.IList selectedItems)
        {
            if (selectedItems == null) return;

            if (selectedItems.Count > 0)
            {
                var selectedItem = selectedItems[0] as FileSystemItem;
                if (selectedItem != null)
                {
                    // 2. 更新右侧文件信息面板（仅触发加载）
                    _updateFileInfoPanel(selectedItem);

                    // 3. 加载预览
                    _filePreviewService?.LoadFilePreview(selectedItem);

                    // 4. 加载备注
                    _fileNotesUIHandler?.LoadFileNotes(selectedItem);

                    // 5. 检查剪贴板状态（如果是剪切，调整透明度）
                    try
                    {
                        bool isCut = FileClipboardManager.IsCutOperation && FileClipboardManager.GetCopiedPaths().Contains(selectedItem.Path);
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

                    // AI标签预测已移除 - Phase 2将重新实现
                    // bool isTagNavMode = _mainWindow.NavTagContent != null && _mainWindow.NavTagContent.Visibility == Visibility.Visible;
                    // bool isTagEditMode = _tagTrainEventHandler != null && _tagTrainEventHandler.CurrentMode == TagTrainEventHandler.TagClickMode.Edit;
                    // ...

                    // 7. 文件夹大小计算
                    if (selectedItem.IsDirectory)
                    {
                        // 检查大小是否已计算（Size为空、"-"、"计算中..."或null表示未计算）
                        if (string.IsNullOrEmpty(selectedItem.Size) ||
                            selectedItem.Size == "-" ||
                            selectedItem.Size == "计算中...")
                        {
                            // 立即计算该文件夹的大小
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
            // 清除预览区
            _filePreviewService?.ClearPreview();
            // 清除预测结果
            _renderPredictionResults(null); // Phase 2: was new List<TagTrain.Services.TagPredictionResult>()
            // 清除备注
            try { _fileNotesUIHandler?.LoadFileNotes(null); } catch { }

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
                        Size = "-", // 将在 ShowDirectoryInfo 中计算
                        Tags = "" // 文件夹没有标签? 或者需要获取标签? 目前暂留空
                    };
                    _updateFileInfoPanel(item);
                }
                else
                {
                    // 如果路径无效（例如搜索结果页面），则清除信息面板
                    _clearPreviewAndInfo();
                }
            }
            catch
            {
                _clearPreviewAndInfo();
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
                if (item != null && (string.IsNullOrEmpty(item.Size) || item.Size == "-" || item.Size == "计算中..."))
                {
                    item.Size = "计算中...";
                    _ = _fileListService.CalculateFolderSizeAsync(item, token);
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }
    }
}
