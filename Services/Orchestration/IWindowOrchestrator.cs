using System.Threading.Tasks;
using YiboFile.Handlers;

using YiboFile.Services.FileOperations;
using YiboFile.Services.Navigation;
using YiboFile.Services.Tabs;
using YiboFile.Services.Favorite;
using YiboFile.Services.QuickAccess;
using YiboFile.Services.FileList;
using YiboFile.Services.Search;
using YiboFile.Services.Core;
using YiboFile.Services.FileInfo; // For FileInfoService
using YiboFile.Services.ColumnManagement; // For ColumnService
using YiboFile.Services.UIHelper; // For IUIHelperService
using YiboFile.Services.Hardware; // For IHardwareMonitorService

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
        /// <param name="window">关联的 MainWindow 实例</param>
        Task ApplyInitialStateAsync(MainWindow window);

        #region Handler & Controller Access

        WindowLifecycleHandler LifecycleHandler { get; }

        /// <summary>每个面板的列交互处理器 [0]=左, [1]=右</summary>
        ColumnInteractionHandler[] ColumnHandlers { get; }
        /// <summary>每个面板的文件列表事件处理器</summary>
        FileListEventHandler[] FileListHandlers { get; }

        // 兼容属性
        ColumnInteractionHandler ColumnInteractionHandler { get; }
        ColumnInteractionHandler SecondColumnInteractionHandler { get; }
        FileListEventHandler MainFileListHandler { get; }
        FileListEventHandler SecondFileListHandler { get; }

        FileOperationService FileOperationService { get; }
        NavigationModeService NavigationModeService { get; }
        INavigationCoordinator NavigationCoordinator { get; }
        NavigationService NavigationService { get; }
        TabService TabService { get; }
        TabService SecondTabService { get; }
        Services.FileInfo.FileInfoService SecondFileInfoService { get; } // Requires Services.FileInfo
        ColumnService ColumnService { get; } // Requires Services.ColumnManagement
        IUIHelperService UIHelperService { get; } // Requires Services.UI.UIHelper
        LibraryService LibraryService { get; }
        FavoriteService FavoriteService { get; }
        QuickAccessService QuickAccessService { get; }
        FileListService FileListService { get; }
        FileListService SecondFileListService { get; }
        SearchService SearchService { get; }
        SearchCacheService SearchCacheService { get; }
        FileSystemWatcherService FileSystemWatcherService { get; }
        Services.WindowStateManager WindowStateManager { get; }
        IHardwareMonitorService HardwareMonitorService { get; }
        KeyboardEventHandler KeyboardEventHandler { get; }

        #endregion
    }
}
