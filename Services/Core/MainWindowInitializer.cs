using System;
using YiboFile.Services.Config;
using YiboFile.Services.Core;
using YiboFile;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.UI.Adapters;

namespace YiboFile.Services
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
            // Deprecated: Moved to WindowOrchestrator
            FileLogger.Log("InitializeConfigServices (Deprecated) called - skipping.");
        }

        /// <summary>
        /// 第二阶段：应用初始状态（必须在 InitializeHandlers 和 InitializeEvents 之后调用）
        /// </summary>
        public void ApplyInitialState()
        {
            // Deprecated: Moved to WindowOrchestrator
            FileLogger.Log("ApplyInitialState (Deprecated) called - skipping.");
        }

        /// <summary>
        /// 加载初始数据
        /// </summary>
        private void LoadInitialData()
        {
            // Deprecated
        }

        /// <summary>
        /// 恢复最后的状态
        /// </summary>
        private void RestoreLastState(AppConfig config)
        {
            // Deprecated
        }
    }
}

