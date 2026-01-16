using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        // 调试开关
        private static readonly bool DEBUG_LASSO = false;

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

            if (DEBUG_LASSO)
            {
                Debug.WriteLine($"[Lasso] === OnPreviewMouseLeftButtonDown ===");
                Debug.WriteLine($"[Lasso] OriginalSource Type: {e.OriginalSource?.GetType().Name}");
                Debug.WriteLine($"[Lasso] MouseButton: {e.ChangedButton}, ClickCount: {e.ClickCount}");
                Debug.WriteLine($"[Lasso] Position: {e.GetPosition(_listView)}");
            }

            // 检查是否点击在不应该触发框选的元素上
            if (IsClickOnInteractiveElement(hitElement))
            {
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] 结果: 点击在交互元素上, 不触发框选");

                var listViewItem = FindAncestor<ListViewItem>(hitElement);
                if (listViewItem != null)
                {
                    int index = _listView.ItemContainerGenerator.IndexFromContainer(listViewItem);
                    if (index >= 0)
                    {
                        _anchorIndex = index;
                        if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] 保存锚点索引: {_anchorIndex}");
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
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] 结果: 点在项目上 (HitTest发现ListViewItem), 不触发框选");

                int index = _listView.ItemContainerGenerator.IndexFromContainer(hitItem);
                if (index >= 0)
                {
                    _anchorIndex = index;
                    if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] 保存锚点索引: {_anchorIndex}");
                }
                return;
            }

            if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] 结果: 点在空白区域, 触发框选!");

            // [Archive/Virtual Support] 额外检测：如果是虚拟文件夹或为空，也允许框选
            StartSelection(e);
        }

        private bool IsClickOnInteractiveElement(DependencyObject hitElement)
        {
            if (hitElement == null) return false;

            // 检查滚动条相关元素
            if (FindAncestor<System.Windows.Controls.Primitives.ScrollBar>(hitElement) != null)
            {
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] IsClickOnInteractiveElement: 发现 ScrollBar");
                return true;
            }

            // 检查列标题
            if (FindAncestor<GridViewColumnHeader>(hitElement) != null)
            {
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] IsClickOnInteractiveElement: 发现 GridViewColumnHeader");
                return true;
            }

            // 检查 Thumb 或 RepeatButton (用于滚动条)
            if (FindAncestor<System.Windows.Controls.Primitives.Thumb>(hitElement) != null)
            {
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] IsClickOnInteractiveElement: 发现 Thumb");
                return true;
            }

            if (FindAncestor<System.Windows.Controls.Primitives.RepeatButton>(hitElement) != null)
            {
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] IsClickOnInteractiveElement: 发现 RepeatButton");
                return true;
            }

            // 检查是否由于项目本身拦截（如按钮、复选框等）
            // 如果点击在 ListViewItem 上，不触发框选 - 这是为了让 ListView 自己处理项目选择和拖拽
            var listViewItem = FindAncestor<ListViewItem>(hitElement);
            if (listViewItem != null)
            {
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] IsClickOnInteractiveElement: 发现 ListViewItem (索引: {_listView.ItemContainerGenerator.IndexFromContainer(listViewItem)})");
                return true;
            }

            if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] IsClickOnInteractiveElement: 未发现交互元素");
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
            if (DEBUG_LASSO)
            {
                Debug.WriteLine($"[Lasso] === StartSelection ===");
                Debug.WriteLine($"[Lasso] 当前选中数量: {_listView.SelectedItems.Count}");
                Debug.WriteLine($"[Lasso] Ctrl键按下: {(Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control}");
            }

            // 重置移动计数器
            _moveCount = 0;

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
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] Ctrl模式: 保存了 {_initialSelection.Count} 个初始选中项");
            }
            else
            {
                _listView.SelectedItems.Clear();
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] 普通模式: 清除了所有选中项");
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

            if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] 框选开始, 起点: {_startPoint}");
        }

        private int _moveCount = 0; // 调试用

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isSelecting) return;

            _moveCount++;
            if (DEBUG_LASSO && _moveCount <= 5)
            {
                Debug.WriteLine($"[Lasso] MouseMove #{_moveCount}: 收到移动事件");
            }

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

            // 每隔一段距离输出一次日志，避免刷屏
            if (DEBUG_LASSO && (width > 50 || height > 50) && _moveCount % 20 == 0)
            {
                Debug.WriteLine($"[Lasso] MouseMove: 当前点={currentPoint}, 框大小={width:F0}x{height:F0}");
            }

            // 坐标需要转换回 Canvas 空间显现
            // 因为我们的 Canvas 是 ListView 的兄弟级，理论上如果它们在同一个 Grid 里且都没有偏移，坐标是一致的
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

            if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] === OnPreviewMouseLeftButtonUp ===");
            EndSelection();
            e.Handled = true;
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isSelecting)
            {
                if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] === OnLostMouseCapture (框选被中断) ===");
                EndSelection();
            }
        }

        private void EndSelection()
        {
            if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] === EndSelection ===");

            _isSelecting = false;
            _selectionBox.Visibility = Visibility.Collapsed;
            _listView.ReleaseMouseCapture();

            if (DEBUG_LASSO) Debug.WriteLine($"[Lasso] 框选结束, 最终选中 {_listView.SelectedItems.Count} 项");

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
            // 注意：通过 SelectedItems.Add/Remove 会触发多次事件，但为了实时反馈，可能无法避免
            // 我们只在状态需要改变时才操作
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

