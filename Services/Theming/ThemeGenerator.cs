using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace YiboFile.Services.Theming
{
    /// <summary>
    /// 主题生成器：基于核心种子色动态推导计算完整的 37 个语义令牌(Semantic Tokens)
    /// </summary>
    public static class ThemeGenerator
    {
        /// <summary>
        /// 基于提供的种子颜色以及暗色/亮色偏好，生成全套主题颜色字典
        /// </summary>
        public static Dictionary<string, string> GenerateSemanticTokens(
            Color primary, Color background, Color surface, Color text, Color border,
            Color success, Color warning, Color error, Color info, bool isDark)
        {
            var tokens = new Dictionary<string, string>();

            // 1. Backgrounds (背景类)
            tokens["BackgroundPrimaryBrush"] = background.ToString();
            tokens["BackgroundSecondaryBrush"] = surface.ToString();
            tokens["BackgroundTertiaryBrush"] = MixColor(surface, text, 0.04f).ToString();
            tokens["BackgroundElevatedBrush"] = isDark ? MixColor(surface, text, 0.08f).ToString() : "#FFFFFF";
            tokens["TitleBarBackgroundBrush"] = surface.ToString();
            tokens["NavigationRegionBrush"] = MixColor(background, text, 0.03f).ToString();
            tokens["SidebarBackgroundBrush"] = MixColor(background, text, 0.02f).ToString();
            tokens["PaneFocusedBackgroundBrush"] = background.ToString();
            tokens["PaneUnfocusedBackgroundBrush"] = MixColor(background, text, 0.015f).ToString();

            // 2. Texts (文本类)
            tokens["ForegroundPrimaryColor"] = text.ToString();
            tokens["ForegroundPrimaryBrush"] = text.ToString();
            tokens["ForegroundSecondaryBrush"] = MixColor(text, background, 0.35f).ToString();
            tokens["ForegroundTertiaryBrush"] = MixColor(text, background, 0.55f).ToString();
            tokens["ForegroundDisabledBrush"] = MixColor(text, background, 0.70f).ToString();
            // 强调色上的文本：亮色下通常为白，暗色如果强调色较浅可能需要黑色（这里简单按Primary亮度判断，也可固定全白或提供参数）
            tokens["ForegroundOnAccentBrush"] = GetLuminance(primary) > 0.6 ? "#000000" : "#FFFFFF";

            // 3. Borders (边框类)
            tokens["BorderDefaultBrush"] = border.ToString();
            tokens["BorderSubtleBrush"] = MixColor(border, background, 0.5f).ToString();
            tokens["BorderFocusBrush"] = primary.ToString();

            // 4. Accents (强调色/交互)
            tokens["AccentColor"] = primary.ToString();
            tokens["AccentHoverColor"] = LightenOrDarken(primary, isDark ? 0.15f : -0.15f).ToString();
            tokens["AccentDefaultBrush"] = primary.ToString();
            tokens["AccentHoverBrush"] = LightenOrDarken(primary, isDark ? 0.15f : -0.15f).ToString();
            tokens["AccentPressedBrush"] = LightenOrDarken(primary, isDark ? 0.25f : -0.25f).ToString();
            tokens["AccentSelectedBrush"] = MixColor(primary, background, 0.3f).ToString();
            tokens["AccentLightBrush"] = MixColor(primary, background, 0.85f).ToString();

            // 5. Control States (控件状态)
            tokens["ControlDefaultBrush"] = MixColor(primary, background, 0.85f).ToString();
            tokens["ControlHoverBrush"] = MixColor(primary, background, 0.75f).ToString();
            tokens["ControlPressedBrush"] = MixColor(primary, background, 0.65f).ToString();
            tokens["ControlDisabledBrush"] = MixColor(background, text, 0.05f).ToString();

            // 6. Statuses (状态色)
            tokens["StatusSuccessBrush"] = success.ToString();
            tokens["StatusWarningBrush"] = warning.ToString();
            tokens["StatusErrorBrush"] = error.ToString();
            tokens["StatusInfoBrush"] = info.ToString();

            // 7. Specials (特殊用途)
            tokens["TransparentBrush"] = "Transparent";
            tokens["OverlayBrush"] = isDark ? "#A0000000" : "#66000000";
            tokens["OverlayLightBrush"] = isDark ? "#66000000" : "#33000000";
            tokens["DividerBrush"] = border.ToString();
            tokens["ShadowBrush"] = isDark ? "#99000000" : "#1A000000";

            return tokens;
        }

        /// <summary>
        /// 混合两种颜色，ratio 是 target (c2) 的比例 [0..1]
        /// </summary>
        public static Color MixColor(Color c1, Color c2, float ratio)
        {
            ratio = Math.Max(0, Math.Min(1, ratio));
            byte r = (byte)(c1.R * (1 - ratio) + c2.R * ratio);
            byte g = (byte)(c1.G * (1 - ratio) + c2.G * ratio);
            byte b = (byte)(c1.B * (1 - ratio) + c2.B * ratio);
            return Color.FromArgb(c1.A, r, g, b);
        }

        /// <summary>
        /// 调整颜色亮度，amount 范围 (-1..1) 负数变暗，正数变亮
        /// </summary>
        public static Color LightenOrDarken(Color color, float amount)
        {
            int r, g, b;
            if (amount < 0)
            {
                // Darken
                r = (int)(color.R * (1 + amount));
                g = (int)(color.G * (1 + amount));
                b = (int)(color.B * (1 + amount));
            }
            else
            {
                // Lighten
                r = (int)(color.R + (255 - color.R) * amount);
                g = (int)(color.G + (255 - color.G) * amount);
                b = (int)(color.B + (255 - color.B) * amount);
            }
            return Color.FromArgb(color.A, (byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
        }

        /// <summary>
        /// 获取相对亮度（数字近似计算）
        /// </summary>
        private static double GetLuminance(Color color)
        {
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        }
    }
}
