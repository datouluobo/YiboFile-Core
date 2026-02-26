using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace YiboFile.Services.Theming
{
    /// <summary>
    /// UI 合约验证器 (Contract Validator)
    /// 确保 XAML 资源字典内包含所有应有的必需键 (必需的 Token)。
    /// 涵盖颜色主题、UI形状、图标映射三个维度。
    /// </summary>
    public static class ContractValidator
    {
        // 37 个必需的主题键
        public static readonly IReadOnlyList<string> ThemeRequiredKeys = new[]
        {
            // 背景
            "BackgroundPrimaryBrush", "BackgroundSecondaryBrush", "BackgroundTertiaryBrush",
            "BackgroundElevatedBrush", "TitleBarBackgroundBrush", "NavigationRegionBrush",
            "SidebarBackgroundBrush", "PaneFocusedBackgroundBrush", "PaneUnfocusedBackgroundBrush",
            // 文本
            "ForegroundPrimaryColor", "ForegroundPrimaryBrush", "ForegroundSecondaryBrush",
            "ForegroundTertiaryBrush", "ForegroundDisabledBrush", "ForegroundOnAccentBrush",
            // 边框
            "BorderDefaultBrush", "BorderSubtleBrush", "BorderFocusBrush",
            // 强调色
            "AccentColor", "AccentHoverColor", "AccentDefaultBrush", "AccentHoverBrush",
            "AccentPressedBrush", "AccentSelectedBrush", "AccentLightBrush",
            // 控件状态
            "ControlDefaultBrush", "ControlHoverBrush", "ControlPressedBrush", "ControlDisabledBrush",
            // 状态色
            "StatusSuccessBrush", "StatusWarningBrush", "StatusErrorBrush", "StatusInfoBrush",
            // 特殊
            "TransparentBrush", "OverlayBrush", "OverlayLightBrush", "DividerBrush", "ShadowBrush"
        };

        // 59 个必需的 UI 风格键
        public static readonly IReadOnlyList<string> UIStyleRequiredKeys = new[]
        {
            "UIStyleId", "UIStyleDisplayName", "UIStyleDescription",
            // Tabs
            "UI.TabItem.CornerRadius", "UI.TabItem.Margin", "UI.TabItem.Padding", "UI.TabItem.Height",
            "UI.TabItem.ActiveBackground", "UI.TabItem.ActiveBorderThickness", "UI.TabItem.SeparatorVisibility",
            "UI.TabItem.SeparatorHeight", "UI.TabItem.ActiveIndicatorVisibility", "UI.TabItem.ActiveIndicatorPosition",
            "UI.TabItem.ActiveIndicatorHeight", "UI.TabItem.ActiveIndicatorMargin", "UI.TabItem.ActiveIndicatorRadius",
            "UI.TabItem.ActiveIndicatorColor",
            // Toolbar
            "UI.ToolbarButton.CornerRadius", "UI.ToolbarButton.BorderThickness", "UI.ToolbarButton.Padding",
            "UI.ToolbarButton.Margin", "UI.ToolbarButton.Height",
            // Sidebar
            "UI.Sidebar.ItemCornerRadius", "UI.Sidebar.ItemMargin", "UI.Sidebar.ActiveIndicatorVisibility",
            "UI.Sidebar.ItemPadding", "UI.Sidebar.ItemHeight", "UI.Sidebar.ActiveBackground",
            // AddressBar
            "UI.AddressBar.CornerRadius", "UI.AddressBar.BorderThickness",
            // FileList
            "UI.FileList.RowCornerRadius", "UI.FileList.RowMargin", "UI.FileList.RowBorderThickness",
            "UI.FileList.RowMinHeight", "UI.FileList.RowPadding", "UI.FileList.SelectedBorderThickness",
            // ColumnHeader
            "UI.ColumnHeader.Padding", "UI.ColumnHeader.BorderThickness", 
            "UI.ColumnHeader.FontWeight", "UI.ColumnHeader.Height",
            // Splitter
            "UI.Splitter.VisibleLine", "UI.Splitter.Width", "UI.Splitter.LineThickness",
            "UI.Splitter.HoverShowGripDot", "UI.Splitter.CollapseButtonOpacity",
            // ScrollBar
            "UI.ScrollBar.ThumbCornerRadius", "UI.ScrollBar.ThumbWidth", "UI.ScrollBar.ThumbHoverWidth",
            "UI.ScrollBar.ThumbOpacity", "UI.ScrollBar.ThumbHoverOpacity", "UI.ScrollBar.Mode",
            // Generic controls
            "UI.Button.CornerRadius", "UI.TextBox.CornerRadius", "UI.ComboBox.CornerRadius",
            "UI.GroupBox.CornerRadius", "UI.ContextMenu.CornerRadius", "UI.MenuItem.CornerRadius",
            // Dialog
            "UI.Dialog.CornerRadius", "UI.Dialog.ButtonCornerRadius"
        };

        // 图标合约必需键 (DrawingImage 制式)
        public static readonly IReadOnlyList<string> IconRequiredKeys = new[]
        {
            "IconStyleId", "IconStyleDisplayName", "IconStyleDescription",
            
            // 通用操作图标
            "Icon_Copy", "Icon_Edit", "Icon_Format", "Icon_Render", "Icon_Wrap",
            "Icon_OpenExternal", "Icon_ChevronUp", "Icon_ChevronDown",
            "Icon_Search", "Icon_Settings", "Icon_Filter", "Icon_Add",
            "Icon_Delete_Outline", "Icon_NewFolder", "Icon_NewFile",
            "Icon_Refresh", "Icon_Desktop", "Icon_User",

            // 导航/工具栏
            "Icon_Back", "Icon_Forward", "Icon_Up", "Icon_ViewList", "Icon_ViewThumb",

            // 文件类型
            "Icon_Drive", "Icon_Folder", "Icon_File", "Icon_Music", "Icon_Video", "Icon_Image", "Icon_Document",

            // 导航面板
            "Icon_Nav_Path", "Icon_Nav_Library", "Icon_Nav_Tag",

            // 窗口/功能面板
            "Icon_Window_Settings", "Icon_Window_About", "Icon_Window_Tasks",
            "Icon_Backup", "Icon_Clipboard",

            // 布局模式
            "Icon_Layout_Focus", "Icon_Layout_Work", "Icon_Layout_Full", "Icon_DualList",

            // 状态/Toast
            "Icon_Success", "Icon_Error", "Icon_Warning", "Icon_Info", "Icon_Close",

            // 通用新增
            "Icon_Tag", "Icon_Library", "Icon_History", "Icon_Pin",
            "Icon_Sort", "Icon_MoreHorizontal", "Icon_MoreVertical", "Icon_Check", "Icon_Download",

            // 播放器
            "Icon_Play", "Icon_Pause", "Icon_Volume_Mute", "Icon_Volume_Low",
            "Icon_Volume_Medium", "Icon_Volume_High"
        };

        /// <summary>
        /// 验证目标资源字典是否包含合约要求的所有必需键。
        /// </summary>
        /// <param name="dict">要验证的 XAML 资源字典</param>
        /// <param name="requiredKeys">需要存在的键集合</param>
        /// <returns>验证结果</returns>
        public static ContractValidationResult Validate(ResourceDictionary dict, IReadOnlyList<string> requiredKeys)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));

            var missingKeys = requiredKeys.Where(k => !dict.Contains(k)).ToList();
            return new ContractValidationResult
            {
                IsValid = missingKeys.Count == 0,
                MissingKeys = missingKeys
            };
        }
    }

    /// <summary>合约验证结果</summary>
    public class ContractValidationResult
    {
        public bool IsValid { get; set; }
        public IReadOnlyList<string> MissingKeys { get; set; } = Array.Empty<string>();
        
        // 组合其他验证信息
        public string ErrorMessage => IsValid ? string.Empty : $"Missing {MissingKeys.Count} required contract keys: {string.Join(", ", MissingKeys)}";
    }
}
