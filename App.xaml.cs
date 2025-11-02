using System;
using System.Windows;

namespace OoiMRR
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 初始化数据库
            DatabaseManager.Initialize();
            
            // 启动主窗口
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
