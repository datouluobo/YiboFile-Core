using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace YiboFile.Controls.Behaviors
{
    /// <summary>
    /// 为 ListView 提供鼠标框选（Lasso Selection）功能
    /// 模拟 Windows 资源管理器的选择行为
    /// </summary>
    public class LassoSelectionBehavior
    {
        private readonly ListView _listView;
        private readonly Canvas _selectionCanvas;
        private readonly Rectangle _selectionBox;

        private bool _isSelecting;
        private Point _startPoint;
        private int _anchorIndex = -1;
        private HashSet<object> _initialSelection = new();

        // 选择框样式
        private static readonly SolidColorBrush SelectionFillBrush = new(Color.FromArgb(60, 0, 120, 215));
        private static readonly SolidColorBrush SelectionStrokeBrush = new(Color.FromArgb(255, 0, 120, 215));

        public LassoSelectionBehavior(ListView listView, Canvas selectionCanvas)
        {
            _listView = listView ?? throw new ArgumentNullException(nameof(listView));
            _selectionCanvas = selectionCanvas ?? throw new ArgumentNullException(nameof(selectionCanvas));

            // 创建选择框
            _selectionBox = new Rectangle
            {
                Fill = SelectionFillBrush,
                Stroke = SelectionStrokeBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 2 }, // 虚线边框
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            _selectionCanvas.Children.Add(_selectionBox);

            // Canvas 不拦截鼠标事件
            _selectionCanvas.IsHitTestVisible = false;
            _selectionCanvas.Background = null;

            // 在 ListView 上使用 Preview 事件
            _listView.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            _listView.PreviewMouseMove += OnPreviewMouseMove;
            _listView.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            _listView.LostMouseCapture += OnLostMouseCapture;
        }

        public void Detach()
        {
            _listView.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            _listView.PreviewMouseMove -= OnPreviewMouseMove;
            _listView.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            _listView.LostMouseCapture -= OnLostMouseCapture;
            _selectionCanvas.Children.Remove(_selectionBox);
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hitElement = e.OriginalSource as DependencyObject;

            // 检查是否点击在不应该触发框选的元素上
            if (IsClickOnInteractiveElement(hitElement))
            {
                var listViewItem = FindAncestor<ListViewItem>(hitElement);
                if (listViewItem != null)
                {
                    int index = _listView.ItemContainerGenerator.IndexFromContainer(listViewItem);
                    if (index >= 0)
                    {
                        _anchorIndex = index;
                    }
                }
                return;
            }

            // 使用命中测试确认点击位置，如果点在项目或其子项上，则不启动框选
            Point clickPoint = _listView.PointFromScreen(_listView.PointToScreen(e.GetPosition(_listView))); // 兼容某些子元素坐标偏移
            clickPoint = e.GetPosition(_listView);

            var hitItem = GetItemAtPoint(clickPoint);
            if (hitItem != null)
            {
                int index = _listView.ItemContainerGenerator.IndexFromContainer(hitItem);
                if (index >= 0)
                {
                    _anchorIndex = index;
                }
                return;
            }

            // [Archive/Virtual Support] 额外检测：如果是虚拟文件夹或为空，也允许框选
            StartSelection(e);
        }

        private bool IsClickOnInteractiveElement(DependencyObject hitElement)
        {
            if (hitElement == null) return false;

            // 检查 TextBox（如重命名输入框）
            if (FindAncestor<System.Windows.Controls.TextBox>(hitElement) != null)
            {
                return true;
            }

            // 检查滚动条相关元素
            if (FindAncestor<System.Windows.Controls.Primitives.ScrollBar>(hitElement) != null)
            {
                return true;
            }

            // 检查列标题
            if (FindAncestor<GridViewColumnHeader>(hitElement) != null)
            {
                return true;
            }

            // 检查 Thumb 或 RepeatButton (用于滚动条)
            if (FindAncestor<System.Windows.Controls.Primitives.Thumb>(hitElement) != null)
            {
                return true;
            }

            if (FindAncestor<System.Windows.Controls.Primitives.RepeatButton>(hitElement) != null)
            {
                return true;
            }

            // 检查是否由于项目本身拦截（如按钮、复选框等）
            var listViewItem = FindAncestor<ListViewItem>(hitElement);
            if (listViewItem != null)
            {
                return true;
            }

            return false;
        }

        private ListViewItem GetItemAtPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(_listView, point);
            if (result?.VisualHit != null)
            {
                return FindAncestor<ListViewItem>(result.VisualHit);
            }
            return null;
        }

        private void StartSelection(MouseButtonEventArgs e)
        {
            // 使用相对于 ListView 的坐标
            _startPoint = e.GetPosition(_listView);
            _isSelecting = true;

            // 保存初始选择状态
            _initialSelection.Clear();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                foreach (var item in _listView.SelectedItems)
                {
                    _initialSelection.Add(item);
                }
            }
            else
            {
                _listView.SelectedItems.Clear();
            }

            // 初始化选择框
            Canvas.SetLeft(_selectionBox, _startPoint.X);
            Canvas.SetTop(_selectionBox, _startPoint.Y);
            _selectionBox.Width = 0;
            _selectionBox.Height = 0;
            _selectionBox.Visibility = Visibility.Visible;

            _listView.CaptureMouse();
            e.Handled = true;
            _listView.Focus();
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;

            // 使用相对于 ListView 的坐标
            Point currentPoint = e.GetPosition(_listView);

            // 计算选择框的位置和大小
            double x = Math.Min(_startPoint.X, currentPoint.X);
            double y = Math.Min(_startPoint.Y, currentPoint.Y);
            double width = Math.Abs(currentPoint.X - _startPoint.X);
            double height = Math.Abs(currentPoint.Y - _startPoint.Y);

            // 限制在边界内
            double maxWidth = _listView.ActualWidth;
            double maxHeight = _listView.ActualHeight;

            x = Math.Max(0, Math.Min(x, maxWidth));
            y = Math.Max(0, Math.Min(y, maxHeight));
            width = Math.Min(width, maxWidth - x);
            height = Math.Min(height, maxHeight - y);

            // 坐标需要转换回 Canvas 空间显现
            Canvas.SetLeft(_selectionBox, x);
            Canvas.SetTop(_selectionBox, y);
            _selectionBox.Width = Math.Max(0, width);
            _selectionBox.Height = Math.Max(0, height);

            // 实时更新选中项
            Rect selectionRect = new Rect(x, y, width, height);
            UpdateSelectionRealtime(selectionRect);

            e.Handled = true;
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isSelecting) return;

            EndSelection();
            e.Handled = true;
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isSelecting)
            {
                EndSelection();
            }
        }

        private void EndSelection()
        {
            _isSelecting = false;
            _selectionBox.Visibility = Visibility.Collapsed;
            _listView.ReleaseMouseCapture();
            _initialSelection.Clear();
        }

        private void UpdateSelectionRealtime(Rect selectionRect)
        {
            bool isCtrlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            // 性能优化：仅通过视觉树查找当前已生成容器的 ListViewItem (即可见项)
            var containers = FindVisualChildren<ListViewItem>(_listView).ToList();

            // 为了批量操作提高性能，先计算应该选中的项
            var itemsToSelect = new List<object>();
            var itemsToDeselect = new List<object>();

            foreach (var container in containers)
            {
                if (container.Visibility != Visibility.Visible) continue;

                try
                {
                    // 获取项目相对于 ListView 的位置
                    GeneralTransform transform = container.TransformToAncestor(_listView);
                    Point topLeft = transform.Transform(new Point(0, 0));
                    Rect itemBounds = new Rect(topLeft, new Size(container.ActualWidth, container.ActualHeight));

                    // 检查是否与选择框相交
                    bool isIntersecting = selectionRect.IntersectsWith(itemBounds);
                    var item = _listView.ItemContainerGenerator.ItemFromContainer(container);
                    if (item == DependencyProperty.UnsetValue) continue;

                    if (isCtrlPressed)
                    {
                        // Ctrl 模式：切换初始状态之外的状态
                        bool wasInitiallySelected = _initialSelection.Contains(item);
                        bool shouldBeSelected = isIntersecting ? !wasInitiallySelected : wasInitiallySelected;

                        if (shouldBeSelected) itemsToSelect.Add(item);
                        else itemsToDeselect.Add(item);
                    }
                    else
                    {
                        // 普通模式
                        if (isIntersecting) itemsToSelect.Add(item);
                        else itemsToDeselect.Add(item);
                    }
                }
                catch { continue; }
            }

            // 应用选择变更
            foreach (var item in itemsToSelect)
            {
                if (!_listView.SelectedItems.Contains(item))
                    _listView.SelectedItems.Add(item);
            }
            foreach (var item in itemsToDeselect)
            {
                if (_listView.SelectedItems.Contains(item))
                    _listView.SelectedItems.Remove(item);
            }
        }


        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
    }
}

