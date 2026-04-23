using System;
using System.IO;
using System.Windows;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Startup;
using YiboFile.Services.Core;

namespace YiboFile
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private Bootstrapper _bootstrapper;

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
            // 全局异常处理已移至 Bootstrapper 中统一配置
        }

        /// <summary>
        /// 配置依赖注入服务
        /// 子类 (Pro/Ultra) 可重写此方法以注册额外服务
        /// </summary>
        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // Core 版默认不注册额外服务
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            _bootstrapper = new Bootstrapper(this, ConfigureServices);
            bool success = _bootstrapper.Initialize();

            if (success)
            {
                // 将 Bootstrapper 初始化后的状态同步到静态属性，以保持向后兼容
                ServiceProvider = _bootstrapper.ServiceProvider;
                IsTagTrainAvailable = _bootstrapper.IsTagTrainAvailable;

                _bootstrapper.Run();
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _bootstrapper?.Dispose();
            base.OnExit(e);
        }
    }
}

