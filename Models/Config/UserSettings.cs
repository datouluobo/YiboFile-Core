using System;
using System.Collections.Generic;
using YiboFile;

namespace YiboFile.Models.Config
{
    public class UserSettings
    {
        public AppearanceSettings Appearance { get; set; } = new AppearanceSettings();
        public BehaviorSettings Behavior { get; set; } = new BehaviorSettings();
        public FontSettings Fonts { get; set; } = new FontSettings();
        public SearchSettings Search { get; set; } = new SearchSettings();
        public HotkeySettings Hotkeys { get; set; } = new HotkeySettings();
        public NavigationSettings Navigation { get; set; } = new NavigationSettings();
        public BackupSettings Backup { get; set; } = new BackupSettings();
        public TabSettings Tabs { get; set; } = new TabSettings();
    }

    public class AppearanceSettings
    {
        public string ThemeMode { get; set; } = "FollowSystem"; // Light, Dark, FollowSystem
        public double WindowOpacity { get; set; } = 1.0;
        public bool AnimationsEnabled { get; set; } = true;
        public string IconStyle { get; set; } = "Emoji"; // Emoji, Remix, Fluent
        public string CustomThemeId { get; set; } = null;
    }

    public class BehaviorSettings
    {
        public int ReuseTabTimeWindow { get; set; } = 10; // seconds
        public bool AlwaysReuseTab { get; set; } = false;
        public bool NeverReuseTab { get; set; } = false;
        public bool ActivateNewTabOnMiddleClick { get; set; } = true;
        public bool EnableMultiWindow { get; set; } = true;
        public TabWidthMode TabWidthMode { get; set; } = TabWidthMode.FixedWidth;
        public double PinnedTabWidth { get; set; } = 120;
    }

    public class FontSettings
    {
        public double UIFontSize { get; set; } = 16;
        public double TagFontSize { get; set; } = 16;
        public double TagBoxWidth { get; set; } = 0;
        public double TagWidth { get; set; } = 120;
    }

    public class SearchSettings
    {
        public bool IsEnableFullTextSearch { get; set; } = true;
        public List<string> FullTextIndexPaths { get; set; } = new List<string>();
        public int HistoryMaxCount { get; set; } = 20;
        public bool AutoExpandHistory { get; set; } = false;
    }

    public class HotkeySettings
    {
        public Dictionary<string, string> CustomHotkeys { get; set; } = new Dictionary<string, string>();
    }

    public class NavigationSettings
    {
        public List<string> SectionsOrder { get; set; } = new List<string>
        {
            "QuickAccess",
            "Drives",
            "FolderFavorites",
            "FileFavorites",
            "Libraries",
            "Tags"
        };
    }

    public class BackupSettings
    {
        public string BackupDirectory { get; set; } = string.Empty;
        public int RetentionDays { get; set; } = 30;
    }

    public class TabSettings
    {
        public List<string> PinnedTabs { get; set; } = new List<string>();
        public Dictionary<string, string> TitleOverrides { get; set; } = new Dictionary<string, string>();
    }
}
