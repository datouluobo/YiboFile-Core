using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controls;

namespace YiboFile.Services.Config
{
    /// <summary>
    /// 配置 UI 辅助接口
    /// 用于封装 MainWindow UI 控件的访问，避免服务层直接依赖 WPF 控件
    /// </summary>
    public interface IConfigUIHelper
    {
        /// <summary>
        /// 获取窗口实例
        /// </summary>
        Window Window { get; }

        /// <summary>
        /// 获取根 Grid
        /// </summary>
        Grid RootGrid { get; }

        /// <summary>
        /// 获取左侧列定义
        /// </summary>
        ColumnDefinition ColLeft { get; }

        /// <summary>
        /// 获取中间列定义
        /// </summary>
        ColumnDefinition ColCenter { get; }

        /// <summary>
        /// 获取右侧列定义
        /// </summary>
        ColumnDefinition ColRight { get; }

        /// <summary>
        /// 获取 TitleActionBar 控件
        /// </summary>
        TitleActionBar TitleActionBar { get; }

        /// <summary>
        /// 获取右侧面板控件
        /// </summary>
        RightPanelControl RightPanelControl { get; }

        /// <summary>
        /// 获取文件浏览器控件
        /// </summary>
        FileBrowserControl FileBrowser { get; }

        /// <summary>
        /// 获取当前路径
        /// </summary>
        string CurrentPath { get; set; }

        /// <summary>
        /// 获取当前库
        /// </summary>
        object CurrentLibrary { get; }



        /// <summary>
        /// 调整列宽度
        /// </summary>
        void AdjustColumnWidths();

        /// <summary>
        /// 确保列最小宽度
        /// </summary>
        void EnsureColumnMinWidths();

        /// <summary>
        /// 获取 Dispatcher（用于 UI 线程操作）
        /// </summary>
        System.Windows.Threading.Dispatcher Dispatcher { get; }



        /// <summary>
        /// 更新窗口状态UI（最大化/还原按钮图标）
        /// </summary>
        void UpdateWindowStateUI();
    }
}


