using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace YiboFile.Services.Tabs
{
    // ═══════════════════════════════════════════════════════════════
    //  宽度策略接口：决定每个标签页的"理想自然宽度"
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 标签页宽度计算策略接口
    /// </summary>
    public interface ITabWidthStrategy
    {
        /// <summary>
        /// 计算每个标签的理想自然宽度
        /// </summary>
        List<(PathTab tab, double naturalWidth)> CalculateNaturalWidths(
            IReadOnlyList<PathTab> tabs,
            double availableWidth,
            Func<PathTab, string> getTitle,
            double fixedWidth,
            double maxWidth,
            double minWidth);
    }

    /// <summary>
    /// 固定宽度策略：所有标签使用相同的固定宽度
    /// </summary>
    public class FixedWidthStrategy : ITabWidthStrategy
    {
        public List<(PathTab tab, double naturalWidth)> CalculateNaturalWidths(
            IReadOnlyList<PathTab> tabs, double availableWidth,
            Func<PathTab, string> getTitle,
            double fixedWidth, double maxWidth, double minWidth)
        {
            var width = Math.Clamp(fixedWidth, minWidth, 300);
            return tabs.Select(t => (t, width)).ToList();
        }
    }

    /// <summary>
    /// 自适应宽度策略：根据标题文本长度自适应
    /// </summary>
    public class AdaptiveWidthStrategy : ITabWidthStrategy
    {
        private const double TAB_INTERNAL_PADDING = 58.0;

        public List<(PathTab tab, double naturalWidth)> CalculateNaturalWidths(
            IReadOnlyList<PathTab> tabs, double availableWidth,
            Func<PathTab, string> getTitle,
            double fixedWidth, double maxWidth, double minWidth)
        {
            return tabs.Select(t =>
            {
                var title = getTitle(t);
                var textWidth = TabTextMeasurer.MeasureTextWidth(title);
                var width = Math.Clamp(textWidth + TAB_INTERNAL_PADDING, minWidth, maxWidth);
                return (t, width);
            }).ToList();
        }
    }

    /// <summary>
    /// 弹性宽度策略：标签平分可用空间，始终填满一行
    /// </summary>
    public class ElasticWidthStrategy : ITabWidthStrategy
    {
        public List<(PathTab tab, double naturalWidth)> CalculateNaturalWidths(
            IReadOnlyList<PathTab> tabs, double availableWidth,
            Func<PathTab, string> getTitle,
            double fixedWidth, double maxWidth, double minWidth)
        {
            if (tabs.Count == 0) return new List<(PathTab, double)>();

            // 平分可用空间（减去标签间距）
            var totalMargins = tabs.Count * 2.0;
            var perTabWidth = (availableWidth - totalMargins) / tabs.Count;
            var width = Math.Clamp(perTabWidth, minWidth, maxWidth);
            return tabs.Select(t => (t, width)).ToList();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  溢出策略接口：决定理想宽度放不下时的处理方式
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 标签页溢出处理策略接口
    /// </summary>
    public interface ITabOverflowStrategy
    {
        /// <summary>
        /// 应用溢出逻辑，将自然宽度调整为最终宽度
        /// </summary>
        void ApplyOverflow(
            List<(PathTab tab, double naturalWidth)> tabWidths,
            double availableWidth,
            double minWidth);
    }

    /// <summary>
    /// 滚动溢出策略：保持自然宽度不变，让 ScrollViewer 处理溢出
    /// </summary>
    public class ScrollOverflowStrategy : ITabOverflowStrategy
    {
        public void ApplyOverflow(
            List<(PathTab tab, double naturalWidth)> tabWidths,
            double availableWidth, double minWidth)
        {
            // 不压缩，直接应用自然宽度
            foreach (var item in tabWidths)
                item.tab.TargetWidth = item.naturalWidth;
        }
    }

    /// <summary>
    /// 压缩溢出策略：等比缩小标签至一行内可见
    /// </summary>
    public class CompressOverflowStrategy : ITabOverflowStrategy
    {
        public void ApplyOverflow(
            List<(PathTab tab, double naturalWidth)> tabWidths,
            double availableWidth, double minWidth)
        {
            double totalNatural = tabWidths.Sum(t => t.naturalWidth + 2); // +2 for margins

            if (totalNatural <= availableWidth)
            {
                // 空间充足，直接使用自然宽度
                foreach (var item in tabWidths)
                    item.tab.TargetWidth = item.naturalWidth;
            }
            else
            {
                // 等比压缩
                double scale = availableWidth / totalNatural;
                foreach (var item in tabWidths)
                    item.tab.TargetWidth = Math.Max(minWidth, item.naturalWidth * scale);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  文本测量工具
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 标签页文本宽度测量工具（静态工具类）
    /// </summary>
    public static class TabTextMeasurer
    {
        /// <summary>
        /// 测量文本的实际渲染宽度（纯文本宽度，不含标签内部开销）
        /// </summary>
        public static double MeasureTextWidth(string text, double fontSize = 12, string fontFamily = "Segoe UI")
        {
            if (string.IsNullOrEmpty(text)) return 20;
            try
            {
                double dpi = 1.0;
                var app = Application.Current;
                if (app != null && app.MainWindow != null)
                {
                    try { dpi = VisualTreeHelper.GetDpi(app.MainWindow).PixelsPerDip; }
                    catch { /* 保持默认 dpi = 1.0 */ }
                }

                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily),
                    fontSize,
                    Brushes.Black,
                    dpi);

                return formattedText.Width;
            }
            catch
            {
                // 粗略估算：ASCII 约 7px，中文约 12px
                double estimated = 0;
                foreach (var c in text)
                    estimated += c > 127 ? 12.0 : 7.0;
                return estimated;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  组合器：TabWidthCalculator
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 标签页宽度计算器（策略模式组合器）
    /// 组合 ITabWidthStrategy 和 ITabOverflowStrategy 计算最终宽度
    /// </summary>
    public class TabWidthCalculator
    {
        private ITabWidthStrategy _widthStrategy;
        private ITabOverflowStrategy _overflowStrategy;
        private readonly Func<AppConfig> _getConfigFunc;
        private readonly Func<PathTab, string> _getDisplayTitleFunc;

        public TabWidthCalculator(
            Func<AppConfig> getConfigFunc,
            Func<PathTab, string> getDisplayTitleFunc)
        {
            _getConfigFunc = getConfigFunc ?? throw new ArgumentNullException(nameof(getConfigFunc));
            _getDisplayTitleFunc = getDisplayTitleFunc ?? throw new ArgumentNullException(nameof(getDisplayTitleFunc));
            // 根据当前配置初始化策略
            RebuildStrategies();
        }

        /// <summary>
        /// 根据当前配置重建策略对象（配置变更时调用）
        /// </summary>
        public void RebuildStrategies()
        {
            var config = _getConfigFunc();
            _widthStrategy = config.TabWidthStrategy switch
            {
                TabWidthStrategy.Fixed => new FixedWidthStrategy(),
                TabWidthStrategy.Adaptive => new AdaptiveWidthStrategy(),
                TabWidthStrategy.Elastic => new ElasticWidthStrategy(),
                _ => new AdaptiveWidthStrategy()
            };

            // Elastic + Scroll 无意义，强制使用 Compress
            if (config.TabWidthStrategy == TabWidthStrategy.Elastic)
            {
                _overflowStrategy = new CompressOverflowStrategy();
            }
            else
            {
                _overflowStrategy = config.TabOverflowStrategy switch
                {
                    TabOverflowStrategy.Scroll => new ScrollOverflowStrategy(),
                    TabOverflowStrategy.Compress => new CompressOverflowStrategy(),
                    _ => new ScrollOverflowStrategy()
                };
            }
        }

        /// <summary>
        /// 计算并应用所有标签页的宽度
        /// </summary>
        public void UpdateTabWidths(double totalWidth, IReadOnlyList<PathTab> tabs)
        {
            if (tabs == null || tabs.Count == 0 || totalWidth <= 0) return;

            var config = _getConfigFunc();

            // 分离固定和非固定标签页
            var pinnedTabs = tabs.Where(t => t.IsPinned).ToList();
            var unpinnedTabs = tabs.Where(t => !t.IsPinned).ToList();

            // 固定（钉住）标签页始终使用 TabFixedWidth
            double pinnedWidth = Math.Max(config.TabMinWidth, config.TabFixedWidth);
            foreach (var p in pinnedTabs) p.TargetWidth = pinnedWidth;

            // 计算非固定标签页可用空间
            double pinnedTotalWidth = pinnedTabs.Count * (pinnedWidth + 2);
            double availableForUnpinned = totalWidth - pinnedTotalWidth - 40; // 40 = 新建按钮

            if (unpinnedTabs.Count > 0 && availableForUnpinned > 0)
            {
                // Step 1: 计算理想宽度
                var naturalWidths = _widthStrategy.CalculateNaturalWidths(
                    unpinnedTabs, availableForUnpinned, _getDisplayTitleFunc,
                    config.TabFixedWidth, config.TabMaxWidth, config.TabMinWidth);

                // Step 2: 应用溢出策略
                _overflowStrategy.ApplyOverflow(naturalWidths, availableForUnpinned, config.TabMinWidth);
            }
        }
    }
}
