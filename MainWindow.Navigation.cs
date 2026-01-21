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
            if (NavPathBtn == null || NavLibraryBtn == null) return;

            NavPathBtn.Tag = mode == "Path" ? "Selected" : null;
            NavLibraryBtn.Tag = mode == "Library" ? "Selected" : null;

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
            else if (!string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath))
            {
                // 路径模式：加载目录
                LoadCurrentDirectory();
            }
            else
            {
                // 如果是库模式但没有当前库，尝试恢复最后选中的库
                if (NavLibraryContent != null && NavLibraryContent.Visibility == Visibility.Visible)
                {
                    if (_configService?.Config.LastLibraryId > 0)
                    {
                        var lastLibrary = DatabaseManager.GetLibrary(_configService.Config.LastLibraryId);
                        if (lastLibrary != null)
                        {
                            _currentLibrary = lastLibrary;
                            // 使用辅助方法确保选中状态正确显示
                            _uiHelperService?.EnsureSelectedItemVisible(LibrariesListBox, lastLibrary);
                            LoadLibraryFiles(lastLibrary);
                            return;
                        }
                    }

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
            try
            {
                // 识别虚拟路径 (宽松检测)
                bool isVirtualPath = false;
                if (!string.IsNullOrEmpty(_currentPath))
                {
                    isVirtualPath = ProtocolManager.IsVirtual(_currentPath);
                }

                if (FileBrowser != null)
                {
                    FileBrowser.AddressText = _currentPath;
                    FileBrowser.IsAddressReadOnly = false;
                    FileBrowser.UpdateBreadcrumb(_currentPath);
                    // 隐藏搜索状态
                    FileBrowser.SetSearchStatus(false);
                }

                // 检查目录是否存在（跳过虚拟路径）
                // 彻底移除 DirectoryNotFoundException 抛出，改为内联提示
                if (!isVirtualPath && !string.IsNullOrEmpty(_currentPath))
                {
                    if (!Directory.Exists(_currentPath))
                    {
                        // 路径不存在：显示空状态，不弹窗
                        _currentFiles.Clear();
                        if (FileBrowser != null)
                            FileBrowser.FilesItemsSource = null;

                        ShowEmptyStateMessage($"路径不存在：\n{_currentPath}");
                        return; // 中止后续加载
                    }
                }

                // 使用 FileListService 异步加载文件
                // 对于虚拟路径，LoadFilesAsync 可能会返回空或由 Search 逻辑单独处理
                if (!isVirtualPath)
                {
                    await LoadFilesAsync();

                    // 设置文件系统监控
                    _fileSystemWatcherService.SetupFileWatcher(_currentPath);
                }
                else
                {
                    // Handle Virtual Paths (like Archives)
                    var protocol = ProtocolManager.Parse(_currentPath);
                    if (protocol.Type == ProtocolType.Archive)
                    {
                        var items = await _archiveService.GetArchiveContentAsync(protocol.TargetPath, protocol.ExtraData);

                        // Update UI on background priority
                        await Dispatcher.BeginInvoke(new Action(() =>
                       {
                           try
                           {
                               _currentFiles.Clear();
                               _currentFiles.AddRange(items);

                               if (FileBrowser != null)
                               {
                                   FileBrowser.FilesItemsSource = _currentFiles;
                                   // Trigger updates if necessary
                                   _selectionEventHandler?.HandleNoSelection();

                                   // Archives are read-only-ish, hide search status
                                   FileBrowser.SetSearchStatus(false);
                               }
                           }
                           catch (Exception) { }
                       }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                    else if (protocol.Type == ProtocolType.Tag)
                    {
                        // Handle Tag Protocol
                        int.TryParse(protocol.TargetPath, out int tagId);
                        if (tagId > 0 && _tagService != null)
                        {
                            var files = await Task.Run(() => _tagService.GetFilesByTag(tagId));

                            // Update UI on background priority
                            await Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    _currentFiles.Clear();

                                    foreach (var file in files)
                                    {
                                        if (File.Exists(file))
                                        {
                                            _currentFiles.Add(new FileSystemItem
                                            {
                                                Path = file,
                                                Name = System.IO.Path.GetFileName(file),
                                                IsDirectory = false,
                                                ModifiedDateTime = File.GetLastWriteTime(file),
                                                SizeBytes = new FileInfo(file).Length
                                            });
                                        }
                                        else if (Directory.Exists(file))
                                        {
                                            _currentFiles.Add(new FileSystemItem
                                            {
                                                Path = file,
                                                Name = System.IO.Path.GetFileName(file),
                                                IsDirectory = true,
                                                ModifiedDateTime = Directory.GetLastWriteTime(file)
                                            });
                                        }
                                    }

                                    if (FileBrowser != null)
                                    {
                                        FileBrowser.FilesItemsSource = _currentFiles;
                                        _selectionEventHandler?.HandleNoSelection();
                                        FileBrowser.SetSearchStatus(false);
                                    }
                                }
                                catch (Exception) { }
                            }), System.Windows.Threading.DispatcherPriority.Background);

                            // Trigger metadata enrichment to populate Tags and Notes
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    using var cts = new CancellationTokenSource();
                                    await _fileListService.EnrichMetadataAsync(_currentFiles, null, cts.Token);
                                }
                                catch { }
                            });
                        }
                    }
                }

                // 高亮匹配当前路径的列表项
                HighlightMatchingItems(_currentPath);

                // 隐藏空状态提示
                HideEmptyStateMessage();
            }
            catch (UnauthorizedAccessException ex)
            {
                // 友好的错误消息
                string errorMessage = $"无法访问路径: {_currentPath}\n";
                if (ex.Message.Contains("Access to the path") && ex.Message.Contains("is denied"))
                {
                    errorMessage += "访问被拒绝。请检查文件夹权限。";
                }
                else
                {
                    errorMessage += ex.Message;
                }
                // 不弹窗，只显示空状态
                // MessageBox.Show(errorMessage, "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);

                // 清空文件列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                ShowEmptyStateMessage($"无法访问此路径：\n{_currentPath}\n(访问被拒绝)");
            }
            catch (DirectoryNotFoundException)
            {
                // MessageBox.Show($"路径不存在: {_currentPath}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                System.Diagnostics.Debug.WriteLine($"[LoadCurrentDirectoryAsync] 路径不存在: {_currentPath}");
                // 清空文件列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                ShowEmptyStateMessage($"路径不存在：\n{_currentPath}");
            }
            catch (Exception ex)
            {
                // MessageBox.Show($"无法加载目录: {_currentPath}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine($"[LoadCurrentDirectoryAsync] 无法加载目录: {_currentPath} Error: {ex.Message}");
                // 清空文件列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                ShowEmptyStateMessage($"加载失败：\n{ex.Message}");
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
            // 识别虚拟路径
            bool isVirtualPath = false;
            if (!string.IsNullOrEmpty(path))
            {
                isVirtualPath = ProtocolManager.IsVirtual(path);
            }

            // [Archive Support] Check if path is an archive file
            if (!isVirtualPath && !Directory.Exists(path) && File.Exists(path))
            {
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".zip" || ext == ".7z" || ext == ".rar" || ext == ".tar" || ext == ".gz")
                {
                    // Redirect to archive schema
                    NavigateToPath($"zip://{path}|");
                    return;
                }
            }

            if (!isVirtualPath && !Directory.Exists(path)) return;

            // 双列表模式：如果焦点在副列表，则在副列表导航
            if (_isDualListMode && _isSecondPaneFocused && _secondTabService != null)
            {
                var secondActiveTab = _secondTabService.ActiveTab;
                // 规则1：同类型标签页直接更新
                if (secondActiveTab != null && secondActiveTab.Type == Services.Tabs.TabType.Path)
                {
                    secondActiveTab.Path = path;
                    _secondTabService.UpdateTabTitle(secondActiveTab, path);
                    SecondFileBrowser_PathChanged(this, path);
                    return;
                }

                // 规则2：查找最近访问的相同Path标签页
                var secondRecentTab = _secondTabService.FindRecentTab(t => t.Type == Services.Tabs.TabType.Path && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10));
                if (secondRecentTab != null)
                {
                    _secondTabService.SwitchToTab(secondRecentTab);
                }
                else
                {
                    // CreateTab(path) is better because it checks for focus again, which is redundant but safe.
                    // But to be explicit and consistent with logic above:
                    _secondTabService.CreatePathTab(path);
                }
                return;
            }

            var activeTab = _tabService.ActiveTab;
            // 规则1：同类型标签页直接更新
            if (activeTab != null && activeTab.Type == Services.Tabs.TabType.Path)
            {
                // 先更新标题，确保标签页显示同步
                _tabService?.UpdateActiveTabPath(path);
                activeTab.Path = path;
                NavigateToPathInternal(path);
                return;
            }

            // 规则2：查找最近访问的相同Path标签页（使用配置时间窗口）
            var recentTab = _tabService.FindRecentTab(t => t.Type == Services.Tabs.TabType.Path && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10));

            if (recentTab != null)
            {
                // 找到了最近访问的标签页，切换到它
                _tabService.SwitchToTab(recentTab);
            }
            else
            {
                // 没有找到或不够新鲜，创建新标签页
                CreateTab(path);
            }
        }

        private void NavigateToPathInternal(string path)
        {
            // 识别虚拟路径
            bool isVirtualPath = false;
            if (!string.IsNullOrEmpty(path))
            {
                isVirtualPath = ProtocolManager.IsVirtual(path);
            }

            if (!isVirtualPath && !Directory.Exists(path)) return;

            // ALWAYS sync navigation service state FIRST for all valid paths (including virtual)
            _currentPath = path;
            if (_navigationService != null)
            {
                _navigationService.CurrentPath = path;
            }

            var isDriveRoot = false;
            if (!isVirtualPath)
            {
                isDriveRoot = string.Equals(
                    System.IO.Path.GetPathRoot(path)?.TrimEnd('\\'),
                    path.TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase);
            }

            // 如果进入的是文件夹，计算并更新其大小缓存（驱动器根目录跳过，避免耗时）
            // 虚拟也不计算
            if (!isVirtualPath && !isDriveRoot)
            {
                Task.Run(() => _folderSizeCalculationService.CalculateAndUpdateFolderSizeAsync(path));
            }

            LoadCurrentDirectory();

            // 更新属性按钮可见性
            UpdatePropertiesButtonVisibility();
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
                try
                {
                    var existingItem = _currentFiles.FirstOrDefault(f => f.Path == item.Path);
                    if (existingItem != null)
                    {
                        existingItem.Size = item.Size;
                        var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                        collectionView?.Refresh();
                    }
                }
                catch (Exception)
                {
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
                    // 更新现有项的元素数据（标签和备注）
                    foreach (var enrichedItem in items)
                    {
                        var existingItem = _currentFiles.FirstOrDefault(f => f.Path == enrichedItem.Path);
                        if (existingItem != null)
                        {
                            existingItem.Tags = enrichedItem.Tags;
                            existingItem.Notes = enrichedItem.Notes;
                        }
                    }

                    var collectionView = System.Windows.Data.CollectionViewSource.GetDefaultView(FileBrowser?.FilesItemsSource);
                    collectionView?.Refresh();
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

