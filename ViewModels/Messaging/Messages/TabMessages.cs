namespace YiboFile.ViewModels.Messaging.Messages
{
    using YiboFile.Services.Tabs; // For PathTab
    using YiboFile.Services.Navigation; // For PaneId

    /// <summary>
    /// 标签页相关消息
    /// </summary>
    public class TabMessages { }

    /// <summary>
    /// 请求创建新标签页
    /// </summary>
    public record CreateTabMessage(string Path = null, bool Activate = true, PaneId? Pane = null);

    /// <summary>
    /// 标签页已激活通知 (UI层同步)
    /// </summary>
    public record TabActivatedMessage(string TabId, string Path, bool IsLibraryTab = false);

    /// <summary>
    /// 请求关闭标签页
    /// </summary>
    public record CloseTabMessage(string TabId);

    /// <summary>
    /// 标签页已关闭通知
    /// </summary>
    public record TabClosedMessage(string TabId);

    /// <summary>
    /// 请求切换到指定标签页
    /// </summary>
    public record SwitchToTabMessage(string TabId);

    /// <summary>
    /// 标签页路径已更新
    /// </summary>
    public record TabPathUpdatedMessage(string TabId, string NewPath);

    // --- Service Layer Events Replacements ---

    /// <summary>
    /// Service层活动标签页变更事件
    /// </summary>
    public record TabActiveChangedMessage(PathTab ActiveTab, PaneId Pane);

    /// <summary>
    /// Service层标签页固定状态变更事件
    /// </summary>
    public record TabPinStateChangedMessage(PathTab Tab, PaneId Pane);

    /// <summary>
    /// Service层标签页标题变更事件
    /// </summary>
    public record TabTitleChangedMessage(PathTab Tab, string OldTitle, string NewTitle, PaneId Pane);

    /// <summary>
    /// Service层标签页添加事件
    /// </summary>
    public record TabAddedMessage(PathTab Tab, PaneId Pane);

    /// <summary>
    /// Service层标签页移除事件
    /// </summary>
    public record TabRemovedMessage(PathTab Tab, PaneId Pane);
}
