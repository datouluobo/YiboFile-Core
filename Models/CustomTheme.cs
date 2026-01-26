using System;
using System.Collections.Generic;

namespace YiboFile.Models
{
    /// <summary>
    /// 自定义主题数据模型
    /// </summary>
    public class CustomTheme
    {
        /// <summary>
        /// 唯一标识符
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 主题名称（用户自定义）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 基于的内置主题（Light/Dark）
        /// </summary>
        public string BaseTheme { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime ModifiedAt { get; set; }

        /// <summary>
        /// 自定义颜色字典
        /// Key: 颜色资源键名（如"AccentDefaultBrush"）
        /// Value: 十六进制颜色值（如"#007BFF"）
        /// </summary>
        public Dictionary<string, string> Colors { get; set; }

        /// <summary>
        /// 预览主色调
        /// </summary>
        public string PreviewPrimaryColor { get; set; }

        /// <summary>
        /// 预览背景色
        /// </summary>
        public string PreviewBackgroundColor { get; set; }

        /// <summary>
        /// 预览表面色
        /// </summary>
        public string PreviewSurfaceColor { get; set; }

        /// <summary>
        /// 预览文本色
        /// </summary>
        public string PreviewTextColor { get; set; }

        public CustomTheme()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTime.Now;
            ModifiedAt = DateTime.Now;
            Colors = new Dictionary<string, string>();
        }

        /// <summary>
        /// 从现有主题创建自定义主题
        /// </summary>
        public static CustomTheme CreateFromBaseTheme(string name, string baseTheme, Dictionary<string, string> colors)
        {
            return new CustomTheme
            {
                Name = name,
                BaseTheme = baseTheme,
                Colors = new Dictionary<string, string>(colors),
                PreviewPrimaryColor = colors.ContainsKey("AccentDefaultBrush") ? colors["AccentDefaultBrush"] : "#007BFF",
                PreviewBackgroundColor = colors.ContainsKey("BackgroundPrimaryBrush") ? colors["BackgroundPrimaryBrush"] : "#FFFFFF",
                PreviewSurfaceColor = colors.ContainsKey("BackgroundSecondaryBrush") ? colors["BackgroundSecondaryBrush"] : "#F8F9FA",
                PreviewTextColor = colors.ContainsKey("ForegroundPrimaryBrush") ? colors["ForegroundPrimaryBrush"] : "#212529"
            };
        }

        /// <summary>
        /// 更新修改时间
        /// </summary>
        public void Touch()
        {
            ModifiedAt = DateTime.Now;
        }
    }
}

