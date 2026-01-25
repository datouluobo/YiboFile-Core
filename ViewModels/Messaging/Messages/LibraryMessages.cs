namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 库相关消息
    /// </summary>

    /// <summary>
    /// 库已选择通知
    /// </summary>
    public record LibrarySelectedMessage(int LibraryId, string LibraryName);

    /// <summary>
    /// 请求打开库
    /// </summary>
    public record OpenLibraryMessage(int LibraryId);

    /// <summary>
    /// 库内容已加载
    /// </summary>
    public record LibraryLoadedMessage(int LibraryId, int FileCount);
}
