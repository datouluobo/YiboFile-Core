using System;
using System.Windows.Input;
using YiboFile.Services;
using YiboFile.Services.Config;

namespace YiboFile.ViewModels.Settings
{
    public class GeneralSettingsViewModel : BaseViewModel
    {
        public ICommand ChangeBaseDirectoryCommand { get; }

        private readonly IConfigPathProvider _pathProvider;
        private readonly IConfigurationService _configService;

        public GeneralSettingsViewModel(IConfigurationService configService)
        {
            _configService = configService;
            _pathProvider = App.ServiceProvider?.GetService(typeof(YiboFile.Services.Config.IConfigPathProvider)) as YiboFile.Services.Config.IConfigPathProvider;
            ChangeBaseDirectoryCommand = new RelayCommand<string>(ChangeBaseDirectory);
            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            var config = _configService.GetSnapshot();

            _isMaximized = config.IsMaximized;
            _enableMultiWindow = config.EnableMultiWindow;
            _activateNewTabOnMiddleClick = config.ActivateNewTabOnMiddleClick;
            _isRightPanelVisible = config.IsRightPanelVisible;

            // 标签页正交维度设置
            _tabWidthStrategy = config.TabWidthStrategy;
            _tabOverflowStrategy = config.TabOverflowStrategy;
            _tabFixedWidth = config.TabFixedWidth > 0 ? config.TabFixedWidth : 140;
            _tabMaxWidth = config.TabMaxWidth > 0 ? config.TabMaxWidth : 200;
            _tabMinWidth = config.TabMinWidth > 0 ? config.TabMinWidth : 50;
            _hideCloseButtonOnInactive = config.HideCloseButtonOnInactive;
            _showOverflowArrows = config.ShowOverflowArrows;
            _showOverflowGradient = config.ShowOverflowGradient;

            _baseDirectory = _pathProvider?.BaseDirectory ?? ConfigManager.GetBaseDirectory();
        }

        // ═══════════════════════════════════════════
        //  窗口设置
        // ═══════════════════════════════════════════

        private bool _isRightPanelVisible;
        public bool IsRightPanelVisible
        {
            get => _isRightPanelVisible;
            set
            {
                if (SetProperty(ref _isRightPanelVisible, value))
                    _configService.Update(c => c.IsRightPanelVisible = value);
            }
        }

        private bool _isMaximized;
        public bool IsMaximized
        {
            get => _isMaximized;
            set
            {
                if (SetProperty(ref _isMaximized, value))
                    _configService.Update(c => c.IsMaximized = value);
            }
        }

        private bool _enableMultiWindow;
        public bool EnableMultiWindow
        {
            get => _enableMultiWindow;
            set
            {
                if (SetProperty(ref _enableMultiWindow, value))
                    _configService.Update(c => c.EnableMultiWindow = value);
            }
        }

        private bool _activateNewTabOnMiddleClick;
        public bool ActivateNewTabOnMiddleClick
        {
            get => _activateNewTabOnMiddleClick;
            set
            {
                if (SetProperty(ref _activateNewTabOnMiddleClick, value))
                    _configService.Update(c => c.ActivateNewTabOnMiddleClick = value);
            }
        }

        // ═══════════════════════════════════════════
        //  标签页设置（正交维度）
        // ═══════════════════════════════════════════

        private TabWidthStrategy _tabWidthStrategy;
        public TabWidthStrategy TabWidthStrategy
        {
            get => _tabWidthStrategy;
            set
            {
                if (SetProperty(ref _tabWidthStrategy, value))
                    _configService.Update(c => c.TabWidthStrategy = value);
            }
        }

        private TabOverflowStrategy _tabOverflowStrategy;
        public TabOverflowStrategy TabOverflowStrategy
        {
            get => _tabOverflowStrategy;
            set
            {
                if (SetProperty(ref _tabOverflowStrategy, value))
                    _configService.Update(c => c.TabOverflowStrategy = value);
            }
        }

        private double _tabFixedWidth;
        private string _tabFixedWidthInput;
        public double TabFixedWidth
        {
            get => _tabFixedWidth;
            set
            {
                value = Math.Clamp(value, 80, 250);
                bool changed = SetProperty(ref _tabFixedWidth, value);
                {
                    _tabFixedWidthInput = null;
                    OnPropertyChanged(nameof(TabFixedWidthInput));
                    if (changed) _configService.Update(c => c.TabFixedWidth = value);
                }
            }
        }

        public string TabFixedWidthInput
        {
            get => _tabFixedWidthInput ?? _tabFixedWidth.ToString();
            set => SetProtectedNumber(ref _tabFixedWidthInput, ref _tabFixedWidth, value, 80, 250, v => TabFixedWidth = v);
        }

        private double _tabMaxWidth;
        private string _tabMaxWidthInput;
        public double TabMaxWidth
        {
            get => _tabMaxWidth;
            set
            {
                value = Math.Clamp(value, 100, 300);
                bool changed = SetProperty(ref _tabMaxWidth, value);
                {
                    _tabMaxWidthInput = null;
                    OnPropertyChanged(nameof(TabMaxWidthInput));
                    if (changed) _configService.Update(c => c.TabMaxWidth = value);
                }
            }
        }

        public string TabMaxWidthInput
        {
            get => _tabMaxWidthInput ?? _tabMaxWidth.ToString();
            set => SetProtectedNumber(ref _tabMaxWidthInput, ref _tabMaxWidth, value, 100, 300, v => TabMaxWidth = v);
        }

        private double _tabMinWidth;
        private string _tabMinWidthInput;
        public double TabMinWidth
        {
            get => _tabMinWidth;
            set
            {
                value = Math.Clamp(value, 30, 100);
                bool changed = SetProperty(ref _tabMinWidth, value);
                {
                    _tabMinWidthInput = null;
                    OnPropertyChanged(nameof(TabMinWidthInput));
                    if (changed) _configService.Update(c => c.TabMinWidth = value);
                }
            }
        }

        public string TabMinWidthInput
        {
            get => _tabMinWidthInput ?? _tabMinWidth.ToString();
            set => SetProtectedNumber(ref _tabMinWidthInput, ref _tabMinWidth, value, 30, 100, v => TabMinWidth = v);
        }

        private bool _hideCloseButtonOnInactive;
        public bool HideCloseButtonOnInactive
        {
            get => _hideCloseButtonOnInactive;
            set
            {
                if (SetProperty(ref _hideCloseButtonOnInactive, value))
                    _configService.Update(c => c.HideCloseButtonOnInactive = value);
            }
        }

        private bool _showOverflowArrows;
        public bool ShowOverflowArrows
        {
            get => _showOverflowArrows;
            set
            {
                if (SetProperty(ref _showOverflowArrows, value))
                    _configService.Update(c => c.ShowOverflowArrows = value);
            }
        }

        private bool _showOverflowGradient;
        public bool ShowOverflowGradient
        {
            get => _showOverflowGradient;
            set
            {
                if (SetProperty(ref _showOverflowGradient, value))
                    _configService.Update(c => c.ShowOverflowGradient = value);
            }
        }

        // ═══════════════════════════════════════════
        //  路径设置
        // ═══════════════════════════════════════════

        private string _baseDirectory;
        public string BaseDirectory
        {
            get => _baseDirectory;
            set => SetProperty(ref _baseDirectory, value);
        }

        private void ChangeBaseDirectory(string newDir)
        {
            if (string.IsNullOrWhiteSpace(newDir)) return;

            var oldDir = _pathProvider?.BaseDirectory ?? ConfigManager.GetBaseDirectory();

            try
            {
                if (string.Equals(System.IO.Path.GetFullPath(oldDir.Trim()), System.IO.Path.GetFullPath(newDir.Trim()), StringComparison.OrdinalIgnoreCase))
                    return;
            }
            catch { return; }

            if (_pathProvider != null)
            {
                _pathProvider.UpdateBaseDirectory(newDir);
            }
            else
            {
                ConfigManager.SetBaseDirectory(newDir, copyMissingFromOld: true);
            }

            try { DatabaseManager.Initialize(); } catch { }

            LoadFromConfig();
            OnPropertyChanged(nameof(BaseDirectory));
        }
    }
}
