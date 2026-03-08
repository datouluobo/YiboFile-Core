using System;
using YiboFile.ViewModels;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Controllers
{
    /// <summary>
    /// 导航工具栏协调器（混合架构 - Controller）
    /// 职责：处理导航和布局模式切换的业务逻辑，驱动 NavigationRailViewModel
    /// </summary>
    public class NavigationRailCoordinator : IDisposable
    {
        private readonly IMessageBus _messageBus;
        private readonly NavigationRailViewModel _viewModel;

        public NavigationRailCoordinator(
            IMessageBus messageBus,
            NavigationRailViewModel viewModel)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            // 订阅请求消息
            _messageBus.Subscribe<RequestNavigationModeMessage>(OnNavigationModeRequested);
            _messageBus.Subscribe<RequestLayoutModeMessage>(OnLayoutModeRequested);
            _messageBus.Subscribe<RequestDualPaneToggleMessage>(OnDualPaneToggleRequested);

            // 监听来自 LayoutModule 的状态变更通知以同步 VM
            _messageBus.Subscribe<LayoutModeChangedMessage>(m => _viewModel.ActiveLayoutMode = m.Mode);
            _messageBus.Subscribe<DualPaneModeChangedMessage>(m => _viewModel.IsDualPaneMode = m.IsEnabled);
            _messageBus.Subscribe<PaneModeChangedMessage>(m => _viewModel.CurrentPaneMode = m.Mode);
            _messageBus.Subscribe<SidebarVisibilityChangedMessage>(m => _viewModel.IsLeftPanelCollapsed = m.IsHidden);
        }

        /// <summary>
        /// 处理导航模式切换请求
        /// </summary>
        private void OnNavigationModeRequested(RequestNavigationModeMessage message)
        {
            if (string.IsNullOrEmpty(message.Mode))
                return;

            // 特殊处理：原 NavigationMode 迁移到标签页
            if (message.Mode == "Tasks" || message.Mode == "Backup" || message.Mode == "Clipboard")
            {
                string tabType = message.Mode.ToLowerInvariant();
                _messageBus.Publish(new OpenContentTabMessage(tabType));
                return; // 不改变左侧导航栏的活动状态
            }

            // 业务逻辑：验证模式是否有效
            if (!IsValidNavigationMode(message.Mode))
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationRailCoordinator] Invalid navigation mode: {message.Mode}");
                return;
            }

            // 特殊处理：Tag 模式需要检查功能是否可用
            if (message.Mode == "Tag" && !App.IsTagTrainAvailable)
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationRailCoordinator] Tag feature is not available");
                return;
            }

            // ✅ Controller 更新 ViewModel 状态
            _viewModel.ActiveNavigationMode = message.Mode;

            // ✅ 发布状态变更通知（供其他组件订阅）
            _messageBus.Publish(new NavigationModeChangedMessage(message.Mode));

            System.Diagnostics.Debug.WriteLine($"[NavigationRailCoordinator] Navigation mode changed to: {message.Mode}");
        }

        /// <summary>
        /// 处理布局模式切换请求
        /// </summary>
        private void OnLayoutModeRequested(RequestLayoutModeMessage message)
        {
            if (string.IsNullOrEmpty(message.Mode))
                return;

            // 业务逻辑：验证模式是否有效
            if (!IsValidLayoutMode(message.Mode))
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationRailCoordinator] Invalid layout mode: {message.Mode}");
                return;
            }

            // ✅ Controller 更新 ViewModel 状态
            _viewModel.ActiveLayoutMode = message.Mode;

            // ❌ 不再发布 LayoutModeChangedMessage，因为 LayoutModule 已经处理并发布了
            // _messageBus.Publish(new LayoutModeChangedMessage(message.Mode));

            System.Diagnostics.Debug.WriteLine($"[NavigationRailCoordinator] Layout mode updated in VM: {message.Mode}");
        }

        /// <summary>
        /// 处理双列表模式切换请求
        /// </summary>
        private void OnDualPaneToggleRequested(RequestDualPaneToggleMessage message)
        {
            // 业务逻辑：切换状态
            bool newState = !_viewModel.IsDualPaneMode;

            // ✅ Controller 更新 ViewModel 状态
            _viewModel.IsDualPaneMode = newState;

            // ❌ 不再发布 DualPaneModeToggledMessage，因为 LayoutModule 已经处理并发布了
            // _messageBus.Publish(new DualPaneModeToggledMessage(newState));

            System.Diagnostics.Debug.WriteLine($"[NavigationRailCoordinator] Dual list mode toggled in VM: {newState}");
        }

        #region 业务规则验证

        private bool IsValidNavigationMode(string mode)
        {
            return mode switch
            {
                "Path" => true,
                "Library" => true,
                "Tag" => true,
                _ => false
            };
        }

        private bool IsValidLayoutMode(string mode)
        {
            return mode switch
            {
                "Focus" => true,
                "Work" => true,
                "Full" => true,
                _ => false
            };
        }

        #endregion

        #region 公共方法（供外部调用）

        /// <summary>
        /// 外部设置导航模式（例如从配置加载）
        /// </summary>
        public void SetNavigationMode(string mode)
        {
            if (IsValidNavigationMode(mode))
            {
                _viewModel.ActiveNavigationMode = mode;
                _messageBus.Publish(new NavigationModeChangedMessage(mode));
            }
        }

        /// <summary>
        /// 外部设置布局模式
        /// </summary>
        public void SetLayoutMode(string mode)
        {
            if (IsValidLayoutMode(mode))
            {
                _viewModel.ActiveLayoutMode = mode;
                _messageBus.Publish(new LayoutModeChangedMessage(mode));
            }
        }

        #endregion

        public void Dispose()
        {
            // MessageBus 会自动处理取消订阅，但可以在这里显式清理
        }
    }
}
