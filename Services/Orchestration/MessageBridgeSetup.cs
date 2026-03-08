using System;
using System.Collections.Generic;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Config;
using YiboFile.Services.Navigation;
using YiboFile.Services.FileList;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Core.Error;
using YiboFile.Models.Navigation;

namespace YiboFile.Services.Orchestration
{
    /// <summary>
    /// 消息桥接配置器
    /// 负责将 Service 层事件转换为 MessageBus 消息，以及订阅 MessageBus 消息驱动 UI 行为
    /// 从 WindowOrchestrator 中拆分，降低单文件复杂度
    /// </summary>
    internal class MessageBridgeSetup
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageBus _messageBus;

        public MessageBridgeSetup(IServiceProvider serviceProvider, IMessageBus messageBus)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
        }

        /// <summary>
        /// 配置 Service 事件 → MessageBus 消息桥接
        /// 将传统 C# event 转换为 MessageBus 消息发布
        /// </summary>
        public void SetupServiceBridges(
            NavigationService navigationService,
            FileListService fileListService,
            FileListService secondFileListService,
            FileSystemWatcherService fileSystemWatcherService,
            Favorite.FavoriteService favoriteService)
        {
            // 1. NavigationService -> MessageBus
            // 已在服务内部实现 IMessageBus 发布，无需此处桥接

            // 2. FileListService & LibraryService -> MessageBus
            // 已在服务内部实现 IMessageBus 发布，无需此处桥接

            // 3. FileSystemWatcherService -> MessageBus
            // 已在服务内部实现 IMessageBus 发布，无需此处桥接

            // 4. LibraryService -> MessageBus (模块已部分桥接，此处补充汇总)
            // LibraryModule 已经处理了 LibraryFilesLoaded 和 LibrariesLoaded

            // 5. FavoriteService & QuickAccessService -> MessageBus
            // 已在服务内部实现 IMessageBus 发布，无需此处桥接
        }

        /// <summary>
        /// 配置 MessageBus 消息 → UI 行为桥接
        /// 订阅消息并执行对应的 UI 操作
        /// </summary>
        public Preview.PreviewService SetupMessageSubscriptions(
            MainWindow window,
            NavigationCoordinator navigationCoordinator,
            FileOperationService fileOperationService,
            Services.FileInfo.FileInfoService fileInfoService,
            Services.FileInfo.FileInfoService secondFileInfoService,
            ViewModels.MainWindowViewModel viewModel)
        {
            // 1. 信息面板更新消息
            _messageBus.Subscribe<ShowFileInfoMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (msg.Pane == PaneId.Second)
                        secondFileInfoService?.ShowFileInfo(msg.Item);
                    else
                        fileInfoService?.ShowFileInfo(msg.Item);
                });
            });

            _messageBus.Subscribe<ShowLibraryInfoMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (msg.Pane == PaneId.Second)
                        secondFileInfoService?.ShowLibraryInfo(msg.Library);
                    else
                        fileInfoService?.ShowLibraryInfo(msg.Library);
                });
            });

            // 2. 预览导航请求
            _messageBus.Subscribe<PreviewNavigationRequestMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    var isSecond = window.IsDualPaneMode && window.IsSecondPaneFocused;
                    var activeBrowser = isSecond ? window.SecondFileBrowser : window.FileBrowser;
                    if (activeBrowser != null && activeBrowser.FilesList != null)
                    {
                        var list = activeBrowser.FilesList;
                        if (list.Items.Count == 0) return;

                        int newIndex = list.SelectedIndex == -1 ? 0 : (msg.IsNext ? list.SelectedIndex + 1 : list.SelectedIndex - 1);

                        if (newIndex >= 0 && newIndex < list.Items.Count)
                        {
                            list.SelectedIndex = newIndex;
                            list.ScrollIntoView(list.Items[newIndex]);
                        }
                    }
                });
            });

            // 4. 打开文件请求
            _messageBus.Subscribe<OpenFileRequestMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (!string.IsNullOrEmpty(msg.FilePath))
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = msg.FilePath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            DialogService.Error($"无法打开文件: {ex.Message}", owner: window);
                        }
                    }
                });
            });

            // 5. 初始化预览服务 (MVVM 桥接)
            var previewService = new Preview.PreviewService(
                _messageBus,
                window.Dispatcher
            );

            // 6. 设置文件操作上下文提供者
            fileOperationService.SetContextProvider(() => window.GetActiveFileOperationContext());

            // 7. 全局错误消息订阅
            _messageBus.Subscribe<ErrorOccurredMessage>(e =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (e.Severity == ErrorSeverity.Critical)
                    {
                        YiboFile.DialogService.Error(e.Message, "严重错误", window);
                    }
                    else
                    {
                        var notificationType = e.Severity switch
                        {
                            ErrorSeverity.Warning => YiboFile.Controls.NotificationType.Warning,
                            ErrorSeverity.Error => YiboFile.Controls.NotificationType.Error,
                            _ => YiboFile.Controls.NotificationType.Info
                        };
                        Services.Core.NotificationService.Show(e.Message, notificationType);
                    }
                });
            });

            // 8. 收藏路径未找到
            _messageBus.Subscribe<FavoritePathNotFoundMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    DialogService.Warning($"收藏的路径不存在: {msg.Favorite.Path}", "错误", owner: window);
                });
            });

            // 9. 文件操作完成通知
            _messageBus.Subscribe<FileOperationCompleteMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    if (!msg.Success && !string.IsNullOrEmpty(msg.ErrorMessage))
                    {
                        Services.Core.NotificationService.Show(msg.ErrorMessage, Controls.NotificationType.Error);
                    }
                });
            });

            // 10. 文件操作状态消息 → 状态栏
            _messageBus.Subscribe<FileOperationStatusMessage>(msg =>
            {
                window.Dispatcher.Invoke(() =>
                {
                    Services.Core.NotificationService.Show(msg.StatusText, Controls.NotificationType.Info);
                });
            });

            return previewService;
        }
    }
}
