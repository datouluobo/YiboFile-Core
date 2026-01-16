using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YiboFile.Services.Config;

namespace YiboFile.Services.Tabs
{
    /// <summary>
    /// 标签页宽度计算器
    /// 负责计算和应用标签页宽度（Fixed 和 Dynamic 模式）
    /// </summary>
    public class TabWidthCalculator
    {
        // Tab width compression constants
        private const double MIN_TAB_WIDTH = 40.0;
        private const double PREFERRED_TAB_WIDTH = 160.0;
        private const double MAX_TAB_WIDTH = 300.0;

        private readonly AppConfig _config;
        private readonly Func<PathTab, string> _getTitleFunc;
        private readonly Func<double> _getPinnedWidthFunc;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">应用配置</param>
        /// <param name="getTitleFunc">获取标签页有效标题的函数</param>
        /// <param name="getPinnedWidthFunc">获取固定标签页宽度的函数</param>
        public TabWidthCalculator(
            AppConfig config,
            Func<PathTab, string> getTitleFunc,
            Func<double> getPinnedWidthFunc)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _getTitleFunc = getTitleFunc ?? throw new ArgumentNullException(nameof(getTitleFunc));
            _getPinnedWidthFunc = getPinnedWidthFunc ?? throw new ArgumentNullException(nameof(getPinnedWidthFunc));
        }

        /// <summary>
        /// 更新所有标签页宽度（基于可用空间的压缩逻辑）
        /// </summary>
        /// <param name="tabsBorder">标签页边框容器</param>
        /// <param name="tabs">所有标签页列表</param>
        public void UpdateTabWidths(Border tabsBorder, IReadOnlyList<PathTab> tabs)
        {
            if (tabs == null || tabs.Count == 0) return;
            if (tabsBorder == null) return;

            double totalWidth = tabsBorder.ActualWidth;
            if (totalWidth <= 0) return;

            // 分离固定和非固定标签页
            var pinnedTabs = tabs.Where(t => t.IsPinned).ToList();
            var unpinnedTabs = tabs.Where(t => !t.IsPinned).ToList();

            // 计算固定标签页总宽度
            double pinnedTotalWidth = 0;
            double pinnedTabWidth = _getPinnedWidthFunc();
            foreach (var p in pinnedTabs)
            {
                if (p.TabButton != null)
                {
                    // 强制设置宽度，确保标签页能够随设置变小
                    p.TabButton.Width = pinnedTabWidth;
                    p.TabButton.MinWidth = 0;
                }
                pinnedTotalWidth += pinnedTabWidth + 2; // + Margin
            }

            // 计算非固定标签页可用宽度
            double availableForUnpinned = totalWidth - pinnedTotalWidth - 8; // Minimal padding

            if (unpinnedTabs.Count > 0 && availableForUnpinned > 0)
            {
                var mode = _config?.TabWidthMode ?? TabWidthMode.FixedWidth;

                if (mode == TabWidthMode.DynamicWidth)
                {
                    UpdateTabWidthsDynamic(unpinnedTabs, availableForUnpinned);
                }
                else
                {
                    UpdateTabWidthsFixed(unpinnedTabs, availableForUnpinned);
                }
            }
        }

        /// <summary>
        /// 固定宽度模式：所有标签页相同宽度
        /// </summary>
        private void UpdateTabWidthsFixed(List<PathTab> unpinnedTabs, double availableForUnpinned)
        {
            // 使用配置的宽度
            double userSettingWidth = _getPinnedWidthFunc();
            double preferredTotalWidth = unpinnedTabs.Count * userSettingWidth;

            double targetWidth;
            if (preferredTotalWidth <= availableForUnpinned)
            {
                // 空间足够时，使用设定宽度
                targetWidth = userSettingWidth;
            }
            else
            {
                // 空间不足时，进行压缩，但不能小于最小值
                double calculatedWidth = availableForUnpinned / unpinnedTabs.Count;
                // 确保不大于设定宽度，且不小于最小值（除非设定值本身就更小）
                double actualMin = Math.Min(userSettingWidth, MIN_TAB_WIDTH);
                targetWidth = Math.Max(actualMin, Math.Min(userSettingWidth, calculatedWidth));
            }

            foreach (var t in unpinnedTabs)
            {
                if (t.TabButton != null)
                {
                    // 强制设置宽度
                    t.TabButton.Width = targetWidth;
                    t.TabButton.MinWidth = 0;
                }
            }
        }

        /// <summary>
        /// 动态宽度模式：根据文本长度自适应
        /// </summary>
        private void UpdateTabWidthsDynamic(List<PathTab> unpinnedTabs, double availableForUnpinned)
        {
            var tabWidths = new List<(PathTab tab, double width)>();
            double totalNaturalWidth = 0;

            foreach (var tab in unpinnedTabs)
            {
                var title = _getTitleFunc(tab);
                var measuredWidth = MeasureTextWidth(title);
                var naturalWidth = Math.Max(MIN_TAB_WIDTH, Math.Min(MAX_TAB_WIDTH, measuredWidth));
                tabWidths.Add((tab, naturalWidth));
                totalNaturalWidth += naturalWidth;
            }

            double scaleFactor = 1.0;
            if (totalNaturalWidth > availableForUnpinned)
            {
                scaleFactor = availableForUnpinned / totalNaturalWidth;
            }

            foreach (var (tab, naturalWidth) in tabWidths)
            {
                if (tab.TabButton != null)
                {
                    double finalWidth = Math.Max(MIN_TAB_WIDTH, naturalWidth * scaleFactor);
                    tab.TabButton.Width = finalWidth;
                    tab.TabButton.MinWidth = 0;
                }
            }
        }

        /// <summary>
        /// 测量文本宽度（用于动态宽度模式）
        /// </summary>
        private double MeasureTextWidth(string text, double fontSize = 12, string fontFamily = "Segoe UI")
        {
            if (string.IsNullOrEmpty(text)) return MIN_TAB_WIDTH;

            try
            {
                var formattedText = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily),
                    fontSize,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip
                );
                return formattedText.Width + 40; // Icon/close (24px) + spacing (4px) + padding (2px) + buffer (10px)
            }
            catch
            {
                return PREFERRED_TAB_WIDTH;
            }
        }
    }
}

