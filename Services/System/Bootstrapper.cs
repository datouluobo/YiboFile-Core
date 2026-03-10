using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using YiboFile.Services.Core;
using YiboFile.Services.Config;
using YiboFile.Services.Localization;
using YiboFile.Services.Favorite;
using YiboFile.Services.QuickAccess;
using YiboFile.Services.FileList;
using YiboFile.Services.Search;
using YiboFile.Services.Navigation;
using YiboFile.Services.FileNotes;
using YiboFile.Services.Tabs;
using YiboFile.Services.Tabs.Content;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Features;
using YiboFile.Controls;
using YiboFile.Services.Plugins;

namespace YiboFile.Services.Startup
{
    /// <summary>
    /// Bootstrapper for application startup logic.
    /// Handles dependency injection, single instance check, error handling, and resource cleanup.
    /// </summary>
    public class Bootstrapper : IDisposable
    {
        private readonly Application _application;
        private readonly Action<IServiceCollection> _serviceConfigCallback;
        private Mutex _mutex;
        private bool _mutexOwned;
        private const string MutexName = "YiboFile_SingleInstance_Mutex";

        public IServiceProvider ServiceProvider { get; private set; }
        public bool IsTagTrainAvailable { get; private set; } = false;

        public Bootstrapper(Application application, Action<IServiceCollection> serviceConfigCallback = null)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _serviceConfigCallback = serviceConfigCallback;
        }

        public bool Initialize()
        {
            try
            {
                // 1. Setup DI Container
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                ServiceProvider = serviceCollection.BuildServiceProvider();

                // 2. Initialize Core Services (MessageBus, Error Handling)
                InitializeCoreServices();

                // 3. Setup Global Exception Handling
                SetupExceptionHandling();

                // 4. Single Instance Check
                if (!CheckSingleInstance())
                {
                    _application.Shutdown();
                    return false;
                }

                // 5. Initialize Database & Cache
                InitializeData();

                // 6. Apply Theme & Settings
                ApplySettings();

                // 7. Check Optional Features
                CheckOptionalFeatures();

                // 8. Load Plugins
                LoadPlugins();

                // 9. Register TabContent types
                RegisterTabContents();

                return true;
            }
            catch (Exception ex)
            {
                HandleStartupException(ex);
                return false;
            }
        }

        public void Run()
        {
            // 9. Launch Main Window
            try
            {
                LaunchMainWindow();
            }
            catch (Exception ex)
            {
                HandleStartupException(ex);
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // 注册核心配置服务 (SSOT)
            services.AddSingleton<ConfigurationService>(provider => ConfigurationService.Instance);
            services.AddSingleton<AppConfig>(provider => ConfigurationService.Instance.Config);

            services.AddSingleton<YiboFile.Services.Core.Error.ErrorService>(provider =>
                new YiboFile.Services.Core.Error.ErrorService(provider.GetService<ViewModels.Messaging.IMessageBus>()));
            services.AddSingleton<Services.FileOperations.FileOperationService>();
            services.AddSingleton<Services.FileOperations.TaskQueue.TaskQueueService>();

            // Configuration & Path Management
            services.AddSingleton<IConfigPathProvider, ConfigPathProvider>();
            services.AddSingleton<IConfigurationService>(sp => Services.Config.ConfigurationService.Instance);
            services.AddSingleton<Services.Config.IO.IExportService, Services.Config.IO.ExportService>();
            services.AddSingleton<Services.Config.IO.IImportService, Services.Config.IO.ImportService>();

            services.AddSingleton<YiboFile.Services.FileOperations.Undo.UndoService>(provider =>
                new YiboFile.Services.FileOperations.Undo.UndoService(provider.GetService<ViewModels.Messaging.IMessageBus>()));
            services.AddSingleton<YiboFile.Services.Archive.ArchiveService>(); // Archive Service
            services.AddSingleton<Services.FileSystem.FileOperations.IFileTemplateService, Services.FileSystem.FileOperations.FileTemplateService>();
            services.AddSingleton<Services.Backup.IBackupService, Services.Backup.BackupService>(); // Backup Service

            // Plugins
            services.AddSingleton<IPluginManager, PluginManager>();

            // Infrastructure & Data Repositories
            services.AddSingleton<Services.Data.Repositories.IFavoriteRepository, Services.Data.Repositories.SqliteFavoriteRepository>();
            services.AddSingleton<Services.Data.Repositories.ILibraryRepository, Services.Data.Repositories.SqliteLibraryRepository>();

            services.AddSingleton<FavoriteService>(provider =>
                new FavoriteService(
                    provider.GetRequiredService<Services.Data.Repositories.IFavoriteRepository>(),
                    provider.GetService<ViewModels.Messaging.IMessageBus>(),
                    _application.Dispatcher));

            services.AddSingleton<QuickAccessService>(provider =>
                new QuickAccessService(
                    provider.GetService<ViewModels.Messaging.IMessageBus>(),
                    _application.Dispatcher));

            services.AddSingleton<FolderSizeCalculationService>();

            // FileListService 需要 Dispatcher
            services.AddTransient<FileListService>(provider =>
                new FileListService(
                    _application.Dispatcher,
                    provider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>(),
                    provider.GetRequiredService<ITagService>(),
                    provider.GetService<ViewModels.Messaging.IMessageBus>(),
                    PaneId.Main));

            // LibraryService 也需要 Dispatcher
            services.AddSingleton<LibraryService>(provider =>
                new LibraryService(
                    _application.Dispatcher,
                    provider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>(),
                    provider.GetService<ViewModels.Messaging.IMessageBus>(),
                    provider.GetRequiredService<YiboFile.Services.Data.Repositories.ILibraryRepository>()));

            // FileSystemWatcherService 需要 Dispatcher
            services.AddTransient<FileSystemWatcherService>(provider =>
                new FileSystemWatcherService(
                    _application.Dispatcher,
                    provider.GetService<ViewModels.Messaging.IMessageBus>(),
                    PaneId.Main));

            // SearchService 及其依赖
            services.AddSingleton<SearchFilterService>();
            services.AddSingleton<SearchCacheService>();

            services.AddTransient<SearchResultBuilder>(provider =>
            {
                var fileListService = provider.GetRequiredService<FileListService>();
                return new SearchResultBuilder(
                   formatFileSize: size => fileListService.FormatFileSize(size),
                   getFileTagIds: path => null, // Phase 2
                   getTagName: tagId => null,   // Phase 2
                   getFileNotes: path => FileNotesService.GetFileNotes(path)
                );
            });

            services.AddTransient<SearchService>();
            services.AddSingleton<SearchHistoryService>(); // Now managed by DI
            services.AddSingleton<YiboFile.Services.Theming.CustomThemeManager>();
            services.AddSingleton<YiboFile.Services.Theming.IThemeService, YiboFile.Services.Theming.ThemeManager>();

            // 注册国际化服务
            services.AddSingleton<ILocalizationService, LocalizationService>();

            // 注册标签服务 (Core Implementation)
            services.AddSingleton<Services.Data.Repositories.ITagsRepository, Services.Data.Repositories.SqliteTagsRepository>();
            services.AddSingleton<ITagService, TagService>();

            // UI Logic Services
            services.AddSingleton<TabContentRegistry>();
            services.AddTransient<TabService>();
            services.AddTransient<ColumnService>();

            // Register Dispatcher
            services.AddSingleton(_application.Dispatcher);

            // MVVM Messaging Infrastructure (Mediator Pattern)
            services.AddSingleton<ViewModels.Messaging.IMessageBus>(provider =>
                new ViewModels.Messaging.MessageBus(provider.GetRequiredService<System.Windows.Threading.Dispatcher>()));

            // 备注模块
            services.AddSingleton<Services.Data.Repositories.INotesRepository, Services.Data.Repositories.SqliteNotesRepository>();
            services.AddSingleton<Services.Features.FileNotes.INotesService, Services.Features.FileNotes.NotesService>();

            // ViewModels
            services.AddSingleton<NavigationCoordinator>();
            services.AddSingleton<ViewModels.NavigationRailViewModel>();
            services.AddSingleton<Controllers.NavigationRailCoordinator>();
            services.AddSingleton<NavigationService>(provider =>
                new NavigationService(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    provider.GetService<ViewModels.Messaging.IMessageBus>()));

            // Window Logic Services (Now in DI)
            services.AddSingleton<WindowStateManager>();
            services.AddSingleton<YiboFile.Services.Navigation.NavigationModeService>();

            // Window Orchestration
            services.AddSingleton<Services.Orchestration.IWindowOrchestrator, Services.Orchestration.WindowOrchestrator>();

            // Invoke external configuration callback (for Pro/Ultra extensions)
            _serviceConfigCallback?.Invoke(services);
        }

        private void InitializeCoreServices()
        {
            // 显式设置单例的消息总线
            var messageBus = ServiceProvider.GetRequiredService<ViewModels.Messaging.IMessageBus>();
            ConfigurationService.Instance.SetMessageBus(messageBus);
            Services.FileOperations.ClipboardService.Instance.SetMessageBus(messageBus);
        }

        private void SetupExceptionHandling()
        {
            var errorService = ServiceProvider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>();

            // 1. UI线程未捕获异常
            _application.DispatcherUnhandledException += (s, args) =>
            {
                errorService.ReportError($"UI线程发生未捕获异常: {args.Exception.GetType().Name}", YiboFile.Services.Core.Error.ErrorSeverity.Critical, args.Exception);
                args.Handled = true; // 防止程序直接崩溃
            };

            // 2. 非UI线程未捕获异常
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var exp = args.ExceptionObject as Exception;
                errorService.ReportError("后台线程发生致命错误", YiboFile.Services.Core.Error.ErrorSeverity.Critical, exp);
            };

            // 3. Task未观察到的异常
            TaskScheduler.UnobservedTaskException += (s, args) =>
            {
                errorService.ReportError("后台任务发生异常", YiboFile.Services.Core.Error.ErrorSeverity.Error, args.Exception);
                args.SetObserved(); // 标记为已观察，防止程序崩溃
            };
        }

        private bool CheckSingleInstance()
        {
            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            _mutexOwned = createdNew;

            if (!createdNew)
            {
                // 检查是否启用了多窗口支持
                var config = YiboFile.Services.Config.ConfigurationService.Instance.Config;
                if (config != null && config.EnableMultiWindow)
                {
                    FileLogger.Log("Function: Multi-Window enabled. Proceeding to launch new instance.");
                    return true;
                }
                else
                {
                    // 已有实例在运行且未启用多窗口 -> 激活现有窗口
                    ActivateExistingInstance();
                    return false;
                }
            }

            FileLogger.Log("Application passing single instance check.");
            return true;
        }

        private void InitializeData()
        {
            FileLogger.Log("Initializing DatabaseManager...");
            DatabaseManager.Initialize();

            // 清理过期的 CHM 缓存
            Task.Run(() =>
            {
                try
                {
                    YiboFile.Services.ChmCacheManager.CleanupExpiredCache();
                    YiboFile.Services.ChmCacheManager.EnforceCacheSizeLimit();
                }
                catch (Exception) { }
            });
        }

        private void ApplySettings()
        {
            try
            {
                var config = YiboFile.Services.Config.ConfigurationService.Instance.Config;
                var themeMode = config?.ThemeMode ?? "FollowSystem";

                // 设置动画启用状态
                (ServiceProvider.GetRequiredService<YiboFile.Services.Theming.IThemeService>() as YiboFile.Services.Theming.ThemeManager).AnimationsEnabled = config?.AnimationsEnabled ?? true;

                // 根据主题模式应用主题
                if (themeMode == "FollowSystem")
                {
                    ServiceProvider.GetRequiredService<YiboFile.Services.Theming.IThemeService>().EnableSystemThemeFollowing();
                    FileLogger.Log("System theme following enabled.");
                }
                else
                {
                    // 使用显式指定的主题
                    ServiceProvider.GetRequiredService<YiboFile.Services.Theming.IThemeService>().SetTheme(themeMode, animate: false);
                    FileLogger.Log($"Theme applied: {themeMode}");
                }

                // 应用UI风格
                var uiStyle = config?.UIStyle ?? "Original";
                ServiceProvider.GetRequiredService<YiboFile.Services.Theming.IThemeService>().SetUIStyle(uiStyle);
                FileLogger.Log($"UI Style applied: {uiStyle}");

                // 应用图标风格
                var iconStyle = config?.IconStyle ?? "Emoji";
                ServiceProvider.GetRequiredService<YiboFile.Services.Theming.IThemeService>().SetIconStyle(iconStyle);
                FileLogger.Log($"Icon Style applied: {iconStyle}");

                // 应用界面语言
                var language = config?.Language ?? "zh-CN";
                ServiceProvider.GetRequiredService<ILocalizationService>().SetLanguage(language);
                FileLogger.Log($"Language applied: {language}");
            }
            catch (Exception ex)
            {
                FileLogger.LogException("Failed to apply theme", ex);
            }
        }

        private void CheckOptionalFeatures()
        {
            // 初始化标签数据（如果可用）
            var tagService = ServiceProvider.GetService<ITagService>();
            if (tagService != null)
            {
                FileLogger.Log("ITagService found, enabling tag features.");
                IsTagTrainAvailable = true;
            }
            else
            {
                FileLogger.Log("Tag features disabled (ITagService not registered).");
                IsTagTrainAvailable = false;
            }
        }

        private void LaunchMainWindow()
        {
            FileLogger.Log("Starting MainWindow...");
            var mainWindow = new MainWindow();
            mainWindow.Show();
            FileLogger.Log("MainWindow.Show called.");

            // 应用窗口透明度和全局字体设置
            try
            {
                var config = YiboFile.Services.Config.ConfigurationService.Instance.Config;
                if (config?.WindowOpacity > 0 && config.WindowOpacity <= 1.0)
                {
                    mainWindow.Opacity = config.WindowOpacity;
                    FileLogger.Log($"Window opacity applied: {config.WindowOpacity}");
                }
                
                if (config?.UIFontSize >= 10 && config.UIFontSize <= 48)
                {
                    mainWindow.FontSize = config.UIFontSize;
                    FileLogger.Log($"UIFontSize applied: {config.UIFontSize}");
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException("Failed to apply window visual configurations", ex);
            }
        }

        private void HandleStartupException(Exception ex)
        {
            ILocalizationService loc = null;
            try { loc = ServiceProvider?.GetService<ILocalizationService>(); } catch { }

            string defaultErrorTitle = "启动错误";
            string title = loc?["Dialog.Error"] ?? defaultErrorTitle;

            // 记录异常并显示错误消息
            string errorMsg = loc != null ? loc.Get("Bootstrapper.StartupFailed", ex.Message) : $"程序启动失败: {ex.Message}";
            if (ex.InnerException != null)
            {
                string inner = loc != null ? loc.Get("Bootstrapper.InnerException", ex.InnerException.Message) : $"\n\n内部异常: {ex.InnerException.Message}";
                errorMsg += inner;
            }
            string stack = loc != null ? loc["Bootstrapper.StackTrace"] : "\n\n堆栈跟踪:\n";
            errorMsg += $"\n{stack}{ex.StackTrace}";

            MessageBox.Show(errorMsg, title, MessageBoxButton.OK, MessageBoxImage.Error);

            // 写入日志文件
            try
            {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] 启动错误:\n{errorMsg}\n\n");
            }
            catch { }

            _application.Shutdown();
        }

        public void Dispose()
        {
            // 取消系统主题监听
            try
            {
                ServiceProvider.GetRequiredService<YiboFile.Services.Theming.IThemeService>().DisableSystemThemeFollowing();
            }
            catch (Exception ex)
            {
                FileLogger.LogException("Failed to disable system theme following", ex);
            }

            // 释放Mutex - 只在我们拥有它时才释放
            if (_mutex != null && _mutexOwned)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException) { }
                finally
                {
                    _mutex.Dispose();
                    _mutex = null;
                    _mutexOwned = false;
                }
            }
        }

        private void LoadPlugins()
        {
            try
            {
                var pluginManager = ServiceProvider.GetService<IPluginManager>();
                if (pluginManager != null)
                {
                    string pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                    Task.Run(async () => await pluginManager.LoadPluginsAsync(pluginsDir)).Wait();
                }
            }
            catch (Exception ex)
            {
                YiboFile.Services.Core.FileLogger.LogException("Failed to load plugins", ex);
            }
        }

        /// <summary>
        /// 注册所有内置标签页内容类型到 TabContentRegistry。
        /// Pro/Ultra 通过 ConfigureServices 注册额外类型。
        /// 第三方插件通过 PluginManager 自动发现 ITabPageExtension。
        /// </summary>
        private void RegisterTabContents()
        {
            try
            {
                var registry = ServiceProvider.GetRequiredService<TabContentRegistry>();
                var loc = ServiceProvider.GetRequiredService<ILocalizationService>();

                // ── 文件浏览类 ──
                registry.Register(TabContentTypes.Path,
                    () => new FileBrowserTabContent(TabContentTypes.Path),
                    new TabContentMetadata { Title = loc["TabContent.FileBrowser"], AllowMultiple = true, SupportsSecondaryPane = true });

                registry.Register(TabContentTypes.Library,
                    () => new FileBrowserTabContent(TabContentTypes.Library),
                    new TabContentMetadata { Title = loc["TabContent.Library"], IconKey = "Icon_Nav_Library", AllowMultiple = true, SupportsSecondaryPane = true });

                registry.Register(TabContentTypes.Tag,
                    () => new FileBrowserTabContent(TabContentTypes.Tag),
                    new TabContentMetadata { Title = loc["TabContent.Tag"], IconKey = "Icon_Nav_Tag", AllowMultiple = true, SupportsSecondaryPane = true });

                registry.Register(TabContentTypes.Search,
                    () => new FileBrowserTabContent(TabContentTypes.Search),
                    new TabContentMetadata { Title = loc["TabContent.Search"], IconKey = "Icon_Nav_Search", AllowMultiple = true, SupportsSecondaryPane = true });

                // ── 功能面板类 ──
                registry.Register(TabContentTypes.Settings,
                    () => new SettingsTabContent(),
                    new TabContentMetadata { Title = loc["TabContent.Settings"], IconKey = "Icon_Window_Settings", AllowMultiple = false, SupportsSecondaryPane = false });

                registry.Register(TabContentTypes.About,
                    () => new AboutTabContent(),
                    new TabContentMetadata { Title = loc["TabContent.About"], IconKey = "Icon_Window_About", AllowMultiple = false, SupportsSecondaryPane = true });

                registry.Register(TabContentTypes.Management,
                    () => new ManagementTabContent(),
                    new TabContentMetadata { Title = loc["TabContent.Management"], IconKey = "Icon_Nav_Library", AllowMultiple = false, SupportsSecondaryPane = true });

                registry.Register(TabContentTypes.Backup,
                    () => new BackupTabContent(),
                    new TabContentMetadata { Title = loc["TabContent.Backup"], IconKey = "Icon_Folder", AllowMultiple = false, SupportsSecondaryPane = true });

                registry.Register(TabContentTypes.Clipboard,
                    () => new ClipboardTabContent(),
                    new TabContentMetadata { Title = loc["TabContent.Clipboard"], IconKey = "Icon_Copy", AllowMultiple = false, SupportsSecondaryPane = true });

                FileLogger.Log($"TabContentRegistry: Registered {registry.GetRegisteredIds().Count} content types");
            }
            catch (Exception ex)
            {
                FileLogger.LogException("Failed to register tab contents", ex);
            }
        }

        #region Single Instance Activation

        private void ActivateExistingInstance()
        {
            var currentProcess = global::System.Diagnostics.Process.GetCurrentProcess();
            var processes = global::System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id)
                {
                    var handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        if (IsIconic(handle))
                        {
                            ShowWindow(handle, SW_RESTORE);
                        }
                        SetForegroundWindow(handle);
                        return;
                    }
                }
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        #endregion
    }
}

