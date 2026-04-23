using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

namespace YiboFile.Controls.Behaviors
{
    public class AutoColumnWidthBehavior
    {
        public static readonly DependencyProperty IsAutoHiddenProperty = 
            DependencyProperty.RegisterAttached("IsAutoHidden", typeof(bool), typeof(AutoColumnWidthBehavior), new PropertyMetadata(false));

        public static readonly DependencyProperty PreCompressWidthProperty = 
            DependencyProperty.RegisterAttached("PreCompressWidth", typeof(double), typeof(AutoColumnWidthBehavior), new PropertyMetadata(-1.0));

        private static readonly System.Collections.Generic.Dictionary<string, double> MinColumnWidths = new System.Collections.Generic.Dictionary<string, double>()
        {
            { "Type",         60 },
            { "Size",         80 },
            { "ModifiedDate", 90 },
            { "CreatedTime",  60 },
            { "Tags",        100 },
            { "Notes",       100 },
        };
        public static readonly DependencyProperty IsFillColumnProperty = 
            DependencyProperty.RegisterAttached(
                "IsFillColumn", 
                typeof(bool), 
                typeof(AutoColumnWidthBehavior), 
                new PropertyMetadata(false));

        public static void SetIsFillColumn(DependencyObject element, bool value)
        {
            element.SetValue(IsFillColumnProperty, value);
        }

        public static bool GetIsFillColumn(DependencyObject element)
        {
            return (bool)element.GetValue(IsFillColumnProperty);
        }

        private ListView _listView;
        private string _targetColumnName;
        private GridViewHeaderRowPresenter _cachedHeader;

        public AutoColumnWidthBehavior(ListView listView, string targetColumnName = "Name")
        {
            _listView = listView;
            _targetColumnName = targetColumnName;

            if (_listView != null)
            {
                _listView.SizeChanged += ListView_SizeChanged;
            }
        }

        public void Detach()
        {
            if (_listView != null)
            {
                _listView.SizeChanged -= ListView_SizeChanged;
            }
        }

        private void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged)
            {
                AdjustTargetColumnWidth();
            }
        }

        public void AdjustTargetColumnWidth()
        {
            if (_listView == null || !_listView.IsLoaded) return;
            if (!(_listView.View is GridView gridView)) return;

            var columns = gridView.Columns;
            if (columns == null || columns.Count == 0) return;

            // 1. 获取当前主题下的边距修正量 (提供更强的鲁棒性)
            double leftCorrection = 10;  // 基准值
            double rightCorrection = 10;
            try
            {
                // 优先从应用资源获取，确保在动态主题切换时能拿到最新值
                var rowMargin = (Application.Current.TryFindResource("UI.FileList.RowMargin") ?? 
                                 _listView.TryFindResource("UI.FileList.RowMargin")) as Thickness?;
                var rowPadding = (Application.Current.TryFindResource("UI.FileList.RowPadding") ?? 
                                  _listView.TryFindResource("UI.FileList.RowPadding")) as Thickness?;
                
                if (rowMargin.HasValue)
                {
                    leftCorrection = rowMargin.Value.Left;
                    rightCorrection = rowMargin.Value.Right;
                }
                
                if (rowPadding.HasValue)
                {
                    leftCorrection += rowPadding.Value.Left;
                    rightCorrection += rowPadding.Value.Right;
                }
            }
            catch { }

            // 2. 查找 ScrollViewer 以获取精确的视窗宽度 (ViewportWidth)
            var scrollViewer = FindVisualChild<ScrollViewer>(_listView);
            double availableWidth = _listView.ActualWidth;
            
            // 3. 获取滚动条占位策略
            double scrollBarWidth = SystemParameters.VerticalScrollBarWidth;
            var scrollMode = _listView.TryFindResource("UI.ScrollBar.Mode") as string;
            bool isOverlay = scrollMode != null && scrollMode.ToString() == "Overlay";

            if (scrollViewer != null)
            {
                // 如果能找到 ScrollViewer，直接用 ViewportWidth，它已自动扣除可见的非 Overlay 滚动条
                availableWidth = scrollViewer.ViewportWidth;
            }
            else if (!isOverlay)
            {
                // Fallback: 仅在非 Overlay 模式下手动扣除
                availableWidth -= scrollBarWidth;
            }

            // 4. 计算可用总列宽
            // 修正：叠加滚动条预留 12px 以避开 8px 的悬浮滑块，非叠加模式仅预留 2px
            double safetyPadding = isOverlay ? 12 : 2; 
            double usableWidth = Math.Max(100, availableWidth - leftCorrection - rightCorrection - safetyPadding);

            // 5. 同步表头位置，使其与行内容严格对齐
            if (_cachedHeader == null || !_cachedHeader.IsLoaded)
            {
                _cachedHeader = FindVisualChild<GridViewHeaderRowPresenter>(_listView);
            }
            
            if (_cachedHeader != null)
            {
                // 表头右侧边距计算：
                // 关键点：表头的 Margin 必须与行内容的视觉边界对齐。
                // 对于标准滚动条模式，行内容的右边界 = rightCorrection + safetyPadding
                // 表头也应该使用相同的值，确保与滚动内容严格对齐。
                // 
                // 注意：标准模式下滚动条位于 ScrollViewer 内部（ListView 的默认行为），
                // 表头的 rightCorrection + safetyPadding 自然与行内容的可用宽度对齐，
                // 不需要额外加减 scrollBarWidth。
                double headerRightCorrection = rightCorrection + safetyPadding;
                
                _cachedHeader.Margin = new Thickness(leftCorrection, 0, headerRightCorrection, 0);
            }

            // 6. 在计算列款前尝试重置自动压缩的列
            foreach (var col in columns)
            {
                bool isAutoHidden = (bool)col.GetValue(IsAutoHiddenProperty);
                double preW = (double)col.GetValue(PreCompressWidthProperty);

                if (isAutoHidden)
                {
                    col.SetValue(IsAutoHiddenProperty, false);
                    if (preW > 0) col.Width = preW;
                }
                else
                {
                    double currentW = col.ActualWidth > 0 ? col.ActualWidth : (double.IsNaN(col.Width) ? 0 : col.Width);
                    if (currentW > 0 && preW > 0 && currentW < preW)
                    {
                        // 尝试恢复之前被压缩的宽度
                        col.Width = preW;
                    }
                    else if (currentW > 0)
                    {
                        // 记录最新期望宽度
                        col.SetValue(PreCompressWidthProperty, currentW);
                    }
                    else if (currentW == 0 && !isAutoHidden)
                    {
                        // 用户主动隐藏了
                        col.SetValue(PreCompressWidthProperty, -1.0);
                    }
                }
            }

            // 6. 统计各列状态
            var fillColumns = new System.Collections.Generic.List<GridViewColumn>();
            var otherColumns = new System.Collections.Generic.List<GridViewColumn>();
            double otherColumnsWidth = 0;

            foreach (var col in columns)
            {
                string tag = GetColumnTag(col);
                if (GetIsFillColumn(col) || tag == _targetColumnName)
                {
                    fillColumns.Add(col);
                }
                else
                {
                    double w = double.IsNaN(col.Width) ? col.ActualWidth : col.Width;
                    if (w > 0)
                    {
                        otherColumnsWidth += w;
                        otherColumns.Add(col);
                    }
                }
            }

            // 均分 fillColumns 的宽度
            if (fillColumns.Count == 0) return;

            double minFillWidth = 40;
            double fillWidth = usableWidth - otherColumnsWidth;

            // --- Level 1: 压缩弹性列 ---
            if (fillWidth < minFillWidth * fillColumns.Count)
            {
                fillWidth = minFillWidth * fillColumns.Count;
            }

            double widthPerFillCol = fillWidth / fillColumns.Count;
            foreach (var fillCol in fillColumns)
            {
                fillCol.Width = widthPerFillCol;
            }

            // ── 第二阶段：如果弹性列已到下限、总宽度仍然溢出，多级瀑布压缩 ──
            double totalUsed = fillWidth + otherColumnsWidth;
            if (totalUsed > usableWidth && otherColumnsWidth > 0)
            {
                double targetOtherWidth = usableWidth - fillWidth;
                if (targetOtherWidth < 0) targetOtherWidth = 0;

                double overflow = otherColumnsWidth - targetOtherWidth;

                // --- Level 2: 压缩固定列，遵守各列硬下限 ---
                double maxCompressibleAmount = 0;
                var colsToCompress = new System.Collections.Generic.List<GridViewColumn>();

                foreach (var col in otherColumns)
                {
                    string tag = GetColumnTag(col) ?? "";
                    double minW = MinColumnWidths.TryGetValue(tag, out double m) ? m : 30;
                    double currW = double.IsNaN(col.Width) ? col.ActualWidth : col.Width;

                    if (currW > minW)
                    {
                        maxCompressibleAmount += (currW - minW);
                        colsToCompress.Add(col);
                    }
                }

                if (overflow > 0 && maxCompressibleAmount > 0)
                {
                    double compressRatio = Math.Min(1.0, overflow / maxCompressibleAmount);
                    foreach (var col in colsToCompress)
                    {
                        string tag = GetColumnTag(col) ?? "";
                        double minW = MinColumnWidths.TryGetValue(tag, out double m) ? m : 30;
                        double currW = double.IsNaN(col.Width) ? col.ActualWidth : col.Width;

                        double reduceAmount = (currW - minW) * compressRatio;
                        col.Width = currW - reduceAmount;
                        overflow -= reduceAmount;
                    }
                }

                // --- Level 3: 极端情况，按优先级隐藏列 ---
                if (overflow > 1)
                {
                    string[] hidePriority = new[] { "CreatedTime", "ModifiedDate", "Size", "Type" };
                    foreach (var tag in hidePriority)
                    {
                        if (overflow <= 1) break;

                        var colToHide = Enumerable.FirstOrDefault(otherColumns, c => GetColumnTag(c) == tag);
                        if (colToHide != null && (double.IsNaN(colToHide.Width) ? colToHide.ActualWidth : colToHide.Width) > 0)
                        {
                            double currW = double.IsNaN(colToHide.Width) ? colToHide.ActualWidth : colToHide.Width;
                            colToHide.SetValue(IsAutoHiddenProperty, true);
                            colToHide.Width = 0;
                            overflow -= currW;
                        }
                    }
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T t) return t;
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        private string GetColumnTag(GridViewColumn column)
        {
            if (column == null) return null;
            if (column.Header is FrameworkElement fe) return fe.Tag?.ToString();
            return column.Header?.ToString();
        }
    }
}
