using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Services;
using YiboFile.Services.Navigation;
using YiboFile.Models.UI;

using YiboFile.Services.Core;
using YiboFile.Services.Config;


namespace YiboFile
{
    public partial class MainWindow
    {
        internal void SwitchNavigationMode(string mode)
        {
            // 更新按钮选中状态
            UpdateNavigationButtonsState(mode);



            // 使用 NavigationModeService 处理导航模式切换
            if (_navigationModeService != null)
            {
                _navigationModeService.SwitchNavigationMode(mode);
            }
        }

        private void UpdateNavigationButtonsState(string mode)
        {
            NavigationRail?.SetActiveMode(mode);
        }

        private void NavPathBtn_Click(object sender, RoutedEventArgs e)
        {
            CloseOverlays();
            SwitchNavigationMode("Path");
        }

        private void NavLibraryBtn_Click(object sender, RoutedEventArgs e)
        {
            CloseOverlays();
            SwitchNavigationMode("Library");
        }



        internal void RefreshFileList()
        {
            // 检查是否是搜索标签页
            var activeTab = _tabService.ActiveTab;
            if (activeTab != null && !string.IsNullOrEmpty(activeTab.Path))
            {
                var path = activeTab.Path.Trim();
                if (path.StartsWith("search://", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
                {
                    CheckAndRefreshSearchTab(activeTab.Path);
                    return;
                }
            }

            // 根据当前导航模式刷新文件列表
            // 标签模式逻辑已移除

            // 检查是否在库模式
            // 检查是否在库模式
            var currentLibrary = _viewModel?.PrimaryPane?.CurrentLibrary;
            var currentPath = _viewModel?.PrimaryPane?.CurrentPath;

            if (currentLibrary != null)
            {
                // 库模式：刷新库文件
                LoadLibraryFiles(currentLibrary);
            }
            else if (!string.IsNullOrEmpty(currentPath))
            {
                // 路径模式或虚拟路径：加载目录
                LoadCurrentDirectory();
            }
            else
            {
                // 如果是库模式但没有当前库，尝试恢复最后选中的库
                if (NavLibraryContent != null && NavLibraryContent.Visibility == Visibility.Visible)
                {
                    if (ConfigurationService.Instance.Config.LastLibraryId > 0)
                    {
                        var lastLibrary = _libraryService.GetLibrary(ConfigurationService.Instance.Config.LastLibraryId);

                        if (lastLibrary != null)
                        {
                            // Delegate to ViewModel
                            _viewModel?.PrimaryPane?.NavigateTo(lastLibrary, loadData: true);
                            return;
                        }
                    }

                    // 如果在库模式但没有选中库，获取第一个库作为默认
                    var firstLibrary = _libraryService.LoadLibraries().FirstOrDefault();
                    if (firstLibrary != null)
                    {
                        _viewModel?.PrimaryPane?.NavigateTo(firstLibrary, loadData: true);
                        return;
                    }

                    // 如果连一个库都没有
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                        _viewModel?.PrimaryPane?.FileList?.Files?.Clear();
                    return;
                }

                // 其他模式：清空列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    _viewModel?.PrimaryPane?.FileList?.Files?.Clear();
                HideEmptyStateMessage();
            }
        }

        /// <summary>
        /// 异步加载当前目录
        /// </summary>
        /// <summary>
        /// 异步加载当前目录
        /// </summary>
        private async Task LoadCurrentDirectoryAsync()
        {
            if (_viewModel?.PrimaryPane?.FileList == null) return;

            // 获取当前 VM 路径
            var currentPath = _viewModel.PrimaryPane.CurrentPath;

            try
            {
                // 更新 UI 状态
                if (FileBrowser != null)
                {
                    FileBrowser.AddressText = currentPath;
                    FileBrowser.IsAddressReadOnly = false;
                    FileBrowser.UpdateBreadcrumb(currentPath);
                    FileBrowser.SetSearchStatus(false);
                }

                // 高亮匹配项
                try
                {
                    _isInternalUpdate = true;
                    HighlightMatchingItems(currentPath);
                }
                finally
                {
                    _isInternalUpdate = false;
                }

                // 更新导航按钮状态
                if (FileBrowser != null)
                {
                    string dirName = null;
                    try { dirName = System.IO.Path.GetDirectoryName(currentPath); } catch { }
                    FileBrowser.NavUpEnabled = !string.IsNullOrEmpty(currentPath) && !ProtocolManager.IsVirtual(currentPath) && !string.IsNullOrEmpty(dirName);
                }

                // MVVM 迁移: 委托给 FileListViewModel 加载
                // 注意: 此时 ViewModel.PrimaryPane.CurrentPath 应该已经是 target path
                await _viewModel.PrimaryPane.FileList.LoadPathAsync(currentPath);

                // 更新空状态无需显示
                HideEmptyStateMessage();

                // 触发选择状态更新
                _selectionEventHandler?.HandleNoSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadCurrentDirectoryAsync] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载当前目录（同步包装器，保持向后兼容）
        /// </summary>
        internal void LoadCurrentDirectory()
        {
            // 使用异步方法，但不等待，避免阻塞UI
            _ = LoadCurrentDirectoryAsync();
        }

        /// <summary>
        /// 高亮匹配当前库的列表项
        /// </summary>
        internal void HighlightMatchingLibrary(Library currentLibrary)
        {
            _navigationService.HighlightMatchingLibrary(currentLibrary);
        }

        /// <summary>
        /// 高亮匹配当前路径的列表项（驱动器、快速访问、收藏）
        /// </summary>
        private void HighlightMatchingItems(string currentPath)
        {
            _navigationService.HighlightMatchingItems(currentPath);
        }

        /// <summary>
        /// 清除所有列表项的高亮状态
        /// </summary>
        private void ClearItemHighlights()
        {
            _navigationService.ClearItemHighlights();
        }

        private void SetupFileWatcher(string path)
        {
            // 使用 FileSystemWatcherService 进行文件系统监控
            _fileSystemWatcherService?.SetupFileWatcher(path);
        }

        internal async void NavigateToPath(string path)
        {
            var currentLibrary = _viewModel?.PrimaryPane.CurrentLibrary;
            var currentPath = _viewModel?.PrimaryPane.CurrentPath;

            // Fix: Prevent recursive loop when updating AddressText in Library mode
            // If we are already in the library, and the path matches the library name (or lib uri), ignore it.
            if (currentLibrary != null && !string.IsNullOrEmpty(path))
            {
                if (path.Equals(currentLibrary.Name, StringComparison.OrdinalIgnoreCase) ||
                    path.Equals($"lib://{currentLibrary.Name}", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            // MVVM 迁移: 使用 NavigationModule 解析路径
            if (_viewModel?.Navigation != null)
            {
                path = _viewModel.Navigation.ResolvePath(path);
            }

            bool isVirtualPath = ProtocolManager.IsVirtual(path);
            if (!isVirtualPath)
            {
                // Offload IO check to background thread to prevent UI freeze (especially for drives/network)
                bool exists = await Task.Run(() =>
                {
                    try { return Directory.Exists(path); }
                    catch { return false; }
                });
                if (!exists) return;
            }

            // Guard: If already at this path, don't re-navigate
            if (path.Equals(currentPath, StringComparison.OrdinalIgnoreCase))
                return;

            // MVVM 迁移: 将标签页选择/创建逻辑委托给 TabsModule
            _viewModel?.Tabs?.NavigateTo(
                path,
                onReuseCurrent: () =>
                {
                    // Break recursion: Call LoadCurrentDirectory instead of NavigateTo again
                    // Update VM directly
                    if (_viewModel?.PrimaryPane != null)
                        _viewModel.PrimaryPane.CurrentPath = path;

                    LoadCurrentDirectory();
                },
                onReuseSecond: () => SecondFileBrowser_PathChanged(this, path)
            );
        }

        private void UpdatePropertiesButtonVisibility()
        {
            if (FileBrowser != null)
            {
                var currentPath = _viewModel?.PrimaryPane?.CurrentPath;
                var currentLibrary = _viewModel?.PrimaryPane?.CurrentLibrary;

                bool visible = true;
                if (currentLibrary != null) visible = false;
                else if (!string.IsNullOrEmpty(currentPath))
                {
                    if (currentPath.StartsWith("search:", StringComparison.OrdinalIgnoreCase) ||
                        ProtocolManager.IsVirtual(currentPath))
                    {
                        visible = false;
                    }
                }
                else if (currentPath == null) // Empty path/home
                {
                    // visible = true; // or false? Usually empty path means "This PC" or drives? 
                    // If My Computer, properties of "This PC"? 
                    // Usually This PC has properties (System properties). 
                    // But let's keep it visible or hidden?
                    // If drives list is shown, what is "current folder"? It's virtual "MyComputer".
                    // ShellProperties might work on "This PC" (CLSID).
                    // Let's safe default to true, or handle specifically. 
                    // For now, let's assume true unless virtual/search.
                }

                FileBrowser.SetPropertiesButtonVisibility(visible);
            }
        }

        /// <summary>
        /// 异步加载文件列表
        /// </summary>
        private async Task LoadFilesAsync()
        {
            CancellationTokenSource cts = null;

            try
            {
                // 创建超时控制
                cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // 5秒超时

                // 使用 FileListService 异步加载文件
                // 直接使用返回的 items 更新 UI，不再依赖 FilesLoaded 事件
                // 添加 try-catch 捕获可能的超时或取消
                List<FileSystemItem> items = null;
                try
                {
                    var currentPath = _viewModel?.PrimaryPane?.CurrentPath;
                    items = await _fileListService.LoadFileSystemItemsAsync(
                        currentPath,
                        null, // OrderTagNames - Phase 2将重新实现
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // 超时后，items 保持为 null
                }

                if (items == null)
                {
                    // 如果超时或返回空，尝试至少显示个空列表，避免一直 Loading
                    items = new List<FileSystemItem>();
                }
                else
                {
                    // 在后台线程应用排序，避免阻塞 UI
                    if (_columnService != null)
                    {
                        try
                        {
                            items = _columnService.SortFiles(items);
                        }
                        catch (Exception)
                        {
                            // 排序失败不应该导致整个加载失败，忽略排序错误
                        }
                    }

                    // 在 UI 线程更新
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            _currentFiles.Clear();
                            _currentFiles.AddRange(items);

                            if (FileBrowser != null)
                            {
                                try
                                {
                                    _viewModel?.PrimaryPane?.FileList?.UpdateFiles(_currentFiles);
                                }
                                catch (ArgumentException)
                                {
                                    // 尝试重建集合以规避可能的 CollectionView 内部错误
                                    var freshList = new List<FileSystemItem>(items);
                                    _viewModel?.PrimaryPane?.FileList?.UpdateFiles(freshList);
                                }

                                // 主动触发空选状态下的信息面板更新（修复首次进入目录不显示信息的问题）
                                _selectionEventHandler?.HandleNoSelection();
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                cts?.Dispose();
            }
        }

        /// <summary>
        /// 加载文件列表（同步包装器，保持向后兼容）
        /// </summary>
        internal void LoadFiles()
        {
            // 使用异步方法，但不等待，避免阻塞UI
            _ = LoadFilesAsync();
        }

        #region FileListService 事件处理

        /// <summary>
        /// FileListService 文件加载完成事件处理
        /// </summary>
        /// <summary>
        /// FileListService 文件加载完成事件处理
        /// </summary>
        private void OnFileListServiceFilesLoaded(object sender, List<FileSystemItem> items)
        {
            // 文件加载完成后在UI线程更新文件列表
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    _currentFiles.Clear();
                    _currentFiles.AddRange(items);

                    // 应用排序
                    if (_columnService != null)
                    {
                        _currentFiles = _columnService.SortFiles(_currentFiles);
                    }

                    if (FileBrowser != null)
                        _viewModel?.PrimaryPane?.FileList?.UpdateFiles(_currentFiles);
                }
                catch (Exception)
                {
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// FileListService 文件夹大小计算完成事件处理
        /// </summary>
        private void OnFileListServiceFolderSizeCalculated(object sender, FileSystemItem item)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Update main list
                var mainItem = _currentFiles.FirstOrDefault(f => f.Path == item.Path);
                if (mainItem != null)
                {
                    mainItem.Size = item.Size;
                    mainItem.SizeBytes = item.SizeBytes;
                }

                // Update second list
                var secondItem = _secondCurrentFiles.FirstOrDefault(f => f.Path == item.Path);
                if (secondItem != null)
                {
                    secondItem.Size = item.Size;
                    secondItem.SizeBytes = item.SizeBytes;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// FileListService 元数据加载完成事件处理
        /// </summary>
        private void OnFileListServiceMetadataEnriched(object sender, List<FileSystemItem> items)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    foreach (var enrichedItem in items)
                    {
                        // Update Main Files
                        var mainItem = _currentFiles.FirstOrDefault(f => f.Path == enrichedItem.Path);
                        if (mainItem != null)
                        {
                            mainItem.Tags = enrichedItem.Tags;
                            mainItem.TagList = enrichedItem.TagList;
                            mainItem.Notes = enrichedItem.Notes;
                            mainItem.NotifyTagsChanged();
                        }

                        // Update Second Files (Dual List Mode)
                        var secondItem = _secondCurrentFiles.FirstOrDefault(f => f.Path == enrichedItem.Path);
                        if (secondItem != null)
                        {
                            secondItem.Tags = enrichedItem.Tags;
                            secondItem.TagList = enrichedItem.TagList;
                            secondItem.Notes = enrichedItem.Notes;
                            secondItem.NotifyTagsChanged();
                        }
                    }

                    // Refresh both views
                    if (FileBrowser != null && _viewModel?.PrimaryPane?.Files != null)
                    {
                        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_viewModel.PrimaryPane.Files);
                        view?.Refresh();
                    }
                    if (SecondFileBrowser != null && _viewModel?.SecondaryPane?.Files != null)
                    {
                        var secondView = System.Windows.Data.CollectionViewSource.GetDefaultView(_viewModel.SecondaryPane.Files);
                        secondView?.Refresh();
                    }
                }
                catch (Exception)
                {
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// FileSystemWatcherService 文件系统变化事件处理
        /// </summary>
        private void OnFileSystemWatcherServiceFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 记录文件系统变化事件（用于调试）
            // 事件已由 FileSystemWatcherService 处理防抖，这里可以记录日志或做其他处理
            // 防抖后的刷新请求会通过 RefreshRequested 事件触发
        }

        /// <summary>
        /// FileSystemWatcherService 刷新请求事件处理
        /// </summary>
        private void OnFileSystemWatcherServiceRefreshRequested(object sender, EventArgs e)
        {
            RefreshFileList();
        }

        internal async void RefreshFileMetadata()
        {
            if (_currentFiles == null || _currentFiles.Count == 0) return;
            if (_fileListService == null) return;

            var cts = new CancellationTokenSource();
            cts.CancelAfter(5000);

            try
            {
                await _fileListService.EnrichMetadataAsync(_currentFiles, null, cts.Token);
            }
            catch { }
        }




        #endregion
    }
}

