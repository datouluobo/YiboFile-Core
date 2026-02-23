using System.Windows.Controls;

namespace YiboFile.Interfaces.Plugins
{
    /// <summary>
    /// 可承载于标签页的内容的统一抽象。
    /// 所有特殊标签页（设置、关于、管理、任务队列等）以及
    /// Pro/Ultra/第三方插件注册的自定义标签页均需实现此接口。
    /// 
    /// 实现者负责管理 View 实例的生命周期和缓存策略：
    /// - AllowMultiple=false 的面板应在首次 CreateView() 后缓存实例
    /// - OnDeactivated() 时不应销毁 View（除非内存压力触发回收）
    /// </summary>
    public interface ITabContent
    {
        /// <summary>
        /// 内容类型唯一标识。
        /// Core 内置类型使用小写字母（如 "settings", "about", "tasks"）。
        /// 插件类型建议使用 "vendor.name" 格式（如 "yibofile.fts-results"）。
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 标签页显示标题。
        /// </summary>
        string Title { get; }

        /// <summary>
        /// 图标资源键（引用 Icon Contract 中的键名）。
        /// </summary>
        string IconKey { get; }

        /// <summary>
        /// 是否允许同时打开多个同类型标签页。
        /// 例如：文件浏览 = true, 设置 = false。
        /// </summary>
        bool AllowMultiple { get; }

        /// <summary>
        /// 是否支持在副栏打开。
        /// 某些面板（如设置页）可能在窄副栏中体验不佳，可返回 false。
        /// </summary>
        bool SupportsSecondaryPane { get; }

        /// <summary>
        /// 创建或返回缓存的 View 实例。
        /// 实现者应在内部管理 View 的生命周期和缓存策略。
        /// 首次调用时创建，后续调用可返回缓存实例。
        /// </summary>
        /// <returns>标签页内容的 UserControl 实例。</returns>
        UserControl CreateView();

        /// <summary>
        /// 标签页被激活时调用（切换到前台）。
        /// 用于恢复后台任务、刷新数据、播放入场动画等。
        /// </summary>
        void OnActivated();

        /// <summary>
        /// 标签页被切离时调用（切换到后台）。
        /// 用于暂停后台任务、保存临时状态、播放退场动画等。
        /// 注意：此时 View 不应被销毁（除非内存压力触发 LRU 回收）。
        /// </summary>
        void OnDeactivated();

        /// <summary>
        /// 标签页被关闭时调用（用户点击关闭按钮）。
        /// 用于释放资源、保存最终状态。调用后此实例不再被复用。
        /// </summary>
        void OnClosed();
    }
}
