using System;
using System.IO;
using System.Windows.Threading;
using YiboFile.Services.FileList;

namespace YiboFile.Services.FileList
{
    /// <summary>
    /// 文件系统监控服务
    /// 负责监控文件系统的变化（创建、删除、重命名、修改），并提供防抖机制
    /// </summary>
    public class FileSystemWatcherService : IDisposable
    {
        #region 字段

        private FileSystemWatcher _fileWatcher;
        private DispatcherTimer _refreshDebounceTimer;
        private string _watchedPath;
        private readonly Dispatcher _dispatcher;
        private readonly object _lock = new object();

        #endregion

        #region 事件定义

        /// <summary>
        /// 文件系统变化事件（创建、删除、重命名、修改）
        /// </summary>
        public event EventHandler<FileSystemEventArgs> FileSystemChanged;

        /// <summary>
        /// 防抖定时器触发后请求刷新事件
        /// </summary>
        public event EventHandler RefreshRequested;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 FileSystemWatcherService
        /// </summary>
        /// <param name="dispatcher">UI线程调度器，用于更新UI</param>
        public FileSystemWatcherService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置文件系统监控
        /// </summary>
        /// <param name="path">要监控的路径，null表示停止监控</param>
        /// <param name="debounceIntervalMs">防抖间隔（毫秒），默认800</param>
        public void SetupFileWatcher(string path, int debounceIntervalMs = 800)
        {
            lock (_lock)
            {
                // 停止并释放旧的监视器
                if (_fileWatcher != null)
                {
                    _fileWatcher.EnableRaisingEvents = false;
                    _fileWatcher.Created -= OnFileSystemChanged;
                    _fileWatcher.Deleted -= OnFileSystemChanged;
                    _fileWatcher.Renamed -= OnFileSystemChanged;
                    _fileWatcher.Changed -= OnFileSystemChanged;
                    _fileWatcher.Dispose();
                    _fileWatcher = null;
                }

                // 停止防抖定时器
                if (_refreshDebounceTimer != null)
                {
                    _refreshDebounceTimer.Stop();
                    _refreshDebounceTimer.Tick -= OnRefreshDebounceTimerTick;
                    _refreshDebounceTimer = null;
                }

                _watchedPath = path;

                if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                {
                    return;
                }

                // 初始化防抖定时器
                _refreshDebounceTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(debounceIntervalMs)
                };
                _refreshDebounceTimer.Tick += OnRefreshDebounceTimerTick;

                try
                {
                    // 创建新的文件系统监视器
                    _fileWatcher = new FileSystemWatcher
                    {
                        Path = path,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                        Filter = "*.*",
                        IncludeSubdirectories = false,
                        InternalBufferSize = 8192
                    };

                    _fileWatcher.Created += OnFileSystemChanged;
                    _fileWatcher.Deleted += OnFileSystemChanged;
                    _fileWatcher.Renamed += OnFileSystemChanged;
                    _fileWatcher.Changed += OnFileSystemChanged;

                    _fileWatcher.EnableRaisingEvents = true;
                }
                catch (Exception)
                {
                }
            }
        }

        /// <summary>
        /// 检查是否正在监控指定路径
        /// </summary>
        /// <param name="path">要检查的路径</param>
        /// <returns>如果正在监控该路径则返回true</returns>
        public bool IsWatching(string path)
        {
            lock (_lock)
            {
                return _fileWatcher != null &&
                       _fileWatcher.EnableRaisingEvents &&
                       string.Equals(_watchedPath, path, StringComparison.OrdinalIgnoreCase);
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 文件系统变化事件处理
        /// </summary>
        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 触发文件系统变化事件
            FileSystemChanged?.Invoke(this, e);

            // 使用防抖机制，避免频繁刷新
            _dispatcher?.BeginInvoke(new Action(() =>
            {
                lock (_lock)
                {
                    if (_refreshDebounceTimer != null)
                    {
                        _refreshDebounceTimer.Stop();
                        _refreshDebounceTimer.Start();
                    }
                    else
                    {
                    }
                }
            }), DispatcherPriority.SystemIdle);
        }

        /// <summary>
        /// 防抖定时器触发事件
        /// </summary>
        private void OnRefreshDebounceTimerTick(object sender, EventArgs e)
        {
            lock (_lock)
            {
                if (_refreshDebounceTimer != null)
                {
                    _refreshDebounceTimer.Stop();
                }

                // 触发刷新请求事件（由外部处理实际的刷新逻辑）
                if (string.IsNullOrEmpty(_watchedPath))
                {
                    return;
                }

                if (!Directory.Exists(_watchedPath))
                {
                    return;
                }
                RefreshRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            SetupFileWatcher(null);
        }

        #endregion
    }
}


