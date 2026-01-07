using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using OoiMRR.Services.FileOperations.TaskQueue;

namespace OoiMRR.Controls
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
}
