using System;
using System.Windows.Input;
using YiboFile.Services.Navigation;
using YiboFile.Services.Core;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 导航模块
    /// 处理路径导航、历史记录、前进/后退等功能
    /// </summary>
    public class NavigationModule : ModuleBase
    {
        private readonly NavigationService _navigationService;
        private readonly Action<string> _onNavigateCallback;

        public override string Name => "Navigation";

        /// <summary>
        /// 当前路径
        /// </summary>
        public string CurrentPath => _navigationService.CurrentPath;

        /// <summary>
        /// 是否可以后退
        /// </summary>
        public bool CanNavigateBack => _navigationService.CanNavigateBack;

        /// <summary>
        /// 是否可以前进
        /// </summary>
        public bool CanNavigateForward => _navigationService.CanNavigateForward;

        public ICommand NavigateBackCommand { get; private set; }
        public ICommand NavigateForwardCommand { get; private set; }
        public ICommand NavigateUpCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }
        public ICommand NavigateToCommand { get; private set; }

        public NavigationModule(
            IMessageBus messageBus,
            NavigationService navigationService,
            Action<string> onNavigateCallback = null)
            : base(messageBus)
        {
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            _onNavigateCallback = onNavigateCallback;

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
                () => !string.IsNullOrEmpty(CurrentPath)); // Simple check, could be more complex

            RefreshCommand = new RelayCommand(
                () => Publish(new RefreshFileListMessage())); // Need to define this message or use callback

            NavigateToCommand = new RelayCommand<string>(
                path => NavigateTo(path));
        }

        private void UpdateCommandStates()
        {
            OnPropertyChanged(nameof(CanNavigateBack));
            OnPropertyChanged(nameof(CanNavigateForward));
            OnPropertyChanged(nameof(CurrentPath));

            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        protected override void OnInitialize()
        {
            // 订阅导航请求消息
            Subscribe<NavigateToPathMessage>(OnNavigateToPath);
            Subscribe<NavigateBackMessage>(OnNavigateBack);
            Subscribe<NavigateForwardMessage>(OnNavigateForward);
            Subscribe<NavigateUpMessage>(OnNavigateUp);
        }

        #region 消息处理

        private void OnNavigateToPath(NavigateToPathMessage message)
        {
            if (string.IsNullOrEmpty(message.Path)) return;

            var oldPath = _navigationService.CurrentPath;
            _navigationService.NavigateTo(message.Path);

            // 发布路径变更通知
            Publish(new PathChangedMessage(message.Path, oldPath));

            // 回调（过渡期使用）
            _onNavigateCallback?.Invoke(message.Path);

            UpdateCommandStates();
        }

        private void OnNavigateBack(NavigateBackMessage message)
        {
            var oldPath = _navigationService.CurrentPath;
            var newPath = _navigationService.NavigateBack();

            if (!string.IsNullOrEmpty(newPath))
            {
                Publish(new PathChangedMessage(newPath, oldPath));
                _onNavigateCallback?.Invoke(newPath);
            }
            UpdateCommandStates();
        }

        private void OnNavigateForward(NavigateForwardMessage message)
        {
            var oldPath = _navigationService.CurrentPath;
            var newPath = _navigationService.NavigateForward();

            if (!string.IsNullOrEmpty(newPath))
            {
                Publish(new PathChangedMessage(newPath, oldPath));
                _onNavigateCallback?.Invoke(newPath);
            }
            UpdateCommandStates();
        }

        private void OnNavigateUp(NavigateUpMessage message)
        {
            var oldPath = _navigationService.CurrentPath;
            var parentPath = _navigationService.NavigateUp();

            if (!string.IsNullOrEmpty(parentPath))
            {
                Publish(new PathChangedMessage(parentPath, oldPath));
                _onNavigateCallback?.Invoke(parentPath);
            }
            UpdateCommandStates();
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 解析路径（处理归档文件重定向等）
        /// </summary>
        public string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // 识别虚拟路径
            bool isVirtualPath = ProtocolManager.IsVirtual(path);

            // [Archive Support] Check if path is an archive file
            if (!isVirtualPath && !System.IO.Directory.Exists(path) && System.IO.File.Exists(path))
            {
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".zip" || ext == ".7z" || ext == ".rar" || ext == ".tar" || ext == ".gz")
                {
                    // Redirect to archive schema
                    return $"zip://{path}|";
                }
            }

            return path;
        }

        #endregion

        #region 公开方法（供直接调用）

        /// <summary>
        /// 导航到指定路径
        /// </summary>
        public void NavigateTo(string path, bool addToHistory = true)
        {
            Publish(new NavigateToPathMessage(path, addToHistory));
        }

        /// <summary>
        /// 后退
        /// </summary>
        public void NavigateBack()
        {
            Publish(new NavigateBackMessage());
        }

        /// <summary>
        /// 前进
        /// </summary>
        public void NavigateForward()
        {
            Publish(new NavigateForwardMessage());
        }

        /// <summary>
        /// 向上导航
        /// </summary>
        public void NavigateUp()
        {
            Publish(new NavigateUpMessage());
        }

        #endregion
    }

    // Define simple refresh message if not exists, or just omit RefreshCommand implementation detail for now
    public class RefreshFileListMessage
    {
        public string Path { get; }
        public RefreshFileListMessage(string path = null) => Path = path;
    }
}
