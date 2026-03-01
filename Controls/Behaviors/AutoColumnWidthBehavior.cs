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
            AdjustTargetColumnWidth();
        }

        public void AdjustTargetColumnWidth()
        {
            if (_listView == null || !_listView.IsLoaded) return;
            if (!(_listView.View is GridView gridView)) return;

            var columns = gridView.Columns;
            if (columns == null || columns.Count == 0) return;

            var fillColumns = new System.Collections.Generic.List<GridViewColumn>();
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
                    // Compute actual width or specified width
                    double w = col.ActualWidth > 0 ? col.ActualWidth : (double.IsNaN(col.Width) ? 0 : col.Width);
                    if (w > 0)
                    {
                        otherColumnsWidth += w;
                    }
                }
            }

            if (fillColumns.Count == 0) return;

            double scrollBarWidth = SystemParameters.VerticalScrollBarWidth;
            double availableWidth = _listView.ActualWidth;
            
            // Subtract scrollbar and margins
            double totalFillWidth = availableWidth - otherColumnsWidth - scrollBarWidth - 20;
            
            double minWidthPerColumn = 120;
            if (totalFillWidth < (minWidthPerColumn * fillColumns.Count)) 
            {
                totalFillWidth = minWidthPerColumn * fillColumns.Count;
            }

            double widthPerFillCol = totalFillWidth / fillColumns.Count;

            foreach (var fillCol in fillColumns)
            {
                fillCol.Width = widthPerFillCol;
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
