using System.Windows.Input;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 导航工具栏视图模型（混合架构 - 仅状态）
    /// 职责：存储 UI 状态，由 NavigationCoordinator 驱动
    /// </summary>
    public class NavigationRailViewModel : BaseViewModel
    {
        private readonly IMessageBus _messageBus;
        private string _activeNavigationMode = "Path";
        private string _activeLayoutMode = "Work";
        private bool _isDualListMode = false;

        public NavigationRailViewModel(IMessageBus messageBus)
        {
            _messageBus = messageBus;

            // ✅ 命令仅发布请求消息，不处理业务逻辑
            NavigateToPathCommand = new RelayCommand(() =>
            {
                System.Diagnostics.Debug.WriteLine("[NAV-DEBUG] NavigationRail: User clicked 'Path' rail button");
                _messageBus.Publish(new RequestNavigationModeMessage("Path"));
            });
            NavigateToLibraryCommand = new RelayCommand(() =>
            {
                System.Diagnostics.Debug.WriteLine("[NAV-DEBUG] NavigationRail: User clicked 'Library' rail button");
                _messageBus.Publish(new RequestNavigationModeMessage("Library"));
            });
            NavigateToTagCommand = new RelayCommand(() =>
            {
                System.Diagnostics.Debug.WriteLine("[NAV-DEBUG] NavigationRail: User clicked 'Tag' rail button");
                _messageBus.Publish(new RequestNavigationModeMessage("Tag"));
            });
            NavigateToTasksCommand = new RelayCommand(() => _messageBus.Publish(new RequestNavigationModeMessage("Tasks")));
            NavigateToBackupCommand = new RelayCommand(() => _messageBus.Publish(new RequestNavigationModeMessage("Backup")));
            NavigateToClipboardCommand = new RelayCommand(() => _messageBus.Publish(new RequestNavigationModeMessage("Clipboard")));

            SetLayoutFocusCommand = new RelayCommand(() => _messageBus.Publish(new RequestLayoutModeMessage("Focus")));
            SetLayoutWorkCommand = new RelayCommand(() => _messageBus.Publish(new RequestLayoutModeMessage("Work")));
            SetLayoutFullCommand = new RelayCommand(() => _messageBus.Publish(new RequestLayoutModeMessage("Full")));
            ToggleDualListCommand = new RelayCommand(() => _messageBus.Publish(new RequestDualListToggleMessage()));

            OpenSettingsCommand = new RelayCommand(() => _messageBus.Publish(new ShowSettingsMessage()));
            OpenAboutCommand = new RelayCommand(() => _messageBus.Publish(new ShowAboutMessage()));
        }

        #region 状态属性（由 Coordinator 更新）

        /// <summary>
        /// 当前激活的导航模式
        /// </summary>
        public string ActiveNavigationMode
        {
            get => _activeNavigationMode;
            set => SetProperty(ref _activeNavigationMode, value);
        }

        /// <summary>
        /// 当前激活的布局模式
        /// </summary>
        public string ActiveLayoutMode
        {
            get => _activeLayoutMode;
            set => SetProperty(ref _activeLayoutMode, value);
        }

        /// <summary>
        /// 是否为双列表模式
        /// </summary>
        public bool IsDualListMode
        {
            get => _isDualListMode;
            set => SetProperty(ref _isDualListMode, value);
        }

        /// <summary>
        /// 标签功能是否可用
        /// </summary>
        public bool IsTagFeatureAvailable => App.IsTagTrainAvailable;

        #endregion

        #region 命令（仅发布消息）

        public ICommand NavigateToPathCommand { get; }
        public ICommand NavigateToLibraryCommand { get; }
        public ICommand NavigateToTagCommand { get; }
        public ICommand NavigateToTasksCommand { get; }
        public ICommand NavigateToBackupCommand { get; }
        public ICommand NavigateToClipboardCommand { get; }

        public ICommand SetLayoutFocusCommand { get; }
        public ICommand SetLayoutWorkCommand { get; }
        public ICommand SetLayoutFullCommand { get; }
        public ICommand ToggleDualListCommand { get; }

        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenAboutCommand { get; }

        #endregion
    }
}
