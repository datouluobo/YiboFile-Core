using System;
using OoiMRR.Services.Config;
using OoiMRR.Services.Core;
using OoiMRR;

namespace OoiMRR.Services
{
    /// <summary>
    /// MainWindow 初始化器
    /// 负责 MainWindow 的应用程序级别初始化
    /// </summary>
    public class MainWindowInitializer
    {
        private readonly MainWindow _mainWindow;

        /// <summary>
        /// 初始化 MainWindowInitializer
        /// </summary>
        /// <param name="mainWindow">主窗口实例</param>
        public MainWindowInitializer(MainWindow mainWindow)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        }

        /// <summary>
        /// 初始化应用程序
        /// 加载配置、初始化服务等
        /// </summary>
        /// <summary>
        /// 初始化应用程序
        /// 加载配置、初始化服务等
        /// </summary>
        public void InitializeApplication()
        {
            InitializeConfigServices();
            ApplyInitialState();
        }

        /// <summary>
        /// 第一阶段：初始化配置和服务（必须在 InitializeHandlers 之前调用）
        /// </summary>
        public void InitializeConfigServices()
        {
            try
            {
                FileLogger.Log("InitializeConfigServices started.");
                // 加载配置（Load 方法内部会自动执行迁移）
                var config = ConfigManager.Load();
                FileLogger.Log("Configuration loaded.");

                // 初始化配置服务
                if (_mainWindow._configService == null)
                {
                    _mainWindow._configService = new ConfigService(config, _mainWindow);
                }
                else
                {
                    _mainWindow._configService.Config = config;
                }

                // 更新 TabService 的配置
                if (_mainWindow._tabService != null && config != null)
                {
                    try
                    {
                        _mainWindow._tabService.UpdateConfig(config);
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogException("UpdateConfig", ex);
                        System.Diagnostics.Debug.WriteLine($"UpdateConfig failed: {ex.Message}");
                    }
                }

                // 初始化窗口状态管理器（需要在RestoreLastState之前创建）
                if (_mainWindow._configService != null && _mainWindow._tabService != null)
                {
                    try
                    {
                        _mainWindow._windowStateManager = new WindowStateManager(
                            _mainWindow,
                            _mainWindow._tabService,
                            _mainWindow._configService,
                            config,
                            _mainWindow._navigationService,
                            _mainWindow._navigationModeService
                        );
                        FileLogger.Log("WindowStateManager initialized.");
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogException("WindowStateManager init", ex);
                        System.Diagnostics.Debug.WriteLine($"WindowStateManager init failed: {ex.Message}");
                    }
                }

                // 初始化导航模式服务
                if (_mainWindow._navigationModeService == null)
                {
                    try
                    {
                        _mainWindow._navigationModeService = new Navigation.NavigationModeService(
                            _mainWindow,
                            _mainWindow._navigationService,
                            _mainWindow._tabService,
                            _mainWindow._configService
                        );
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogException("NavigationModeService init", ex);
                        System.Diagnostics.Debug.WriteLine($"NavigationModeService init failed: {ex.Message}");
                    }
                }

                // 初始化设置覆盖控制器
                if (_mainWindow._settingsOverlayController == null)
                {
                    try
                    {
                        var settingsOverlay = _mainWindow.FindName("SettingsOverlay") as System.Windows.Controls.Grid;
                        var settingsPanel = _mainWindow.FindName("SettingsPanel") as Controls.SettingsPanelControl;
                        if (settingsOverlay != null && settingsPanel != null)
                        {
                            _mainWindow._settingsOverlayController = new Settings.SettingsOverlayController(
                                settingsOverlay,
                                settingsPanel,
                                (cfg) => _mainWindow._configService?.ApplyConfig(cfg)
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        FileLogger.LogException("SettingsOverlayController init", ex);
                        System.Diagnostics.Debug.WriteLine($"SettingsOverlayController init failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException("InitializeConfigServices FATAL", ex);
                System.Diagnostics.Debug.WriteLine($"InitializeConfigServices fatal error: {ex.Message}");
            }
        }

        /// <summary>
        /// 第二阶段：应用初始状态（必须在 InitializeHandlers 和 InitializeEvents 之后调用）
        /// </summary>
        public void ApplyInitialState()
        {
            try
            {
                FileLogger.Log("ApplyInitialState started.");
                // 获取当前配置
                var config = _mainWindow._configService?.Config ?? ConfigManager.Load();

                // 应用配置
                try
                {
                    _mainWindow._configService?.ApplyConfig(config);
                    FileLogger.Log("Configuration applied.");
                }
                catch (Exception ex)
                {
                    FileLogger.LogException("ApplyConfig", ex);
                    System.Diagnostics.Debug.WriteLine($"ApplyConfig failed: {ex.Message}");
                }

                // 加载初始数据
                FileLogger.Log("Loading initial data...");
                LoadInitialData();
                FileLogger.Log("Initial data loaded.");

                // 恢复最后的状态
                RestoreLastState(config);

                // 关键修复：初始化完成后，强制调整列宽逻辑
                // 这会确保中间列被恢复为 Star (自适应) 宽度，防止启动时出现右侧空白间隙（因为配置中保存的是固定像素宽度）
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow._windowLifecycleHandler?.AdjustColumnWidths();
                }, System.Windows.Threading.DispatcherPriority.Loaded);

                FileLogger.Log("ApplyInitialState completed.");
            }
            catch (Exception ex)
            {
                FileLogger.LogException("ApplyInitialState FATAL", ex);
                System.Diagnostics.Debug.WriteLine($"ApplyInitialState fatal error: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载初始数据
        /// </summary>
        private void LoadInitialData()
        {
            // 加载库列表
            try
            {
                _mainWindow._libraryService?.LoadLibraries();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadLibraries failed: {ex.Message}");
            }

            // 加载收藏列表  
            if (_mainWindow.FavoritesListBox != null)
            {
                try
                {
                    var favoriteServiceField = typeof(MainWindow).GetField("_favoriteService",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var favoriteService = favoriteServiceField?.GetValue(_mainWindow);
                    favoriteService?.GetType().GetMethod("LoadFavorites")?.Invoke(favoriteService,
                        new[] { _mainWindow.FavoritesListBox });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadFavorites failed: {ex.Message}");
                }
            }

            // 加载快速访问列表
            if (_mainWindow.QuickAccessListBox != null)
            {
                try
                {
                    var quickAccessServiceField = typeof(MainWindow).GetField("_quickAccessService",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var quickAccessService = quickAccessServiceField?.GetValue(_mainWindow);
                    quickAccessService?.GetType().GetMethod("LoadQuickAccess")?.Invoke(quickAccessService,
                        new[] { _mainWindow.QuickAccessListBox });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadQuickAccess failed: {ex.Message}");
                }
            }

            // 加载驱动器列表
            try
            {
                _mainWindow.LoadDrives();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadDrives failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复最后的状态
        /// </summary>
        private void RestoreLastState(AppConfig config)
        {
            if (config == null)
                return;

            try
            {
                // 恢复导航模式
                if (!string.IsNullOrEmpty(config.LastNavigationMode))
                {
                    _mainWindow._navigationModeService?.SwitchNavigationMode(config.LastNavigationMode);
                }

                // 使用WindowStateManager恢复标签页状态（包括降级处理）
                if (_mainWindow._windowStateManager != null)
                {
                    _mainWindow._windowStateManager.RestoreTabsState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RestoreLastState failed: {ex.Message}");
            }
        }
    }
}
