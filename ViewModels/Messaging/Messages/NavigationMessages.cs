using YiboFile.Models.Navigation;
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
        string NavigationMode = "Path",
        System.Collections.Generic.IEnumerable<string> BackStack = null,
        System.Collections.Generic.IEnumerable<string> ForwardStack = null);

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
    public record NavigateToPathMessage(string Path, bool AddToHistory = true, PaneId? Pane = null);

    /// <summary>
    /// 请求切换导航模式
    /// </summary>
    public record RequestNavigationModeMessage(string Mode);

    /// <summary>
    /// 请求切换布局模式（系统/特殊页使用）
    /// </summary>
    public record RequestLayoutModeMessage(string Mode);

    /// <summary>
    /// 请求切换左侧导航面板显隐
    /// </summary>
    public record RequestSidebarToggleMessage();

    /// <summary>
    /// 导航栏显隐状态变更消息
    /// </summary>
    public class SidebarVisibilityChangedMessage
    {
        public bool IsHidden { get; }
        public SidebarVisibilityChangedMessage(bool isHidden) { IsHidden = isHidden; }
    }

    /// <summary>
    /// 请求切换双列表模式
    /// </summary>
    public record RequestDualPaneToggleMessage();

    /// <summary>
    /// 面板布局模式枚举：单栏 / 双栏 / 预览
    /// </summary>
    public enum PaneMode
    {
        Single,     // 单栏（仅主面板）
        DualPane,   // 双栏（左右两个文件列表）
        Preview     // 预览（焦点面板文件列表 + 非焦点面板显示预览）
    }

    /// <summary>
    /// 请求面板布局模式循环切换（三态：单栏→双栏→预览→单栏）
    /// </summary>
    public record RequestPaneModeToggleMessage();

    /// <summary>
    /// 面板布局模式变更通知
    /// </summary>
    public class PaneModeChangedMessage
    {
        public PaneMode Mode { get; }
        public PaneModeChangedMessage(PaneMode mode) { Mode = mode; }
    }

    /// <summary>
    /// 请求后退
    /// </summary>
    public record NavigateBackMessage(PaneId? Pane = null);

    /// <summary>
    /// 请求前进
    /// </summary>
    public record NavigateForwardMessage(PaneId? Pane = null);

    /// <summary>
    /// 请求向上导航
    /// </summary>
    public record NavigateUpMessage(PaneId? Pane = null);

    /// <summary>
    /// 请求导航到指定库
    /// </summary>
    public record NavigateToLibraryMessage(YiboFile.Library Library, PaneId? Pane = null);

    /// <summary>
    /// 恢复导航状态消息 (用于标签页切换)
    /// </summary>
    public record RestoreNavigationStateMessage(
        string Path,
        System.Collections.Generic.Stack<string> BackStack,
        System.Collections.Generic.Stack<string> ForwardStack,
        PaneId Pane);
}
