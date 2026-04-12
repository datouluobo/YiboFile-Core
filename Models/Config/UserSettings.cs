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
        public string Language { get; set; } = "Auto";
        public string ThemeMode { get; set; } = "FollowSystem"; // Light, Dark, FollowSystem
        public string UIStyle { get; set; } = "Original"; // Original, Fluent, MacOS, Geek
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

        // ── 标签页行为设置（新，正交维度） ──
        public TabWidthStrategy TabWidthStrategy { get; set; } = TabWidthStrategy.Adaptive;
        public TabOverflowStrategy TabOverflowStrategy { get; set; } = TabOverflowStrategy.Scroll;
        public double TabFixedWidth { get; set; } = 140;
        public double TabMaxWidth { get; set; } = 200;
        public double TabMinWidth { get; set; } = 50;
        public bool HideCloseButtonOnInactive { get; set; } = true;
        public bool ShowOverflowArrows { get; set; } = true;
        public bool ShowOverflowGradient { get; set; } = true;

        /// <summary>新建标签页的行为</summary>
        public NewTabAction NewTabAction { get; set; } = NewTabAction.Desktop;

        // ── 旧字段（向后兼容） ──
        [System.Obsolete("使用新的正交维度字段替代")]
        public TabWidthMode TabWidthMode { get; set; } = TabWidthMode.FixedWidth;
        [System.Obsolete("使用 TabFixedWidth 替代")]
        public double PinnedTabWidth { get; set; } = 120;

        // ── 系统右键菜单集成设置（方案 C+） ──

        /// <summary>
        /// 系统右键菜单集成方案
        /// "Native": YiboFile 原生菜单（提供现代化 UI，底部保留全量系统入口）
        /// "System": Windows 系统菜单（直接接管右键点击，弹出全量 Win32 原生菜单）
        /// </summary>
        public string ShellMenuMode { get; set; } = "System";

        /// <summary>
        /// 重命名时点击空白区域的行为
        /// "Commit": 接受命名（默认，与 Windows 资源管理器一致）
        /// "Cancel": 取消命名
        /// </summary>
        public string RenameLostFocusBehavior { get; set; } = "Commit";

        /// <summary>
        /// 用户固定到主菜单的 Shell 命令标识列表 (UniqueKey)
        /// </summary>
        public List<string> PinnedShellCommands { get; set; } = new List<string>();

        /// <summary>
        /// 用户隐藏的 Shell 命令标识列表
        /// </summary>
        public List<string> HiddenShellCommands { get; set; } = new List<string>();
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
        
        public List<string> RailTopItems { get; set; } = new List<string> { "Path", "Library", "Tag", "Tasks", "Backup", "Clipboard" };
        public List<string> RailBottomItems { get; set; } = new List<string> { "Focus", "Work", "Full", "DualPane", "Settings", "About" };
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
