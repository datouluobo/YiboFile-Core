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
    /// 负责计算标签页理想宽度并同步到模型（数据驱动模式）
    /// </summary>
    public class TabWidthCalculator
    {
        private const double MIN_TAB_WIDTH = 40.0;
        private const double MAX_TAB_WIDTH = 300.0;

        private readonly AppConfig _config;
        private readonly Func<PathTab, string> _getTitleFunc;
        private readonly Func<double> _getPinnedWidthFunc;

        public TabWidthCalculator(
            AppConfig config,
            Func<PathTab, string> getTitleFunc,
            Func<double> getPinnedWidthFunc)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _getTitleFunc = getTitleFunc ?? throw new ArgumentNullException(nameof(getTitleFunc));
            _getPinnedWidthFunc = getPinnedWidthFunc ?? throw new ArgumentNullException(nameof(getPinnedWidthFunc));
        }

        public void UpdateTabWidths(double totalWidth, IReadOnlyList<PathTab> tabs)
        {
            if (tabs == null || tabs.Count == 0 || totalWidth <= 0) return;

            // 分离固定和非固定标签页
            var pinnedTabs = tabs.Where(t => t.IsPinned).ToList();
            var unpinnedTabs = tabs.Where(t => !t.IsPinned).ToList();

            // 更新固定标签页
            double pinnedTabWidth = _getPinnedWidthFunc();
            foreach (var p in pinnedTabs) p.TargetWidth = pinnedTabWidth;

            double pinnedTotalWidth = pinnedTabs.Count * (pinnedTabWidth + 2);
            double availableForUnpinned = totalWidth - pinnedTotalWidth - 10;

            if (unpinnedTabs.Count > 0 && availableForUnpinned > 0)
            {
                var mode = _config?.TabWidthMode ?? TabWidthMode.FixedWidth;
                if (mode == TabWidthMode.DynamicWidth)
                    UpdateTabWidthsDynamic(unpinnedTabs, availableForUnpinned);
                else
                    UpdateTabWidthsFixed(unpinnedTabs, availableForUnpinned);
            }
        }

        private void UpdateTabWidthsFixed(List<PathTab> unpinnedTabs, double availableForUnpinned)
        {
            double userSettingWidth = _getPinnedWidthFunc();
            double preferredTotalWidth = unpinnedTabs.Count * userSettingWidth;

            double targetWidth;
            if (preferredTotalWidth <= availableForUnpinned)
            {
                targetWidth = userSettingWidth;
            }
            else
            {
                double calculatedWidth = availableForUnpinned / unpinnedTabs.Count;
                double actualMin = Math.Min(userSettingWidth, MIN_TAB_WIDTH);
                targetWidth = Math.Max(actualMin, Math.Min(userSettingWidth, calculatedWidth));
            }

            foreach (var t in unpinnedTabs) t.TargetWidth = targetWidth;
        }

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

            double scaleFactor = totalNaturalWidth > availableForUnpinned ? availableForUnpinned / totalNaturalWidth : 1.0;

            foreach (var item in tabWidths)
            {
                item.tab.TargetWidth = Math.Max(MIN_TAB_WIDTH, item.width * scaleFactor);
            }
        }

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
                    VisualTreeHelper.GetDpi(Application.Current.MainWindow ?? new Window()).PixelsPerDip
                );
                return formattedText.Width + 50;
            }
            catch
            {
                return 160.0;
            }
        }
    }
}
