using System;
using System.Collections.Generic;
using System.Linq;
using YiboFile;
using YiboFile.Models.Config;

namespace YiboFile.Services.Config
{
    /// <summary>
    /// Helper class to map between monolithic AppConfig and split UserSettings/AppState.
    /// 重构后直接使用 Panes[] 数组映射，消除 _Secondary 概念。
    /// </summary>
    public static class ConfigMapper
    {
        public static void MapToModels(AppConfig source, UserSettings settings, AppState state)
        {
            if (source == null) return;

            // --- UserSettings ---
            // Appearance
            settings.Appearance.Language = source.Language;
            settings.Appearance.ThemeMode = source.ThemeMode;
            settings.Appearance.WindowOpacity = source.WindowOpacity;
            settings.Appearance.AnimationsEnabled = source.AnimationsEnabled;
            settings.Appearance.IconStyle = source.IconStyle;
            settings.Appearance.UIStyle = source.UIStyle;

            // Behavior
            settings.Behavior.ReuseTabTimeWindow = source.ReuseTabTimeWindow;
            settings.Behavior.AlwaysReuseTab = source.AlwaysReuseTab;
            settings.Behavior.NeverReuseTab = source.NeverReuseTab;
            settings.Behavior.ActivateNewTabOnMiddleClick = source.ActivateNewTabOnMiddleClick;
            settings.Behavior.EnableMultiWindow = source.EnableMultiWindow;
            settings.Behavior.TabWidthStrategy = source.TabWidthStrategy;
            settings.Behavior.TabOverflowStrategy = source.TabOverflowStrategy;
            settings.Behavior.TabFixedWidth = source.TabFixedWidth;
            settings.Behavior.TabMaxWidth = source.TabMaxWidth;
            settings.Behavior.TabMinWidth = source.TabMinWidth;
            settings.Behavior.HideCloseButtonOnInactive = source.HideCloseButtonOnInactive;
            settings.Behavior.ShowOverflowArrows = source.ShowOverflowArrows;
            settings.Behavior.ShowOverflowGradient = source.ShowOverflowGradient;
            #pragma warning disable CS0612
            settings.Behavior.TabWidthMode = source.TabWidthMode;
            settings.Behavior.PinnedTabWidth = source.PinnedTabWidth;
            #pragma warning restore CS0612

            // Fonts
            settings.Fonts.UIFontSize = source.UIFontSize;
            settings.Fonts.TagFontSize = source.TagFontSize;
            settings.Fonts.TagBoxWidth = source.TagBoxWidth;
            settings.Fonts.TagWidth = source.TagWidth;

            // Search
            settings.Search.IsEnableFullTextSearch = source.IsEnableFullTextSearch;
            settings.Search.FullTextIndexPaths = new List<string>(source.FullTextIndexPaths ?? new List<string>());
            settings.Search.HistoryMaxCount = source.HistoryMaxCount;
            settings.Search.AutoExpandHistory = source.AutoExpandHistory;

            // Hotkeys
            settings.Hotkeys.CustomHotkeys = new Dictionary<string, string>(source.CustomHotkeys ?? new Dictionary<string, string>());

            // Navigation
            settings.Navigation.SectionsOrder = new List<string>(source.NavigationSectionsOrder ?? new List<string>());
            settings.Navigation.RailTopItems = new List<string>(source.RailTopItems ?? new List<string>());
            settings.Navigation.RailBottomItems = new List<string>(source.RailBottomItems ?? new List<string>());

            // Backup
            settings.Backup.BackupDirectory = source.BackupDirectory;
            settings.Backup.RetentionDays = source.BackupRetentionDays;

            // Tabs
            settings.Tabs.PinnedTabs = new List<string>(source.PinnedTabs ?? new List<string>());
            settings.Tabs.TitleOverrides = new Dictionary<string, string>(source.TabTitleOverrides ?? new Dictionary<string, string>());

            // --- AppState ---
            // Window
            state.Window.Width = source.WindowWidth;
            state.Window.Height = source.WindowHeight;
            state.Window.Top = source.WindowTop;
            state.Window.Left = source.WindowLeft;
            state.Window.IsMaximized = source.IsMaximized;

            // Layout
            state.Layout.LayoutMode = source.LayoutMode;
            state.Layout.ColLeftWidth = source.ColLeftWidth;
            state.Layout.ColCenterWidth = source.ColCenterWidth;
            state.Layout.ColRightWidth = source.ColRightWidth;
            state.Layout.IsSidebarCollapsed = source.IsSidebarCollapsed;
            state.Layout.IsPreviewCollapsed = source.IsPreviewCollapsed;
            state.Layout.IsRightPanelVisible = source.IsRightPanelVisible;
            state.Layout.IsDualPaneMode = source.IsDualPaneMode;
            state.Layout.PaneModeStr = source.PaneModeStr;
            state.Layout.RightPanelNotesHeight = source.RightPanelNotesHeight;
            state.Layout.CenterPanelInfoHeight = source.CenterPanelInfoHeight;
            state.Layout.SecondPanelInfoHeight = source.SecondPanelInfoHeight;

            // ── Pane A (Panes[0]) — 主面板列头 + 会话 ──
            EnsurePaneCount(state, 2);
            var paneA = state.Panes[0];
            paneA.Id = "A";
            MapColumnStateFromConfig(paneA.Columns, source.ColNameWidth, source.ColSizeWidth,
                source.ColModifiedDateWidth, source.ColCreatedTimeWidth, source.ColTypeWidth,
                source.ColTagsWidth, source.ColNotesWidth, source.ColumnOrder,
                source.VisibleColumns_Path, source.VisibleColumns_Library, source.VisibleColumns_Tag);

            paneA.Session.LastPath = source.LastPath;
            paneA.Session.LastNavigationMode = source.LastNavigationMode;
            paneA.Session.LastLibraryId = source.LastLibraryId;
            paneA.Session.OpenTabs = new List<string>(source.OpenTabs ?? new List<string>());
            paneA.Session.ActiveTabKey = source.ActiveTabKey;

            // ── Pane B (Panes[1]) — 副面板列头 + 会话 ──
            var paneB = state.Panes[1];
            paneB.Id = "B";
            MapColumnStateFromConfig(paneB.Columns, source.ColNameWidth_Secondary, source.ColSizeWidth_Secondary,
                source.ColModifiedDateWidth_Secondary, source.ColCreatedTimeWidth_Secondary, source.ColTypeWidth_Secondary,
                source.ColTagsWidth_Secondary, source.ColNotesWidth_Secondary, source.ColumnOrder_Secondary,
                source.VisibleColumns_Path_Secondary, source.VisibleColumns_Library_Secondary, source.VisibleColumns_Tag_Secondary);

            paneB.Session.OpenTabs = new List<string>(source.OpenTabsSecondary ?? new List<string>());
            paneB.Session.ActiveTabKey = source.ActiveTabKeySecondary;

            // View
            state.View.FileViewMode = source.FileViewMode;
            state.View.SortColumn = source.SortColumn;
            state.View.SortDirection = source.SortDirection;

            // Sidebar
            state.Sidebar.ExpanderStates = new Dictionary<string, bool>(source.SidebarExpanderStates ?? new Dictionary<string, bool>());

            // Misc
            state.Misc.BackupBrowserWidth = source.BackupBrowserWidth;
            state.Misc.BackupBrowserHeight = source.BackupBrowserHeight;
        }

        public static AppConfig MapToAppConfig(UserSettings settings, AppState state)
        {
            var config = new AppConfig();

            // Settings -> AppConfig
            config.Language = settings.Appearance.Language;
            config.ThemeMode = settings.Appearance.ThemeMode;
            config.WindowOpacity = settings.Appearance.WindowOpacity;
            config.AnimationsEnabled = settings.Appearance.AnimationsEnabled;
            config.IconStyle = settings.Appearance.IconStyle;
            config.UIStyle = settings.Appearance.UIStyle;

            config.ReuseTabTimeWindow = settings.Behavior.ReuseTabTimeWindow;
            config.AlwaysReuseTab = settings.Behavior.AlwaysReuseTab;
            config.NeverReuseTab = settings.Behavior.NeverReuseTab;
            config.ActivateNewTabOnMiddleClick = settings.Behavior.ActivateNewTabOnMiddleClick;
            config.EnableMultiWindow = settings.Behavior.EnableMultiWindow;
            config.TabWidthStrategy = settings.Behavior.TabWidthStrategy;
            config.TabOverflowStrategy = settings.Behavior.TabOverflowStrategy;
            config.TabFixedWidth = settings.Behavior.TabFixedWidth;
            config.TabMaxWidth = settings.Behavior.TabMaxWidth;
            config.TabMinWidth = settings.Behavior.TabMinWidth;
            config.HideCloseButtonOnInactive = settings.Behavior.HideCloseButtonOnInactive;
            config.ShowOverflowArrows = settings.Behavior.ShowOverflowArrows;
            config.ShowOverflowGradient = settings.Behavior.ShowOverflowGradient;
            #pragma warning disable CS0612
            config.TabWidthMode = settings.Behavior.TabWidthMode;
            config.PinnedTabWidth = settings.Behavior.PinnedTabWidth;
            #pragma warning restore CS0612

            config.UIFontSize = settings.Fonts.UIFontSize;
            config.TagFontSize = settings.Fonts.TagFontSize;
            config.TagBoxWidth = settings.Fonts.TagBoxWidth;
            config.TagWidth = settings.Fonts.TagWidth;

            config.IsEnableFullTextSearch = settings.Search.IsEnableFullTextSearch;
            config.FullTextIndexPaths = new List<string>(settings.Search.FullTextIndexPaths);
            config.HistoryMaxCount = settings.Search.HistoryMaxCount;
            config.AutoExpandHistory = settings.Search.AutoExpandHistory;

            config.CustomHotkeys = new Dictionary<string, string>(settings.Hotkeys.CustomHotkeys);
            config.NavigationSectionsOrder = new List<string>(settings.Navigation.SectionsOrder);
            config.RailTopItems = new List<string>(settings.Navigation.RailTopItems);
            config.RailBottomItems = new List<string>(settings.Navigation.RailBottomItems);

            config.BackupDirectory = settings.Backup.BackupDirectory;
            config.BackupRetentionDays = settings.Backup.RetentionDays;

            config.PinnedTabs = new List<string>(settings.Tabs.PinnedTabs);
            config.TabTitleOverrides = new Dictionary<string, string>(settings.Tabs.TitleOverrides);

            // State -> AppConfig
            config.WindowWidth = state.Window.Width;
            config.WindowHeight = state.Window.Height;
            config.WindowTop = state.Window.Top;
            config.WindowLeft = state.Window.Left;
            config.IsMaximized = state.Window.IsMaximized;

            config.ColLeftWidth = state.Layout.ColLeftWidth;
            config.LayoutMode = state.Layout.LayoutMode;
            config.ColCenterWidth = state.Layout.ColCenterWidth;
            config.ColRightWidth = state.Layout.ColRightWidth;
            config.IsSidebarCollapsed = state.Layout.IsSidebarCollapsed;
            config.IsPreviewCollapsed = state.Layout.IsPreviewCollapsed;
            config.IsRightPanelVisible = state.Layout.IsRightPanelVisible;
            config.IsDualPaneMode = state.Layout.IsDualPaneMode;
            config.PaneModeStr = state.Layout.PaneModeStr;
            config.RightPanelNotesHeight = state.Layout.RightPanelNotesHeight;
            config.CenterPanelInfoHeight = state.Layout.CenterPanelInfoHeight;
            config.SecondPanelInfoHeight = state.Layout.SecondPanelInfoHeight;

            // ── Pane A → AppConfig 主面板字段 ──
            EnsurePaneCount(state, 2);
            var paneA = state.Panes[0];
            MapColumnStateToConfig(paneA.Columns,
                v => config.ColNameWidth = v, v => config.ColSizeWidth = v,
                v => config.ColModifiedDateWidth = v, v => config.ColCreatedTimeWidth = v,
                v => config.ColTypeWidth = v, v => config.ColTagsWidth = v, v => config.ColNotesWidth = v);
            config.ColumnOrder = paneA.Columns.ColumnOrder;
            if (paneA.Columns.VisibleColumns.ContainsKey("Path")) config.VisibleColumns_Path = paneA.Columns.VisibleColumns["Path"];
            if (paneA.Columns.VisibleColumns.ContainsKey("Library")) config.VisibleColumns_Library = paneA.Columns.VisibleColumns["Library"];
            if (paneA.Columns.VisibleColumns.ContainsKey("Tag")) config.VisibleColumns_Tag = paneA.Columns.VisibleColumns["Tag"];

            config.LastPath = paneA.Session.LastPath;
            config.LastNavigationMode = paneA.Session.LastNavigationMode;
            config.LastLibraryId = paneA.Session.LastLibraryId;
            config.OpenTabs = new List<string>(paneA.Session.OpenTabs);
            config.ActiveTabKey = paneA.Session.ActiveTabKey;

            // ── Pane B → AppConfig 副面板字段 ──
            var paneB = state.Panes[1];
            MapColumnStateToConfig(paneB.Columns,
                v => config.ColNameWidth_Secondary = v, v => config.ColSizeWidth_Secondary = v,
                v => config.ColModifiedDateWidth_Secondary = v, v => config.ColCreatedTimeWidth_Secondary = v,
                v => config.ColTypeWidth_Secondary = v, v => config.ColTagsWidth_Secondary = v, v => config.ColNotesWidth_Secondary = v);
            config.ColumnOrder_Secondary = paneB.Columns.ColumnOrder;
            if (paneB.Columns.VisibleColumns.ContainsKey("Path")) config.VisibleColumns_Path_Secondary = paneB.Columns.VisibleColumns["Path"];
            if (paneB.Columns.VisibleColumns.ContainsKey("Library")) config.VisibleColumns_Library_Secondary = paneB.Columns.VisibleColumns["Library"];
            if (paneB.Columns.VisibleColumns.ContainsKey("Tag")) config.VisibleColumns_Tag_Secondary = paneB.Columns.VisibleColumns["Tag"];

            config.OpenTabsSecondary = new List<string>(paneB.Session.OpenTabs);
            config.ActiveTabKeySecondary = paneB.Session.ActiveTabKey;

            // View
            config.FileViewMode = state.View.FileViewMode;
            config.SortColumn = state.View.SortColumn;
            config.SortDirection = state.View.SortDirection;

            // Sidebar
            config.SidebarExpanderStates = new Dictionary<string, bool>(state.Sidebar.ExpanderStates);

            // Misc
            config.BackupBrowserWidth = state.Misc.BackupBrowserWidth;
            config.BackupBrowserHeight = state.Misc.BackupBrowserHeight;

            return config;
        }

        // ── 辅助方法 ──

        private static void EnsurePaneCount(AppState state, int count)
        {
            while (state.Panes.Count < count)
            {
                state.Panes.Add(new PaneState { Id = state.Panes.Count == 0 ? "A" : "B" });
            }
        }

        private static void MapColumnStateFromConfig(ColumnState cs,
            double nameW, double sizeW, double modifiedW, double createdW,
            double typeW, double tagsW, double notesW, string order,
            string visPath, string visLib, string visTag)
        {
            cs.ColNameWidth = nameW;
            cs.ColSizeWidth = sizeW;
            cs.ColModifiedDateWidth = modifiedW;
            cs.ColCreatedTimeWidth = createdW;
            cs.ColTypeWidth = typeW;
            cs.ColTagsWidth = tagsW;
            cs.ColNotesWidth = notesW;
            cs.ColumnOrder = order;
            cs.VisibleColumns["Path"] = visPath;
            cs.VisibleColumns["Library"] = visLib;
            cs.VisibleColumns["Tag"] = visTag;
        }

        private static void MapColumnStateToConfig(ColumnState cs,
            Action<double> setName, Action<double> setSize,
            Action<double> setModified, Action<double> setCreated,
            Action<double> setType, Action<double> setTags, Action<double> setNotes)
        {
            setName(cs.ColNameWidth);
            setSize(cs.ColSizeWidth);
            setModified(cs.ColModifiedDateWidth);
            setCreated(cs.ColCreatedTimeWidth);
            setType(cs.ColTypeWidth);
            setTags(cs.ColTagsWidth);
            setNotes(cs.ColNotesWidth);
        }
    }
}
