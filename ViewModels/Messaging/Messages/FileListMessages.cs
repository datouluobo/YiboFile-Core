using System.Collections;
using YiboFile.Models;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 文件列表相关消息
    /// </summary>

    /// <summary>
    /// 请求刷新文件列表
    /// </summary>
    public record RefreshFileListMessage(string Path = null, YiboFile.Services.Navigation.PaneId Pane = YiboFile.Services.Navigation.PaneId.Main);

    /// <summary>
    /// 文件列表内容已加载通知 (包含项)
    /// </summary>
    public record FileListItemsLoadedMessage(string Path, System.Collections.Generic.List<YiboFile.Models.FileSystemItem> Items, YiboFile.Services.Navigation.PaneId Pane);

    /// <summary>
    /// 文件列表元数据已增强通知
    /// </summary>
    public record FileListMetadataEnrichedMessage(System.Collections.Generic.List<YiboFile.Models.FileSystemItem> Items, YiboFile.Services.Navigation.PaneId Pane);

    /// <summary>
    /// 文件选择变更通知
    /// </summary>
    public record FileSelectionChangedMessage(IList SelectedItems, bool RequestPreview = true, YiboFile.Services.Navigation.PaneId Pane = YiboFile.Services.Navigation.PaneId.Main);

    /// <summary>
    /// 请求清除过滤器
    /// </summary>
    public record ClearFilterMessage();

    /// <summary>
    /// 视图模式变更
    /// </summary>
    public record ViewModeChangedMessage(string Mode, YiboFile.Services.Navigation.PaneId TargetPane = YiboFile.Services.Navigation.PaneId.Main);
    /// <summary>
    /// 请求在信息面板显示文件信息
    /// </summary>
    /// <summary>
    /// 请求在信息面板显示文件信息
    /// </summary>
    public record ShowFileInfoMessage(YiboFile.Models.FileSystemItem Item, YiboFile.Services.Navigation.PaneId Pane = YiboFile.Services.Navigation.PaneId.Main);

    /// <summary>
    /// 请求在信息面板显示库信息
    /// </summary>
    public record ShowLibraryInfoMessage(YiboFile.Library Library, YiboFile.Services.Navigation.PaneId Pane = YiboFile.Services.Navigation.PaneId.Main);

    /// <summary>
    /// 文件系统发生变更通知
    /// </summary>
    public record FileSystemChangedMessage(string Path, string ChangeType);

    /// <summary>
    /// 文件夹大小计算完成通知
    /// </summary>
    public record FolderSizeCalculatedMessage(string Path, long Size, string FormattedSize);

    /// <summary>
    /// 文件元数据处理完成通知
    /// </summary>
    public record MetadataEnrichedMessage(YiboFile.Models.FileSystemItem Item);
}
