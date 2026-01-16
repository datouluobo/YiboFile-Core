using System.Windows.Controls;

namespace YiboFile.Services.UIHelper
{
    /// <summary>
    /// UI 辅助服务接口
    /// 提供通用的 UI 操作辅助方法
    /// </summary>
    public interface IUIHelperService
    {
        /// <summary>
        /// 确保 ListBox 的选中项可见并正确显示
        /// </summary>
        /// <param name="listBox">目标 ListBox</param>
        /// <param name="selectedItem">要选中的项</param>
        void EnsureSelectedItemVisible(ListBox listBox, object selectedItem);

        /// <summary>
        /// 确保 ListView 的选中项可见并正确显示
        /// </summary>
        /// <param name="listView">目标 ListView</param>
        /// <param name="selectedItem">要选中的项</param>
        void EnsureSelectedItemVisible(ListView listView, object selectedItem);

        /// <summary>
        /// 更新地址栏文本
        /// </summary>
        /// <param name="text">地址栏文本</param>
        void UpdateAddressBar(string text);

        /// <summary>
        /// 更新面包屑导航文本
        /// </summary>
        /// <param name="text">面包屑文本</param>
        void UpdateBreadcrumb(string text);
    }
}





