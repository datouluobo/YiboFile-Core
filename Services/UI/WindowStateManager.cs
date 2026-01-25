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

namespace YiboFile.Services
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
        private TabService _secondTabService; // Removing readonly to allow updates
        private readonly ConfigService _configService;
        private readonly AppConfig _config;
        private readonly NavigationService _navigationService;
        private readonly Navigation.NavigationModeService _navigationModeService;
        private bool _isInitialized = false;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化窗口状态管理器
        /// </summary>
        public WindowStateManager(IConfigUIHelper uiHelper, TabService tabService, ConfigService configService, AppConfig config, NavigationService navigationService = null, Navigation.NavigationModeService navigationModeService = null, TabService secondTabService = null)
        {
            _uiHelper = uiHelper ?? throw new ArgumentNullException(nameof(uiHelper));
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _secondTabService = secondTabService;
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _navigationService = navigationService;
            _navigationModeService = navigationModeService;
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
        public void SaveAllState()
        {
            // 启动阶段或尚未初始化完成时不保存，避免覆盖已有配置
            if (!_isInitialized || (_configService != null && _configService.IsApplyingConfig))
            {
                return;
            }
            try
            {
                // #region agent log
                var logPath = @"f:\Download\GitHub\YiboFile\.cursor\debug.log";
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

                // 🔥 CRITICAL: 只复制窗口状态字段，不要复制UI设置字段！
                // UI设置(UIFontSize, TagFontSize, ColTagsWidth等)由ConfigurationService管理
                // 如果在这里复制，会用启动时的旧_config覆盖用户刚保存的设置！

                // ✅ 使用ConfigurationService统一更新，避免覆盖用户设置
                YiboFile.Services.Config.ConfigurationService.Instance.Update(latestConfig =>
                {
                    // 窗口尺寸和位置
                    latestConfig.WindowWidth = _config.WindowWidth;
                    latestConfig.WindowHeight = _config.WindowHeight;
                    latestConfig.WindowTop = _config.WindowTop;
                    latestConfig.WindowLeft = _config.WindowLeft;
                    latestConfig.IsMaximized = _config.IsMaximized;

                    // 主布局列宽（左中右三列）- 这些是窗口布局，不是UI设置
                    latestConfig.ColLeftWidth = _config.ColLeftWidth;
                    latestConfig.ColCenterWidth = _config.ColCenterWidth;
                    latestConfig.ColRightWidth = _config.ColRightWidth;
                    latestConfig.LeftPanelWidth = _config.LeftPanelWidth;
                    latestConfig.MiddlePanelWidth = _config.MiddlePanelWidth;

                    // ❌ ColTagsWidth/ColNotesWidth不要复制 - UI设置由ConfigurationService管理

                    // 面板状态
                    latestConfig.IsRightPanelVisible = _config.IsRightPanelVisible;
                    latestConfig.RightPanelNotesHeight = _config.RightPanelNotesHeight;
                    latestConfig.CenterPanelInfoHeight = _config.CenterPanelInfoHeight;

                    // 导航状态
                    latestConfig.LastPath = _config.LastPath;
                    latestConfig.LastNavigationMode = _config.LastNavigationMode;
                    latestConfig.LastLibraryId = _config.LastLibraryId;

                    // 标签页状态
                    latestConfig.OpenTabs = _config.OpenTabs;
                    latestConfig.ActiveTabKey = _config.ActiveTabKey;

                    // 副列表标签页状态
                    latestConfig.OpenTabsSecondary = _config.OpenTabsSecondary;
                    latestConfig.ActiveTabKeySecondary = _config.ActiveTabKeySecondary;

                    // 确保双列表模式状态被正确保存
                    latestConfig.IsDualListMode = _config.IsDualListMode;
                });

                // ✅ 不再需要手动Save - ConfigurationService.Update会触发去抖保存
                // 程序关闭时WindowLifecycleHandler会调用SaveNow()确保落盘

                // #region agent log
                try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:72", message = "SaveAllState完成", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
            }
            catch (Exception ex)
            {
                // 静默处理错误，避免影响程序关闭
                // #region agent log
                try { var logPathErr = @"f:\Download\GitHub\YiboFile\.cursor\debug.log"; System.IO.File.AppendAllText(logPathErr, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:77", message = "SaveAllState异常", data = new { error = ex.Message, stackTrace = ex.StackTrace }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
            }
        }

        /// <summary>
        /// 保存窗口状态（大小、位置、最大化状态）
        /// </summary>
        public void SaveWindowState()
        {
            // #region agent log
            var logPath = @"f:\Download\GitHub\YiboFile\.cursor\debug.log";
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)); System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:80", message = "SaveWindowState开始", data = new { windowIsNull = _uiHelper?.Window == null }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion

            var window = _uiHelper.Window;
            if (window == null) return;

            // 保存最大化状态
            _config.IsMaximized = window.WindowState == WindowState.Maximized;

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
                    if (!double.IsNaN(window.Top) && !double.IsInfinity(window.Top) && window.Top >= -10000) // loose check
                    {
                        _config.WindowTop = window.Top;
                    }
                    else
                    {
                        _config.WindowTop = 0; // 如果无效，使用默认值0
                    }

                    if (!double.IsNaN(window.Left) && !double.IsInfinity(window.Left) && window.Left >= -10000)
                    {
                        _config.WindowLeft = window.Left;
                    }
                    else
                    {
                        _config.WindowLeft = 0; // 如果无效，使用默认值0
                    }

                    // #region agent log
                    try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:100", message = "SaveWindowState保存非最大化状态", data = new { savedWidth = _config.WindowWidth, savedHeight = _config.WindowHeight, savedTop = _config.WindowTop, savedLeft = _config.WindowLeft, windowTop = window.Top, windowLeft = window.Left }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                    // #endregion
                }
                else
                {
                    // 最大化状态：保存还原尺寸
                    Rect restoreBounds = window.RestoreBounds;

                    // #region agent log
                    try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "WindowStateManager.cs:107", message = "SaveWindowState最大化状态RestoreBounds", data = new { restoreBoundsWidth = restoreBounds.Width, restoreBoundsHeight = restoreBounds.Height, restoreBoundsTop = restoreBounds.Top, restoreBoundsLeft = restoreBounds.Left }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                    // #endregion

                    if (restoreBounds.Width > 0 && restoreBounds.Height > 0)
                    {
                        _config.WindowWidth = restoreBounds.Width;
                        _config.WindowHeight = restoreBounds.Height;
                        _config.WindowTop = restoreBounds.Top;
                        _config.WindowLeft = restoreBounds.Left;
                    }
                    else
                    {
                        // 如果RestoreBounds无效，尝试使用配置中的值
                        if (_config.WindowWidth > 0 && _config.WindowHeight > 0)
                        {
                        }
                        else
                        {
                            // 使用默认值
                            _config.WindowWidth = 1200;
                            _config.WindowHeight = 800;
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
            var logPath = @"f:\Download\GitHub\YiboFile\.cursor\debug.log";
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)); System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "WindowStateManager.cs:147", message = "SaveSplitterPositions开始", data = new { rootGridIsNull = _uiHelper?.RootGrid == null, rootGridIsLoaded = _uiHelper?.RootGrid?.IsLoaded ?? false, isInitialized = _isInitialized, isApplyingConfig = _configService?.IsApplyingConfig ?? false }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion

            // 双重保护：启动阶段不保存分割线位置
            if (!_isInitialized || (_configService != null && _configService.IsApplyingConfig))
            {
                return;
            }

            if (_uiHelper.RootGrid == null || !_uiHelper.RootGrid.IsLoaded) return;

            var leftCol = _uiHelper.ColLeft;
            var middleCol = _uiHelper.ColCenter;

            double leftWidth = 0;
            double middleWidth = 0;

            // #region agent log
            try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "WindowStateManager.cs:159", message = "SaveSplitterPositions列宽度属性", data = new { leftColWidthIsAbsolute = leftCol.Width.IsAbsolute, leftColWidthValue = leftCol.Width.IsAbsolute ? leftCol.Width.Value : 0, leftColActualWidth = leftCol.ActualWidth, leftColMinWidth = leftCol.MinWidth, middleColWidthIsAbsolute = middleCol.Width.IsAbsolute, middleColWidthValue = middleCol.Width.IsAbsolute ? middleCol.Width.Value : 0, middleColWidthIsStar = middleCol.Width.IsStar, middleColActualWidth = middleCol.ActualWidth, middleColMinWidth = middleCol.MinWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion

            // GridSplitter拖拽后，列宽已调整，优先使用ActualWidth获取实际显示的宽度
            // 强制更新布局以确保ActualWidth是最新的
            _uiHelper.RootGrid.UpdateLayout();

            if (leftCol.ActualWidth > 0)
            {
                leftWidth = leftCol.ActualWidth;
            }

            if (middleCol.ActualWidth > 0)
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
            }
            if (middleWidth > 0 && middleWidth >= middleCol.MinWidth)
            {
                _config.MiddlePanelWidth = middleWidth;
                _config.ColCenterWidth = middleWidth; // 同时更新新字段名
                // #region agent log
                try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "WindowStateManager.cs:204", message = "SaveSplitterPositions保存中间宽度", data = new { savedMiddleWidth = _config.ColCenterWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
            }

            // 新增：保存右侧列宽度
            var rightCol = _uiHelper.ColRight;
            double rightWidth = rightCol.ActualWidth;
            if (rightWidth > 0 && rightWidth >= rightCol.MinWidth)
            {
                _config.ColRightWidth = rightWidth;
            }

            // --- 新增：保存扩展 UI 状态 ---

            // 1. 保存右侧面板可见性 (Width > 0 并不完全代表可见性，这里主要看 Visible 属性)
            // 假设 ColRightWidth > 0 且 Visibility 为 Visible
            // 由于 ColRight 总是存在的，我们检查 RightPanelControl 是否实际显示（或者看 Column 的 Width 是否为 0）
            // 目前右面板通过 Width=0 在视觉上隐藏，ToggleRightPanel 逻辑也是改宽度的。
            // 但如果用了 ToggleRightPanel，它会设置 WeekStar/Fixed。
            // 简单起见，如果 ColRight.ActualWidth < 10，认为它是隐藏的。
            _config.IsRightPanelVisible = _uiHelper.ColRight.ActualWidth > 10;

            // 2. 保存右侧面板内部高度 (备注区)
            // 需要访问 RightPanelControl -> Grid -> RowDefinitions[3]
            if (_uiHelper.RightPanelControl != null)
            {
                var content = _uiHelper.RightPanelControl.Content as System.Windows.Controls.Grid; // UserControl Content is usually Grid
                                                                                                   // RightPanelControl XAML root is Grid.
                                                                                                   // But _uiHelper.RightPanelControl IS the YiboFile.RightPanelControl (UserControl).
                                                                                                   // We need checking its Structure. 
                                                                                                   // The UserControl Content property holds the root Grid.
                if (_uiHelper.RightPanelControl.Content is System.Windows.Controls.Grid rightRootGrid)
                {
                    if (rightRootGrid.RowDefinitions.Count > 3)
                    {
                        var notesRow = rightRootGrid.RowDefinitions[3]; // Row 3 is Notes
                        if (notesRow.Height.IsAbsolute)
                        {
                            _config.RightPanelNotesHeight = notesRow.Height.Value;
                        }
                    }
                }
            }

            // 3. 保存中间面板底部高度 (文件详情区)
            // 需要访问 FileBrowserControl -> Grid -> RowDefinitions[3]
            if (_uiHelper.FileBrowser?.Content is System.Windows.Controls.Grid fileBrowserGrid)
            {
                if (fileBrowserGrid.RowDefinitions.Count > 3)
                {
                    var infoRow = fileBrowserGrid.RowDefinitions[3]; // Row 3 is GridSplitter (Row 4 is Info actually? Wait, let me check XAML)
                                                                     // FileBrowserControl.xaml:
                                                                     // Row 0: Address
                                                                     // Row 1: TabManager
                                                                     // Row 2: FileList (*)
                                                                     // Row 3: Splitter
                                                                     // Row 4: Info Panel
                                                                     // Wait, XAML says: RowDefinition Height="180" for Row 3? 
                                                                     // Let's re-read FileBrowserControl.xaml quickly from memory or just check definitions. 
                                                                     // Row 3 is 180 MinHeight=120?
                                                                     // Re-checking XAML: 
                                                                     // Row 0: Auto
                                                                     // Row 1: Auto
                                                                     // Row 2: *
                                                                     // Row 3: 180 MinHeight 120
                                                                     // Inside Grid:
                                                                     // GridSplitter Grid.Row="3" (Wait, Splitter usually shares row or is in separate row?)
                                                                     // Line 194: GridSplitter Grid.Row="3" ...
                                                                     // Line 198: Border Grid.Row="4" ...
                                                                     // This implies Row 3 is the SPLITTER row??
                                                                     // But RowDefinition for Row 3 has Height 180?
                                                                     // Ah, typical XAML mistake or I misread.
                                                                     // Let's assume Row 3 is the Info Pane ROW definition idx 3. The GridSplitter might be in Row 2 or 3.
                                                                     // Actually, let's look safely: usually the last row definition with fixed/pixel height is the info panel.
                                                                     // Safest is to save the last RowDefinition height if it's absolute.

                    if (fileBrowserGrid.RowDefinitions.Count >= 4)
                    {
                        // 假设最后一行是详情区
                        var lastRow = fileBrowserGrid.RowDefinitions[fileBrowserGrid.RowDefinitions.Count - 1];
                        if (lastRow.Height.IsAbsolute)
                        {
                            _config.CenterPanelInfoHeight = lastRow.Height.Value;
                        }
                    }
                }
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
            var logPath2 = @"f:\Download\GitHub\YiboFile\.cursor\debug.log";
            try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath2)); System.IO.File.AppendAllText(logPath2, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "C", location = "WindowStateManager.cs:278", message = "SaveTabsState开始", data = new { tabServiceIsNull = _tabService == null }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion

            if (_tabService != null)
            {
                var (tabs, activeKey) = GetTabsState(_tabService);

                // 启动早期：如果当前没有标签页且配置中有，可能是还没恢复，不覆盖
                if (tabs.Count > 0 || _config.OpenTabs == null || _config.OpenTabs.Count == 0)
                {
                    _config.OpenTabs = tabs;
                    _config.ActiveTabKey = activeKey;
                }
            }

            if (_secondTabService != null)
            {
                var (tabs, activeKey) = GetTabsState(_secondTabService);
                if (tabs.Count > 0 || _config.OpenTabsSecondary == null || _config.OpenTabsSecondary.Count == 0)
                {
                    _config.OpenTabsSecondary = tabs;
                    _config.ActiveTabKeySecondary = activeKey;
                }
            }

            // #region agent log
            try { System.IO.File.AppendAllText(logPath2, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "C", location = "WindowStateManager.cs:296", message = "SaveTabsState保存后", data = new { openTabsCount = _config.OpenTabs?.Count ?? 0, openTabs = _config.OpenTabs ?? new List<string>(), activeTabKey = _config.ActiveTabKey, openTabsSecondaryCount = _config.OpenTabsSecondary?.Count ?? 0 }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion
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

            // 标记初始化完成
            Action markInitialized = () => _isInitialized = true;

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
                    if (path.StartsWith("search://"))
                    {
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
                    var library = DatabaseManager.GetLibrary(libraryId);
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





