using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Services.Tabs;
using YiboFile.Services.Config;
using YiboFile.Services.Navigation;
using YiboFile.Services.Data.Repositories;
using YiboFile.Services.Core;


namespace YiboFile.Services.Navigation
{
    /// <summary>
    /// 导航模式管理服务
    /// 负责导航模式切换、UI更新、状态管理等
    /// </summary>
    public class NavigationModeService
    {
        #region 私有字段

        private readonly INavigationModeUIHelper _uiHelper;
        private readonly NavigationService _navigationService;
        private readonly TabService _tabService;
        private readonly ConfigurationService _configService;

        private readonly ILibraryRepository _libraryRepository;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化导航模式服务
        /// </summary>
        public NavigationModeService(
            INavigationModeUIHelper uiHelper,
            NavigationService navigationService,
            TabService tabService,
            ConfigurationService configService,

            ILibraryRepository libraryRepository = null)
        {
            _uiHelper = uiHelper ?? throw new ArgumentNullException(nameof(uiHelper));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));

            _libraryRepository = libraryRepository ?? App.ServiceProvider?.GetService(typeof(ILibraryRepository)) as ILibraryRepository;
        }

        #endregion

        #region 导航模式切换

        /// <summary>
        /// 切换导航模式
        /// </summary>
        /// <param name="mode">导航模式</param>
        /// <param name="skipRefresh">是否跳过刷新文件列表（启动时恢复状态使用）</param>
        public void SwitchNavigationMode(string mode, bool skipRefresh = false)
        {
            if (string.IsNullOrEmpty(mode)) return;

            // 使用 NavigationService 处理基础 UI 切换
            _navigationService.SwitchNavigationMode(mode);

            // 更新导航按钮样式（橙色标记当前模式）
            UpdateNavigationButtonStyles(mode);

            // 切换到非库模式时清空当前库
            if (mode != "Library")
            {
                _uiHelper.CurrentLibrary = null;
            }

            // 根据模式显示对应内容和按钮
            switch (mode)
            {
                case "Path":
                    HandlePathMode(skipRefresh);
                    break;
                case "Library":
                    HandleLibraryMode(skipRefresh);
                    break;
                case "Tag":
                    HandleTagMode(skipRefresh);
                    break;

            }

            // 保存当前模式
            if (_configService != null)
            {
                _configService.Set(cfg => cfg.LastNavigationMode, mode);
            }


            // 应用可见列设置并确保右键菜单绑定
            _uiHelper.ApplyVisibleColumnsForCurrentMode();
            _uiHelper.EnsureHeaderContextMenuHook();

            // 更新文件列表（导航操作本身也会加载文件，这里作为备用刷新）
            // 启动时恢复状态时跳过此步骤，避免与标签页恢复冲突
            if (!skipRefresh)
            {
                _uiHelper.RefreshFileList();
            }
        }

        /// <summary>
        /// 更新导航按钮样式，用橙色标记当前模式
        /// </summary>
        private void UpdateNavigationButtonStyles(string activeMode)
        {
            if (_uiHelper.Dispatcher == null) return;

            _uiHelper.Dispatcher.BeginInvoke(new Action(() =>
            {
                // 获取样式资源
                var activeStyle = Application.Current.TryFindResource("ActiveNavigationButtonStyle") as System.Windows.Style;
                var normalStyle = Application.Current.TryFindResource("FramelessNavButtonStyle") as System.Windows.Style;

                // 重置所有按钮为普通样式
                if (_uiHelper.NavPathButton != null && normalStyle != null)
                {
                    _uiHelper.NavPathButton.Style = normalStyle;
                }
                if (_uiHelper.NavLibraryButton != null && normalStyle != null)
                {
                    _uiHelper.NavLibraryButton.Style = normalStyle;
                }
                if (_uiHelper.NavTagButton != null && normalStyle != null)
                {
                    _uiHelper.NavTagButton.Style = normalStyle;
                }

                // 设置当前模式的按钮为橙色样式
                switch (activeMode)
                {
                    case "Path":
                        if (_uiHelper.NavPathButton != null && activeStyle != null)
                        {
                            _uiHelper.NavPathButton.Style = activeStyle;
                        }
                        break;
                    case "Library":
                        if (_uiHelper.NavLibraryButton != null && activeStyle != null)
                        {
                            _uiHelper.NavLibraryButton.Style = activeStyle;
                        }
                        break;
                    case "Tag":
                        if (_uiHelper.NavTagButton != null && activeStyle != null)
                        {
                            _uiHelper.NavTagButton.Style = activeStyle;
                        }
                        break;

                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// 处理路径模式切换
        /// </summary>
        /// <param name="skipRefresh">是否跳过刷新操作（启动时恢复状态使用）</param>
        private void HandlePathMode(bool skipRefresh = false)
        {
            // 隐藏标签页面底部按钮
            // Tag Bottom Buttons hidden - Phase 2

            // 隐藏库管理按钮


            if (_uiHelper.FileBrowser != null)
            {
                _uiHelper.FileBrowser.TabsVisible = true;
            }

            // 从库切换到路径时，查找或创建标签页
            // 启动时恢复状态时跳过，避免与标签页恢复冲突
            if (!skipRefresh)
            {
                _uiHelper.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_uiHelper.FileBrowser == null) return;

                    if (string.IsNullOrEmpty(_uiHelper.CurrentPath))
                    {
                        // 查找第一个使用路径的标签页
                        PathTab matchingTab = _tabService.Tabs.FirstOrDefault();
                        if (matchingTab != null && Directory.Exists(matchingTab.Path))
                        {
                            _uiHelper.CurrentPath = matchingTab.Path;
                            _uiHelper.SwitchToTab(matchingTab);
                        }
                        else
                        {
                            // 如果没有标签页，创建新标签页，默认路径为桌面
                            _uiHelper.CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                            _uiHelper.CreateTab(_uiHelper.CurrentPath);
                        }
                    }
                    else
                    {
                        // 如果已有路径，查找或创建对应的标签页
                        PathTab existingTab = _tabService.FindTabByPath(_uiHelper.CurrentPath);
                        if (existingTab != null)
                        {
                            _uiHelper.SwitchToTab(existingTab);
                        }
                        else
                        {
                            _uiHelper.CreateTab(_uiHelper.CurrentPath);
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 处理库模式切换
        /// </summary>
        /// <param name="skipRefresh">是否跳过刷新操作（启动时恢复状态使用）</param>
        private void HandleLibraryMode(bool skipRefresh = false)
        {
            // 隐藏标签页面底部按钮
            // Tag Bottom Buttons hidden - Phase 2

            // 显示库管理按钮


            // 库模式下也显示标签页
            if (_uiHelper.FileBrowser != null)
            {
                _uiHelper.FileBrowser.TabsVisible = true;
            }

            // 切换到库模式时，恢复最后选中的库
            // 启动时恢复状态时跳过，避免与标签页恢复冲突
            if (!skipRefresh)
            {
                _uiHelper.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 检查是否应该是副面板操作
                    if (_uiHelper is MainWindow mw && mw.IsDualListMode)
                    {
                        // 如果是副面板，我们应该尝试设置副面板的库
                        // 但目前 NavigationModeService 没有直接引用 _secondTabService 或类似状态
                        // 依赖 MainWindow.LoadSecondFileBrowserContent 或类似机制
                        // 对于副面板，我们暂时简单地保持当前库（如果有），这通常由Tab切换触发

                        // 需要调用 MainWindow 特定的逻辑来加载副列表库？
                        // 或者这里只是通用 UI 状态切换?

                        // 如果副面板处于焦点，我们让 MainWindow 处理特定加载
                        // 但为了保持一致性，如果 CurrentLibrary 不为空，我们应该尝试应用到焦点面板？
                        // 不，CurrentLibrary 属性是指向主面板的库状态

                        // 所以这里如果不做任何事，副面板可能为空。
                        // 我们应该保留原逻辑（针对主面板），同时扩展支持副面板？
                        // 暂时保持主面板逻辑不变，因为“库模式”通常是全局的 UI 状态（左侧导航栏高亮）
                        // 而具体哪个面板显示什么，由面板自己的 ViewModel 决定。

                        // 但是，如果用户点击“库”按钮，且当前焦点在副面板，我们需要导航副面板到库视图。
                        // NavigationModeService 的 SwitchNavigationMode -> HandleLibraryMode 是响应点击。

                        // Trick: 调用 MainWindow 的辅助方法
                        // 移除强制导航副面板到库的逻辑，避免死循环
                        // 副面板应该保持当前状态，除非用户显式在副面板操作
                        // mw.NavigateSecondaryPaneToLibrary(null);
                    }
                    else
                    {
                        // 主面板逻辑
                        if (_uiHelper.CurrentLibrary == null)
                        {
                            Library libraryToSelect = null;
                            if (_configService?.Config.LastLibraryId > 0)
                            {
                                libraryToSelect = _libraryRepository?.GetLibrary(_configService.Config.LastLibraryId);
                            }

                            // If last library not found, pick the first one
                            if (libraryToSelect == null)
                            {
                                libraryToSelect = _libraryRepository?.GetAllLibraries().FirstOrDefault();
                            }

                            if (_uiHelper.CurrentLibrary == libraryToSelect)
                            {
                                // 已经是当前库，仅确保高亮，不重新加载
                                _uiHelper.HighlightMatchingLibrary(libraryToSelect);
                                // 确保不重复触发加载导致取消异常
                                return;
                            }

                            if (libraryToSelect != null)
                            {
                                _uiHelper.CurrentLibrary = libraryToSelect;
                                _uiHelper.EnsureSelectedItemVisible(_uiHelper.LibrariesListBox, libraryToSelect);
                                _uiHelper.HighlightMatchingLibrary(libraryToSelect);
                                _uiHelper.LoadLibraryFiles(libraryToSelect);
                            }
                        }
                        else
                        {
                            // 如果已有当前库，仅高亮它，避免重复加载
                            _uiHelper.HighlightMatchingLibrary(_uiHelper.CurrentLibrary);
                        }
                        _uiHelper.InitializeNavigationPanelDragDrop();
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        /// <summary>
        /// 处理标签模式切换
        /// </summary>
        /// <param name="skipRefresh">是否跳过刷新操作（启动时恢复状态使用）</param>
        private void HandleTagMode(bool skipRefresh = false)
        {
            if (_uiHelper.FileBrowser != null)
            {
                _uiHelper.FileBrowser.TabsVisible = true;
            }

            // 切换到标签模式时，通知 UI 显示标签面板
            if (!skipRefresh)
            {
                _uiHelper.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // NavigationService.SwitchNavigationMode("Tag") handles hiding/showing grids 
                    // like NavPathContent/NavLibraryContent/NavTagContent usually.
                    // But we ensure the side bar is in the right state.

                    // If no path is active, we might want to stay on current path 
                    // but show the tag cloud on the left.
                    _uiHelper.RefreshTagList();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        #endregion

        #region 导航按钮状态更新

        /// <summary>
        /// 更新导航按钮状态
        /// </summary>
        public void UpdateNavigationButtonsState()
        {
            if (_uiHelper.FileBrowser != null)
            {
                _uiHelper.FileBrowser.NavBackEnabled = _navigationService.CanGoBack;
                _uiHelper.FileBrowser.NavForwardEnabled = _navigationService.CanGoForward;

                // 更新“向上”按钮状态
                string currentPath = _navigationService.CurrentPath;
                bool canGoUp = false;
                if (!string.IsNullOrEmpty(currentPath))
                {
                    var protocol = ProtocolManager.Parse(currentPath);
                    if (protocol.Type == ProtocolType.Local)
                    {
                        string dir = null;
                        try { dir = Path.GetDirectoryName(currentPath); } catch { }
                        canGoUp = !string.IsNullOrEmpty(dir);
                    }
                    else if (protocol.Type == ProtocolType.Archive)
                    {
                        // 压缩包内总是可以向上（返回上一级文件夹或返回到文件系统）
                        canGoUp = true;
                    }
                }
                _uiHelper.FileBrowser.NavUpEnabled = canGoUp;
            }
        }

        #endregion
    }
}


