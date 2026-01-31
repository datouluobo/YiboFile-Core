using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Services;
using YiboFile.Services.Navigation;
using YiboFile.Services.FileNotes;

namespace YiboFile
{
    /// <summary>
    /// MainWindow 的库管理功能
    /// </summary>
    public partial class MainWindow
    {
        #region 库功能

        /// <summary>
        /// 加载所有库
        /// </summary>
        internal void LoadLibraries()
        {
            _libraryService.LoadLibraries();
        }

        /// <summary>
        /// 加载库文件
        /// </summary>
        internal void LoadLibraryFiles(Library library)
        {
            try
            {
                _currentFiles.Clear();
                _currentPath = null; // 标记当前在库模式下
                if (FileBrowser != null)
                {
                    FileBrowser.NavUpEnabled = false;
                    // 隐藏搜索状态
                    FileBrowser.SetSearchStatus(false);
                    // 更新属性按钮可见性
                    UpdatePropertiesButtonVisibility();
                }

                // 使用库服务加载文件
                _libraryService.LoadLibraryFiles(library,
                    (path) => DatabaseManager.GetFolderSize(path),
                    (bytes) => _fileListService.FormatFileSize(bytes));
            }
            catch (Exception ex)
            {
                DialogService.Error($"加载库文件失败: {ex.Message}", owner: this);
            }
        }

        #endregion

        #region 库列表事件处理

        /// <summary>
        /// 库列表鼠标按下事件 - 处理中键和Ctrl+左键
        /// </summary>
        private void LibrariesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox == null) return;

            var clickType = NavigationCoordinator.GetClickType(e);
            if (clickType == ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var hitResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext is Library library)
                {
                    e.Handled = true;
                    _navigationCoordinator.HandleLibraryNavigation(library, clickType);
                    return;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        /// <summary>
        /// 库列表选择变化事件 - 处理左键点击
        /// </summary>
        private void LibrariesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                // 使用统一导航协调器处理库导航（左键点击）
                _navigationCoordinator.HandleLibraryNavigation(selectedLibrary, ClickType.LeftClick);
            }
            else
            {
                _currentLibrary = null;
                if (_configService != null)
                {
                    _configService.Config.LastLibraryId = 0;
                    _configService.SaveCurrentConfig();
                }
                _currentFiles.Clear();
                if (FileBrowser != null)
                {
                    _viewModel?.PrimaryPane?.FileList?.UpdateFiles(new List<FileSystemItem>());
                    FileBrowser.AddressText = "";
                }

                // 清除所有库的高亮
                _navigationService.ClearItemHighlights();
            }
        }



        /// <summary>
        /// 库列表上下文菜单打开事件
        /// </summary>
        private void LibrariesListBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // 根据是否有选中项来启用/禁用菜单项
            bool hasSelection = LibrariesListBox.SelectedItem != null;

            if (LibraryContextMenu != null)
            {
                var renameItem = LibraryContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "LibraryRenameMenuItem");
                var deleteItem = LibraryContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "LibraryDeleteMenuItem");
                var manageItem = LibraryContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "LibraryManageMenuItem");
                var openItem = LibraryContextMenu.Items.OfType<MenuItem>().FirstOrDefault(m => m.Name == "LibraryOpenInExplorerMenuItem");

                if (renameItem != null) renameItem.IsEnabled = hasSelection;
                if (deleteItem != null) deleteItem.IsEnabled = hasSelection;
                if (manageItem != null) manageItem.IsEnabled = hasSelection;
                if (openItem != null) openItem.IsEnabled = hasSelection;
            }
        }

        #endregion

        #region 库导入导出逻辑

        /// <summary>
        /// 导入库逻辑
        /// </summary>
        private void ImportLibrary_Click_Logic()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _libraryService.ImportLibrary(dialog.SelectedPath);
                    _libraryService.LoadLibraries();
                }
            }
        }

        /// <summary>
        /// 导出库逻辑
        /// </summary>
        private void ExportLibrary_Click_Logic()
        {
            DialogService.Info("导出库功能待实现", owner: this);
        }

        #endregion

        #region 库显示逻辑

        // 菜单事件桥接方法 - 已迁移到 MenuEventHandler
        private void AddLibrary_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.AddLibrary_Click(sender, e);
        private void ManageLibraries_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.ManageLibraries_Click(sender, e);

        // 库管理事件桥接方法 - 已迁移到 MenuEventHandler
        private void LibraryRename_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryRename_Click(sender, e);
        private void LibraryDelete_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryDelete_Click(sender, e);
        private void LibraryManage_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryManage_Click(sender, e);
        private void LibraryOpenInExplorer_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryOpenInExplorer_Click(sender, e);
        private void LibraryRefresh_Click(object sender, RoutedEventArgs e) => _menuEventHandler?.LibraryRefresh_Click(sender, e);

        /// <summary>
        /// 显示合并的库文件（所有路径的文件合并显示）
        /// </summary>
        private void ShowMergedLibraryFiles(List<FileSystemItem> items, Library library)
        {
            if (library == null) return;

            _currentFiles.Clear();
            _currentFiles.AddRange(items ?? new List<FileSystemItem>());
            // 应用排序
            if (_columnService != null)
            {
                _currentFiles = _columnService.SortFiles(_currentFiles);
            }
            // 确保UI控件存在
            if (FileBrowser != null)
            {
                // FileBrowser.FilesItemsSource = null; // Do not break binding
                // FileBrowser.FilesItemsSource = _currentFiles; // Do not break binding
                _viewModel?.PrimaryPane?.FileList?.UpdateFiles(_currentFiles);
                // FileBrowser.FilesList?.Items.Refresh(); // Binding handles this
                // 隐藏搜索状态
                FileBrowser.SetSearchStatus(false);
            }

            // 高亮当前库（作为匹配当前库）
            HighlightMatchingLibrary(library);

            // 如果文件列表为空，显示空状态提示；否则隐藏
            if (_currentFiles.Count == 0)
            {
                ShowEmptyStateMessage($"库 \"{library.Name}\" 中没有文件或文件夹");
            }
            else
            {
                HideEmptyStateMessage();
            }

            // 更新地址栏显示库名称
            if (FileBrowser != null)
            {
                FileBrowser.AddressText = library.Name;
                // 允许在库标签页中进行搜索，不设置为只读
                FileBrowser.SetLibraryBreadcrumb(library.Name);
            }
            // 取消之前的文件夹大小计算任务
            _folderSizeCalculationService?.Cancel();

            // 异步加载标签和备注（延迟加载，避免阻塞UI）
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // 批量加载标签和备注（限制并发，减少到2个避免CPU占用过高）
                    var semaphore = new System.Threading.SemaphoreSlim(2, 2); // 最多2个并发查询
                    var tasks = _currentFiles.Select(async item =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            // Tag加载已移除 - Phase 2将重新实现
                            item.Tags = "";

                            var notes = FileNotesService.GetFileNotes(item.Path);
                            if (notes != null && notes.Length > 0)
                            {
                                var firstLine = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                                item.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                            }
                            else
                            {
                                item.Notes = "";
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }).ToList();

                    try
                    {
                        System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
                    }
                    catch { }

                    // 批量更新UI（减少刷新次数）
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                        collectionView?.Refresh();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                catch { }
            });

            // 异步计算文件夹大小（使用服务方法，限制数量和延迟，避免资源消耗过大）
            var dirsToCalculate = _currentFiles.Where(f => f.IsDirectory).ToList();
            // 只计算前5个文件夹，大幅减少资源消耗
            int maxCalculations = Math.Min(5, dirsToCalculate.Count);

            // 立即计算前5个文件夹
            for (int i = 0; i < maxCalculations; i++)
            {
                var dir = dirsToCalculate[i];
                var path = dir.Path;
                var currentDelay = i * 1000; // 每个任务延迟1秒，避免同时启动

                System.Threading.Tasks.Task.Run(async () =>
                {
                    try
                    {
                        // 延迟启动，避免同时启动太多任务
                        if (currentDelay > 0)
                        {
                            await System.Threading.Tasks.Task.Delay(currentDelay);
                        }

                        // 使用服务方法计算文件夹大小
                        await _folderSizeCalculationService.CalculateAndUpdateFolderSizeAsync(path);

                        // 更新UI
                        _ = Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var item = _currentFiles.FirstOrDefault(f => f.Path == path);
                            if (item != null)
                            {
                                var cachedSize = DatabaseManager.GetFolderSize(path);
                                if (cachedSize.HasValue)
                                {
                                    item.Size = _fileListService.FormatFileSize(cachedSize.Value);
                                    // 使用低优先级批量更新，减少UI刷新频率
                                    var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                                    collectionView?.Refresh();
                                }
                            }
                        }), System.Windows.Threading.DispatcherPriority.SystemIdle);
                    }
                    catch { }
                });
            }

            // 剩余文件夹通过服务的批量计算方法异步处理
            if (dirsToCalculate.Count > maxCalculations)
            {
                var remainingPaths = dirsToCalculate.Skip(maxCalculations).Select(d => d.Path).ToArray();
                _folderSizeCalculationService?.CalculateSubfolderSizesBatchAsync(remainingPaths);
            }
        }

        #endregion
    }
}

