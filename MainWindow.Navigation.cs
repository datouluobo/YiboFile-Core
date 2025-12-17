using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using OoiMRR.Services;
using OoiMRR.Services.Navigation;
using OoiMRR.Models.UI;

namespace OoiMRR
{
    public partial class MainWindow
    {
        internal void SwitchNavigationMode(string mode)
        {
            // 使用 NavigationModeService 处理导航模式切换
            if (_navigationModeService != null)
            {
                _navigationModeService.SwitchNavigationMode(mode);
            }
        }
        
        private void NavPathBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchNavigationMode("Path");
        }
        
        private void NavLibraryBtn_Click(object sender, RoutedEventArgs e)
        {
            SwitchNavigationMode("Library");
        }
        
        private void NavTagBtn_Click(object sender, RoutedEventArgs e)
        {
            // 只有在 TagTrain 可用时才切换到标签模式
            if (App.IsTagTrainAvailable)
            {
                SwitchNavigationMode("Tag");
            }
            else
            {
                MessageBox.Show("TagTrain 不可用，无法使用标签功能。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        internal void RefreshFileList()
        {
            // 检查是否是搜索标签页
            var activeTab = _tabService.ActiveTab;
            if (activeTab != null && activeTab.Path != null && activeTab.Path.StartsWith("search://"))
            {
                CheckAndRefreshSearchTab(activeTab.Path);
                return;
            }
            
            // 根据当前导航模式刷新文件列表
            if (NavTagContent != null && NavTagContent.Visibility == Visibility.Visible)
            {
                // 标签模式：使用TagTrain面板，如果有选中的标签，显示该标签的文件；否则清空文件列表
                if (_currentTagFilter != null)
                {
                    FilterByTag(_currentTagFilter);
                }
                else
                {
                    // 没有选中标签，清空文件列表
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = null;
                    }
                    HideEmptyStateMessage();
                }
            }
            else if (_currentLibrary != null)
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
                // 路径页使用文件浏览控件
                if (FileBrowser != null)
                {
                    FileBrowser.AddressText = _currentPath;
                    FileBrowser.IsAddressReadOnly = false;
                    FileBrowser.UpdateBreadcrumb(_currentPath);
                }

                // 检查目录是否存在
                if (string.IsNullOrEmpty(_currentPath) || !Directory.Exists(_currentPath))
                {
                    throw new DirectoryNotFoundException($"路径不存在: {_currentPath}");
                }

                // 使用 FileListService 异步加载文件
                await LoadFilesAsync();

                // 设置文件系统监控
                _fileSystemWatcherService.SetupFileWatcher(_currentPath);
                
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
                
                System.Diagnostics.Debug.WriteLine(errorMessage);
                // 不弹窗，只显示空状态
                // MessageBox.Show(errorMessage, "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                
                // 清空文件列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                ShowEmptyStateMessage($"无法访问此路径：\n{_currentPath}\n(访问被拒绝)");
            }
            catch (DirectoryNotFoundException ex)
            {
                MessageBox.Show($"路径不存在: {_currentPath}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                // 清空文件列表
                _currentFiles.Clear();
                if (FileBrowser != null)
                    FileBrowser.FilesItemsSource = null;
                ShowEmptyStateMessage($"路径不存在：\n{_currentPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法加载目录: {_currentPath}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (Directory.Exists(path))
            {
                // 更新或创建标签页
                var activeTab = _tabService.ActiveTab;
                if (activeTab != null && activeTab.Type == Services.Tabs.TabType.Path && activeTab.Path == path)
                {
                    // 已经是当前标签页的路径，直接导航
                    NavigateToPathInternal(path);
                }
                else
                {
                    // 查找是否已有该路径的标签页
                    var existingTab = _tabService.FindTabByPath(path);
                    if (existingTab != null)
                    {
                        SwitchToTab(existingTab);
                    }
                    else
                    {
                        // 更新当前标签页路径或创建新标签页
                        if (activeTab != null && activeTab.Type == Services.Tabs.TabType.Path)
                        {
                            // 如果当前标签页是路径类型，更新它
                            activeTab.Path = path;
                            _tabService?.UpdateTabTitle(activeTab, path);
                            NavigateToPathInternal(path);
                        }
                        else
                        {
                            // 创建新标签页
                            CreateTab(path);
                        }
                    }
                }
            }
        }

        private void NavigateToPathInternal(string path)
        {
            if (!Directory.Exists(path)) return;

            var isDriveRoot = string.Equals(
                System.IO.Path.GetPathRoot(path)?.TrimEnd('\\'),
                path.TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);

            // 如果进入的是文件夹，计算并更新其大小缓存（驱动器根目录跳过，避免耗时）
            if (!isDriveRoot)
            {
                 Task.Run(() => _folderSizeCalculationService.CalculateAndUpdateFolderSizeAsync(path));
            }

            _currentPath = path;
            
            // 确保导航服务状态同步
            if (_navigationService != null)
            {
                _navigationService.CurrentPath = path;
            }

            LoadCurrentDirectory();
        }

        /// <summary>
        /// 异步加载文件列表
        /// </summary>
        private async Task LoadFilesAsync()
        {
            // 使用信号量防止重复加载
            // 使用 0 ms 等待，如果获取不到立即跳过，避免堆积请求
            if (!_loadFilesSemaphore.Wait(0))
            {
                System.Diagnostics.Debug.WriteLine("LoadFiles: 已有加载任务在进行，跳过此次调用");
                return;
            }
            
            CancellationTokenSource cts = null;

            try
            {
                // 设置加载标志
                _isLoadingFiles = true;

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
                        OrderTagNames,
                        cts.Token);
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadFilesAsync: 操作已超时或被取消 ({_currentPath})");
                    // 超时后，items 保持为 null
                }

                if (items == null)
                {
                     // 如果超时或返回空，尝试至少显示个空列表，避免一直 Loading
                     items = new List<FileSystemItem>();
                }
                else
                {
                    // 在 UI 线程更新
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            _currentFiles.Clear();
                            _currentFiles.AddRange(items);
                            
                            // 应用排序
                            if (_columnService != null)
                            {
                                try 
                                {
                                    _currentFiles = _columnService.SortFiles(_currentFiles);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"排序失败: {ex.Message}");
                                    // 排序失败不应该导致整个加载失败，忽略排序错误
                                }
                            }

                            if (FileBrowser != null)
                            {
                                try
                                {
                                    FileBrowser.FilesItemsSource = _currentFiles;
                                }
                                catch (ArgumentException argEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"FileBrowser 绑定失败 (ArgumentException): {argEx.Message}");
                                    System.Diagnostics.Debug.WriteLine($"Stack: {argEx.StackTrace}");
                                    // 尝试重建集合以规避可能的 CollectionView 内部错误
                                    var freshList = new List<FileSystemItem>(items);
                                    FileBrowser.FilesItemsSource = freshList; 
                                }
                            }
                        }
                        catch (Exception innerEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"UI 更新失败: {innerEx.Message}");
                        }
                        finally
                        {
                            // 确保 UI 状态重置
                            _isLoadingFiles = false;
                        }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadFilesAsync 失败: {ex.Message}");
            }
            finally
            {
                // 确保释放资源
                _isLoadingFiles = false;
                try
                {
                    _loadFilesSemaphore.Release();
                }
                catch (SemaphoreFullException)
                {
                    // Ignore if already released (unlikely)
                }
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
                        
                    // 重置加载标志并释放信号量
                    _isLoadingFiles = false;
                    _loadFilesSemaphore.Release();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"处理文件加载完成事件失败: {ex.Message}");
                    _isLoadingFiles = false;
                    _loadFilesSemaphore.Release();
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"处理文件夹大小计算完成事件失败: {ex.Message}");
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"处理元数据加载完成事件失败: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// FileSystemWatcherService 文件系统变化事件处理
        /// </summary>
        private void OnFileSystemWatcherServiceFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 记录文件系统变化事件（用于调试）
            System.Diagnostics.Debug.WriteLine($"[MainWindow] 文件系统变化: {e.ChangeType} - {e.Name}");
            
            // 事件已由 FileSystemWatcherService 处理防抖，这里可以记录日志或做其他处理
            // 防抖后的刷新请求会通过 RefreshRequested 事件触发
        }

        /// <summary>
        /// FileSystemWatcherService 刷新请求事件处理
        /// </summary>
        private void OnFileSystemWatcherServiceRefreshRequested(object sender, EventArgs e)
        {
            // 检查是否正在加载，避免重复加载
            if (!_isLoadingFiles)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 收到刷新请求，开始刷新文件列表...");
                RefreshFileList();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] 正在加载中，跳过刷新请求");
            }
        }

        /// <summary>
        /// FileListService 错误发生事件处理
        /// </summary>
        private void OnFileListServiceErrorOccurred(object sender, string errorMessage)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    // 清空文件列表
                    _currentFiles.Clear();
                    if (FileBrowser != null)
                    {
                        FileBrowser.FilesItemsSource = null;
                    }

                    // 显示错误消息
                    if (errorMessage.Contains("无权限访问"))
                    {
                        MessageBox.Show(errorMessage, "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        ShowEmptyStateMessage($"无法访问此路径：\n{_currentPath}");
                    }
                    else if (errorMessage.Contains("路径不存在"))
                    {
                        MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        ShowEmptyStateMessage($"路径不存在：\n{_currentPath}");
                    }
                    else
                    {
                        MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        ShowEmptyStateMessage($"加载失败：\n{errorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"处理错误事件失败: {ex.Message}");
                }
                finally
                {
                    // 释放加载锁定
                    _isLoadingFiles = false;
                    _loadFilesSemaphore.Release();
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }
        
        #endregion
    }
}
