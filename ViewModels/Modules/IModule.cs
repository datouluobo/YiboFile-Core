using System;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 模块接口
    /// 所有功能模块需实现此接口
    /// </summary>
    public interface IModule : IDisposable
    {
        /// <summary>
        /// 模块名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 模块是否已初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 初始化模块
        /// </summary>
        void Initialize();

        /// <summary>
        /// 关闭模块
        /// </summary>
        void Shutdown();
    }
}
