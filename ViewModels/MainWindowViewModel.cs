using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
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
        private readonly Handlers.SelectionEventHandler _mainSelectionHandler;
        private readonly Handlers.SelectionEventHandler _secondSelectionHandler;
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


        /// <summary>面板数组 — Panes[0] 和 Panes[1] 各自对等独立</summary>
        private PaneViewModel[] _panes = new PaneViewModel[2];

        /// <summary>Pane A（默认在左侧位置）</summary>
        public PaneViewModel PrimaryPane
        {
            get => _panes[0];
            set { _panes[0] = value; OnPropertyChanged(nameof(PrimaryPane)); }
        }

        /// <summary>Pane B（默认在右侧位置）</summary>
        public PaneViewModel SecondaryPane
        {
            get => _panes[1];
            set { _panes[1] = value; OnPropertyChanged(nameof(SecondaryPane)); }
        }

        private PaneViewModel _activePane;
        public PaneViewModel ActivePane
        {
            get => _activePane;
            set
            {
                if (_activePane != value)
                {
                    if (_activePane != null) _activePane.IsActive = false;
                    _activePane = value;
                    if (_activePane != null) _activePane.IsActive = true;
                    OnPropertyChanged(nameof(ActivePane));
                    OnPropertyChanged(nameof(SelectionHandler));
                }
                else if (_activePane != null && !_activePane.IsActive)
                {
                    // 强制恢复焦点状态
                    _activePane.IsActive = true;
                }
                
                // 确保另一个面板关闭焦点外框
                var inactivePane = _activePane == _panes[0] ? _panes[1] : _panes[0];
                if (inactivePane != null)
                {
                    inactivePane.IsActive = false;
                }
            }
        }

        /// <summary>交换 Pane A / Pane B 的位置</summary>
        public ICommand SwapPanesCommand { get; }

        /// <summary>
        /// 交换左右面板位置。通过交换 ViewModel 引用实现，
        /// MVVM 数据绑定会自动更新 UI，无需搬运视觉元素。
        /// </summary>
        public void SwapPanes()
        {
            var temp = _panes[0];
            _panes[0] = _panes[1];
            _panes[1] = temp;

            _panes[0].IsSecondary = false;
            _panes[0].Selection.IsSecondary = false;
            _panes[1].IsSecondary = true;
            _panes[1].Selection.IsSecondary = true;

            // 重要：通知布局模块现在的焦点位置已经跑到另一边了，让它保存最新的状态
            if (_activePane != null)
            {
                _messageBus.Publish(new Messaging.Messages.SetFocusedPaneMessage(_activePane.IsSecondary));
            }

            OnPropertyChanged(nameof(PrimaryPane));
            OnPropertyChanged(nameof(SecondaryPane));

            // 同时调换 Tabs 的数据源
            Tabs?.SwapTabs();

            // 持久化面板顺序
            var state = Services.Config.ConfigurationService.Instance.State;
            state.PaneOrder = new List<int> 
            { 
                _panes[0].PaneLabel == "A" ? 0 : 1,
                _panes[1].PaneLabel == "A" ? 0 : 1
            };
            Services.Config.ConfigurationService.Instance.MarkDirty();

            // Notify UI that ActivePane position has potentially moved
            OnPropertyChanged(nameof(ActivePane));
            OnPropertyChanged(nameof(SelectionHandler));
        }

        /// <summary>
        /// 选择处理器
        /// </summary>
        /// <summary>
        /// 选择处理器 (Active)
        /// </summary>
        public Handlers.SelectionEventHandler SelectionHandler =>
            ActivePane == SecondaryPane ? _secondSelectionHandler : _mainSelectionHandler;

        public Handlers.SelectionEventHandler MainSelectionHandler => _mainSelectionHandler;
        public Handlers.SelectionEventHandler SecondSelectionHandler => _secondSelectionHandler;

        #endregion

        public MainWindowViewModel(
            IMessageBus messageBus,
            Services.Preview.PreviewService previewService,
            FileListService fileListService,
            FolderSizeCalculationService folderSizeService)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            SwapPanesCommand = new RelayCommand(SwapPanes);

            // Initialize Specific Selection Handlers for each pane position (0=Left/Primary, 1=Right/Secondary)
            _mainSelectionHandler = new Handlers.SelectionEventHandler(
                previewService,
                _messageBus,
                fileListService,
                () => PrimaryPane?.FileList?.Files?.ToList() ?? new List<Models.FileSystemItem>(),
                () => PrimaryPane?.CurrentPath,
                () => PrimaryPane?.CurrentLibrary,
                () => true, // unused
                path => folderSizeService != null ? folderSizeService.CalculateAndUpdateFolderSizeAsync(path) : System.Threading.Tasks.Task.CompletedTask,
                (item, pane) => _messageBus.Publish(new Messaging.Messages.ShowFileInfoMessage(item, pane)),
                (lib, pane) => _messageBus.Publish(new Messaging.Messages.ShowLibraryInfoMessage(lib, pane))
            );

            _secondSelectionHandler = new Handlers.SelectionEventHandler(
                previewService,
                _messageBus,
                fileListService,
                () => SecondaryPane?.FileList?.Files?.ToList() ?? new List<Models.FileSystemItem>(),
                () => SecondaryPane?.CurrentPath,
                () => SecondaryPane?.CurrentLibrary,
                () => true, // unused
                path => folderSizeService != null ? folderSizeService.CalculateAndUpdateFolderSizeAsync(path) : System.Threading.Tasks.Task.CompletedTask,
                (item, pane) => _messageBus.Publish(new Messaging.Messages.ShowFileInfoMessage(item, pane)),
                (lib, pane) => _messageBus.Publish(new Messaging.Messages.ShowLibraryInfoMessage(lib, pane))
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
                catch (Exception)
                {

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

            // Fix BUG-018: Force update info panel on navigation to ensure new path info is displayed
            if (message.Pane == YiboFile.Services.Navigation.PaneId.Second)
                _secondSelectionHandler?.HandleNoSelection(message.Pane);
            else
                _mainSelectionHandler?.HandleNoSelection(message.Pane);
        }

        private void OnNavigationModeChanged(Messaging.Messages.NavigationModeChangedMessage message)
        {
            CurrentNavigationMode = message.Mode;
        }

        private void OnFileSelectionChanged(Messaging.Messages.FileSelectionChangedMessage message)
        {
            if (message.Pane == YiboFile.Services.Navigation.PaneId.Second)
                _secondSelectionHandler?.HandleSelectionChanged(message.SelectedItems, message.Pane);
            else
                _mainSelectionHandler?.HandleSelectionChanged(message.SelectedItems, message.Pane);
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
