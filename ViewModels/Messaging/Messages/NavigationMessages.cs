namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 导航相关消息
    /// </summary>

    /// <summary>
    /// 请求导航到指定路径
    /// </summary>
    public record NavigateToPathMessage(string Path, bool AddToHistory = true);

    /// <summary>
    /// 路径已变更通知
    /// </summary>
    public record PathChangedMessage(string NewPath, string OldPath = null);

    /// <summary>
    /// 请求后退导航
    /// </summary>
    public record NavigateBackMessage();

    /// <summary>
    /// 请求前进导航
    /// </summary>
    public record NavigateForwardMessage();

    /// <summary>
    /// 请求向上导航
    /// </summary>
    public record NavigateUpMessage();

    /// <summary>
    /// 导航模式切换消息
    /// </summary>
    public record NavigationModeChangedMessage(string Mode);
}
