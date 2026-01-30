using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YiboFile.Models;
using YiboFile.Models.Navigation;
using YiboFile.Services.Tabs;

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

        // Pane-specific navigation delegates
        private Action<string> _navigateMain;
        private Action<string> _navigateSecond;

        // 兼容旧代码的事件，直到迁移完成
        public event Action<string, bool, bool?> PathNavigateRequested;
        public event Action<Library, bool, bool?> LibraryNavigateRequested;
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
            Action<string> navigateMain = null,
            Action<string> navigateSecond = null)
        {
            _mainTabService = mainTab;
            _secondTabService = secondTab;
            _navigationService = navService;
            _libraryService = libService;
            _navigateMain = navigateMain;
            _navigateSecond = navigateSecond;
        }

        public async Task NavigateAsync(NavigationRequest request)
        {
            if (request?.Target == null) return;

            var tabService = request.Pane == PaneId.Second ? _secondTabService : _mainTabService;
            if (tabService == null) return;

            switch (request.Target.Type)
            {
                case NavigationTargetType.Path:
                    HandlePathRequest(request, tabService);
                    break;
                case NavigationTargetType.Library:
                    HandleLibraryRequest(request, tabService);
                    break;
            }

            await Task.CompletedTask;
        }

        private void HandlePathRequest(NavigationRequest request, TabService tabService)
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
                // 使用面板特定的导航委托
                if (request.Pane == PaneId.Main && _navigateMain != null)
                {
                    _navigateMain(path);
                }
                else if (request.Pane == PaneId.Second && _navigateSecond != null)
                {
                    _navigateSecond(path);
                }
                else
                {
                    // 回退到全局事件 (主要针对 MainWindow 处理)
                    // 注意：这可能无法正确处理副面板，因此应尽量使用 Initialize 传入的委托
                    PathNavigateRequested?.Invoke(path, false, request.Activate);
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
                // 库导航通常比较特殊，暂时保持使用事件或后续扩展委托
                // 目前 MainWindow 监听 LibraryNavigateRequested 并处理 OpenLibraryInTab (通常是 Main logic?)
                // 如果需要支持副面板库切换，这里也需要增强
                if (request.Pane == PaneId.Second)
                {
                    // 副面板库切换，直接操作 TabService
                    tabService.OpenLibraryTab(library, forceNewTab: false, activate: request.Activate);
                }
                else
                {
                    LibraryNavigateRequested?.Invoke(library, false, request.Activate);
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


