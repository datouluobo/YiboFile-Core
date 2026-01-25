using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Services.Config;
using YiboFile.Services.Theming;
using YiboFile.Services.FullTextSearch;
using System.Threading.Tasks;
using YiboFile.Services.Features;
using Microsoft.Extensions.DependencyInjection; // For service location

using YiboFile.Services;
using YiboFile.Models;

namespace YiboFile.ViewModels
{
    public class LibraryItemViewModel : BaseViewModel
    {
        public int Id { get; set; }

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private ObservableCollection<string> _paths;
        public ObservableCollection<string> Paths
        {
            get => _paths;
            set => SetProperty(ref _paths, value);
        }

        public string DisplayPath => Paths != null && Paths.Count > 0 ? Paths[0] : "无路径";
        public string ToolTipText => Paths != null ? string.Join(Environment.NewLine, Paths) : "";

        public LibraryItemViewModel(Library lib)
        {
            Id = lib.Id;
            Name = lib.Name;
            Paths = new ObservableCollection<string>(lib.Paths ?? new List<string>());
        }
    }

    /// <summary>
    /// Minimal SettingsViewModel for settings panels.
    /// Does NOT contain static events or services that run at startup.
    /// Each settings panel creates its own instance.
    /// </summary>
    public class SettingsViewModel : BaseViewModel
    {
        public ICommand ResetThemeCommand { get; }
        public ICommand ApplyAccentColorCommand { get; }

        public ICommand RebuildIndexCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        // Library Management Commands
        public ICommand ImportLibrariesCommand { get; }
        public ICommand ExportLibrariesCommand { get; }
        public ICommand OpenLibraryManagerCommand { get; }

        public ICommand AddLibraryCommand { get; }
        public ICommand RemoveLibraryCommand { get; }

        public event EventHandler OpenLibraryManagerRequested;

        // Hotkey Settings Commands
        public ICommand ResetHotkeysCommand { get; }
        public ICommand ResetSingleHotkeyCommand { get; }

        // Tag Settings Commands
        public ICommand AddTagGroupCommand { get; }
        public ICommand RenameTagGroupCommand { get; }
        public ICommand DeleteTagGroupCommand { get; }
        public ICommand AddTagCommand { get; }
        public ICommand RenameTagCommand { get; }
        public ICommand DeleteTagCommand { get; }
        public ICommand UpdateTagColorCommand { get; }

        public event EventHandler<TagGroupManageViewModel> RenameTagGroupRequested;
        public event EventHandler<TagItemManageViewModel> RenameTagRequested;
        public event EventHandler<TagItemManageViewModel> UpdateTagColorRequested;

        public ICommand MoveSectionUpCommand { get; }
        public ICommand MoveSectionDownCommand { get; }

        public ICommand ChangeBaseDirectoryCommand { get; }
        public ICommand ExportConfigsCommand { get; }
        public ICommand ImportConfigsCommand { get; }
        public ICommand ExportDataCommand { get; }
        public ICommand ImportDataCommand { get; }
        public ICommand ExportAllCommand { get; }
        public ICommand ImportAllCommand { get; }

        // Note: Commands requiring UI interaction (e.g. AddScope) will be handled via events or interactions setup in View

        public SettingsViewModel()
        {
            ResetThemeCommand = new RelayCommand(ResetTheme);
            ApplyAccentColorCommand = new RelayCommand<string>(ApplyAccentColor);
            RebuildIndexCommand = new RelayCommand(RebuildIndex);
            ClearHistoryCommand = new RelayCommand(ClearHistory);

            ImportLibrariesCommand = new RelayCommand<string>(ImportLibraries);
            ExportLibrariesCommand = new RelayCommand<string>(ExportLibraries);
            OpenLibraryManagerCommand = new RelayCommand(OpenLibraryManager);

            AddLibraryCommand = new RelayCommand(AddLibrary);
            RemoveLibraryCommand = new RelayCommand<LibraryItemViewModel>(RemoveLibrary);

            ResetHotkeysCommand = new RelayCommand(ResetHotkeys);
            ResetSingleHotkeyCommand = new RelayCommand<HotkeyItemViewModel>(ResetSingleHotkey);

            AddTagGroupCommand = new RelayCommand(AddTagGroup);
            RenameTagGroupCommand = new RelayCommand<TagGroupManageViewModel>(g => RenameTagGroupRequested?.Invoke(this, g));
            DeleteTagGroupCommand = new RelayCommand<TagGroupManageViewModel>(DeleteTagGroup);

            AddTagCommand = new RelayCommand<TagGroupManageViewModel>(AddTag);
            RenameTagCommand = new RelayCommand<TagItemManageViewModel>(t => RenameTagRequested?.Invoke(this, t));
            DeleteTagCommand = new RelayCommand<TagItemManageViewModel>(DeleteTag);
            UpdateTagColorCommand = new RelayCommand<TagItemManageViewModel>(t => UpdateTagColorRequested?.Invoke(this, t));

            // Rename and AddTag (inside group) involve inputs. 
            // I'll expose public methods for these to be called from View's dialog logic.

            MoveSectionUpCommand = new RelayCommand<NavigationSectionItemViewModel>(MoveSectionUp);
            MoveSectionDownCommand = new RelayCommand<NavigationSectionItemViewModel>(MoveSectionDown);

            ChangeBaseDirectoryCommand = new RelayCommand<string>(ChangeBaseDirectory);
            ExportConfigsCommand = new RelayCommand<string>(ExportConfigs);
            ImportConfigsCommand = new RelayCommand<string>(ImportConfigs);
            ExportDataCommand = new RelayCommand<string>(ExportData);
            ImportDataCommand = new RelayCommand<string>(ImportData);
            ExportAllCommand = new RelayCommand<string>(ExportAll);
            ImportAllCommand = new RelayCommand<string>(ImportAll);

            LoadFromConfig();
        }

        ~SettingsViewModel()
        {
            if (FullTextSearchService.Instance?.IndexingService != null)
            {
                FullTextSearchService.Instance.IndexingService.ProgressChanged -= OnIndexingProgressChanged;
            }
        }

        public void LoadFromConfig()
        {
            var config = ConfigurationService.Instance.GetSnapshot();

            // Basic settings
            _isMaximized = config.IsMaximized;
            _enableMultiWindow = config.EnableMultiWindow;
            _tabWidthMode = config.TabWidthMode;
            _pinnedTabWidth = config.PinnedTabWidth > 0 ? config.PinnedTabWidth : 120;
            _uiFontSize = config.UIFontSize > 0 ? config.UIFontSize : 16;
            _tagFontSize = config.TagFontSize > 0 ? config.TagFontSize : 16;
            _tagBoxWidth = config.TagBoxWidth;
            _baseDirectory = ConfigManager.GetBaseDirectory();

            _colTagsWidth = config.ColTagsWidth > 0 ? config.ColTagsWidth : 150;
            _colNotesWidth = config.ColNotesWidth > 0 ? config.ColNotesWidth : 200;

            _activateNewTabOnMiddleClick = config.ActivateNewTabOnMiddleClick;

            // Appearance settings
            _windowOpacity = config.WindowOpacity > 0 ? config.WindowOpacity : 1.0;
            _enableAnimations = config.AnimationsEnabled;

            InitializeThemes(config);
            InitializeIconStyles(config);
            InitializePathSettings(config);
            InitializeHotkeySettings(config);
            InitializeTagManagement(); // Not config based, loads from DB via Service
            InitializeLibraryManagement(); // Not config based, loads from DB via Service
            InitializeSearchSettings(config);
        }

        #region Library Management
        private ObservableCollection<LibraryItemViewModel> _libraries;
        public ObservableCollection<LibraryItemViewModel> Libraries
        {
            get => _libraries;
            set => SetProperty(ref _libraries, value);
        }

        private LibraryService _libraryService;

        private void InitializeLibraryManagement()
        {
            // LibraryService needs Dispatcher and ErrorService. 
            // Ideally retrieved from DI container if registered, or instantiated if transient.
            // Usually LibraryService is singleton or scoped. Let's try getting from ServiceProvider.
            _libraryService = App.ServiceProvider?.GetService<LibraryService>();

            // If not registered (legacy), instantiate manually if possible (but ErrorService?)
            // Assuming it IS registered as per modern DI patterns in this app.

            RefreshLibraries();
        }

        public void RefreshLibraries()
        {
            if (_libraryService == null)
            {
                _libraryService = App.ServiceProvider?.GetService<LibraryService>();
            }

            if (_libraryService == null) return;

            var libs = _libraryService.LoadLibraries();
            var vmList = new ObservableCollection<LibraryItemViewModel>();
            foreach (var lib in libs)
            {
                vmList.Add(new LibraryItemViewModel(lib));
            }
            Libraries = vmList;
        }

        private void AddLibrary()
        {
            // Logic: Pick folder -> Default Name = Folder Name -> Add
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "选择文件夹作为新的库";
            dialog.UseDescriptionForTitle = true;
            dialog.ShowNewFolderButton = true;

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string path = dialog.SelectedPath;
                string name = new System.IO.DirectoryInfo(path).Name;

                // Add via service
                try
                {
                    if (_libraryService != null)
                    {
                        _libraryService.AddLibrary(name, path);
                        RefreshLibraries();
                    }
                }
                catch (Exception ex)
                {
                    YiboFile.DialogService.Error("添加库失败: " + ex.Message);
                }
            }
        }

        private void RemoveLibrary(LibraryItemViewModel item)
        {
            if (item == null) return;
            if (YiboFile.DialogService.Ask($"确定要移除库 \"{item.Name}\" 吗？", "确认"))
            {
                try
                {
                    _libraryService?.DeleteLibrary(item.Id, item.Name);
                    RefreshLibraries();
                }
                catch (Exception ex)
                {
                    YiboFile.DialogService.Error("移除库失败: " + ex.Message);
                }
            }
        }
        #endregion

        #region Path Settings
        private ObservableCollection<NavigationSectionItemViewModel> _navigationSections;
        public ObservableCollection<NavigationSectionItemViewModel> NavigationSections
        {
            get => _navigationSections;
            set => SetProperty(ref _navigationSections, value);
        }

        private void InitializePathSettings(AppConfig config)
        {
            var order = config.NavigationSectionsOrder;
            if (order == null || order.Count == 0)
            {
                order = new List<string> { "Drives", "QuickAccess" };
            }

            var sections = new ObservableCollection<NavigationSectionItemViewModel>();
            var fixedSections = new HashSet<string> { "QuickAccess", "Drives" };

            // Add sorted
            foreach (var key in order)
            {
                if (fixedSections.Contains(key))
                {
                    sections.Add(new NavigationSectionItemViewModel(key, GetSectionDisplayName(key)));
                }
                else if (key.StartsWith("FavoriteGroup_"))
                {
                    sections.Add(new NavigationSectionItemViewModel(key, GetSectionDisplayName(key)));
                }
            }

            // Ensure fixed sections exist
            foreach (var key in fixedSections)
            {
                if (!sections.Any(s => s.Key == key))
                {
                    sections.Add(new NavigationSectionItemViewModel(key, GetSectionDisplayName(key)));
                }
            }

            // Add any missing favorite groups
            var groups = DatabaseManager.GetAllFavoriteGroups();
            foreach (var group in groups)
            {
                string key = $"FavoriteGroup_{group.Id}";
                if (!sections.Any(s => s.Key == key))
                {
                    sections.Add(new NavigationSectionItemViewModel(key, group.Name));
                }
            }

            NavigationSections = sections;
        }

        private string GetSectionDisplayName(string key)
        {
            if (key == "QuickAccess") return "快速访问";
            if (key == "Drives") return "此电脑 (驱动器)";
            if (key.StartsWith("FavoriteGroup_"))
            {
                if (int.TryParse(key.Substring("FavoriteGroup_".Length), out int groupId))
                {
                    var group = DatabaseManager.GetAllFavoriteGroups().FirstOrDefault(g => g.Id == groupId);
                    if (group != null) return group.Name;
                }
                return "收藏项";
            }
            if (key == "FolderFavorites") return "收藏夹 (文件夹)"; // Legacy
            if (key == "FileFavorites") return "收藏夹 (文件)";   // Legacy
            return key;
        }

        private void MoveSectionUp(NavigationSectionItemViewModel item)
        {
            if (item == null) return;
            var index = NavigationSections.IndexOf(item);
            if (index > 0)
            {
                NavigationSections.Move(index, index - 1);
                SavePathSettings();
            }
        }

        private void MoveSectionDown(NavigationSectionItemViewModel item)
        {
            if (item == null) return;
            var index = NavigationSections.IndexOf(item);
            if (index >= 0 && index < NavigationSections.Count - 1)
            {
                NavigationSections.Move(index, index + 1);
                SavePathSettings();
            }
        }

        private void SavePathSettings()
        {
            var newOrder = NavigationSections.Select(s => s.Key).ToList();
            ConfigurationService.Instance.Update(c => c.NavigationSectionsOrder = newOrder);
        }
        #endregion

        #region Basic Settings
        private bool _isMaximized;
        public bool IsMaximized
        {
            get => _isMaximized;
            set
            {
                if (SetProperty(ref _isMaximized, value))
                    ConfigurationService.Instance.Update(c => c.IsMaximized = value);
            }
        }

        private bool _enableMultiWindow;
        public bool EnableMultiWindow
        {
            get => _enableMultiWindow;
            set
            {
                if (SetProperty(ref _enableMultiWindow, value))
                    ConfigurationService.Instance.Update(c => c.EnableMultiWindow = value);
            }
        }

        private TabWidthMode _tabWidthMode;
        public TabWidthMode TabWidthMode
        {
            get => _tabWidthMode;
            set
            {
                if (SetProperty(ref _tabWidthMode, value))
                    ConfigurationService.Instance.Update(c => c.TabWidthMode = value);
            }
        }

        private double _pinnedTabWidth;
        public double PinnedTabWidth
        {
            get => _pinnedTabWidth;
            set
            {
                if (SetProperty(ref _pinnedTabWidth, value))
                    ConfigurationService.Instance.Update(c => c.PinnedTabWidth = value);
            }
        }

        private double _uiFontSize;
        public double UIFontSize
        {
            get => _uiFontSize;
            set
            {
                if (SetProperty(ref _uiFontSize, value))
                    ConfigurationService.Instance.Update(c => c.UIFontSize = value);
            }
        }

        private double _tagFontSize;
        public double TagFontSize
        {
            get => _tagFontSize;
            set
            {
                if (SetProperty(ref _tagFontSize, value))
                    ConfigurationService.Instance.Update(c => c.TagFontSize = value);
            }
        }

        private double _tagBoxWidth;
        public double TagBoxWidth
        {
            get => _tagBoxWidth;
            set
            {
                if (SetProperty(ref _tagBoxWidth, value))
                    ConfigurationService.Instance.Update(c => c.TagBoxWidth = value);
            }
        }

        private string _baseDirectory;
        public string BaseDirectory
        {
            get => _baseDirectory;
            set => SetProperty(ref _baseDirectory, value);
        }
        private double _colTagsWidth;
        public double ColTagsWidth
        {
            get => _colTagsWidth;
            set
            {
                if (SetProperty(ref _colTagsWidth, value))
                    ConfigurationService.Instance.Update(c => c.ColTagsWidth = value);
            }
        }

        private double _colNotesWidth;
        public double ColNotesWidth
        {
            get => _colNotesWidth;
            set
            {
                if (SetProperty(ref _colNotesWidth, value))
                    ConfigurationService.Instance.Update(c => c.ColNotesWidth = value);
            }
        }

        private bool _activateNewTabOnMiddleClick;
        public bool ActivateNewTabOnMiddleClick
        {
            get => _activateNewTabOnMiddleClick;
            set
            {
                if (SetProperty(ref _activateNewTabOnMiddleClick, value))
                    ConfigurationService.Instance.Update(c => c.ActivateNewTabOnMiddleClick = value);
            }
        }
        #endregion

        #region Tag Settings
        private ObservableCollection<TagGroupManageViewModel> _tagGroups;
        public ObservableCollection<TagGroupManageViewModel> TagGroups
        {
            get => _tagGroups;
            set => SetProperty(ref _tagGroups, value);
        }

        private string _newGroupName;
        public string NewGroupName
        {
            get => _newGroupName;
            set => SetProperty(ref _newGroupName, value);
        }

        private ITagService _tagService;

        private void InitializeTagManagement()
        {
            _tagService = App.ServiceProvider?.GetService<ITagService>();
            if (_tagService != null)
            {
                _tagService.TagUpdated -= OnTagServiceTagUpdated;
                _tagService.TagUpdated += OnTagServiceTagUpdated;
            }
            RefreshTagGroups();
        }

        private void OnTagServiceTagUpdated(int tagId, string newColor)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (TagGroups != null)
                {
                    foreach (var group in TagGroups)
                    {
                        var tag = group.Tags.FirstOrDefault(t => t.Id == tagId);
                        if (tag != null)
                        {
                            tag.Color = newColor;
                        }
                    }
                }
            });
        }

        public void RefreshTagGroups()
        {
            if (_tagService == null)
            {
                // Try resolve again (in case initialized before container ready)
                _tagService = App.ServiceProvider?.GetService<ITagService>();
            }
            if (_tagService == null) return;

            var list = new ObservableCollection<TagGroupManageViewModel>();
            var groups = _tagService.GetTagGroups();

            // Convert to ViewModel
            foreach (var group in groups)
            {
                var groupVm = new TagGroupManageViewModel
                {
                    Id = group.Id,
                    Name = group.Name,
                    Color = group.Color,
                    Tags = new ObservableCollection<TagItemManageViewModel>()
                };

                var tags = _tagService.GetTagsByGroup(group.Id);
                foreach (var tag in tags)
                {
                    groupVm.Tags.Add(new TagItemManageViewModel
                    {
                        Id = tag.Id,
                        Name = tag.Name,
                        Color = tag.Color ?? "#2E8B57",
                        GroupId = tag.GroupId
                    });
                }

                list.Add(groupVm);
            }

            TagGroups = list;
        }


        public void AddTagGroup()
        {
            if (string.IsNullOrWhiteSpace(NewGroupName)) return;
            try
            {
                _tagService?.AddTagGroup(NewGroupName);
                NewGroupName = string.Empty; // Clear input
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public void RenameTagGroup(TagGroupManageViewModel group, string newName)
        {
            if (group == null || string.IsNullOrWhiteSpace(newName)) return;
            try
            {
                _tagService?.RenameTagGroup(group.Id, newName);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public void DeleteTagGroup(TagGroupManageViewModel group)
        {
            if (group == null) return;
            try
            {
                _tagService?.DeleteTagGroup(group.Id);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public void AddTag(TagGroupManageViewModel group)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.NewTagText)) return;
            try
            {
                _tagService?.AddTag(group.Id, group.NewTagText);
                group.NewTagText = string.Empty; // Clear Input
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public void RenameTag(TagItemManageViewModel tag, string newName)
        {
            if (tag == null || string.IsNullOrWhiteSpace(newName)) return;
            try
            {
                _tagService?.RenameTag(tag.Id, newName);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public void DeleteTag(TagItemManageViewModel tag)
        {
            if (tag == null) return;
            try
            {
                _tagService?.DeleteTag(tag.Id);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        public void UpdateTagColor(TagItemManageViewModel tag, string newColor)
        {
            if (tag == null) return;
            try
            {
                _tagService?.UpdateTagColor(tag.Id, newColor);
                RefreshTagGroups();
            }
            catch (Exception ex) { throw new Exception(ex.Message); }
        }

        #endregion

        #region Hotkey Settings
        private ObservableCollection<HotkeyItemViewModel> _hotkeys;
        public ObservableCollection<HotkeyItemViewModel> Hotkeys
        {
            get => _hotkeys;
            set => SetProperty(ref _hotkeys, value);
        }

        private void InitializeHotkeySettings(AppConfig config)
        {
            // Default Hotkeys Definition
            var defaults = new List<HotkeyItemViewModel>
            {
                // Tab Actions
                new HotkeyItemViewModel("新建标签页", "Ctrl+T"),
                new HotkeyItemViewModel("关闭标签页", "Ctrl+W"),
                new HotkeyItemViewModel("下一个标签", "Ctrl+Tab"),
                new HotkeyItemViewModel("上一个标签", "Ctrl+Shift+Tab"),
                new HotkeyItemViewModel("切换双面板焦点", "Tab"),
                
                // File Actions
                new HotkeyItemViewModel("复制", "Ctrl+C"),
                new HotkeyItemViewModel("剪切", "Ctrl+X"),
                new HotkeyItemViewModel("粘贴", "Ctrl+V"),
                new HotkeyItemViewModel("删除 (移到回收站)", "Delete"),
                new HotkeyItemViewModel("永久删除", "Shift+Delete"),
                new HotkeyItemViewModel("重命名", "F2"),
                new HotkeyItemViewModel("全选", "Ctrl+A"),
                new HotkeyItemViewModel("新建文件夹", "Ctrl+N"),
                new HotkeyItemViewModel("新建窗口", "Ctrl+Shift+N"),
                
                // Undo/Redo
                new HotkeyItemViewModel("撤销", "Ctrl+Z"),
                new HotkeyItemViewModel("重做", "Ctrl+Y"),
                
                // Navigation
                new HotkeyItemViewModel("返回上级目录", "Backspace"),
                new HotkeyItemViewModel("地址栏编辑", "Alt+D"),
                new HotkeyItemViewModel("刷新", "F5"),
                new HotkeyItemViewModel("打开文件/文件夹", "Enter"),
                
                // View & Preview
                new HotkeyItemViewModel("QuickLook 预览", "Space"),
                new HotkeyItemViewModel("属性", "Alt+Enter"),
                
                // Layout
                new HotkeyItemViewModel("专注模式", "Ctrl+Shift+F"),
                new HotkeyItemViewModel("工作模式", "Ctrl+Shift+W"),
                new HotkeyItemViewModel("完整模式", "Ctrl+Shift+A"),
            };

            var customs = config.CustomHotkeys ?? new Dictionary<string, string>();

            foreach (var item in defaults)
            {
                if (customs.TryGetValue(item.Description, out var customKey))
                {
                    item.KeyCombination = customKey;
                }

                // Subscribe to changes to save config
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(HotkeyItemViewModel.KeyCombination))
                    {
                        SaveHotkeySettings();
                    }
                };
            }

            Hotkeys = new ObservableCollection<HotkeyItemViewModel>(defaults);
        }

        private void ResetHotkeys()
        {
            foreach (var item in Hotkeys)
            {
                item.KeyCombination = item.DefaultKey;
            }
            SaveHotkeySettings();
        }

        private void ResetSingleHotkey(HotkeyItemViewModel item)
        {
            if (item != null)
            {
                item.KeyCombination = item.DefaultKey;
                SaveHotkeySettings();
            }
        }

        private void SaveHotkeySettings()
        {
            var customs = new Dictionary<string, string>();
            foreach (var item in Hotkeys)
            {
                if (item.IsModified)
                {
                    customs[item.Description] = item.KeyCombination;
                }
            }
            ConfigurationService.Instance.Update(c => c.CustomHotkeys = customs);
        }
        #endregion

        #region Appearance Settings
        private double _windowOpacity;
        public double WindowOpacity
        {
            get => _windowOpacity;
            set
            {
                if (SetProperty(ref _windowOpacity, value))
                {
                    ConfigurationService.Instance.Update(c => c.WindowOpacity = value);
                    if (System.Windows.Application.Current?.MainWindow != null)
                        System.Windows.Application.Current.MainWindow.Opacity = value;
                }
            }
        }

        private bool _enableAnimations;
        public bool EnableAnimations
        {
            get => _enableAnimations;
            set
            {
                if (SetProperty(ref _enableAnimations, value))
                    ConfigurationService.Instance.Update(c => c.AnimationsEnabled = value);
            }
        }

        private ObservableCollection<ThemeItemViewModel> _themes;
        public ObservableCollection<ThemeItemViewModel> Themes
        {
            get => _themes;
            set => SetProperty(ref _themes, value);
        }

        private ThemeItemViewModel _selectedTheme;
        public ThemeItemViewModel SelectedTheme
        {
            get => _selectedTheme;
            set
            {
                if (SetProperty(ref _selectedTheme, value) && value != null)
                {
                    if (value.Id == "FollowSystem")
                        ThemeManager.EnableSystemThemeFollowing();
                    else
                    {
                        ThemeManager.DisableSystemThemeFollowing();
                        ThemeManager.SetTheme(value.Id, animate: _enableAnimations);
                    }
                    ConfigurationService.Instance.Update(c => c.ThemeMode = value.Id);
                }
            }
        }

        private ObservableCollection<IconStyleItemViewModel> _iconStyles;
        public ObservableCollection<IconStyleItemViewModel> IconStyles
        {
            get => _iconStyles;
            set => SetProperty(ref _iconStyles, value);
        }

        private IconStyleItemViewModel _selectedIconStyle;
        public IconStyleItemViewModel SelectedIconStyle
        {
            get => _selectedIconStyle;
            set
            {
                if (SetProperty(ref _selectedIconStyle, value) && value != null)
                {
                    ThemeManager.ChangeIconStyle(value.Id);
                    ConfigurationService.Instance.Update(c => c.IconStyle = value.Id);
                }
            }
        }
        #endregion

        #region Search Settings
        private bool _isEnableFullTextSearch;
        public bool IsEnableFullTextSearch
        {
            get => _isEnableFullTextSearch;
            set
            {
                if (SetProperty(ref _isEnableFullTextSearch, value))
                {
                    ConfigurationService.Instance.Update(c => c.IsEnableFullTextSearch = value);
                    if (value && FullTextSearchService.Instance != null)
                        FullTextSearchService.Instance.StartBackgroundIndexing();
                }
            }
        }

        private bool _autoExpandHistory;
        public bool AutoExpandHistory
        {
            get => _autoExpandHistory;
            set
            {
                if (SetProperty(ref _autoExpandHistory, value))
                    ConfigurationService.Instance.Update(c => c.AutoExpandHistory = value);
            }
        }

        private int _historyMaxCount;
        public int HistoryMaxCount
        {
            get => _historyMaxCount;
            set
            {
                if (SetProperty(ref _historyMaxCount, value))
                    ConfigurationService.Instance.Update(c => c.HistoryMaxCount = value);
            }
        }

        private string _indexLocation;
        public string IndexLocation
        {
            get => _indexLocation;
            set => SetProperty(ref _indexLocation, value);
        }

        private int _indexedFileCount;
        public int IndexedFileCount
        {
            get => _indexedFileCount;
            set => SetProperty(ref _indexedFileCount, value);
        }

        private ObservableCollection<string> _indexScopes;
        public ObservableCollection<string> IndexScopes
        {
            get => _indexScopes;
            set => SetProperty(ref _indexScopes, value);
        }

        private double _indexingProgress;
        public double IndexingProgress
        {
            get => _indexingProgress;
            set => SetProperty(ref _indexingProgress, value);
        }

        private string _indexingStatusText;
        public string IndexingStatusText
        {
            get => _indexingStatusText;
            set => SetProperty(ref _indexingStatusText, value);
        }

        private bool _isIndexing;
        public bool IsIndexing
        {
            get => _isIndexing;
            set => SetProperty(ref _isIndexing, value);
        }
        #endregion



        public void RefreshThemes()
        {
            var config = ConfigurationService.Instance.GetSnapshot();
            InitializeThemes(config);
        }

        private void InitializeThemes(AppConfig config)
        {
            Themes = new ObservableCollection<ThemeItemViewModel>
            {
                new ThemeItemViewModel("FollowSystem", "跟随系统", "💻"),
                new ThemeItemViewModel("Light", "浅色模式", "☀️"),
                new ThemeItemViewModel("Dark", "深色模式", "🌙"),
                new ThemeItemViewModel("Ocean", "海洋之歌", "🌊"),
                new ThemeItemViewModel("Forest", "森林之息", "🌲"),
                new ThemeItemViewModel("Sunset", "日落大道", "🌅"),
                new ThemeItemViewModel("Purple", "紫罗兰梦", "💜"),
                new ThemeItemViewModel("Nordic", "北欧冰原", "🏔️")
            };

            var customThemes = CustomThemeManager.LoadAll();
            foreach (var ct in customThemes)
                Themes.Add(new ThemeItemViewModel(ct.Id, ct.Name, "🎨"));

            var currentTheme = config.ThemeMode ?? "FollowSystem";
            _selectedTheme = Themes.FirstOrDefault(x => x.Id == currentTheme) ?? Themes.First();
        }

        private void InitializeIconStyles(AppConfig config)
        {
            IconStyles = new ObservableCollection<IconStyleItemViewModel>
            {
                new IconStyleItemViewModel("Emoji", "🌈 系统 Emoji (默认)"),
                new IconStyleItemViewModel("Remix", "✒️ Remix Icon (现代) [实验性]"),
                new IconStyleItemViewModel("Fluent", "💠 Fluent Icons (Win11) [实验性]"),
                new IconStyleItemViewModel("Material", "✨ Material Design (Google) [实验性]")
            };
            var currentIconStyle = config.IconStyle ?? "Emoji";
            _selectedIconStyle = IconStyles.FirstOrDefault(x => x.Id == currentIconStyle) ?? IconStyles.First();
        }

        private void ResetTheme()
        {
            SelectedTheme = Themes.FirstOrDefault(t => t.Id == "FollowSystem");
        }

        private void ApplyAccentColor(string hexColor)
        {
            if (string.IsNullOrEmpty(hexColor)) return;

            try
            {
                var currentId = ConfigurationService.Instance.GetSnapshot().ThemeMode;
                string baseTheme = currentId == "Dark" || currentId == "Sunset" || currentId == "Ocean" || currentId == "Purple" ? "Dark" : "Light";

                var theme = CustomThemeManager.CreateFromCurrent("我的自定义主题", baseTheme);
                theme.Id = "QuickCustomTheme";

                var baseColor = (Color)ColorConverter.ConvertFromString(hexColor);
                theme.Colors["AccentDefaultBrush"] = hexColor;
                theme.Colors["AccentHoverBrush"] = ChangeColorBrightness(baseColor, 0.2f);
                theme.Colors["AccentPressedBrush"] = ChangeColorBrightness(baseColor, -0.2f);
                theme.Colors["AccentSelectedBrush"] = hexColor;
                theme.Colors["ControlFocusBrush"] = hexColor;
                theme.Colors["BorderFocusBrush"] = hexColor;
                theme.Colors["ForegroundOnAccentBrush"] = "#FFFFFF";

                CustomThemeManager.Save(theme);
                CustomThemeManager.Apply(theme);

                // Reload themes to include new custom theme
                var config = ConfigurationService.Instance.GetSnapshot();
                InitializeThemes(config);
                OnPropertyChanged(nameof(Themes));
                SelectedTheme = Themes.FirstOrDefault(t => t.Id == theme.Id);

                ConfigurationService.Instance.Update(c => c.ThemeMode = theme.Id);
            }
            catch { }
        }

        private string ChangeColorBrightness(Color color, float factor)
        {
            float red = color.R, green = color.G, blue = color.B;
            if (factor < 0)
            {
                factor = 1 + factor;
                red *= factor; green *= factor; blue *= factor;
            }
            else
            {
                red = (255 - red) * factor + red;
                green = (255 - green) * factor + green;
                blue = (255 - blue) * factor + blue;
            }
            return Color.FromRgb((byte)red, (byte)green, (byte)blue).ToString();
        }

        private void InitializeSearchSettings(AppConfig config)
        {
            _isEnableFullTextSearch = config.IsEnableFullTextSearch;
            _autoExpandHistory = config.AutoExpandHistory;
            _historyMaxCount = config.HistoryMaxCount;

            IndexScopes = new ObservableCollection<string>(config.FullTextIndexPaths ?? new List<string>());

            // Initial stats
            IndexLocation = config.FullTextIndexDbPath; // Or from service if available
            IndexedFileCount = FullTextSearchService.Instance.IndexedFileCount; // Assuming property exists
            IndexingStatusText = "就绪";

            // Subscribe to progress
            if (FullTextSearchService.Instance.IndexingService != null)
            {
                FullTextSearchService.Instance.IndexingService.ProgressChanged -= OnIndexingProgressChanged; // Prevent double sub
                FullTextSearchService.Instance.IndexingService.ProgressChanged += OnIndexingProgressChanged;
            }
        }

        private void OnIndexingProgressChanged(object sender, IndexingProgressEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (e.IsCompleted)
                {
                    IndexingStatusText = $"索引完成: {e.TotalFiles} 个文件";
                    IsIndexing = false;
                    IndexingProgress = 100;
                    RefreshIndexStats();
                }
                else
                {
                    IndexingStatusText = $"正在索引: {e.ProcessedFiles}/{e.TotalFiles}";
                    IsIndexing = true;
                    if (e.TotalFiles > 0)
                    {
                        IndexingProgress = (double)e.ProcessedFiles / e.TotalFiles * 100;
                    }
                    else
                    {
                        IndexingProgress = 0; // Indeterminate handling in UI via IsIndeterminate binding if needed
                    }
                }
            });
        }

        public void RefreshIndexStats()
        {
            Task.Run(() =>
            {
                try
                {
                    int count = FullTextSearchService.Instance.IndexedFileCount;
                    string path = FullTextSearchService.Instance.IndexDbPath;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IndexedFileCount = count;
                        if (!string.IsNullOrEmpty(path)) IndexLocation = path;
                    });
                }
                catch { }
            });
        }

        public void UpdateIndexScopes(IEnumerable<string> scopes)
        {
            // Helper to update scopes from View
            IndexScopes = new ObservableCollection<string>(scopes);
            ConfigurationService.Instance.Update(c => c.FullTextIndexPaths = scopes.ToList());
        }

        public void UpdateIndexLocation(string newPath)
        {
            ConfigurationService.Instance.Update(c => c.FullTextIndexDbPath = newPath);
            IndexLocation = newPath;
        }

        private async void RebuildIndex()
        {
            IsIndexing = true;
            IndexingStatusText = "正在清理...";
            IndexingProgress = 0;

            try
            {
                await Task.Run(async () =>
                {
                    FullTextSearchService.Instance.ClearIndex();

                    var config = ConfigurationService.Instance.GetSnapshot();
                    IEnumerable<string> scanPaths = config.FullTextIndexPaths;

                    if (scanPaths == null || !scanPaths.Any())
                    {
                        var libraries = YiboFile.DatabaseManager.GetAllLibraries();
                        scanPaths = libraries?.SelectMany(l => l.Paths ?? Enumerable.Empty<string>()) ?? Enumerable.Empty<string>();
                    }

                    foreach (var path in scanPaths)
                    {
                        if (System.IO.Directory.Exists(path))
                        {
                            await FullTextSearchService.Instance.IndexingService.StartIndexingAsync(path, recursive: true);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                IndexingStatusText = $"重建失败: {ex.Message}";
                IsIndexing = false;
            }
        }

        private void ClearHistory()
        {
            YiboFile.Services.Search.SearchHistoryService.Instance.Clear();
        }

        private void ChangeBaseDirectory(string newDir)
        {
            if (string.IsNullOrWhiteSpace(newDir)) return;

            var oldDir = ConfigManager.GetBaseDirectory();
            try
            {
                if (string.Equals(System.IO.Path.GetFullPath(oldDir.Trim()), System.IO.Path.GetFullPath(newDir.Trim()), StringComparison.OrdinalIgnoreCase))
                    return;
            }
            catch { return; }

            ConfigManager.SetBaseDirectory(newDir, copyMissingFromOld: true);

            if (App.IsTagTrainAvailable)
            {
                try
                {
                    // TagTrain.Services.SettingsManager.ClearCache();
                    // TagTrain.Services.SettingsManager.SetDataStorageDirectory(ConfigManager.GetBaseDirectory());
                    // TagTrain.Services.SettingsManager.ClearCache();
                }
                catch { }
            }

            try { DatabaseManager.Initialize(); } catch { }

            // Reload all settings as config path changed
            LoadFromConfig();
            // Need to notify that everything might have changed (or at least BaseDirectory)
            OnPropertyChanged(nameof(BaseDirectory));
        }

        private void ImportLibraries(string file)
        {
            if (string.IsNullOrEmpty(file)) return;
            try
            {
                string json = System.IO.File.ReadAllText(file);
                var libraryService = new YiboFile.Services.LibraryService(System.Windows.Application.Current.Dispatcher, null);
                libraryService.ImportLibrariesFromJson(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"导入库配置失败: {ex.Message}");
            }
        }

        private void ExportLibraries(string file)
        {
            if (string.IsNullOrEmpty(file)) return;
            try
            {
                var libraryService = new YiboFile.Services.LibraryService(System.Windows.Application.Current.Dispatcher, null);
                string json = libraryService.ExportLibrariesToJson();
                if (!string.IsNullOrEmpty(json))
                {
                    System.IO.File.WriteAllText(file, json);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"导出库配置失败: {ex.Message}");
            }
        }

        private void OpenLibraryManager()
        {
            OpenLibraryManagerRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExportConfigs(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try { ConfigManager.ExportConfigsZip(fileName); } catch (Exception ex) { throw new Exception($"导出配置失败: {ex.Message}"); }
        }

        private void ImportConfigs(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                ConfigManager.ImportConfigsZip(fileName);
                LoadFromConfig();
            }
            catch (Exception ex) { throw new Exception($"导入配置失败: {ex.Message}"); }
        }

        private void ExportData(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try { ConfigManager.ExportDataZip(fileName); } catch (Exception ex) { throw new Exception($"导出数据失败: {ex.Message}"); }
        }

        private void ImportData(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                ConfigManager.ImportDataZip(fileName);
                LoadFromConfig();
            }
            catch (Exception ex) { throw new Exception($"导入数据失败: {ex.Message}"); }
        }

        private void ExportAll(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try { ConfigManager.ExportAllZip(fileName); } catch (Exception ex) { throw new Exception($"导出全部失败: {ex.Message}"); }
        }

        private void ImportAll(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return;
            try
            {
                ConfigManager.ImportAllZip(fileName);
                LoadFromConfig();
            }
            catch (Exception ex) { throw new Exception($"导入全部失败: {ex.Message}"); }
        }
    }

    public class ThemeItemViewModel
    {
        public string Id { get; }
        public string Name { get; }
        public string Emoji { get; }
        public string DisplayName => $"{Emoji} {Name}";

        public ThemeItemViewModel(string id, string name, string emoji)
        {
            Id = id;
            Name = name;
            Emoji = emoji;
        }

        public override string ToString() => DisplayName;
    }

    public class IconStyleItemViewModel
    {
        public string Id { get; }
        public string Name { get; }

        public IconStyleItemViewModel(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString() => Name;
    }
    public class NavigationSectionItemViewModel
    {
        public string Key { get; }
        public string DisplayName { get; }

        public NavigationSectionItemViewModel(string key, string displayName)
        {
            Key = key;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }

    public class HotkeyItemViewModel : BaseViewModel
    {
        public string Description { get; }
        public string DefaultKey { get; }

        private string _keyCombination;
        public string KeyCombination
        {
            get => _keyCombination;
            set
            {
                if (SetProperty(ref _keyCombination, value))
                {
                    OnPropertyChanged(nameof(IsModified));
                }
            }
        }

        public bool IsModified => KeyCombination != DefaultKey;

        public HotkeyItemViewModel(string description, string defaultKey)
        {
            Description = description;
            DefaultKey = defaultKey;
            _keyCombination = defaultKey;
        }
    }

    public class TagGroupManageViewModel : BaseViewModel
    {
        public int Id { get; set; }
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _color;
        public string Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                {
                    OnPropertyChanged(nameof(ColorBrush));
                }
            }
        }

        public Brush ColorBrush
        {
            get
            {
                try
                {
                    if (string.IsNullOrEmpty(Color)) return Brushes.Transparent;
                    return (Brush)new BrushConverter().ConvertFrom(Color);
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }
        }

        public ObservableCollection<TagItemManageViewModel> Tags { get; set; } = new ObservableCollection<TagItemManageViewModel>();

        private string _newTagText;
        public string NewTagText
        {
            get => _newTagText;
            set => SetProperty(ref _newTagText, value);
        }
    }

    public class TagItemManageViewModel : BaseViewModel
    {
        public int Id { get; set; }
        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _color;
        public string Color
        {
            get => _color;
            set
            {
                if (SetProperty(ref _color, value))
                {
                    OnPropertyChanged(nameof(ColorBrush));
                }
            }
        }

        public Brush ColorBrush
        {
            get
            {
                return YiboFile.Models.TagViewModel.GetColorBrush(Name, Color);
            }
        }

        public int GroupId { get; set; }
    }
}
