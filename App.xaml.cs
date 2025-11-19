using System;
using System.IO;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using OoiMRR.Services;
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

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
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

                base.OnStartup(e);
                
                // 初始化 FFmpeg（用于视频缩略图提取）
                try
                {
                    bool ffmpegAvailable = FFmpegHelper.InitializeFFmpeg();
                    if (ffmpegAvailable)
                    {
                        System.Diagnostics.Debug.WriteLine("FFmpeg 初始化成功，视频缩略图功能可用");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("警告：未找到可用的 FFmpeg，视频缩略图功能将不可用");
                    }
                }
                catch (Exception ffmpegEx)
                {
                    System.Diagnostics.Debug.WriteLine($"FFmpeg 初始化失败: {ffmpegEx.Message}");
                }
                
                // 初始化 Everything（用于快速文件搜索）
                _ = Task.Run(async () =>
                {
                    try
                    {
                        bool everythingAvailable = await EverythingHelper.InitializeAsync();
                        if (everythingAvailable)
                        {
                            string version = EverythingHelper.GetVersion();
                            System.Diagnostics.Debug.WriteLine($"Everything 初始化成功，快速搜索功能可用 (版本: {version})");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("警告：Everything 初始化失败，将使用传统搜索方式");
                        }
                    }
                    catch (Exception everythingEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Everything 初始化失败: {everythingEx.Message}");
                    }
                });
                
                // 初始化数据库
                DatabaseManager.Initialize();
                
                // 初始化 TagTrain（确保数据目录存在）
                try
                {
                    // 优先使用 OoiMRR 自己保存的 TT 数据目录（持久化），避免默认落到 OoiMRR\\bin\\data
                    var appConfig = ConfigManager.Load();
                    var tagTrainDataDir = appConfig?.TagTrainDataDirectory;
                    
                    if (!string.IsNullOrWhiteSpace(tagTrainDataDir) && Directory.Exists(tagTrainDataDir))
                    {
                        // 显式设置，确保 TT 使用该路径
                        SettingsManager.SetDataStorageDirectory(tagTrainDataDir);
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
                    }
                    tagTrainDataDir = Path.GetFullPath(tagTrainDataDir);
                    System.Diagnostics.Debug.WriteLine($"TagTrain 数据目录路径(来自设置): {tagTrainDataDir}");
                    
                    // 确保目录存在（只创建，不覆盖设置）
                    if (!Directory.Exists(tagTrainDataDir))
                    {
                        Directory.CreateDirectory(tagTrainDataDir);
                    }
                    
                    // 主动初始化 TagTrain
                    OoiMRRIntegration.Initialize();
                    
                    // 验证初始化是否成功
                    var tagCount = OoiMRRIntegration.GetAllTags(OoiMRR.Services.OoiMRRIntegration.TagSortMode.Name)?.Count ?? 0;
                    System.Diagnostics.Debug.WriteLine($"TagTrain 初始化成功，当前标签数: {tagCount}");
                    System.Diagnostics.Debug.WriteLine($"TagTrain 数据目录: {tagTrainDataDir}");
                    System.Diagnostics.Debug.WriteLine($"TagTrain 数据库路径: {TagTrain.Services.DataManager.GetDatabasePath()}");
                    IsTagTrainAvailable = true;
                }
                catch (Exception tagTrainEx)
                {
                    System.Diagnostics.Debug.WriteLine($"TagTrain 初始化失败: {tagTrainEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"堆栈跟踪: {tagTrainEx.StackTrace}");
                    IsTagTrainAvailable = false;
                    // 不阻止程序启动，只是记录错误
                }
                
                // 启动主窗口
                var mainWindow = new MainWindow();
                mainWindow.Show();
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
