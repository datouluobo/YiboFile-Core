using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using YiboFile.Services.FileOperations.TaskQueue;

namespace YiboFile.Controls
{
    public partial class NavTaskQueueView : UserControl, INotifyPropertyChanged
    {
        private TaskQueueService _queueService;

        public event PropertyChangedEventHandler PropertyChanged;
        
        public string TaskCountText => _queueService != null && _queueService.Tasks.Count > 0
            ? $"后台任务 ({_queueService.Tasks.Count})" 
            : "后台任务";

        public NavTaskQueueView()
        {
            InitializeComponent();
            DataContext = this;

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
            foreach (var task in _queueService.Tasks)
            {
                task.PropertyChanged += Task_PropertyChanged;
            }
            UpdateVisibility();
        }

        private void Tasks_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                if (e.NewItems != null)
                {
                    foreach (FileOperationTask item in e.NewItems)
                        item.PropertyChanged += Task_PropertyChanged;
                }
                if (e.OldItems != null)
                {
                    foreach (FileOperationTask item in e.OldItems)
                        item.PropertyChanged -= Task_PropertyChanged;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TaskCountText)));
                UpdateVisibility();
            });
        }

        private void Task_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FileOperationTask.IsSilent))
            {
                Dispatcher.Invoke(() =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TaskCountText)));
                    UpdateVisibility();
                });
            }
        }

        private void UpdateVisibility()
        {
            if (_queueService == null) return;

            bool hasTasks = _queueService.Tasks.Any(t => !t.IsSilent);

            if (hasTasks)
            {
                // 每次有新任务进入且未全部完成时，自动展开
                bool hasActiveTasks = _queueService.Tasks.Any(t => !t.IsSilent && (t.Status == TaskStatus.Running || t.Status == TaskStatus.Pending));
                if (hasActiveTasks && ExpandToggle.IsChecked == false)
                {
                    ExpandToggle.IsChecked = true;
                }
            }
        }

        private void HeaderBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 点击标题栏展开折叠
            ExpandToggle.IsChecked = !ExpandToggle.IsChecked;
            e.Handled = true;
        }

        private void ClearCompleted_Click(object sender, RoutedEventArgs e)
        {
            _queueService?.ClearCompleted();
            e.Handled = true;
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
            e.Handled = true;
        }

        private void CancelTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is FileOperationTask task)
            {
                task.Cancel();
            }
            e.Handled = true;
        }
        
        private void ExpandToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (ListScrollViewer != null)
                ListScrollViewer.Visibility = Visibility.Visible;
            if (ExpandArrow != null)
                ExpandArrow.RenderTransform = new RotateTransform(180);
        }

        private void ExpandToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (ListScrollViewer != null)
                ListScrollViewer.Visibility = Visibility.Collapsed;
            if (ExpandArrow != null)
                ExpandArrow.RenderTransform = new RotateTransform(0);
        }
    }

    /* Converters 迁移到了这里，增加 Nav 前缀防止命名冲突 */
    public class NavTaskStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskStatus status)
            {
                return status switch
                {
                    TaskStatus.Running => Brushes.DodgerBlue,
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

    public class NavStatusToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TaskStatus status)
            {
                string action = parameter as string;
                return action switch
                {
                    "CanPause" => (status == TaskStatus.Running || status == TaskStatus.Paused) ? Visibility.Visible : Visibility.Collapsed,
                    "CanCancel" => (status == TaskStatus.Running || status == TaskStatus.Paused || status == TaskStatus.Pending) ? Visibility.Visible : Visibility.Collapsed,
                    _ => Visibility.Collapsed
                };
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
    
    public class NavProgressWidthConverter : IMultiValueConverter
    {
        public static readonly NavProgressWidthConverter Instance = new NavProgressWidthConverter();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 &&
                values[0] is double value &&
                values[1] is double maximum &&
                values[2] is double actualWidth &&
                maximum > 0 && actualWidth > 0 && !double.IsInfinity(actualWidth))
            {
                return (value / maximum) * actualWidth;
            }
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
