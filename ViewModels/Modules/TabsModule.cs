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
        private TabService _tabService;
        private TabService _secondTabService;
        private readonly Services.Navigation.NavigationService _navigationService;
        private readonly TabContentRegistry _registry;
        private readonly Func<bool> _isDualPaneMode;
        private readonly Func<bool> _isSecondPaneFocused;
        private readonly Func<PaneMode> _currentPaneMode;
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
            Services.Navigation.NavigationService navigationService = null,
            TabContentRegistry registry = null,
            Func<bool> isDualPaneMode = null,
            Func<bool> isSecondPaneFocused = null,
            Func<PaneMode> currentPaneMode = null)
            : base(messageBus)
        {
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _secondTabService = secondTabService;
            _navigationService = navigationService;
            _registry = registry;
            _isDualPaneMode = isDualPaneMode ?? (() => false);
            _isSecondPaneFocused = isSecondPaneFocused ?? (() => false);
            _currentPaneMode = currentPaneMode ?? (() => PaneMode.Single);

            InitializeCommands();
        }

        public void SwapTabs()
        {
            if (_secondTabService != null)
            {
                // 1. Swap tabs
                _tabService.SwapStateWith(_secondTabService);

                // 2. Swap navigation history/states
                _navigationService?.SwapStates(PaneId.Main, PaneId.Second);

                OnPropertyChanged(nameof(PrimaryTabs));
                OnPropertyChanged(nameof(SecondaryTabs));
                OnPropertyChanged(nameof(PrimaryActiveTab));
                OnPropertyChanged(nameof(SecondaryActiveTab));
                
                // 通知订阅方标签发生了大量重组
                Publish(new TabActiveChangedMessage(_tabService.ActiveTab, _tabService.Pane));
                Publish(new TabActiveChangedMessage(_secondTabService.ActiveTab, _secondTabService.Pane));
            }
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

            // 订阅交换面板请求
            Subscribe<RequestSwapPanesMessage>(m => SwapTabs());
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

                    if (tab.ContentTypeId == TabContentTypes.Tag && tab.Path?.StartsWith("tag://") == true)
                    {
                        // 获取当前路径用于搜索上下文
                    }

                    Publish(new TabActivatedMessage(tab.Path ?? "", tab.Path ?? "", tab.ContentTypeId == TabContentTypes.Library));
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
                    CreateTab(path, forceNewTab: false, activate: true);
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

            if (activeTab != null && IsTabCompatibleWithPath(activeTab.ContentTypeId, message.NewPath))
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
                : (_isDualPaneMode() && _isSecondPaneFocused());

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
                : (_isDualPaneMode() && _isSecondPaneFocused());

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

            if (_isDualPaneMode() && _isSecondPaneFocused() && _secondTabService != null)
            {
                var secondActiveTab = _secondTabService.ActiveTab;
                if (secondActiveTab != null && secondActiveTab.ContentTypeId == TabContentTypes.Path)
                {
                    secondActiveTab.Path = path;
                    _secondTabService.UpdateTabTitle(secondActiveTab, path);
                    onReuseSecond?.Invoke();
                    return;
                }

                var secondRecentTab = _secondTabService.FindRecentTab(t => t.ContentTypeId == TabContentTypes.Path && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10));
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
            if (activeTab != null && IsTabCompatibleWithPath(activeTab.ContentTypeId, path))
            {
                _tabService?.UpdateActiveTabPath(path);
                onReuseCurrent?.Invoke();
                return;
            }

            var recentTab = _tabService?.FindRecentTab(t => IsTabCompatibleWithPath(t.ContentTypeId, path) && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10));

            if (recentTab != null)
            {
                _tabService?.SwitchToTab(recentTab);
            }
            else
            {
                CreateTab(path);
            }
        }

        private bool IsTabCompatibleWithPath(string contentTypeId, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            if (path.StartsWith("lib://", StringComparison.OrdinalIgnoreCase)) return contentTypeId == TabContentTypes.Library;
            if (path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase)) return contentTypeId == TabContentTypes.Tag;
            if (path.StartsWith("search://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("content://", StringComparison.OrdinalIgnoreCase)) return contentTypeId == TabContentTypes.Search;

            return contentTypeId == TabContentTypes.Path;
        }

        #endregion

        #region 特殊标签页处理

        /// <summary>
        /// 处理特殊标签页打开请求。
        /// </summary>
        private void OnOpenContentTab(OpenContentTabMessage message)
        {
            if (string.IsNullOrEmpty(message.ContentTypeId)) return;

            var content = _registry.Resolve(message.ContentTypeId);
            if (content == null)
            {
                FileLogger.Log($"TabsModule.OnOpenContentTab: Failed to resolve '{message.ContentTypeId}'");
                return;
            }

            // 1. 确定本应开启的目标面板 (优先跟随焦点侧)
            bool useSecond;
            PaneMode currentMode = _currentPaneMode();
            if (currentMode == PaneMode.Preview)
            {
                useSecond = _isSecondPaneFocused();
            }
            else
            {
                useSecond = message.TargetPane.HasValue
                    ? message.TargetPane.Value == PaneId.Second
                    : (_isDualPaneMode() && _isSecondPaneFocused());
            }

            // 2. 检查 SupportsSecondaryPane 回退逻辑
            // 注意：在预览模式下不再强制回退，因为此时该栏（即便 ID 是 Second）实际上是宽大的主显示区
            if (useSecond && !content.SupportsSecondaryPane && currentMode != PaneMode.Preview)
            {
                useSecond = false;
            }

            var finalTabService = useSecond && _secondTabService != null ? _secondTabService : _tabService;
            var otherService = finalTabService == _tabService ? _secondTabService : _tabService;

            // 3. AllowMultiple=false 时的冲突解决：强制迁移
            if (!content.AllowMultiple)
            {
                // 如果在对侧已开启，先移除，从而在本侧重新开启
                var existingInOther = otherService?.FindTabByContentTypeId(message.ContentTypeId);
                if (existingInOther != null)
                {
                    otherService.RemoveTab(existingInOther);
                }
            }

            // 4. 执行创建/激活
            finalTabService?.CreateSpecialTab(message.ContentTypeId);
        }

        #endregion
    }
}
