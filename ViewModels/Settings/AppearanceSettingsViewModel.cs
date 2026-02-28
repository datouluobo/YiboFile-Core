using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Services.Config;
using YiboFile.Services.Theming;

namespace YiboFile.ViewModels.Settings
{
    public class AppearanceSettingsViewModel : BaseViewModel
    {
        public ObservableCollection<ThemeItemViewModel> Themes { get; set; }
        public ObservableCollection<IconStyleItemViewModel> IconStyles { get; set; }
        public ObservableCollection<ItemViewModel> UIStyles { get; set; }

        public ICommand ResetThemeCommand { get; }
        public ICommand ApplyAccentColorCommand { get; }

        private readonly YiboFile.Services.Theming.IThemeService _themeService;
        private readonly IConfigurationService _configService;

        public AppearanceSettingsViewModel(YiboFile.Services.Theming.IThemeService themeService, IConfigurationService configService)
        {
            _themeService = themeService;
            _configService = configService;
            ResetThemeCommand = new RelayCommand(ResetTheme);
            ApplyAccentColorCommand = new RelayCommand<string>(ApplyAccentColor);

            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            var config = _configService.GetSnapshot();
            _windowOpacity = config.WindowOpacity > 0 ? config.WindowOpacity : 1.0;
            _enableAnimations = config.AnimationsEnabled;
            _uiFontSize = config.UIFontSize > 0 ? config.UIFontSize : 16;

            InitializeThemes(config);
            InitializeIconStyles(config);
            InitializeUIStyles(config);
        }

        private double _uiFontSize;
        private string _uiFontSizeInput;
        public double UIFontSize
        {
            get => _uiFontSize;
            set
            {
                value = Math.Clamp(value, 10, 48);
                bool changed = SetProperty(ref _uiFontSize, value);
                {
                    _uiFontSizeInput = null;
                    OnPropertyChanged(nameof(UIFontSizeInput));
                    if (changed)
                    {
                        _configService?.Update(c => c.UIFontSize = value);
                        if (System.Windows.Application.Current?.MainWindow != null)
                        {
                            System.Windows.Application.Current.MainWindow.FontSize = value;
                        }
                    }
                }
            }
        }

        public string UIFontSizeInput
        {
            get => _uiFontSizeInput ?? _uiFontSize.ToString();
            set => SetProtectedNumber(ref _uiFontSizeInput, ref _uiFontSize, value, 10, 48, v => UIFontSize = v);
        }

        private double _windowOpacity;
        public double WindowOpacity
        {
            get => _windowOpacity;
            set
            {
                if (SetProperty(ref _windowOpacity, value))
                {
                    _configService.Update(c => c.WindowOpacity = value);
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
                    _configService.Update(c => c.AnimationsEnabled = value);
            }
        }

        private ThemeItemViewModel _selectedTheme;
        public ThemeItemViewModel SelectedTheme
        {
            get 
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] GET SelectedTheme => {_selectedTheme?.Id ?? "null"}");
                return _selectedTheme;
            }
            set
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] SET SelectedTheme called with value => {value?.Id ?? "null"}");
                if (value == null) return;
                if (SetProperty(ref _selectedTheme, value))
                {
                    if (value.Id == "FollowSystem")
                        _themeService.EnableSystemThemeFollowing();
                    else
                    {
                        _themeService.DisableSystemThemeFollowing();
                        _themeService.SetTheme(value.Id, animate: _enableAnimations);
                    }
                    _configService.Update(c => c.ThemeMode = value.Id);
                }
            }
        }

        private IconStyleItemViewModel _selectedIconStyle;
        public IconStyleItemViewModel SelectedIconStyle
        {
            get => _selectedIconStyle;
            set
            {
                if (value == null) return;
                if (SetProperty(ref _selectedIconStyle, value))
                {
                    _themeService.SetIconStyle(value.Id);
                    _configService.Update(c => c.IconStyle = value.Id);
                }
            }
        }

        private ItemViewModel _selectedUIStyle;
        public ItemViewModel SelectedUIStyle
        {
            get => _selectedUIStyle;
            set
            {
                if (value == null) return;
                if (SetProperty(ref _selectedUIStyle, value))
                {
                    _themeService.SetUIStyle(value.Id);
                    _configService.Update(c => c.UIStyle = value.Id);
                }
            }
        }

        private void InitializeThemes(AppConfig config)
        {
            Themes = new ObservableCollection<ThemeItemViewModel>
            {
                new ThemeItemViewModel("FollowSystem", "跟随系统", "💻")
            };

            foreach (var theme in _themeService.AvailableThemes)
            {
                string emoji = theme.Id switch
                {
                    "Light" => "☀️",
                    "Dark" => "🌙",
                    "Ocean" => "🌊",
                    "Forest" => "🌲",
                    "Sunset" => "🌅",
                    "Purple" => "💜",
                    "Nordic" => "🏔️",
                    _ => "🎨"
                };
                Themes.Add(new ThemeItemViewModel(theme.Id, theme.DisplayName, emoji));
            }

            foreach (var ct in _themeService.CustomThemes)
                Themes.Add(new ThemeItemViewModel(ct.Id, ct.Name, "🎨"));

            var currentTheme = config.ThemeMode ?? "FollowSystem";
            _selectedTheme = Themes.FirstOrDefault(x => x.Id == currentTheme) ?? Themes.First();
            OnPropertyChanged(nameof(Themes));
            OnPropertyChanged(nameof(SelectedTheme));
        }

        private void InitializeIconStyles(AppConfig config)
        {
            IconStyles = new ObservableCollection<IconStyleItemViewModel>();
            foreach (var icon in _themeService.AvailableIconStyles)
            {
                string prefix = icon.Id switch
                {
                    "Emoji" => "🌈 ",
                    "Remix" => "✒️ ",
                    "Fluent" => "💠 ",
                    "Material" => "✨ ",
                    "Lucide" => "🚀 ",
                    "Pixel" => "👾 ",
                    "Prism" => "💎 ",
                    "Tabler" => "📋 ",
                    "Phosphor" => "💡 ",
                    _ => "📦 "
                };
                IconStyles.Add(new IconStyleItemViewModel(icon.Id, prefix + icon.DisplayName));
            }
            
            var currentIconStyle = config.IconStyle ?? "Emoji";
            _selectedIconStyle = IconStyles.FirstOrDefault(x => x.Id == currentIconStyle) ?? IconStyles.First();
            OnPropertyChanged(nameof(IconStyles));
            OnPropertyChanged(nameof(SelectedIconStyle));
        }

        private void InitializeUIStyles(AppConfig config)
        {
            UIStyles = new ObservableCollection<ItemViewModel>();
            foreach (var ui in _themeService.AvailableUIStyles)
            {
                UIStyles.Add(new ItemViewModel { Id = ui.Id, Name = $"{ui.DisplayName} ({ui.Description})" });
            }
            
            var currentUIStyle = config.UIStyle ?? "Original";
            _selectedUIStyle = UIStyles.FirstOrDefault(x => x.Id == currentUIStyle) ?? UIStyles.First();
            OnPropertyChanged(nameof(UIStyles));
            OnPropertyChanged(nameof(SelectedUIStyle));
        }

        /// <summary>
        /// 在 UI 绑定建立完成后调用，强制重新通知所有选中项属性，
        /// 解决 WPF ComboBox 绑定时序导致的初始值不显示问题。
        /// </summary>
        public void RefreshBindings()
        {
            OnPropertyChanged(nameof(Themes));
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(IconStyles));
            OnPropertyChanged(nameof(SelectedIconStyle));
            OnPropertyChanged(nameof(UIStyles));
            OnPropertyChanged(nameof(SelectedUIStyle));
            OnPropertyChanged(nameof(WindowOpacity));
            OnPropertyChanged(nameof(EnableAnimations));
        }

        public void RefreshThemes()
        {
            var config = _configService.GetSnapshot();
            InitializeThemes(config);
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
                var currentId = _configService.GetSnapshot().ThemeMode;
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

                var config = _configService.GetSnapshot();
                InitializeThemes(config);
                RefreshThemes(); // Force refresh UI list
                SelectedTheme = Themes.FirstOrDefault(t => t.Id == theme.Id);

                _configService.Update(c => c.ThemeMode = theme.Id);
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
    }

    public class ItemViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is ItemViewModel other)
                return Id == other.Id;
            return false;
        }

        public override int GetHashCode() => Id?.GetHashCode() ?? 0;
    }
}

