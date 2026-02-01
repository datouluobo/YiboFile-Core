namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 搜索相关消息
    /// </summary>

    /// <summary>
    /// 触发执行搜素
    /// </summary>
    public record ExecuteSearchMessage(string SearchText, bool SearchNames, bool SearchNotes, string TargetPaneId = "Primary");

    /// <summary>
    /// 搜索结果更新通知
    /// </summary>
    public record SearchResultUpdatedMessage(
        System.Collections.Generic.List<YiboFile.Models.FileSystemItem> Results,
        string StatusMessage,
        bool IsSearching,
        string TargetPaneId,
        bool HasMore = false,
        string SearchTabPath = null,
        string NormalizedKeyword = null);

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
