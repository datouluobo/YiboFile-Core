using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Threading.Tasks;
using YiboFile.Services.Config;
using YiboFile.Services.FullTextSearch;
using YiboFile.Services;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// Minimal SettingsViewModel for settings panels.
    /// Each settings panel creates its own instance.
    /// </summary>
    public partial class SettingsViewModel : BaseViewModel
    {
        public ICommand ResetThemeCommand { get; }
        public ICommand ApplyAccentColorCommand { get; }

        public ICommand RebuildIndexCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        public ICommand ImportLibrariesCommand { get; }
        public ICommand ExportLibrariesCommand { get; }
        public ICommand OpenLibraryManagerCommand { get; }

        public ICommand AddLibraryCommand { get; }
        public ICommand RemoveLibraryCommand { get; }

        public event EventHandler OpenLibraryManagerRequested;

        public ICommand ResetHotkeysCommand { get; }
        public ICommand ResetSingleHotkeyCommand { get; }

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

            // General settings
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

            // Delegated inits
            _windowOpacity = config.WindowOpacity > 0 ? config.WindowOpacity : 1.0;
            _enableAnimations = config.AnimationsEnabled;

            InitializeThemes(config);
            InitializeIconStyles(config);
            InitializePathSettings(config);
            InitializeHotkeySettings(config);
            InitializeTagManagement();
            InitializeLibraryManagement();
            InitializeSearchSettings(config);
        }

        #region General Properties
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
    }
}
