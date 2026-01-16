using System;
using System.Windows;
using System.Windows.Threading;

namespace YiboFile.Services.Config
{
    /// <summary>
    /// 配置管理服务
    /// 负责应用配置、保存配置、管理配置定时器等
    /// </summary>
    public class ConfigService
    {
        #region 私有字段

        private AppConfig _config;
        private IConfigUIHelper _uiHelper;
        private readonly YiboFile.Services.Core.Error.ErrorService _errorService;
        private bool _isApplyingConfig = false;
        private DispatcherTimer _saveTimer;
        private DispatcherTimer _columnWidthSaveTimer;

        #endregion

        #region 属性

        /// <summary>
        /// 当前配置
        /// </summary>
        public AppConfig Config
        {
            get => _config;
            set => _config = value;
        }

        /// <summary>
        /// UI 辅助接口
        /// </summary>
        public IConfigUIHelper UIHelper
        {
            get => _uiHelper;
            set => _uiHelper = value;
        }

        /// <summary>
        /// 是否正在应用配置
        /// </summary>
        public bool IsApplyingConfig => _isApplyingConfig;

        #endregion

        #region 事件

        /// <summary>
        /// 配置已应用事件
        /// </summary>
        public event EventHandler<AppConfig> ConfigApplied;

        /// <summary>
        /// 配置已保存事件
        /// </summary>
        public event EventHandler<AppConfig> ConfigSaved;

        /// <summary>
        /// 操作按钮更新请求事件
        /// </summary>
        public event EventHandler<string> ActionButtonsUpdateRequested;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化配置服务
        /// </summary>
        /// <summary>
        /// 初始化配置服务
        /// </summary>
        public ConfigService(AppConfig config, YiboFile.Services.Core.Error.ErrorService errorService)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _errorService = errorService ?? throw new ArgumentNullException(nameof(errorService));
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 应用配置
        /// </summary>
        public void ApplyConfig(AppConfig cfg)
        {
            if (cfg == null || _uiHelper == null) return;

            try
            {
                // #region agent log
                var logPath = @"f:\Download\GitHub\YiboFile\.cursor\debug.log";
                try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)); System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "ConfigService.cs:87", message = "ApplyConfig开始", data = new { cfgWindowWidth = cfg.WindowWidth, cfgWindowHeight = cfg.WindowHeight, cfgWindowTop = cfg.WindowTop, cfgWindowLeft = cfg.WindowLeft, cfgIsMaximized = cfg.IsMaximized, cfgLeftPanelWidth = cfg.LeftPanelWidth, cfgMiddlePanelWidth = cfg.MiddlePanelWidth, cfgColLeftWidth = cfg.ColLeftWidth, cfgColCenterWidth = cfg.ColCenterWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion

                _isApplyingConfig = true;

                var window = _uiHelper.Window;
                if (window == null) return;

                if (cfg.IsMaximized)
                {
                    window.WindowState = WindowState.Maximized;
                    _uiHelper.UpdateWindowStateUI();
                }
                else
                {
                    ApplyNonMaximizedWindowState(window, cfg);
                }

                // Apply Window Opacity
                if (cfg.WindowOpacity > 0)
                {
                    window.Opacity = cfg.WindowOpacity;
                }

                if (window.IsLoaded)
                {
                    ApplySplitterPositions(cfg);
                }
                else
                {
                    window.Loaded += (s, e) => ApplySplitterPositions(cfg);
                }

                ConfigApplied?.Invoke(this, cfg);
            }
            catch (Exception ex)
            {
                _errorService.ReportError("应用配置失败", YiboFile.Services.Core.Error.ErrorSeverity.Error, ex);
            }
            finally
            {
                _isApplyingConfig = false;
            }
        }

        /// <summary>
        /// 保存当前配置
        /// </summary>
        public void SaveCurrentConfig()
        {
            // 如果正在应用配置，不保存
            if (_isApplyingConfig || _config == null || _uiHelper == null) return;

            try
            {
                var window = _uiHelper.Window;
                if (window == null) return;

                // 保存当前路径
                _config.LastPath = _uiHelper.CurrentPath;

                // 保存当前库ID
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

                _config.IsMaximized = window.WindowState == WindowState.Maximized;
                if (!_config.IsMaximized)
                {
                    _config.WindowWidth = window.Width;
                    _config.WindowHeight = window.Height;
                    _config.WindowTop = window.Top;
                    _config.WindowLeft = window.Left;
                }

                // 注意：列宽（分割线位置）由 WindowStateManager.SaveSplitterPositions() 统一管理
                // 这里不再保存列宽，避免覆盖用户拖动后的位置
                // 只有在 WindowStateManager 未初始化时（启动早期），才不保存列宽

                ConfigManager.Save(_config);
                ConfigSaved?.Invoke(this, _config);
            }
            catch (Exception ex)
            {
                _errorService.ReportError("保存配置失败", YiboFile.Services.Core.Error.ErrorSeverity.Warning, ex);
            }
        }

        /// <summary>
        /// 更新操作按钮
        /// </summary>
        public void UpdateActionButtons(string mode)
        {
            // TitleActionBar已经根据Mode自动显示/隐藏对应的面板，只需要更新Mode属性
            if (_uiHelper?.TitleActionBar != null)
            {
                _uiHelper.TitleActionBar.Mode = mode;
            }
            ActionButtonsUpdateRequested?.Invoke(this, mode);
        }

        /// <summary>
        /// 初始化保存定时器
        /// </summary>
        public void InitializeSaveTimer()
        {
            if (_uiHelper?.Dispatcher == null) return;

            _saveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _saveTimer.Tick += (s, e) =>
            {
                _saveTimer.Stop();
                SaveCurrentConfig();
            };
        }

        /// <summary>
        /// 启动延迟保存
        /// </summary>
        public void StartDelayedSave()
        {
            if (_saveTimer == null)
            {
                InitializeSaveTimer();
            }

            if (_saveTimer != null)
            {
                _saveTimer.Stop();
                _saveTimer.Start();
            }
        }

        /// <summary>
        /// 初始化列宽保存定时器
        /// </summary>
        public void InitializeColumnWidthSaveTimer(Action saveColumnWidthsAction)
        {
            if (_uiHelper?.Dispatcher == null || saveColumnWidthsAction == null) return;

            _columnWidthSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _columnWidthSaveTimer.Tick += (s, e) =>
            {
                saveColumnWidthsAction();
                _columnWidthSaveTimer.Stop();
            };
        }

        /// <summary>
        /// 启动延迟保存列宽
        /// </summary>
        public void StartDelayedColumnWidthSave()
        {
            if (_columnWidthSaveTimer != null)
            {
                _columnWidthSaveTimer.Stop();
                _columnWidthSaveTimer.Start();
            }
        }

        /// <summary>
        /// 停止所有定时器
        /// </summary>
        public void StopAllTimers()
        {
            if (_saveTimer != null)
            {
                _saveTimer.Stop();
                _saveTimer = null;
            }

            if (_columnWidthSaveTimer != null)
            {
                _columnWidthSaveTimer.Stop();
                _columnWidthSaveTimer = null;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopAllTimers();
        }

        /// <summary>
        /// 应用非最大化窗口状态
        /// </summary>
        private void ApplyNonMaximizedWindowState(Window window, AppConfig cfg)
        {
            if (window == null || cfg == null) return;

            window.WindowState = WindowState.Normal; // 确保窗口状态为正常
            if (cfg.WindowWidth > 0) window.Width = cfg.WindowWidth;
            if (cfg.WindowHeight > 0) window.Height = cfg.WindowHeight;
            if (!double.IsNaN(cfg.WindowTop) && cfg.WindowTop >= 0) window.Top = cfg.WindowTop;
            if (!double.IsNaN(cfg.WindowLeft) && cfg.WindowLeft >= 0) window.Left = cfg.WindowLeft;

            window.ResizeMode = ResizeMode.CanResize;

            // #region agent log
            var logPath = @"f:\Download\GitHub\YiboFile\.cursor\debug.log";
            try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "ConfigService.cs:ApplyNonMaximizedWindowState", message = "应用非最大化窗口状态", data = new { appliedWidth = window.Width, appliedHeight = window.Height, appliedTop = window.Top, appliedLeft = window.Left }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion
        }

        /// <summary>
        /// 应用分割线位置（列宽度）
        /// </summary>
        private void ApplySplitterPositions(AppConfig cfg)
        {
            if (_uiHelper.RootGrid == null || cfg == null) return;

            // 列 0,2 分别对应 左/中；右侧自适应
            // 优先使用ColLeftWidth和ColCenterWidth（新字段），如果为0则使用LeftPanelWidth和MiddlePanelWidth（兼容字段）
            var leftWidth = cfg.ColLeftWidth > 0 ? cfg.ColLeftWidth : cfg.LeftPanelWidth;
            var centerWidth = cfg.ColCenterWidth > 0 ? cfg.ColCenterWidth : cfg.MiddlePanelWidth;

            // 如果宽度有效（大于0），确保不小于最小宽度；如果为0，使用Star模式（自适应）
            if (leftWidth > 0)
            {
                leftWidth = Math.Max(_uiHelper.ColLeft.MinWidth, leftWidth);
                _uiHelper.RootGrid.ColumnDefinitions[0].Width = new GridLength(leftWidth);
            }
            else
            {
                // 如果为0，使用Star模式（自适应）
                _uiHelper.RootGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            }

            // 如果要支持中间列自适应(Star)且右边列固定，这里应该强制中间列为Star
            // 之前的逻辑是如果 centerWidth > 0 就设为 Fixed，但这会导致启动后无法自适应填充
            // 为了配合之前的 Gap Fix (中间列自适应)，这里我们不再恢复中间列的固定宽度，而是强制设为 Star
            _uiHelper.RootGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);

            // 右侧(列4)恢复固定宽度
            var rightWidth = cfg.ColRightWidth > 0 ? cfg.ColRightWidth : 360;
            rightWidth = Math.Max(_uiHelper.ColRight.MinWidth, rightWidth);
            _uiHelper.RootGrid.ColumnDefinitions[4].Width = new GridLength(rightWidth);

            // #region agent log
            var logPath = @"f:\Download\GitHub\YiboFile\.cursor\debug.log";
            try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "ConfigService.cs:ApplySplitterPositions", message = "应用分割线位置", data = new { appliedLeftWidth = leftWidth, appliedCenterWidth = centerWidth, cfgColLeftWidth = cfg.ColLeftWidth, cfgColCenterWidth = cfg.ColCenterWidth, cfgLeftPanelWidth = cfg.LeftPanelWidth, cfgMiddlePanelWidth = cfg.MiddlePanelWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion

            // 确保窗口最小宽度正确设置
            var window = _uiHelper.Window;
            if (window != null)
            {
                var minTotalWidth = _uiHelper.ColLeft.MinWidth + _uiHelper.ColCenter.MinWidth + _uiHelper.ColRight.MinWidth + 12;
                if (window.MinWidth < minTotalWidth)
                {
                    window.MinWidth = minTotalWidth;
                }

                // 确保窗口大小已经确定，如果窗口宽度小于列宽总和，需要调整
                if (window.IsLoaded)
                {
                    double totalColumnWidth = leftWidth + centerWidth + 12; // 两个分割器宽度
                    if (window.ActualWidth > 0 && window.ActualWidth < totalColumnWidth)
                    {
                        // 窗口宽度不够，需要调整窗口大小，确保能容纳所有列
                        window.Width = Math.Max(window.Width, totalColumnWidth + 20); // 加一些边距
                    }
                }
            }

            // 应用完配置后，更新布局以确保MinWidth生效
            _uiHelper.RootGrid.UpdateLayout();

            // --- 恢复扩展 UI 状态 ---

            // 1. 恢复右侧面板可见性
            if (!cfg.IsRightPanelVisible)
            {
                // 如果配置为不可见，则折叠右侧面板
                // 这里我们调用 ToggleRightPanel 或者手动设置 Width=0
                // 但 ToggleRightPanel 逻辑比较复杂，我们直接操作列宽
                // 为了避免冲突，我们模拟 ToggleRightPanel 的 "关闭" 状态

                // 注意：如果我们在上面刚刚设置了 Width=360，这里再设为 0
                // 下次 Toggle 打开时，需要知道恢复多少。ConfigService 不持有该逻辑。
                // 最好是确保 RightPanelControl 处于隐藏状态如果 IsRightPanelVisible=false
                // 不过 ToggleRightPanel 实际上是操作 ColRight.Width。
                // 简单处理：如果不可见，将列宽设为 0。
                _uiHelper.RootGrid.ColumnDefinitions[4].Width = new GridLength(0);

                // 同时需要更新 TitleActionBar 的状态 (如果能访问到)
                if (_uiHelper.TitleActionBar != null)
                {
                    // 这是一个 Hack，理想情况应该通过 Command 或 Service 调用
                    // 这里假设 TitleActionBar 会根据 Width 自动更新，或者我们无法触及
                }
            }

            // 2. 恢复右侧面板内部高度 (备注区)
            if (cfg.RightPanelNotesHeight > 0 && _uiHelper.RightPanelControl != null)
            {
                if (_uiHelper.RightPanelControl.Content is System.Windows.Controls.Grid rightRootGrid)
                {
                    if (rightRootGrid.RowDefinitions.Count > 3)
                    {
                        var notesRow = rightRootGrid.RowDefinitions[3];
                        notesRow.Height = new GridLength(cfg.RightPanelNotesHeight);
                    }
                }
            }

            // 3. 恢复中间面板底部高度 (文件详情区)
            if (cfg.CenterPanelInfoHeight > 0 && _uiHelper.FileBrowser != null && _uiHelper.FileBrowser.Content is System.Windows.Controls.Grid fileBrowserGrid)
            {
                if (fileBrowserGrid.RowDefinitions.Count >= 4)
                {
                    var lastRow = fileBrowserGrid.RowDefinitions[fileBrowserGrid.RowDefinitions.Count - 1];
                    lastRow.Height = new GridLength(cfg.CenterPanelInfoHeight);
                }
            }

            // 注意：不要在启动时调用 EnsureColumnMinWidths() 或 AdjustColumnWidths()
            // 这些方法会重新计算列宽，覆盖我们刚刚从配置恢复的固定宽度
            // 配置中的列宽已经是用户拖动后的正确值，不需要重新计算
        }

        #endregion
    }
}


