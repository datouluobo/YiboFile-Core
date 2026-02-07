using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using YiboFile.Models;
using YiboFile.Controls;
using YiboFile.Services;
using YiboFile.Services.Core;
using YiboFile.Services.Features;
using YiboFile.Services.FileList;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Favorite;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Config;
using YiboFile.Services.Navigation;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 面板 ViewModel，管理单个面板的完整状态
    /// 支持主面板和副面板，可独立运作
    /// </summary>
    public class PaneViewModel : BaseViewModel, IPaneContext, IDisposable
    {
        #region Private Fields

        private readonly Dispatcher _dispatcher;
        private readonly bool _isSecondary;
        private readonly FileListService _fileListService;
        private readonly LibraryService _libraryService;
        private readonly FavoriteService _favoriteService;
        private readonly ITagService _tagService;
        private readonly IMessageBus _messageBus;
        private readonly FolderSizeCalculationService _folderSizeService;

        private string _currentPath;
        private Library _currentLibrary;
        private TagViewModel _currentTag;
        private string _navigationMode = "Path";
        private bool _isActive;
        private bool _isLoadingDisabled;
        private ObservableCollection<FileSystemItem> _files;
        private ObservableCollection<FileSystemItem> _selectedItems = new ObservableCollection<FileSystemItem>();
        private bool _isLoading;

        private CancellationTokenSource _loadCts;
        private FileSystemWatcher _fileWatcher;
        private DispatcherTimer _refreshDebounceTimer;
        private string _searchStatusText;
        private bool _isSearching;
        private readonly System.Collections.Generic.Stack<string> _backStack = new System.Collections.Generic.Stack<string>();
        private readonly System.Collections.Generic.Stack<string> _forwardStack = new System.Collections.Generic.Stack<string>();
        private bool _isNavigatingHistory;

        private ObservableCollection<ContextMenuItemViewModel> _libraryMenuItems = new ObservableCollection<ContextMenuItemViewModel>();
        private ObservableCollection<ContextMenuItemViewModel> _favoriteMenuItems = new ObservableCollection<ContextMenuItemViewModel>();
        private ObservableCollection<ContextMenuItemViewModel> _tagMenuItems = new ObservableCollection<ContextMenuItemViewModel>();
        private string _fileViewMode = "List";

        #endregion

        #region Events

        /// <summary>
        /// 视图模式变更事件
        /// </summary>
        public event EventHandler<string> ViewModeChanged;

        /// <summary>
        /// 路径变更事件
        /// </summary>
        public event EventHandler<string> PathChanged;

        /// <summary>
        /// 库变更事件
        /// </summary>
        public event EventHandler<Library> LibraryChanged;

        /// <summary>
        /// 标签变更事件
        /// </summary>
        public event EventHandler<TagViewModel> TagChanged;

        /// <summary>
        /// 导航模式变更事件
        /// </summary>
        public event EventHandler<string> NavigationModeChanged;

        /// <summary>
        /// 文件列表加载完成事件
        /// </summary>
        public event EventHandler<ObservableCollection<FileSystemItem>> FilesLoaded;

        #endregion

        #region Properties

        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                // 核心修复：防止路径闪变
                // 如果当前已经是虚拟路径（如 lib://），且尝试设置的是一个普通物理路径（不含协议头）
                // 且该物理路径不包含协议标志，则我们应该谨慎对待，或者保持虚拟路径的“身份”

                string oldValue = _currentPath;
                if (SetProperty(ref _currentPath, value))
                {
                    // 自动根据路径前缀同步导航模式
                    if (value != null)
                    {
                        if (value.StartsWith("lib://", StringComparison.OrdinalIgnoreCase))
                        {
                            _navigationMode = "Library";
                            OnPropertyChanged(nameof(NavigationMode));
                        }
                        else if (value.StartsWith("tag://", StringComparison.OrdinalIgnoreCase))
                        {
                            _navigationMode = "Tag";
                            OnPropertyChanged(nameof(NavigationMode));
                        }
                        else if (value.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
                        {
                            _navigationMode = "Search";
                            OnPropertyChanged(nameof(NavigationMode));
                        }
                        else if (!string.IsNullOrEmpty(value) && !ProtocolManager.IsVirtual(value))
                        {
                            // 只有在明确不是虚拟路径且非上述特殊协议时才切换到 Path 模式
                            if (_navigationMode != "Path")
                            {
                                _navigationMode = "Path";
                                OnPropertyChanged(nameof(NavigationMode));
                                // 切换到普通路径模式时，清除库和标签的选中状态，防止残留
                                CurrentLibrary = null;
                                CurrentTag = null;
                            }
                        }
                    }
                    PathChanged?.Invoke(this, value);
                }
            }
        }

        public Library CurrentLibrary
        {
            get => _currentLibrary;
            set
            {
                if (SetProperty(ref _currentLibrary, value))
                {
                    LibraryChanged?.Invoke(this, value);
                }
            }
        }

        public TagViewModel CurrentTag
        {
            get => _currentTag;
            set
            {
                if (SetProperty(ref _currentTag, value))
                {
                    TagChanged?.Invoke(this, value);
                }
            }
        }

        public string NavigationMode
        {
            get => _navigationMode;
            set
            {
                if (SetProperty(ref _navigationMode, value))
                {
                    NavigationModeChanged?.Invoke(this, value);
                }
            }
        }

        public ObservableCollection<FileSystemItem> Files
        {
            get => FileList?.Files ?? _files;
        }

        public ObservableCollection<FileSystemItem> SelectedItems
        {
            get => _selectedItems;
            private set => SetProperty(ref _selectedItems, value);
        }

        private FileSystemItem _selectedItem;
        public FileSystemItem SelectedItem
        {
            get => _selectedItem;
            private set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(IsSingleSelection));
                    OnPropertyChanged(nameof(IsNoSelection));
                }
            }
        }

        public bool HasSelection => SelectedItems != null && SelectedItems.Count > 0;
        public bool IsSingleSelection => SelectedItems != null && SelectedItems.Count == 1;
        public bool IsNoSelection => SelectedItems == null || SelectedItems.Count == 0;

        public void UpdateSelection(System.Collections.IList items)
        {
            _selectedItems.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item is FileSystemItem fsItem)
                    {
                        _selectedItems.Add(fsItem);
                    }
                }
            }
            SelectedItem = _selectedItems.FirstOrDefault();

            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsSingleSelection));
            OnPropertyChanged(nameof(IsNoSelection));

            // 更新动态菜单项
            UpdateDynamicMenuItems();

            // 发送消息以便其他模块（如预览面板）同步
            if (SelectedItem != null)
            {
                // 如果只选择了一个项，请求预览
                _messageBus.Publish(new FileSelectionChangedMessage(SelectedItems.ToList()));

                // 如果是文件夹且大小未计算，触发计算
                if (SelectedItem.IsDirectory && (string.IsNullOrEmpty(SelectedItem.Size) || SelectedItem.Size == "-" || SelectedItem.Size == "计算中..."))
                {
                    _folderSizeService?.CalculateAndUpdateFolderSizeAsync(SelectedItem.Path);
                }
            }
            else
            {
                // 无选择时通知
                _messageBus.Publish(new FileSelectionChangedMessage(null));
            }
        }

        /// <summary>
        /// 是否为当前激活的面板
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// 是否禁用加载功能
        /// </summary>
        public bool IsLoadingDisabled
        {
            get => _isLoadingDisabled;
            set => SetProperty(ref _isLoadingDisabled, value);
        }

        public string FileViewMode
        {
            get => _fileViewMode;
            set
            {
                if (SetProperty(ref _fileViewMode, value))
                {
                    ViewModeChanged?.Invoke(this, value);
                    OnPropertyChanged(nameof(ViewModeIcon));
                    // 同步到全局配置
                    ConfigurationService.Instance.Set(cfg => cfg.FileViewMode, value);
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public object ViewModeIcon
        {
            get
            {
                return FileViewMode switch
                {
                    "Thumbnail" or "Tiles" or "SmallIcons" => Application.Current.TryFindResource("Icon_ViewThumb"),
                    _ => Application.Current.TryFindResource("Icon_ViewList")
                };
            }
        }

        private FileListViewModel _fileList;
        public FileListViewModel FileList
        {
            get => _fileList;
            set
            {
                var oldFileList = _fileList;
                if (SetProperty(ref _fileList, value))
                {
                    if (oldFileList != null)
                    {
                        oldFileList.PropertyChanged -= OnFileListPropertyChanged;
                    }
                    if (_fileList != null)
                    {
                        _fileList.PropertyChanged += OnFileListPropertyChanged;
                    }
                    OnPropertyChanged(nameof(Files));
                }
            }
        }

        private void OnFileListPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FileListViewModel.Files))
            {
                OnPropertyChanged(nameof(Files));
            }
        }

        public string SearchStatusText
        {
            get => _searchStatusText;
            set => SetProperty(ref _searchStatusText, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public bool IsSecondary => _isSecondary;

        public IMessageBus MessageBus => _messageBus;

        public void RequestActivation()
        {
            _messageBus.Publish(new SetFocusedPaneMessage(_isSecondary));
        }

        public SearchViewModel Search { get; }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }
        public ICommand NavigateBackCommand { get; }
        public ICommand NavigateForwardCommand { get; }
        public ICommand NavigateUpCommand { get; }
        public ICommand SwitchViewModeCommand { get; }
        public ICommand PropertiesCommand { get; }
        public ICommand NewFolderCommand { get; }
        public ICommand NewFileCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand CutCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand RenameCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        public ICommand ToggleLibraryCommand { get; }
        public ICommand AddToFavoriteCommand { get; }
        public ICommand ToggleTagCommand { get; }
        public ICommand NewLibraryCommand { get; }
        public ICommand NewFavoriteGroupCommand { get; }

        // Tag Handling Commands
        public ICommand NewTagCommand { get; }
        public ICommand ManageTagsCommand { get; }
        public ICommand BatchAddTagsCommand { get; }
        public ICommand TagStatisticsCommand { get; }

        public ObservableCollection<ContextMenuItemViewModel> LibraryMenuItems => _libraryMenuItems;
        public ObservableCollection<ContextMenuItemViewModel> FavoriteMenuItems => _favoriteMenuItems;
        public ObservableCollection<ContextMenuItemViewModel> TagMenuItems => _tagMenuItems;

        #region Navigation State

        public bool CanNavigateBack => _backStack.Count > 0;
        public bool CanNavigateForward => _forwardStack.Count > 0;
        public bool CanNavigateUp
        {
            get
            {
                if (string.IsNullOrEmpty(CurrentPath) || ProtocolManager.IsVirtual(CurrentPath)) return false;
                try { return !string.IsNullOrEmpty(Path.GetDirectoryName(CurrentPath)); } catch { return false; }
            }
        }

        #endregion

        #endregion

        #region Constructor

        /// <summary>
        /// 创建面板 ViewModel
        /// </summary>
        /// <param name="dispatcher">UI 调度器</param>
        /// <param name="isSecondary">是否为次要面板</param>
        public PaneViewModel(Dispatcher dispatcher, IMessageBus messageBus, bool isSecondary = false)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _isSecondary = isSecondary;
            _files = new ObservableCollection<FileSystemItem>();
            _folderSizeService = App.ServiceProvider.GetService(typeof(FolderSizeCalculationService)) as FolderSizeCalculationService;

            // 初始化命令
            RefreshCommand = new RelayCommand(() => RequestRefresh());
            NavigateBackCommand = new RelayCommand(ExecuteNavigateBack, () => CanNavigateBack);
            NavigateForwardCommand = new RelayCommand(ExecuteNavigateForward, () => CanNavigateForward);
            NavigateUpCommand = new RelayCommand(ExecuteNavigateUp, () => CanNavigateUp);
            SwitchViewModeCommand = new RelayCommand<string>(ExecuteSwitchViewMode);

            // 下列命令初步实现，后续可接入 FileOperationService
            PropertiesCommand = new RelayCommand(ExecuteShowProperties, () => SelectedItem != null);
            NewFolderCommand = new RelayCommand(ExecuteNewFolder);
            NewFileCommand = new RelayCommand(ExecuteNewFile);
            DeleteCommand = new RelayCommand(ExecuteDelete, () => SelectedItems.Count > 0);
            CopyCommand = new RelayCommand(ExecuteCopy, () => SelectedItems.Count > 0);
            CutCommand = new RelayCommand(ExecuteCut, () => SelectedItems.Count > 0);
            PasteCommand = new RelayCommand(ExecutePaste);
            RenameCommand = new RelayCommand(ExecuteRename, () => SelectedItems.Count == 1);
            UndoCommand = new RelayCommand(ExecuteUndo);
            RedoCommand = new RelayCommand(ExecuteRedo);

            ToggleLibraryCommand = new RelayCommand<Library>(ExecuteToggleLibrary);
            AddToFavoriteCommand = new RelayCommand<int>(ExecuteAddToFavorite);
            ToggleTagCommand = new RelayCommand<ITag>(ExecuteToggleTag);
            NewLibraryCommand = new RelayCommand(ExecuteNewLibrary);
            NewFavoriteGroupCommand = new RelayCommand(ExecuteNewFavoriteGroup);

            // Tag Commands
            NewTagCommand = new RelayCommand(ExecuteManageTags); // Reuse Manage logic for now
            ManageTagsCommand = new RelayCommand(ExecuteManageTags);
            BatchAddTagsCommand = new RelayCommand(ExecuteBatchAddTags, () => SelectedItems.Count > 0);
            TagStatisticsCommand = new RelayCommand(ExecuteTagStatistics);

            SelectAllCommand = new RelayCommand(ExecuteSelectAll);

            Search = new SearchViewModel(_messageBus);
            Search.SetTargetPane(isSecondary ? "Secondary" : "Primary");

            // 订阅搜索结果消息
            _messageBus.Subscribe<SearchResultUpdatedMessage>(OnSearchResultUpdated);
            _messageBus.Subscribe<Messaging.Messages.FocusedPaneChangedMessage>(OnFocusedPaneChanged);

            // 订阅实时更新消息
            _messageBus.Subscribe<NotesUpdatedMessage>(OnNotesUpdated);
            _messageBus.Subscribe<FileTagsChangedMessage>(OnFileTagsChanged);
            _messageBus.Subscribe<TagListChangedMessage>(OnTagListChanged);
            // 订阅刷新消息
            _messageBus.Subscribe<RefreshFileListMessage>(OnRefreshFileList);
            // 订阅库选择消息
            _messageBus.Subscribe<LibrarySelectedMessage>(OnLibrarySelected);
            _messageBus.Subscribe<FileSelectionChangedMessage>(OnFileSelectionChanged);
            // 订阅路径导航请求
            _messageBus.Subscribe<NavigateToPathMessage>(OnNavigateToPath);

            // 获取服务
            var errorService = App.ServiceProvider?.GetService<YiboFile.Services.Core.Error.ErrorService>();
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            _libraryService = App.ServiceProvider?.GetService<LibraryService>();
            _favoriteService = App.ServiceProvider?.GetService<FavoriteService>();

            if (errorService != null)
            {
                _fileListService = new FileListService(_dispatcher, errorService, _tagService);
            }

            if (_libraryService != null)
            {
                _libraryService.LibrariesLoaded += OnLibrariesLoaded;
            }

            // 初始化显示防抖定时器
            _refreshDebounceTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _refreshDebounceTimer.Tick += (s, e) =>
            {
                _refreshDebounceTimer.Stop();
                Refresh();
            };

            // 初始化视图模式
            _fileViewMode = ConfigurationService.Instance.Get(cfg => cfg.FileViewMode) ?? "List";
        }

        #endregion

        #region Command Implementations

        private void ExecuteSwitchViewMode(string mode)
        {
            if (string.IsNullOrEmpty(mode)) return;
            FileViewMode = mode;
        }

        private void ExecuteNavigateBack()
        {
            if (_backStack.Count == 0) return;
            _isNavigatingHistory = true;
            _forwardStack.Push(CurrentPath);
            var prev = _backStack.Pop();
            NavigateTo(prev);
            _isNavigatingHistory = false;
            OnPropertyChanged(nameof(CanNavigateBack));
            OnPropertyChanged(nameof(CanNavigateForward));
        }

        private void ExecuteNavigateForward()
        {
            if (_forwardStack.Count == 0) return;
            _isNavigatingHistory = true;
            _backStack.Push(CurrentPath);
            var next = _forwardStack.Pop();
            NavigateTo(next);
            _isNavigatingHistory = false;
            OnPropertyChanged(nameof(CanNavigateBack));
            OnPropertyChanged(nameof(CanNavigateForward));
        }

        private void ExecuteNavigateUp()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;

            // 使用统一的智能向上导航逻辑
            string upPath = null;
            if (ProtocolManager.IsVirtual(CurrentPath))
            {
                // 处理虚拟路径，如 zip://path|/subfolder -> zip://path|
                int lastSlash = CurrentPath.LastIndexOf('/');
                if (lastSlash > 0)
                {
                    // 如果以 / 结尾，去掉后再找
                    var pathToCheck = CurrentPath.EndsWith("/") ? CurrentPath.Substring(0, CurrentPath.Length - 1) : CurrentPath;
                    lastSlash = pathToCheck.LastIndexOf('/');
                    if (lastSlash > 0)
                    {
                        var potential = pathToCheck.Substring(0, lastSlash);
                        // 如果剩下的是协议头部分 (如 zip://path|)，停止
                        if (potential.EndsWith("|") || potential.EndsWith("//"))
                            upPath = potential;
                        else
                            upPath = potential;
                    }
                }

                // 如果没找到斜杠，或者就在根部
                if (upPath == null && CurrentPath.Contains("|"))
                {
                    // 从压缩包回到物理文件路径
                    upPath = CurrentPath.Substring(CurrentPath.IndexOf("//") + 2);
                    if (upPath.Contains("|")) upPath = upPath.Substring(0, upPath.IndexOf("|"));
                }
            }
            else
            {
                upPath = Path.GetDirectoryName(CurrentPath);
            }

            if (!string.IsNullOrEmpty(upPath))
                NavigateTo(upPath);
        }

        private void ExecuteSelectAll()
        {
            // 发布消息让对应的 ListView 全选
            _messageBus.Publish(new Messaging.Messages.SelectAllRequestMessage(_isSecondary ? PaneId.Second : PaneId.Main));
        }

        private void ExecuteShowProperties()
        {
            if (SelectedItem != null)
            {
                _messageBus.Publish(new ShowPropertiesRequestMessage(SelectedItem, CurrentPath));
            }
            else if (!string.IsNullOrEmpty(CurrentPath))
            {
                _messageBus.Publish(new ShowPropertiesRequestMessage(null, CurrentPath));
            }
        }

        private void ExecuteNewFolder() => _messageBus.Publish(new CreateFolderRequestMessage(CurrentPath));
        private void ExecuteNewFile() => _messageBus.Publish(new CreateFileRequestMessage(CurrentPath));

        private void ExecuteDelete() => _messageBus.Publish(new DeleteItemsRequestMessage(SelectedItems.ToList()));
        private void ExecuteCopy() => _messageBus.Publish(new CopyItemsRequestMessage(SelectedItems.ToList()));
        private void ExecuteCut() => _messageBus.Publish(new CutItemsRequestMessage(SelectedItems.ToList()));
        private void ExecutePaste() => _messageBus.Publish(new PasteItemsRequestMessage(CurrentPath));
        private void ExecuteRename() => _messageBus.Publish(new RenameItemRequestMessage(SelectedItem));
        private void ExecuteUndo() => _messageBus.Publish(new UndoRequestMessage());
        private void ExecuteRedo() => _messageBus.Publish(new RedoRequestMessage());

        private void ExecuteToggleLibrary(Library library)
        {
            if (library == null || SelectedItems.Count == 0 || _libraryService == null) return;

            bool anyIn = SelectedItems.Any(i => library.Paths != null && library.Paths.Contains(i.Path));
            bool shouldAdd = !anyIn;

            foreach (var item in SelectedItems)
            {
                if (shouldAdd) _libraryService.AddLibraryPath(library.Id, item.Path);
                else _libraryService.RemoveLibraryPath(library.Id, item.Path);
            }
            UpdateDynamicMenuItems();
        }

        private void ExecuteAddToFavorite(int groupId)
        {
            if (SelectedItems.Count == 0 || _favoriteService == null) return;
            _favoriteService.AddFavorite(SelectedItems.ToList(), groupId);
        }

        private void ExecuteNewLibrary()
        {
            if (_libraryService == null) return;
            var dialog = new YiboFile.Controls.Dialogs.InputDialog("新建库", "请输入库名称:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                int newLibId = _libraryService.AddLibrary(dialog.InputText);
                if (newLibId != 0 && SelectedItems.Count > 0)
                {
                    int targetId = Math.Abs(newLibId);
                    foreach (var item in SelectedItems.Where(i => i.IsDirectory))
                    {
                        _libraryService.AddLibraryPath(targetId, item.Path);
                    }
                }
                UpdateDynamicMenuItems();
            }
        }

        private void ExecuteNewFavoriteGroup()
        {
            if (_favoriteService == null) return;
            var inputName = YiboFile.DialogService.ShowInput("请输入新分组名称：", "新分组", "新建分组");
            if (!string.IsNullOrEmpty(inputName))
            {
                int newGroupId = _favoriteService.CreateGroup(inputName.Trim());
                if (newGroupId != -1 && SelectedItems.Count > 0)
                {
                    _favoriteService.AddFavorite(SelectedItems.ToList(), newGroupId);
                }
                UpdateDynamicMenuItems();
            }
        }

        private async void ExecuteToggleTag(ITag tag)
        {
            if (tag == null || SelectedItems.Count == 0 || _tagService == null) return;

            foreach (var item in SelectedItems)
            {
                var fileTags = await _tagService.GetFileTagsAsync(item.Path);
                if (fileTags.Any(t => t.Id == tag.Id))
                {
                    await _tagService.RemoveTagFromFileAsync(item.Path, tag.Id);
                }
                else
                {
                    await _tagService.AddTagToFileAsync(item.Path, tag.Id);
                }
            }
            UpdateDynamicMenuItems();
        }



        private void ExecuteManageTags()
        {
            var dialog = new YiboFile.Controls.Dialogs.TagManagementDialog();
            if (Application.Current?.MainWindow != null)
                dialog.Owner = Application.Current.MainWindow;
            dialog.ShowDialog();

            // Notify system that tags might have changed
            _messageBus.Publish(new TagListChangedMessage());
            UpdateDynamicMenuItems();
        }

        private void ExecuteBatchAddTags()
        {
            if (SelectedItems.Count == 0 || _tagService == null) return;

            var dialog = new YiboFile.Controls.Dialogs.TagSelectionDialog();
            if (Application.Current?.MainWindow != null)
                dialog.Owner = Application.Current.MainWindow;

            if (dialog.ShowDialog() == true)
            {
                int successCount = 0;
                Task.Run(async () =>
                {
                    int tagId = dialog.SelectedTagId;
                    foreach (var item in SelectedItems)
                    {
                        try
                        {
                            await _tagService.AddTagToFileAsync(item.Path, tagId);
                            successCount++;
                            _messageBus.Publish(new FileTagsChangedMessage(item.Path));
                        }
                        catch { }
                    }

                    _dispatcher.Invoke(() =>
                    {
                        if (successCount > 0)
                        {
                            UpdateDynamicMenuItems();
                        }
                    });
                });
            }
        }

        private void ExecuteTagStatistics()
        {
            if (_tagService == null) return;
            try
            {
                var tags = _tagService.GetAllTags();
                var groups = _tagService.GetTagGroups();
                string stats = $"标签总数: {tags.Count()}\n标签分组: {groups.Count()}";
                MessageBox.Show(stats, "标签统计", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取统计失败: {ex.Message}");
            }
        }

        private void UpdateDynamicMenuItems()
        {
            if (_dispatcher == null) return;

            _dispatcher.Invoke(() =>
            {
                // Libraries
                var allLibraries = _libraryService?.GetAllLibraries() ?? new System.Collections.Generic.List<Library>();
                _libraryMenuItems.Clear();
                foreach (var lib in allLibraries)
                {
                    bool isChecked = SelectedItems.Count > 0 && SelectedItems.All(i => lib.Paths != null && lib.Paths.Contains(i.Path));
                    _libraryMenuItems.Add(new ContextMenuItemViewModel
                    {
                        Header = lib.Name,
                        Command = ToggleLibraryCommand,
                        CommandParameter = lib,
                        IsCheckable = true,
                        IsChecked = isChecked,
                        Icon = Application.Current.TryFindResource("Icon_Library")
                    });
                }

                if (allLibraries.Count > 0) _libraryMenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });
                _libraryMenuItems.Add(new ContextMenuItemViewModel { Header = "新建库...", Command = NewLibraryCommand });

                // Tags
                _tagMenuItems.Clear();
                if (App.IsTagTrainAvailable)
                {
                    var allTags = _tagService?.GetAllTags() ?? new System.Collections.Generic.List<ITag>();
                    foreach (var tag in allTags)
                    {
                        bool isChecked = SelectedItems.Count > 0 && SelectedItems.All(i => i.TagList != null && i.TagList.Any(t => t.Id == tag.Id));
                        _tagMenuItems.Add(new ContextMenuItemViewModel
                        {
                            Header = tag.Name,
                            Command = ToggleTagCommand,
                            CommandParameter = tag,
                            IsCheckable = true,
                            IsChecked = isChecked,
                            IconBrush = tag.Color ?? "#808080"
                        });
                    }
                }

                // Favorites
                var groups = _favoriteService?.GetAllGroups() ?? new System.Collections.Generic.List<FavoriteGroup>();
                _favoriteMenuItems.Clear();
                foreach (var group in groups)
                {
                    _favoriteMenuItems.Add(new ContextMenuItemViewModel
                    {
                        Header = group.Name,
                        Command = AddToFavoriteCommand,
                        CommandParameter = group.Id,
                        Icon = Application.Current.TryFindResource("Icon_Favorite")
                    });
                }

                if (groups.Count > 0) _favoriteMenuItems.Add(new ContextMenuItemViewModel { IsSeparator = true });
                _favoriteMenuItems.Add(new ContextMenuItemViewModel { Header = "+ 新建分组...", Command = NewFavoriteGroupCommand });
            });
        }

        #endregion

        #region Navigation Methods

        /// <summary>
        /// 导航到指定路径
        /// </summary>
        public void NavigateTo(string path, bool loadData = true)
        {
            if (string.IsNullOrEmpty(path)) return;

            // 记录历史
            if (!_isNavigatingHistory && !string.IsNullOrEmpty(CurrentPath) && path != CurrentPath)
            {
                _backStack.Push(CurrentPath);
                _forwardStack.Clear();
                OnPropertyChanged(nameof(CanNavigateBack));
                OnPropertyChanged(nameof(CanNavigateForward));
            }

            // 识别协议并保持模式
            var protocol = ProtocolManager.Parse(path);
            if (protocol.Type == ProtocolType.Library) NavigationMode = "Library";
            else if (protocol.Type == ProtocolType.Tag) NavigationMode = "Tag";
            else NavigationMode = "Path";

            CurrentLibrary = null;
            CurrentTag = null;
            CurrentPath = path;

            // 更新 Up 状态
            OnPropertyChanged(nameof(CanNavigateUp));

            if (loadData && !IsLoadingDisabled)
            {
                _ = LoadPathAsync(path);
            }
            else
            {
                FileList?.SetFiles(new System.Collections.Generic.List<FileSystemItem>());
            }
        }

        /// <summary>
        /// 导航到指定库
        /// </summary>
        /// <summary>
        /// 检查是否需要导航到指定的库
        /// </summary>
        public bool ShouldNavigateToLibrary(Library library)
        {
            if (library == null) return false;
            return _currentLibrary != library || _navigationMode != "Library";
        }

        public void NavigateTo(Library library, bool loadData = true)
        {
            if (library == null) return;

            NavigationMode = "Library";
            CurrentTag = null;
            CurrentLibrary = library;
            // 关键修复：确保 CurrentPath 包含协议头，以便地址栏正确识别模式
            CurrentPath = $"lib://{library.Name}";

            if (loadData && !IsLoadingDisabled)
            {
                if (FileList != null)
                {
                    _ = FileList.LoadPathAsync(CurrentPath);
                }
                else
                {
                    _ = LoadLibraryAsync(library);
                }
            }
            else
            {
                FileList?.SetFiles(new System.Collections.Generic.List<FileSystemItem>());
            }
        }

        /// <summary>
        /// 导航到指定标签
        /// </summary>
        public void NavigateTo(TagViewModel tag, bool loadData = true)
        {
            if (tag == null) return;

            NavigationMode = "Tag";
            CurrentLibrary = null;
            CurrentTag = tag;
            // 关键修复：确保 CurrentPath 包含协议头
            CurrentPath = $"tag://{tag.Name}";

            if (loadData && !IsLoadingDisabled)
            {
                if (FileList != null)
                {
                    _ = FileList.LoadPathAsync(CurrentPath);
                }
                else
                {
                    _ = LoadTagAsync(tag);
                }
            }
            else
            {
                FileList?.SetFiles(new System.Collections.Generic.List<FileSystemItem>());
            }
        }

        /// <summary>
        /// 刷新当前视图
        /// </summary>
        public void Refresh()
        {
            if (IsLoadingDisabled)
            {
                FileList?.SetFiles(new System.Collections.Generic.List<FileSystemItem>());
                return;
            }

            switch (NavigationMode)
            {
                case "Path":
                    if (!string.IsNullOrEmpty(CurrentPath))
                        _ = LoadPathAsync(CurrentPath);
                    break;
                case "Library":
                    if (CurrentLibrary != null)
                        _ = LoadLibraryAsync(CurrentLibrary);
                    break;
                case "Tag":
                    if (CurrentTag != null)
                        _ = LoadTagAsync(CurrentTag);
                    break;
            }
        }

        /// <summary>
        /// 请求刷新（带防抖）
        /// </summary>
        public void RequestRefresh()
        {
            _refreshDebounceTimer.Stop();
            _refreshDebounceTimer.Start();
        }

        #endregion

        #region Loading Methods

        /// <summary>
        /// 异步加载路径
        /// </summary>
        private async Task LoadPathAsync(string path)
        {
            if (IsLoadingDisabled) return;
            if (string.IsNullOrEmpty(path) || _fileListService == null) return;

            // 取消之前的加载
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                if (FileList != null)
                {
                    // Delegate to FileListViewModel which handles threading/IO robustly
                    await FileList.LoadPathAsync(path);
                    return;
                }

                IsLoading = true;

                var items = await Task.Run(async () =>
                {
                    try
                    {
                        var result = await _fileListService.LoadFileSystemItemsAsync(path, null, token);
                        if ((result == null || result.Count == 0) && path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase) && _tagService != null)
                        {
                            // Fallback: try loading tags directly if service returned empty
                            var tagName = path.Substring(6); // len(tag://)
                            var files = await _tagService.GetFilesByTagNameAsync(tagName);
                            // Convert paths to items... manually?
                            // This confirms if service is failing.
                        }
                        return result;
                    }
                    catch (Exception)
                    {
                        return new System.Collections.Generic.List<FileSystemItem>();
                    }
                }, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                await _dispatcher.InvokeAsync(() =>
                {
                    Files.Clear();
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            Files.Add(item);
                        }
                    }
                    FilesLoaded?.Invoke(this, Files);
                });

                // 设置文件监视
                SetupFileWatcher(path);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception)
            {
                // _errorService?.ReportError($"Load error: {ex.Message}"); 
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 异步加载库
        /// </summary>
        private async Task LoadLibraryAsync(Library library)
        {
            if (IsLoadingDisabled) return;
            if (library == null || _fileListService == null) return;

            // 取消之前的加载
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                IsLoading = true;

                var items = await Task.Run(async () =>
                {
                    token.ThrowIfCancellationRequested();
                    // 在后台线程检查路径是否存在，避免在 UI 线程阻塞（特别是针对慢速网络路径）
                    var validPaths = library.Paths?.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).ToList()
                                     ?? new System.Collections.Generic.List<string>();

                    if (validPaths.Count == 0) return new System.Collections.Generic.List<FileSystemItem>();

                    return await _fileListService.LoadFileSystemItemsFromMultiplePathsAsync(validPaths, null, null, token);
                }, token);

                if (token.IsCancellationRequested) return;

                // 批量更新集合，避免逐个 Add 导致大量 UI 重绘
                var newCollection = new ObservableCollection<FileSystemItem>(items);
                await _dispatcher.InvokeAsync(() =>
                {
                    if (FileList != null) FileList.Files = newCollection;
                    else _files = newCollection;

                    OnPropertyChanged(nameof(Files));
                    FilesLoaded?.Invoke(this, Files);
                }, DispatcherPriority.Normal);
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaneViewModel.LoadLibraryAsync] Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 异步加载标签
        /// </summary>
        private async Task LoadTagAsync(TagViewModel tag)
        {
            if (IsLoadingDisabled) return;
            if (tag == null || _tagService == null) return;

            // 取消之前的加载
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            try
            {
                IsLoading = true;

                var filePaths = await Task.Run(() =>
                {
                    token.ThrowIfCancellationRequested();
                    // 优先使用 ID 查询，若无结果则回退到名称查询
                    var paths = _tagService.GetFilesByTag(tag.Id);
                    if ((paths == null || !paths.Any()) && !string.IsNullOrEmpty(tag.Name))
                    {
                        paths = _tagService.GetFilesByTagName(tag.Name);
                    }
                    return paths;
                }, token);

                if (token.IsCancellationRequested) return;

                // 使用 FileListService 创建 FileSystemItem (移至后台线程)
                var items = await Task.Run(async () =>
                {
                    var result = new System.Collections.Generic.List<FileSystemItem>();
                    foreach (var path in filePaths)
                    {
                        if (token.IsCancellationRequested) break;

                        // 统一在后台线程检查并加载，避免 IO 阻塞 UI
                        if (File.Exists(path) || Directory.Exists(path))
                        {
                            var dir = Path.GetDirectoryName(path);
                            // 注意：此处使用异步方法，确保不阻塞后台工作线程同时也解耦
                            var fileItems = await _fileListService.LoadFileSystemItemsAsync(dir, null, token);
                            var item = fileItems?.FirstOrDefault(f => f.Path == path);
                            if (item != null) result.Add(item);
                        }
                    }
                    return result;
                }, token);

                if (token.IsCancellationRequested) return;

                var newCollection = new ObservableCollection<FileSystemItem>(items);
                await _dispatcher.InvokeAsync(() =>
                {
                    if (FileList != null) FileList.Files = newCollection;
                    else _files = newCollection;

                    OnPropertyChanged(nameof(Files));
                    FilesLoaded?.Invoke(this, Files);
                }, DispatcherPriority.Normal);

                // 设置路径，虽然是标签模式，但有时需要一个基准路径（取第一个文件的目录）
                if (items.Count > 0)
                {
                    var firstDir = Path.GetDirectoryName(items[0].Path);
                    if (Directory.Exists(firstDir)) SetupFileWatcher(firstDir);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PaneViewModel.LoadTagAsync] Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 直接设置文件列表（用于搜索结果等外部数据源）
        /// </summary>
        public void SetFiles(System.Collections.Generic.IEnumerable<FileSystemItem> files)
        {
            var items = files?.ToList() ?? new System.Collections.Generic.List<FileSystemItem>();
            var newCollection = new ObservableCollection<FileSystemItem>(items);

            _dispatcher.Invoke(() =>
            {
                if (FileList != null) FileList.Files = newCollection;
                else _files = newCollection;

                OnPropertyChanged(nameof(Files));
                FilesLoaded?.Invoke(this, Files);
            }, DispatcherPriority.Normal);
        }

        #endregion

        #region File Watcher

        private void SetupFileWatcher(string path)
        {
            // 清理旧的监视器
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            // 虚拟路径不监视
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;

            try
            {
                _fileWatcher = new FileSystemWatcher(path)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                   NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Created += OnFileSystemChanged;
                _fileWatcher.Deleted += OnFileSystemChanged;
                _fileWatcher.Renamed += OnFileSystemChanged;
                _fileWatcher.Changed += OnFileSystemChanged;
            }
            catch (Exception)
            {
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            // 使用防抖刷新
            _dispatcher.BeginInvoke(new Action(RequestRefresh));
        }

        #endregion

        private void OnLibrariesLoaded(object sender, System.Collections.Generic.List<Library> libraries)
        {
            _dispatcher.Invoke(() =>
            {
                if (NavigationMode == "Library" && CurrentLibrary != null)
                {
                    var newLib = libraries.FirstOrDefault(l => l.Id == CurrentLibrary.Id);
                    if (newLib != null)
                    {
                        // 更新引用并刷新
                        // 注意：设置 CurrentLibrary 可能会触发 PathChanged 或其他事件
                        // 但这里我们主要想确保刷新时使用有效的数据
                        if (SetProperty(ref _currentLibrary, newLib, nameof(CurrentLibrary)))
                        {
                            // 仅在实际更改时刷新？
                        }

                        RequestRefresh();
                    }
                }
            });
        }

        #region Message Handlers

        private void OnFocusedPaneChanged(Messaging.Messages.FocusedPaneChangedMessage message)
        {
            IsActive = (message.IsSecondPaneFocused == _isSecondary);
        }

        private void OnNavigateToPath(NavigateToPathMessage message)
        {
            if (!IsActive) return;
            NavigateTo(message.Path);
        }

        private void OnNotesUpdated(NotesUpdatedMessage message)
        {
            if (Files == null) return;
            var item = Files.FirstOrDefault(f => string.Equals(f.Path, message.FilePath, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                _dispatcher.Invoke(() =>
                {
                    item.Notes = message.Notes;
                });
            }
        }

        private async void OnFileTagsChanged(FileTagsChangedMessage message)
        {
            if (Files == null || _tagService == null) return;
            var item = Files.FirstOrDefault(f => string.Equals(f.Path, message.FilePath, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                // 刷新该项的标签
                try
                {
                    var tags = await _tagService.GetFileTagsAsync(message.FilePath);
                    var tagList = tags.Select(t => new TagViewModel { Id = t.Id, Name = t.Name, Color = t.Color }).ToList();

                    await _dispatcher.InvokeAsync(() =>
                    {
                        item.TagList = tagList;
                        item.Tags = string.Join(", ", tagList.Select(t => t.Name));
                    });
                }
                catch { }
            }
        }

        private void OnTagListChanged(TagListChangedMessage message)
        {
            // 如果正在浏览特定标签，刷新以反映潜在的定义更改（如颜色、名称）
            if (NavigationMode == "Tag")
            {
                RequestRefresh();
            }
        }

        private void OnSearchResultUpdated(SearchResultUpdatedMessage message)
        {
            // 检查目标面板是否匹配
            bool isTargetSecondary = message.TargetPaneId == "Secondary";
            if (isTargetSecondary != _isSecondary) return;

            _dispatcher.Invoke(() =>
            {
                if (message.Results != null)
                {
                    SetFiles(message.Results);
                    if (!string.IsNullOrEmpty(message.SearchTabPath))
                    {
                        // 这是一个搜索路径 (search:// 或 content://)
                        // 设置当前路径但不触发普通加载，因为结果已经传过来了
                        _currentPath = message.SearchTabPath;
                        OnPropertyChanged(nameof(CurrentPath));
                        PathChanged?.Invoke(this, message.SearchTabPath);
                    }
                }

                SearchStatusText = message.StatusMessage;
                IsSearching = message.IsSearching;
            });
        }

        private void OnRefreshFileList(RefreshFileListMessage message)
        {
            // 如果指定了具体路径
            if (!string.IsNullOrEmpty(message.Path))
            {
                if (NavigationMode == "Path")
                {
                    // 路径模式：必须路径完全匹配
                    if (!string.Equals(CurrentPath, message.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                else if (NavigationMode == "Library" && CurrentLibrary != null)
                {
                    // 库模式：如果变更路径是库包含的路径之一，则刷新
                    bool isPathInLibrary = false;
                    try
                    {
                        // 统一路径格式以便比较
                        string normPath = message.Path.TrimEnd('\\');
                        isPathInLibrary = CurrentLibrary.Paths.Any(p =>
                            string.Equals(p.TrimEnd('\\'), normPath, StringComparison.OrdinalIgnoreCase));
                    }
                    catch { }

                    if (!isPathInLibrary) return;
                }
                else
                {
                    // 其他虚拟模式（Tag/Search等）通常不响应物理路径的简单刷新消息
                    return;
                }
            }
            else
            {
                // 空路径消息通常意味着“全局强制刷新”
                // 在双面板模式下，只有当前面板或处于常规路径导航的面板才应该响应全局刷新
                if (NavigationMode != "Path" && !IsActive)
                {
                    return;
                }
            }

            RequestRefresh();
        }

        private void OnFileSelectionChanged(FileSelectionChangedMessage message)
        {
            // 更新动态菜单项（库、标签等）
            UpdateDynamicMenuItems();
        }

        private void OnLibrarySelected(LibrarySelectedMessage message)
        {
            if (message?.Library == null || _fileListService == null) return;

            // 重要：只有激活的面板才响应全局侧边栏的库选择消息
            if (!IsActive) return;

            // 如果当前已经在这个库，不再重复加载
            // 库名匹配 + 导航模式是 Library
            if (NavigationMode == "Library" && CurrentLibrary?.Name == message.Library.Name)
                return;

            NavigateTo(message.Library);
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_libraryService != null)
            {
                _libraryService.LibrariesLoaded -= OnLibrariesLoaded;
            }

            _loadCts?.Cancel();
            _loadCts?.Dispose();

            _refreshDebounceTimer?.Stop();

            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
        }

        #endregion
    }
}
