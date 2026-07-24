using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace YiboFile
{
    /// <summary>
    /// 标签页宽度模式（已弃用，向后兼容保留）
    /// </summary>
    [Obsolete("使用 TabWidthStrategy + TabOverflowStrategy 替代")]
    public enum TabWidthMode
    {
        FixedWidth,
        DynamicWidth
    }

    /// <summary>
    /// 标签页宽度策略：决定每个标签的宽度计算方式
    /// </summary>
    public enum TabWidthStrategy
    {
        /// <summary>所有标签使用相同的固定宽度</summary>
        Fixed,
        /// <summary>每个标签根据标题文本长度自适应宽度</summary>
        Adaptive,
        /// <summary>标签平分可用空间，始终填满一行</summary>
        Elastic
    }

    /// <summary>
    /// 标签页溢出策略：决定标签放不下时的处理方式
    /// </summary>
    public enum TabOverflowStrategy
    {
        /// <summary>标签保持宽度不变，超出时启用水平滚动</summary>
        Scroll,
        /// <summary>所有标签等比缩小，优先保持一行内全部可见</summary>
        Compress
    }

    /// <summary>
    /// 新建标签页的行为
    /// </summary>
    public enum NewTabAction
    {
        /// <summary>打开桌面目录</summary>
        Desktop,
        /// <summary>复制当前活动的标签页</summary>
        DuplicateCurrent
    }

    public class AppConfig
    {
        public string LastPath { get; set; } = string.Empty;
        public string LastNavigationMode { get; set; } = "Path"; // Path, Library, Tag, Search
        public int LastLibraryId { get; set; } = 0; // 最后选中的库ID
        // Removed TagTrainDataDirectory
        public double WindowWidth { get; set; } = 1200;
        public double WindowHeight { get; set; } = 800;
        public double? WindowTop { get; set; } = null;
        public double? WindowLeft { get; set; } = null;
        public bool IsMaximized { get; set; } = true;
        public string Theme { get; set; } = "Light"; // Light, Dark (保留兼容性)

        // 外观设置
        public string ThemeMode { get; set; } = "FollowSystem"; // Light, Dark, FollowSystem
        public string UIStyle { get; set; } = "Original"; // Original, Fluent, MacOS, Geek
        public string TabStyle { get; set; } = ""; // 标签页风格；空值时跟随 UIStyle 迁移
        public string Language { get; set; } = "Auto"; // 界面语言
        public string LayoutMode { get; set; } = "Full"; // Focus, Work, Full
        public bool IsDualPaneMode { get; set; } = false; // 双列表模式
        public string PaneModeStr { get; set; } = "Single"; // 三态面板模式: Single, DualPane, Preview
        public double WindowOpacity { get; set; } = 1.0; // 窗口透明度 (0.5-1.0)
        public bool AnimationsEnabled { get; set; } = true; // 动画效果启用
        public string IconStyle { get; set; } = "Emoji"; // 图标风格 (Emoji, Remix, Fluent)

        // 主布局列宽度（列1和列2）
        public double ColLeftWidth { get; set; } = 220; // 列1（左侧导航区）宽度
        public double ColCenterWidth { get; set; } = 0; // 列2（中间文件浏览器）宽度，0表示使用Star模式
        // 新增：列3（右侧预览区）宽度 - 默认360
        public double ColRightWidth { get; set; } = 360;

        // 兼容旧版本的属性名
        public double LeftPanelWidth { get => ColLeftWidth; set => ColLeftWidth = value; }
        public double MiddlePanelWidth { get => ColCenterWidth; set => ColCenterWidth = value; }
        public double RightPanelWidth { get => ColRightWidth; set => ColRightWidth = value; }

        // 列头宽度
        public double ColNameWidth { get; set; } = 200;
        public double ColSizeWidth { get; set; } = 100;
        public double ColTypeWidth { get; set; } = 100;
        public double ColModifiedDateWidth { get; set; } = 150;
        public double ColCreatedTimeWidth { get; set; } = 50;
        public double ColTagsWidth { get; set; } = 150;
        public double ColNotesWidth { get; set; } = 200;

        // 副列表专属列头宽度
        public double ColNameWidth_Secondary { get; set; } = 200;
        public double ColSizeWidth_Secondary { get; set; } = 100;
        public double ColTypeWidth_Secondary { get; set; } = 100;
        public double ColModifiedDateWidth_Secondary { get; set; } = 150;
        public double ColCreatedTimeWidth_Secondary { get; set; } = 50;
        public double ColTagsWidth_Secondary { get; set; } = 150;
        public double ColNotesWidth_Secondary { get; set; } = 200;

        // 列头顺序
        public string ColumnOrder { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";
        public string ColumnOrder_Secondary { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";

        // 按模式存储可见列（CSV）
        public string VisibleColumns_Path { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";
        public string VisibleColumns_Library { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";
        public string VisibleColumns_Tag { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";

        // 副列表按模式存储可见列
        public string VisibleColumns_Path_Secondary { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";
        public string VisibleColumns_Library_Secondary { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";
        public string VisibleColumns_Tag_Secondary { get; set; } = "Name,Size,Type,ModifiedDate,CreatedTime,Tags,Notes";

        public System.Collections.Generic.Dictionary<string, string> TabTitleOverrides { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
        public System.Collections.Generic.List<string> PinnedTabs { get; set; } = new System.Collections.Generic.List<string>();

        // ── 标签页行为设置（新，正交维度） ──
        public TabWidthStrategy TabWidthStrategy { get; set; } = TabWidthStrategy.Adaptive;
        public TabOverflowStrategy TabOverflowStrategy { get; set; } = TabOverflowStrategy.Scroll;
        public double TabFixedWidth { get; set; } = 140;       // Fixed 模式标签宽度
        public double TabMaxWidth { get; set; } = 200;          // Adaptive/Elastic 模式最大宽度
        public double TabMinWidth { get; set; } = 50;           // 所有模式最小宽度
        public bool HideCloseButtonOnInactive { get; set; } = true;
        public bool ShowOverflowArrows { get; set; } = true;
        public bool ShowOverflowGradient { get; set; } = true;

        // ── 旧字段（向后兼容，JSON 反序列化用） ──
        [Obsolete("使用 TabFixedWidth 替代")] public double PinnedTabWidth { get; set; } = 120;

        // 标签页状态保存（所有打开的标签页和活动标签页）
        public System.Collections.Generic.List<string> OpenTabs { get; set; } = new System.Collections.Generic.List<string>(); // 所有打开的标签页键值列表（按顺序）
        public string ActiveTabKey { get; set; } = string.Empty; // 活动标签页的键值

        // 副列表（双栏模式）标签页状态保存
        public System.Collections.Generic.List<string> OpenTabsSecondary { get; set; } = new System.Collections.Generic.List<string>();
        public string ActiveTabKeySecondary { get; set; } = string.Empty;

        // 每标签页视图模式映射 (tabKey → ViewMode 枚举名)
        public System.Collections.Generic.Dictionary<string, string> TabViewModes { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
        public System.Collections.Generic.Dictionary<string, string> TabViewModes_Secondary { get; set; } = new System.Collections.Generic.Dictionary<string, string>();

        // 字体设置
        public double UIFontSize { get; set; } = 16; // 界面字体大小（默认16）
        public double TagFontSize { get; set; } = 16; // Tag字体大小（默认16）
        public double TagBoxWidth { get; set; } = 0; // Tag框宽度（0表示自动计算，>0表示固定宽度）
        public double TagWidth { get; set; } = 120; // Tag框宽度（默认120）

        // 新增：持久化状态字段
        public bool IsRightPanelVisible { get; set; } = true; // 右侧面板可见性
        public double RightPanelNotesHeight { get; set; } = 200; // 右侧备注区高度
        public double CenterPanelInfoHeight { get; set; } = 180; // 中间底部详情区高度
        public double SecondPanelInfoHeight { get; set; } = 180; // 副列表底部详情区高度
        public YiboFile.Models.Enums.FileListViewMode FileViewMode { get; set; } = YiboFile.Models.Enums.FileListViewMode.List; // 视图模式
        public YiboFile.Models.Enums.FileListViewMode FileViewMode_Secondary { get; set; } = YiboFile.Models.Enums.FileListViewMode.List; 
        public string SortColumn { get; set; } = "Name"; // 排序字段
        public string SortDirection { get; set; } = "Ascending"; // 排序方向
        public string SortColumn_Secondary { get; set; } = "Name";
        public string SortDirection_Secondary { get; set; } = "Ascending";

        // 标签页复用策略配置
        public int ReuseTabTimeWindow { get; set; } = 10; // 复用标签页的时间窗口（秒），默认10秒
        public bool AlwaysReuseTab { get; set; } = false; // 总是复用标签页（忽略时间窗口）
        public bool NeverReuseTab { get; set; } = false; // 从不复用标签页（总是创建新的）

        // 标签页宽度模式（旧，向后兼容）
        #pragma warning disable CS0612
        [Obsolete("使用 TabWidthStrategy + TabOverflowStrategy 替代")] public TabWidthMode TabWidthMode { get; set; } = TabWidthMode.FixedWidth;
        #pragma warning restore CS0612

        // 布局状态持久化
        public bool IsSidebarCollapsed { get; set; } = false;
        public bool IsPreviewCollapsed { get; set; } = true;

        // 搜索设置
        public bool IsEnableFullTextSearch { get; set; } = true;
        public System.Collections.Generic.List<string> FullTextIndexPaths { get; set; } = new System.Collections.Generic.List<string>(); // 启用全文搜索
        public string FullTextIndexDbPath { get; set; } = string.Empty; // 索引数据库路径

        // 多窗口支持
        public bool EnableMultiWindow { get; set; } = true;

        // 搜索历史设置
        public int HistoryMaxCount { get; set; } = 20;
        public bool AutoExpandHistory { get; set; } = false;

        // 导航栏项目顺序
        public System.Collections.Generic.List<string> NavigationSectionsOrder { get; set; } = new System.Collections.Generic.List<string>
        {
            "QuickAccess",
            "Drives",
            "FolderFavorites",
            "FileFavorites",
            "Libraries",
            "Tags"
        };
        
        // 侧边栏按钮排序
        public System.Collections.Generic.List<string> RailTopItems { get; set; } = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> RailBottomItems { get; set; } = new System.Collections.Generic.List<string>();

        // 侧边栏折叠状态持久化
        public System.Collections.Generic.Dictionary<string, bool> SidebarExpanderStates { get; set; } = new System.Collections.Generic.Dictionary<string, bool>();

        // 自定义快捷键 (Description -> KeyCombination)
        public System.Collections.Generic.Dictionary<string, string> CustomHotkeys { get; set; } = new System.Collections.Generic.Dictionary<string, string>();

        // 中键打开标签页行为
        public bool ActivateNewTabOnMiddleClick { get; set; } = true;

        // ── 剪切板设置 ──
        public int ClipboardMaxHistory { get; set; } = 50;
        public bool ClipboardAutoClean { get; set; } = true;
        public int ClipboardRetentionDays { get; set; } = 7;
        public bool ClipboardPersistHistory { get; set; } = true;
        public bool ClipboardCaptureFiles { get; set; } = true;
        public bool ClipboardCaptureImages { get; set; } = true;
        public bool ClipboardCaptureText { get; set; } = true;
        public bool ClipboardCaptureScreenshots { get; set; } = true;

        // Shell Context Menu Integration (Phase 3)
        public System.Collections.Generic.List<string> PinnedShellVerbs { get; set; } = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> HiddenShellVerbs { get; set; } = new System.Collections.Generic.List<string>();

        // 新建标签页的行为
        public NewTabAction NewTabAction { get; set; } = NewTabAction.Desktop;

        // Shell 菜单集成模式
        public string ShellMenuMode { get; set; } = "System";

        // 重命名时点击空白区域的行为
        public string RenameLostFocusBehavior { get; set; } = "Commit";
    }

    public class AllSettingsConfig
    {
        public AppConfig YiboFileConfig { get; set; } = new AppConfig();
        // Removed TagTrainSettings
    }

    public static class ConfigManager
    {
        private const string DataFileName = "yibofile_data.db";
        private const string BasePathMarkerFileName = "basepath.txt";

        private static string GetDefaultBaseDirectory()
        {
            try
            {
                string portableDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");
                if (Directory.Exists(portableDir))
                {
                    // 核心逻辑：检查该目录是否真的可写（防止 MSIX 封包把 AppData 也打进去导致只读）
                    try 
                    {
                        string testFile = Path.Combine(portableDir, ".write_test");
                        File.WriteAllText(testFile, DateTime.Now.ToString());
                        File.Delete(testFile);
                        return portableDir;
                    }
                    catch 
                    {
                        // 目录存在但不可写，说明处于只读封包环境，退回到用户的 AppData
                    }
                }
            }
            catch { }

            // 检查 Windows 商店/MSIX 环境（通过引用的 Package.Current 等方式更精准，但此处用通用逻辑即可）
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YiboFile");
        }

        private static string _baseDirectory;

        public static string GetConfigDirectory()
        {
            return GetBaseDirectory();
        }

        public static string GetDataFilePath() 
        {
            return Path.Combine(GetBaseDirectory(), DataFileName);
        }

        public static string GetBaseDirectory()
        {
            if (!string.IsNullOrEmpty(_baseDirectory))
            {
                return _baseDirectory;
            }

            string defaultDir = GetDefaultBaseDirectory();
            var marker = Path.Combine(defaultDir, BasePathMarkerFileName);
            string selected = defaultDir;

            if (File.Exists(marker))
            {
                try
                {
                    var path = File.ReadAllText(marker).Trim();
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    {
                        selected = path;
                    }
                }
                catch { }
            }

            try
            {
                Directory.CreateDirectory(selected);
            }
            catch { }

            _baseDirectory = selected;
            WriteBasePathMarker(_baseDirectory);

            return _baseDirectory;
        }

        public static void SetBaseDirectory(string newBaseDirectory, bool copyMissingFromOld = true)
        {
            if (string.IsNullOrWhiteSpace(newBaseDirectory))
            {
                return;
            }

            _baseDirectory = newBaseDirectory;

            try
            {
                Directory.CreateDirectory(_baseDirectory);
            }
            catch { }

            WriteBasePathMarker(_baseDirectory);
        }

        private static void WriteBasePathMarker(string baseDirectory)
        {
            try
            {
                string portableDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AppData");
                if (Directory.Exists(portableDir))
                {
                    File.WriteAllText(Path.Combine(portableDir, BasePathMarkerFileName), baseDirectory);
                }
            }
            catch { }
        }

    }
}
