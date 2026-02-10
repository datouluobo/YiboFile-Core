using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YiboFile.Models;
using YiboFile.Models.Navigation;
using YiboFile.Services.Tabs;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Services.Navigation
{
    public enum NavigationSource
    {
        AddressBar,
        Breadcrumb,
        SidebarLibrary,
        FileList,
        Favorite,
        QuickAccess,
        FolderClick,
        History,
        External
    }

    public enum ClickType
    {
        LeftClick,
        CtrlLeftClick,
        MiddleClick,
        RightClick
    }

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

        public NavigationCoordinator(ViewModels.Messaging.IMessageBus messageBus)
        {
            _messageBus = messageBus;
        }

        // 兼容旧代码的事件，直到迁移完成

        public event Action<string> FileOpenRequested;
        public event Action<YiboFile.Favorite> FavoritePathNotFound;

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
                    // TODO: Implement HandleSearchRequest
                    await HandlePathRequest(request, tabService);
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
            bool isDrillDown = request.Source == NavigationSource.FileList.ToString() ||
                               request.Source == NavigationSource.FolderClick.ToString();

            if (isDrillDown && !request.ForceNewTab)
            {
                await ExecuteNavigationInViewModel(vm, path, request.Pane, request.Source, tabService);
                return;
            }

            // Rule 2: Deduplication (排重检测)
            // 检查当前面板的其他标签页是否已经打开了该路径
            if (!request.ForceNewTab)
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
            // 如果当前标签页是路径类型，且未要求强制新建，则复用
            if (!request.ForceNewTab && tabService.ActiveTab != null && tabService.ActiveTab.Type == TabType.Path)
            {
                await ExecuteNavigationInViewModel(vm, path, request.Pane, request.Source, tabService);
                return;
            }

            // Rule 4: New Tab Creation (新建标签页)
            // 默认新建，或以上规则不适用
            tabService.CreatePathTab(path, forceNewTab: true, activate: request.Activate);
        }

        private async Task ExecuteNavigationInViewModel(ViewModels.PaneViewModel vm, string path, PaneId pane, string source, TabService tabService)
        {
            if (vm != null)
            {
                // 1. 执行导航 (ViewModel)
                vm.NavigateTo(path);

                // 2. [关键修复] 同步更新 Tab 状态
                tabService.UpdateActiveTabPath(path);

                // 3. 副作用消息发送 (MessageBus)
                var sourceStr = source ?? NavigationSource.External.ToString();
                _messageBus.Publish(new NavigationCompleteMessage(
                    path,
                    pane,
                    Enum.TryParse<NavigationSource>(sourceStr, out var src) ? src : NavigationSource.External,
                    vm.NavigationMode));
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationCoordinator] Warning: Cannot resolve PaneVM for {pane}. Path: {path}");
            }
        }

        private void HandleLibraryRequest(NavigationRequest request, TabService tabService)
        {
            var library = request.Target.Library;
            if (library == null) return;

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
            if (!request.ForceNewTab && tabService.ActiveTab != null && tabService.ActiveTab.Type == TabType.Library)
            {
                ExecuteLibraryNavigationInViewModel(library, request.Pane, tabService);
                return;
            }

            // Rule 4: New Tab
            tabService.OpenLibraryTab(library, forceNewTab: true, activate: request.Activate);
        }

        private void ExecuteLibraryNavigationInViewModel(Library library, PaneId pane, TabService tabService)
        {
            var vm = _paneViewModelResolver?.Invoke(pane);
            if (vm != null)
            {
                // 1. 更新 ViewModel
                vm.NavigateTo(library, loadData: true);

                // 2. 同步更新 Tab 状态
                var activeTab = tabService.ActiveTab;
                if (activeTab != null)
                {
                    activeTab.Type = TabType.Library;
                    activeTab.Path = $"lib://{library.Name}";
                    tabService.UpdateTabTitle(activeTab, library.Name);
                }

                // 3. 发布消息
                _messageBus.Publish(new NavigationCompleteMessage(
                    $"lib://{library.Name}",
                    pane,
                    NavigationSource.SidebarLibrary));
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

        public void HandlePathNavigation(string path, NavigationSource source, ClickType clickType, bool forceNewTab = false, PaneId pane = PaneId.Main)
        {
            var request = new NavigationRequest
            {
                Target = NavigationTarget.FromPath(path),
                ForceNewTab = forceNewTab || clickType == ClickType.MiddleClick || clickType == ClickType.CtrlLeftClick,
                Source = source.ToString(),
                Pane = pane
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
                FileOpenRequested?.Invoke(favorite.Path);
            }
            else
            {
                FavoritePathNotFound?.Invoke(favorite);
            }
        }
        #endregion
        private async Task HandleTagRequest(NavigationRequest request, TabService tabService)
        {
            var tagName = request.Target.TagName;
            if (string.IsNullOrEmpty(tagName)) return;

            var tagPath = $"tag://{tagName}";

            // 标签导航逻辑与路径导航对齐：支持在当前标签页打开
            var modifiedRequest = new NavigationRequest
            {
                Target = NavigationTarget.FromPath(tagPath),
                Pane = request.Pane,
                ForceNewTab = request.ForceNewTab,
                Activate = request.Activate,
                Source = request.Source ?? "Tag"
            };

            await HandlePathRequest(modifiedRequest, tabService);
        }
    }
}


