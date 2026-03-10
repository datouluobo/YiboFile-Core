using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile.Controls.Behaviors
{
    public class AutoColumnWidthBehavior
    {
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

            var fillColumns = new System.Collections.Generic.List<GridViewColumn>();
            var otherColumns = new System.Collections.Generic.List<GridViewColumn>();
            double otherColumnsWidth = 0;

            foreach (var col in columns)
            {
                string tag = GetColumnTag(col);
                // 兼容：如果设置了附加属性 IsFillColumn=True，或者 Tag 恰好匹配初始目标名称
                if (GetIsFillColumn(col) || tag == _targetColumnName)
                {
                    fillColumns.Add(col);
                }
                else
                {
                    double w = col.ActualWidth > 0 ? col.ActualWidth : (double.IsNaN(col.Width) ? 0 : col.Width);
                    if (w > 0)
                    {
                        otherColumnsWidth += w;
                        otherColumns.Add(col);
                    }
                }
            }

            if (fillColumns.Count == 0) return;

            double scrollBarWidth = SystemParameters.VerticalScrollBarWidth;
            double availableWidth = _listView.ActualWidth;
            double usableWidth = availableWidth - scrollBarWidth - 2;

            // ── 第一阶段：压缩弹性列（Name）──
            double minFillWidth = 40;
            double fillWidth = usableWidth - otherColumnsWidth;

            if (fillWidth < minFillWidth * fillColumns.Count)
            {
                fillWidth = minFillWidth * fillColumns.Count;
            }

            double widthPerFillCol = fillWidth / fillColumns.Count;
            foreach (var fillCol in fillColumns)
            {
                fillCol.Width = widthPerFillCol;
            }

            // ── 第二阶段：如果弹性列已到下限、总宽度仍然溢出，等比压缩其他列 ──
            double totalUsed = fillWidth + otherColumnsWidth;
            if (totalUsed > usableWidth && otherColumnsWidth > 0)
            {
                // 需要从其他列中整体再砍掉的宽度
                double targetOtherWidth = usableWidth - fillWidth;
                if (targetOtherWidth < 0) targetOtherWidth = 0;

                double scale = targetOtherWidth / otherColumnsWidth;
                double minOtherCol = 30; // 每列的绝对最小宽度

                foreach (var col in otherColumns)
                {
                    double w = col.ActualWidth > 0 ? col.ActualWidth : (double.IsNaN(col.Width) ? 0 : col.Width);
                    double newW = Math.Max(minOtherCol, w * scale);
                    col.Width = newW;
                }
            }
        }

        private string GetColumnTag(GridViewColumn column)
        {
            if (column == null) return null;
            if (column.Header is FrameworkElement fe) return fe.Tag?.ToString();
            return column.Header?.ToString();
        }
    }
}
