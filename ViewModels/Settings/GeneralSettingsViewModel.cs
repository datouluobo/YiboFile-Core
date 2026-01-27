using System;
using System.Windows.Input;
using YiboFile.Services;
using YiboFile.Services.Config;

namespace YiboFile.ViewModels.Settings
{
    public class GeneralSettingsViewModel : BaseViewModel
    {
        public ICommand ChangeBaseDirectoryCommand { get; }

        public GeneralSettingsViewModel()
        {
            ChangeBaseDirectoryCommand = new RelayCommand<string>(ChangeBaseDirectory);
            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            var config = ConfigurationService.Instance.GetSnapshot();

            _isMaximized = config.IsMaximized;
            _enableMultiWindow = config.EnableMultiWindow;
            _tabWidthMode = config.TabWidthMode;
            _pinnedTabWidth = config.PinnedTabWidth > 0 ? config.PinnedTabWidth : 120;
            _uiFontSize = config.UIFontSize > 0 ? config.UIFontSize : 16;
            _tagFontSize = config.TagFontSize > 0 ? config.TagFontSize : 16;
            _tagBoxWidth = config.TagBoxWidth;
            _baseDirectory = ConfigManager.GetBaseDirectory();
            _activateNewTabOnMiddleClick = config.ActivateNewTabOnMiddleClick;
        }

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

            try { DatabaseManager.Initialize(); } catch { }

            LoadFromConfig();
            OnPropertyChanged(nameof(BaseDirectory));
        }
    }
}
