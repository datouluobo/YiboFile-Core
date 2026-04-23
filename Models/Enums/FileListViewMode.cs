namespace YiboFile.Models.Enums
{
    /// <summary>
    /// 文件列表的显示视图模式，取代原有的魔术字符串。
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public enum FileListViewMode
    {
        /// <summary>
        /// 列表视图 (详细信息)，最详细的层级
        /// </summary>
        List,

        /// <summary>
        /// 大图标 / 缩略图模式
        /// </summary>
        Thumbnail,

        /// <summary>
        /// 平铺视图 (中等图标加详细信息)
        /// </summary>
        Tiles,

        /// <summary>
        /// 小图标视图
        /// </summary>
        SmallIcons,

        /// <summary>
        /// 内容视图 (单行横向延伸)
        /// </summary>
        Content,

        /// <summary>
        /// 紧凑视图
        /// </summary>
        Compact
    }
}
