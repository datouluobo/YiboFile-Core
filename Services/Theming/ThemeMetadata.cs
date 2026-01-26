using System;

namespace YiboFile.Services.Theming
{
    /// <summary>
    /// 主题元数据
    /// </summary>
    public class ThemeMetadata
    {
        /// <summary>
        /// 主题唯一标识（如 "Light", "Dark"）
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 显示名称（支持本地化）
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 主题描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 作者信息
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// 版本号
        /// </summary>
        public Version Version { get; set; }

        /// <summary>
        /// 资源字典 URI
        /// </summary>
        public Uri Source { get; set; }

        /// <summary>
        /// 预览色彩（用于设置界面显示）
        /// </summary>
        public ThemePreviewColors PreviewColors { get; set; }

        /// <summary>
        /// 是否为内置主题
        /// </summary>
        public bool IsBuiltIn { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 主题预览色彩
    /// </summary>
    public class ThemePreviewColors
    {
        /// <summary>
        /// 主色调
        /// </summary>
        public string Primary { get; set; }

        /// <summary>
        /// 背景色
        /// </summary>
        public string Background { get; set; }

        /// <summary>
        /// 表面色
        /// </summary>
        public string Surface { get; set; }

        /// <summary>
        /// 主文本色
        /// </summary>
        public string TextPrimary { get; set; }
    }
}

