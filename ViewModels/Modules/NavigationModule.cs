using System;
using System.Windows.Input;
using YiboFile.Services.Navigation;
using YiboFile.Services.Core;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Models.Navigation;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 导航模块
    /// 处理路径导航、历史记录、前进/后退、以及导航模式切换（路径/库/标签）
    /// </summary>
    public class NavigationModule : ModuleBase
    {
        private readonly NavigationService _navigationService;
        private readonly INavigationCoordinator _navigationCoordinator;
        private readonly Func<PaneId> _activePaneResolver;
        private string _currentMode = "Path";

        public override string Name => "Navigation";

        /// <summary>
        /// 当前路径
        /// </summary>
        public string CurrentPath => _navigationService.CurrentPath;

        private PaneId ActivePane => _activePaneResolver?.Invoke() ?? PaneId.Main;

        /// <summary>
        /// 是否可以后退
        /// </summary>
        public bool CanNavigateBack => _navigationService.CanNavigateBackFor(ActivePane);

        /// <summary>
        /// 是否可以前进
        /// </summary>
        public bool CanNavigateForward => _navigationService.CanNavigateForwardFor(ActivePane);

        /// <summary>
        /// 当前导航模式 (Path, Library, Tag, Search, Tasks, Backup, Clipboard)
        /// </summary>
        public string CurrentMode
        {
            get => _currentMode;
            set
            {
                if (SetProperty(ref _currentMode, value))
                {
                    OnModeChanged(value);
                }
            }
        }

        public ICommand NavigateBackCommand { get; private set; }
        public ICommand NavigateForwardCommand { get; private set; }
        public ICommand NavigateUpCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand NavigateToCommand { get; private set; }
        public ICommand SwitchModeCommand { get; private set; }

        public NavigationModule(
            IMessageBus messageBus,
            NavigationService navigationService,
            INavigationCoordinator navigationCoordinator,
            Func<PaneId> activePaneResolver)
            : base(messageBus)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _activePaneResolver = activePaneResolver;
            _currentMode = YiboFile.Services.Config.ConfigurationService.Instance.Config.LastNavigationMode ?? "Path";

            InitializeCommands();
        }

        private void InitializeCommands()
        {
            NavigateBackCommand = new RelayCommand(
                () => NavigateBack(),
                () => CanNavigateBack);

            NavigateForwardCommand = new RelayCommand(
                () => NavigateForward(),
                () => CanNavigateForward);

            NavigateUpCommand = new RelayCommand(
                () => NavigateUp(),
                () => !string.IsNullOrEmpty(_navigationService.GetCurrentPath(ActivePane)));

            RefreshCommand = new RelayCommand(
                () => Publish(new RefreshFileListMessage()));

            NavigateToCommand = new RelayCommand<string>(
                path => NavigateTo(path));

            SwitchModeCommand = new RelayCommand<string>(
                mode => CurrentMode = mode);
        }

        private void OnModeChanged(string mode)
        {
            // 同步到全局配置
            YiboFile.Services.Config.ConfigurationService.Instance.Set(cfg => cfg.LastNavigationMode, mode);
        }

        private void UpdateCommandStates()
        {
            OnPropertyChanged(nameof(CanNavigateBack));
            OnPropertyChanged(nameof(CanNavigateForward));
            OnPropertyChanged(nameof(CurrentPath));

            CommandManager.InvalidateRequerySuggested();
        }

        protected override void OnInitialize()
        {
            // 订阅导航请求消息
            Subscribe<NavigateToPathMessage>(OnNavigateToPath);
            Subscribe<NavigateBackMessage>(OnNavigateBack);
            Subscribe<NavigateForwardMessage>(OnNavigateForward);
            Subscribe<NavigateUpMessage>(OnNavigateUp);
            Subscribe<NavigationModeChangedMessage>(m => CurrentMode = m.Mode);
            Subscribe<TagClickedMessage>(OnTagClicked);

            // Subscribe to completion to update UI states
            Subscribe<NavigationCompleteMessage>(msg => UpdateCommandStates());
        }

        #region 消息处理

        private void OnNavigateToPath(NavigateToPathMessage message)
        {
            if (string.IsNullOrEmpty(message.Path)) return;

            var pane = message.Pane ?? ActivePane;

            _navigationService.NavigateTo(pane, message.Path, message.AddToHistory);

            // Notify Coordinator (UI updates, Focus etc)
            _navigationCoordinator.HandlePathNavigation(
                message.Path,
                NavigationSource.External,
                ClickType.LeftClick,
                pane: pane);
        }

        private void OnNavigateBack(NavigateBackMessage message)
        {
            var pane = message.Pane ?? ActivePane;
            // NavigationService.NavigateBack internally updates state and publishes
            // NavigationCompleteMessage, which PaneViewModel subscribes to.
            // Do NOT call NavigationCoordinator here — it would cause double-refresh
            // and unwanted tab creation when crossing content-type boundaries.
            _navigationService.NavigateBack(pane);
        }

        private void OnNavigateForward(NavigateForwardMessage message)
        {
            var pane = message.Pane ?? ActivePane;
            _navigationService.NavigateForward(pane);
        }

        private void OnNavigateUp(NavigateUpMessage message)
        {
            var pane = message.Pane ?? ActivePane;
            // NavigationService.NavigateUp internally calls NavigateTo which publishes
            // NavigationCompleteMessage. No coordinator call needed.
            _navigationService.NavigateUp(pane);
        }

        private void OnTagClicked(TagClickedMessage message)
        {
            if (message.Tag != null && !string.IsNullOrEmpty(message.Tag.Name))
            {
                _navigationCoordinator.HandlePathNavigation(
                    $"tag://{message.Tag.Name}",
                    NavigationSource.AddressBar,
                    ClickType.LeftClick,
                    pane: message.TargetPane
                );
            }
        }

        #endregion

        #region 辅助方法

        public string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            bool isVirtualPath = ProtocolManager.IsVirtual(path);
            if (!isVirtualPath && !System.IO.Directory.Exists(path) && System.IO.File.Exists(path))
            {
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".zip" || ext == ".7z" || ext == ".rar" || ext == ".tar" || ext == ".gz")
                {
                    return $"zip://{path}|";
                }
            }
            return path;
        }

        #endregion

        #region 公开方法（供直接调用）

        public void NavigateTo(string path, bool addToHistory = true)
        {
            var pane = ActivePane;
            Publish(new NavigateToPathMessage(path, addToHistory, pane));
        }

        public void NavigateBack() => Publish(new NavigateBackMessage(ActivePane));
        public void NavigateForward() => Publish(new NavigateForwardMessage(ActivePane));
        public void NavigateUp() => Publish(new NavigateUpMessage(ActivePane));

    #endregion
    }
}
