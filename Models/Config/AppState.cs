using System;
using System.Collections.Generic;

namespace YiboFile.Models.Config
{
    public class AppState
    {
        public WindowState Window { get; set; } = new WindowState();
        public LayoutState Layout { get; set; } = new LayoutState();
        public ColumnState Columns { get; set; } = new ColumnState();
        public ColumnState ColumnsSecondary { get; set; } = new ColumnState();
        public SessionState Session { get; set; } = new SessionState();
        public ViewState View { get; set; } = new ViewState();
        public SidebarState Sidebar { get; set; } = new SidebarState();
        public MiscState Misc { get; set; } = new MiscState();
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
        public bool IsDualListMode { get; set; } = false;
        public double RightPanelNotesHeight { get; set; } = 200;
        public double CenterPanelInfoHeight { get; set; } = 180;
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

        public string Order { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";

        // Maps mode (Path, Library, etc) to visible columns string
        public Dictionary<string, string> VisibleColumns { get; set; } = new Dictionary<string, string>();
    }

    public class SessionState
    {
        public string LastPath { get; set; } = string.Empty;
        public string LastNavigationMode { get; set; } = "Path"; // Path, Library, Tag, Search
        public int LastLibraryId { get; set; } = 0;

        public List<string> OpenTabs { get; set; } = new List<string>();
        public string ActiveTabKey { get; set; } = string.Empty;

        public List<string> OpenTabsSecondary { get; set; } = new List<string>();
        public string ActiveTabKeySecondary { get; set; } = string.Empty;
    }

    public class ViewState
    {
        public YiboFile.Models.Enums.FileListViewMode FileViewMode { get; set; } = YiboFile.Models.Enums.FileListViewMode.List; // List, Thumbnail
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
