using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using YiboFile.ViewModels.Messaging;
using YiboFile.Services;
using YiboFile.Services.FileList;
using YiboFile.Models;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel
    /// 作为模块宿主和协调者
    /// </summary>
    public class MainWindowViewModel : BaseViewModel, IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly Handlers.SelectionEventHandler _selectionHandler;
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
        private PaneViewModel _primaryPane;
        public PaneViewModel PrimaryPane
        {
            get => _primaryPane;
            set => SetProperty(ref _primaryPane, value);
        }

        private PaneViewModel _secondaryPane;
        public PaneViewModel SecondaryPane
        {
            get => _secondaryPane;
            set => SetProperty(ref _secondaryPane, value);
        }

        private PaneViewModel _activePane;
        public PaneViewModel ActivePane
        {
            get => _activePane;
            set => SetProperty(ref _activePane, value);
        }

        /// <summary>
        /// 选择处理器
        /// </summary>
        public Handlers.SelectionEventHandler SelectionHandler => _selectionHandler;

        #endregion

        public MainWindowViewModel(
            IMessageBus messageBus,
            RightPanelViewModel rightPanel,
            Services.Preview.PreviewService previewService,
            FileListService fileListService,
            FolderSizeCalculationService folderSizeService)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            RightPanel = rightPanel ?? throw new ArgumentNullException(nameof(rightPanel));

            // Initialize Selection Handler
            _selectionHandler = new Handlers.SelectionEventHandler(
                previewService,
                _messageBus,
                fileListService,
                () => ActivePane?.FileList?.Files?.ToList() ?? new List<Models.FileSystemItem>(),
                () => ActivePane?.CurrentPath,
                () => ActivePane?.CurrentLibrary,
                () => PrimaryPane != null && SecondaryPane != null, // Simplified IsDualMode check
                path => folderSizeService != null ? folderSizeService.CalculateAndUpdateFolderSizeAsync(path) : System.Threading.Tasks.Task.CompletedTask,
                (item, pane) => _messageBus.Publish(new Messaging.Messages.ShowFileInfoMessage(item, pane)), // Use message instead of direct Action
                (lib, pane) => _messageBus.Publish(new Messaging.Messages.ShowLibraryInfoMessage(lib, pane))   // Use message instead of direct Action
            );

            // 订阅核心消息
            _messageBus.Subscribe<Messaging.Messages.PathChangedMessage>(OnPathChanged);
            _messageBus.Subscribe<Messaging.Messages.NavigationModeChangedMessage>(OnNavigationModeChanged);
            _messageBus.Subscribe<Messaging.Messages.FocusedPaneChangedMessage>(OnFocusedPaneChanged);
            _messageBus.Subscribe<Messaging.Messages.FileSelectionChangedMessage>(OnFileSelectionChanged);
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

        private void OnFileSelectionChanged(Messaging.Messages.FileSelectionChangedMessage message)
        {
            _selectionHandler?.HandleSelectionChanged(message.SelectedItems, message.Pane);
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
