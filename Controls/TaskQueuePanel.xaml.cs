using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using YiboFile.Services.FileOperations.TaskQueue;

namespace YiboFile.Controls
{
    public partial class TaskQueuePanel : UserControl
    {
        private TaskQueueService _queueService;

        public TaskQueuePanel()
        {
            InitializeComponent();

            // Should inject via constructor or property, but for UserControl used in XAML, we often resolve via App.ServiceProvider
            if (App.ServiceProvider != null)
            {
                var service = App.ServiceProvider.GetService(typeof(TaskQueueService)) as TaskQueueService;
                if (service != null)
                {
                    SetService(service);
                }
            }
        }

        public void SetService(TaskQueueService service)
        {
            _queueService = service;
            TasksList.ItemsSource = _queueService.Tasks;
            _queueService.Tasks.CollectionChanged += Tasks_CollectionChanged;
            UpdateVisibility();
        }

        private void Tasks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.Invoke(UpdateVisibility);
        }

        private void UpdateVisibility()
        {
            if (_queueService.Tasks.Count > 0)
            {
                if (this.Visibility != Visibility.Visible)
                    this.Visibility = Visibility.Visible;
            }
            else
            {
                if (this.Visibility != Visibility.Collapsed)
                    this.Visibility = Visibility.Collapsed;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Visibility = Visibility.Collapsed;
        }

        private void ClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            _queueService?.ClearCompleted();
        }

        private void ForceRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            _queueService?.ForceRemoveAll();
        }

        private void PauseResume_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FileOperationTask task)
            {
                if (task.IsPaused)
                    task.Resume();
                else
                    task.Pause();
            }
        }

        private void CancelTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FileOperationTask task)
            {
                task.Cancel();
            }
        }

        #region Drag Support

        private bool _isDragging = false;
        private Point _lastMousePosition;

        private void HeaderBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border header)
            {
                _isDragging = true;
                _lastMousePosition = e.GetPosition(null);
                header.CaptureMouse();
            }
        }

        private void HeaderBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDragging && sender is Border header)
            {
                _isDragging = false;
                header.ReleaseMouseCapture();
            }
        }

        private void HeaderBorder_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && PanelTranslateTransform != null)
            {
                var currentPosition = e.GetPosition(null);
                var offset = currentPosition - _lastMousePosition;

                PanelTranslateTransform.X += offset.X;
                PanelTranslateTransform.Y += offset.Y;

                _lastMousePosition = currentPosition;
            }
        }

        #endregion
    }

    // Converters within the same file for simplicity (or should move to Converters folder)
    public class TaskStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskStatus status)
            {
                return status switch
                {
                    TaskStatus.Running => Brushes.DodgerBlue,
                    // TaskStatus.Percentage does not exist, use Running or remove
                    TaskStatus.Paused => Brushes.Orange,
                    TaskStatus.Completed => Brushes.Green,
                    TaskStatus.Failed => Brushes.Red,
                    TaskStatus.Canceling => Brushes.Gray,
                    TaskStatus.Canceled => Brushes.Gray,
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class PauseResumeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // value is IsPaused (bool)
            if (value is bool isPaused)
            {
                return isPaused ? "继续" : "暂停";
            }
            return "暂停";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// 将 TaskStatus 枚举转换为中文文本
    /// </summary>
    public class TaskStatusToTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskStatus status)
            {
                return status switch
                {
                    TaskStatus.Pending => "等待中",
                    TaskStatus.Running => "进行中",
                    TaskStatus.Paused => "已暂停",
                    TaskStatus.Canceling => "取消中",
                    TaskStatus.Canceled => "已取消",
                    TaskStatus.Completed => "已完成",
                    TaskStatus.Failed => "失败",
                    _ => "未知"
                };
            }
            return "未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    /// <summary>
    /// 将进度值转换为实际宽度
    /// </summary>
    public class ProgressWidthConverter : IMultiValueConverter
    {
        public static readonly ProgressWidthConverter Instance = new ProgressWidthConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 &&
                values[0] is double value &&
                values[1] is double maximum &&
                values[2] is double actualWidth &&
                maximum > 0)
            {
                return (value / maximum) * actualWidth;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

