using System;
using System.IO;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services;
using YiboFile.Services.Core;
using YiboFile.Services.Config;
using YiboFile.Services.Favorite;
using YiboFile.Services.QuickAccess;
using YiboFile.Services.FileList;
using YiboFile.Services.Search;

using YiboFile.Services.FileNotes;
using YiboFile.Services.Tabs;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Features;
using YiboFile.Controls;
using System.Runtime.InteropServices; // Added for P/Invoke

namespace YiboFile
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _mutex = null;
        private static bool _mutexOwned = false; // 跟踪是否拥有mutex
        private const string MutexName = "YiboFile_SingleInstance_Mutex";

        /// <summary>
        /// 全局服务提供者
        /// </summary>
        public static IServiceProvider ServiceProvider { get; private set; }

        /// <summary>
        /// 标签功能是否可用（由 ITagService 注入情况决定）
        /// </summary>
        public static bool IsTagTrainAvailable { get; private set; } = false;



        public App()
        {
            // 全局异常处理已移至 OnStartup 中统一配置
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // e.Handled = true; // Uncomment if we want to suppress crash, but we want to see it for now (or maybe just log)
        }

        /// <summary>
        /// 配置依赖注入服务
        /// </summary>
        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // 注册核心服务
            services.AddSingleton<AppConfig>(provider => ConfigManager.Load() ?? new AppConfig());
            services.AddSingleton<ConfigService>(); // 配置服务应为单例
            services.AddSingleton<YiboFile.Services.Core.Error.ErrorService>(); // 统一错误处理服务
            services.AddSingleton<Services.FileOperations.FileOperationService>();
            services.AddSingleton<Services.FileOperations.TaskQueue.TaskQueueService>(); // Register TaskQueueService
            services.AddSingleton<YiboFile.Services.FileOperations.Undo.UndoService>(); // 撤销/重做服务
            services.AddSingleton<YiboFile.Services.Archive.ArchiveService>(); // Archive Service
            services.AddSingleton<Services.Backup.IBackupService, Services.Backup.BackupService>(); // Backup Service

            // DatabaseManager 是静态类/单例模式，但如果我们需要注入它，可以封装一下，或者暂时保持静态访问
            // 这里我们注册那些非静态的服务

            services.AddSingleton<FavoriteService>();
            services.AddSingleton<QuickAccessService>();

            // FolderSizeCalculationService 看起来是无状态或短暂状态的，Transient 或 Singleton 都可以，这里选 Singleton 方便复用
            services.AddSingleton<FolderSizeCalculationService>();

            // FileListService 需要 Dispatcher
            services.AddTransient<FileListService>(provider =>
                new FileListService(
                    Application.Current.Dispatcher,
                    provider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>()));

            // LibraryService 也需要 Dispatcher
            services.AddSingleton<LibraryService>(provider =>
                new LibraryService(
                    Application.Current.Dispatcher,
                    provider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>()));

            // FileSystemWatcherService 需要 Dispatcher
            services.AddTransient<FileSystemWatcherService>(provider =>
                new FileSystemWatcherService(Application.Current.Dispatcher));

            // SearchService 及其依赖
            services.AddSingleton<SearchFilterService>();
            services.AddSingleton<SearchCacheService>();

            // SearchResultBuilder 需要复杂的工厂逻辑，暂时先 Transient，或者在 MainWindow 中手动创建，或者这里配置工厂
            // 由于 SearchResultBuilder 依赖 FileListService 的 FormatFileSize 等方法，反向依赖稍微有点复杂
            // 简化起见，我们先不注册 SearchService，或者使用工厂方法解决依赖
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

            // ViewModels / Windows (Optional, if we want to inject MainWindow)
            // services.AddTransient<MainWindow>();

            // 注册标签服务 (Core Implementation)
            services.AddSingleton<ITagService, TagService>();

            // UI Logic Services
            services.AddTransient<TabService>();
            services.AddTransient<ColumnService>();

            // Register Dispatcher
            services.AddSingleton(System.Windows.Application.Current.Dispatcher);

            // MVVM Messaging Infrastructure (Mediator Pattern)
            services.AddSingleton<ViewModels.Messaging.IMessageBus>(provider =>
                new ViewModels.Messaging.MessageBus(provider.GetRequiredService<System.Windows.Threading.Dispatcher>()));
        }


        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // 初始化依赖注入容器
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                ServiceProvider = serviceCollection.BuildServiceProvider();



                // 注册全局异常处理
                var errorService = ServiceProvider.GetRequiredService<YiboFile.Services.Core.Error.ErrorService>();

                // 1. UI线程未捕获异常
                this.DispatcherUnhandledException += (s, args) =>
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

                // 移除了 --tagtrain 启动参数支持（Phase 2将重新实现）

                // 原有的 YiboFile 启动逻辑
                // 检查是否已有实例运行
                bool createdNew;
                _mutex = new Mutex(true, MutexName, out createdNew);
                _mutexOwned = createdNew; // 记录是否成功获得mutex所有权

                if (!createdNew)
                {
                    // 检查是否启用了多窗口支持
                    var config = ConfigManager.Load();
                    if (config != null && config.EnableMultiWindow)
                    {
                        FileLogger.Log("Function: Multi-Window enabled. Proceeding to launch new instance.");
                        // DO NOT RETURN/SHUTDOWN
                    }
                    else
                    {
                        // 已有实例在运行且未启用多窗口 -> 激活现有窗口
                        ActivateExistingInstance();
                        Shutdown();
                        return;
                    }
                }

                FileLogger.Log("Application passing single instance check.");

                base.OnStartup(e);

                // FFmpeg 和 Everything 改为按需加载，不再在启动时初始化
                // 这样可以减少启动时间和内存占用

                // 初始化数据库
                FileLogger.Log("Initializing DatabaseManager...");
                DatabaseManager.Initialize();

                // 应用保存的主题设置
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

                // 清理过期的 CHM 缓存
                Task.Run(() =>
                {
                    try
                    {
                        YiboFile.Services.ChmCacheManager.CleanupExpiredCache();
                        YiboFile.Services.ChmCacheManager.EnforceCacheSizeLimit();
                    }
                    catch (Exception)
                    {
                    }
                });

                // 启动主窗口
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
            catch (Exception ex)
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
                    string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                    System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] 启动错误:\n{errorMsg}\n\n");
                }
                catch { }

                Shutdown();
            }
        }

        // InitializeTagTrain 方法已移除，将在 Phase 2 重新实现

        protected override void OnExit(ExitEventArgs e)
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
                catch (ApplicationException)
                {
                    // 如果mutex已经被释放或不属于当前线程,忽略异常
                }
                finally
                {
                    _mutex.Dispose();
                    _mutex = null;
                    _mutexOwned = false;
                }
            }

            base.OnExit(e);
        }

        #region Single Instance Activation

        private void ActivateExistingInstance()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);
            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id)
                {
                    // 简单的尝试激活主窗口
                    // 注意: 更严谨的做法通常涉及遍历窗口句柄或使用其它IPC机制，
                    // 但对于大多数单窗口应用，MainWindowHandle 足够。
                    var handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        // 如果窗口最小化，则还原
                        if (IsIconic(handle))
                        {
                            ShowWindow(handle, SW_RESTORE);
                        }

                        // 将窗口以前台方式显示（不改变大小状态）
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

