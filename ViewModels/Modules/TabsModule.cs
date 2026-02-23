using System;
using System.Linq;
using System.Windows.Input;
using YiboFile.Services.Core;
using YiboFile.Services.Tabs;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Models;
using YiboFile.Models.UI;
using YiboFile.Services.Navigation;
using YiboFile.Models.Navigation;

namespace YiboFile.ViewModels.Modules
{
    /// <summary>
    /// 标签页模块
    /// 处理标签页的创建、切换、关闭等功能
    /// </summary>
    public class TabsModule : ModuleBase
    {
        private readonly TabService _tabService;
        private readonly TabService _secondTabService;
        private readonly TabContentRegistry _registry;
        private readonly Func<bool> _isDualListMode;
        private readonly Func<bool> _isSecondPaneFocused;
        private bool _isSuppressingNavigation = false;

        public override string Name => "Tabs";

        public System.Collections.Generic.IEnumerable<PathTab> PrimaryTabs => _tabService?.Tabs;
        public System.Collections.Generic.IEnumerable<PathTab> SecondaryTabs => _secondTabService?.Tabs;

        public System.Windows.Input.ICommand PrimaryNewTabCommand => _tabService?.NewTabCommand;
        public System.Windows.Input.ICommand SecondaryNewTabCommand => _secondTabService?.NewTabCommand;

        /// <summary>
        /// 主栏当前活跃标签页（供 PaneContentHost 绑定）。
        /// </summary>
        public PathTab PrimaryActiveTab => _tabService?.ActiveTab;

        /// <summary>
        /// 副栏当前活跃标签页（供 PaneContentHost 绑定）。
        /// </summary>
        public PathTab SecondaryActiveTab => _secondTabService?.ActiveTab;

        public TabsModule(
            IMessageBus messageBus,
            TabService tabService,
            TabService secondTabService = null,
            TabContentRegistry registry = null,
            Func<bool> isDualListMode = null,
            Func<bool> isSecondPaneFocused = null)
            : base(messageBus)
        {
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _secondTabService = secondTabService;
            _registry = registry;
            _isDualListMode = isDualListMode ?? (() => false);
            _isSecondPaneFocused = isSecondPaneFocused ?? (() => false);
        }

        protected override void OnInitialize()
        {
            // 订阅标签页请求消息
            Subscribe<CreateTabMessage>(OnCreateTab);
            Subscribe<CloseTabMessage>(OnCloseTab);

            Subscribe<SwitchToTabMessage>(OnSwitchToTab);
            Subscribe<NavigationCompleteMessage>(OnNavigationComplete);

            // 订阅路径变更以更新当前标签页
            Subscribe<PathChangedMessage>(OnPathChanged);

            Subscribe<TabActiveChangedMessage>(m => OnActiveTabChanged(m.ActiveTab, m.Pane));
            Subscribe<TabPinStateChangedMessage>(m => OnTabPinStateChanged(m.Tab, m.Pane));
            Subscribe<TabTitleChangedMessage>(m => OnTabTitleChanged(m.Tab, m.Pane));

            // 订阅特殊标签页打开请求
            Subscribe<OpenContentTabMessage>(OnOpenContentTab);
        }

        private void OnActiveTabChanged(PathTab tab, PaneId pane)
        {
            if (tab == null || _isSwitchingTab) return;

            try
            {
                _isSwitchingTab = true;

                // 通知绑定系统更新 PrimaryActiveTab / SecondaryActiveTab
                if (pane == PaneId.Second)
                    OnPropertyChanged(nameof(SecondaryActiveTab));
                else
                    OnPropertyChanged(nameof(PrimaryActiveTab));

                // 特殊标签页（yibofile:// 协议）不需要触发文件导航
                // PaneContentHost 会通过 ActiveTab 绑定自动切换内容
                if (!string.IsNullOrEmpty(tab.ContentTypeId) && !TabContentTypes.IsFileBrowserType(tab.ContentTypeId))
                {
                    // 仅标记当前活跃但不发送导航消息
                    return;
                }

                if (!_isSuppressingNavigation && !string.IsNullOrEmpty(tab.Path))
                {

                    Publish(new RestoreNavigationStateMessage(
                        tab.Path,
                        tab.BackStack,
                        tab.ForwardStack,
                        pane));

                    if (tab.Type == TabType.Tag && tab.Path?.StartsWith("tag://") == true)
                    {
                        // 获取当前路径用于搜索上下文
                    }

                    Publish(new TabActivatedMessage(tab.Path ?? "", tab.Path ?? "", tab.Type == TabType.Library));
                }
            }
            finally
            {
                _isSwitchingTab = false;
            }
        }

        private void OnTabPinStateChanged(PathTab tab, PaneId pane)
        {
            // Publish(new TabPinStateChangedMessage(tab.Path, tab.IsPinned));
        }

        private void OnTabTitleChanged(PathTab tab, PaneId pane)
        {
            // Publish(new TabTitleChangedMessage(tab.Path, tab.Title));
        }

        protected override void OnShutdown()
        {
            base.OnShutdown();
        }

        private bool _isSwitchingTab;

        #region 消息处理

        private void OnCreateTab(CreateTabMessage message)
        {
            CreateTab(message.Path, forceNewTab: true, activate: message.Activate, targetPane: message.Pane);
        }

        private void OnCloseTab(CloseTabMessage message)
        {
        }

        public ICommand SwitchTabCommand { get; private set; }
        public ICommand OpenInNewTabCommand { get; private set; }

        private void InitializeCommands()
        {
            SwitchTabCommand = new RelayCommand<PathTab>(tab => SwitchToTab(tab));

            OpenInNewTabCommand = new RelayCommand<string>(path =>
            {
                if (!string.IsNullOrEmpty(path))
                {
                    CreateTab(path, forceNewTab: true, activate: true);
                }
            });
        }

        private void OnSwitchToTab(SwitchToTabMessage message)
        {
            // 查找并切换
            var tab = _tabService?.FindTabByPath(message.TabId);
            if (tab != null)
            {
                _tabService.SwitchToTab(tab);
                return;
            }

            if (_secondTabService != null)
            {
                var tab2 = _secondTabService.FindTabByPath(message.TabId);
                if (tab2 != null)
                {
                    _secondTabService.SwitchToTab(tab2);
                }
            }
        }

        private void OnPathChanged(PathChangedMessage message)
        {
            var targetService = (message.Pane == PaneId.Second) ? _secondTabService : _tabService;
            if (targetService == null) return;

            var activeTab = targetService.ActiveTab;

            if (activeTab != null && IsTabCompatibleWithPath(activeTab.Type, message.NewPath))
            {
                targetService.UpdateActiveTabPath(message.NewPath);
                Publish(new TabPathUpdatedMessage(activeTab.Path ?? "", message.NewPath));
            }
            else
            {
                if (message.Pane == PaneId.Second)
                    CreateTab(message.NewPath, forceNewTab: false, activate: true, targetPane: PaneId.Second);
                else
                    CreateTab(message.NewPath, forceNewTab: false, activate: true, targetPane: PaneId.Main);
            }
        }

        private void OnNavigationComplete(NavigationCompleteMessage msg)
        {
            var tabService = msg.Pane == PaneId.Second ? _secondTabService : _tabService;
            if (tabService?.ActiveTab != null)
            {
                if (msg.BackStack != null)
                    tabService.ActiveTab.BackStack = new System.Collections.Generic.Stack<string>(msg.BackStack.Reverse());
                else
                    tabService.ActiveTab.BackStack.Clear();

                if (msg.ForwardStack != null)
                    tabService.ActiveTab.ForwardStack = new System.Collections.Generic.Stack<string>(msg.ForwardStack.Reverse());
                else
                    tabService.ActiveTab.ForwardStack.Clear();
            }
        }

        #endregion

        #region 公开方法

        public void UpdateActiveTabPath(string path, PaneId pane = PaneId.Main)
        {
            var service = (pane == PaneId.Second) ? _secondTabService : _tabService;
            service?.UpdateActiveTabPath(path);
        }

        public void CreateTab(string path = null, bool forceNewTab = false, bool activate = true, PaneId? targetPane = null)
        {
            bool useSecond = targetPane.HasValue
                ? targetPane.Value == PaneId.Second
                : (_isDualListMode() && _isSecondPaneFocused());

            var tabService = useSecond && _secondTabService != null ? _secondTabService : _tabService;

            if (tabService == null) return;

            if (string.IsNullOrEmpty(path))
            {
                // CreateDuplicateTab handles null sourceTab by duplicating ActiveTab
                tabService.CreateDuplicateTab(null);
            }
            else
            {
                tabService.CreatePathTab(path, forceNewTab, activate);
            }
        }

        public void OpenLibraryInTab(Library library, bool forceNewTab = false, bool activate = true, PaneId? targetPane = null)
        {
            bool useSecond = targetPane.HasValue
                ? targetPane.Value == PaneId.Second
                : (_isDualListMode() && _isSecondPaneFocused());

            if (useSecond && _secondTabService != null)
            {
                _secondTabService.OpenLibraryTab(library, forceNewTab, activate);
            }
            else
            {
                _tabService?.OpenLibraryTab(library, forceNewTab, activate);
            }
        }

        public void CloseTab(string tabId)
        {
            Publish(new CloseTabMessage(tabId));
        }

        public void SwitchToTab(string tabId)
        {
            Publish(new SwitchToTabMessage(tabId));
        }

        public void SwitchToTab(PathTab tab)
        {
            if (_secondTabService != null && _secondTabService.Tabs.Contains(tab))
            {
                _secondTabService.SwitchToTab(tab);
            }
            else
            {
                _tabService?.SwitchToTab(tab);
            }
        }

        public void NavigateTo(string path, Action onReuseCurrent, Action onReuseSecond)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (_isDualListMode() && _isSecondPaneFocused() && _secondTabService != null)
            {
                var secondActiveTab = _secondTabService.ActiveTab;
                if (secondActiveTab != null && secondActiveTab.Type == TabType.Path)
                {
                    secondActiveTab.Path = path;
                    _secondTabService.UpdateTabTitle(secondActiveTab, path);
                    onReuseSecond?.Invoke();
                    return;
                }

                var secondRecentTab = _secondTabService.FindRecentTab(t => t.Type == TabType.Path && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10));
                if (secondRecentTab != null)
                {
                    _secondTabService.SwitchToTab(secondRecentTab);
                }
                else
                {
                    _secondTabService.CreatePathTab(path);
                }
                return;
            }

            var activeTab = _tabService?.ActiveTab;
            if (activeTab != null && IsTabCompatibleWithPath(activeTab.Type, path))
            {
                _tabService?.UpdateActiveTabPath(path);
                onReuseCurrent?.Invoke();
                return;
            }

            var recentTab = _tabService?.FindRecentTab(t => IsTabCompatibleWithPath(t.Type, path) && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10));

            if (recentTab != null)
            {
                _tabService?.SwitchToTab(recentTab);
            }
            else
            {
                CreateTab(path);
            }
        }

        private bool IsTabCompatibleWithPath(TabType type, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            if (path.StartsWith("lib://", StringComparison.OrdinalIgnoreCase)) return type == TabType.Library;
            if (path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase)) return type == TabType.Tag;
            if (path.StartsWith("search://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("content://", StringComparison.OrdinalIgnoreCase)) return type == TabType.Search;

            return type == TabType.Path;
        }

        #endregion

        #region 特殊标签页处理

        /// <summary>
        /// 处理特殊标签页打开请求。
        /// </summary>
        private void OnOpenContentTab(OpenContentTabMessage message)
        {
            if (string.IsNullOrEmpty(message.ContentTypeId) || _registry == null)
                return;

            // 确定目标 TabService
            bool useSecond = message.TargetPane.HasValue
                ? message.TargetPane.Value == PaneId.Second
                : (_isDualListMode() && _isSecondPaneFocused());

            var tabService = useSecond && _secondTabService != null ? _secondTabService : _tabService;

            tabService?.CreateSpecialTab(message.ContentTypeId, _registry);
        }

        #endregion
    }
}
