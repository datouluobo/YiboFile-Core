using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Controls;
using YiboFile.Services.Tabs;
using YiboFile.Services.FileOperations;
using YiboFile.Services.Core;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile
{
    /// <summary>
    /// 布局模式切换功能
    /// </summary>
    public partial class MainWindow
    {
        #region 布局模式枚举和字段

        /// <summary>
        /// 布局模式
        /// </summary>
        private enum LayoutMode
        {
            Focus,  // 专注模式：折叠左右
            Work,   // 工作模式：左导航+文件列表
            Full    // 完整模式：三栏完整
        }

        private LayoutMode _currentLayoutMode = LayoutMode.Full;

        #endregion

        #region 布局模式切换
        internal void SwitchLayoutModeByIndex(int modeIndex)
        {
            string mode = modeIndex switch
            {
                0 => "Focus",
                1 => "Work",
                2 => "Full",
                _ => null
            };

            if (mode != null)
            {
                _layoutModule?.SwitchLayoutMode(mode);
            }
        }

        /// <summary>
        /// 切换布局模式
        /// </summary>
        private void SwitchLayoutMode(LayoutMode mode)
        {
            CloseOverlays(); // Ensure overlays are closed when switching layout

            if (_currentLayoutMode == mode) return;

            _currentLayoutMode = mode;
            _currentLayoutMode = mode;

            // 更新按钮激活状态
            if (NavigationRail != null)
            {
                NavigationRail.FocusModeButton.Tag = mode == LayoutMode.Focus ? "Active" : null;
                NavigationRail.WorkModeButton.Tag = mode == LayoutMode.Work ? "Active" : null;
                NavigationRail.FullModeButton.Tag = mode == LayoutMode.Full ? "Active" : null;
            }

            // 应用布局
            ApplyLayout(mode);

            // 更新标签页边距
            UpdateTabManagerMargin();

            // 保存配置
            if (_configService?.Config != null)
            {
                _configService.Config.LayoutMode = mode.ToString();
                _configService.SaveCurrentConfig();
            }
        }

        /// <summary>
        /// 应用布局（调用现有的 CollapsibleGridSplitter 方法）
        /// </summary>
        private void ApplyLayout(LayoutMode mode)
        {
            switch (mode)
            {
                case LayoutMode.Focus:
                    // 专注模式：折叠左+右
                    EnsureCollapsed(SplitterLeft, true);   // 折叠左侧
                    EnsureCollapsed(SplitterRight, false); // 折叠右侧
                    break;

                case LayoutMode.Work:
                    // 工作模式：展开左，折叠右
                    EnsureExpanded(SplitterLeft, true);    // 展开左侧
                    EnsureCollapsed(SplitterRight, false); // 折叠右侧
                    break;

                case LayoutMode.Full:
                    // 完整模式：展开全部
                    EnsureExpanded(SplitterLeft, true);    // 展开左侧
                    EnsureExpanded(SplitterRight, false);  // 展开右侧
                    break;
            }
        }

        /// <summary>
        /// 确保指定面板已折叠（如果未折叠则折叠）
        /// </summary>
        private void EnsureCollapsed(CollapsibleGridSplitter splitter, bool isPrevious)
        {
            bool isCollapsed = isPrevious ? splitter.IsPreviousCollapsed : splitter.IsNextCollapsed;

            if (!isCollapsed)
            {
                // 模拟点击折叠按钮
                SimulateButtonClick(splitter, isPrevious);
            }
        }

        /// <summary>
        /// 确保指定面板已展开（如果已折叠则展开）
        /// </summary>
        private void EnsureExpanded(CollapsibleGridSplitter splitter, bool isPrevious)
        {
            bool isCollapsed = isPrevious ? splitter.IsPreviousCollapsed : splitter.IsNextCollapsed;

            if (isCollapsed)
            {
                // 模拟点击展开按钮
                SimulateButtonClick(splitter, isPrevious);
            }
        }

        /// <summary>
        /// 模拟点击分割器的折叠/展开按钮
        /// </summary>
        private void SimulateButtonClick(CollapsibleGridSplitter splitter, bool isPrevious)
        {
            try
            {
                var buttonName = isPrevious ? "PART_CollapsePreviousButton" : "PART_CollapseNextButton";
                var button = splitter.Template?.FindName(buttonName, splitter) as Button;

                if (button != null && button.Visibility == Visibility.Visible)
                {
                    // 触发按钮的点击事件
                    button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            }
            catch
            {
                // 忽略错误
            }
        }

        #endregion

        #region 布局按钮事件处理

        private void LayoutFocus_Click(object sender, RoutedEventArgs e)
        {
            _layoutModule?.SwitchLayoutMode("Focus");
        }

        private void LayoutWork_Click(object sender, RoutedEventArgs e)
        {
            _layoutModule?.SwitchLayoutMode("Work");
        }

        private void LayoutFull_Click(object sender, RoutedEventArgs e)
        {
            _layoutModule?.SwitchLayoutMode("Full");
        }

        #endregion

        #region 双列表模式

        /// <summary>
        /// 双列表模式状态
        /// </summary>
        private bool _isDualListMode = false;
        public bool IsDualListMode => _isDualListMode;

        /// <summary>
        /// 双列表模式切换按钮点击事件
        /// </summary>
        private void DualListToggle_Click(object sender, RoutedEventArgs e)
        {
            _layoutModule?.ToggleDualListMode();
        }


        /// <summary>
        /// 设置双列表模式
        /// </summary>
        private void SetDualListMode(bool enable)
        {
            if (_isDualListMode == enable) return;
            _isDualListMode = enable;

            // 切换可见性
            RightPanel.Visibility = _isDualListMode ? Visibility.Collapsed : Visibility.Visible;
            SecondFileBrowserContainer.Visibility = _isDualListMode ? Visibility.Visible : Visibility.Collapsed;

            // 如果开启双列表，确保右侧栏展开
            if (_isDualListMode)
            {
                EnsureExpanded(SplitterRight, false);
            }

            // 更新按钮状态
            if (NavigationRail != null)
            {
                NavigationRail.DualListButton.Tag = _isDualListMode ? "Active" : null;
            }

            // 调整标签页布局
            UpdateTabManagerLayout();

            // 更新焦点边框
            UpdateFocusBorders();

            // 如果切换到双列表模式，初始化副列表
            if (_isDualListMode && SecondFileBrowser != null)
            {
                // (FileInfoService migration is handled via MVVM messages)

                // 初始化副标签页服务（首次进入时）
                if (_secondTabService == null && SecondTabManager != null)
                {
                    _secondTabService = new TabService(new AppConfig());

                    // 先绑定 UI 上下文，因为 UpdateConfig 会触发 UpdateTabWidths，后者依赖 UI 上下文
                    AttachSecondTabServiceUiContext();

                    // 然后应用实际配置
                    _secondTabService.UpdateConfig(_configService?.Config ?? new AppConfig());

                    // 通知 WindowStateManager 更新引用并恢复标签页
                    if (_windowStateManager != null)
                    {
                        _windowStateManager.SetSecondTabService(_secondTabService);
                        _windowStateManager.RestoreSecondaryTabs();
                    }
                }

                InitializeSecondFileBrowserEvents();
                LoadSecondFileBrowserContent();

                // 为副列表创建初始标签页（如果恢复失败或为空）
                EnsureSecondTabExists();
            }

            // 保存配置
            if (_configService?.Config != null)
            {
                _configService.Config.IsDualListMode = _isDualListMode;
                _configService.SaveCurrentConfig();
            }
        }

        /// <summary>
        /// 调整标签页管理器布局
        /// </summary>
        /// <summary>
        /// 调整标签页管理器布局
        /// </summary>
        /// <summary>
        /// 调整标签页管理器布局
        /// </summary>
        private void UpdateTabManagerLayout()
        {
            // 无论何种模式，TabManager 均应位于 Column 3 (Center)，与文件列表对齐
            if (TabManager.Parent is Grid grid)
            {
                Grid.SetColumn(TabManager, 3);
                Grid.SetColumnSpan(TabManager, 1);
            }

            // 统一调用 Margin 更新逻辑
            UpdateTabManagerMargin();
        }

        /// <summary>
        /// 当前焦点面板（主/副）
        /// </summary>
        private bool _isSecondPaneFocused = false;

        /// <summary>
        /// 更新焦点边框
        /// </summary>
        private void UpdateFocusBorders()
        {
            if (!_isDualListMode)
            {
                // 单列表模式：清除边框
                if (FileBrowser?.FocusBorderControl != null) FileBrowser.FocusBorderControl.BorderBrush = new SolidColorBrush(Colors.Transparent);
                if (SecondFileBrowser?.FocusBorderControl != null) SecondFileBrowser.FocusBorderControl.BorderBrush = new SolidColorBrush(Colors.Transparent);
                // 确保 UserControl 本身没有边框
                FileBrowser.BorderThickness = new Thickness(0);
                SecondFileBrowser.BorderThickness = new Thickness(0);
                return;
            }

            // 双列表模式：显示焦点边框 (使用覆盖层 Border 防止抖动)
            var focusBrush = new SolidColorBrush(Color.FromArgb(120, 0, 120, 215)); // 半透明蓝色
            var normalBrush = new SolidColorBrush(Colors.Transparent);

            if (FileBrowser?.FocusBorderControl != null)
            {
                FileBrowser.FocusBorderControl.BorderBrush = _isSecondPaneFocused ? normalBrush : focusBrush;
            }
            // 移除 UserControl 边框设置
            FileBrowser.BorderThickness = new Thickness(0);

            if (SecondFileBrowser?.FocusBorderControl != null)
            {
                SecondFileBrowser.FocusBorderControl.BorderBrush = _isSecondPaneFocused ? focusBrush : normalBrush;
            }
            // 移除 UserControl 边框设置
            SecondFileBrowser.BorderThickness = new Thickness(0);
        }

        /// <summary>
        /// 切换焦点面板
        /// </summary>
        internal void SwitchFocusedPane()
        {
            if (!_isDualListMode) return;

            // 调用模块进行状态切换，模块会发布消息触发 UI 更新 (UpdateFocusBorders)
            _layoutModule?.SwitchFocusedPane();
        }

        /// <summary>
        /// 绑定副标签页服务的 UI 上下文
        /// </summary>
        private bool _secondTabEventsSubscribed = false;
        private void AttachSecondTabServiceUiContext()
        {
            if (_secondTabService == null || SecondTabManager == null) return;

            var uiContext = new TabUiContext
            {
                FileBrowser = SecondFileBrowser,
                TabManager = SecondTabManager,
                Dispatcher = this.Dispatcher,
                OwnerWindow = this,
                GetConfig = () => _configService?.Config ?? new AppConfig(),
                SaveConfig = (config) => _configService?.SaveCurrentConfig(),
                GetCurrentPath = () => _secondCurrentPath ?? _currentPath,
                SetCurrentPath = (path) => _secondCurrentPath = path,
                SetNavigationCurrentPath = (path) => _secondCurrentPath = path,
                LoadLibraryFiles = (library) =>
                {
                    // 委托给 ViewModel 加载
                    // Prevent redundant calls if ViewModel already has this library AND mode
                    if (_viewModel.SecondaryPane.CurrentLibrary != library || _viewModel.SecondaryPane.NavigationMode != "Library")
                    {
                        _viewModel.SecondaryPane.NavigateTo(library);
                    }
                },
                NavigateToPathInternal = (path) =>
                {
                    // 避免重复加载相同路径，并确保模式正确
                    if (_viewModel.SecondaryPane.CurrentPath != path || _viewModel.SecondaryPane.NavigationMode != "Path")
                    {
                        SecondFileBrowser_PathChanged(this, path);
                    }
                },
                UpdateNavigationButtonsState = () => { },
                GetCurrentNavigationMode = () => "Path",
                GetSearchCacheService = () => _searchCacheService,
                GetSearchOptions = () => null,
                GetCurrentFiles = () => _secondCurrentFiles,
                SetCurrentFiles = (files) =>
                {
                    _secondCurrentFiles = files;
                    _viewModel?.SecondaryPane?.FileList?.UpdateFiles(files);
                },
                ClearFilter = () => { },
                FindResource = (key) => this.TryFindResource(key)
            };

            _secondTabService.AttachUiContext(uiContext);

            // 订阅事件（仅首次）
            if (!_secondTabEventsSubscribed)
            {
                _secondTabEventsSubscribed = true;

                // [SSOT] 副列表同步
                _secondTabService.ActiveTabChanged += (s, tab) => SyncSecondUiWithActiveTab(tab);

                // 订阅新建标签页事件
                SecondTabManager.NewTabRequested += (s, e) =>
                {
                    try
                    {
                        _secondTabService?.CreateBlankTab();
                    }
                    catch
                    {
                        // 忽略错误
                    }
                };

                // 确保点击标签栏也能激活副面板焦点
                SecondTabManager.PreviewMouseDown += (s, e) =>
                {
                    if (!_isSecondPaneFocused)
                    {
                        _isSecondPaneFocused = true;
                        UpdateFocusBorders();
                    }
                };
            }
        }

        /// <summary>
        /// 确保副列表有初始标签页
        /// </summary>
        private void EnsureSecondTabExists()
        {
            if (_secondTabService == null) return;

            // 如果没有标签页，创建一个默认标签页
            if (_secondTabService.Tabs.Count == 0)
            {
                var path = _secondCurrentPath ?? _currentPath;
                // 使用 CreatePathTab 确保创建 UI 元素
                _secondTabService.CreatePathTab(path, forceNewTab: true, activate: true);
            }
        }

        /// <summary>
        /// 初始化副文件列表事件处理
        /// </summary>
        private bool _secondFileBrowserEventsInitialized = false;
        private void InitializeSecondFileBrowserEvents()
        {
            if (_secondFileBrowserEventsInitialized || SecondFileBrowser == null) return;
            _secondFileBrowserEventsInitialized = true;

            // 路径变化事件
            SecondFileBrowser.PathChanged += SecondFileBrowser_PathChanged;
            SecondFileBrowser.BreadcrumbClicked += SecondFileBrowser_BreadcrumbClicked;
            SecondFileBrowser.NavigationBack += SecondFileBrowser_NavigationBack;
            SecondFileBrowser.NavigationForward += SecondFileBrowser_NavigationForward;
            SecondFileBrowser.NavigationUp += SecondFileBrowser_NavigationUp;

            // 双击打开事件
            SecondFileBrowser.FilesPreviewMouseDoubleClick += SecondFileBrowser_FilesDoubleClick;

            // 中键点击事件（打开新标签页）- 直接处理以确保在副面板打开
            SecondFileBrowser.FilesPreviewMouseDown += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle) return;

                var listView = SecondFileBrowser.FilesList;
                if (listView == null) return;

                // 获取点击位置对应的项目
                var hitResult = VisualTreeHelper.HitTest(listView, e.GetPosition(listView));
                if (hitResult == null) return;

                // 向上查找 ListViewItem
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listView)
                {
                    if (current is ListViewItem item)
                    {
                        if (item.Content is FileSystemItem selectedItem)
                        {
                            if (selectedItem.IsDirectory)
                            {
                                // 强制在副标签页服务中创建新标签页
                                _secondTabService?.CreatePathTab(selectedItem.Path, forceNewTab: true);
                                e.Handled = true;
                                return;
                            }
                        }
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            };

            // 选择变化事件（更新文件信息面板）
            SecondFileBrowser.FilesSelectionChanged += SecondFileBrowser_SelectionChanged;

            // 焦点事件 - 使用 PreviewMouseDown 确保点击列表任何位置都能激活焦点
            SecondFileBrowser.PreviewMouseDown += (s, e) =>
            {
                if (!_isSecondPaneFocused)
                {
                    _isSecondPaneFocused = true;
                    _layoutModule?.SetFocusedPane(true);
                    UpdateFocusBorders();
                }
            };
            FileBrowser.PreviewMouseDown += (s, e) =>
            {
                if (_isSecondPaneFocused)
                {
                    _isSecondPaneFocused = false;
                    _layoutModule?.SetFocusedPane(false);
                    UpdateFocusBorders();
                }
            };

            // 保留原有 GotFocus 以防键盘导航触发
            SecondFileBrowser.GotFocus += (s, e) =>
            {
                if (!_isSecondPaneFocused)
                {
                    _isSecondPaneFocused = true;
                    _layoutModule?.SetFocusedPane(true);
                    UpdateFocusBorders();
                }
            };
            FileBrowser.GotFocus += (s, e) =>
            {
                if (_isSecondPaneFocused)
                {
                    _isSecondPaneFocused = false;
                    _layoutModule?.SetFocusedPane(false);
                    UpdateFocusBorders();
                }
            };

            // 绑定文件操作事件 (右键菜单支持)
            // Copy/Paste/Refresh handled below with Toolbar support
            SecondFileBrowser.FileCut += (s, e) => _menuEventHandler?.Cut_Click(s, e);
            SecondFileBrowser.FileRename += (s, e) => _menuEventHandler?.Rename_Click(s, e);
            SecondFileBrowser.FileProperties += (s, e) => ShowSelectedFileProperties();

            // F2快捷键和其他键盘事件支持
            // Handled by _secondFileListHandler
            // SecondFileBrowser.FilesPreviewKeyDown += FilesListView_PreviewKeyDown;

            // 空白区域点击取消选择
            // Handled by _secondFileListHandler
            // SecondFileBrowser.FilesPreviewMouseLeftButtonDown += (s, e) =>
            // {
            //     if (SecondFileBrowser?.FilesList is ListView listView)
            //     {
            //         FilesListView_PreviewMouseLeftButtonDown(listView, e);
            //     }
            // };

            // 顶部工具栏按钮支持
            SecondFileBrowser.FileNewFolder += (s, e) => _menuEventHandler?.NewFolder_Click(s, e);
            SecondFileBrowser.FileNewFile += (s, e) => _menuEventHandler?.NewFile_Click(s, e);
            SecondFileBrowser.FileRefresh += (s, e) => LoadSecondFileBrowserDirectory(_secondCurrentPath);
            SecondFileBrowser.FileCopy += async (s, e) => await CopySelectedFilesAsync();
            SecondFileBrowser.FilePaste += async (s, e) => await PasteFilesAsync();
            SecondFileBrowser.FileAddTag += FileAddTag_Click;

            // F2 Rename handling for Second Browser
            SecondFileBrowser.CommitRename += (s, e) =>
            {
                if (e.Item == null || string.IsNullOrWhiteSpace(e.NewName)) return;

                IFileOperationContext context = null;
                // Currently only Path mode supported for Second Browser usually?
                // Or verify if it's library. Assuming Path mode for now or reuse simple context.
                context = new PathOperationContext(_secondCurrentPath, SecondFileBrowser, this, () => LoadSecondFileBrowserDirectory(_secondCurrentPath));

                var op = new Services.FileOperations.RenameOperation(context, this, _fileOperationService);
                op.Execute(e.Item, e.NewName);
            };


            SecondFileBrowser.FileDelete += async (s, e) =>
            {
                try
                {
                    // 使用 GetActiveContext 确保获取正确的上下文
                    var (browser, path, library) = GetActiveContext();
                    IFileOperationContext context = null;
                    if (library != null)
                    {
                        context = new LibraryOperationContext(library, browser, this, RefreshActiveFileList);
                    }
                    else
                    {
                        context = new PathOperationContext(path, browser, this, RefreshActiveFileList);
                    }
                    var items = browser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList();

                    // 使用统一的文件操作服务进行删除，它已经包含了确认对话框和撤销支持
                    // 注意：DeleteOperation 是旧的实现，这里我们应该尽量使用 FileOperationService
                    // 但为了保持与 FileDelete 事件签名一致 (RoutedEventHandler)，我们这里手动调用

                    if (_fileOperationService != null)
                    {
                        await _fileOperationService.DeleteAsync(items);
                    }
                    else
                    {
                        // Fallback to legacy DeleteOperation if service not available (unlikely)
                        var op = new Services.FileOperations.DeleteOperation(context);
                        await op.ExecuteAsync(items);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        /// <summary>
        /// 处理副列表选择变化，更新文件信息面板
        /// </summary>
        private void SecondFileBrowser_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecondFileBrowser?.FilesSelectedItem is FileSystemItem item)
            {
                _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { item }));
            }
            else
            {
                // 处理无选择的情况：显示当前文件夹信息
                if (!string.IsNullOrEmpty(_secondCurrentPath) && Directory.Exists(_secondCurrentPath))
                {
                    try
                    {
                        var dirInfo = new DirectoryInfo(_secondCurrentPath);
                        var folderItem = new FileSystemItem
                        {
                            Name = dirInfo.Name,
                            Path = dirInfo.FullName,
                            Type = "文件夹",
                            IsDirectory = true,
                            ModifiedDateTime = dirInfo.LastWriteTime,
                            ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy/M/d HH:mm"),
                            CreatedDateTime = dirInfo.CreationTime,
                            CreatedTime = dirInfo.CreationTime.ToString("yyyy/M/d HH:mm"),
                            Size = "-", // 将在 ShowDirectoryInfo 中异步计算
                            Tags = ""
                        };
                        _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { folderItem }));
                    }
                    catch
                    {
                        // 忽略错误
                    }
                }
            }
        }

        // 副文件列表导航状态
        private string _secondCurrentPath;
        private readonly Stack<string> _secondNavHistory = new Stack<string>();
        private readonly Stack<string> _secondNavForward = new Stack<string>();

        private void LoadSecondFileBrowserContent()
        {
            // 优先使用现有的副面板路径，或者副标签页的路径
            if (string.IsNullOrEmpty(_secondCurrentPath))
            {
                if (_secondTabService?.ActiveTab != null && !string.IsNullOrEmpty(_secondTabService.ActiveTab.Path))
                {
                    _secondCurrentPath = _secondTabService.ActiveTab.Path;
                }
                else
                {
                    // 只有在完全没有状态时才默认跟随主面板
                    _secondCurrentPath = _currentPath;
                }
            }

            LoadSecondFileBrowserDirectory(_secondCurrentPath);
        }

        private void LoadSecondFileBrowserDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || SecondFileBrowser == null) return;

            // 委托给 ViewModel 加载
            // Prevent redundant calls if ViewModel already has this path AND mode
            if (_viewModel.SecondaryPane.CurrentPath != path || _viewModel.SecondaryPane.NavigationMode != "Path")
            {
                _viewModel.SecondaryPane.NavigateTo(path);
            }

            // 立即同步 UI 状态，避免等待事件回调导致的延迟或在路径相同时不刷新的问题
            SecondFileBrowser.AddressText = path;
            SecondFileBrowser.UpdateBreadcrumb(path);
            SecondFileBrowser.IsAddressReadOnly = false;
            SecondFileBrowser.SetSearchStatus(false);
            SecondFileBrowser.SetPropertiesButtonVisibility(!ProtocolManager.IsVirtual(path));

            // 更新导航按钮状态
            SecondFileBrowser.NavBackEnabled = _secondNavHistory.Count > 0;
            SecondFileBrowser.NavForwardEnabled = _secondNavForward.Count > 0;
            string dirName = null;
            try { dirName = System.IO.Path.GetDirectoryName(path); } catch { }
            SecondFileBrowser.NavUpEnabled = !string.IsNullOrEmpty(path) && !ProtocolManager.IsVirtual(path) && !string.IsNullOrEmpty(dirName);

            // (FileInfo Service logic migrated to MVVM messages)

            // 显示当前文件夹信息
            if (Directory.Exists(path))
            {
                // ... same logic ...
                try
                {
                    var dirInfo = new DirectoryInfo(path);
                    var folderItem = new FileSystemItem
                    {
                        Name = dirInfo.Name,
                        Path = dirInfo.FullName,
                        Type = "文件夹",
                        IsDirectory = true,
                        ModifiedDateTime = dirInfo.LastWriteTime,
                        ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy/M/d HH:mm"),
                        CreatedDateTime = dirInfo.CreationTime,
                        CreatedTime = dirInfo.CreationTime.ToString("yyyy/M/d HH:mm"),
                        Size = "-",
                        Tags = ""
                    };
                    _messageBus.Publish(new FileSelectionChangedMessage(new List<FileSystemItem> { folderItem }));
                }
                catch { }
            }
        }

        private void SecondFileBrowser_PathChanged(object sender, string newPath)
        {
            if (!string.IsNullOrEmpty(_secondCurrentPath))
            {
                _secondNavHistory.Push(_secondCurrentPath);
            }
            _secondNavForward.Clear();
            _secondCurrentPath = newPath;

            // 同步更新当前激活的标签页路径
            if (_secondTabService != null)
            {
                _secondTabService.UpdateActiveTabPath(newPath);
            }

            LoadSecondFileBrowserDirectory(newPath);

            // 更新副列表属性按钮可见性
            if (SecondFileBrowser != null)
            {
                bool visible = true;
                if (!string.IsNullOrEmpty(newPath))
                {
                    if (newPath.StartsWith("search:", StringComparison.OrdinalIgnoreCase) ||
                       ProtocolManager.IsVirtual(newPath))
                    {
                        visible = false;
                    }
                }
                SecondFileBrowser.SetPropertiesButtonVisibility(visible);
            }
        }

        private void SecondFileBrowser_BreadcrumbClicked(object sender, string path)
        {
            SecondFileBrowser_PathChanged(sender, path);
        }

        private void SecondFileBrowser_NavigationBack(object sender, RoutedEventArgs e)
        {
            if (_secondNavHistory.Count > 0)
            {
                _secondNavForward.Push(_secondCurrentPath);
                _secondCurrentPath = _secondNavHistory.Pop();
                // 更新副标签页标题
                _secondTabService?.UpdateActiveTabPath(_secondCurrentPath);
                LoadSecondFileBrowserDirectory(_secondCurrentPath);
            }
        }

        private void SecondFileBrowser_NavigationForward(object sender, RoutedEventArgs e)
        {
            if (_secondNavForward.Count > 0)
            {
                _secondNavHistory.Push(_secondCurrentPath);
                _secondCurrentPath = _secondNavForward.Pop();
                // 更新副标签页标题
                _secondTabService?.UpdateActiveTabPath(_secondCurrentPath);
                LoadSecondFileBrowserDirectory(_secondCurrentPath);
            }
        }

        private void SecondFileBrowser_NavigationUp(object sender, RoutedEventArgs e)
        {
            var parent = System.IO.Path.GetDirectoryName(_secondCurrentPath);
            if (!string.IsNullOrEmpty(parent))
            {
                SecondFileBrowser_PathChanged(sender, parent);
            }
        }

        private void SecondFileBrowser_FilesDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SecondFileBrowser.FilesSelectedItem is FileSystemItem item)
            {
                if (item.IsDirectory)
                {
                    SecondFileBrowser_PathChanged(sender, item.Path);
                }
                else
                {
                    // 检查是否为归档文件或其他特殊协议
                    var protocolInfo = Services.Core.ProtocolManager.Parse(item.Path);
                    if (protocolInfo.Type == Services.Core.ProtocolType.Archive)
                    {
                        MessageBox.Show("暂不支持直接打开压缩包内的文件。\n请先解压后再试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    // 打开文件
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

        /// <summary>
        /// 恢复双列表模式状态
        /// </summary>
        private void RestoreDualListMode()
        {
            if (_configService?.Config != null)
            {
                _isDualListMode = _configService.Config.IsDualListMode;
                RightPanel.Visibility = _isDualListMode ? Visibility.Collapsed : Visibility.Visible;
                SecondFileBrowserContainer.Visibility = _isDualListMode ? Visibility.Visible : Visibility.Collapsed;
                if (NavigationRail != null) NavigationRail.DualListButton.Tag = _isDualListMode ? "Active" : null;

                if (_isDualListMode)
                {
                    // 同样需要初始化服务，否则重启恢复模式时会报错或功能缺失
                    if (_secondTabService == null && SecondTabManager != null)
                    {
                        // (FileInfoService migration is handled via MVVM messages)

                        _secondTabService = new TabService(new AppConfig());
                        AttachSecondTabServiceUiContext();
                        _secondTabService.UpdateConfig(_configService?.Config ?? new AppConfig());

                        // 通知 WindowStateManager 更新引用并恢复标签页
                        if (_windowStateManager != null)
                        {
                            _windowStateManager.SetSecondTabService(_secondTabService);
                            _windowStateManager.RestoreSecondaryTabs();
                        }
                    }

                    UpdateTabManagerLayout();
                    InitializeSecondFileBrowserEvents();
                    LoadSecondFileBrowserContent();

                    // 确保有标签页
                    EnsureSecondTabExists();

                    UpdateFocusBorders();
                }
            }
        }

        #endregion

        #region 布局模式恢复

        /// <summary>
        /// 恢复保存的布局模式
        /// </summary>
        private void RestoreLayoutMode()
        {
            if (_configService?.Config != null)
            {
                if (Enum.TryParse<LayoutMode>(_configService.Config.LayoutMode, out var mode))
                {
                    SwitchLayoutMode(mode);
                }
                else
                {
                    // 默认完整模式
                    SwitchLayoutMode(LayoutMode.Full);
                }
            }
        }

        #endregion

        #region 上下文获取辅助方法

        /// <summary>
        /// 获取当前激活的文件上下文（浏览器和路径）
        /// 支持单/双栏模式自动识别
        /// </summary>
        internal (Controls.FileBrowserControl browser, string path, Library library) GetActiveContext()
        {
            if (_isDualListMode && _isSecondPaneFocused && SecondFileBrowser != null)
            {
                // 副列表目前只支持路径模式，暂不支持库
                return (SecondFileBrowser, _secondCurrentPath, null);
            }
            return (FileBrowser, _currentPath, _currentLibrary);
        }

        /// <summary>
        /// 刷新当前激活的文件列表
        /// </summary>
        internal void RefreshActiveFileList()
        {
            if (_isDualListMode && _isSecondPaneFocused && SecondFileBrowser != null)
            {
                if (!string.IsNullOrEmpty(_secondCurrentPath))
                {
                    LoadSecondFileBrowserDirectory(_secondCurrentPath);
                }
            }
            else
            {
                RefreshFileList();
            }
        }

        #endregion

        #region 布局初始化

        /// <summary>
        /// 初始化布局模式（恢复上次的布局配置）
        ///在 MainWindow 初始化时调用
        /// </summary>
        internal void InitializeLayoutMode()
        {
            // 初始恢复配置
            RestoreLayoutMode();
            RestoreDualListMode();

            // 同步初始状态到模块
            _layoutModule?.InitializeState(_currentLayoutMode.ToString(), _isDualListMode, _isSecondPaneFocused);

            // 订阅 MVVM 消息，实现桥接
            _messageBus?.Subscribe<LayoutModeChangedMessage>(m =>
            {
                if (Enum.TryParse<LayoutMode>(m.Mode, out var mode))
                {
                    SwitchLayoutMode(mode);
                }
            });

            _messageBus?.Subscribe<DualListModeChangedMessage>(m =>
            {
                if (_isDualListMode != m.IsEnabled)
                {
                    SetDualListMode(m.IsEnabled);
                }
            });

            _messageBus?.Subscribe<FocusedPaneChangedMessage>(m =>
            {
                // 同步本地状态并更新 UI
                if (_isSecondPaneFocused != m.IsSecondPaneFocused)
                {
                    _isSecondPaneFocused = m.IsSecondPaneFocused;
                    UpdateFocusBorders();

                    // 将焦点设置到对应的文件列表
                    if (_isSecondPaneFocused)
                    {
                        SecondFileBrowser?.FilesList?.Focus();
                    }
                    else
                    {
                        FileBrowser?.FilesList?.Focus();
                    }
                }
            });
        }

        /// <summary>
        /// 供 KeyboardEventHandler 调用的快捷切换桥接
        /// </summary>
        internal void SwitchFocusedPaneFromKeyboard()
        {
            _layoutModule?.SwitchFocusedPane();
        }

        #endregion

        #region 副面板 SSOT 同步

        /// <summary>
        /// [SSOT] 基于当前副活动标签页状态同步 UI
        /// </summary>
        private void SyncSecondUiWithActiveTab(PathTab tab)
        {
            if (tab == null || SecondFileBrowser == null) return;

            // 1. 同步库/路径上下文
            if (tab.Type == TabType.Library)
            {
                // 设置当前路径为空，表示在库模式
                _secondCurrentPath = null;
                // 加载库
                if (tab.Library != null)
                {
                    LoadSecondFileBrowserLibrary(tab.Library);
                }
            }
            else
            {
                // 路径模式
                _secondCurrentPath = tab.Path;

                // 加载路径
                if (tab.Path != null && !tab.Path.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
                {
                    // 只有非搜索路径才直接加载 (搜索由搜索逻辑驱动)
                    LoadSecondFileBrowserDirectory(tab.Path);
                }
            }

            // 监听标签页属性变更 
            tab.PropertyChanged -= OnSecondActiveTabPropertyChanged;
            tab.PropertyChanged += OnSecondActiveTabPropertyChanged;
        }

        private void OnSecondActiveTabPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is PathTab tab && tab == _secondTabService?.ActiveTab)
            {
                if (e.PropertyName == nameof(PathTab.Path) || e.PropertyName == nameof(PathTab.Library))
                {
                    SyncSecondUiWithActiveTab(tab);
                }
            }
        }

        /// <summary>
        /// 加载副文件列表的库内容
        /// </summary>
        private async void LoadSecondFileBrowserLibrary(Library library)
        {
            if (library == null || SecondFileBrowser == null) return;

            // 委托给 ViewModel 加载
            // Prevent redundant calls if ViewModel already has this library AND mode
            if (_viewModel.SecondaryPane.CurrentLibrary != library || _viewModel.SecondaryPane.NavigationMode != "Library")
            {
                _viewModel.SecondaryPane.NavigateTo(library);
            }

            try
            {
                // 更新 UI 状态
                SecondFileBrowser.NavUpEnabled = false;
                SecondFileBrowser.SetSearchStatus(false);
                SecondFileBrowser.AddressText = library.Name;
                SecondFileBrowser.IsAddressReadOnly = true;
                // 使用 SetLibraryBreadcrumb 确保面包屑显示库名
                SecondFileBrowser.SetLibraryBreadcrumb(library.Name);

                // 设置属性按钮可见性
                SecondFileBrowser.SetPropertiesButtonVisibility(true);

                // 加载文件
                if (library.Paths == null || library.Paths.Count == 0)
                {
                    _viewModel?.SecondaryPane?.FileList?.UpdateFiles(new List<FileSystemItem>());
                    return;
                }

                // 调用 FileListService 加载
                var items = await _fileListService.LoadFileSystemItemsFromMultiplePathsAsync(
                     library.Paths,
                     (path) => DatabaseManager.GetFolderSize(path),
                     (bytes) => _fileListService.FormatFileSize(bytes)
                );

                _secondCurrentFiles = items;
                _viewModel?.SecondaryPane?.FileList?.UpdateFiles(items);
            }
            catch (Exception ex)
            {
                DialogService.Error($"加载库文件失败: {ex.Message}", owner: this);
            }
        }

        #endregion
    }
}

