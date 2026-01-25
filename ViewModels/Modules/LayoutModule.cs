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
            private set
            {
                if (_isDualListMode != value)
                {
                    _isDualListMode = value;
                    Publish(new DualListModeChangedMessage(_isDualListMode));
                }
            }
        }

        /// <summary>
        /// 是否为副面板获得焦点 (双列表模式)
        /// </summary>
        public bool IsSecondPaneFocused
        {
            get => _isSecondPaneFocused;
            private set
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
        public void InitializeState(string layoutMode, bool isDualListMode, bool isSecondPaneFocused)
        {
            _currentLayoutMode = layoutMode;
            _isDualListMode = isDualListMode;
            _isSecondPaneFocused = isSecondPaneFocused;
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
        }

        /// <summary>
        /// 切换布局模式
        /// </summary>
        public void SwitchLayoutMode(string mode)
        {
            CurrentLayoutMode = mode;
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
