using System;
using System.Windows.Input;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 布局管理模块
    /// </summary>
    public class LayoutModule : ModuleBase
    {
        private string _currentLayoutMode = "Work"; // Default
        private bool _isDualListMode;
        private bool _isSecondPaneFocused;
        private bool _isLeftPanelCollapsed;
        private bool _isRightPanelCollapsed;
        private bool _isMainLayoutVisible = true;
        private string _activeSpecialPanel = "None"; // None, Tasks, Backup, Clipboard

        public override string Name => "LayoutModule";

        /// <summary>
        /// 当前激活的特殊面板
        /// </summary>
        public string ActiveSpecialPanel
        {
            get => _activeSpecialPanel;
            set => SetProperty(ref _activeSpecialPanel, value);
        }

        /// <summary>
        /// 当前布局模式 (Focus, Work, Full)
        /// </summary>
        public string CurrentLayoutMode
        {
            get => _currentLayoutMode;
            private set
            {
                if (_currentLayoutMode != value)
                {
                    _currentLayoutMode = value;
                    Publish(new LayoutModeChangedMessage(_currentLayoutMode));

                    // 持久化状态
                    YiboFile.Services.Config.ConfigurationService.Instance.Set(cfg => cfg.LayoutMode, value);
                    YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
                }
            }
        }

        /// <summary>
        /// 是否为双列表模式
        /// </summary>
        public bool IsDualListMode
        {
            get => _isDualListMode;
            internal set
            {
                if (_isDualListMode != value)
                {
                    _isDualListMode = value;
                    Publish(new DualListModeChangedMessage(_isDualListMode));
                    OnPropertyChanged(nameof(IsDualListEffectivelyVisible));

                    // 如果开启双列表，确保右侧面板是不折叠的（让出空间给副列表）
                    if (_isDualListMode)
                    {
                        IsRightPanelCollapsed = false;
                    }

                    // 持久化状态
                    YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.IsDualListMode, value);
                    YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
                }
            }
        }

        /// <summary>
        /// 左面板是否已折叠
        /// </summary>
        public bool IsLeftPanelCollapsed
        {
            get => _isLeftPanelCollapsed;
            set => SetProperty(ref _isLeftPanelCollapsed, value);
        }

        /// <summary>
        /// 右面板是否已折叠
        /// </summary>
        public bool IsRightPanelCollapsed
        {
            get => _isRightPanelCollapsed;
            set
            {
                if (SetProperty(ref _isRightPanelCollapsed, value))
                {
                    // 当右侧面板折叠状态改变时，可能需要通知其他组件
                }
            }
        }

        /// <summary>
        /// 主布局是否可见（当显示特殊面板如备份、任务队列时为 false）
        /// </summary>
        public bool IsMainLayoutVisible
        {
            get => _isMainLayoutVisible;
            set
            {
                if (SetProperty(ref _isMainLayoutVisible, value))
                {
                    // 发布消息通知相关组件（如 RightPanel）同步隐藏
                    Publish(new MainLayoutVisibilityChangedMessage(value));
                    OnPropertyChanged(nameof(IsDualListEffectivelyVisible));
                }
            }
        }

        /// <summary>
        /// 副列表实际可见性（考虑双列表开关和全局布局状态）
        /// </summary>
        public bool IsDualListEffectivelyVisible => IsDualListMode && IsMainLayoutVisible;

        /// <summary>
        /// 是否为副面板获得焦点 (双列表模式)
        /// </summary>
        public bool IsSecondPaneFocused
        {
            get => _isSecondPaneFocused;
            internal set
            {
                if (_isSecondPaneFocused != value)
                {
                    _isSecondPaneFocused = value;
                    // 发布状态变更通知（不是请求）
                    Publish(new FocusedPaneChangedMessage(_isSecondPaneFocused));
                }
            }
        }

        public ICommand SwitchLayoutModeCommand { get; private set; }
        public ICommand ToggleDualListModeCommand { get; private set; }
        public ICommand SwitchFocusedPaneCommand { get; private set; }

        public LayoutModule(IMessageBus messageBus) : base(messageBus)
        {
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            SwitchLayoutModeCommand = new RelayCommand<string>(mode => SwitchLayoutMode(mode));
            ToggleDualListModeCommand = new RelayCommand(() => ToggleDualListMode());
            SwitchFocusedPaneCommand = new RelayCommand(() => SwitchFocusedPane());
        }

        /// <summary>
        /// 初始化状态（不发布消息）
        /// </summary>
        public void InitializeState(string layoutMode, bool isDualListMode, bool isSecondPaneFocused, bool isLeftCollapsed, bool isRightCollapsed)
        {
            CurrentLayoutMode = layoutMode;
            IsDualListMode = isDualListMode;
            IsSecondPaneFocused = isSecondPaneFocused;
            IsLeftPanelCollapsed = isLeftCollapsed;
            IsRightPanelCollapsed = isRightCollapsed;
        }

        protected override void OnInitialize()
        {
            // 订阅焦点切换请求（外部请求切换焦点时触发）
            Subscribe<SwitchFocusedPaneMessage>(m =>
            {
                if (IsDualListMode)
                {
                    // 直接修改内部字段并发布通知，避免递归
                    _isSecondPaneFocused = !_isSecondPaneFocused;
                    Publish(new FocusedPaneChangedMessage(_isSecondPaneFocused));
                }
            });

            Subscribe<SetFocusedPaneMessage>(m =>
            {
                // Only allow setting focus if dual list mode is active, OR if we want to allow setting primary (0) always?
                // Actually even in Single mode, Primary is focused.
                // If Single mode and request Secondary, ignore.
                if (!IsDualListMode && m.IsSecondPane) return;

                if (_isSecondPaneFocused != m.IsSecondPane)
                {
                    _isSecondPaneFocused = m.IsSecondPane;
                    Publish(new FocusedPaneChangedMessage(_isSecondPaneFocused));
                }
            });

            // 订阅布局变更请求
            Subscribe<RequestLayoutModeMessage>(m => SwitchLayoutMode(m.Mode));
            Subscribe<RequestDualListToggleMessage>(m => ToggleDualListMode());

            // 订阅导航模式变更，用于自动显示/隐藏特殊面板
            Subscribe<NavigationModeChangedMessage>(m =>
            {
                // 只有这三个模式需要显示特殊覆盖面板并隐藏主布局
                if (m.Mode == "Tasks" || m.Mode == "Backup" || m.Mode == "Clipboard")
                {
                    ActiveSpecialPanel = m.Mode;
                    IsMainLayoutVisible = false;
                }
                else
                {
                    // Path, Library, Tag, Search 等都使用主 FileBrowser，不需要特殊面板
                    ActiveSpecialPanel = "None";
                    IsMainLayoutVisible = true;
                }
            });

            // 订阅高度和视图模式变更，用于持久化
            Subscribe<ViewModeChangedMessage>(m =>
            {
                // 目前配置是全局的，所以不区分 pane
                YiboFile.Services.Config.ConfigurationService.Instance.Set(cfg => cfg.FileViewMode, m.Mode);
                YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
            });

            Subscribe<NotesHeightChangedMessage>(m =>
            {
                YiboFile.Services.Config.ConfigurationService.Instance.Set(cfg => cfg.RightPanelNotesHeight, m.NewHeight);
                YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
            });


            Subscribe<SplitterDragCompletedMessage>(OnSplitterDragCompleted);
        }

        private void OnSplitterDragCompleted(SplitterDragCompletedMessage message)
        {
            if (message.SplitterName == "Left")
            {
                YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.LeftPanelWidth, message.NewValue);
            }
            else if (message.SplitterName == "Right")
            {
                YiboFile.Services.Config.ConfigurationService.Instance.Set(c => c.RightPanelWidth, message.NewValue);
            }
            YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();
        }

        /// <summary>
        /// 切换布局模式
        /// </summary>
        public void SwitchLayoutMode(string mode)
        {
            CurrentLayoutMode = mode;

            switch (mode)
            {
                case "Focus":
                    IsLeftPanelCollapsed = true;
                    IsRightPanelCollapsed = true;
                    break;
                case "Work":
                    IsLeftPanelCollapsed = false;
                    IsRightPanelCollapsed = true;
                    break;
                case "Full":
                    IsLeftPanelCollapsed = false;
                    IsRightPanelCollapsed = false;
                    break;
            }
        }

        /// <summary>
        /// 切换双列表模式
        /// </summary>
        public void ToggleDualListMode(bool? forcedValue = null)
        {
            IsDualListMode = forcedValue ?? !IsDualListMode;
        }

        /// <summary>
        /// 切换焦点面板 (从主列表到副列表，反之亦然)
        /// </summary>
        public void SwitchFocusedPane()
        {
            IsSecondPaneFocused = !IsSecondPaneFocused;
        }

        /// <summary>
        /// 设置焦点面板
        /// </summary>
        public void SetFocusedPane(bool isSecondPane)
        {
            IsSecondPaneFocused = isSecondPane;
        }
    }
}
