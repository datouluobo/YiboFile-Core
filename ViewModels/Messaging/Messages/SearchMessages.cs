namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 搜索相关消息
    /// </summary>

    /// <summary>
    /// 搜索请求
    /// </summary>
    public record SearchRequestMessage(string Query, string SearchPath = null);

    /// <summary>
    /// 搜索完成通知
    /// </summary>
    public record SearchCompletedMessage(string Query, int ResultCount, long ElapsedMs);

    /// <summary>
    /// 搜索取消请求
    /// </summary>
    public record SearchCancelMessage();
}
