using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.IO;
using YiboFile.Handlers;
using HandlerMouseEventHandler = YiboFile.Handlers.MouseEventHandler;
using YiboFile.Services;
using YiboFile.Services.FileNotes;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Navigation;
using YiboFile.Services.Search;
using YiboFile.Services.Tabs;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Settings;
// using YiboFile.Services.TagTrain; // Phase 2
// using TagTrain.UI; // Phase 2
using System.Windows.Media;
using YiboFile.Services.Core;
using YiboFile.Services.Config;
using YiboFile.ViewModels.Messaging.Messages;


namespace YiboFile
{
    public partial class MainWindow
    {
        internal void CloseOverlays()
        {
            if (SettingsOverlay != null && SettingsOverlay.Visibility == Visibility.Visible)
            {
                _settingsOverlayController?.Hide();
            }
            if (AboutOverlay != null && AboutOverlay.Visibility == Visibility.Visible)
            {
                AboutOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private Services.Tabs.TabService GetActiveTabService()
        {
            return GetActivePaneId() == Services.Navigation.PaneId.Second ? _secondTabService : _tabService;
        }

        internal Services.Navigation.PaneId GetActivePaneId()
        {
            // 优先检查 ViewModel 状态，因为点击侧边栏会导致列表失去焦点，使 IsSecondPaneFocused 变得不可靠
            if (_viewModel?.ActivePane != null)
            {
                // 使用属性判断而非引用判断，更稳健
                return _viewModel.ActivePane.IsSecondary ? Services.Navigation.PaneId.Second : Services.Navigation.PaneId.Main;
            }
            // 降级使用 LayoutModule/UI 状态
            return (IsDualListMode && IsSecondPaneFocused) ? Services.Navigation.PaneId.Second : Services.Navigation.PaneId.Main;
        }








        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // 如果是在全屏覆盖层打开的情况下点击标题栏空白处，关闭覆盖层
            if (SettingsOverlay != null && SettingsOverlay.Visibility == Visibility.Visible)
            {
                _settingsOverlayController?.Hide();
            }
            if (AboutOverlay != null && AboutOverlay.Visibility == Visibility.Visible)
            {
                AboutOverlay.Visibility = Visibility.Collapsed;
            }

            // 双击最大化/还原
            if (e.ClickCount == 2 && e.ChangedButton == MouseButton.Left)
            {
                if (WindowState == WindowState.Maximized)
                    WindowState = WindowState.Normal;
                else
                    WindowState = WindowState.Maximized;
                return;
            }

            // 支持通过拖动标题栏移动窗口
            if (e.ChangedButton == MouseButton.Left)
            {
                try { this.DragMove(); } catch { }
            }
        }



        // Mouse interaction for Sidebar is now handled by MouseEventHandler via WindowOrchestrator wiring

        internal void ShowSelectedFileProperties()
        {
            var (browser, path, library) = GetActiveContext();
            var item = browser?.FilesSelectedItem as FileSystemItem;

            // 目标路径：优先选中项，否则当前文件夹
            string targetPath = null;
            if (item != null && !string.IsNullOrEmpty(item.Path))
            {
                targetPath = item.Path;
            }
            else if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !ProtocolManager.IsVirtual(path))
            {
                // 注意：只有物理路径才支持文件夹属性
                targetPath = path;
            }

            if (!string.IsNullOrEmpty(targetPath))
            {
                // 如果是虚拟路径（如 zip 内部），可能无法显示系统属性，给予提示或处理
                if (ProtocolManager.IsVirtual(targetPath))
                {
                    // 暂时不支持压缩包内文件的系统属性
                    MessageBox.Show($"暂不支持查看此类型的系统属性：\n{targetPath}", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                Services.Core.ShellNative.ShowFileProperties(targetPath);
            }
        }



        // Grid Column Header Click is now handled by ColumnInteractionHandler

        // ==================== Existing but separate ====================

        private void FileBrowser_FilesSizeChanged(SizeChangedEventArgs e)
        {
            _columnService?.AdjustListViewColumnWidths(FileBrowser);
        }

        private void FileBrowser_GridSplitterDragDelta(DragDeltaEventArgs e)
        {
            if (ColLeft != null)
            {
                double newWidth = ColLeft.Width.Value + e.HorizontalChange;
                if (newWidth < 150) newWidth = 150; // Minimum width
                ColLeft.Width = new GridLength(newWidth);
            }
        }



        // Helpers for MenuEventHandler





        internal void Back_Click_Logic()
        {
            if (_navigationService != null && _navigationService.CanNavigateBack)
            {
                _navigationService.NavigateBack();
            }
        }

        private void SetClipboardDataObjectWithRetry(System.Windows.DataObject data)
        {
            const int MaxRetries = 10;    // 从50减少到10
            const int DelayMs = 50;        // 从100ms减少到50ms

            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    System.Windows.Clipboard.SetDataObject(data, true);
                    return;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    // CLIPBRD_E_CANT_OPEN = 0x800401D0
                    const uint CLIPBRD_E_CANT_OPEN = 0x800401D0;
                    if ((uint)ex.ErrorCode != CLIPBRD_E_CANT_OPEN)
                    {
                        throw;
                    }
                    if (i == MaxRetries - 1)
                    {
                        DialogService.Warning("剪贴板被占用，请稍后再试。", owner: this);
                        return;
                    }
                    System.Threading.Thread.Sleep(DelayMs);
                }
            }
        }
        #region 遗留点击事件处理器 (桥接到 ViewModel Command)

        internal void ManageLibraries_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.NewLibraryCommand?.Execute(null);
        internal void Copy_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.CopyCommand?.Execute(null);
        internal void Paste_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.PasteCommand?.Execute(null);
        internal void Cut_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.CutCommand?.Execute(null);
        internal void Delete_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.DeleteCommand?.Execute(null);
        internal void Rename_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.RenameCommand?.Execute(null);
        internal void ShowProperties_Click(object sender, RoutedEventArgs e) => _viewModel?.ActivePane?.PropertiesCommand?.Execute(null);

        #endregion
    }
}

