using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace OoiMRR.Services.FileOperations.TaskQueue
{
    public class TaskQueueService
    {
        private readonly ObservableCollection<FileOperationTask> _tasks = new ObservableCollection<FileOperationTask>();

        /// <summary>
        /// 任务列表（UI绑定源）
        /// </summary>
        public ObservableCollection<FileOperationTask> Tasks => _tasks;

        /// <summary>
        /// 添加新任务
        /// </summary>
        public void EnqueueTask(FileOperationTask task)
        {
            if (task == null) return;
            // UI thread check usually needed for ObservableCollection if bound directly?
            // Actually ObservableCollection usually requires UI thread or EnableCollectionSynchronization.
            // For now assuming invocation on UI thread or using BindingOperations.EnableCollectionSynchronization in UI.
            // We'll let UI handle synchronization or dispatch here if needed.
            // But since this is a global service, it might be called from background.
            // We should use Application.Current.Dispatcher for safety if possible, or leave it to caller.
            // Let's use Dispatcher to be safe.

            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => _tasks.Add(task));
            }
            else
            {
                _tasks.Add(task);
            }
        }

        /// <summary>
        /// 移除任务
        /// </summary>
        public void RemoveTask(FileOperationTask task)
        {
            if (task == null) return;
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() => _tasks.Remove(task));
            }
            else
            {
                _tasks.Remove(task);
            }
        }

        /// <summary>
        /// 清理已完成的任务
        /// </summary>
        public void ClearCompleted()
        {
            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var completed = _tasks.Where(t => t.Status == TaskStatus.Completed || t.Status == TaskStatus.Canceled || t.Status == TaskStatus.Failed).ToList();
                    foreach (var t in completed) _tasks.Remove(t);
                });
            }
        }
    }
}
