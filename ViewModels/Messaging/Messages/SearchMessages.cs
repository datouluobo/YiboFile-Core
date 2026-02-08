namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 搜索相关消息
    /// </summary>

    // ===== Request 消息（用户操作 → Coordinator） =====

    /// <summary>
    /// 请求设置搜索范围预设（复杂的多状态协调逻辑）
    /// </summary>
    public record RequestSetScopePresetMessage(string Preset);

    // ===== Notification 消息（Coordinator → 订阅者） =====

    /// <summary>
    /// 触发执行搜素
    /// </summary>
    public record ExecuteSearchMessage(string SearchText, bool SearchNames, bool SearchNotes, string TargetPaneId = "Primary", YiboFile.Services.Search.SearchOptions Options = null);

    /// <summary>
    /// 搜索结果更新通知
    /// </summary>
    public record SearchResultUpdatedMessage(
        System.Collections.Generic.List<YiboFile.Models.FileSystemItem> Results,
        string StatusMessage,
        bool IsSearching,
        string TargetPaneId,
        bool HasMore = false,
        int Offset = 0,
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
    /// 搜索选项变更通知
    /// </summary>
    public record SearchOptionsChangedMessage(YiboFile.Services.Search.SearchOptions Options, string TargetPaneId);

    /// <summary>
    /// 搜索取消请求
    /// </summary>
    public record SearchCancelMessage();
}
