using YiboFile.Models;
using YiboFile.Services.Navigation;

namespace YiboFile.Models.Navigation
{
    /// <summary>
    /// 导航来源
    /// </summary>
    public enum NavigationSource
    {
        Drive,          // 驱动器
        QuickAccess,    // 快速访问
        Favorite,       // 收藏夹
        Library,        // 库
        Breadcrumb,     // 面包屑
        AddressBar,     // 地址栏
        FileList,       // 文件列表
        SidebarLibrary, // 侧边栏库
        FolderClick,    // 文件夹点击
        History,        // 历史记录
        SidebarTag,     // 侧边栏标签
        Search,         // 搜索
        External        // 外部跳转
    }

    /// <summary>
    /// 点击类型
    /// </summary>
    public enum ClickType
    {
        LeftClick,      // 左键点击
        MiddleClick,    // 中键点击
        CtrlLeftClick,  // Ctrl+左键点击
        RightClick      // 右键点击
    }

    /// <summary>
    /// 导航目标类型
    /// </summary>
    public enum NavigationTargetType
    {
        Path,       // 普通文件路径
        Library,    // 库
        Tag,        // 标签
        Search,     // 搜索结果
        Content     // 笔记/文件内容
    }

    /// <summary>
    /// 导航目标描述
    /// </summary>
    public class NavigationTarget
    {
        public NavigationTargetType Type { get; set; }
        public string Path { get; set; }
        public Library Library { get; set; }
        public string SearchKeyword { get; set; }
        public string TagName { get; set; }

        public static NavigationTarget FromPath(string path) => new() { Type = NavigationTargetType.Path, Path = path };
        public static NavigationTarget FromLibrary(Library library) => new() { Type = NavigationTargetType.Library, Library = library, Path = $"lib://{library?.Name}" };
        public static NavigationTarget FromTag(string tagName) => new() { Type = NavigationTargetType.Tag, TagName = tagName, Path = $"tag://{tagName}" };
        public static NavigationTarget FromSearch(string keyword) => new() { Type = NavigationTargetType.Search, SearchKeyword = keyword, Path = $"search://{keyword}" };
    }

    /// <summary>
    /// 统一导航请求
    /// </summary>
    public class NavigationRequest
    {
        /// <summary>
        /// 目标位置
        /// </summary>
        public NavigationTarget Target { get; set; }

        /// <summary>
        /// 目标面板
        /// </summary>
        public PaneId Pane { get; set; } = PaneId.Main;

        /// <summary>
        /// 是否强制在新标签页打开
        /// </summary>
        public bool ForceNewTab { get; set; }

        /// <summary>
        /// 是否激活该标签页
        /// </summary>
        public bool Activate { get; set; } = true;

        /// <summary>
        /// 导航来源标识
        /// </summary>
        public NavigationSource Source { get; set; } = NavigationSource.External;
    }
}
