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
        private bool _isSuppressingNavigation = false; // 仅保留用于 OnPathChanged 内部递归抑制

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

                System.Diagnostics.Debug.WriteLine($"[NAV-DEBUG] TabsModule ({pane}): Active tab changed to '{(tab.Title ?? "Untitled")}' with path '{(tab.Path ?? "null")}'. Suppressing={_isSuppressingNavigation}");

                // [SSOT 关键修正] 
                // 1. 如果我们正在执行 OnPathChanged（_isSuppressingNavigation == true），绝对不能反向发消息，否则会死循环。
                // 2. 如果不是处于同步中，则必须发布消息，哪怕是在程序启动时。
                if (!_isSuppressingNavigation && !string.IsNullOrEmpty(tab.Path))
                {
                    System.Diagnostics.Debug.WriteLine($"[NAV-DEBUG] TabsModule ({pane}): Publishing NavigateToPathMessage for '{tab.Path}'");
                    Publish(new NavigateToPathMessage(tab.Path, AddToHistory: false, Pane: pane));
                }


                // 对于标签，额外发送消息同步侧边栏选中状态
                else if (tab.Type == TabType.Tag && tab.Path?.StartsWith("tag://") == true)
                {
                    // 解析标签名称
                    var tagName = tab.Path.Substring(6);
                    // 查找对应的 TagViewModel 并通过消息发布 (如果需要同步侧边栏)
                    // 目前 PaneViewModel 会在解析 tag:// 时自动更新 CurrentTag
                }

                // 发布激活消息供 MainWindow 或其他组件（如搜索框）同步局部 UI 状态
                Publish(new TabActivatedMessage(tab.Path ?? "", tab.Path ?? "", tab.Type == TabType.Library)
                {
                    // MainWindow 的搜索框等组件会监听此消息同步模式
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
            // [SSOT 关键修正] 取消此处的强制抑制标志，改为由各事件处理器内部判断。
            // 之前的强制抑制导致了启动时（第一次同步）无法触发文件列表加载。

            // 根据消息中的 Pane 标识更新对应的 TabService
            var targetService = (message.Pane == PaneId.Second) ? _secondTabService : _tabService;
            if (targetService == null) return;

            var activeTab = targetService.ActiveTab;

            // 如果新路径与当前标签页类型兼容，则直接同步路径
            if (activeTab != null && IsTabCompatibleWithPath(activeTab.Type, message.NewPath))
            {
                targetService.UpdateActiveTabPath(message.NewPath);
                Publish(new TabPathUpdatedMessage(activeTab.Path ?? "", message.NewPath));
            }
            else
            {
                // [语义隔离] 类型不兼容或无当前页
                if (message.Pane == PaneId.Second)
                    CreateTab(message.NewPath, forceNewTab: false, activate: true, targetPane: PaneId.Second);
                else
                    CreateTab(message.NewPath, forceNewTab: false, activate: true, targetPane: PaneId.Main);
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
        public void CreateTab(string path = null, bool forceNewTab = false, bool activate = true, PaneId? targetPane = null)
        {
            // Use explicit pane if provided, otherwise fallback to focus-based detection
            bool useSecond = targetPane.HasValue
                ? targetPane.Value == PaneId.Second
                : (_isDualListMode() && _isSecondPaneFocused());

            var tabService = useSecond && _secondTabService != null ? _secondTabService : _tabService;

            if (tabService == null) return;

            if (string.IsNullOrEmpty(path))
            {
                tabService.CreateDuplicateTab();
            }
            else
            {
                tabService.CreatePathTab(path, forceNewTab, activate);
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
            // 规则1：同构智能复用
            // 只有当当前标签页类型与目标路径协议兼容时，才允许原地复用。
            // 例如：Path 标签页不能被 lib:// 导航直接复用（应由 Coordinator 决策是开新页还是找现有库页）
            if (activeTab != null && IsTabCompatibleWithPath(activeTab.Type, path))
            {
                // 更新路径和类型，确保标签页显示同步
                _tabService?.UpdateActiveTabPath(path);
                onReuseCurrent?.Invoke();
                return;
            }

            // 规则2：查找最近访问的相同Path标签页
            var recentTab = _tabService?.FindRecentTab(t => IsTabCompatibleWithPath(t.Type, path) && string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(10));

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

        private bool IsTabCompatibleWithPath(TabType type, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            if (path.StartsWith("lib://", StringComparison.OrdinalIgnoreCase)) return type == TabType.Library;
            if (path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase)) return type == TabType.Tag;
            if (path.StartsWith("search://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("content://", StringComparison.OrdinalIgnoreCase)) return type == TabType.Search;

            // 物理路径
            return type == TabType.Path;
        }

        #endregion
    }
}
