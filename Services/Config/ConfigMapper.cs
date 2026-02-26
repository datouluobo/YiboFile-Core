using System;
using System.Collections.Generic;
using System.Linq;
using YiboFile;
using YiboFile.Models.Config;

namespace YiboFile.Services.Config
{
    /// <summary>
    /// Helper class to map between monolithic AppConfig and split UserSettings/AppState.
    /// </summary>
    public static class ConfigMapper
    {
        public static void MapToModels(AppConfig source, UserSettings settings, AppState state)
        {
            if (source == null) return;

            // --- UserSettings ---
            // Appearance
            settings.Appearance.ThemeMode = source.ThemeMode;
            settings.Appearance.WindowOpacity = source.WindowOpacity;
            settings.Appearance.AnimationsEnabled = source.AnimationsEnabled;
            settings.Appearance.IconStyle = source.IconStyle;
            settings.Appearance.UIStyle = source.UIStyle;
            // CustomThemeId not explicitly in AppConfig, handle if needed or assume null

            // Behavior
            settings.Behavior.ReuseTabTimeWindow = source.ReuseTabTimeWindow;
            settings.Behavior.AlwaysReuseTab = source.AlwaysReuseTab;
            settings.Behavior.NeverReuseTab = source.NeverReuseTab;
            settings.Behavior.ActivateNewTabOnMiddleClick = source.ActivateNewTabOnMiddleClick;
            settings.Behavior.EnableMultiWindow = source.EnableMultiWindow;
            settings.Behavior.TabWidthMode = source.TabWidthMode;
            settings.Behavior.PinnedTabWidth = source.PinnedTabWidth;

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
            state.Layout.IsDualListMode = source.IsDualListMode;
            state.Layout.RightPanelNotesHeight = source.RightPanelNotesHeight;
            state.Layout.CenterPanelInfoHeight = source.CenterPanelInfoHeight;

            // Columns
            state.Columns.ColNameWidth = source.ColNameWidth;
            state.Columns.ColSizeWidth = source.ColSizeWidth;
            state.Columns.ColModifiedDateWidth = source.ColModifiedDateWidth;
            state.Columns.ColCreatedTimeWidth = source.ColCreatedTimeWidth;
            state.Columns.ColTypeWidth = source.ColTypeWidth;
            state.Columns.ColTagsWidth = source.ColTagsWidth;
            state.Columns.ColNotesWidth = source.ColNotesWidth;
            state.Columns.Order = source.ColumnOrder;
            state.Columns.VisibleColumns["Path"] = source.VisibleColumns_Path;
            state.Columns.VisibleColumns["Library"] = source.VisibleColumns_Library;
            state.Columns.VisibleColumns["Tag"] = source.VisibleColumns_Tag;

            // Session
            state.Session.LastPath = source.LastPath;
            state.Session.LastNavigationMode = source.LastNavigationMode;
            state.Session.LastLibraryId = source.LastLibraryId;
            state.Session.OpenTabs = new List<string>(source.OpenTabs ?? new List<string>());
            state.Session.ActiveTabKey = source.ActiveTabKey;
            state.Session.OpenTabsSecondary = new List<string>(source.OpenTabsSecondary ?? new List<string>());
            state.Session.ActiveTabKeySecondary = source.ActiveTabKeySecondary;

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
            config.TabWidthMode = settings.Behavior.TabWidthMode;
            config.PinnedTabWidth = settings.Behavior.PinnedTabWidth;

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
            config.IsDualListMode = state.Layout.IsDualListMode;
            config.RightPanelNotesHeight = state.Layout.RightPanelNotesHeight;
            config.CenterPanelInfoHeight = state.Layout.CenterPanelInfoHeight;

            config.ColNameWidth = state.Columns.ColNameWidth;
            config.ColSizeWidth = state.Columns.ColSizeWidth;
            config.ColModifiedDateWidth = state.Columns.ColModifiedDateWidth;
            config.ColCreatedTimeWidth = state.Columns.ColCreatedTimeWidth;
            config.ColTypeWidth = state.Columns.ColTypeWidth;
            config.ColTagsWidth = state.Columns.ColTagsWidth;
            config.ColNotesWidth = state.Columns.ColNotesWidth;
            config.ColumnOrder = state.Columns.Order;

            if (state.Columns.VisibleColumns.ContainsKey("Path")) config.VisibleColumns_Path = state.Columns.VisibleColumns["Path"];
            if (state.Columns.VisibleColumns.ContainsKey("Library")) config.VisibleColumns_Library = state.Columns.VisibleColumns["Library"];
            if (state.Columns.VisibleColumns.ContainsKey("Tag")) config.VisibleColumns_Tag = state.Columns.VisibleColumns["Tag"];

            config.LastPath = state.Session.LastPath;
            config.LastNavigationMode = state.Session.LastNavigationMode;
            config.LastLibraryId = state.Session.LastLibraryId;
            config.OpenTabs = new List<string>(state.Session.OpenTabs);
            config.ActiveTabKey = state.Session.ActiveTabKey;
            config.OpenTabsSecondary = new List<string>(state.Session.OpenTabsSecondary);
            config.ActiveTabKeySecondary = state.Session.ActiveTabKeySecondary;

            config.FileViewMode = state.View.FileViewMode;
            config.SortColumn = state.View.SortColumn;
            config.SortDirection = state.View.SortDirection;

            config.SidebarExpanderStates = new Dictionary<string, bool>(state.Sidebar.ExpanderStates);

            config.BackupBrowserWidth = state.Misc.BackupBrowserWidth;
            config.BackupBrowserHeight = state.Misc.BackupBrowserHeight;

            return config;
        }
    }
}
