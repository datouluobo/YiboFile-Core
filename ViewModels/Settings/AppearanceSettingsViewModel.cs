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

        public AppearanceSettingsViewModel()
        {
            ResetThemeCommand = new RelayCommand(ResetTheme);
            ApplyAccentColorCommand = new RelayCommand<string>(ApplyAccentColor);

            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            var config = ConfigurationService.Instance.GetSnapshot();
            _windowOpacity = config.WindowOpacity > 0 ? config.WindowOpacity : 1.0;
            _enableAnimations = config.AnimationsEnabled;

            InitializeThemes(config);
            InitializeIconStyles(config);
            InitializeUIStyles(config);
        }

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

        private ItemViewModel _selectedUIStyle;
        public ItemViewModel SelectedUIStyle
        {
            get => _selectedUIStyle;
            set
            {
                if (SetProperty(ref _selectedUIStyle, value) && value != null)
                {
                    ThemeManager.SetUIStyle(value.Id);
                    ConfigurationService.Instance.Update(c => c.UIStyle = value.Id);
                }
            }
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
            OnPropertyChanged(nameof(Themes));
            OnPropertyChanged(nameof(SelectedTheme));
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
            OnPropertyChanged(nameof(IconStyles));
            OnPropertyChanged(nameof(SelectedIconStyle));
        }

        private void InitializeUIStyles(AppConfig config)
        {
            UIStyles = new ObservableCollection<ItemViewModel>
            {
                new ItemViewModel { Id = "Original", Name = "经典 (当前风格)" },
                new ItemViewModel { Id = "Fluent", Name = "Fluent Design (Windows 11原生风)" },
                new ItemViewModel { Id = "MacOS", Name = "极简悬浮 (类MacOS卡片风)" },
                new ItemViewModel { Id = "Geek", Name = "Geek Studio (极客工业紧凑风)" }
            };
            var currentUIStyle = config.UIStyle ?? "Original";
            _selectedUIStyle = UIStyles.FirstOrDefault(x => x.Id == currentUIStyle) ?? UIStyles.First();
            OnPropertyChanged(nameof(UIStyles));
            OnPropertyChanged(nameof(SelectedUIStyle));
        }

        public void RefreshThemes()
        {
            var config = ConfigurationService.Instance.GetSnapshot();
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

                var config = ConfigurationService.Instance.GetSnapshot();
                InitializeThemes(config);
                RefreshThemes(); // Force refresh UI list
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
    }

    public class ItemViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
