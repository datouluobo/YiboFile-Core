using System;
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

        public override string Name => "LayoutModule";

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

        public LayoutModule(IMessageBus messageBus) : base(messageBus)
        {
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
