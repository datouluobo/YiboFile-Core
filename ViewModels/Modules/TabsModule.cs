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
        private readonly Func<bool> _isDualListMode;
        private readonly Func<bool> _isSecondPaneFocused;
        private readonly Action<string, bool> _onCreateTabCallback;
        private readonly Action<string> _onSwitchTabCallback;

        public override string Name => "Tabs";

        public System.Collections.Generic.IEnumerable<PathTab> PrimaryTabs => _tabService?.Tabs;
        public System.Collections.Generic.IEnumerable<PathTab> SecondaryTabs => _secondTabService?.Tabs;

        public System.Windows.Input.ICommand PrimaryNewTabCommand => _tabService?.NewTabCommand;
        public System.Windows.Input.ICommand SecondaryNewTabCommand => _secondTabService?.NewTabCommand;

        public TabsModule(
            IMessageBus messageBus,
            TabService tabService,
            TabService secondTabService = null,
            Func<bool> isDualListMode = null,
            Func<bool> isSecondPaneFocused = null,
            Action<string, bool> onCreateTabCallback = null,
            Action<string> onSwitchTabCallback = null)
            : base(messageBus)
        {
            _tabService = tabService ?? throw new ArgumentNullException(nameof(tabService));
            _secondTabService = secondTabService;
            _isDualListMode = isDualListMode ?? (() => false);
            _isSecondPaneFocused = isSecondPaneFocused ?? (() => false);
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

            if (_tabService != null)
            {
                _tabService.ActiveTabChanged += OnActiveTabChanged;
                _tabService.TabPinStateChanged += OnTabPinStateChanged;
                _tabService.TabTitleChanged += OnTabTitleChanged;
            }

            if (_secondTabService != null)
            {
                _secondTabService.ActiveTabChanged += OnActiveTabChanged;
                _secondTabService.TabPinStateChanged += OnTabPinStateChanged;
                _secondTabService.TabTitleChanged += OnTabTitleChanged;
            }
        }

        private void OnActiveTabChanged(object sender, PathTab tab)
        {
            if (tab == null || _isSwitchingTab) return;

            try
            {
                _isSwitchingTab = true;

                // 判断归属 Pane
                var pane = (sender == _secondTabService) ? PaneId.Second : PaneId.Main;

                if (tab.Type == TabType.Library && tab.Library != null)
                {
                    Publish(new LibrarySelectedMessage(tab.Library));
                }
                else if (tab.Type == TabType.Path && !string.IsNullOrEmpty(tab.Path))
                {
                    // 发布导航消息以更新文件列表
                    Publish(new NavigateToPathMessage(tab.Path, AddToHistory: false));
                }

                // 发布激活消息供 MainWindow 同步 UI 状态
                Publish(new TabActivatedMessage(tab.Path ?? "", tab.Path ?? "", tab.Type == TabType.Library)
                {
                    // 这里可以扩展消息携带 Pane 信息，但 TabActivatedMessage 当前没带 Pane
                    // 暂时保持现状，MainWindow 会根据 CurrentTab 自动判断
                });
            }
            finally
            {
                _isSwitchingTab = false;
            }
        }

        private void OnTabPinStateChanged(object sender, PathTab tab)
        {
            // 发布钉住状态变更消息
            // Publish(new TabPinStateChangedMessage(tab.Path, tab.IsPinned));
        }

        private void OnTabTitleChanged(object sender, PathTab tab)
        {
            // 发布标题变更消息
            // Publish(new TabTitleChangedMessage(tab.Path, tab.Title));
        }

        protected override void OnShutdown()
        {
            if (_tabService != null)
            {
                _tabService.ActiveTabChanged -= OnActiveTabChanged;
                _tabService.TabPinStateChanged -= OnTabPinStateChanged;
                _tabService.TabTitleChanged -= OnTabTitleChanged;
            }
            if (_secondTabService != null)
            {
                _secondTabService.ActiveTabChanged -= OnActiveTabChanged;
                _secondTabService.TabPinStateChanged -= OnTabPinStateChanged;
                _secondTabService.TabTitleChanged -= OnTabTitleChanged;
            }
            base.OnShutdown();
        }

        private bool _isSwitchingTab;

        #region 消息处理

        private void OnCreateTab(CreateTabMessage message)
        {
            // 使用模块内部逻辑创建标签页
            CreateTab(message.Path, forceNewTab: true, activate: message.Activate, targetPane: message.Pane);
        }

        private void OnCloseTab(CloseTabMessage message)
        {
            // 通过 TabService 关闭标签页
            // 将在后续完全迁移
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
            _onSwitchTabCallback?.Invoke(message.TabId);
        }

        private void OnPathChanged(PathChangedMessage message)
        {
            // [SSOT 关键修复] 根据消息中的 Pane 标识更新对应的 TabService
            // 解决双面板模式下，点击左侧导航导致两侧面板同时发生路径变更的同步 Bug
            var targetService = (message.Pane == PaneId.Second) ? _secondTabService : _tabService;
            if (targetService == null) return;

            // 更新对应业务服务的当前标签页路径
            targetService.UpdateActiveTabPath(message.NewPath);

            // 发布标签页路径更新通知 (用于同步地址栏等 UI 组件)
            var activeTab = targetService.ActiveTab;
            if (activeTab != null)
            {
                // 使用 Path 作为标签页的唯一标识
                Publish(new TabPathUpdatedMessage(activeTab.Path ?? "", message.NewPath));
            }
        }

        #endregion

        #region 公开方法

        /// <summary>
        /// 更新当前激活标签页的路径
        /// </summary>
        public void UpdateActiveTabPath(string path, PaneId pane = PaneId.Main)
        {
            var service = (pane == PaneId.Second) ? _secondTabService : _tabService;
            service?.UpdateActiveTabPath(path);
        }

        /// <summary>
        /// 创建新标签页
        /// </summary>
        public void CreateTab(string path, bool forceNewTab = false, bool activate = true, PaneId? targetPane = null)
        {
            // Use explicit pane if provided, otherwise fallback to focus-based detection
            bool useSecond = targetPane.HasValue
                ? targetPane.Value == PaneId.Second
                : (_isDualListMode() && _isSecondPaneFocused());

            if (useSecond && _secondTabService != null)
            {
                _secondTabService.CreatePathTab(path, forceNewTab, activate);
            }
            else
            {
                _tabService?.CreatePathTab(path, forceNewTab, activate);
            }
        }

        /// <summary>
        /// 在标签页中打开库
        /// </summary>
        public void OpenLibraryInTab(Library library, bool forceNewTab = false, bool activate = true, PaneId? targetPane = null)
        {
            // Use explicit pane if provided, otherwise fallback to focus-based detection
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

        /// <summary>
        /// 关闭标签页
        /// </summary>
        public void CloseTab(string tabId)
        {
            // TODO: 需要一种方式识别 tabId 属于哪个 Service，或者尝试两者
            // 目前 CreateTabMessage 没有 tabId，只有 CreatePathTab 会返回
            // 这里暂且保留 message 发布，或者直接调用
            Publish(new CloseTabMessage(tabId));
        }

        /// <summary>
        /// 切换到标签页
        /// </summary>
        public void SwitchToTab(string tabId)
        {
            Publish(new SwitchToTabMessage(tabId));
        }

        /// <summary>
        /// 切换到指定标签页对象
        /// </summary>
        public void SwitchToTab(PathTab tab)
        {
            // 尝试在两个服务中查找并切换
            if (_secondTabService != null && _secondTabService.Tabs.Contains(tab))
            {
                _secondTabService.SwitchToTab(tab);
            }
            else
            {
                _tabService?.SwitchToTab(tab);
            }
        }


        /// <summary>
        /// 智能导航到路径（处理标签页复用、切换或创建）
        /// </summary>
        /// <param name="path">目标路径</param>
        /// <param name="onReuseCurrent">当复用主列表当前标签页时的回调</param>
        /// <param name="onReuseSecond">当复用副列表当前标签页时的回调</param>
        public void NavigateTo(string path, Action onReuseCurrent, Action onReuseSecond)
        {
            if (string.IsNullOrEmpty(path)) return;

            // 双列表模式：如果焦点在副列表，则在副列表导航
            if (_isDualListMode() && _isSecondPaneFocused() && _secondTabService != null)
            {
                var secondActiveTab = _secondTabService.ActiveTab;
                // 规则1：同类型标签页直接更新
                if (secondActiveTab != null && secondActiveTab.Type == TabType.Path)
                {
                    secondActiveTab.Path = path;
                    _secondTabService.UpdateTabTitle(secondActiveTab, path);
                    onReuseSecond?.Invoke();
                    return;
                }

                // 规则2：查找最近访问的相同Path标签页
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
            // 规则1：同类型标签页直接更新
            if (activeTab != null && activeTab.Type == TabType.Path)
            {
                // 先更新标题，确保标签页显示同步
                _tabService?.UpdateActiveTabPath(path);
                activeTab.Path = path;
                onReuseCurrent?.Invoke();
                return;
            }

            // 规则2：查找最近访问的相同Path标签页（使用配置时间窗口）
            var recentTab = _tabService?.FindRecentTab(t => t.Type == TabType.Path && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10));

            if (recentTab != null)
            {
                // 找到了最近访问的标签页，切换到它
                _tabService?.SwitchToTab(recentTab);
            }
            else
            {
                // 没有找到或不够新鲜，创建新标签页
                CreateTab(path);
            }
        }

        #endregion
    }
}
