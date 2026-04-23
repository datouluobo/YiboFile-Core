using YiboFile.Services.Navigation;
using YiboFile.Models;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 分割器拖拽结束消息
    /// </summary>
    public class SplitterDragCompletedMessage
    {
        /// <summary>
        /// 分割器标识 (例如 "Left", "Right")
        /// </summary>
        public string SplitterName { get; set; }

        /// <summary>
        /// 变更后的宽度/高度
        /// </summary>
        public double NewValue { get; set; }
    }

    /// <summary>
    /// 列宽变更消息
    /// </summary>
    public class ColumnWidthChangedMessage
    {
        public string ColumnName { get; set; }
        public double NewWidth { get; set; }
        public PaneId TargetPane { get; set; }
    }

    /// <summary>
    /// GridView 列头被点击消息 (用于排序触发)
    /// </summary>
    public class GridViewColumnHeaderClickedMessage
    {
        public System.Windows.Controls.GridViewColumnHeader Header { get; set; }
        public PaneId TargetPane { get; set; }
    }

    /// <summary>
    /// 导航栏加载完成消息
    /// </summary>
    public class NavigationRailLoadedMessage
    {
    }


    /// <summary>
    /// 备注区高度变更消息
    /// </summary>
    public class NotesHeightChangedMessage
    {
        public double NewHeight { get; set; }
    }

    /// <summary>
    /// 信息栏高度变更消息
    /// </summary>
    public class InfoHeightChangedMessage
    {
        public double NewHeight { get; set; }
        public PaneId TargetPane { get; set; }
    }

    /// <summary>
    /// 列表选择项变更消息
    /// </summary>
    public class SelectionChangedMessage
    {
        public System.Collections.IList SelectedItems { get; set; }
        public PaneId TargetPane { get; set; }
    }

    public class TagClickedMessage
    {
        public TagViewModel Tag { get; set; }
        public PaneId TargetPane { get; set; }
    }
}
