using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YiboFile.Models;
using YiboFile.Models.Navigation;
using YiboFile.Services.Tabs;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Services.Navigation
{
    /// <summary>
    /// 统一导航协调器
    /// 负责处理所有导航模式的链接打开行为，确保行为一致性
    /// </summary>
    public class NavigationCoordinator : INavigationCoordinator
    {
        private TabService _mainTabService;
        private TabService _secondTabService;
        private NavigationService _navigationService;
        private LibraryService _libraryService;
        private readonly ViewModels.Messaging.IMessageBus _messageBus;

        // Pane-specific navigation ViewModel access
        private Func<PaneId, ViewModels.PaneViewModel> _paneViewModelResolver;

        // Per-pane navigation sequence counter — ensures stale async completions
        // (from fire-and-forget NavigateAsync) don't override newer navigations.
        // Incremented on every HandlePathNavigation call.
        private readonly ConcurrentDictionary<PaneId, long> _navigationSeq = new();

        public NavigationCoordinator(ViewModels.Messaging.IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }



        /// <summary>
        /// 初始化协调器
        /// </summary>
        public void Initialize(
            TabService mainTab,
            TabService secondTab,
            NavigationService navService,
            LibraryService libService,
            Func<PaneId, ViewModels.PaneViewModel> paneViewModelResolver)
        {
            _mainTabService = mainTab;
            _secondTabService = secondTab;
            _navigationService = navService;
            _libraryService = libService;
            _paneViewModelResolver = paneViewModelResolver;
        }

        public async Task NavigateAsync(NavigationRequest request)
        {
            if (request?.Target == null) return;

            var tabService = request.Pane == PaneId.Second ? _secondTabService : _mainTabService;
            if (tabService == null) return;

            switch (request.Target.Type)
            {
                case NavigationTargetType.Path:
                    await HandlePathRequest(request, tabService);
                    break;
                case NavigationTargetType.Library:
                    HandleLibraryRequest(request, tabService);
                    break;
                case NavigationTargetType.Tag:
                    await HandleTagRequest(request, tabService);
                    break;
                case NavigationTargetType.Search:
                    await HandleSearchRequest(request, tabService);
                    break;
            }

            await Task.CompletedTask;
        }

        private async Task HandlePathRequest(NavigationRequest request, TabService tabService)
        {
            var path = request.Target.Path;
            if (string.IsNullOrEmpty(path)) return;

            // 获取当前面板的 ViewModel
            var vm = _paneViewModelResolver?.Invoke(request.Pane);

            // Rule 1: Drill-down (列表内钻取)
            // 如果来源是列表点击或 Enter，且强制要求在当前标签页打开（默认行为）
            bool isDrillDown = request.Source == YiboFile.Models.Navigation.NavigationSource.FileList ||
                               request.Source == YiboFile.Models.Navigation.NavigationSource.FolderClick;

            if (isDrillDown && !request.ForceNewTab)
            {
                await ExecuteNavigationInViewModel(vm, path, request.Pane, request.Source, tabService, request.PathToSelect, request);
                return;
            }

            // Rule 2: Deduplication (排重检测)
            // 检查当前面板的其他标签页是否已经打开了该路径
            // 注意：如果指定了 PathToSelect，则不能直接返回，因为需要触发选择逻辑
            if (!request.ForceNewTab && string.IsNullOrEmpty(request.PathToSelect))
            {
                var existingTab = tabService.FindTabByPath(path);
                if (existingTab != null)
                {
                    if (request.Activate)
                    {
                        tabService.SetActiveTab(existingTab);
                    }
                    return;
                }
            }

            // Rule 3: Type Consistency Reuse (类型一致性复用)
            // 只有当目标类型与当前标签页类型完全一致时才复用（同构复用）
            var targetType = request.Target.Type;
            // 兼容性处理：如果 Target 类型是 Path 但路径是 tag:// / search://，则推断其实际类型
            if (targetType == NavigationTargetType.Path && path != null)
            {
                if (path.StartsWith("lib://")) targetType = NavigationTargetType.Library;
                else if (path.StartsWith("tag://")) targetType = NavigationTargetType.Tag;
                else if (path.StartsWith("search://") || path.StartsWith("content://")) targetType = NavigationTargetType.Search;
            }

            string targetContentTypeId = targetType switch
            {
                NavigationTargetType.Library => TabContentTypes.Library,
                NavigationTargetType.Tag => TabContentTypes.Tag,
                NavigationTargetType.Search => TabContentTypes.Search,
                _ => TabContentTypes.Path
            };

            if (!request.ForceNewTab && tabService.ActiveTab != null && tabService.ActiveTab.ContentTypeId == targetContentTypeId)
            {
                // Prevent infinite loop if path is already active AND no PathToSelect is requested
                if (string.Equals(tabService.ActiveTab.Path, path, StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(request.PathToSelect))
                {
                    return;
                }

                await ExecuteNavigationInViewModel(vm, path, request.Pane, request.Source, tabService, request.PathToSelect, request);
                return;
            }

            // Rule 4: New Tab Creation (新建标签页)
            // 默认新建，或以上规则不适用
            tabService.CreatePathTab(path, forceNewTab: true, activate: request.Activate);

            // [关键修复] 如果是库路径，同步设置 Tab 的 Library 对象，以便持久化时能保存 ID
            if (path != null && path.StartsWith("lib://"))
            {
                var activeTab = tabService.ActiveTab;
                if (activeTab != null)
                {
                    activeTab.ContentTypeId = TabContentTypes.Library;
                    string libName = path.Substring(6);
                    if (activeTab.Library == null || activeTab.Library.Name != libName)
                    {
                        activeTab.Library = _libraryService?.GetAllLibraries()?.FirstOrDefault(l => l.Name == libName);
                    }
                }
            }

            // [关键修复] CreatePathTab 仅创建 Tab UI，不会驱动 PaneViewModel 加载内容
            // 必须手动同步 PaneViewModel 以触发文件列表加载
            var vmForNewTab = _paneViewModelResolver?.Invoke(request.Pane);
            if (vmForNewTab != null)
            {
                // Guard against stale completions: only apply if this request is still the latest
                if (IsLatestRequest(request.Pane, request.Sequence))
                {
                    vmForNewTab.CurrentPath = path;
                    _messageBus.Publish(new NavigationCompleteMessage(
                        path, request.Pane, request.Source, vmForNewTab.NavigationMode,
                        BackStack: vmForNewTab.BackStack, ForwardStack: vmForNewTab.ForwardStack,
                        PathToSelect: request.PathToSelect));
                }
            }
        }

        private async Task ExecuteNavigationInViewModel(
            ViewModels.PaneViewModel vm, string path, PaneId pane,
            YiboFile.Models.Navigation.NavigationSource source, TabService tabService,
            string pathToSelect = null, NavigationRequest request = null)
        {
            if (vm != null)
            {
                // Only proceed if this request is still the latest for this pane.
                // This prevents stale async completions from fire-and-forget
                // NavigateAsync calls overriding newer navigations (e.g. NavigateUp).
                if (request != null && !IsLatestRequest(pane, request.Sequence))
                {
                    return;
                }

                // 1. 执行导航 (ViewModel)
                // Use CurrentPath setter directly to avoid infinite loop (NavigateTo publishes message)
                vm.CurrentPath = path;

                // 2. [关键修复] 同步更新 Tab 状态
                tabService.UpdateActiveTabPath(path);

                // [关键修复] 如果是库路径，同步更新 Tab 的 Library 对象
                if (path != null && path.StartsWith("lib://"))
                {
                    var activeTab = tabService.ActiveTab;
                    if (activeTab != null)
                    {
                        activeTab.ContentTypeId = TabContentTypes.Library;
                        string libName = path.Substring(6);
                        if (activeTab.Library == null || activeTab.Library.Name != libName)
                        {
                            activeTab.Library = _libraryService?.GetAllLibraries()?.FirstOrDefault(l => l.Name == libName);
                        }
                    }
                }

                // 3. 副作用消息发送 (MessageBus)
                _messageBus.Publish(new NavigationCompleteMessage(
                    path,
                    pane,
                    source,
                    vm.NavigationMode,
                    BackStack: vm.BackStack,
                    ForwardStack: vm.ForwardStack,
                    PathToSelect: pathToSelect));
            }
            else
            {
                // Silent
            }
        }

        /// <summary>
        /// 检查指定序列号是否仍然是该面板最新的导航请求。
        /// 如果已被更新的请求取代，则忽略本次完成回调。
        /// seq=0 表示未设置序列号（非路径导航），视为始终最新。
        /// </summary>
        private bool IsLatestRequest(PaneId pane, long seq)
        {
            if (seq == 0) return true; // Unsequenced requests always pass
            return _navigationSeq.TryGetValue(pane, out var current) && seq == current;
        }

        private void HandleLibraryRequest(NavigationRequest request, TabService tabService)
        {
            var library = request.Target.Library;
            // Fix: Allow navigation to "lib://" root (All Libraries view) where library object is null
            if (library == null && !string.Equals(request.Target.Path, "lib://", StringComparison.OrdinalIgnoreCase)) return;

            // Rule 2: Deduplication (排重检测)
            if (!request.ForceNewTab)
            {
                var existingTab = tabService.FindTabByLibraryId(library.Id);
                if (existingTab != null)
                {
                    if (request.Activate)
                    {
                        tabService.SetActiveTab(existingTab);
                    }
                    return;
                }
            }

            // Rule 3: Type Consistency (类型一致性复用)
            // 如果当前标签页已经是 Library 类型且未要求强制新建，则复用
            // Rule 3: Type Consistency (类型一致性复用)
            // 如果当前标签页已经是 Library 类型且未要求强制新建，则复用
            if (!request.ForceNewTab && tabService.ActiveTab != null && tabService.ActiveTab.ContentTypeId == TabContentTypes.Library)
            {
                if (library != null) ExecuteLibraryNavigationInViewModel(library, request.Pane, tabService);
                else ExecuteLibraryRootNavigationInViewModel(request.Pane, tabService);
                return;
            }

            // Rule 4: New Tab
            if (library != null) tabService.OpenLibraryTab(library, forceNewTab: true, activate: request.Activate);
            else
            {
                tabService.CreatePathTab("lib://", forceNewTab: true, activate: request.Activate);
                if (tabService.ActiveTab != null && tabService.ActiveTab.Path == "lib://")
                {
                    tabService.ActiveTab.ContentTypeId = TabContentTypes.Library;
                    tabService.UpdateTabTitle(tabService.ActiveTab, "lib://");
                }
            }
        }

        private void ExecuteLibraryRootNavigationInViewModel(PaneId pane, TabService tabService)
        {
            var vm = _paneViewModelResolver?.Invoke(pane);
            if (vm != null)
            {
                vm.CurrentLibrary = null;
                vm.CurrentPath = "lib://";

                var activeTab = tabService.ActiveTab;
                if (activeTab != null)
                {
                    activeTab.ContentTypeId = TabContentTypes.Library;
                    activeTab.Path = "lib://";
                    activeTab.Library = null;
                    tabService.UpdateTabTitle(activeTab, "所有库");
                }

                _messageBus.Publish(new NavigationCompleteMessage(
                    "所有库",
                    pane,
                    YiboFile.Models.Navigation.NavigationSource.SidebarLibrary,
                    "Library",
                    BackStack: vm.BackStack,
                    ForwardStack: vm.ForwardStack));
            }
        }

        private void ExecuteLibraryNavigationInViewModel(Library library, PaneId pane, TabService tabService)
        {
            var vm = _paneViewModelResolver?.Invoke(pane);
            if (vm != null)
            {
                // 1. 设置 CurrentLibrary并更新 Path
                vm.CurrentLibrary = library;
                vm.CurrentPath = $"lib://{library.Name}";

                // 2. 同步更新 Tab 状态
                var activeTab = tabService.ActiveTab;
                if (activeTab != null)
                {
                    activeTab.ContentTypeId = TabContentTypes.Library;
                    activeTab.Path = $"lib://{library.Name}";
                    activeTab.Library = library;
                    tabService.UpdateTabTitle(activeTab, library.Name);
                }

                // 3. 发布消息
                _messageBus.Publish(new NavigationCompleteMessage(
                    library.Name,
                    pane,
                    YiboFile.Models.Navigation.NavigationSource.SidebarLibrary,
                    "Library",
                    BackStack: vm.BackStack,
                    ForwardStack: vm.ForwardStack));
            }
        }

        public string GetActivePath(PaneId pane)
        {
            var tabService = pane == PaneId.Second ? _secondTabService : _mainTabService;
            return tabService?.ActiveTab?.Path;
        }

        #region 静态工具与兼容方法

        public static ClickType GetClickType(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
                return ClickType.MiddleClick;

            if (e.ChangedButton == MouseButton.Left &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                return ClickType.CtrlLeftClick;

            return ClickType.LeftClick;
        }

        public void HandlePathNavigation(string path, NavigationSource source, ClickType clickType, bool forceNewTab = false, PaneId pane = PaneId.Main, string pathToSelect = null)
        {
            // Increment sequence — any stale async completion with an older seq is ignored.
            long seq = _navigationSeq.AddOrUpdate(pane, 1, (key, old) => old + 1);

            var request = new NavigationRequest
            {
                Target = NavigationTarget.FromPath(path),
                ForceNewTab = forceNewTab || clickType == ClickType.MiddleClick || clickType == ClickType.CtrlLeftClick,
                Source = source,
                Pane = pane,
                PathToSelect = pathToSelect,
                Sequence = seq
            };
            _ = NavigateAsync(request);
        }

        public void HandleLibraryNavigation(Library library, ClickType clickType, PaneId pane = PaneId.Main)
        {
            var request = new NavigationRequest
            {
                Target = NavigationTarget.FromLibrary(library),
                ForceNewTab = clickType == ClickType.MiddleClick || clickType == ClickType.CtrlLeftClick,
                Pane = pane
            };
            _ = NavigateAsync(request);
        }

        public void HandleFavoriteNavigation(YiboFile.Favorite favorite, ClickType clickType, PaneId pane = PaneId.Main)
        {
            if (favorite == null) return;

            if (favorite.IsDirectory && Directory.Exists(favorite.Path))
            {
                HandlePathNavigation(favorite.Path, NavigationSource.Favorite, clickType, pane: pane);
            }
            else if (!favorite.IsDirectory && File.Exists(favorite.Path))
            {
                _messageBus.Publish(new OpenFileRequestMessage(favorite.Path));
            }
            else
            {
                _messageBus.Publish(new FavoritePathNotFoundMessage(favorite));
            }
        }
        #endregion
        private async Task HandleTagRequest(NavigationRequest request, TabService tabService)
        {
            var tagName = request.Target.TagName;
            if (string.IsNullOrEmpty(tagName)) return;

            var tagPath = $"tag://{tagName}";

            // 优先检查面板中是否已有相同的标签页
            var existingTab = tabService.Tabs.FirstOrDefault(t => t.ContentTypeId == TabContentTypes.Tag && string.Equals(t.Path, tagPath, StringComparison.OrdinalIgnoreCase));
            if (existingTab != null && !request.ForceNewTab)
            {
                if (request.Activate) tabService.SetActiveTab(existingTab);
                return;
            }

            // 同构复用：如果当前标签页是 Tag 类型，则直接更新它
            if (!request.ForceNewTab && tabService.ActiveTab != null && tabService.ActiveTab.ContentTypeId == TabContentTypes.Tag)
            {
                // Prevent infinite loop if path is already active
                if (string.Equals(tabService.ActiveTab.Path, tagPath, StringComparison.OrdinalIgnoreCase)) return;

                var vm = _paneViewModelResolver?.Invoke(request.Pane);
                await ExecuteNavigationInViewModel(vm, tagPath, request.Pane, request.Source, tabService, request: request);
                return;
            }

            // 否则创建新标签页
            tabService.CreateTagTab(tagName, forceNewTab: true, activate: request.Activate);

            // [关键修复] 同步 PaneViewModel 以触发标签文件列表加载
            var vmForNewTag = _paneViewModelResolver?.Invoke(request.Pane);
            if (vmForNewTag != null)
            {
                // Guard against stale completions
                if (IsLatestRequest(request.Pane, request.Sequence))
                {
                    vmForNewTag.CurrentPath = tagPath;
                    _messageBus.Publish(new NavigationCompleteMessage(
                        tagPath, request.Pane, request.Source, vmForNewTag.NavigationMode,
                        BackStack: vmForNewTag.BackStack, ForwardStack: vmForNewTag.ForwardStack));
                }
            }
        }

        private async Task HandleSearchRequest(NavigationRequest request, TabService tabService)
        {
            var searchPath = request.Target.Path;
            if (string.IsNullOrEmpty(searchPath)) return;

            // 优先检查面板中是否已有相同的标签页
            var existingTab = tabService.Tabs.FirstOrDefault(t => t.ContentTypeId == TabContentTypes.Search && string.Equals(t.Path, searchPath, StringComparison.OrdinalIgnoreCase));
            if (existingTab != null && !request.ForceNewTab)
            {
                if (request.Activate) tabService.SetActiveTab(existingTab);
                return;
            }

            // 同构复用：如果当前标签页是 Search 类型，则直接更新它
            if (!request.ForceNewTab && tabService.ActiveTab != null && tabService.ActiveTab.ContentTypeId == TabContentTypes.Search)
            {
                // Prevent infinite loop if path is already active
                if (string.Equals(tabService.ActiveTab.Path, searchPath, StringComparison.OrdinalIgnoreCase)) return;

                var vm = _paneViewModelResolver?.Invoke(request.Pane);
                await ExecuteNavigationInViewModel(vm, searchPath, request.Pane, request.Source, tabService, request: request);
                return;
            }

            // 否则创建新标签页
            tabService.CreateSearchTab(searchPath, forceNewTab: true, activate: request.Activate);

            // [关键修复] 同步 PaneViewModel 以触发搜索结果加载
            var vmForNewSearch = _paneViewModelResolver?.Invoke(request.Pane);
            if (vmForNewSearch != null)
            {
                // Guard against stale completions
                if (IsLatestRequest(request.Pane, request.Sequence))
                {
                    vmForNewSearch.CurrentPath = searchPath;
                    _messageBus.Publish(new NavigationCompleteMessage(
                        searchPath, request.Pane, request.Source, vmForNewSearch.NavigationMode,
                        BackStack: vmForNewSearch.BackStack, ForwardStack: vmForNewSearch.ForwardStack));
                }
            }
        }
    }
}
