using YiboFile;
using YiboFile.Models;
using YiboFile.Services.Navigation;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 库相关消息
    /// </summary>

    /// <summary>
    /// 库已选择通知
    /// </summary>
    public record LibrarySelectedMessage(Library Library, PaneId? Pane = null);

    /// <summary>
    /// 请求打开库
    /// </summary>
    public record OpenLibraryMessage(int LibraryId);

    /// <summary>
    /// 库内容已加载
    /// </summary>
    public record LibraryLoadedMessage(int LibraryId, int FileCount);

    /// <summary>
    /// 请求切换库路径（添加或移除）
    /// </summary>
    public record ToggleLibraryPathRequestMessage(Library Library, System.Collections.Generic.List<string> Paths, bool ForceAdd = false);

    /// <summary>
    /// 请求创建新库
    /// </summary>
    public record CreateLibraryRequestMessage(string Name, System.Collections.Generic.List<string> InitialPaths = null);

    /// <summary>
    /// 库列表发生变化（添加、删除或内容修改）
    /// </summary>
    public record LibraryListChangedMessage();

    /// <summary>
    /// 库文件列表已加载
    /// </summary>
    public record LibraryFilesLoadedMessage(Library Library, System.Collections.Generic.List<FileSystemItem> Files, bool IsEmpty, YiboFile.Services.Navigation.PaneId TargetPane);

    /// <summary>
    /// 请求高亮库通知
    /// </summary>
    public record LibraryHighlightRequestedMessage(Library Library);
}
