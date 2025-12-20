using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OoiMRR.Services;
using OoiMRR.Services.Navigation;

namespace OoiMRR
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
            // 使用信号量防止重复加载
            // 等待200ms以允许上一个加载任务完成，避免标签页切换时文件列表为空
            if (!_loadFilesSemaphore.Wait(200))
            {
                System.Diagnostics.Debug.WriteLine("LoadLibraryFiles: 已有加载任务在进行（等待200ms后仍未完成），跳过此次调用");
                return;
            }

            try
            {
                // 设置加载标志
                _isLoadingFiles = true;

                _currentFiles.Clear();
                _currentPath = null; // 标记当前在库模式下
                if (FileBrowser != null) FileBrowser.NavUpEnabled = false;

                // 使用库服务加载文件
                _libraryService.LoadLibraryFiles(library,
                    (path) => DatabaseManager.GetFolderSize(path),
                    (bytes) => _fileListService.FormatFileSize(bytes));
            }
            catch (Exception ex)
            {
                // 确保释放锁
                _isLoadingFiles = false;
                _loadFilesSemaphore.Release();
                MessageBox.Show($"加载库文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (clickType == NavigationCoordinator.ClickType.LeftClick) return; // 左键由SelectionChanged处理

            var hitResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listBox)
            {
                if (current is ListBoxItem item && item.DataContext is Library library)
                {
                    e.Handled = true;
                    var updatedLibrary = DatabaseManager.GetLibrary(library.Id);
                    if (updatedLibrary != null)
                    {
                        _navigationCoordinator.HandleLibraryNavigation(updatedLibrary, clickType);
                    }
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
                // 重新从数据库加载库信息，确保路径信息是最新的
                var updatedLibrary = DatabaseManager.GetLibrary(selectedLibrary.Id);
                if (updatedLibrary != null)
                {
                    // 使用统一导航协调器处理库导航（左键点击）
                    _navigationCoordinator.HandleLibraryNavigation(updatedLibrary, NavigationCoordinator.ClickType.LeftClick);

                    // 高亮当前选中的库（作为匹配当前库）- 在加载文件后执行，确保库列表已更新
                    HighlightMatchingLibrary(updatedLibrary);
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
                        FileBrowser.FilesItemsSource = null;
                    if (FileBrowser != null)
                        FileBrowser.AddressText = "";
                }
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
                    FileBrowser.FilesItemsSource = null;
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
            MessageBox.Show("导出库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
}
