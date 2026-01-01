using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using OoiMRR.Controls;
using OoiMRR.Services.Config;
using OoiMRR.Services.Theming;

namespace OoiMRR.Controls.Settings
{
    public partial class AppearanceSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private RadioButton _themeLightRadio;
        private RadioButton _themeDarkRadio;
        private RadioButton _themeFollowSystemRadio;
        private Slider _opacitySlider;
        private TextBlock _opacityValueText;
        private CheckBox _animationsEnabledCheckBox;

        // 主题颜色预览
        private Border _previewPrimaryColor;
        private Border _previewBackgroundColor;
        private Border _previewSurfaceColor;
        private Border _previewTextColor;

        private bool _isLoadingSettings = false;

        public AppearanceSettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            var stackPanel = new StackPanel { Margin = new Thickness(0) };

            // ========================================
            // 主题选择区域
            // ========================================
            var themeTitle = new TextBlock
            {
                Text = "主题设置",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(themeTitle);

            var themeDescription = new TextBlock
            {
                Text = "选择应用程序的颜色主题。跟随系统将根据Windows主题自动切换。",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            };
            themeDescription.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            stackPanel.Children.Add(themeDescription);

            // 主题选项
            _themeLightRadio = new RadioButton
            {
                Content = "浅色模式 (Light)",
                GroupName = "ThemeMode",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _themeLightRadio.Checked += ThemeRadio_Checked;
            stackPanel.Children.Add(_themeLightRadio);

            _themeDarkRadio = new RadioButton
            {
                Content = "深色模式 (Dark)",
                GroupName = "ThemeMode",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _themeDarkRadio.Checked += ThemeRadio_Checked;
            stackPanel.Children.Add(_themeDarkRadio);

            _themeFollowSystemRadio = new RadioButton
            {
                Content = "跟随系统",
                GroupName = "ThemeMode",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 24)
            };
            _themeFollowSystemRadio.Checked += ThemeRadio_Checked;
            stackPanel.Children.Add(_themeFollowSystemRadio);

            // ========================================
            // 主题颜色预览
            // ========================================
            var previewTitle = new TextBlock
            {
                Text = "主题颜色预览",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stackPanel.Children.Add(previewTitle);

            // 颜色预览容器 - 使用卡片样式
            var previewContainer = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 24),
                BorderThickness = new Thickness(1)
            };
            previewContainer.SetResourceReference(Border.BackgroundProperty, "BackgroundSecondaryBrush");
            previewContainer.SetResourceReference(Border.BorderBrushProperty, "BorderDefaultBrush");

            var previewGrid = new Grid();
            previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _previewPrimaryColor = CreateColorPreview("主色调", "AccentDefaultBrush", 0);
            _previewBackgroundColor = CreateColorPreview("背景色", "BackgroundPrimaryBrush", 1);
            _previewSurfaceColor = CreateColorPreview("表面色", "BackgroundSecondaryBrush", 2);
            _previewTextColor = CreateColorPreview("文本色", "ForegroundPrimaryBrush", 3);

            previewGrid.Children.Add(_previewPrimaryColor);
            previewGrid.Children.Add(_previewBackgroundColor);
            previewGrid.Children.Add(_previewSurfaceColor);
            previewGrid.Children.Add(_previewTextColor);

            previewContainer.Child = previewGrid;
            stackPanel.Children.Add(previewContainer);

            // ========================================
            // 窗口透明度
            // ========================================
            var opacityTitle = new TextBlock
            {
                Text = "窗口透明度",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 24, 0, 16)
            };
            stackPanel.Children.Add(opacityTitle);

            var opacityGrid = new Grid { Margin = new Thickness(0, 0, 0, 24) };
            opacityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            opacityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            opacityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var opacityLabel = new TextBlock
            {
                Text = "透明度:",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                MinWidth = 80
            };
            Grid.SetColumn(opacityLabel, 0);
            opacityGrid.Children.Add(opacityLabel);

            _opacitySlider = new Slider
            {
                Minimum = 0.5,
                Maximum = 1.0,
                Value = 1.0,
                TickFrequency = 0.05,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            _opacitySlider.ValueChanged += OpacitySlider_ValueChanged;
            Grid.SetColumn(_opacitySlider, 1);
            opacityGrid.Children.Add(_opacitySlider);

            _opacityValueText = new TextBlock
            {
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 60,
                Text = "100%"
            };
            Grid.SetColumn(_opacityValueText, 2);
            opacityGrid.Children.Add(_opacityValueText);

            stackPanel.Children.Add(opacityGrid);

            // ========================================
            // 动画效果
            // ========================================
            var animationTitle = new TextBlock
            {
                Text = "动画效果",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 24, 0, 16)
            };
            stackPanel.Children.Add(animationTitle);

            _animationsEnabledCheckBox = new CheckBox
            {
                Content = "启用界面动画效果（主题切换、窗口过渡等）",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 8),
                IsChecked = true
            };
            _animationsEnabledCheckBox.Checked += AnimationsCheckBox_Changed;
            _animationsEnabledCheckBox.Unchecked += AnimationsCheckBox_Changed;
            stackPanel.Children.Add(_animationsEnabledCheckBox);

            var animationHint = new TextBlock
            {
                Text = "禁用动画可以提高性能，适合低配置设备。",
                FontSize = 12,
                Margin = new Thickness(20, 0, 0, 24),
                TextWrapping = TextWrapping.Wrap
            };
            animationHint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            stackPanel.Children.Add(animationHint);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = stackPanel
            };

            Content = scrollViewer;
        }

        private Border CreateColorPreview(string label, string resourceKey, int column)
        {
            var container = new StackPanel
            {
                Margin = new Thickness(0, 0, column < 3 ? 12 : 0, 0)
            };

            var colorLabel = new TextBlock
            {
                Text = label,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontWeight = FontWeights.Medium
            };
            colorLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            container.Children.Add(colorLabel);

            var colorBorder = new Border
            {
                Height = 80,  // 增大高度
                CornerRadius = new CornerRadius(6),  // 更圆润的圆角
                BorderThickness = new Thickness(2)
            };
            colorBorder.SetResourceReference(Border.BackgroundProperty, resourceKey);
            colorBorder.SetResourceReference(Border.BorderBrushProperty, "BorderDefaultBrush");

            // 添加悬停效果
            colorBorder.MouseEnter += (s, e) =>
            {
                colorBorder.BorderThickness = new Thickness(3);
            };
            colorBorder.MouseLeave += (s, e) =>
            {
                colorBorder.BorderThickness = new Thickness(2);
            };

            container.Children.Add(colorBorder);

            var wrapper = new Border { Child = container };
            Grid.SetColumn(wrapper, column);
            return wrapper;
        }

        public void LoadSettings()
        {
            _isLoadingSettings = true;
            try
            {
                var config = ConfigurationService.Instance.GetSnapshot();

                // 加载主题模式
                var themeMode = config.ThemeMode ?? "FollowSystem";
                if (themeMode == "Light")
                    _themeLightRadio.IsChecked = true;
                else if (themeMode == "Dark")
                    _themeDarkRadio.IsChecked = true;
                else
                    _themeFollowSystemRadio.IsChecked = true;

                // 加载窗口透明度
                _opacitySlider.Value = config.WindowOpacity > 0 ? config.WindowOpacity : 1.0;

                // 加载动画设置
                _animationsEnabledCheckBox.IsChecked = config.AnimationsEnabled;
            }
            finally
            {
                _isLoadingSettings = false;
            }
        }

        public void SaveSettings()
        {
            if (_isLoadingSettings) return;

            ConfigurationService.Instance.Update(config =>
            {
                // 保存主题模式
                if (_themeLightRadio.IsChecked == true)
                    config.ThemeMode = "Light";
                else if (_themeDarkRadio.IsChecked == true)
                    config.ThemeMode = "Dark";
                else
                    config.ThemeMode = "FollowSystem";

                // 保存窗口透明度
                config.WindowOpacity = _opacitySlider.Value;

                // 保存动画设置
                config.AnimationsEnabled = _animationsEnabledCheckBox.IsChecked ?? true;
            });
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            // 立即应用主题
            if (_themeLightRadio.IsChecked == true)
            {
                ThemeManager.SetTheme("Light", animate: _animationsEnabledCheckBox.IsChecked ?? true);
            }
            else if (_themeDarkRadio.IsChecked == true)
            {
                ThemeManager.SetTheme("Dark", animate: _animationsEnabledCheckBox.IsChecked ?? true);
            }
            else if (_themeFollowSystemRadio.IsChecked == true)
            {
                // 应用系统主题
                var systemTheme = DetectSystemTheme();
                ThemeManager.SetTheme(systemTheme, animate: _animationsEnabledCheckBox.IsChecked ?? true);
            }

            SaveSettings();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_opacityValueText != null)
            {
                _opacityValueText.Text = $"{(int)(_opacitySlider.Value * 100)}%";
            }

            if (_isLoadingSettings) return;

            // 实时应用透明度到主窗口
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                mainWindow.Opacity = _opacitySlider.Value;
            }

            SaveSettings();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void AnimationsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            SaveSettings();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 检测系统主题（Light或Dark）
        /// </summary>
        private string DetectSystemTheme()
        {
            try
            {
                // 读取注册表检测Windows主题
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("AppsUseLightTheme");
                        if (value is int intValue)
                        {
                            // 0 = Dark, 1 = Light
                            return intValue == 0 ? "Dark" : "Light";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to detect system theme: {ex.Message}");
            }

            // 默认返回浅色
            return "Light";
        }
    }
}
