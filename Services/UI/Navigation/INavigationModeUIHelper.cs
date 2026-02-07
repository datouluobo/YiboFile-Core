using System;
using System.Windows;
using System.Windows.Controls;


namespace YiboFile.Services.Navigation
{
    /// <summary>
    /// 导航模式 UI 辅助接口
    /// 用于封装 MainWindow 的 UI 访问，避免服务层直接依赖 WPF 控件
    /// </summary>
    public interface INavigationModeUIHelper
    {
        /// <summary>
        /// 获取 Dispatcher（用于 UI 线程操作）
        /// </summary>
        System.Windows.Threading.Dispatcher Dispatcher { get; }

        /// <summary>
        /// 获取或设置当前库
        /// </summary>
        Library CurrentLibrary { get; set; }

        /// <summary>
        /// 获取或设置当前路径
        /// </summary>
        string CurrentPath { get; set; }



        /// <summary>
        /// 获取标签底部按钮面板
        /// </summary>




        /// <summary>
        /// 获取标签导航内容区域
        /// </summary>




        /// <summary>
        /// 获取文件浏览器控件
        /// </summary>
        Controls.FileBrowserControl FileBrowser { get; }

        /// <summary>
        /// 获取库列表控件
        /// </summary>
        ListBox LibrariesListBox { get; }



        /// <summary>
        /// 切换到指定标签页
        /// </summary>
        void SwitchToTab(Services.Tabs.PathTab tab);

        /// <summary>
        /// 创建新标签页
        /// </summary>
        void CreateTab(string path);

        /// <summary>
        /// 高亮匹配的库
        /// </summary>
        void HighlightMatchingLibrary(Library library);

        /// <summary>
        /// 确保列表项可见
        /// </summary>
        void EnsureSelectedItemVisible(ListBox listBox, object selectedItem);

        /// <summary>
        /// 加载库文件
        /// </summary>
        void LoadLibraryFiles(Library library);

        /// <summary>
        /// 初始化导航面板拖拽支持
        /// </summary>
        void InitializeNavigationPanelDragDrop();

        /// <summary>
        /// 应用当前模式的可见列设置
        /// </summary>
        void ApplyVisibleColumnsForCurrentMode();

        /// <summary>
        /// 确保表头右键菜单绑定
        /// </summary>
        void EnsureHeaderContextMenuHook();

        /// <summary>
        /// 刷新文件列表
        /// </summary>
        void RefreshFileList();

        /// <summary>
        /// 获取导航面板控件
        /// </summary>
        Controls.NavigationPanelControl NavigationPanelControl { get; }

        /// <summary>
        /// 获取路径导航按钮
        /// </summary>
        System.Windows.Controls.Button NavPathButton { get; }

        /// <summary>
        /// 获取库导航按钮
        /// </summary>
        System.Windows.Controls.Button NavLibraryButton { get; }

        /// <summary>
        /// 获取标签导航按钮
        /// </summary>
        System.Windows.Controls.Button NavTagButton { get; }

        /// <summary>
        /// 刷新标签列表
        /// </summary>
        void RefreshTagList();

        //     Browse,
        //     Edit
        // }
    }


}


