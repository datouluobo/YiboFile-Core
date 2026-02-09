using System.Collections;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 文件列表相关消息
    /// </summary>

    /// <summary>
    /// 请求刷新文件列表
    /// </summary>
    public record RefreshFileListMessage(string Path = null);

    /// <summary>
    /// 文件列表已加载通知
    /// </summary>
    public record FileListLoadedMessage(string Path, int FileCount);

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
}
