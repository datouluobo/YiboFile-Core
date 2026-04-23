using System;
using System.Collections.Generic;

namespace YiboFile.Models.Config
{
    public class AppState
    {
        public WindowState Window { get; set; } = new WindowState();
        public LayoutState Layout { get; set; } = new LayoutState();

        /// <summary>
        /// 面板状态数组 — Pane A 和 Pane B 各拥有一份完全独立的状态。
        /// Panes[0] = Pane A（默认左侧），Panes[1] = Pane B（默认右侧）。
        /// </summary>
        public List<PaneState> Panes { get; set; } = new List<PaneState>
        {
            new PaneState { Id = "A" },
            new PaneState { Id = "B" }
        };

        /// <summary>面板位置顺序 [0]=左侧面板索引, [1]=右侧面板索引</summary>
        public List<int> PaneOrder { get; set; } = new List<int> { 0, 1 };

        /// <summary>当前活动面板索引</summary>
        public int ActivePaneIndex { get; set; } = 0;

        // ---- 以下为全局共享状态（不随面板切换） ----

        public ViewState View { get; set; } = new ViewState();
        public SidebarState Sidebar { get; set; } = new SidebarState();
        public MiscState Misc { get; set; } = new MiscState();

        // ---- 兼容性辅助属性（便于渐进式迁移期间代码访问） ----

        /// <summary>[兼容] 主面板列头状态</summary>
        [Obsolete("使用 Panes[0].Columns 替代")]
        public ColumnState Columns
        {
            get => Panes.Count > 0 ? Panes[0].Columns : new ColumnState();
            set { if (Panes.Count > 0) Panes[0].Columns = value; }
        }

        /// <summary>[兼容] 副面板列头状态</summary>
        [Obsolete("使用 Panes[1].Columns 替代")]
        public ColumnState ColumnsSecondary
        {
            get => Panes.Count > 1 ? Panes[1].Columns : new ColumnState();
            set { if (Panes.Count > 1) Panes[1].Columns = value; }
        }

        /// <summary>[兼容] 会话状态 — 映射到 Pane A 的 Session</summary>
        [Obsolete("使用 Panes[0].Session 替代")]
        public SessionState Session
        {
            get
            {
                var ps = Panes.Count > 0 ? Panes[0].Session : new PaneSessionState();
                return new SessionState
                {
                    LastPath = ps.LastPath,
                    LastNavigationMode = ps.LastNavigationMode,
                    LastLibraryId = ps.LastLibraryId,
                    OpenTabs = ps.OpenTabs,
                    ActiveTabKey = ps.ActiveTabKey,
                    TabViewModes = ps.TabViewModes,
                    OpenTabsSecondary = Panes.Count > 1 ? Panes[1].Session.OpenTabs : new List<string>(),
                    ActiveTabKeySecondary = Panes.Count > 1 ? Panes[1].Session.ActiveTabKey : string.Empty,
                    TabViewModes_Secondary = Panes.Count > 1 ? Panes[1].Session.TabViewModes : new Dictionary<string, string>()
                };
            }
            set
            {
                if (value == null) return;
                if (Panes.Count > 0)
                {
                    Panes[0].Session.LastPath = value.LastPath;
                    Panes[0].Session.LastNavigationMode = value.LastNavigationMode;
                    Panes[0].Session.LastLibraryId = value.LastLibraryId;
                    Panes[0].Session.OpenTabs = value.OpenTabs;
                    Panes[0].Session.ActiveTabKey = value.ActiveTabKey;
                    Panes[0].Session.TabViewModes = value.TabViewModes ?? new Dictionary<string, string>();
                }
                if (Panes.Count > 1)
                {
                    Panes[1].Session.OpenTabs = value.OpenTabsSecondary;
                    Panes[1].Session.ActiveTabKey = value.ActiveTabKeySecondary;
                    Panes[1].Session.TabViewModes = value.TabViewModes_Secondary ?? new Dictionary<string, string>();
                }
            }
        }
    }

    public class WindowState
    {
        public double Width { get; set; } = 1200;
        public double Height { get; set; } = 800;
        public double? Top { get; set; } = null;
        public double? Left { get; set; } = null;
        public bool IsMaximized { get; set; } = true;
    }

    public class LayoutState
    {
        public string LayoutMode { get; set; } = "Full"; // Focus, Work, Full
        public double ColLeftWidth { get; set; } = 220;
        public double ColCenterWidth { get; set; } = 0; // 0 = star
        public double ColRightWidth { get; set; } = 360;
        public bool IsSidebarCollapsed { get; set; } = false;
        public bool IsPreviewCollapsed { get; set; } = true;
        public bool IsRightPanelVisible { get; set; } = true;
        public bool IsDualPaneMode { get; set; } = false;
        public string PaneModeStr { get; set; } = "Single"; // 新增：三态模式
        public double RightPanelNotesHeight { get; set; } = 200;
        public double CenterPanelInfoHeight { get; set; } = 180;
        public double SecondPanelInfoHeight { get; set; } = 180;
    }

    public class ColumnState
    {
        public double ColNameWidth { get; set; } = 200;
        public double ColSizeWidth { get; set; } = 100;
        public double ColModifiedDateWidth { get; set; } = 150;
        public double ColCreatedTimeWidth { get; set; } = 50;
        public double ColTypeWidth { get; set; } = 100;
        public double ColTagsWidth { get; set; } = 150;
        public double ColNotesWidth { get; set; } = 200;

        public string ColumnOrder { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";

        // Maps mode (Path, Library, etc) to visible columns string
        public Dictionary<string, string> VisibleColumns { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>[兼容] 旧版会话状态，方便渐进迁移</summary>
    public class SessionState
    {
        public string LastPath { get; set; } = string.Empty;
        public string LastNavigationMode { get; set; } = "Path";
        public int LastLibraryId { get; set; } = 0;

        public List<string> OpenTabs { get; set; } = new List<string>();
        public string ActiveTabKey { get; set; } = string.Empty;
        public Dictionary<string, string> TabViewModes { get; set; } = new Dictionary<string, string>();

        [Obsolete("使用 Panes[1].Session 替代")]
        public List<string> OpenTabsSecondary { get; set; } = new List<string>();
        [Obsolete("使用 Panes[1].Session 替代")]
        public string ActiveTabKeySecondary { get; set; } = string.Empty;
        [Obsolete("使用 Panes[1].Session 替代")]
        public Dictionary<string, string> TabViewModes_Secondary { get; set; } = new Dictionary<string, string>();
    }

    public class ViewState
    {
        public YiboFile.Models.Enums.FileListViewMode FileViewMode { get; set; } = YiboFile.Models.Enums.FileListViewMode.List;
        public string SortColumn { get; set; } = "Name";
        public string SortDirection { get; set; } = "Ascending";
    }

    public class SidebarState
    {
        public Dictionary<string, bool> ExpanderStates { get; set; } = new Dictionary<string, bool>();
    }

    public class MiscState
    {
        public double BackupBrowserWidth { get; set; } = 1000;
        public double BackupBrowserHeight { get; set; } = 650;
    }
}
