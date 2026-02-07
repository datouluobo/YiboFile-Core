using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Services.Navigation;
using YiboFile.ViewModels;
using YiboFile.Services.FileNotes;
using YiboFile.Services.Config;


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
        internal void LoadLibraryFiles(Library library, PaneId targetPane = PaneId.Main)
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
                    (bytes) => _fileListService.FormatFileSize(bytes),
                    targetPane);
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
            if (_isInternalUpdate) return; // 内部更新触发的事件，直接跳过

            if (LibrariesListBox.SelectedItem is Library selectedLibrary)
            {
                // 如果当前已经是这个库，不再重复加载
                if (selectedLibrary == _currentLibrary) return;

                // 使用统一导航协调器处理库导航（左键点击）
                // [FIX] 显式传递目标面板，以便在副栏聚焦时使用副栏逻辑（包含禁用加载的逻辑）
                _navigationCoordinator.HandleLibraryNavigation(selectedLibrary, ClickType.LeftClick, IsSecondPaneFocused ? PaneId.Second : PaneId.Main);
            }
            else
            {
                _currentLibrary = null;
                // Reset last library in config
                ConfigurationService.Instance.Set(c => c.LastLibraryId, 0);
                ConfigurationService.Instance.SaveNow();


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
            if (NavigationPanelControl?.LibraryContextMenuControl is ContextMenu cm)
            {
                var selectedLibrary = LibrariesListBox.SelectedItem as Library;
                bool hasSelection = selectedLibrary != null;

                SetLibraryMenuItemVisibility(cm, "LibraryRefreshItem", !hasSelection);
                SetLibraryMenuItemAvailability(cm, "LibraryOpenInExplorerItem", hasSelection);
                SetLibraryMenuItemAvailability(cm, "LibraryRenameItem", hasSelection);
                SetLibraryMenuItemAvailability(cm, "LibraryDeleteItem", hasSelection);
                SetLibraryMenuItemAvailability(cm, "LibraryManageItem", true); // 库管理总是可用
            }
        }

        private void SetLibraryMenuItemVisibility(ContextMenu cm, string name, bool visible)
        {
            var item = cm.Items.OfType<MenuItem>().FirstOrDefault(x => x.Name == name);
            if (item != null) item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SetLibraryMenuItemAvailability(ContextMenu cm, string name, bool enabled)
        {
            var item = cm.Items.OfType<MenuItem>().FirstOrDefault(x => x.Name == name);
            if (item != null) item.IsEnabled = enabled;
        }

        /// <summary>
        /// 库右键菜单点击分发
        /// </summary>
        private void LibraryContextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                switch (menuItem.Name)
                {
                    case "LibraryRefreshItem":
                        LibraryRefresh_Click(sender, e);
                        break;
                    case "LibraryOpenInExplorerItem":
                        LibraryOpenInExplorer_Click(sender, e);
                        break;
                    case "LibraryRenameItem":
                        LibraryRename_Click(sender, e);
                        break;
                    case "LibraryDeleteItem":
                        LibraryDelete_Click(sender, e);
                        break;
                    case "LibraryManageItem":
                        LibraryManage_Click(sender, e);
                        break;
                }
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

        // [临时保留] 库管理功能的事件处理方法
        // TODO: 这些功能后续需要迁移到 LibraryManagementViewModel 中的 Command

        private void LibraryRefresh_Click(object sender, RoutedEventArgs e)
        {
            _libraryService?.LoadLibraries();
            if (_currentLibrary != null) LoadLibraryFiles(_currentLibrary);
        }

        private void LibraryOpenInExplorer_Click(object sender, RoutedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library lib && lib.Paths != null && lib.Paths.Count > 0)
            {
                System.Diagnostics.Process.Start("explorer.exe", lib.Paths[0]);
            }
        }

        private void LibraryRename_Click(object sender, RoutedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library lib)
            {
                var dialog = new YiboFile.Controls.Dialogs.InputDialog("重命名库", "请输入新名称:", lib.Name);
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                {
                    // TODO: 实现库重命名功能（LibraryService需要增加RenameLibrary方法）
                    DialogService.Info("库重命名功能待实现", owner: this);
                }
            }
        }

        private void LibraryDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LibrariesListBox.SelectedItem is Library lib)
            {
                if (DialogService.Ask($"确定要删除库 \"{lib.Name}\" 吗？", "确认删除", this))
                {
                    _libraryService?.DeleteLibrary(lib.Id, lib.Name);
                    LoadLibraries();
                }
            }
        }

        private void LibraryManage_Click(object sender, RoutedEventArgs e)
        {
            DialogService.Info("库管理功能待完善", owner: this);
        }


        /// <summary>
        /// 显示合并的库文件（所有路径的文件合并显示）
        /// </summary>
        private void ShowMergedLibraryFiles(List<FileSystemItem> items, Library library, PaneId targetPane = PaneId.Main)
        {
            if (library == null) return;

            // 1. 处理副面板更新 (独立逻辑，不使用 _currentFiles)
            if (targetPane == PaneId.Second)
            {
                if (SecondFileBrowser != null && _viewModel?.SecondaryPane?.FileList != null)
                {
                    // 即使是空列表也需要更新，以清除旧显示
                    var filesToUpdate = items ?? new List<FileSystemItem>();

                    // 如果需要排序，且列表非空
                    if (filesToUpdate.Count > 0 && _columnService != null)
                    {
                        // 注意：这里仍在 UI 线程排序，但在副面板IsLoadingDisabled=true情况下，lines通常为0，不会卡顿
                        // 如果IsLoadingDisabled=false，建议后续优化为后台排序
                        filesToUpdate = _columnService.SortFiles(filesToUpdate);
                    }


                    _viewModel.SecondaryPane.FileList.UpdateFiles(filesToUpdate);
                    // Update VM state to trigger UI binding
                    _viewModel.SecondaryPane.NavigationMode = "Library";
                    _viewModel.SecondaryPane.CurrentLibrary = library;
                    _viewModel.SecondaryPane.CurrentPath = $"lib://{library.Name}";

                    // Legacy UI updates removed (handled by binding now)

                    if (filesToUpdate.Count == 0)
                    {
                        SecondFileBrowser.ShowEmptyState($"库 \"{library.Name}\" 中没有文件或文件夹");
                    }
                    else
                    {
                        SecondFileBrowser.HideEmptyState();
                    }

                    // 高亮副面板的库列表项（如果有）
                    // TODO: 副面板如有独立库列表，需在此通过 Binding 或 Service 更新选中状态
                }
                return;
            }

            // 2. 处理主面板更新 (使用 _currentFiles 保持兼容性)
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
                _viewModel.PrimaryPane.FileList.UpdateFiles(_currentFiles);
                // Update VM state just in case it wasn't updated by navigation (e.g. direct load)
                if (_viewModel.PrimaryPane.NavigationMode != "Library" || _viewModel.PrimaryPane.CurrentLibrary != library)
                {
                    _viewModel.PrimaryPane.NavigationMode = "Library";
                    _viewModel.PrimaryPane.CurrentLibrary = library;
                    _viewModel.PrimaryPane.CurrentPath = $"lib://{library.Name}";
                }

                FileBrowser.SetSearchStatus(false);
                // FileBrowser.AddressText assignment legacy removed
                // FileBrowser.SetLibraryBreadcrumb legacy removed

                if (_currentFiles.Count == 0)
                {
                    FileBrowser.ShowEmptyState($"库 \"{library.Name}\" 中没有文件或文件夹");
                }
                else
                {
                    FileBrowser.HideEmptyState();
                }
            }

            // 高亮当前库（作为匹配当前库）
            try
            {
                _isInternalUpdate = true;
                HighlightMatchingLibrary(library);
            }
            finally
            {
                _isInternalUpdate = false;
            }
        }
        #endregion
    }
}

