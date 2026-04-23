using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YiboFile.Controls.Behaviors
{
    public class ColumnReorderBehavior
    {
        private GridViewHeaderRowPresenter _headerRowPresenter;
        private Canvas _dropIndicatorCanvas;
        private Border _dropIndicator;
        private bool _isColumnDragging;
        private Point _lastMousePos;
        private Action _saveOrderCallback;

        public ColumnReorderBehavior(GridViewHeaderRowPresenter headerRowPresenter, Canvas dropIndicatorCanvas, Border dropIndicator, Action saveOrderCallback)
        {
            _headerRowPresenter = headerRowPresenter;
            _dropIndicatorCanvas = dropIndicatorCanvas;
            _dropIndicator = dropIndicator;
            _saveOrderCallback = saveOrderCallback;

            if (_headerRowPresenter != null)
            {
                _headerRowPresenter.PreviewMouseMove += HeaderRowPresenter_PreviewMouseMove;
                _headerRowPresenter.PreviewMouseLeftButtonUp += HeaderRowPresenter_PreviewMouseLeftButtonUp;
                _headerRowPresenter.MouseLeave += HeaderRowPresenter_MouseLeave;
            }
        }

        public void Detach()
        {
            if (_headerRowPresenter != null)
            {
                _headerRowPresenter.PreviewMouseMove -= HeaderRowPresenter_PreviewMouseMove;
                _headerRowPresenter.PreviewMouseLeftButtonUp -= HeaderRowPresenter_PreviewMouseLeftButtonUp;
                _headerRowPresenter.MouseLeave -= HeaderRowPresenter_MouseLeave;
            }
        }

        private void HeaderRowPresenter_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                if (_isColumnDragging)
                {
                    HideColumnDropIndicator();
                    _isColumnDragging = false;
                }
                return;
            }

            Point mousePos = e.GetPosition(_headerRowPresenter);

            if (!_isColumnDragging)
            {
                if (Math.Abs(mousePos.X - _lastMousePos.X) > 20 || Math.Abs(mousePos.Y - _lastMousePos.Y) > 20)
                {
                    _isColumnDragging = true;
                }
                _lastMousePos = mousePos;
            }

            if (_isColumnDragging)
            {
                UpdateColumnDropIndicator(mousePos);
            }
        }

        private void HeaderRowPresenter_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isColumnDragging)
            {
                HideColumnDropIndicator();
                _isColumnDragging = false;
                
                if (_headerRowPresenter != null && _headerRowPresenter.Dispatcher != null)
                {
                    _headerRowPresenter.Dispatcher.BeginInvoke(new Action(() => _saveOrderCallback?.Invoke()), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        private void HeaderRowPresenter_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isColumnDragging)
            {
                HideColumnDropIndicator();
                _isColumnDragging = false;
            }
        }

        private void UpdateColumnDropIndicator(Point mousePos)
        {
            if (_headerRowPresenter == null || _dropIndicatorCanvas == null || _dropIndicator == null) return;

            _dropIndicatorCanvas.Visibility = Visibility.Visible;
            _dropIndicator.Visibility = Visibility.Visible;

            var headers = GetVisualChildren<GridViewColumnHeader>(_headerRowPresenter)
                .Where(h => h.Visibility == Visibility.Visible && h.ActualWidth > 0 && h.Role == GridViewColumnHeaderRole.Normal)
                .OrderBy(h => h.TranslatePoint(new Point(0, 0), _headerRowPresenter).X)
                .ToList();

            double headerHeight = 28;
            if (headers.Count > 0 && headers[0].ActualHeight > 0)
            {
                headerHeight = headers[0].ActualHeight;
            }

            double newHeight = Math.Max(24, headerHeight - 2);
            _dropIndicator.Height = newHeight;

            if (headers.Count == 0) return;

            double indicatorX = 0;
            foreach (var header in headers)
            {
                Point headerPos = header.TranslatePoint(new Point(0, 0), _headerRowPresenter);
                double headerCenter = headerPos.X + header.ActualWidth / 2;

                if (mousePos.X < headerCenter)
                {
                    indicatorX = headerPos.X;
                    break;
                }
                else
                {
                    indicatorX = headerPos.X + header.ActualWidth;
                }
            }

            double leftOffset = _headerRowPresenter.TranslatePoint(new Point(0, 0), _dropIndicatorCanvas).X;
            Canvas.SetLeft(_dropIndicator, leftOffset + indicatorX - _dropIndicator.ActualWidth / 2);
            Canvas.SetTop(_dropIndicator, 0);
        }

        private void HideColumnDropIndicator()
        {
            if (_dropIndicatorCanvas != null)
            {
                _dropIndicatorCanvas.Visibility = Visibility.Collapsed;
            }
        }

        private static System.Collections.Generic.IEnumerable<T> GetVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t)
                {
                    yield return t;
                }
                foreach (var descendant in GetVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
