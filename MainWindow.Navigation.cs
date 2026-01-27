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
            if (_currentLibrary != null)
            {
                // 库模式：刷新库文件
                LoadLibraryFiles(_currentLibrary);
            }
            else if (!string.IsNullOrEmpty(_currentPath) && (Directory.Exists(_currentPath) || ProtocolManager.IsVirtual(_currentPath)))
            {
                // 路径模式或虚拟路径：加载目录
                LoadCurrentDirectory();
            }
            else
            {
                // 如果是库模式但没有当前库，尝试恢复最后选中的库
                if (NavLibraryContent != null && NavLibraryContent.Visibility == Visibility.Visible)
                {
                    if (_configService?.Config.LastLibraryId > 0)
                    {
                        var lastLibrary = _libraryService.GetLibrary(_configService.Config.LastLibraryId);
                        if (lastLibrary != null)
                        {
                            _currentLibrary = lastLibrary;
                            // 使用辅助方法确保选中状态正确显示
                            _uiHelperService?.EnsureSelectedItemVisible(LibrariesListBox, lastLibrary);
                            LoadLibraryFiles(lastLibrary);
                            return;
                        }
                    }

                    // 如果在库模式但没有选中库，且无法恢复上一次的库，则显示库概览视图
                    _currentPath = "lib://";
                    LoadCurrentDirectory();
                    return;
                }

                // 其他模式：清空列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                HideEmptyStateMessage();
            }
        }

        /// <summary>
        /// 异步加载当前目录
        /// </summary>
        private async Task LoadCurrentDirectoryAsync()
        {
            if (_viewModel?.FileList == null) return;

            try
            {
                // 更新 UI 状态
                if (FileBrowser != null)
                {
                    FileBrowser.AddressText = _currentPath;
                    FileBrowser.IsAddressReadOnly = false;
                    FileBrowser.UpdateBreadcrumb(_currentPath);
                    FileBrowser.SetSearchStatus(false);
                }

                // 高亮匹配项
                HighlightMatchingItems(_currentPath);

                if (_currentPath == "lib://" && FileBrowser != null)
                {
                    FileBrowser.GetFileListControl()?.SetViewMode("Thumbnail");
                }

                // MVVM 迁移: 委托给 FileListViewModel 加载
                await _viewModel.FileList.LoadPathAsync(_currentPath);

                // 更新空状态无需显示
                HideEmptyStateMessage();

                // 触发选择状态更新
                _selectionEventHandler?.HandleNoSelection();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoadCurrentDirectoryAsync] Error: {ex.Message}");
                // ShowEmptyStateMessage($"加载失败：\n{ex.Message}");
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

        internal void NavigateToPath(string path)
        {
            // MVVM 迁移: 使用 NavigationModule 解析路径
            if (_viewModel?.Navigation != null)
            {
                path = _viewModel.Navigation.ResolvePath(path);
            }

            bool isVirtualPath = ProtocolManager.IsVirtual(path);
            if (!isVirtualPath && !Directory.Exists(path)) return;

            // MVVM 迁移: 将标签页选择/创建逻辑委托给 TabsModule
            _viewModel?.Tabs?.NavigateTo(
                path,
                onReuseCurrent: () => _viewModel?.Navigation?.NavigateTo(path),
                onReuseSecond: () => SecondFileBrowser_PathChanged(this, path)
            );
        }

        private void UpdatePropertiesButtonVisibility()
        {
            if (FileBrowser != null)
            {
                bool visible = true;
                if (_currentLibrary != null) visible = false;
                else if (!string.IsNullOrEmpty(_currentPath))
                {
                    if (_currentPath.StartsWith("search:", StringComparison.OrdinalIgnoreCase) ||
                        ProtocolManager.IsVirtual(_currentPath))
                    {
                        visible = false;
                    }
                }
                else if (_currentPath == null) // Empty path/home
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
                    items = await _fileListService.LoadFileSystemItemsAsync(
                        _currentPath,
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
                                    FileBrowser.FilesItemsSource = _currentFiles;
                                }
                                catch (ArgumentException)
                                {
                                    // 尝试重建集合以规避可能的 CollectionView 内部错误
                                    var freshList = new List<FileSystemItem>(items);
                                    FileBrowser.FilesItemsSource = freshList;
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
                        FileBrowser.FilesItemsSource = _currentFiles;
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
                    if (FileBrowser != null)
                    {
                        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser.FilesItemsSource);
                        view?.Refresh();
                    }
                    if (SecondFileBrowser != null)
                    {
                        var secondView = System.Windows.Data.CollectionViewSource.GetDefaultView(SecondFileBrowser.FilesItemsSource);
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

