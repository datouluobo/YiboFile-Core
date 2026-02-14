using System.Threading.Tasks;
using YiboFile.Models.Navigation;
using YiboFile;

namespace YiboFile.Services.Navigation
{
    /// <summary>
    /// 统一导航协调器接口
    /// 负责调度整个应用的导航逻辑，解决标签页与列表同步问题
    /// </summary>
    public interface INavigationCoordinator
    {
        /// <summary>
        /// 执行统一导航请求
        /// </summary>
        Task NavigateAsync(NavigationRequest request);

        /// <summary>
        /// 获取当前指定面板的活动路径
        /// </summary>
        string GetActivePath(PaneId pane);

        /// <summary>
        /// 统一路径导航处理
        /// </summary>
        void HandlePathNavigation(string path, YiboFile.Models.Navigation.NavigationSource source, YiboFile.Models.Navigation.ClickType clickType, bool forceNewTab = false, PaneId pane = PaneId.Main);

        /// <summary>
        /// 统一库导航处理
        /// </summary>
        void HandleLibraryNavigation(Library library, YiboFile.Models.Navigation.ClickType clickType, PaneId pane = PaneId.Main);

        /// <summary>
        /// 统一收藏导航处理
        /// </summary>
        void HandleFavoriteNavigation(YiboFile.Favorite favorite, YiboFile.Models.Navigation.ClickType clickType, PaneId pane = PaneId.Main);
    }
}
