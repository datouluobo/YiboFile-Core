using YiboFile.Models;
using System;
using System.Collections.Generic;

namespace YiboFile.Services.Theming
{
    /// <summary>
    /// UI三维坐标系（颜色主题、UI风格、图标风格）的统一管理接口
    /// 负责加载、解析和应用全部 UI 资源。
    /// </summary>
    public interface IThemeService
    {
        // ========================================
        // 一、颜色主题 (Color Themes)
        // ========================================
        ThemeMetadata CurrentTheme { get; }
        IReadOnlyList<ThemeMetadata> AvailableThemes { get; }
        void SetTheme(string themeId, bool animate = true);
        void ToggleTheme();

        // ========================================
        // 二、UI 风格 (UI Styles)
        // ========================================
        UIStyleMetadata CurrentUIStyle { get; }
        IReadOnlyList<UIStyleMetadata> AvailableUIStyles { get; }
        void SetUIStyle(string styleId);

        // ========================================
        // 三、标签页风格 (Tab Styles)
        // ========================================
        TabStyleMetadata CurrentTabStyle { get; }
        IReadOnlyList<TabStyleMetadata> AvailableTabStyles { get; }
        void SetTabStyle(string styleId);

        // ========================================
        // 四、图标风格 (Icon Styles)
        // ========================================
        IconStyleMetadata CurrentIconStyle { get; }
        IReadOnlyList<IconStyleMetadata> AvailableIconStyles { get; }
        void SetIconStyle(string styleId);

        // ========================================
        // 五、系统主题跟随
        // ========================================
        bool IsFollowingSystemTheme { get; }
        void EnableSystemThemeFollowing();
        void DisableSystemThemeFollowing();

        // ========================================
        // 六、自定义主题 (Custom Themes)
        // ========================================
        IReadOnlyList<CustomTheme> CustomThemes { get; }
        CustomTheme CreateCustomTheme(string name, string baseTheme);
        void SaveCustomTheme(CustomTheme theme);
        void DeleteCustomTheme(string themeId);
        void ApplyCustomTheme(CustomTheme theme);

        // ========================================
        // 七、诊断与验证
        // ========================================
        ContractValidationResult ValidateAll();

        // ========================================
        // 八、事件通知
        // ========================================
        event EventHandler<ThemeChangedEventArgs> ThemeChanged;
        event EventHandler<UIStyleChangedEventArgs> UIStyleChanged;
        event EventHandler<TabStyleChangedEventArgs> TabStyleChanged;
        event EventHandler<IconStyleChangedEventArgs> IconStyleChanged;
    }

    /// <summary>UI风格元数据</summary>
    public class UIStyleMetadata
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    /// <summary>标签页风格元数据</summary>
    public class TabStyleMetadata
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public System.Uri Source { get; set; }
        public System.Windows.Media.Brush ActiveBackground { get; set; }
        public System.Windows.Media.Brush ActiveBorderBrush { get; set; }
        public System.Windows.Media.Brush ActiveIndicatorBrush { get; set; }
        public System.Windows.Media.Brush ActiveSideIndicatorBrush { get; set; }
        public System.Windows.Thickness ActiveBorderThickness { get; set; }
        public System.Windows.Thickness Margin { get; set; }
        public System.Windows.Thickness ActiveIndicatorMargin { get; set; }
        public System.Windows.Thickness ActiveSideIndicatorMargin { get; set; }
        public System.Windows.CornerRadius CornerRadius { get; set; }
        public System.Windows.CornerRadius ActiveSideIndicatorRadius { get; set; }
        public double Height { get; set; }
        public double ActiveIndicatorHeight { get; set; }
        public double ActiveIndicatorRadius { get; set; }
        public double ActiveSideIndicatorWidth { get; set; }
        public System.Windows.VerticalAlignment ActiveIndicatorPosition { get; set; }
        public System.Windows.Visibility ActiveIndicatorVisibility { get; set; }
        public System.Windows.Visibility ActiveSideIndicatorVisibility { get; set; }
        public System.Windows.FontWeight ActiveFontWeight { get; set; }
    }

    /// <summary>图标集元数据</summary>
    public class IconStyleMetadata
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    /// <summary>主题切换事件参数</summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public ThemeMetadata OldTheme { get; }
        public ThemeMetadata NewTheme { get; }
        public bool IsCustom { get; }

        public ThemeChangedEventArgs(ThemeMetadata oldTheme, ThemeMetadata newTheme, bool isCustom = false)
        {
            OldTheme = oldTheme;
            NewTheme = newTheme;
            IsCustom = isCustom;
        }
    }

    /// <summary>UI风格切换事件参数</summary>
    public class UIStyleChangedEventArgs : EventArgs
    {
        public UIStyleMetadata OldStyle { get; }
        public UIStyleMetadata NewStyle { get; }

        public UIStyleChangedEventArgs(UIStyleMetadata oldStyle, UIStyleMetadata newStyle)
        {
            OldStyle = oldStyle;
            NewStyle = newStyle;
        }
    }

    /// <summary>标签页风格切换事件参数</summary>
    public class TabStyleChangedEventArgs : EventArgs
    {
        public TabStyleMetadata OldStyle { get; }
        public TabStyleMetadata NewStyle { get; }

        public TabStyleChangedEventArgs(TabStyleMetadata oldStyle, TabStyleMetadata newStyle)
        {
            OldStyle = oldStyle;
            NewStyle = newStyle;
        }
    }

    /// <summary>图标风格切换事件参数</summary>
    public class IconStyleChangedEventArgs : EventArgs
    {
        public IconStyleMetadata OldStyle { get; }
        public IconStyleMetadata NewStyle { get; }

        public IconStyleChangedEventArgs(IconStyleMetadata oldStyle, IconStyleMetadata newStyle)
        {
            OldStyle = oldStyle;
            NewStyle = newStyle;
        }
    }
}

