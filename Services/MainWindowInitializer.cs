using System;
using OoiMRR.Services.Config;
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
        public void InitializeApplication()
        {
            // 加载配置（Load 方法内部会自动执行迁移）
            var config = ConfigManager.Load();

            // 初始化配置服务
            if (_mainWindow._configService == null)
            {
                _mainWindow._configService = new ConfigService(config, _mainWindow);
            }
            else
            {
                _mainWindow._configService.Config = config;
            }

            // 应用配置
            _mainWindow._configService.ApplyConfig(config);

            // 更新 TabService 的配置
            if (_mainWindow._tabService != null && config != null)
            {
                _mainWindow._tabService.UpdateConfig(config);
            }

            // 初始化窗口状态管理器（需要在RestoreLastState之前创建）
            if (_mainWindow._configService != null && _mainWindow._tabService != null)
            {
                _mainWindow._windowStateManager = new WindowStateManager(
                    _mainWindow, 
                    _mainWindow._tabService, 
                    _mainWindow._configService, 
                    config,
                    _mainWindow._navigationService,
                    _mainWindow._navigationModeService
                );
            }

            // 初始化导航模式服务
            if (_mainWindow._navigationModeService == null)
            {
                _mainWindow._navigationModeService = new Navigation.NavigationModeService(
                    _mainWindow,
                    _mainWindow._navigationService,
                    _mainWindow._tabService,
                    _mainWindow._configService
                );
            }

            // 初始化设置覆盖控制器
            if (_mainWindow._settingsOverlayController == null)
            {
                var settingsOverlay = _mainWindow.FindName("SettingsOverlay") as System.Windows.Controls.Grid;
                var settingsPanel = _mainWindow.FindName("SettingsPanel") as Controls.SettingsPanelControl;
                if (settingsOverlay != null && settingsPanel != null)
                {
                    _mainWindow._settingsOverlayController = new Settings.SettingsOverlayController(
                        settingsOverlay,
                        settingsPanel,
                        (config) => _mainWindow._configService?.ApplyConfig(config)
                    );
                }
            }

            // 加载初始数据
            LoadInitialData();

            // 恢复最后的状态
            RestoreLastState(config);
        }

        /// <summary>
        /// 加载初始数据
        /// </summary>
        private void LoadInitialData()
        {
            // 加载库列表
            _mainWindow._libraryService?.LoadLibraries();

            // 加载收藏列表  
            if (_mainWindow.FavoritesListBox != null)
            {
                var favoriteServiceField = typeof(MainWindow).GetField("_favoriteService", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var favoriteService = favoriteServiceField?.GetValue(_mainWindow);
                favoriteService?.GetType().GetMethod("LoadFavorites")?.Invoke(favoriteService, 
                    new[] { _mainWindow.FavoritesListBox });
            }

            // 加载快速访问列表
            if (_mainWindow.QuickAccessListBox != null)
            {
                var quickAccessServiceField = typeof(MainWindow).GetField("_quickAccessService", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var quickAccessService = quickAccessServiceField?.GetValue(_mainWindow);
                quickAccessService?.GetType().GetMethod("LoadQuickAccess")?.Invoke(quickAccessService, 
                    new[] { _mainWindow.QuickAccessListBox });
            }

            // 加载驱动器列表
            _mainWindow.LoadDrives();
        }

        /// <summary>
        /// 恢复最后的状态
        /// </summary>
        private void RestoreLastState(AppConfig config)
        {
            if (config == null)
                return;

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
    }
}
