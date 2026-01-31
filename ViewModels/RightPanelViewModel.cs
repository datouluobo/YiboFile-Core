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
using System.Windows.Input;
using YiboFile.Services.Navigation;
using YiboFile.Models.Navigation;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 右侧面板信息项
    /// </summary>
    /// <summary>
    /// 信息项基类
    /// </summary>
    public abstract class BaseInfoItem : BaseViewModel
    {
        private string _label;
        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }
    }

    /// <summary>
    /// 文本信息项
    /// </summary>
    public class TextInfoItem : BaseInfoItem
    {
        private string _value;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }

    /// <summary>
    /// 标签信息项
    /// </summary>
    public class TagsInfoItem : BaseInfoItem
    {
        private ObservableCollection<TagViewModel> _tags;
        public ObservableCollection<TagViewModel> Tags
        {
            get => _tags;
            set => SetProperty(ref _tags, value);
        }

        public ICommand TagClickCommand { get; set; }
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
        private readonly INavigationCoordinator _navigationCoordinator;

        private bool _isVisible;
        private double _notesHeight;
        private string _currentNotes;
        private System.Windows.Threading.DispatcherTimer _autoSaveTimer;
        private bool _isUpdatingNotes;
        private FileSystemItem _selectedItem;
        private Library _selectedLibrary;
        private ObservableCollection<BaseInfoItem> _infoItems = new ObservableCollection<BaseInfoItem>();

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

        public ObservableCollection<BaseInfoItem> InfoItems => _infoItems;

        /// <summary>
        /// 当前选中项
        /// </summary>
        public FileSystemItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    if (value != null)
                    {
                        if (_selectedLibrary != null)
                        {
                            _selectedLibrary = null;
                            OnPropertyChanged(nameof(SelectedLibrary));
                        }

                        UpdateInfoItems();

                        // 加载备注
                        _messageBus.Publish(new GetNotesRequestMessage(value.Path));
                    }
                    else
                    {
                        _isUpdatingNotes = true;
                        CurrentNotes = string.Empty;
                        _isUpdatingNotes = false;
                        UpdateInfoItems();
                    }
                }
            }
        }

        /// <summary>
        /// 当前选中库
        /// </summary>
        public Library SelectedLibrary
        {
            get => _selectedLibrary;
            set
            {
                if (SetProperty(ref _selectedLibrary, value))
                {
                    if (value != null)
                    {
                        if (_selectedItem != null)
                        {
                            _selectedItem = null;
                            OnPropertyChanged(nameof(SelectedItem));
                        }

                        _isUpdatingNotes = true;
                        CurrentNotes = string.Empty; // 库目前不支持备注
                        _isUpdatingNotes = false;
                        UpdateInfoItems();
                    }
                    else
                    {
                        UpdateInfoItems();
                    }
                }
            }
        }
        public ICommand TagClickedCommand { get; private set; }

        public RightPanelViewModel(IMessageBus messageBus, ConfigService configService, FileListService fileListService, INavigationCoordinator navigationCoordinator, ITagService tagService = null)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _fileListService = fileListService ?? throw new ArgumentNullException(nameof(fileListService));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _tagService = tagService ?? App.ServiceProvider?.GetService(typeof(ITagService)) as ITagService;

            // 初始化初始值
            var cfg = _configService.Config;
            _isVisible = cfg.IsRightPanelVisible;
            _notesHeight = cfg.RightPanelNotesHeight;

            TagClickedCommand = new RelayCommand<TagViewModel>(OnTagClicked);

            // 订阅消息
            _messageBus.Subscribe<FileSelectionChangedMessage>(OnSelectionChanged);
            _messageBus.Subscribe<LibrarySelectedMessage>(OnLibrarySelected);
            _messageBus.Subscribe<NotesLoadedMessage>(OnNotesLoaded);
            _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);

            // 初始化自动保存定时器
            _autoSaveTimer = new System.Windows.Threading.DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(500);
            _autoSaveTimer.Tick += (s, e) => SaveNotes();



            System.Diagnostics.Debug.WriteLine($"[RightPanelViewModel] Initialized. Visible={IsVisible}, NotesHeight={NotesHeight}");
        }

        private void OnTagClicked(TagViewModel tag)
        {
            if (tag == null) return;

            var request = new NavigationRequest
            {
                Target = NavigationTarget.FromPath($"tag://{tag.Name}"),
                Source = "AddressBar",
                Pane = PaneId.Main
            };

            _navigationCoordinator.NavigateAsync(request);
        }

        private void OnSelectionChanged(FileSelectionChangedMessage message)
        {
            System.Diagnostics.Debug.WriteLine($"[RightPanelViewModel] OnSelectionChanged received. Items: {message.SelectedItems?.Count ?? 0}");
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
                // Only clear if no library is currently selected
                if (SelectedLibrary == null)
                {
                    SelectedItem = null;
                }
            }
        }

        private void OnLibrarySelected(LibrarySelectedMessage message)
        {
            // Force save pending notes before switching
            if (_autoSaveTimer.IsEnabled && SelectedItem != null)
            {
                _autoSaveTimer.Stop();
                SaveNotes();
            }
            SelectedLibrary = message.Library;
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
            if (App.Current == null) return;

            App.Current.Dispatcher.Invoke(() =>
            {
                _infoItems.Clear();
                System.Diagnostics.Debug.WriteLine($"[RightPanelViewModel] UpdateInfoItems called. SelectedItem: {SelectedItem?.Name}, SelectedLibrary: {SelectedLibrary?.Name}");



                if (SelectedItem != null)
                {
                    LoadFileSystemItemInfo(SelectedItem);
                }
                else if (SelectedLibrary != null)
                {
                    LoadLibraryInfo(SelectedLibrary);
                }
                else
                {
                    // Optional: Show "No Selection" state if needed, or leave empty
                    // _infoItems.Add(new TextInfoItem { Label = "信息", Value = "未选择项目" });
                }
            });
        }

        private void LoadFileSystemItemInfo(FileSystemItem item)
        {
            // Existing logic for FileSystemItem...
            _infoItems.Add(new TextInfoItem { Label = "名称", Value = item.Name });
            _infoItems.Add(new TextInfoItem { Label = "路径", Value = item.Path });
            _infoItems.Add(new TextInfoItem { Label = "类型", Value = item.Type ?? (item.IsDirectory ? "文件夹" : "文件") });
            _infoItems.Add(new TextInfoItem { Label = "修改日期", Value = item.ModifiedDate });
            _infoItems.Add(new TextInfoItem { Label = "创建日期", Value = item.CreatedTime });

            if (!item.IsDirectory)
            {
                _infoItems.Add(new TextInfoItem { Label = "大小", Value = item.Size });
                LoadFileDetails(item);
            }
            else
            {
                _infoItems.Add(new TextInfoItem { Label = "项目数", Value = item.Size == "-" ? "正在计算..." : item.Size });
                if (item.Size == "-" || item.Size == "计算中...")
                {
                    LoadDirectoryInfo(item.Path);
                }
            }

            // Tags
            var tagsItem = CreateTagsItem(item.Tags);
            if (tagsItem != null) _infoItems.Add(tagsItem);
        }

        private void LoadLibraryInfo(Library library)
        {
            _infoItems.Add(new TextInfoItem { Label = "库名称", Value = library.Name });
            _infoItems.Add(new TextInfoItem { Label = "包含路径", Value = string.Join(", ", library.Paths) });
        }

        private void LoadFileDetails(FileSystemItem item)
        {
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
                    // Find where to insert (try after Size)
                    int insertIndex = -1;
                    for (int i = 0; i < InfoItems.Count; i++)
                    {
                        if (InfoItems[i] is TextInfoItem ti && ti.Label == "大小")
                        {
                            insertIndex = i + 1;
                            break;
                        }
                    }
                    if (insertIndex == -1) insertIndex = InfoItems.Count; // Fallback to end if "大小" not found
                    InfoItems.Insert(insertIndex, new TextInfoItem { Label = "时长", Value = durationStr });
                }
            }

            // 图片尺寸
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif", ".svg", ".psd", ".ico" };
            if (!string.IsNullOrEmpty(fileExtension) && imageExtensions.Contains(fileExtension))
            {
                var capturedItem = item; // Capture for lambda
                Task.Run(() =>
                {
                    string dimensions = GetImageDimensions(capturedItem.Path);
                    if (!string.IsNullOrEmpty(dimensions))
                    {
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            if (SelectedItem == capturedItem) // 确保选择没变
                            {
                                // Find where to insert (try after Size)
                                int insertIndex = -1;
                                for (int i = 0; i < InfoItems.Count; i++)
                                {
                                    if (InfoItems[i] is TextInfoItem ti && ti.Label == "大小")
                                    {
                                        insertIndex = i + 1;
                                        break;
                                    }
                                }
                                if (insertIndex == -1) insertIndex = InfoItems.Count; // Fallback to end if "大小" not found
                                if (insertIndex > InfoItems.Count) insertIndex = InfoItems.Count;

                                InfoItems.Insert(insertIndex, new TextInfoItem { Label = "尺寸", Value = dimensions });
                            }
                        });
                    }
                });
            }
        }

        private void LoadDirectoryInfo(string directoryPath)
        {
            var filesCountItem = new TextInfoItem { Label = "文件数", Value = "计算中..." };
            var dirsCountItem = new TextInfoItem { Label = "文件夹数", Value = "计算中..." };
            var totalSizeItem = new TextInfoItem { Label = "总大小", Value = "计算中..." };

            // Insert before Tags if possible, or append
            // Tags is last added.
            int insertIndex = InfoItems.Count; // Append to end for now, or find a better place

            InfoItems.Insert(insertIndex, totalSizeItem);
            InfoItems.Insert(insertIndex, dirsCountItem);
            InfoItems.Insert(insertIndex, filesCountItem);


            var capturedPath = directoryPath;
            Task.Run(() =>
            {
                try
                {
                    var files = Directory.GetFiles(capturedPath);
                    var directories = Directory.GetDirectories(capturedPath);
                    long totalSize = 0;

                    foreach (var file in files)
                    {
                        try { totalSize += new FileInfo(file).Length; } catch { }
                    }

                    var filesCountStr = files.Length.ToString();
                    var dirsCountStr = directories.Length.ToString();
                    string totalSizeStr = _fileListService.FormatFileSize(totalSize);

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (capturedPath == SelectedItem?.Path)
                        {
                            filesCountItem.Value = filesCountStr;
                            dirsCountItem.Value = dirsCountStr;
                            totalSizeItem.Value = totalSizeStr;
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[RightPanelViewModel] Error calculating directory info: {ex.Message}");
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (capturedPath == SelectedItem?.Path)
                        {
                            filesCountItem.Value = "错误";
                            dirsCountItem.Value = "错误";
                            totalSizeItem.Value = "-";
                        }
                    });
                }
            });
        }

        private TagsInfoItem CreateTagsItem(string tagsString)
        {
            var item = new TagsInfoItem
            {
                Label = "标签",
                Tags = new ObservableCollection<TagViewModel>(),
                TagClickCommand = TagClickedCommand
            };

            if (!string.IsNullOrWhiteSpace(tagsString))
            {
                var tags = tagsString.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var tagName in tags)
                {
                    var cleanTag = tagName.Trim();
                    if (string.IsNullOrEmpty(cleanTag)) continue;

                    string tagColor = null;
                    try
                    {
                        if (_tagService != null) tagColor = _tagService.GetTagColorByName(cleanTag);
                    }
                    catch { }

                    item.Tags.Add(new TagViewModel
                    {
                        Name = cleanTag,
                        Color = tagColor
                    });
                }
            }
            // Even if empty, return the item (it will show empty list or "-" handled by UI ideally, or we add dummy)
            if (item.Tags.Count == 0)
            {
                // UI can handle empty tags, or we add placeholder? 
                // Let's leave empty and handle in UI Triggers if needed
            }

            return item;
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
