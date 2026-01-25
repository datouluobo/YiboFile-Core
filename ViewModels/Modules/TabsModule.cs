using System;
using YiboFile.Services.Tabs;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 标签页模块
    /// 处理标签页的创建、切换、关闭等功能
    /// </summary>
    public class TabsModule : ModuleBase
    {
        private readonly TabService _tabService;
        private readonly Action<string, bool> _onCreateTabCallback;
        private readonly Action<string> _onSwitchTabCallback;

        public override string Name => "Tabs";

        public TabsModule(
            IMessageBus messageBus,
            TabService tabService,
            Action<string, bool> onCreateTabCallback = null,
            Action<string> onSwitchTabCallback = null)
            : base(messageBus)
        {
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _onCreateTabCallback = onCreateTabCallback;
            _onSwitchTabCallback = onSwitchTabCallback;
        }

        protected override void OnInitialize()
        {
            // 订阅标签页请求消息
            Subscribe<CreateTabMessage>(OnCreateTab);
            Subscribe<CloseTabMessage>(OnCloseTab);
            Subscribe<SwitchToTabMessage>(OnSwitchToTab);

            // 订阅路径变更以更新当前标签页
            Subscribe<PathChangedMessage>(OnPathChanged);
        }

        #region 消息处理

        private void OnCreateTab(CreateTabMessage message)
        {
            // 过渡期：调用回调让 MainWindow 处理
            _onCreateTabCallback?.Invoke(message.Path, message.Activate);
        }

        private void OnCloseTab(CloseTabMessage message)
        {
            // 通过 TabService 关闭标签页
            // 将在后续完全迁移
        }

        private void OnSwitchToTab(SwitchToTabMessage message)
        {
            _onSwitchTabCallback?.Invoke(message.TabId);
        }

        private void OnPathChanged(PathChangedMessage message)
        {
            // 更新当前标签页的路径
            _tabService?.UpdateActiveTabPath(message.NewPath);

            // 发布标签页路径更新通知
            var activeTab = _tabService?.ActiveTab;
            if (activeTab != null)
            {
                // 使用 Path 作为标签页的唯一标识
                Publish(new TabPathUpdatedMessage(activeTab.Path ?? "", message.NewPath));
            }
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 创建新标签页
        /// </summary>
        public void CreateTab(string path = null, bool activate = true)
        {
            Publish(new CreateTabMessage(path, activate));
        }

        /// <summary>
        /// 关闭标签页
        /// </summary>
        public void CloseTab(string tabId)
        {
            Publish(new CloseTabMessage(tabId));
        }

        /// <summary>
        /// 切换到标签页
        /// </summary>
        public void SwitchToTab(string tabId)
        {
            Publish(new SwitchToTabMessage(tabId));
        }

        #endregion
    }
}
