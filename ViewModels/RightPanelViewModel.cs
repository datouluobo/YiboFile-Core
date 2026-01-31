using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using System.IO;
using YiboFile.Services.FileList;
using YiboFile.Services.Features;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 右侧面板信息项
    /// </summary>
    public class InfoItem : BaseViewModel
    {
        private string _label;
        private string _value;

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    /// <summary>
    /// 右侧面板 ViewModel
    /// 负责管理文件预览、元数据展示和备注逻辑
    /// </summary>
    public class RightPanelViewModel : BaseViewModel
    {
        private readonly IMessageBus _messageBus;
        private readonly ConfigService _configService;
        private readonly FileListService _fileListService;
        private readonly ITagService _tagService;

        private bool _isVisible;
        private double _notesHeight;
        private string _currentNotes;
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        private bool _isUpdatingNotes;
        private FileSystemItem _selectedItem;
        private ObservableCollection<InfoItem> _infoItems = new ObservableCollection<InfoItem>();

        /// <summary>
        /// 右侧面板是否可见
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                {
                    // 同步到配置
                    var cfg = _configService.Config;
                    if (cfg.IsRightPanelVisible != value)
                    {
                        cfg.IsRightPanelVisible = value;
                        _configService.StartDelayedSave();
                    }
                }
            }
        }

        /// <summary>
        /// 备注栏高度
        /// </summary>
        public double NotesHeight
        {
            get => _notesHeight;
            set
            {
                if (SetProperty(ref _notesHeight, value))
                {
                    // 同步到配置
                    var cfg = _configService.Config;
                    if (Math.Abs(cfg.RightPanelNotesHeight - value) > 0.1)
                    {
                        cfg.RightPanelNotesHeight = value;
                        _configService.StartDelayedSave();
                    }
                }
            }
        }

        public string CurrentNotes
        {
            get => _currentNotes;
            set
            {
                if (SetProperty(ref _currentNotes, value))
                {
                    if (!_isUpdatingNotes)
                    {
                        StartAutoSaveTimer();
                    }
                }
            }
        }

        public ObservableCollection<InfoItem> InfoItems
        {
            get => _infoItems;
            set => SetProperty(ref _infoItems, value);
        }

        public FileSystemItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    UpdateInfoItems();
                }
            }
        }

        public RightPanelViewModel(IMessageBus messageBus, ConfigService configService, FileListService fileListService, ITagService tagService = null)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _fileListService = fileListService ?? throw new ArgumentNullException(nameof(fileListService));
            _tagService = tagService ?? App.ServiceProvider?.GetService(typeof(ITagService)) as ITagService;

            // 初始化初始值
            var cfg = _configService.Config;
            _isVisible = cfg.IsRightPanelVisible;
            _notesHeight = cfg.RightPanelNotesHeight;

            // 订阅消息
            _messageBus.Subscribe<FileSelectionChangedMessage>(OnSelectionChanged);
            _messageBus.Subscribe<NotesLoadedMessage>(OnNotesLoaded);
            _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);

            // 初始化自动保存定时器
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
            _autoSaveTimer.Tick += (s, e) => SaveNotes();

            System.Diagnostics.Debug.WriteLine($"[RightPanelViewModel] Initialized. Visible={IsVisible}, NotesHeight={NotesHeight}");
        }

        private void OnSelectionChanged(FileSelectionChangedMessage message)
        {
            if (message.SelectedItems != null && message.SelectedItems.Count > 0)
            {
                // Force save pending notes before switching
                if (_autoSaveTimer.IsEnabled)
                {
                    _autoSaveTimer.Stop();
                    SaveNotes();
                }

                SelectedItem = message.SelectedItems[0] as FileSystemItem;
            }
            else
            {
                SelectedItem = null;
            }
        }

        private void OnNotesLoaded(NotesLoadedMessage message)
        {
            if (SelectedItem != null && message.FilePath == SelectedItem.Path)
            {
                _isUpdatingNotes = true;
                CurrentNotes = message.Notes;
                _isUpdatingNotes = false;
            }
        }

        private void OnNotesUpdated(NotesUpdatedMessage message)
        {
            // If notes were updated from elsewhere, we might want to refresh if it's the current file
            if (SelectedItem != null && message.FilePath == SelectedItem.Path && message.Notes != CurrentNotes)
            {
                _isUpdatingNotes = true;
                CurrentNotes = message.Notes;
                _isUpdatingNotes = false;
            }
        }

        private void StartAutoSaveTimer()
        {
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void SaveNotes()
        {
            _autoSaveTimer.Stop();
            if (SelectedItem != null && !string.IsNullOrEmpty(SelectedItem.Path))
            {
                _messageBus.Publish(new SaveNotesRequestMessage(SelectedItem.Path, CurrentNotes));
            }
        }

        private void UpdateInfoItems()
        {
            InfoItems.Clear();
            if (SelectedItem == null)
            {
                System.Diagnostics.Debug.WriteLine("[RightPanelViewModel] Selection cleared.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[RightPanelViewModel] Updating info for: {SelectedItem.Name}");

            if (SelectedItem.IsDirectory)
            {
                LoadDirectoryInfo();
            }
            else
            {
                LoadFileDetails();
            }
        }

        private void LoadFileDetails()
        {
            var item = SelectedItem;
            InfoItems.Add(new InfoItem { Label = "名称", Value = item.Name });
            InfoItems.Add(new InfoItem { Label = "路径", Value = item.Path });
            InfoItems.Add(new InfoItem { Label = "类型", Value = item.Type });
            InfoItems.Add(new InfoItem { Label = "大小", Value = item.Size });
            InfoItems.Add(new InfoItem { Label = "修改日期", Value = item.ModifiedDate });
            InfoItems.Add(new InfoItem { Label = "标签", Value = string.IsNullOrWhiteSpace(item.Tags) ? "-" : item.Tags });

            var fileExtension = Path.GetExtension(item.Path)?.ToLowerInvariant();

            // 媒体时长
            if (!string.IsNullOrEmpty(fileExtension) &&
                (YiboFile.Services.Search.SearchFilterService.VideoExtensions.Contains(fileExtension) ||
                 YiboFile.Services.Search.SearchFilterService.AudioExtensions.Contains(fileExtension)))
            {
                if (item.DurationMs > 0)
                {
                    TimeSpan t = TimeSpan.FromMilliseconds(item.DurationMs);
                    string durationStr = (t.TotalHours >= 1) ? t.ToString(@"hh\:mm\:ss") : t.ToString(@"mm\:ss");
                    InfoItems.Insert(4, new InfoItem { Label = "时长", Value = durationStr });
                }
            }

            // 图片尺寸
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif", ".svg", ".psd", ".ico" };
            if (!string.IsNullOrEmpty(fileExtension) && imageExtensions.Contains(fileExtension))
            {
                Task.Run(() =>
                {
                    string dimensions = GetImageDimensions(item.Path);
                    if (!string.IsNullOrEmpty(dimensions))
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            if (SelectedItem == item) // 确保选择没变
                            {
                                InfoItems.Insert(4, new InfoItem { Label = "尺寸", Value = dimensions });
                            }
                        });
                    }
                });
            }
        }

        private void LoadDirectoryInfo()
        {
            var item = SelectedItem;
            InfoItems.Add(new InfoItem { Label = "名称", Value = item.Name });
            InfoItems.Add(new InfoItem { Label = "路径", Value = item.Path });
            InfoItems.Add(new InfoItem { Label = "类型", Value = "文件夹" });
            InfoItems.Add(new InfoItem { Label = "修改日期", Value = item.ModifiedDate });
            InfoItems.Add(new InfoItem { Label = "标签", Value = string.IsNullOrWhiteSpace(item.Tags) ? "-" : item.Tags });

            var filesCountItem = new InfoItem { Label = "文件数", Value = "计算中..." };
            var dirsCountItem = new InfoItem { Label = "文件夹数", Value = "计算中..." };
            var totalSizeItem = new InfoItem { Label = "总大小", Value = "计算中..." };

            InfoItems.Add(filesCountItem);
            InfoItems.Add(dirsCountItem);
            InfoItems.Add(totalSizeItem);

            Task.Run(() =>
            {
                try
                {
                    var files = Directory.GetFiles(item.Path);
                    var directories = Directory.GetDirectories(item.Path);
                    long totalSize = 0;

                    foreach (var file in files)
                    {
                        try { totalSize += new FileInfo(file).Length; } catch { }
                    }

                    var filesCountStr = files.Length.ToString();
                    var dirsCountStr = directories.Length.ToString();
                    var totalSizeStr = _fileListService.FormatFileSize(totalSize);

                    App.Current.Dispatcher.Invoke(() =>
                    {
                        if (SelectedItem == item)
                        {
                            filesCountItem.Value = filesCountStr;
                            dirsCountItem.Value = dirsCountStr;
                            totalSizeItem.Value = totalSizeStr;
                        }
                    });
                }
                catch { }
            });
        }

        private string GetImageDimensions(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) return null;
                try
                {
                    using (var image = new ImageMagick.MagickImage(imagePath))
                    {
                        return $"{image.Width} × {image.Height} 像素";
                    }
                }
                catch
                {
                    try
                    {
                        using (var image = System.Drawing.Image.FromFile(imagePath))
                        {
                            return $"{image.Width} × {image.Height} 像素";
                        }
                    }
                    catch { return null; }
                }
            }
            catch { return null; }
        }
    }
}
