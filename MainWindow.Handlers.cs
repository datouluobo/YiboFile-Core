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

        private void InitializeHandlers()
        {
            // 订阅 TabManager 的关闭覆盖层请求
            if (TabManager != null)
            {
                TabManager.CloseOverlayRequested += (s, e) => CloseOverlays();
            }

            if (SecondTabManager != null)
            {
                SecondTabManager.CloseOverlayRequested += (s, e) => CloseOverlays();
            }

            // [Already moved to Initialization.cs] FileInfoServices and Message Subscriptions

            // 初始化 KeyboardEventHandler (SSOT)
            Func<Services.Tabs.TabService> getActiveTabService = () =>
                (IsDualListMode && IsSecondPaneFocused && _secondTabService != null) ? _secondTabService : _tabService;

            _keyboardEventHandler = new YiboFile.Handlers.KeyboardEventHandler(
                FileBrowser,
                () => GetActiveContext().browser, // NEW: Active browser delegate
                getActiveTabService,
                (tab) => getActiveTabService().RemoveTab(tab),
                (path) => CreateTab(path),
                (tab) => getActiveTabService().SwitchToTab(tab),
                () => _viewModel?.ActivePane?.NewFolderCommand?.Execute(null), // 通过ViewModel Command
                RefreshFileList,
                () => _viewModel?.ActivePane?.CopyCommand?.Execute(null),
                () => _viewModel?.ActivePane?.PasteCommand?.Execute(null),
                () => _viewModel?.ActivePane?.CutCommand?.Execute(null),
                () => _viewModel?.ActivePane?.DeleteCommand?.Execute(null),
                async () => await DeleteSelectedFilesAsync(permanent: true), // Shift+Delete 永久删除
                () => _viewModel?.ActivePane?.RenameCommand?.Execute(null),
                NavigateToPath,
                SwitchNavigationMode,
                () => _currentLibrary != null,
                () => CloseOverlays(), // closeOverlays
                Back_Click_Logic, // navigateBack
                () => Undo_Click(null, null),
                () => Redo_Click(null, null),
                SwitchLayoutModeByIndex,  // 添加布局切换回调
                () => _layoutModule?.IsDualListMode ?? false, // isDualListMode 检查
                () => _layoutModule?.SwitchFocusedPane() // switchDualPaneFocus 回调
            );

            _columnInteractionHandler = new Handlers.ColumnInteractionHandler(this, FileBrowser, _columnService);
            _columnInteractionHandler.Initialize();
            _columnInteractionHandler.HookHeaderThumbs(); // 挂载列头拖拽事件

            // 初始化 MouseEventHandler
            _mouseEventHandler = new Handlers.MouseEventHandler(
                () => WindowMaximize_Click(null, null),
                () => this.DragMove(),
                () => NavigationPanelControl?.QuickAccessListBox,
                _navigationCoordinator,
                fav => _navigationCoordinator.HandleFavoriteNavigation(fav, Services.Navigation.ClickType.LeftClick),
                path => _navigationCoordinator.HandlePathNavigation(path, NavigationSource.QuickAccess, ClickType.LeftClick)
            );

            // 初始化 Second ColumnInteractionHandler
            if (SecondFileBrowser != null)
            {
                _secondColumnInteractionHandler = new Handlers.ColumnInteractionHandler(this, SecondFileBrowser, _columnService);
                _secondColumnInteractionHandler.Initialize();
                _secondColumnInteractionHandler.HookHeaderThumbs();

                // Wire up SecondFileBrowser Tag Click
                SecondFileBrowser.TagClicked += (s, tag) =>
                {
                    if (tag != null && !string.IsNullOrEmpty(tag.Name))
                    {
                        // Navigate in Second Pane
                        _navigationCoordinator.HandlePathNavigation(
                            $"tag://{tag.Name}",
                            NavigationSource.AddressBar,
                            ClickType.LeftClick,
                            pane: YiboFile.Services.Navigation.PaneId.Second
                        );
                    }
                };

                // Wire up SecondFileBrowser Sorting
                SecondFileBrowser.GridViewColumnHeaderClick += SecondGridViewColumnHeader_Click;
            }

            // 初始化 WindowLifecycleHandler
            _windowLifecycleHandler = new Handlers.WindowLifecycleHandler(this, _windowStateManager, _columnService);

            // 初始化 FileOperationHandler
            _fileOperationHandler = new Handlers.FileOperationHandler(this, App.ServiceProvider.GetService<YiboFile.Services.FileOperations.Undo.UndoService>(), _fileOperationService);

            // 初始化 Main FileListEventHandler
            _mainFileListHandler = new Handlers.FileListEventHandler(
                FileBrowser,
                _navigationCoordinator,
                () => _currentLibrary != null, // IsLibraryMode
                mode => SwitchNavigationMode(mode),
                path => NavigateToPath(path),
                () => Back_Click_Logic(),
                col => AutoSizeGridViewColumn(col),
                () => _currentPath,
                () => ShowSelectedFileProperties(),
                (path, force, activate) => CreateTab(path, force, activate) // Main Browser CreateTab
            );
            _mainFileListHandler.Initialize(FileBrowser.FilesList);

            // 初始化 Second FileListEventHandler
            if (SecondFileBrowser != null)
            {
                _secondFileListHandler = new Handlers.FileListEventHandler(
                    SecondFileBrowser,
                    _navigationCoordinator,
                    () => _viewModel?.SecondaryPane?.NavigationMode == "Library", // IsLibraryMode
                    mode => { /* handled elsewhere */ },
                    path => LoadSecondFileBrowserDirectory(path),
                    () => { /* Second Browser Back Logic? */ },
                    col => AutoSizeGridViewColumn(col),
                    () => _viewModel?.SecondaryPane?.CurrentPath,
                    () => ShowSelectedFileProperties(),
                    (path, force, activate) =>
                    {
                        bool shouldActivate = activate ?? ConfigurationService.Instance.Config?.ActivateNewTabOnMiddleClick ?? true;
                        _secondTabService?.CreatePathTab(path, force, false, shouldActivate);
                    },
                    YiboFile.Services.Navigation.PaneId.Second
                );
                _secondFileListHandler.Initialize(SecondFileBrowser.FilesList);
            }

            // Initialize Drag & Drop
            InitializeDragDrop();

            if (AboutPanel != null)
            {
                AboutPanel.CloseRequested += (s, e) =>
                {
                    if (AboutOverlay != null) AboutOverlay.Visibility = Visibility.Collapsed;
                };
            }
        }




        private string ExtractPathFromListBoxItem(ListBox listBox, System.Windows.Point position)
        {
            var hitResult = VisualTreeHelper.HitTest(listBox, position);
            if (hitResult == null) return null;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext != null)
                {
                    var pathProperty = item.DataContext.GetType().GetProperty("Path");
                    if (pathProperty != null)
                    {
                        return pathProperty.GetValue(item.DataContext) as string;
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private Favorite ExtractFavoriteFromListBoxItem(ListBox listBox, System.Windows.Point position)
        {
            var hitResult = VisualTreeHelper.HitTest(listBox, position);
            if (hitResult == null) return null;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext != null)
                {
                    var favoriteProperty = item.DataContext.GetType().GetProperty("Favorite");
                    if (favoriteProperty != null)
                    {
                        return favoriteProperty.GetValue(item.DataContext) as Favorite;
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
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



        private void QuickAccessListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var path = ExtractPathFromListBoxItem(listBox, e.GetPosition(listBox));
            if (!string.IsNullOrEmpty(path))
            {
                _navigationService.LastLeftNavSource = "QuickAccess";
                _navigationCoordinator.HandlePathNavigation(path, NavigationSource.QuickAccess, clickType);
                e.Handled = true;
            }
        }

        private void FolderFavoritesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HandleFavoriteListBoxPreviewMouseDown(sender as ListBox, e, "FolderFavorites");
        }

        private void FileFavoritesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            HandleFavoriteListBoxPreviewMouseDown(sender as ListBox, e, "FileFavorites");
        }

        private void HandleFavoriteListBoxPreviewMouseDown(ListBox listBox, MouseButtonEventArgs e, string sourceName)
        {
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var favorite = ExtractFavoriteFromListBoxItem(listBox, e.GetPosition(listBox));
            if (favorite != null)
            {
                _navigationService.LastLeftNavSource = sourceName;
                _navigationCoordinator.HandleFavoriteNavigation(favorite, clickType);
                e.Handled = true;
            }
        }

        private void ShowSelectedFileProperties()
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



        private DateTime _lastColumnClickTime = DateTime.MinValue;
        private string _lastClickedColumn = null;

        internal void GridSplitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Migrated logic directly here if still needed, or handled by control
            if (ColLeft != null)
            {
                double newWidth = ColLeft.Width.Value + e.HorizontalChange;
                if (newWidth < 150) newWidth = 150; // Minimum width
                ColLeft.Width = new GridLength(newWidth);
            }
        }
        internal void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var header = sender as GridViewColumnHeader;
            if (header == null || FileBrowser == null) return;

            // 防抖：忽略200ms内的重复点击
            var now = DateTime.Now;
            var columnTag = header.Tag?.ToString();
            if ((now - _lastColumnClickTime).TotalMilliseconds < 200 && columnTag == _lastClickedColumn)
            {
                return;
            }
            _lastColumnClickTime = now;
            _lastClickedColumn = columnTag;

            _columnService?.HandleColumnHeaderClick(
                header,
                _currentFiles,
                (sortedFiles) =>
                {
                    _currentFiles = sortedFiles;
                    _viewModel?.PrimaryPane?.FileList?.UpdateFiles(_currentFiles);
                },
                FileBrowser.FilesGrid
            );
        }

        internal void SecondGridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var header = sender as GridViewColumnHeader;
            if (header == null || SecondFileBrowser == null) return;

            // Simple debounce (optional, but good practice)
            // Reusing same variables might be tricky if dual clicking, but explicit click is serial.
            // Let's use local debounce if needed or shared - shared is fine for UI clicks.
            var now = DateTime.Now;
            var columnTag = header.Tag?.ToString();
            if ((now - _lastColumnClickTime).TotalMilliseconds < 200 && columnTag == _lastClickedColumn)
            {
                return;
            }
            _lastColumnClickTime = now;
            _lastClickedColumn = columnTag;

            var currentFiles = _viewModel?.SecondaryPane?.Files;
            if (currentFiles == null) return;
            var fileList = currentFiles.ToList();

            _columnService?.HandleColumnHeaderClick(
                header,
                fileList,
                (sortedFiles) =>
                {
                    _secondCurrentFiles = sortedFiles;
                    _viewModel?.SecondaryPane?.FileList?.UpdateFiles(sortedFiles);
                },
                SecondFileBrowser.FilesGrid
            );
        }

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





        private void Back_Click_Logic()
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

