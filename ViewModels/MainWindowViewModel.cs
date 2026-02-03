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

        private Modules.NavigationModule _navigation;
        public Modules.NavigationModule Navigation
        {
            get => _navigation;
            set => SetProperty(ref _navigation, value);
        }

        private Modules.TabsModule _tabs;
        public Modules.TabsModule Tabs
        {
            get => _tabs;
            set => SetProperty(ref _tabs, value);
        }

        private Modules.LayoutModule _layout;
        public Modules.LayoutModule Layout
        {
            get => _layout;
            set => SetProperty(ref _layout, value);
        }

        private Modules.FileOperationModule _fileOperation;
        public Modules.FileOperationModule FileOperation
        {
            get => _fileOperation;
            set => SetProperty(ref _fileOperation, value);
        }

        private Modules.NotesModule _notes;
        public Modules.NotesModule Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private Modules.TagsModule _tags;
        public Modules.TagsModule Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        private Modules.FavoritesModule _favorites;
        public Modules.FavoritesModule Favorites
        {
            get => _favorites;
            set => SetProperty(ref _favorites, value);
        }

        private Modules.LibraryModule _library;
        public Modules.LibraryModule Library
        {
            get => _library;
            set => SetProperty(ref _library, value);
        }

        private Modules.SearchModule _search;
        public Modules.SearchModule Search
        {
            get => _search;
            set => SetProperty(ref _search, value);
        }

        private RightPanelViewModel _rightPanel;
        public RightPanelViewModel RightPanel
        {
            get => _rightPanel;
            set => SetProperty(ref _rightPanel, value);
        }
        /// <summary>
        /// 主面板（左侧/上方）
        /// </summary>
        public PaneViewModel PrimaryPane { get; set; }

        /// <summary>
        /// 副面板（右侧/下方，仅在双栏模式启用）
        /// </summary>
        public PaneViewModel SecondaryPane { get; set; }

        private PaneViewModel _activePane;
        public PaneViewModel ActivePane
        {
            get => _activePane;
            set => SetProperty(ref _activePane, value);
        }


        #endregion

        public MainWindowViewModel(IMessageBus messageBus, RightPanelViewModel rightPanel)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            RightPanel = rightPanel ?? throw new ArgumentNullException(nameof(rightPanel));

            // 订阅核心消息
            _messageBus.Subscribe<Messaging.Messages.PathChangedMessage>(OnPathChanged);
            _messageBus.Subscribe<Messaging.Messages.NavigationModeChangedMessage>(OnNavigationModeChanged);
            _messageBus.Subscribe<Messaging.Messages.FocusedPaneChangedMessage>(OnFocusedPaneChanged);
        }

        private void OnFocusedPaneChanged(Messaging.Messages.FocusedPaneChangedMessage message)
        {
            if (message.IsSecondPaneFocused)
            {
                ActivePane = SecondaryPane;
            }
            else
            {
                ActivePane = PrimaryPane;
            }
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
