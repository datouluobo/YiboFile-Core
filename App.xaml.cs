using System;
using System.IO;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using OoiMRR.Services;
using OoiMRR.Services.Core;
using TagTrain.Services;
using OoiMRR.Controls;

namespace OoiMRR
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _mutex = null;
        private const string MutexName = "OoiMRR_SingleInstance_Mutex";
        
        /// <summary>
        /// TagTrain 是否可用
        /// </summary>
        public static bool IsTagTrainAvailable { get; private set; } = false;

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[GlobalException] Type: {e.Exception.GetType().Name}, Message: {e.Exception.Message}");
            System.Diagnostics.Debug.WriteLine($"[GlobalException] StackTrace: {e.Exception.StackTrace}");
            // e.Handled = true; // Uncomment if we want to suppress crash, but we want to see it for now (or maybe just log)
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // 检查启动参数：如果是 --tagtrain，直接打开 TagTrain 窗口
                if (e.Args.Length > 0 && e.Args[0] == "--tagtrain")
                {
                    // TagTrain 模式：跳过单实例检查，直接打开 TrainingWindow
                    base.OnStartup(e);
                    
                    // 初始化 TagTrain
                    InitializeTagTrain();
                    
                    // 打开 TagTrain 主窗口
                    var trainingWindow = new TagTrain.UI.TrainingWindow();
                    trainingWindow.Show();
                    
                    // 设置为主窗口，确保关闭时程序退出
                    MainWindow = trainingWindow;
                    return;
                }
                
                // 原有的 OoiMRR 启动逻辑
                // 检查是否已有实例运行
                bool createdNew;
                _mutex = new Mutex(true, MutexName, out createdNew);
                
                if (!createdNew)
                {
                    // 已有实例在运行
                    MessageBox.Show("程序已在运行中，请勿重复启动。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
                
                // TagTrain 改为延迟加载，不再在启动时初始化模型
                // 只确保数据目录存在，模型在实际使用时再加载
                try
                {
                    FileLogger.Log("Configuring TagTrain storage...");
                    // 优先使用 OoiMRR 自己保存的 TT 数据目录（持久化），避免默认落到 OoiMRR\\bin\\data
                    var appConfig = ConfigManager.Load();
                    var tagTrainDataDir = appConfig?.TagTrainDataDirectory;
                    
                    if (!string.IsNullOrWhiteSpace(tagTrainDataDir) && Directory.Exists(tagTrainDataDir))
                    {
                        // 显式设置，确保 TT 使用该路径
                        SettingsManager.SetDataStorageDirectory(tagTrainDataDir);
                        // 清除缓存，确保使用新设置的路径
                        TagTrain.Services.SettingsManager.ClearCache();
                        TagTrain.Services.DataManager.ClearDatabasePathCache();
                    }
                    
                    // 读取（可能是刚刚设置的）数据目录
                    tagTrainDataDir = TagTrain.Services.SettingsManager.GetDataStorageDirectory();
                    if (string.IsNullOrWhiteSpace(tagTrainDataDir))
                    {
                        // 设置为空时兜底一次（不写回设置）
                        var ooiMRRBinDir = AppDomain.CurrentDomain.BaseDirectory;
                        var ooiMRRProjectDir = Path.GetFullPath(Path.Combine(ooiMRRBinDir, "..", "..", ".."));
                        var githubDir = Path.GetDirectoryName(ooiMRRProjectDir);
                        tagTrainDataDir = Path.Combine(githubDir ?? ooiMRRProjectDir, "TagTrain", "data");
                        // 设置默认路径后也要清除缓存
                        if (!string.IsNullOrWhiteSpace(tagTrainDataDir))
                        {
                            TagTrain.Services.SettingsManager.SetDataStorageDirectory(tagTrainDataDir);
                            TagTrain.Services.SettingsManager.ClearCache();
                            TagTrain.Services.DataManager.ClearDatabasePathCache();
                        }
                    }
                    tagTrainDataDir = Path.GetFullPath(tagTrainDataDir);
                    System.Diagnostics.Debug.WriteLine($"TagTrain 数据目录路径(来自设置): {tagTrainDataDir}");
                    
                    // 确保目录存在（只创建，不覆盖设置）
                    if (!Directory.Exists(tagTrainDataDir))
                    {
                        Directory.CreateDirectory(tagTrainDataDir);
                    }
                    
                    // 只初始化数据库，不加载模型（延迟加载）
                    TagTrain.Services.DataManager.InitializeDatabase();
                    
                    System.Diagnostics.Debug.WriteLine($"TagTrain 数据库路径: {TagTrain.Services.DataManager.GetDatabasePath()}");
                    IsTagTrainAvailable = true;
                    FileLogger.Log("TagTrain initialized successfully.");
                }
                catch (Exception ex)
                {
                    IsTagTrainAvailable = false;
                    FileLogger.LogException("TagTrain initialization failed", ex);
                    // 不阻止程序启动，只是记录错误
                }
                
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

        /// <summary>
        /// 初始化 TagTrain（提取为独立方法，供启动参数模式使用）
        /// </summary>
        private void InitializeTagTrain()
        {
            try
            {
                // 确保默认数据目录存在（程序目录下的 data 目录）
                var defaultDataDir = TagTrain.Services.SettingsManager.GetDataStorageDirectory();
                if (!Directory.Exists(defaultDataDir))
                {
                    try
                    {
                        Directory.CreateDirectory(defaultDataDir);
                    }
                    catch (Exception)
                    {
                    }
                }
                
                // 迁移旧配置文件到统一配置
                TagTrain.Services.SettingsManager.MigrateOldSettings();
                
                // 初始化数据库
                TagTrain.Services.DataManager.InitializeDatabase();
                
                IsTagTrainAvailable = true;
            }
            catch
            {
                IsTagTrainAvailable = false;
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 释放Mutex
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _mutex = null;
            }
            
            base.OnExit(e);
        }
    }
}
