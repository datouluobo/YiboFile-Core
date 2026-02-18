using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using YiboFile;
using YiboFile.Controls;
using YiboFile.Services.Config;
using YiboFile.Services.Tabs;
using YiboFile.Services.Navigation;
using System.Windows.Controls;

namespace YiboFile.Services
{
    /// <summary>
    /// 窗口状态管理器
    /// 统一管理窗口大小、位置、分割线位置、导航位置和标签页状态的保存与恢复
    /// </summary>
    public class WindowStateManager
    {
        #region 私有字段

        private IConfigUIHelper _uiHelper;
        private TabService _tabService;
        private TabService _secondTabService;
        private NavigationService _navigationService;
        private Navigation.NavigationModeService _navigationModeService;
        private Data.Repositories.ILibraryRepository _libraryRepository;

        // 快捷访问单例配置
        private AppConfig _config => ConfigurationService.Instance.Config;

        private bool _isInitialized = false;
        private bool _isApplyingConfig = false; // 用于追踪是否正在恢复状态
        private bool _isTabsRestored = false;   // 用于追踪标签页是否已恢复

        #endregion

        #region 构造函数


        /// <summary>
        /// 初始化窗口状态管理器
        /// </summary>
        public WindowStateManager(IConfigUIHelper uiHelper = null, TabService tabService = null, NavigationService navigationService = null, Navigation.NavigationModeService navigationModeService = null, TabService secondTabService = null, Data.Repositories.ILibraryRepository libraryRepository = null)
        {
            if (uiHelper != null)
            {
                Initialize(uiHelper, tabService, navigationService, navigationModeService, secondTabService, libraryRepository);
            }
            else
            {
                _libraryRepository = libraryRepository ?? App.ServiceProvider?.GetService(typeof(Data.Repositories.ILibraryRepository)) as Data.Repositories.ILibraryRepository;
            }
        }

        /// <summary>
        /// 延迟初始化或重新绑定 UI 上下文
        /// </summary>
        public void Initialize(IConfigUIHelper uiHelper, TabService tabService, NavigationService navigationService = null, Navigation.NavigationModeService navigationModeService = null, TabService secondTabService = null, Data.Repositories.ILibraryRepository libraryRepository = null)
        {
            _uiHelper = uiHelper ?? throw new ArgumentNullException(nameof(uiHelper));
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _secondTabService = secondTabService;
            _navigationService = navigationService;
            _navigationModeService = navigationModeService;
            if (libraryRepository != null) _libraryRepository = libraryRepository;
            _isInitialized = true;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 更新副标签页服务实例（用于动态初始化的双列表模式）
        /// </summary>
        public void SetSecondTabService(TabService service)
        {
            _secondTabService = service;
        }

        /// <summary>
        /// 专门恢复副列表标签页状态
        /// </summary>
        public void RestoreSecondaryTabs()
        {
            if (_secondTabService != null)
            {
                RestoreTabsForService(_secondTabService, _config.OpenTabsSecondary, _config.ActiveTabKeySecondary);
            }
        }

        #endregion

        #region 保存状态

        /// <summary>
        /// 保存所有窗口状态（窗口大小、位置、分割线、导航位置、标签页）
        /// </summary>
        /// <param name="force">是否强制保存（用于程序关闭时，绕过初始化检查）</param>
        public void SaveAllState(bool force = false)
        {
            // 物理状态（窗口尺寸、位置、分割线）可以强制保存，或者是初始化完成后保存
            bool canSavePhysical = force || (_isInitialized && !_isApplyingConfig);

            // 逻辑状态（标签页、导航路径）:
            // - 正常运行期间：需要完全初始化且标签页已恢复
            // - force=true（程序关闭时）：只需已初始化即可，SaveTabsState 内部有空列表防护
            //   不再要求 _isTabsRestored，因为 RestoreTabsState 是通过 Dispatcher.BeginInvoke 延迟执行的，
            //   如果用户快速关闭程序，_isTabsRestored 可能还是 false，导致标签页永远无法保存。
            bool canSaveLogical = force
                ? (_isInitialized && !_isApplyingConfig)
                : (_isInitialized && !_isApplyingConfig && _isTabsRestored);

            if (!canSavePhysical && !canSaveLogical)
            {
                return;
            }

            try
            {
                // ✅ 使用ConfigurationService统一更新
                // 修复 BUG-023: 避免直接修改 _config 属性导致自赋值无效
                YiboFile.Services.Config.ConfigurationService.Instance.Update(latestConfig =>
                {
                    // 仅当允许保存物理状态时才更新这些字段
                    if (canSavePhysical)
                    {
                        // 确保窗口布局已更新 (UI操作需要在UI线程，但Update回调可能在任意线程? 不，Update是同步的)
                        // 注意：Update 回调是在锁内执行的，应避免耗时操作
                        // 但我们需要读取 UI 属性

                        // 最佳实践：先读取 UI 值到局部变量，再传入 Update?
                        // 但由于逻辑复杂，我们保持在 UI 线程调用 SaveAllState，所以直接访问 UI 是安全的
                        // ConfigurationService.Update 是线程安全的锁操作

                        SaveWindowStateTo(latestConfig);
                        SaveSplitterPositionsTo(latestConfig);
                    }

                    // 仅当允许保存逻辑状态时才更新这些字段
                    if (canSaveLogical)
                    {
                        SaveNavigationStateTo(latestConfig);
                        SaveTabsStateTo(latestConfig);
                    }
                });
            }
            catch (Exception)
            {
                // 静默处理错误，避免影响程序关闭
            }
        }

        /// <summary>
        /// 保存窗口状态到目标配置对象
        /// </summary>
        private void SaveWindowStateTo(AppConfig targetConfig)
        {
            var window = _uiHelper.Window;
            if (window == null) return;

            // 确保布局更新
            if (window.IsLoaded) window.UpdateLayout();

            // 保存最大化状态 (如果是最小化，则保持之前的状态，避免覆盖)
            if (window.WindowState != WindowState.Minimized)
            {
                targetConfig.IsMaximized = window.WindowState == WindowState.Maximized;
            }

            // 如果窗口已加载，保存实际尺寸和位置
            if (window.IsLoaded)
            {
                if (!targetConfig.IsMaximized)
                {
                    // 非最大化状态：保存实际尺寸和位置
                    targetConfig.WindowWidth = window.Width;
                    targetConfig.WindowHeight = window.Height;

                    // 确保位置值有效（不是NaN或无效值）
                    if (!double.IsNaN(window.Top) && !double.IsInfinity(window.Top) && window.Top >= -10000)
                    {
                        targetConfig.WindowTop = window.Top;
                    }
                    else
                    {
                        targetConfig.WindowTop = null;
                    }

                    if (!double.IsNaN(window.Left) && !double.IsInfinity(window.Left) && window.Left >= -10000)
                    {
                        targetConfig.WindowLeft = window.Left;
                    }
                    else
                    {
                        targetConfig.WindowLeft = null;
                    }
                }
                else
                {
                    // 最大化状态：保存还原尺寸
                    Rect restoreBounds = window.RestoreBounds;

                    if (restoreBounds.Width > 0 && restoreBounds.Height > 0)
                    {
                        targetConfig.WindowWidth = restoreBounds.Width;
                        targetConfig.WindowHeight = restoreBounds.Height;
                        targetConfig.WindowTop = restoreBounds.Top;
                        targetConfig.WindowLeft = restoreBounds.Left;
                    }
                    else
                    {
                        // 如果RestoreBounds无效，保持原值或使用默认值
                        if (targetConfig.WindowWidth <= 0 || targetConfig.WindowHeight <= 0)
                        {
                            // 使用默认值
                            targetConfig.WindowWidth = 1200;
                            targetConfig.WindowHeight = 800;
                        }
                    }
                }
            }
            else
            {
                // 窗口未加载，使用当前配置值或默认值
                if (!targetConfig.IsMaximized && targetConfig.WindowWidth <= 0)
                {
                    targetConfig.WindowWidth = 1200;
                    targetConfig.WindowHeight = 800;
                }
            }

            // 复制其他属性以保持一致
            targetConfig.WindowOpacity = _config.WindowOpacity; // 这个一般不自动变，或者是绑定的
        }

        // 保留旧方法签名以防兼容性问题，但标记为废弃或重定向
        public void SaveWindowState()
        {
            // 临时适配：调用 Update 来执行保存
            YiboFile.Services.Config.ConfigurationService.Instance.Update(cfg => SaveWindowStateTo(cfg));
        }

        /// <summary>
        /// 保存分割线位置到目标配置
        /// </summary>
        private void SaveSplitterPositionsTo(AppConfig targetConfig)
        {
            // 正在应用配置时不保存分割线位置
            if (!_isInitialized || _isApplyingConfig)
            {
                return;
            }

            if (_uiHelper.RootGrid == null || !_uiHelper.RootGrid.IsLoaded) return;

            var leftCol = _uiHelper.ColLeft;
            var middleCol = _uiHelper.ColCenter;

            double leftWidth = 0;
            double middleWidth = 0;

            // GridSplitter拖拽后，列宽已调整，优先使用ActualWidth获取实际显示的宽度
            // 外部已调用 UpdateLayout，这里直接读取

            if (leftCol.ActualWidth > 0)
            {
                leftWidth = leftCol.ActualWidth;
            }

            if (middleCol.ActualWidth > 0)
            {
                middleWidth = middleCol.ActualWidth;
            }

            // 保存有效的宽度值（必须大于最小宽度）
            if (leftWidth > 0 && leftWidth >= leftCol.MinWidth)
            {
                targetConfig.LeftPanelWidth = leftWidth;
                targetConfig.ColLeftWidth = leftWidth;
            }
            if (middleWidth > 0 && middleWidth >= middleCol.MinWidth)
            {
                targetConfig.MiddlePanelWidth = middleWidth;
                targetConfig.ColCenterWidth = middleWidth;
            }

            // 新增：保存右侧列宽度
            var rightCol = _uiHelper.ColRight;
            double rightWidth = rightCol.ActualWidth;
            if (rightWidth > 0 && rightWidth >= rightCol.MinWidth)
            {
                targetConfig.ColRightWidth = rightWidth;
            }

            // --- 新增：保存扩展 UI 状态 ---

            // 1. 保存右侧面板可见性
            targetConfig.IsRightPanelVisible = _uiHelper.ColRight.ActualWidth > 10;

            // 2. 保存右侧面板内部高度 (备注区)
            if (_uiHelper.RightPanelControl != null)
            {
                if (_uiHelper.RightPanelControl.Content is System.Windows.Controls.Grid rightRootGrid)
                {
                    if (rightRootGrid.RowDefinitions.Count > 3)
                    {
                        var notesRow = rightRootGrid.RowDefinitions[3]; // Row 3 is Notes
                        if (notesRow.Height.IsAbsolute)
                        {
                            targetConfig.RightPanelNotesHeight = notesRow.Height.Value;
                        }
                    }
                }
            }

            // 3. 保存中间面板底部高度 (文件详情区)
            if (_uiHelper.FileBrowser?.Content is System.Windows.Controls.Grid fileBrowserGrid)
            {
                if (fileBrowserGrid.RowDefinitions.Count >= 4)
                {
                    var lastRow = fileBrowserGrid.RowDefinitions[fileBrowserGrid.RowDefinitions.Count - 1];
                    if (lastRow.Height.IsAbsolute)
                    {
                        targetConfig.CenterPanelInfoHeight = lastRow.Height.Value;
                    }
                }
            }

            // 保存双列表模式
            targetConfig.IsDualListMode = _config.IsDualListMode;
        }

        private void SaveSplitterPositions()
        {
            YiboFile.Services.Config.ConfigurationService.Instance.Update(cfg => SaveSplitterPositionsTo(cfg));
        }

        /// <summary>
        /// 保存导航状态（当前路径、导航模式、库ID）
        /// </summary>
        /// <summary>
        /// 保存导航状态到目标配置
        /// </summary>
        private void SaveNavigationStateTo(AppConfig targetConfig)
        {
            targetConfig.LastPath = _uiHelper.CurrentPath ?? string.Empty;

            // 保存导航模式：优先从配置中获取（NavigationModeService 在切换时会保存）
            // 如果配置中没有，尝试从当前活动标签页推断
            if (string.IsNullOrEmpty(targetConfig.LastNavigationMode))
            {
                if (_tabService != null)
                {
                    var activeTab = _tabService.ActiveTab;
                    if (activeTab != null)
                    {
                        switch (activeTab.Type)
                        {
                            case TabType.Library:
                                targetConfig.LastNavigationMode = "Library";
                                break;
                            default:
                                targetConfig.LastNavigationMode = "Path";
                                break;
                        }
                    }
                    else
                    {
                        targetConfig.LastNavigationMode = "Path";
                    }
                }
                else
                {
                    targetConfig.LastNavigationMode = "Path";
                }
            }
            // 如果配置中已有导航模式，保持它（NavigationModeService 已经更新过）

            var currentLibrary = _uiHelper.CurrentLibrary;
            if (currentLibrary != null)
            {
                var libraryIdProperty = currentLibrary.GetType().GetProperty("Id");
                if (libraryIdProperty != null)
                {
                    var libraryId = libraryIdProperty.GetValue(currentLibrary);
                    if (libraryId is int id)
                    {
                        targetConfig.LastLibraryId = id;
                    }
                    else
                    {
                        targetConfig.LastLibraryId = 0;
                    }
                }
                else
                {
                    targetConfig.LastLibraryId = 0;
                }
            }
            else
            {
                targetConfig.LastLibraryId = 0;
            }
        }

        private void SaveNavigationState()
        {
            YiboFile.Services.Config.ConfigurationService.Instance.Update(cfg => SaveNavigationStateTo(cfg));
        }

        /// <summary>
        /// 保存标签页状态（所有打开的标签页和活动标签页）
        /// 注意：不再在此处检查 _isTabsRestored，由调用方 SaveAllState 控制时机。
        /// 内部通过 tabs.Count > 0 防护，确保空标签页列表不会覆盖有效的配置数据。
        /// </summary>
        /// <summary>
        /// 保存标签页状态到目标配置
        /// </summary>
        private void SaveTabsStateTo(AppConfig targetConfig)
        {
            if (_tabService != null)
            {
                var (tabs, activeKey) = GetTabsState(_tabService);

                // 防护：如果当前没有标签页但配置中有，说明还没恢复完成，不覆盖
                // 注意：这里我们比较 targetConfig.OpenTabs，它是最新的配置状态
                if (tabs.Count > 0 || targetConfig.OpenTabs == null || targetConfig.OpenTabs.Count == 0)
                {
                    targetConfig.OpenTabs = tabs;
                    targetConfig.ActiveTabKey = activeKey;
                }
            }

            if (_secondTabService != null)
            {
                var (tabs, activeKey) = GetTabsState(_secondTabService);
                if (tabs.Count > 0 || targetConfig.OpenTabsSecondary == null || targetConfig.OpenTabsSecondary.Count == 0)
                {
                    targetConfig.OpenTabsSecondary = tabs;
                    targetConfig.ActiveTabKeySecondary = activeKey;
                }
            }
        }

        private void SaveTabsState()
        {
            YiboFile.Services.Config.ConfigurationService.Instance.Update(cfg => SaveTabsStateTo(cfg));
        }

        private (List<string> tabs, string activeKey) GetTabsState(TabService service)
        {
            var orderedTabs = service.GetTabsInOrder();

            var tabs = orderedTabs.Select(tab => GetTabKey(tab)).ToList();
            var activeKey = string.Empty;

            var activeTab = service.ActiveTab;
            if (activeTab != null)
            {
                activeKey = GetTabKey(activeTab);
            }

            return (tabs, activeKey);
        }

        /// <summary>
        /// 获取标签页的键值
        /// </summary>
        private string GetTabKey(PathTab tab)
        {
            if (tab == null) return string.Empty;

            switch (tab.Type)
            {
                case TabType.Tag:
                case TabType.Search:
                case TabType.Path:
                    return "path:" + (tab.Path ?? string.Empty);
                case TabType.Library:
                    return "library:" + (tab.Library?.Id.ToString() ?? "");

                default:
                    return "unknown:" + (tab.Title ?? "");
            }
        }

        #endregion

        #region 恢复状态

        /// <summary>
        /// 恢复所有窗口状态
        /// </summary>
        public void RestoreAllState()
        {
            try
            {
                RestoreWindowState();
                RestoreSplitterPositions();
                // 导航状态和标签页状态在 MainWindowInitializer 中恢复
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 恢复窗口状态（大小、位置、最大化状态）
        /// </summary>
        private void RestoreWindowState()
        {
            var window = _uiHelper.Window;
            if (window == null || _config == null) return;

            _isApplyingConfig = true;
            try
            {
                if (_config.IsMaximized)
                {
                    window.WindowState = WindowState.Maximized;
                    _uiHelper.UpdateWindowStateUI();
                }
                else
                {
                    ApplyNonMaximizedWindowState(window, _config);
                }

                // 应用窗口透明度
                if (_config.WindowOpacity > 0 && _config.WindowOpacity <= 1.0)
                {
                    window.Opacity = _config.WindowOpacity;
                }
            }
            finally
            {
                _isApplyingConfig = false;
            }
        }

        private void ApplyNonMaximizedWindowState(System.Windows.Window window, AppConfig cfg)
        {
            window.WindowState = WindowState.Normal;
            if (cfg.WindowWidth > 0) window.Width = cfg.WindowWidth;
            if (cfg.WindowHeight > 0) window.Height = cfg.WindowHeight;
            if (cfg.WindowTop.HasValue && cfg.WindowTop.Value >= -10000) window.Top = cfg.WindowTop.Value;
            if (cfg.WindowLeft.HasValue && cfg.WindowLeft.Value >= -10000) window.Left = cfg.WindowLeft.Value;

            window.ResizeMode = ResizeMode.CanResize;
        }

        /// <summary>
        /// 恢复分割线位置（列宽度）
        /// </summary>
        private void RestoreSplitterPositions()
        {
            if (_uiHelper.RootGrid == null || _config == null) return;

            _isApplyingConfig = true;
            try
            {
                // 应用左中右三列宽度
                var leftWidth = _config.ColLeftWidth > 0 ? _config.ColLeftWidth : _config.LeftPanelWidth;
                var rightWidth = _config.ColRightWidth > 0 ? _config.ColRightWidth : 360;

                if (leftWidth > 0)
                {
                    _uiHelper.ColLeft.Width = new GridLength(Math.Max(_uiHelper.ColLeft.MinWidth, leftWidth));
                }

                // 中间列固定为自适应 (Gap Fix)
                _uiHelper.ColCenter.Width = new GridLength(1, GridUnitType.Star);

                if (rightWidth > 0)
                {
                    _uiHelper.ColRight.Width = new GridLength(Math.Max(_uiHelper.ColRight.MinWidth, rightWidth));
                }

                // 恢复右侧面板可见性
                if (!_config.IsRightPanelVisible)
                {
                    _uiHelper.ColRight.Width = new GridLength(0);
                }

                // 恢复详细高度
                if (_config.RightPanelNotesHeight > 0 && _uiHelper.RightPanelControl?.Content is Grid rightGrid && rightGrid.RowDefinitions.Count > 3)
                {
                    rightGrid.RowDefinitions[3].Height = new GridLength(_config.RightPanelNotesHeight);
                }

                if (_config.CenterPanelInfoHeight > 0 && _uiHelper.FileBrowser?.Content is Grid browserGrid && browserGrid.RowDefinitions.Count >= 4)
                {
                    browserGrid.RowDefinitions[browserGrid.RowDefinitions.Count - 1].Height = new GridLength(_config.CenterPanelInfoHeight);
                }

                _uiHelper.RootGrid.UpdateLayout();
            }
            finally
            {
                _isApplyingConfig = false;
            }
        }


        /// <summary>
        /// 恢复标签页状态
        /// </summary>
        public void RestoreTabsState()
        {
            if (_tabService == null || _config == null) return;

            var window = _uiHelper.Window;
            if (window == null) return;

            // 标记初始化完成
            Action markInitialized = () =>
            {
                _isInitialized = true;
                _isTabsRestored = true;
            };

            if (!window.IsLoaded)
            {
                window.Loaded += (s, e) =>
                {
                    window.Dispatcher.BeginInvoke(() =>
                    {
                        RestoreTabsStateInternal();
                        markInitialized();
                    }, System.Windows.Threading.DispatcherPriority.Loaded);
                };
                return;
            }

            window.Dispatcher.BeginInvoke(() =>
            {
                RestoreTabsStateInternal();
                markInitialized();
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 内部恢复标签页状态实现
        /// </summary>
        private void RestoreTabsStateInternal()
        {
            try
            {
                // 恢复主列表标签页
                RestoreTabsForService(_tabService, _config.OpenTabs, _config.ActiveTabKey);

                // 恢复副列表标签页
                if (_secondTabService != null)
                {
                    RestoreTabsForService(_secondTabService, _config.OpenTabsSecondary, _config.ActiveTabKeySecondary);
                }
            }
            catch (Exception)
            {
            }
        }

        private void RestoreTabsForService(TabService service, List<string> openTabs, string activeTabKey)
        {
            if (service == null) return;

            // 恢复保存的标签页状态
            if (openTabs != null && openTabs.Count > 0)
            {
                // 恢复所有标签页
                foreach (var tabKey in openTabs)
                {
                    if (string.IsNullOrEmpty(tabKey)) continue;

                    try
                    {
                        RestoreTabFromKey(service, tabKey);
                    }
                    catch (Exception)
                    {
                        // 单个标签页恢复失败不影响其他标签页
                    }
                }

                // 恢复活动标签页
                if (!string.IsNullOrEmpty(activeTabKey))
                {
                    var activeTab = FindTabByKey(service, activeTabKey);
                    if (activeTab != null)
                    {
                        service.SwitchToTab(activeTab);
                    }
                    else if (service.Tabs != null && service.Tabs.Count > 0)
                    {
                        // 如果找不到活动标签页，但有其他标签页，切换到第一个
                        var firstTab = service.Tabs.First();
                        service.SwitchToTab(firstTab);
                    }
                }
                else if (service.Tabs != null && service.Tabs.Count > 0)
                {
                    // 如果没有保存活动标签页，但恢复了标签页，切换到第一个
                    var firstTab = service.Tabs.First();
                    service.SwitchToTab(firstTab);
                }
            }
            else
            {
                // 如果没有保存的标签页，创建默认标签页
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktopPath))
                {
                    service.CreatePathTab(desktopPath, false);
                }
            }
        }

        /// <summary>
        /// 从键值恢复标签页
        /// </summary>
        private void RestoreTabFromKey(TabService service, string tabKey)
        {
            if (string.IsNullOrEmpty(tabKey)) return;

            if (tabKey.StartsWith("path:"))
            {
                var path = tabKey.Substring("path:".Length);
                if (!string.IsNullOrEmpty(path))
                {
                    // 恢复模式：先检查是否已存在相同路径的标签页，避免重复创建
                    var existingTab = service.FindTabByPath(path);
                    if (existingTab != null)
                    {
                        // 如果已存在，切换到该标签页即可
                        service.SwitchToTab(existingTab);
                        return;
                    }

                    // 搜索标签页的路径格式是 "search://keyword"
                    // 对于恢复模式，即使路径暂时不存在也尝试创建标签页（跳过验证）
                    // 这样可以恢复网络路径、USB设备等可能暂时不可用的路径
                    // ValidatePath 已经支持 search:// 路径，可以直接调用 CreatePathTab
                    // 搜索标签页会在切换到该标签页时自动刷新（通过MainWindow的CheckAndRefreshSearchTab）
                    if (path.StartsWith("search://") || path.StartsWith("content://"))
                    {
                        service.CreatePathTab(path, true, skipValidation: true, activate: false);
                    }
                    else if (path.StartsWith("tag://"))
                    {
                        // 增加对 tag:// 协议的支持
                        service.CreatePathTab(path, true, skipValidation: true, activate: false);
                    }
                    else if (System.IO.Path.IsPathRooted(path) || (path.Length >= 2 && path[1] == ':'))
                    {
                        // 对于有效路径格式（绝对路径或驱动器路径），即使暂时不存在也尝试恢复（跳过验证）
                        // 这样可以恢复网络路径、USB设备等可能暂时不可用的路径
                        service.CreatePathTab(path, true, skipValidation: true, activate: false);
                    }
                    else if (Directory.Exists(path))
                    {
                        // 对于相对路径，只有在存在时才恢复
                        service.CreatePathTab(path, true, skipValidation: false, activate: false);
                    }
                }
            }
            else if (tabKey.StartsWith("library:"))
            {
                var libraryIdStr = tabKey.Substring("library:".Length);
                if (int.TryParse(libraryIdStr, out int libraryId))
                {
                    var library = _libraryRepository?.GetLibrary(libraryId);
                    if (library != null)
                    {
                        service.OpenLibraryTab(library, false, activate: false); // 允许复用已存在的标签页
                    }
                }
            }
        }
        /// <summary>
        /// 根据键值查找标签页
        /// </summary>
        private PathTab FindTabByKey(TabService service, string tabKey)
        {
            if (string.IsNullOrEmpty(tabKey)) return null;

            if (tabKey.StartsWith("path:"))
            {
                var path = tabKey.Substring("path:".Length);
                return service.FindTabByPath(path);
            }
            else if (tabKey.StartsWith("library:"))
            {
                var libraryIdStr = tabKey.Substring("library:".Length);
                if (int.TryParse(libraryIdStr, out int libraryId))
                {
                    return service.FindTabByLibraryId(libraryId);
                }
            }

            return null;
        }

        #endregion
    }
}
