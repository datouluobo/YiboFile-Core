using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controllers;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Controls
{
    /// <summary>
    /// NavigationRailControl 的交互逻辑
    /// 导航工具栏 - 混合架构实现（保持向后兼容）
    /// </summary>
    public partial class NavigationRailControl : UserControl
    {
        // 向后兼容：保留事件供外部使用
        public event EventHandler<string> NavigationModeChanged;
        public event EventHandler LayoutFocusRequested;
        public event EventHandler LayoutWorkRequested;
        public event EventHandler LayoutFullRequested;
        public event EventHandler DualListToggleRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler AboutRequested;

        private NavigationRailCoordinator _coordinator;

        /// <summary>
        /// 获取或设置 ViewModel
        /// </summary>
        public NavigationRailViewModel ViewModel
        {
            get => DataContext as NavigationRailViewModel;
            set => DataContext = value;
        }

        /// <summary>
        /// 获取或设置 Coordinator
        /// </summary>
        public NavigationRailCoordinator Coordinator
        {
            get => _coordinator;
            set => _coordinator = value;
        }

        public NavigationRailControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置消息总线以桥接 ViewModel 消息到事件（向后兼容）
        /// </summary>
        public void SetupMessageBridge(IMessageBus messageBus)
        {
            messageBus.Subscribe<NavigationModeChangedMessage>(msg =>
            {
                NavigationModeChanged?.Invoke(this, msg.Mode);
            });

            messageBus.Subscribe<LayoutModeChangedMessage>(msg =>
            {
                switch (msg.Mode)
                {
                    case "Focus":
                        LayoutFocusRequested?.Invoke(this, EventArgs.Empty);
                        break;
                    case "Work":
                        LayoutWorkRequested?.Invoke(this, EventArgs.Empty);
                        break;
                    case "Full":
                        LayoutFullRequested?.Invoke(this, EventArgs.Empty);
                        break;
                }
            });

            messageBus.Subscribe<DualListModeToggledMessage>(msg =>
            {
                DualListToggleRequested?.Invoke(this, EventArgs.Empty);
            });

            messageBus.Subscribe<ShowSettingsMessage>(msg =>
            {
                SettingsRequested?.Invoke(this, EventArgs.Empty);
            });

            messageBus.Subscribe<ShowAboutMessage>(msg =>
            {
                AboutRequested?.Invoke(this, EventArgs.Empty);
            });
        }

        #region 向后兼容：公开按钮引用供外部访问

        // 注意：这些是虚拟属性，返回 null。真正的状态管理在 ViewModel + Coordinator 中。
        // 如果 MainWindow 需要访问按钮状态，应改为订阅 ViewModel 属性变更。

        public Button PathButton => null;
        public Button LibraryButton => null;
        public Button TagButton => null;
        public Button FocusModeButton => null;
        public Button WorkModeButton => null;
        public Button FullModeButton => null;
        public Button DualListButton => null;
        public Button SettingsButton => null;
        public Button AboutButton => null;

        #endregion

        /// <summary>
        /// 外部设置导航模式（用于配置加载等场景）
        /// </summary>
        public void SetActiveMode(string mode)
        {
            _coordinator?.SetNavigationMode(mode);
        }

        /// <summary>
        /// 外部设置布局模式
        /// </summary>
        public void SetLayoutMode(string mode)
        {
            _coordinator?.SetLayoutMode(mode);
        }
    }
}
