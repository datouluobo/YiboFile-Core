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
            }

            await Task.CompletedTask;
        }

        private async Task HandlePathRequest(NavigationRequest request, TabService tabService)
        {
            var path = request.Target.Path;
            if (string.IsNullOrEmpty(path)) return;

            // [FIX] 如果当前标签页是库，当导航到普通路径时，强制新建标签页，避免覆盖库标签页
            bool forceNewTab = request.ForceNewTab;
            if (!forceNewTab && tabService.ActiveTab != null && tabService.ActiveTab.Type == TabType.Library)
            {
                forceNewTab = true;
            }

            if (forceNewTab)
            {
                tabService.CreatePathTab(path, forceNewTab: true, activate: request.Activate);
            }
            else
            {
                // Path C: 直接驱动 ViewModel
                var vm = _paneViewModelResolver?.Invoke(request.Pane);
                if (vm != null)
                {
                    // 这里执行导航
                    vm.NavigateTo(path);

                    // 副作用消息发送 (MessageBus)
                    var sourceStr = request.Source ?? NavigationSource.External.ToString();
                    _messageBus.Publish(new NavigationCompleteMessage(
                        path,
                        request.Pane,
                        Enum.TryParse<NavigationSource>(sourceStr, out var src) ? src : NavigationSource.External,
                        vm.NavigationMode));
                }
                else
                {
                    // Fallback removed: PathNavigateRequested legacy event.
                    // This block should ideally not be reached if resolver is set up correctly.
                    System.Diagnostics.Debug.WriteLine($"[NavigationCoordinator] Warning: Cannot resolve PaneVM for {request.Pane}. Path: {path}");
                }
            }
        }

        private void HandleLibraryRequest(NavigationRequest request, TabService tabService)
        {
            var library = request.Target.Library;
            if (library == null) return;

            if (request.ForceNewTab)
            {
                tabService.OpenLibraryTab(library, forceNewTab: true, activate: request.Activate);
            }
            else
            {
                // 修改 ViewModel 的库模式
                var vm = _paneViewModelResolver?.Invoke(request.Pane);
                if (vm != null)
                {
                    vm.CurrentLibrary = library;
                    vm.NavigationMode = "Library";

                    _messageBus.Publish(new NavigationCompleteMessage(
                        $"lib://{library.Name}",
                        request.Pane,
                        NavigationSource.SidebarLibrary));
                }
                else
                {
                    // Fallback removed: LibraryNavigateRequested legacy event.
                    System.Diagnostics.Debug.WriteLine($"[NavigationCoordinator] Warning: Cannot resolve PaneVM for {request.Pane}. Library: {library.Name}");
                }
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

        public void HandleFavoriteNavigation(YiboFile.Favorite favorite, ClickType clickType)
        {
            if (favorite == null) return;

            if (favorite.IsDirectory && Directory.Exists(favorite.Path))
            {
                HandlePathNavigation(favorite.Path, NavigationSource.Favorite, clickType);
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
    }
}


