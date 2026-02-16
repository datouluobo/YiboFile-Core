using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controls;
using YiboFile.Interfaces;
using YiboFile.Models;
using YiboFile.Models.Navigation;
using YiboFile.Services;
using YiboFile.Services.Core;
using YiboFile.Services.Navigation;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 文件列表事件处理器（Facade / Coordinator）
    /// 统一协调鼠标和键盘事件处理器，并执行核心文件打开逻辑
    /// </summary>
    public class FileListEventHandler
    {
        private readonly FileBrowserControl _fileBrowser;
        private readonly NavigationCoordinator _navigationCoordinator;
        private readonly IShellWindow _shellWindow;
        private readonly PaneId _paneId;

        // 子处理器
        private readonly FileListMouseHandler _mouseHandler;
        private readonly FileListKeyboardHandler _keyboardHandler;

        public FileListEventHandler(
            FileBrowserControl fileBrowser,
            NavigationCoordinator navigationCoordinator,
            NavigationModeService navigationModeService,
            IShellWindow shellWindow,
            PaneId paneId = PaneId.Main)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _shellWindow = shellWindow ?? throw new ArgumentNullException(nameof(shellWindow));
            _paneId = paneId;

            // 初始化子处理器
            _mouseHandler = new FileListMouseHandler(
                fileBrowser,
                navigationCoordinator,
                navigationModeService,
                shellWindow,
                paneId,
                HandleFileOpen);

            _keyboardHandler = new FileListKeyboardHandler(
                fileBrowser,
                navigationCoordinator,
                shellWindow,
                paneId,
                HandleFileOpen);
        }

        /// <summary>
        /// 初始化事件绑定
        /// </summary>
        public void Initialize(ListView filesList)
        {
            if (filesList == null) return;

            // 路由到鼠标处理器
            filesList.PreviewMouseDoubleClick += _mouseHandler.OnPreviewMouseDoubleClick;
            filesList.MouseDoubleClick += _mouseHandler.OnMouseDoubleClick;
            filesList.PreviewMouseLeftButtonDown += _mouseHandler.OnPreviewMouseLeftButtonDown;
            filesList.PreviewMouseDown += _mouseHandler.OnPreviewMouseDown;
            filesList.MouseLeftButtonUp += _mouseHandler.OnMouseLeftButtonUp;
            filesList.PreviewMouseDoubleClick += _mouseHandler.OnPreviewMouseDoubleClickForBlank;

            // 路由到键盘处理器
            filesList.PreviewKeyDown += _keyboardHandler.OnPreviewKeyDown;
        }

        /// <summary>
        /// 统一处理文件打开逻辑
        /// </summary>
        public void HandleFileOpen(FileSystemItem item)
        {
            if (item == null) return;

            if (ProtocolManager.Parse(item.Path).Type == ProtocolType.Archive)
            {
                MessageBox.Show("暂不支持直接打开压缩包内的文件。\n请先解压后再试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ext = Path.GetExtension(item.Path);
            if (!string.IsNullOrEmpty(ext))
            {
                var extLower = ext.ToLowerInvariant();
                if (extLower == ".zip" || extLower == ".7z" || extLower == ".rar" || extLower == ".tar" || extLower == ".gz")
                {
                    string archiveUrl = $"{ProtocolManager.ZipProtocol}{item.Path}|";
                    _navigationCoordinator.HandlePathNavigation(archiveUrl, NavigationSource.FileList, ClickType.LeftClick, pane: _paneId);
                    return;
                }
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
