using System;
using System.IO;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using OoiMRR.Services;
using OoiMRR.Services.Core;
// using TagTrain.Services; // Phase 2将重新实现标签功能
using OoiMRR.Controls;

namespace OoiMRR
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _mutex = null;
        private static bool _mutexOwned = false; // 跟踪是否拥有mutex
        private const string MutexName = "OoiMRR_SingleInstance_Mutex";

        /// <summary>
        /// 标签功能是否可用（Phase 2将实现）
        /// </summary>
        public static bool IsTagTrainAvailable => false;

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            // e.Handled = true; // Uncomment if we want to suppress crash, but we want to see it for now (or maybe just log)
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // 移除了 --tagtrain 启动参数支持（Phase 2将重新实现）

                // 原有的 OoiMRR 启动逻辑
                // 检查是否已有实例运行
                bool createdNew;
                _mutex = new Mutex(true, MutexName, out createdNew);
                _mutexOwned = createdNew; // 记录是否成功获得mutex所有权

                if (!createdNew)
                {
                    // 已有实例在运行
                    MessageBox.Show("程序已在运行中,请勿重复启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    Shutdown();
                    return;
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
                    OoiMRR.Services.Theming.ThemeManager.AnimationsEnabled = config?.AnimationsEnabled ?? true;

                    // 根据主题模式应用主题
                    if (themeMode == "FollowSystem")
                    {
                        OoiMRR.Services.Theming.ThemeManager.EnableSystemThemeFollowing();
                        FileLogger.Log("System theme following enabled.");
                    }
                    else
                    {
                        // 使用显式指定的主题
                        OoiMRR.Services.Theming.ThemeManager.SetTheme(themeMode, animate: false);
                        FileLogger.Log($"Theme applied: {themeMode}");
                    }
                }
                catch (Exception ex)
                {
                    FileLogger.LogException("Failed to apply theme", ex);
                }

                // 标签功能已移除，将在 Phase 2 重新实现
                FileLogger.Log("Tag features disabled, will be reimplemented in Phase 2.");

                // 清理过期的 CHM 缓存
                Task.Run(() =>
                {
                    try
                    {
                        OoiMRR.Services.ChmCacheManager.CleanupExpiredCache();
                        OoiMRR.Services.ChmCacheManager.EnforceCacheSizeLimit();
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
                OoiMRR.Services.Theming.ThemeManager.DisableSystemThemeFollowing();
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
    }
}
