using System.Collections.Generic;

namespace YiboFile.Models.Config
{
    /// <summary>
    /// 面板状态容器 — 每个 BrowserPane 实例拥有一份独立的 PaneState。
    /// 完全自包含：列头、标签页、导航、预览的状态均在此处管理。
    /// </summary>
    public class PaneState
    {
        /// <summary>
        /// 面板标识 ("A" / "B")
        /// </summary>
        public string Id { get; set; } = "A";

        /// <summary>列头状态</summary>
        public ColumnState Columns { get; set; } = new ColumnState();

        /// <summary>标签页状态</summary>
        public PaneSessionState Session { get; set; } = new PaneSessionState();

        /// <summary>预览附件状态</summary>
        public PreviewState Preview { get; set; } = new PreviewState();
    }

    /// <summary>
    /// 面板专属会话状态（标签页、最后路径等）
    /// </summary>
    public class PaneSessionState
    {
        public string LastPath { get; set; } = string.Empty;
        public string LastNavigationMode { get; set; } = "Path";
        public int LastLibraryId { get; set; } = 0;
        public List<string> OpenTabs { get; set; } = new List<string>();
        public string ActiveTabKey { get; set; } = string.Empty;
        /// <summary>每标签页视图模式映射 (tabKey → ViewMode 枚举名)</summary>
        public Dictionary<string, string> TabViewModes { get; set; } = new Dictionary<string, string>();
        public YiboFile.Models.Enums.FileListViewMode FileViewMode { get; set; } = YiboFile.Models.Enums.FileListViewMode.List;
        public string SortColumn { get; set; } = "Name";
        public string SortDirection { get; set; } = "Ascending";
    }

    /// <summary>
    /// 面板预览附件状态（每个面板可独立配置）
    /// </summary>
    public class PreviewState
    {
        public bool Enabled { get; set; } = false;
        public string Mode { get; set; } = "Bottom"; // Bottom, Side, None
        public double Height { get; set; } = 250;
        public double Width { get; set; } = 300;
    }
}
