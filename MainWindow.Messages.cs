using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using YiboFile.Models;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Navigation;

namespace YiboFile
{
    public partial class MainWindow
    {
        internal void InitializeMessageSubscriptions()
        {
            if (_messageBus == null) return;

            // 1. 库文件加载处理
            _messageBus.Subscribe<LibraryFilesLoadedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() => HandleLibraryFilesLoaded(msg));
            });

            // 2. 文件系统变更处理
            _messageBus.Subscribe<FileSystemChangedMessage>(msg =>
            {
                // 可以视情况决定是否自动刷新或显示通知
                // 目前逻辑主要由 FileListService 内部触发
            });

            // 3. 文件夹大小计算完成
            _messageBus.Subscribe<FolderSizeCalculatedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    var dummyItem = new FileSystemItem { Path = msg.Path, Size = msg.FormattedSize, SizeBytes = msg.Size };
                    OnFileListServiceFolderSizeCalculated(null, dummyItem);
                });
            });

            // 4. 元数据增强
            _messageBus.Subscribe<MetadataEnrichedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() => OnFileListServiceMetadataEnriched(null, new List<FileSystemItem> { msg.Item }));
            });

            // 5. 库高亮请求
            _messageBus.Subscribe<LibrarySelectedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() => HighlightMatchingLibrary(msg.Library));
            });

            // 6. 导航请求 (已废弃 - 改由各 PaneViewModel 独立响应其归属的导航消息)
            /*
            _messageBus.Subscribe<NavigateToPathMessage>(msg =>
            {
                this.Dispatcher.Invoke(() => NavigateToPath(msg.Path, PaneId.Main));
            });
            */

            // 7. 库导航请求 - 已废弃，改由 PaneViewModel 监听 LibrarySelectedMessage 直接处理
            /*
            _messageBus.Subscribe<NavigateToLibraryMessage>(msg =>
            {
                this.Dispatcher.Invoke(() => NavigateToLibrary(msg.Library, msg.Pane));
            });
            */

            // 8. 焦点面板变更 (同步逻辑焦点)
            _messageBus.Subscribe<FocusedPaneChangedMessage>(msg =>
            {
                this.Dispatcher.Invoke(() =>
                {
                    if (msg.IsSecondPaneFocused)
                    {
                        SecondFileBrowser?.Focus();
                        SecondFileBrowser?.FilesList?.Focus();
                    }
                    else
                    {
                        FileBrowser?.Focus();
                        FileBrowser?.FilesList?.Focus();
                    }
                });
            });
        }

        private void HandleLibraryFilesLoaded(LibraryFilesLoadedMessage msg)
        {
            if (msg.IsEmpty)
            {
                if (msg.TargetPane == PaneId.Second)
                {
                    if (SecondFileBrowser != null)
                    {
                        _viewModel?.SecondaryPane?.FileList?.Files?.Clear();
                        SecondFileBrowser.AddressText = msg.Library.Name + " (无位置)";
                        SecondFileBrowser.SetLibraryBreadcrumb(msg.Library.Name);
                        SecondFileBrowser.ShowEmptyState($"库 \"{msg.Library.Name}\" 中没有文件或文件夹");
                    }
                }
                else
                {
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                    {
                        _viewModel?.PrimaryPane?.FileList?.Files?.Clear();
                        FileBrowser.AddressText = msg.Library.Name + " (无位置)";
                        FileBrowser.SetLibraryBreadcrumb(msg.Library.Name);
                        FileBrowser.ShowEmptyState($"库 \"{msg.Library.Name}\" 中没有文件或文件夹");
                    }
                }
            }
            else
            {
                ShowMergedLibraryFiles(msg.Files, msg.Library, msg.TargetPane);
            }
        }
    }
}
