using System;
using System.Collections.Generic;
using System.Windows;
using YiboFile.ViewModels.Messaging;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel
    /// 作为模块宿主和协调者
    /// </summary>
    public class MainWindowViewModel : BaseViewModel, IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly List<Modules.IModule> _modules = new();
        private bool _disposed;

        private string _currentPath;
        private string _currentNavigationMode = "Path";
        private bool _isLoading;

        #region 属性

        /// <summary>
        /// 当前路径
        /// </summary>
        public string CurrentPath
        {
            get => _currentPath;
            set => SetProperty(ref _currentPath, value);
        }

        /// <summary>
        /// 当前导航模式
        /// </summary>
        public string CurrentNavigationMode
        {
            get => _currentNavigationMode;
            set => SetProperty(ref _currentNavigationMode, value);
        }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public Modules.NavigationModule Navigation { get; set; }
        public Modules.TabsModule Tabs { get; set; }
        public Modules.LayoutModule Layout { get; set; }
        public Modules.FileOperationModule FileOperation { get; set; }
        public Modules.NotesModule Notes { get; set; }
        public Modules.TagsModule Tags { get; set; }
        public Modules.FavoritesModule Favorites { get; set; }
        public Modules.LibraryModule Library { get; set; }

        /// <summary>
        /// 主面板（左侧/上方）
        /// </summary>
        public PaneViewModel PrimaryPane { get; set; }

        /// <summary>
        /// 副面板（右侧/下方，仅在双栏模式启用）
        /// </summary>
        public PaneViewModel SecondaryPane { get; set; }

        /// <summary>
        /// 兼容旧代码的文件列表 VM (将逐步迁移到 PrimaryPane)
        /// </summary>
        [Obsolete("Use PrimaryPane instead")]
        public FileListViewModel FileList { get; set; }

        #endregion

        public MainWindowViewModel(IMessageBus messageBus)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            // 订阅核心消息
            _messageBus.Subscribe<Messaging.Messages.PathChangedMessage>(OnPathChanged);
            _messageBus.Subscribe<Messaging.Messages.NavigationModeChangedMessage>(OnNavigationModeChanged);
        }

        #region 模块管理

        /// <summary>
        /// 注册模块
        /// </summary>
        public void RegisterModule(Modules.IModule module)
        {
            if (module == null) return;
            _modules.Add(module);
        }

        /// <summary>
        /// 初始化所有模块
        /// </summary>
        public void InitializeModules()
        {
            foreach (var module in _modules)
            {
                try
                {
                    module.Initialize();
                    System.Diagnostics.Debug.WriteLine($"[MainWindowViewModel] Module initialized: {module.Name}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindowViewModel] Module init failed: {module.Name} - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 关闭所有模块
        /// </summary>
        public void ShutdownModules()
        {
            foreach (var module in _modules)
            {
                try
                {
                    module.Shutdown();
                }
                catch { }
            }
        }

        #endregion

        #region 消息处理

        private void OnPathChanged(Messaging.Messages.PathChangedMessage message)
        {
            CurrentPath = message.NewPath;
        }

        private void OnNavigationModeChanged(Messaging.Messages.NavigationModeChangedMessage message)
        {
            CurrentNavigationMode = message.Mode;
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;

            ShutdownModules();
            foreach (var module in _modules)
            {
                module.Dispose();
            }
            _modules.Clear();

            _disposed = true;
        }
    }
}
