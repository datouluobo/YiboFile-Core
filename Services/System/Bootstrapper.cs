using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.InteropServices;
using YiboFile.Services.Core;
using YiboFile.Services.Config;
using YiboFile.Services.Favorite;
using YiboFile.Services.QuickAccess;
using YiboFile.Services.FileList;
using YiboFile.Services.Search;
using YiboFile.Services.Navigation;
using YiboFile.Services.FileNotes;
using YiboFile.Services.Tabs;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Features;
using YiboFile.Controls;

namespace YiboFile.Services.Startup
{
    /// <summary>
    /// Bootstrapper for application startup logic.
    /// Handles dependency injection, single instance check, error handling, and resource cleanup.
    /// </summary>
    public class Bootstrapper : IDisposable
    {
        private readonly Application _application;
        private Mutex _mutex;
        private bool _mutexOwned;
        private const string MutexName = "YiboFile_SingleInstance_Mutex";

        public IServiceProvider ServiceProvider { get; private set; }
        public bool IsTagTrainAvailable { get; private set; } = false;

        public Bootstrapper(Application application)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
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
            // 8. Launch Main Window
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
            services.AddSingleton<YiboFile.Services.FileOperations.Undo.UndoService>(provider =>
                new YiboFile.Services.FileOperations.Undo.UndoService(provider.GetService<ViewModels.Messaging.IMessageBus>()));
            services.AddSingleton<YiboFile.Services.Archive.ArchiveService>(); // Archive Service
            services.AddSingleton<Services.FileSystem.FileOperations.IFileTemplateService, Services.FileSystem.FileOperations.FileTemplateService>();
            services.AddSingleton<Services.Backup.IBackupService, Services.Backup.BackupService>(); // Backup Service

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

            // 注册标签服务 (Core Implementation)
            services.AddSingleton<Services.Data.Repositories.ITagsRepository, Services.Data.Repositories.SqliteTagsRepository>();
            services.AddSingleton<ITagService, TagService>();

            // UI Logic Services
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
            services.AddSingleton<ViewModels.RightPanelViewModel>();
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
                var config = ConfigManager.Load();
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
                var config = ConfigManager.Load();
                var themeMode = config?.ThemeMode ?? "FollowSystem";

                // 设置动画启用状态
                YiboFile.Services.Theming.ThemeManager.AnimationsEnabled = config?.AnimationsEnabled ?? true;

                // 根据主题模式应用主题
                if (themeMode == "FollowSystem")
                {
                    YiboFile.Services.Theming.ThemeManager.EnableSystemThemeFollowing();
                    FileLogger.Log("System theme following enabled.");
                }
                else
                {
                    // 使用显式指定的主题
                    YiboFile.Services.Theming.ThemeManager.SetTheme(themeMode, animate: false);
                    FileLogger.Log($"Theme applied: {themeMode}");
                }
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

            // 应用窗口透明度设置
            try
            {
                var config = ConfigManager.Load();
                if (config?.WindowOpacity > 0 && config.WindowOpacity <= 1.0)
                {
                    mainWindow.Opacity = config.WindowOpacity;
                    FileLogger.Log($"Window opacity applied: {config.WindowOpacity}");
                }
            }
            catch (Exception ex)
            {
                FileLogger.LogException("Failed to apply window opacity", ex);
            }
        }

        private void HandleStartupException(Exception ex)
        {
            // 记录异常并显示错误消息
            string errorMsg = $"程序启动失败: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMsg += $"\n\n内部异常: {ex.InnerException.Message}";
            }
            errorMsg += $"\n\n堆栈跟踪:\n{ex.StackTrace}";

            MessageBox.Show(errorMsg, "启动错误", MessageBoxButton.OK, MessageBoxImage.Error);

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
                YiboFile.Services.Theming.ThemeManager.DisableSystemThemeFollowing();
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
