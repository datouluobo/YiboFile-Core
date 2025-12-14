using System;
using System.Windows;
using System.Windows.Threading;

namespace OoiMRR.Services.Config
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
        public ConfigService(AppConfig config, IConfigUIHelper uiHelper)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _uiHelper = uiHelper ?? throw new ArgumentNullException(nameof(uiHelper));
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
                var logPath = @"f:\Download\GitHub\OoiMRR\.cursor\debug.log";
                try { System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)); System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "ConfigService.cs:87", message = "ApplyConfig开始", data = new { cfgWindowWidth = cfg.WindowWidth, cfgWindowHeight = cfg.WindowHeight, cfgWindowTop = cfg.WindowTop, cfgWindowLeft = cfg.WindowLeft, cfgIsMaximized = cfg.IsMaximized, cfgLeftPanelWidth = cfg.LeftPanelWidth, cfgMiddlePanelWidth = cfg.MiddlePanelWidth, cfgColLeftWidth = cfg.ColLeftWidth, cfgColCenterWidth = cfg.ColCenterWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
                
                _isApplyingConfig = true;

                var window = _uiHelper.Window;
                if (window == null) return;

                if (cfg.IsMaximized)
                {
                    // 使用伪最大化而不是WindowState.Maximized，避免系统边距
                    // RestoreBounds应该使用保存的位置和尺寸，如果无效则使用默认值
                    double restoreWidth = cfg.WindowWidth > 0 ? cfg.WindowWidth : 1200;
                    double restoreHeight = cfg.WindowHeight > 0 ? cfg.WindowHeight : 800;
                    double restoreTop = !double.IsNaN(cfg.WindowTop) && cfg.WindowTop >= 0 ? cfg.WindowTop : 0;
                    double restoreLeft = !double.IsNaN(cfg.WindowLeft) && cfg.WindowLeft >= 0 ? cfg.WindowLeft : 0;

                    _uiHelper.RestoreBounds = new Rect(restoreLeft, restoreTop, restoreWidth, restoreHeight);

                    // 设置初始状态
                    _uiHelper.IsPseudoMaximized = true;
                    window.ResizeMode = ResizeMode.NoResize;

                    // 在窗口加载完成后应用最大化
                    window.Loaded += (s, e) =>
                    {
                        if (_uiHelper.IsPseudoMaximized)
                        {
                            var wa = _uiHelper.GetCurrentMonitorWorkAreaDIPs();
                            window.WindowState = WindowState.Normal;
                            window.Left = wa.Left;
                            window.Top = wa.Top;
                            window.Width = wa.Width;
                            window.Height = wa.Height;
                            window.UpdateLayout();
                            _uiHelper.ExtendFrameIntoClientArea(-1, -1, -1, -1);
                            // 更新窗口状态UI（最大化按钮图标）
                            _uiHelper.UpdateWindowStateUI();
                        }
                    };
                }
                else
                {
                    window.WindowState = WindowState.Normal; // 确保窗口状态为正常
                    window.Width = cfg.WindowWidth;
                    window.Height = cfg.WindowHeight;
                    if (!double.IsNaN(cfg.WindowTop)) window.Top = cfg.WindowTop;
                    if (!double.IsNaN(cfg.WindowLeft)) window.Left = cfg.WindowLeft;
                    _uiHelper.IsPseudoMaximized = false;
                    window.ResizeMode = ResizeMode.CanResize;
                    
                    // #region agent log
                    try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "ConfigService.cs:125", message = "ApplyConfig应用窗口尺寸", data = new { appliedWidth = window.Width, appliedHeight = window.Height, appliedTop = window.Top, appliedLeft = window.Left }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                    // #endregion
                }

                // 应用主Grid列宽
                if (_uiHelper.RootGrid != null)
                {
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
                    
                    if (centerWidth > 0)
                    {
                        centerWidth = Math.Max(_uiHelper.ColCenter.MinWidth, centerWidth);
                        _uiHelper.RootGrid.ColumnDefinitions[2].Width = new GridLength(centerWidth);
                    }
                    else
                    {
                        // 如果为0，使用Star模式（自适应）
                        _uiHelper.RootGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
                    }
                    // 右侧(列4)使用*自适应，不设置固定宽度

                    // #region agent log
                    try { System.IO.File.AppendAllText(logPath, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "ConfigService.cs:139", message = "ApplyConfig应用列宽度", data = new { appliedLeftWidth = leftWidth, appliedCenterWidth = centerWidth, cfgColLeftWidth = cfg.ColLeftWidth, cfgColCenterWidth = cfg.ColCenterWidth, cfgLeftPanelWidth = cfg.LeftPanelWidth, cfgMiddlePanelWidth = cfg.MiddlePanelWidth }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                    // #endregion

                    // 确保窗口最小宽度正确设置
                    var minTotalWidth = _uiHelper.ColLeft.MinWidth + _uiHelper.ColCenter.MinWidth + _uiHelper.ColRight.MinWidth + 12;
                    if (window.MinWidth < minTotalWidth)
                    {
                        window.MinWidth = minTotalWidth;
                    }
                }

                // 重要：应用完配置后，立即调整列宽以确保MinWidth生效
                // 但只在列宽度为Star模式时才调整，如果已经设置了固定宽度，不要覆盖
                window.UpdateLayout();
                // 延迟调用AdjustColumnWidths，避免覆盖刚刚设置的固定宽度
                window.Dispatcher.BeginInvoke(new Action(() =>
                {
                    // 只有在列宽度为Star模式时才调整
                    if (_uiHelper.RootGrid != null)
                    {
                        var leftCol = _uiHelper.RootGrid.ColumnDefinitions[0];
                        var centerCol = _uiHelper.RootGrid.ColumnDefinitions[2];
                        if (leftCol.Width.IsStar || centerCol.Width.IsStar)
                        {
                            _uiHelper.AdjustColumnWidths();
                        }
                    }
                    _uiHelper.EnsureColumnMinWidths();
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                ConfigApplied?.Invoke(this, cfg);
            }
            catch (Exception ex)
            {
                // #region agent log
                try { var logPath2 = @"f:\Download\GitHub\OoiMRR\.cursor\debug.log"; System.IO.File.AppendAllText(logPath2, System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "E", location = "ConfigService.cs:158", message = "ApplyConfig异常", data = new { error = ex.Message, stackTrace = ex.StackTrace }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion
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
                
                _config.IsMaximized = _uiHelper.IsPseudoMaximized; // 使用IsPseudoMaximized而不是WindowState
                if (!_config.IsMaximized)
                {
                    _config.WindowWidth = window.Width;
                    _config.WindowHeight = window.Height;
                    _config.WindowTop = window.Top;
                    _config.WindowLeft = window.Left;
                }

                if (_uiHelper.RootGrid != null && _uiHelper.RootGrid.IsLoaded)
                {
                    // 强制更新布局
                    _uiHelper.RootGrid.UpdateLayout();

                    // 获取实际尺寸
                    var leftWidth = _uiHelper.RootGrid.ColumnDefinitions[0].ActualWidth;
                    var middleWidth = _uiHelper.RootGrid.ColumnDefinitions[2].ActualWidth;
                    var rightWidth = _uiHelper.RootGrid.ColumnDefinitions[4].ActualWidth;

                    // 只有当实际尺寸大于0时才保存
                    if (leftWidth > 0) _config.LeftPanelWidth = leftWidth;
                    if (middleWidth > 0) _config.MiddlePanelWidth = middleWidth;
                    // 右侧为自适应宽度，不保存
                }
                else if (_uiHelper.RootGrid != null)
                {
                    // 如果Grid未加载，尝试使用Width值
                    var leftWidth = _uiHelper.RootGrid.ColumnDefinitions[0].Width;
                    var middleWidth = _uiHelper.RootGrid.ColumnDefinitions[2].Width;
                    var rightWidth = _uiHelper.RootGrid.ColumnDefinitions[4].Width;

                    if (leftWidth.IsAbsolute) _config.LeftPanelWidth = leftWidth.Value;
                    if (middleWidth.IsAbsolute) _config.MiddlePanelWidth = middleWidth.Value;
                    // 右侧为自适应宽度，不保存
                }

                ConfigManager.Save(_config);
                ConfigSaved?.Invoke(this, _config);
            }
            catch { }
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

        #endregion
    }
}

