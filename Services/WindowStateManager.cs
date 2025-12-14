using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using OoiMRR.Services.Config;
using OoiMRR.Services.Tabs;
using OoiMRR.Services.Navigation;

namespace OoiMRR.Services
{
    /// <summary>
    /// 窗口状态管理器
    /// 统一管理窗口大小、位置、分割线位置、导航位置和标签页状态的保存与恢复
    /// </summary>
    public class WindowStateManager
    {
        #region 私有字段

        private readonly IConfigUIHelper _uiHelper;
        private readonly TabService _tabService;
        private readonly ConfigService _configService;
        private readonly AppConfig _config;
        private readonly NavigationService _navigationService;
        private readonly Navigation.NavigationModeService _navigationModeService;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化窗口状态管理器
        /// </summary>
        public WindowStateManager(IConfigUIHelper uiHelper, TabService tabService, ConfigService configService, AppConfig config, NavigationService navigationService = null, Navigation.NavigationModeService navigationModeService = null)
        {
            _uiHelper = uiHelper ?? throw new ArgumentNullException(nameof(uiHelper));
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _navigationService = navigationService;
            _navigationModeService = navigationModeService;
        }

        #endregion

        #region 保存状态

        /// <summary>
        /// 保存所有窗口状态（窗口大小、位置、分割线、导航位置、标签页）
        /// </summary>
        public void SaveAllState()
        {
            try
            {
                // #region agent log
                var logPath = @"f:\Download\GitHub\OoiMRR\.cursor\debug.log";
                try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)); System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:51", message = "SaveAllState开始", data = new { windowLoaded = _uiHelper?.Window?.IsLoaded ?? false }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
                
                var window = _uiHelper.Window;
                if (window != null && window.IsLoaded)
                {
                    // 确保窗口布局已更新
                    window.UpdateLayout();
                }

                SaveWindowState();
                SaveSplitterPositions();
                SaveNavigationState();
                SaveTabsState();
                
                // #region agent log
                try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:68", message = "SaveAllState保存前_config状态", data = new { windowWidth = _config.WindowWidth, windowHeight = _config.WindowHeight, windowTop = _config.WindowTop, windowLeft = _config.WindowLeft, isMaximized = _config.IsMaximized, colLeftWidth = _config.ColLeftWidth, colCenterWidth = _config.ColCenterWidth, openTabsCount = _config.OpenTabs?.Count ?? 0, activeTabKey = _config.ActiveTabKey }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
                
                // 保存配置
                ConfigManager.Save(_config);
                
                // #region agent log
                try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:72", message = "SaveAllState完成", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
            }
            catch (Exception ex)
            {
                // 静默处理错误，避免影响程序关闭
                System.Diagnostics.Debug.WriteLine($"保存窗口状态失败: {ex.Message}");
                // #region agent log
                try { var logPathErr = @"f:\Download\GitHub\OoiMRR\.cursor\debug.log"; System.IO.File.AppendAllText(logPathErr, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:77", message = "SaveAllState异常", data = new { error = ex.Message, stackTrace = ex.StackTrace }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
            }
        }

        /// <summary>
        /// 保存窗口状态（大小、位置、最大化状态）
        /// </summary>
        private void SaveWindowState()
        {
            // #region agent log
            var logPath = @"f:\Download\GitHub\OoiMRR\.cursor\debug.log";
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)); System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:80", message = "SaveWindowState开始", data = new { windowIsNull = _uiHelper?.Window == null }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion
            
            var window = _uiHelper.Window;
            if (window == null) return;

            // 保存最大化状态
            _config.IsMaximized = _uiHelper.IsPseudoMaximized;
            
            // #region agent log
            try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:88", message = "SaveWindowState窗口属性", data = new { isLoaded = window.IsLoaded, isMaximized = _config.IsMaximized, windowWidth = window.Width, windowHeight = window.Height, windowTop = window.Top, windowLeft = window.Left }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion
            
            // 如果窗口已加载，保存实际尺寸和位置
            if (window.IsLoaded)
            {
                if (!_config.IsMaximized)
                {
                    // 非最大化状态：保存实际尺寸和位置
                    _config.WindowWidth = window.Width;
                    _config.WindowHeight = window.Height;
                    
                    // 确保位置值有效（不是NaN或无效值）
                    if (!double.IsNaN(window.Top) && !double.IsInfinity(window.Top) && window.Top >= 0)
                    {
                        _config.WindowTop = window.Top;
                    }
                    else
                    {
                        _config.WindowTop = 0; // 如果无效，使用默认值0
                    }
                    
                    if (!double.IsNaN(window.Left) && !double.IsInfinity(window.Left) && window.Left >= 0)
                    {
                        _config.WindowLeft = window.Left;
                    }
                    else
                    {
                        _config.WindowLeft = 0; // 如果无效，使用默认值0
                    }
                    
                    // 同时更新 RestoreBounds，以便下次最大化时使用
                    _uiHelper.RestoreBounds = new Rect(_config.WindowLeft, _config.WindowTop, _config.WindowWidth, _config.WindowHeight);
                    
                    // #region agent log
                    try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:100", message = "SaveWindowState保存非最大化状态", data = new { savedWidth = _config.WindowWidth, savedHeight = _config.WindowHeight, savedTop = _config.WindowTop, savedLeft = _config.WindowLeft, windowTop = window.Top, windowLeft = window.Left }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                    // #endregion
                    
                    System.Diagnostics.Debug.WriteLine($"[WindowStateManager] 保存窗口状态: {window.Width}x{window.Height} @ ({window.Left}, {window.Top})");
                }
                else
                {
                    // 最大化状态：保存还原尺寸（从 RestoreBounds 获取）
                    var restoreBounds = _uiHelper.RestoreBounds;
                    // #region agent log
                    try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:107", message = "SaveWindowState最大化状态RestoreBounds", data = new { restoreBoundsWidth = restoreBounds.Width, restoreBoundsHeight = restoreBounds.Height, restoreBoundsTop = restoreBounds.Top, restoreBoundsLeft = restoreBounds.Left }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                    // #endregion
                    
                    if (restoreBounds.Width > 0 && restoreBounds.Height > 0)
                    {
                        _config.WindowWidth = restoreBounds.Width;
                        _config.WindowHeight = restoreBounds.Height;
                        _config.WindowTop = restoreBounds.Top;
                        _config.WindowLeft = restoreBounds.Left;
                        System.Diagnostics.Debug.WriteLine($"[WindowStateManager] 保存窗口状态(最大化): 还原尺寸 {restoreBounds.Width}x{restoreBounds.Height} @ ({restoreBounds.Left}, {restoreBounds.Top})");
                    }
                    else
                    {
                        // 如果RestoreBounds无效，尝试使用配置中的值
                        if (_config.WindowWidth > 0 && _config.WindowHeight > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WindowStateManager] 使用配置中的窗口尺寸: {_config.WindowWidth}x{_config.WindowHeight}");
                        }
                        else
                        {
                            // 使用默认值
                            _config.WindowWidth = 1200;
                            _config.WindowHeight = 800;
                            System.Diagnostics.Debug.WriteLine($"[WindowStateManager] 使用默认窗口尺寸: 1200x800");
                        }
                    }
                }
            }
            else
            {
                // #region agent log
                try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:133", message = "SaveWindowState窗口未加载", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
                
                // 窗口未加载，使用当前配置值或默认值
                if (!_config.IsMaximized && _config.WindowWidth <= 0)
                {
                    _config.WindowWidth = 1200;
                    _config.WindowHeight = 800;
                }
            }
        }

        /// <summary>
        /// 保存分割线位置（列宽度）
        /// </summary>
        private void SaveSplitterPositions()
        {
            // #region agent log
            var logPath = @"f:\Download\GitHub\OoiMRR\.cursor\debug.log";
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)); System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "WindowStateManager.cs:147", message = "SaveSplitterPositions开始", data = new { rootGridIsNull = _uiHelper?.RootGrid == null, rootGridIsLoaded = _uiHelper?.RootGrid?.IsLoaded ?? false }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion
            
            if (_uiHelper.RootGrid == null || !_uiHelper.RootGrid.IsLoaded) return;

            var leftCol = _uiHelper.RootGrid.ColumnDefinitions[0];
            var middleCol = _uiHelper.RootGrid.ColumnDefinitions[2];

            double leftWidth = 0;
            double middleWidth = 0;

            // #region agent log
            try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "WindowStateManager.cs:159", message = "SaveSplitterPositions列宽度属性", data = new { leftColWidthIsAbsolute = leftCol.Width.IsAbsolute, leftColWidthValue = leftCol.Width.IsAbsolute ? leftCol.Width.Value : 0, leftColActualWidth = leftCol.ActualWidth, leftColMinWidth = leftCol.MinWidth, middleColWidthIsAbsolute = middleCol.Width.IsAbsolute, middleColWidthValue = middleCol.Width.IsAbsolute ? middleCol.Width.Value : 0, middleColWidthIsStar = middleCol.Width.IsStar, middleColActualWidth = middleCol.ActualWidth, middleColMinWidth = middleCol.MinWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion

            // 优先使用 Width.Value（如果IsAbsolute），因为这是用户拖拽设置的值
            // GridSplitter拖拽时直接设置 Width = new GridLength(newW)，所以应该从Width获取
            if (leftCol.Width.IsAbsolute && leftCol.Width.Value > 0)
            {
                leftWidth = leftCol.Width.Value;
            }
            else
            {
                // 如果Width不是绝对宽度（可能是Star），使用ActualWidth
                // 强制更新布局以确保ActualWidth是最新的
                _uiHelper.RootGrid.UpdateLayout();
                if (leftCol.ActualWidth > 0)
                {
                    leftWidth = leftCol.ActualWidth;
                }
            }

            if (middleCol.Width.IsAbsolute && middleCol.Width.Value > 0)
            {
                middleWidth = middleCol.Width.Value;
            }
            else if (middleCol.Width.IsStar)
            {
                // 中间列可能是Star模式，使用ActualWidth
                _uiHelper.RootGrid.UpdateLayout();
                if (middleCol.ActualWidth > 0)
                {
                    middleWidth = middleCol.ActualWidth;
                }
            }
            else if (middleCol.ActualWidth > 0)
            {
                middleWidth = middleCol.ActualWidth;
            }

            // #region agent log
            try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "WindowStateManager.cs:192", message = "SaveSplitterPositions计算后的宽度", data = new { leftWidth = leftWidth, middleWidth = middleWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion

            // 保存有效的宽度值（必须大于最小宽度）
            if (leftWidth > 0 && leftWidth >= leftCol.MinWidth) 
            {
                _config.LeftPanelWidth = leftWidth;
                _config.ColLeftWidth = leftWidth; // 同时更新新字段名
                // #region agent log
                try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "WindowStateManager.cs:197", message = "SaveSplitterPositions保存左侧宽度", data = new { savedLeftWidth = _config.ColLeftWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
                System.Diagnostics.Debug.WriteLine($"[WindowStateManager] 保存左侧列宽度: {leftWidth}");
            }
            if (middleWidth > 0 && middleWidth >= middleCol.MinWidth) 
            {
                _config.MiddlePanelWidth = middleWidth;
                _config.ColCenterWidth = middleWidth; // 同时更新新字段名
                // #region agent log
                try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "WindowStateManager.cs:204", message = "SaveSplitterPositions保存中间宽度", data = new { savedMiddleWidth = _config.ColCenterWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
                System.Diagnostics.Debug.WriteLine($"[WindowStateManager] 保存中间列宽度: {middleWidth}");
            }
        }

        /// <summary>
        /// 保存导航状态（当前路径、导航模式、库ID）
        /// </summary>
        private void SaveNavigationState()
        {
            _config.LastPath = _uiHelper.CurrentPath ?? string.Empty;

            // 保存导航模式：优先从配置中获取（NavigationModeService 在切换时会保存）
            // 如果配置中没有，尝试从当前活动标签页推断
            if (string.IsNullOrEmpty(_config.LastNavigationMode))
            {
                if (_tabService != null)
                {
                    var activeTab = _tabService.ActiveTab;
                    if (activeTab != null)
                    {
                        switch (activeTab.Type)
                        {
                            case TabType.Library:
                                _config.LastNavigationMode = "Library";
                                break;
                            case TabType.Tag:
                                _config.LastNavigationMode = "Tag";
                                break;
                            default:
                                _config.LastNavigationMode = "Path";
                                break;
                        }
                    }
                    else
                    {
                        _config.LastNavigationMode = "Path";
                    }
                }
                else
                {
                    _config.LastNavigationMode = "Path";
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
                        _config.LastLibraryId = id;
                    }
                    else
                    {
                        _config.LastLibraryId = 0;
                    }
                }
                else
                {
                    _config.LastLibraryId = 0;
                }
            }
            else
            {
                _config.LastLibraryId = 0;
            }
        }

        /// <summary>
        /// 保存标签页状态（所有打开的标签页和活动标签页）
        /// </summary>
        private void SaveTabsState()
        {
            // #region agent log
            var logPath2 = @"f:\Download\GitHub\OoiMRR\.cursor\debug.log";
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath2)); System.IO.File.AppendAllText(logPath2, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "C", location = "WindowStateManager.cs:278", message = "SaveTabsState开始", data = new { tabServiceIsNull = _tabService == null }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion
            
            if (_tabService == null) return;

            var allTabs = _tabService.Tabs;
            var orderedTabs = _tabService.GetTabsInOrder();

            // #region agent log
            try { System.IO.File.AppendAllText(logPath2, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "C", location = "WindowStateManager.cs:285", message = "SaveTabsState标签页列表", data = new { allTabsCount = allTabs?.Count ?? 0, orderedTabsCount = orderedTabs?.Count() ?? 0, orderedTabsKeys = orderedTabs?.Select(tab => GetTabKey(tab)).ToList() ?? new List<string>() }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion

            _config.OpenTabs = orderedTabs.Select(tab => GetTabKey(tab)).ToList();
            
            var activeTab = _tabService.ActiveTab;
            if (activeTab != null)
            {
                _config.ActiveTabKey = GetTabKey(activeTab);
            }
            else
            {
                _config.ActiveTabKey = string.Empty;
            }
            
            // #region agent log
            try { System.IO.File.AppendAllText(logPath2, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "C", location = "WindowStateManager.cs:296", message = "SaveTabsState保存后", data = new { openTabsCount = _config.OpenTabs?.Count ?? 0, openTabs = _config.OpenTabs ?? new List<string>(), activeTabKey = _config.ActiveTabKey }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion
        }

        /// <summary>
        /// 获取标签页的键值
        /// </summary>
        private string GetTabKey(PathTab tab)
        {
            if (tab == null) return string.Empty;

            switch (tab.Type)
            {
                case TabType.Path:
                    return "path:" + (tab.Path ?? string.Empty);
                case TabType.Library:
                    return "library:" + (tab.Library?.Id.ToString() ?? "");
                case TabType.Tag:
                    return "tag:" + tab.TagId.ToString();
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复窗口状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复窗口状态（大小、位置、最大化状态）
        /// </summary>
        private void RestoreWindowState()
        {
            // 窗口状态由 ConfigService.ApplyConfig 处理
            // 这里不需要重复处理
        }

        /// <summary>
        /// 恢复分割线位置（列宽度）
        /// </summary>
        private void RestoreSplitterPositions()
        {
            // 分割线位置由 ConfigService.ApplyConfig 处理
            // 这里不需要重复处理
        }

        /// <summary>
        /// 恢复标签页状态
        /// </summary>
        public void RestoreTabsState()
        {
            if (_tabService == null || _config == null) return;

            var window = _uiHelper.Window;
            if (window == null) return;

            // 如果窗口还未加载，等待窗口加载完成后再恢复
            if (!window.IsLoaded)
            {
                window.Loaded += (s, e) =>
                {
                    // 延迟到下一个Dispatcher优先级，确保所有控件都已加载
                    window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        RestoreTabsStateInternal();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                };
                return;
            }

            // 如果窗口已加载，延迟执行以确保控件已完全初始化
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                RestoreTabsStateInternal();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// 内部恢复标签页状态实现
        /// </summary>
        private void RestoreTabsStateInternal()
        {
            try
            {
                // 恢复保存的标签页状态
                if (_config.OpenTabs != null && _config.OpenTabs.Count > 0)
                {
                    // 恢复所有标签页
                    foreach (var tabKey in _config.OpenTabs)
                    {
                        if (string.IsNullOrEmpty(tabKey)) continue;

                        try
                        {
                            RestoreTabFromKey(tabKey);
                        }
                        catch (Exception ex)
                        {
                            // 单个标签页恢复失败不影响其他标签页
                            System.Diagnostics.Debug.WriteLine($"恢复标签页失败 {tabKey}: {ex.Message}");
                        }
                    }

                    // 恢复活动标签页
                    if (!string.IsNullOrEmpty(_config.ActiveTabKey))
                    {
                        var activeTab = FindTabByKey(_config.ActiveTabKey);
                        if (activeTab != null)
                        {
                            _tabService.SwitchToTab(activeTab);
                        }
                    }
                }
                else
                {
                    // 如果没有保存的标签页，创建默认标签页
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    if (Directory.Exists(desktopPath))
                    {
                        _tabService?.CreatePathTab(desktopPath, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"恢复标签页状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从键值恢复标签页
        /// </summary>
        private void RestoreTabFromKey(string tabKey)
        {
            if (string.IsNullOrEmpty(tabKey)) return;

            if (tabKey.StartsWith("path:"))
            {
                var path = tabKey.Substring("path:".Length);
                if (!string.IsNullOrEmpty(path))
                {
                    // 恢复模式：先检查是否已存在相同路径的标签页，避免重复创建
                    var existingTab = _tabService.FindTabByPath(path);
                    if (existingTab != null)
                    {
                        // 如果已存在，切换到该标签页即可
                        _tabService.SwitchToTab(existingTab);
                        return;
                    }

                    // 搜索标签页的路径格式是 "search://keyword"
                    // 对于恢复模式，即使路径暂时不存在也尝试创建标签页（跳过验证）
                    // 这样可以恢复网络路径、USB设备等可能暂时不可用的路径
                    // ValidatePath 已经支持 search:// 路径，可以直接调用 CreatePathTab
                    // 搜索标签页会在切换到该标签页时自动刷新（通过MainWindow的CheckAndRefreshSearchTab）
                    if (path.StartsWith("search://"))
                    {
                        _tabService.CreatePathTab(path, true, skipValidation: true);
                    }
                    else if (System.IO.Path.IsPathRooted(path) || (path.Length >= 2 && path[1] == ':'))
                    {
                        // 对于有效路径格式（绝对路径或驱动器路径），即使暂时不存在也尝试恢复（跳过验证）
                        // 这样可以恢复网络路径、USB设备等可能暂时不可用的路径
                        _tabService.CreatePathTab(path, true, skipValidation: true);
                    }
                    else if (Directory.Exists(path))
                    {
                        // 对于相对路径，只有在存在时才恢复
                        _tabService.CreatePathTab(path, true, skipValidation: false);
                    }
                }
            }
            else if (tabKey.StartsWith("library:"))
            {
                var libraryIdStr = tabKey.Substring("library:".Length);
                if (int.TryParse(libraryIdStr, out int libraryId))
                {
                    var library = DatabaseManager.GetLibrary(libraryId);
                    if (library != null)
                    {
                        _tabService.OpenLibraryTab(library, true);
                    }
                }
            }
            else if (tabKey.StartsWith("tag:"))
            {
                var tagIdStr = tabKey.Substring("tag:".Length);
                if (int.TryParse(tagIdStr, out int tagId))
                {
                    // 使用OoiMRRIntegration获取标签名称
                    var tagName = OoiMRRIntegration.GetTagName(tagId);
                    if (!string.IsNullOrEmpty(tagName))
                    {
                        var tag = new OoiMRR.Tag { Id = tagId, Name = tagName };
                        _tabService.OpenTagTab(tag, true);
                    }
                }
            }
        }

        /// <summary>
        /// 根据键值查找标签页
        /// </summary>
        private PathTab FindTabByKey(string tabKey)
        {
            if (string.IsNullOrEmpty(tabKey)) return null;

            if (tabKey.StartsWith("path:"))
            {
                var path = tabKey.Substring("path:".Length);
                return _tabService.FindTabByPath(path);
            }
            else if (tabKey.StartsWith("library:"))
            {
                var libraryIdStr = tabKey.Substring("library:".Length);
                if (int.TryParse(libraryIdStr, out int libraryId))
                {
                    return _tabService.FindTabByLibraryId(libraryId);
                }
            }
            else if (tabKey.StartsWith("tag:"))
            {
                var tagIdStr = tabKey.Substring("tag:".Length);
                if (int.TryParse(tagIdStr, out int tagId))
                {
                    return _tabService.FindTabByTagId(tagId);
                }
            }

            return null;
        }

        #endregion
    }
}
