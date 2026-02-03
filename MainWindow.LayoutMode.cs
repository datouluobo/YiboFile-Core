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
using YiboFile.Services.Config;


namespace YiboFile
{
    /// <summary>
    /// 布局模式切换功能
    /// </summary>
    public partial class MainWindow
    {
        #region 布局模式枚举和字段
        // _currentLayoutMode 字段已由 LayoutModule.CurrentLayoutMode 接管
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

        #endregion



        #region 双列表模式

        /// <summary>
        /// 双列表模式状态 (代理到 LayoutModule)
        /// </summary>
        public bool IsDualListMode => _layoutModule?.IsDualListMode ?? false;


        /// <summary>
        /// 设置双列表模式
        /// </summary>
        private void SetDualListMode(bool enable)
        {
            // 切换可见性由 XAML 绑定处理 (RightPanel.EffectiveVisibility 和 Layout.IsDualListMode)

            // 更新按钮状态
            if (NavigationRail != null)
            {
                NavigationRail.DualListButton.Tag = enable ? "Active" : null;
            }

            // 调整标签页布局
            UpdateTabManagerLayout();

            // 更新焦点边框
            UpdateFocusBorders();

            // 如果切换到双列表模式，初始化副列表
            if (enable && SecondFileBrowser != null)
            {
                // (FileInfoService migration is handled via MVVM messages)

                // 初始化副标签页服务（首次进入时）
                if (_secondTabService == null && SecondTabManager != null)
                {
                    _secondTabService = new TabService(new AppConfig());

                    // 先绑定 UI 上下文，因为 UpdateConfig 会触发 UpdateTabWidths，后者依赖 UI 上下文
                    AttachSecondTabServiceUiContext();

                    // 然后应用实际配置
                    _secondTabService.UpdateConfig(ConfigurationService.Instance.Config);



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

            // 保存配置 (持久化逻辑)
            ConfigurationService.Instance.Set(c => c.IsDualListMode, enable);
            ConfigurationService.Instance.SaveNow();


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
        /// 当前焦点面板（主/副） (代理到 LayoutModule)
        /// </summary>
        public bool IsSecondPaneFocused => _layoutModule?.IsSecondPaneFocused ?? false;

        /// <summary>
        /// 更新焦点边框
        /// </summary>
        private void UpdateFocusBorders()
        {
            if (!IsDualListMode)
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
                FileBrowser.FocusBorderControl.BorderBrush = IsSecondPaneFocused ? normalBrush : focusBrush;
            }
            // 移除 UserControl 边框设置
            FileBrowser.BorderThickness = new Thickness(0);

            if (SecondFileBrowser?.FocusBorderControl != null)
            {
                SecondFileBrowser.FocusBorderControl.BorderBrush = IsSecondPaneFocused ? focusBrush : normalBrush;
            }
            // 移除 UserControl 边框设置
            SecondFileBrowser.BorderThickness = new Thickness(0);
        }

        /// <summary>
        /// 切换焦点面板
        /// </summary>
        internal void SwitchFocusedPane()
        {
            if (!IsDualListMode) return;

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
                GetConfig = () => ConfigurationService.Instance.Config,
                SaveConfig = (config) => ConfigurationService.Instance.SaveNow(),


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
                    if (!IsSecondPaneFocused)
                    {
                        _layoutModule?.SetFocusedPane(true);
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
                if (!IsSecondPaneFocused)
                {
                    _layoutModule?.SetFocusedPane(true);
                }
            };
            FileBrowser.PreviewMouseDown += (s, e) =>
            {
                if (IsSecondPaneFocused)
                {
                    _layoutModule?.SetFocusedPane(false);
                }
            };

            // 保留原有 GotFocus 以防键盘导航触发
            SecondFileBrowser.GotFocus += (s, e) =>
            {
                if (!IsSecondPaneFocused)
                {
                    _layoutModule?.SetFocusedPane(true);
                }
            };
            FileBrowser.GotFocus += (s, e) =>
            {
                if (IsSecondPaneFocused)
                {
                    _layoutModule?.SetFocusedPane(false);
                }
            };
            // 绑定文件操作事件 (右键菜单支持) - Migrated to Commands
            /*
            // Copy/Paste/Refresh handled below with Toolbar support
            SecondFileBrowser.FileCut += (s, e) => _menuEventHandler?.Cut_Click(s, e);
            SecondFileBrowser.FileRename += (s, e) => _menuEventHandler?.Rename_Click(s, e);
            */
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

            // 顶部工具栏按钮支持 - Migrated to Commands
            /*
            SecondFileBrowser.FileNewFolder += (s, e) => _menuEventHandler?.NewFolder_Click(s, e);
            SecondFileBrowser.FileNewFile += (s, e) => _menuEventHandler?.NewFile_Click(s, e);
            SecondFileBrowser.FileRefresh += (s, e) => LoadSecondFileBrowserDirectory(_secondCurrentPath);
            SecondFileBrowser.FileCopy += async (s, e) => await CopySelectedFilesAsync();
            SecondFileBrowser.FilePaste += async (s, e) => await PasteFilesAsync();
            */
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


            /*
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
            */
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
            // 检查副标签页服务当前激活的标签页类型
            if (_secondTabService?.ActiveTab != null)
            {
                if (_secondTabService.ActiveTab.Type == TabType.Library && _secondTabService.ActiveTab.Library != null)
                {
                    LoadSecondFileBrowserLibrary(_secondTabService.ActiveTab.Library);
                    return;
                }
                else if (!string.IsNullOrEmpty(_secondTabService.ActiveTab.Path))
                {
                    _secondCurrentPath = _secondTabService.ActiveTab.Path;
                }
            }

            // 优先使用现有的副面板路径
            if (string.IsNullOrEmpty(_secondCurrentPath))
            {
                // 只有在完全没有状态时才默认跟随主面板
                _secondCurrentPath = _currentPath;
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

                    // 修复：显式更新副面板的信息栏
                    _secondFileInfoService?.ShowFileInfo(folderItem);

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

        #endregion

        #region 上下文获取辅助方法

        /// <summary>
        /// 获取当前激活的文件上下文（浏览器和路径）
        /// 支持单/双栏模式自动识别
        /// </summary>
        internal (Controls.FileBrowserControl browser, string path, Library library) GetActiveContext()
        {
            if (IsDualListMode && IsSecondPaneFocused && SecondFileBrowser != null)
            {
                // 副列表支持路径和库模式
                var secLib = _viewModel?.SecondaryPane?.CurrentLibrary;
                return (SecondFileBrowser, _secondCurrentPath, secLib);
            }
            return (FileBrowser, _currentPath, _currentLibrary);
        }

        /// <summary>
        /// 刷新当前激活的文件列表
        /// </summary>
        internal void RefreshActiveFileList()
        {
            if (IsDualListMode && IsSecondPaneFocused && SecondFileBrowser != null)
            {
                if (_viewModel?.SecondaryPane?.NavigationMode == "Library" && _viewModel.SecondaryPane.CurrentLibrary != null)
                {
                    LoadSecondFileBrowserLibrary(_viewModel.SecondaryPane.CurrentLibrary);
                }
                else if (!string.IsNullOrEmpty(_secondCurrentPath))
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

        internal void InitializeLayoutMode()
        {
            // 订阅 MVVM 消息，实现桥接
            _messageBus?.Subscribe<LayoutModeChangedMessage>(m =>
            {
                ApplyLayoutModeUI(m.Mode);
            });

            _messageBus?.Subscribe<DualListModeChangedMessage>(m =>
            {
                SetDualListMode(m.IsEnabled);
            });

            _messageBus?.Subscribe<FocusedPaneChangedMessage>(m =>
            {
                // 同步 UI 状态
                UpdateFocusBorders();

                // 将焦点设置到对应的文件列表
                if (m.IsSecondPaneFocused)
                {
                    SecondFileBrowser?.FilesList?.Focus();
                }
                else
                {
                    FileBrowser?.FilesList?.Focus();
                }
            });

            // 应用初始 UI 状态
            if (_layoutModule != null)
            {
                ApplyLayoutModeUI(_layoutModule.CurrentLayoutMode);

                // 应用初始双列表状态（触发事件绑定和内容加载）
                SetDualListMode(_layoutModule.IsDualListMode);
            }
        }

        private void ApplyLayoutModeUI(string mode)
        {
            CloseOverlays();

            // 更新按钮激活状态
            if (NavigationRail != null)
            {
                NavigationRail.FocusModeButton.Tag = mode == "Focus" ? "Active" : null;
                NavigationRail.WorkModeButton.Tag = mode == "Work" ? "Active" : null;
                NavigationRail.FullModeButton.Tag = mode == "Full" ? "Active" : null;
            }

            // 更新标签页边距
            UpdateTabManagerMargin();
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
        private void LoadSecondFileBrowserLibrary(Library library)
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
                // 防止修改地址栏触发路径导航导致递归死循环
                if (SecondFileBrowser != null)
                {
                    SecondFileBrowser.PathChanged -= SecondFileBrowser_PathChanged;
                }

                // 更新 UI 状态
                SecondFileBrowser.NavUpEnabled = false;
                SecondFileBrowser.SetSearchStatus(false);
                SecondFileBrowser.AddressText = library.Name;
                SecondFileBrowser.IsAddressReadOnly = true;
                // 使用 SetLibraryBreadcrumb 确保面包屑显示库名
                SecondFileBrowser.SetLibraryBreadcrumb(library.Name);

                // 恢复事件监听
                if (SecondFileBrowser != null)
                {
                    SecondFileBrowser.PathChanged += SecondFileBrowser_PathChanged;
                }

                // 设置属性按钮可见性
                SecondFileBrowser.SetPropertiesButtonVisibility(true);

                // 显式更新副面板为库信息
                _secondFileInfoService?.ShowLibraryInfo(library);

                // 加载文件 - 委托给 ViewModel，此处不再手动加载
                // ViewModel 的 NavigateTo 会触发后台加载，如果不移除手动加载会导致双重加载和UI卡死

                // 确保 ViewModel 正在加载
                if (_viewModel?.SecondaryPane?.IsLoading == false)
                {
                    // Force refresh if needed? Usually NavigateTo handles it.
                }

                // _secondCurrentFiles 应该通过绑定或 ViewModel 事件更新
                // 但为了兼容旧逻辑，我们可能需要从 ViewModel 获取?
                // 暂时留空，假定 ViewModel -> UI 绑定工作正常
            }
            catch (Exception ex)
            {
                DialogService.Error($"加载库文件失败: {ex.Message}", owner: this);
            }
        }

        /// <summary>
        /// 导航副面板到库视图
        /// </summary>
        internal void NavigateSecondaryPaneToLibrary(Library library)
        {
            if (!IsDualListMode || SecondFileBrowser == null) return;

            // 确保焦点正确
            if (!IsSecondPaneFocused)
            {
                // _layoutModule?.SetFocusedPane(true); // 可选：强制聚焦
            }

            // 如果未指定库，尝试从配置恢复上次的库，或使用第一个库
            if (library == null)
            {
                // 注意：这里我们简单地使用与主面板相同的恢复逻辑，或者是副面板独立的？
                // 目前没有为副面板独立保存 LastLibraryId，所以可能跟随主面板或者默认第一个
                if (ConfigurationService.Instance.Config.LastLibraryId > 0)
                {
                    library = _libraryService.GetLibrary(ConfigurationService.Instance.Config.LastLibraryId);
                }


                if (library == null)
                {
                    library = _libraryService.LoadLibraries().FirstOrDefault();
                }
            }

            if (library != null)
            {
                LoadSecondFileBrowserLibrary(library);
            }
        }


        /// <summary>
        /// 导航副面板到标签视图
        /// </summary>
        internal void NavigateSecondaryPaneToTag(TagViewModel tag)
        {
            if (!IsDualListMode || SecondFileBrowser == null) return;

            // 委托给 ViewModel 加载
            if (tag != null)
            {
                LoadSecondFileBrowserTag(tag);
            }
        }

        private void LoadSecondFileBrowserTag(TagViewModel tag)
        {
            if (tag == null || SecondFileBrowser == null) return;

            // 委托给 ViewModel 加载
            if (_viewModel.SecondaryPane.CurrentTag != tag || _viewModel.SecondaryPane.NavigationMode != "Tag")
            {
                _viewModel.SecondaryPane.NavigateTo(tag);
            }

            try
            {
                // 更新 UI 状态
                SecondFileBrowser.NavUpEnabled = false;
                SecondFileBrowser.SetSearchStatus(false);
                SecondFileBrowser.AddressText = tag.Name;
                SecondFileBrowser.IsAddressReadOnly = true;
                SecondFileBrowser.UpdateBreadcrumb(tag.Name);

                SecondFileBrowser.SetPropertiesButtonVisibility(false);

                // 构造标签项以显示信息
                var tagItem = new FileSystemItem
                {
                    Name = tag.Name,
                    Path = $"tag://{tag.Name}",
                    Type = "标签",
                    IsDirectory = true,
                    Size = "-",
                    Tags = tag.Name
                };
                _secondFileInfoService?.ShowFileInfo(tagItem);

                // 加载文件 - 委托给 ViewModel，此处不再手动加载
                // 移除手动加载逻辑，避免卡死
            }
            catch (Exception ex)
            {
                DialogService.Error($"加载标签文件失败: {ex.Message}", owner: this);
            }
        }

        #endregion
    }
}

