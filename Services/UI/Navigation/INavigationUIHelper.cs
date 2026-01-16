using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YiboFile.Services.Navigation
{
    /// <summary>
    /// 导航 UI 辅助接口
    /// 用于封装 UI 控件的访问，避免服务层直接依赖 WPF 控件
    /// </summary>
    public interface INavigationUIHelper
    {
        /// <summary>
        /// 获取驱动器列表
        /// </summary>
        IEnumerable GetDrivesListItems();

        /// <summary>
        /// 获取快速访问列表
        /// </summary>
        IEnumerable GetQuickAccessListItems();

        /// <summary>
        /// 获取收藏列表
        /// </summary>
        IEnumerable GetFavoritesListItems();

        /// <summary>
        /// 获取库列表
        /// </summary>
        IEnumerable GetLibrariesListItems();

        /// <summary>
        /// 设置列表项的高亮状态
        /// </summary>
        void SetItemHighlight(string listType, object item, bool highlight);

        /// <summary>
        /// 清除指定列表的所有高亮
        /// </summary>
        void ClearListBoxHighlights(string listType);

        /// <summary>
        /// 设置导航内容区域的可见性
        /// </summary>
        void SetNavigationContentVisibility(string mode);

        /// <summary>
        /// 更新操作按钮模式
        /// </summary>
        void UpdateActionButtons(string mode);

        /// <summary>
        /// 获取资源（用于高亮样式）
        /// </summary>
        SolidColorBrush GetResourceBrush(string resourceKey);

        /// <summary>
        /// 获取 Dispatcher（用于 UI 线程操作）
        /// </summary>
        System.Windows.Threading.Dispatcher Dispatcher { get; }

        /// <summary>
        /// 设置库列表的选中项
        /// </summary>
        void SetLibrarySelectedItem(object library);
    }
}






