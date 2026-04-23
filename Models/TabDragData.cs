using YiboFile.Services.Tabs;

namespace YiboFile.Models
{
    /// <summary>
    /// 标签页跨栏拖拽的载荷数据格式
    /// </summary>
    public class TabDragData
    {
        /// <summary>
        /// 被拖拽的标签页实例
        /// </summary>
        public PathTab Tab { get; set; }

        /// <summary>
        /// 拖拽起点所在的 TabService，用于跨栏操作时从源移除或与源对比
        /// </summary>
        public TabService SourceService { get; set; }

        /// <summary>
        /// 源标签页是否固定
        /// </summary>
        public bool IsPinned { get; set; }
    }

    public static class TabDragDropFormats
    {
        public const string TabData = "YiboFile_TabDragData";
    }
}
