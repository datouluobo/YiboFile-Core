using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using OoiMRR.Controls;
using OoiMRR.Services;
using OoiMRR.Services.FileList;

namespace OoiMRR.ViewModels
{
    /// <summary>
    /// 文件列表 ViewModel
    /// 负责管理文件列表的加载、刷新、排序等功能
    /// </summary>
    public class FileListViewModel : BaseViewModel
    {
        private readonly FileBrowserControl _fileBrowser;
        private readonly Window _ownerWindow;
        private readonly Dispatcher _dispatcher;
        private readonly FileListService _fileListService;

        private ObservableCollection<FileSystemItem> _files = new ObservableCollection<FileSystemItem>();
        private bool _isLoading = false;
        private string _lastSortColumn = "Name";
        private bool _sortAscending = true;
        private FileSystemWatcher _fileWatcher;
        private DispatcherTimer _refreshDebounceTimer;
        private bool _isLoadingFiles = false;
        private SemaphoreSlim _loadFilesSemaphore = new SemaphoreSlim(1, 1);
        // 文件夹大小计算相关（预留，暂未实现）
        // private SemaphoreSlim _folderSizeCalculationSemaphore = new SemaphoreSlim(1, 1);
        // private CancellationTokenSource _folderSizeCalculationCancellation = new CancellationTokenSource();
        // private Queue<string> _pendingFolderSizeCalculations = new Queue<string>();
        // private DispatcherTimer _idleFolderSizeCalculationTimer;

        public ObservableCollection<FileSystemItem> Files
        {
            get => _files;
            set => SetProperty(ref _files, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string LastSortColumn
        {
            get => _lastSortColumn;
            set => SetProperty(ref _lastSortColumn, value);
        }

        public bool SortAscending
        {
            get => _sortAscending;
            set => SetProperty(ref _sortAscending, value);
        }

        public FileListViewModel(FileBrowserControl fileBrowser, Window ownerWindow)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _dispatcher = ownerWindow.Dispatcher;
            _fileListService = new FileListService();

            // 初始化防抖定时器
            _refreshDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(3000)
            };
            _refreshDebounceTimer.Tick += (s, e) =>
            {
                _refreshDebounceTimer.Stop();
                if (!_isLoadingFiles)
                {
                    RefreshFiles();
                }
            };
        }

        /// <summary>
        /// 加载文件列表
        /// </summary>
        public async Task LoadFilesAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                Files.Clear();
                return;
            }

            await _loadFilesSemaphore.WaitAsync();
            try
            {
                if (_isLoadingFiles)
                    return;

                _isLoadingFiles = true;
                IsLoading = true;

                await Task.Run(() =>
                {
                    _dispatcher.Invoke(() =>
                    {
                        Files.Clear();
                    });

                    try
                    {
                        // 使用 FileListService 加载文件列表
                        var files = _fileListService.LoadFileSystemItems(path);

                        // 排序
                        SortFiles(files);

                        // 更新 UI
                        _dispatcher.Invoke(() =>
                        {
                            foreach (var item in files)
                            {
                                Files.Add(item);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(_ownerWindow, $"加载文件列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });

                // 更新 FileBrowser
                _dispatcher.Invoke(() =>
                {
                    if (_fileBrowser != null)
                    {
                        _fileBrowser.FilesItemsSource = Files;
                    }
                });
            }
            finally
            {
                _isLoadingFiles = false;
                IsLoading = false;
                _loadFilesSemaphore.Release();
            }
        }

        /// <summary>
        /// 刷新文件列表
        /// </summary>
        public void RefreshFiles()
        {
            // 这个方法需要由外部调用时传入当前路径
            // 这里只是占位，实际实现需要根据当前导航模式决定
        }

        /// <summary>
        /// 设置文件监视器
        /// </summary>
        public void SetupFileWatcher(string path)
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            try
            {
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"无法设置文件监视器: {ex.Message}");
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            if (_isLoadingFiles)
                return;

            _dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_isLoadingFiles && _refreshDebounceTimer != null)
                {
                    _refreshDebounceTimer.Stop();
                    _refreshDebounceTimer.Start();
                }
            }), DispatcherPriority.SystemIdle);
        }

        /// <summary>
        /// 排序文件列表
        /// </summary>
        public void SortFiles(List<FileSystemItem> files, string column = null, bool? ascending = null)
        {
            if (files == null || files.Count == 0)
                return;

            string sortColumn = column ?? LastSortColumn;
            bool sortAscending = ascending ?? SortAscending;

            switch (sortColumn)
            {
                case "Name":
                    files.Sort((a, b) => sortAscending
                        ? string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                        : string.Compare(b.Name, a.Name, StringComparison.OrdinalIgnoreCase));
                    break;
                case "Type":
                    files.Sort((a, b) => sortAscending
                        ? string.Compare(a.Type, b.Type, StringComparison.OrdinalIgnoreCase)
                        : string.Compare(b.Type, a.Type, StringComparison.OrdinalIgnoreCase));
                    break;
                case "Size":
                    files.Sort((a, b) =>
                    {
                        long sizeA = ParseFileSize(a.Size);
                        long sizeB = ParseFileSize(b.Size);
                        return sortAscending ? sizeA.CompareTo(sizeB) : sizeB.CompareTo(sizeA);
                    });
                    break;
                case "ModifiedDate":
                    files.Sort((a, b) =>
                    {
                        DateTime dateA = ParseDate(a.ModifiedDate);
                        DateTime dateB = ParseDate(b.ModifiedDate);
                        return sortAscending ? dateA.CompareTo(dateB) : dateB.CompareTo(dateA);
                    });
                    break;
            }

            LastSortColumn = sortColumn;
            SortAscending = sortAscending;
        }

        private long ParseFileSize(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr))
                return 0;

            sizeStr = sizeStr.Trim().ToUpper();
            if (sizeStr.EndsWith("B"))
            {
                sizeStr = sizeStr.Substring(0, sizeStr.Length - 1).Trim();
            }

            if (sizeStr.EndsWith("KB"))
            {
                if (double.TryParse(sizeStr.Substring(0, sizeStr.Length - 2).Trim(), out double kb))
                    return (long)(kb * 1024);
            }
            else if (sizeStr.EndsWith("MB"))
            {
                if (double.TryParse(sizeStr.Substring(0, sizeStr.Length - 2).Trim(), out double mb))
                    return (long)(mb * 1024 * 1024);
            }
            else if (sizeStr.EndsWith("GB"))
            {
                if (double.TryParse(sizeStr.Substring(0, sizeStr.Length - 2).Trim(), out double gb))
                    return (long)(gb * 1024 * 1024 * 1024);
            }
            else if (long.TryParse(sizeStr, out long bytes))
            {
                return bytes;
            }

            return 0;
        }

        private DateTime ParseDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out DateTime result))
                return result;
            return DateTime.MinValue;
        }


        public void Dispose()
        {
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            _refreshDebounceTimer?.Stop();
            _loadFilesSemaphore?.Dispose();
            // _folderSizeCalculationSemaphore?.Dispose();
            // _folderSizeCalculationCancellation?.Cancel();
        }
    }
}

