using YiboFile.Services.Navigation;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 导航完成消息
    /// 用于通知跨组件（如地址栏、副作用处理逻辑、插件）
    /// </summary>
    public record NavigationCompleteMessage(
        string Path,
        PaneId Pane,
        NavigationSource Source,
        string NavigationMode = "Path");

    /// <summary>
    /// 导航状态变更消息 (可选，用于同步全局按钮状态)
    /// </summary>
    public record NavigationStatusChangedMessage(
        PaneId Pane,
        bool CanBack,
        bool CanForward,
        bool CanUp);

    /// <summary>
    /// 路径已变更消息 (轻量级通知)
    /// </summary>
    public record PathChangedMessage(string NewPath, PaneId Pane, string OldPath = null);

    /// <summary>
    /// 导航模式已变更消息
    /// </summary>
    public record NavigationModeChangedMessage(string Mode);

    /// <summary>
    /// 请求导航到指定路径
    /// </summary>
    public record NavigateToPathMessage(string Path, bool AddToHistory = true);

    /// <summary>
    /// 请求切换导航模式
    /// </summary>
    public record RequestNavigationModeMessage(string Mode);

    /// <summary>
    /// 请求切换布局模式
    /// </summary>
    public record RequestLayoutModeMessage(string Mode);

    /// <summary>
    /// 请求切换双列表模式
    /// </summary>
    public record RequestDualListToggleMessage();

    /// <summary>
    /// 请求后退
    /// </summary>
    public record NavigateBackMessage();

    /// <summary>
    /// 请求前进
    /// </summary>
    public record NavigateForwardMessage();

    /// <summary>
    /// 请求向上导航
    /// </summary>
    public record NavigateUpMessage();
}
