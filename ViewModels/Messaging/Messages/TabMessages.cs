namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 标签页相关消息
    /// </summary>

    /// <summary>
    /// 请求创建新标签页
    /// </summary>
    public record CreateTabMessage(string Path = null, bool Activate = true);

    /// <summary>
    /// 标签页已激活通知
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
}
