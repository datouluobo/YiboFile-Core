using System.Threading.Tasks;

namespace YiboFile.Services.Orchestration
{
    /// <summary>
    /// 窗口编排器接口
    /// 负责 MainWindow 的初始化序列和模块协调
    /// </summary>
    public interface IWindowOrchestrator
    {
        /// <summary>
        /// 执行完整的初始化序列
        /// </summary>
        /// <param name="window">关联的 MainWindow 实例</param>
        Task InitializeAsync(MainWindow window);

        /// <summary>
        /// 第一阶段：服务初始化
        /// </summary>
        void InitializeServices(MainWindow window);

        /// <summary>
        /// 第二阶段：MVVM 模块初始化与依赖注入
        /// </summary>
        void InitializeMvvmModules(MainWindow window);

        /// <summary>
        /// 第三阶段：处理器与事件挂载
        /// </summary>
        void InitializeHandlers(MainWindow window);

        /// <summary>
        /// 第四阶段：状态恢复与初始数据加载
        /// </summary>
        Task ApplyInitialStateAsync(MainWindow window);
    }
}
