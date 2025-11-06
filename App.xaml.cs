using System;
using System.Windows;
using System.Threading;

namespace OoiMRR
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static Mutex _mutex = null;
        private const string MutexName = "OoiMRR_SingleInstance_Mutex";

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
                
                // 初始化数据库
                DatabaseManager.Initialize();
                
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
