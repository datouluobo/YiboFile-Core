using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace YiboFile.Services.FileOperations.TaskQueue
{
    /// <summary>
    /// 表示文件操作任务的状态
    /// </summary>
    public enum TaskStatus
    {
        Pending,
        Running,
        Paused,
        Canceling,
        Canceled,
        Completed,
        Failed
    }

    /// <summary>
    /// 表示一个文件操作任务
    /// </summary>
    public class FileOperationTask : INotifyPropertyChanged
    {
        private TaskStatus _status;
        private double _progress;
        private string _currentFile;
        private string _description;
        private long _totalBytes;
        private long _processedBytes;
        private int _totalItems;
        private int _processedItems;
        private bool _isSilent;
        private DateTime _startTime;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 任务唯一ID
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// 是否静默运行（不自动弹出任务面板）
        /// </summary>
        public bool IsSilent
        {
            get => _isSilent;
            set => SetProperty(ref _isSilent, value);
        }

        /// <summary>
        /// 任务开始时间
        /// </summary>
        public DateTime StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        /// <summary>
        /// 任务描述（如"复制到 X"）
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// 当前状态
        /// </summary>
        public TaskStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// 总进度 (0-100)
        /// </summary>
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        /// <summary>
        /// 当前正在处理的文件名
        /// </summary>
        public string CurrentFile
        {
            get => _currentFile;
            set => SetProperty(ref _currentFile, value);
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set => SetProperty(ref _totalBytes, value);
        }

        public long ProcessedBytes
        {
            get => _processedBytes;
            set => SetProperty(ref _processedBytes, value);
        }

        public int TotalItems
        {
            get => _totalItems;
            set => SetProperty(ref _totalItems, value);
        }

        public int ProcessedItems
        {
            get => _processedItems;
            set => SetProperty(ref _processedItems, value);
        }

        // 控制信号
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
        private readonly ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true);

        public bool IsPaused => !_pauseEvent.IsSet;

        public void Pause()
        {
            if (Status == TaskStatus.Running)
            {
                Status = TaskStatus.Paused;
                _pauseEvent.Reset();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPaused)));
            }
        }

        public void Resume()
        {
            if (Status == TaskStatus.Paused)
            {
                Status = TaskStatus.Running;
                _pauseEvent.Set();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPaused)));
            }
        }

        public void Cancel()
        {
            if (Status != TaskStatus.Completed && Status != TaskStatus.Failed && Status != TaskStatus.Canceled)
            {
                Status = TaskStatus.Canceling;
                CancellationTokenSource.Cancel();
                // 如果暂停中取消，必须释放暂停锁，否则任务永远卡在 wait
                _pauseEvent.Set();
            }
        }

        /// <summary>
        /// 在循环中调用此方法以响应暂停
        /// </summary>
        public void WaitIfPaused()
        {
            _pauseEvent.Wait();
        }

        protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}

