using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using OoiMRR.Controls;
using OoiMRR.Models;
using OoiMRR.Services.Config;
using OoiMRR.Services.Theming;
using OoiMRR.Windows;
using System.Windows.Controls.Primitives;

namespace OoiMRR.Controls.Settings
{
    public partial class AppearanceSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private ComboBox _themeComboBox;
        private Slider _opacitySlider;
        private TextBlock _opacityValueText;
        private CheckBox _animationsEnabledCheckBox;

        // 主题颜色预览
        private Border _previewPrimaryColor;
        private Border _previewBackgroundColor;
        private Border _previewSurfaceColor;
        private Border _previewTextColor;

        private bool _isLoadingSettings = false;

        // 详细颜色编辑相关
        private Popup _colorPickerPopup;
        private ColorPickerControl _colorPicker;
        private string _editingColorKey;
        private Dictionary<string, Border> _colorBlockMap = new Dictionary<string, Border>();

        // 颜色分组定义 (与ThemeColorEditorWindow保持一致)
        private static readonly Dictionary<string, List<(string Key, string Name)>> ColorGroups = new Dictionary<string, List<(string, string)>>
        {
            {
                "背景色", new List<(string, string)>
                {
                    ("BackgroundPrimaryBrush", "主背景"),
                    ("BackgroundSecondaryBrush", "次背景/面板"),
                    ("BackgroundTertiaryBrush", "三级背景/卡片"),
                    ("BackgroundElevatedBrush", "对话框/浮动"),
                    ("AppBackgroundBrush", "应用背景")
                }
            },
            {
                "文本色", new List<(string, string)>
                {
                    ("ForegroundPrimaryBrush", "主要文本"),
                    ("ForegroundSecondaryBrush", "次要文本"),
                    ("ForegroundDisabledBrush", "禁用文本"),
                    ("ForegroundOnAccentBrush", "强调色上的文本")
                }
            },
            {
                "强调色/交互", new List<(string, string)>
                {
                    ("AccentDefaultBrush", "主强调色"),
                    ("AccentHoverBrush", "悬停"),
                    ("AccentPressedBrush", "按下"),
                    ("AccentSelectedBrush", "选中"),
                    ("AccentLightBrush", "浅强调色背景")
                }
            },
            {
                "边框色", new List<(string, string)>
                {
                    ("BorderDefaultBrush", "默认边框"),
                    ("BorderSubtleBrush", "淡边框"),
                    ("BorderFocusBrush", "聚焦边框")
                }
            },
            {
                "控件状态", new List<(string, string)>
                {
                    ("ControlDefaultBrush", "控件默认"),
                    ("ControlHoverBrush", "控件悬停"),
                    ("ControlPressedBrush", "控件按下"),
                    ("ControlDisabledBrush", "控件禁用")
                }
            },
            {
                "语义颜色", new List<(string, string)>
                {
                    ("SuccessBrush", "成功"),
                    ("WarningBrush", "警告"),
                    ("ErrorBrush", "错误"),
                    ("InfoBrush", "信息")
                }
            },
            {
                "特殊用途", new List<(string, string)>
                {
                    ("ShadowBrush", "阴影"),
                    ("OverlayBrush", "遮罩"),
                    ("DividerBrush", "分隔线")
                }
            }
        };

        public AppearanceSettingsPanel()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            // Root Grid for 2-column layout
            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Left: Settings
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });                  // Spacer
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) }); // Right: Color Palette

            // ========================================
            // LEFT COLUMN: General Settings
            // ========================================
            var leftPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };

            // 1. Theme Selection
            var themeTitle = new TextBlock
            {
                Text = "主题设置",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            leftPanel.Children.Add(themeTitle);

            var themeDescription = new TextBlock
            {
                Text = "选择应用程序的颜色主题。跟随系统将根据Windows主题自动切换。",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap
            };
            themeDescription.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            leftPanel.Children.Add(themeDescription);

            var themeSelectionLabel = new TextBlock
            {
                Text = "选择主题：",
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 8)
            };
            themeSelectionLabel.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundPrimaryBrush");
            leftPanel.Children.Add(themeSelectionLabel);

            _themeComboBox = new ComboBox
            {
                MinHeight = 36,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 24)
            };
            _themeComboBox.SetResourceReference(ComboBox.BackgroundProperty, "BackgroundElevatedBrush");
            _themeComboBox.SetResourceReference(ComboBox.ForegroundProperty, "ForegroundPrimaryBrush");
            _themeComboBox.SetResourceReference(ComboBox.BorderBrushProperty, "BorderDefaultBrush");

            // Add Items
            _themeComboBox.Items.Add(new ThemeComboBoxItem
            {
                DisplayName = "🔄 跟随系统",
                ThemeId = "FollowSystem",
                Description = "自动跟随Windows系统主题设置"
            });
            _themeComboBox.Items.Add(new ThemeComboBoxItem
            {
                DisplayName = "──────────",
                ThemeId = "Separator",
                IsEnabled = false
            });

            var availableThemes = ThemeManager.GetAvailableThemes().OrderBy(t => t.Id).ToList();
            foreach (var theme in availableThemes)
            {
                string emoji = GetThemeEmoji(theme.Id);
                _themeComboBox.Items.Add(new ThemeComboBoxItem
                {
                    DisplayName = $"{emoji} {theme.DisplayName}",
                    ThemeId = theme.Id,
                    Description = theme.Description
                });
            }
            _themeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
            leftPanel.Children.Add(_themeComboBox);

            // 2. Custom Theme Buttons
            var customThemeTitle = new TextBlock
            {
                Text = "自定义主题",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 12)
            };
            leftPanel.Children.Add(customThemeTitle);

            var customThemeButtonsGrid = new Grid { Margin = new Thickness(0, 0, 0, 24) };
            customThemeButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            customThemeButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            customThemeButtonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var createCustomThemeButton = new Button
            {
                Content = "➕ 创建",
                Padding = new Thickness(16, 8, 16, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 0)
            };
            createCustomThemeButton.SetResourceReference(Button.BackgroundProperty, "AccentDefaultBrush");
            createCustomThemeButton.SetResourceReference(Button.ForegroundProperty, "ForegroundOnAccentBrush");
            createCustomThemeButton.Click += CreateCustomTheme_Click;
            Grid.SetColumn(createCustomThemeButton, 0);
            customThemeButtonsGrid.Children.Add(createCustomThemeButton);

            var manageCustomThemeButton = new Button
            {
                Content = "🔧 管理",
                Padding = new Thickness(16, 8, 16, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 0)
            };
            manageCustomThemeButton.Click += ManageCustomThemes_Click;
            Grid.SetColumn(manageCustomThemeButton, 2);
            customThemeButtonsGrid.Children.Add(manageCustomThemeButton);

            leftPanel.Children.Add(customThemeButtonsGrid);

            // 3. Theme Colors Preview
            var previewTitle = new TextBlock
            {
                Text = "主题颜色预览",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            leftPanel.Children.Add(previewTitle);

            var previewContainer = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 24),
                BorderThickness = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Stretch
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
            leftPanel.Children.Add(previewContainer);

            // 4. Opacity & Animation
            var opacityTitle = new TextBlock
            {
                Text = "界面效果",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 12, 0, 16)
            };
            leftPanel.Children.Add(opacityTitle);

            // Opacity
            var opacityGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            opacityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            opacityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            opacityGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var opacityLabel = new TextBlock
            {
                Text = "窗口透明度:",
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
                MinWidth = 50,
                TextAlignment = TextAlignment.Right,
                Text = "100%"
            };
            Grid.SetColumn(_opacityValueText, 2);
            opacityGrid.Children.Add(_opacityValueText);
            leftPanel.Children.Add(opacityGrid);

            // Animation
            _animationsEnabledCheckBox = new CheckBox
            {
                Content = "启用界面动画效果",
                FontSize = 14,
                MinHeight = 32,
                Margin = new Thickness(0, 0, 0, 4),
                IsChecked = true
            };
            _animationsEnabledCheckBox.Checked += AnimationsCheckBox_Changed;
            _animationsEnabledCheckBox.Unchecked += AnimationsCheckBox_Changed;
            leftPanel.Children.Add(_animationsEnabledCheckBox);

            var animationHint = new TextBlock
            {
                Text = "禁用动画可以提高性能，适合低配置设备。",
                FontSize = 12,
                Margin = new Thickness(24, 0, 0, 24),
                TextWrapping = TextWrapping.Wrap
            };
            animationHint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            leftPanel.Children.Add(animationHint);

            // Reset Button
            var resetButton = new Button
            {
                Content = "↺ 恢复默认主题",
                Padding = new Thickness(12, 8, 12, 8),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 12, 0, 0)
            };
            resetButton.SetResourceReference(Button.BackgroundProperty, "ControlDefaultBrush");
            resetButton.Click += ResetTheme_Click;
            leftPanel.Children.Add(resetButton);


            // ========================================
            // RIGHT COLUMN: Detailed Color Tuning
            // ========================================
            var rightPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 24) };

            var accentTitle = new TextBlock
            {
                Text = "主题颜色详情调节",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            rightPanel.Children.Add(accentTitle);

            var accentHint = new TextBlock
            {
                Text = "点击下方颜色块直接修改。修改将自动创建并应用“我的自定义主题”。",
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 24),
                TextWrapping = TextWrapping.Wrap
            };
            accentHint.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            rightPanel.Children.Add(accentHint);

            // Build Groups
            foreach (var group in ColorGroups)
            {
                // Group Header
                var groupHeader = new TextBlock
                {
                    Text = group.Key,
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(4, 0, 0, 8),
                    Opacity = 0.8
                };
                groupHeader.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundPrimaryBrush");
                rightPanel.Children.Add(groupHeader);

                // Colors Container
                var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };

                foreach (var (key, name) in group.Value)
                {
                    var colorBlockContainer = CreateColorBlockUi(key, name);
                    wrapPanel.Children.Add(colorBlockContainer);
                }

                rightPanel.Children.Add(wrapPanel);
            }

            // Initialize Popup
            InitializeColorPickerPopup();


            // ========================================
            // Add to Grid
            // ========================================
            Grid.SetColumn(leftPanel, 0);
            rootGrid.Children.Add(leftPanel);

            Grid.SetColumn(rightPanel, 2);
            rootGrid.Children.Add(rightPanel);

            // Wrap in ScrollViewer
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(24, 24, 36, 24), // More padding on right
                Content = rootGrid
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

                // 在ComboBox中查找匹配的主题
                foreach (ThemeComboBoxItem item in _themeComboBox.Items)
                {
                    if (item.ThemeId == themeMode)
                    {
                        _themeComboBox.SelectedItem = item;
                        break;
                    }
                }

                // 加载窗口透明度
                _opacitySlider.Value = config.WindowOpacity > 0 ? config.WindowOpacity : 1.0;

                // 加载动画设置
                _animationsEnabledCheckBox.IsChecked = config.AnimationsEnabled;

                // 刷新颜色块状态
                // RefreshColorBlocks(); -> No longer needed with DynamicResource
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
                if (_themeComboBox.SelectedItem is ThemeComboBoxItem selectedItem)
                {
                    config.ThemeMode = selectedItem.ThemeId;
                }
                else
                {
                    config.ThemeMode = "FollowSystem";
                }

                // 保存窗口透明度
                config.WindowOpacity = _opacitySlider.Value;

                // 保存动画设置
                config.AnimationsEnabled = _animationsEnabledCheckBox.IsChecked ?? true;
            });
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

        private void CreateCustomTheme_Click(object sender, RoutedEventArgs e)
        {
            // 获取当前主题作为基础
            var config = ConfigurationService.Instance.GetSnapshot();
            var baseTheme = config.ThemeMode == "Dark" ? "Dark" : "Light";

            // 打开主题编辑窗口
            var editorWindow = new ThemeColorEditorWindow(baseTheme)
            {
                Owner = Window.GetWindow(this)
            };

            if (editorWindow.ShowDialog() == true && editorWindow.Result != null)
            {
                // 应用新创建的主题
                try
                {
                    CustomThemeManager.Apply(editorWindow.Result);
                    MessageBox.Show($"自定义主题\"{editorWindow.Result.Name}\"已创建并应用！",
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"应用主题失败: {ex.Message}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ManageCustomThemes_Click(object sender, RoutedEventArgs e)
        {
            // 加载所有自定义主题
            var customThemes = CustomThemeManager.LoadAll();

            if (customThemes.Count == 0)
            {
                MessageBox.Show("还没有自定义主题。\n\n点击\"创建自定义主题\"来创建你的第一个主题！",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 创建主题管理对话框
            var dialog = new Window
            {
                Title = "管理自定义主题",
                Width = 600,
                Height = 450,
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dialog.SetResourceReference(Window.BackgroundProperty, "BackgroundPrimaryBrush");

            var stackPanel = new StackPanel { Margin = new Thickness(16, 16, 16, 16) };

            var titleText = new TextBlock
            {
                Text = $"自定义主题列表 ({customThemes.Count})",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 16)
            };
            stackPanel.Children.Add(titleText);

            var scrollViewer = new ScrollViewer
            {
                Height = 300,
                Margin = new Thickness(0, 0, 0, 16)
            };

            var themesPanel = new StackPanel();

            foreach (var theme in customThemes)
            {
                var themeCard = CreateThemeCard(theme, dialog);
                themesPanel.Children.Add(themeCard);
            }

            scrollViewer.Content = themesPanel;
            stackPanel.Children.Add(scrollViewer);

            var closeButton = new Button
            {
                Content = "关闭",
                Padding = new Thickness(24, 8, 24, 8),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (s, args) => dialog.Close();
            stackPanel.Children.Add(closeButton);

            dialog.Content = stackPanel;
            dialog.ShowDialog();
        }

        private Border CreateThemeCard(CustomTheme theme, Window parentDialog)
        {
            var card = new Border
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(16, 16, 16, 16),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                MaxWidth = 600,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            card.SetResourceReference(Border.BackgroundProperty, "BackgroundSecondaryBrush");
            card.SetResourceReference(Border.BorderBrushProperty, "BorderDefaultBrush");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 左侧：主题信息
            var infoPanel = new StackPanel();

            var nameText = new TextBlock
            {
                Text = theme.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            nameText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundPrimaryBrush");
            infoPanel.Children.Add(nameText);

            var detailsText = new TextBlock
            {
                Text = $"基于: {theme.BaseTheme} | 创建于: {theme.CreatedAt:yyyy-MM-dd}",
                FontSize = 11
            };
            detailsText.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundSecondaryBrush");
            infoPanel.Children.Add(detailsText);

            Grid.SetColumn(infoPanel, 0);
            grid.Children.Add(infoPanel);

            // 右侧：按钮
            var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var applyButton = new Button
            {
                Content = "应用",
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0)
            };
            applyButton.SetResourceReference(Button.BackgroundProperty, "AccentDefaultBrush");
            applyButton.SetResourceReference(Button.ForegroundProperty, "ForegroundOnAccentBrush");
            applyButton.Click += (s, e) =>
            {
                try
                {
                    CustomThemeManager.Apply(theme);
                    MessageBox.Show($"已应用主题\"{theme.Name}\"", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    parentDialog.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"应用主题失败: {ex.Message}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            buttonsPanel.Children.Add(applyButton);

            var editButton = new Button
            {
                Content = "编辑",
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0)
            };
            editButton.Click += (s, e) =>
            {
                var editorWindow = new ThemeColorEditorWindow(theme)
                {
                    Owner = parentDialog
                };

                if (editorWindow.ShowDialog() == true)
                {
                    MessageBox.Show("主题已保存", "成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    parentDialog.Close();
                    // 重新打开管理窗口以刷新列表
                    ManageCustomThemes_Click(this, new RoutedEventArgs());
                }
            };
            buttonsPanel.Children.Add(editButton);

            var deleteButton = new Button
            {
                Content = "删除",
                Padding = new Thickness(12, 6, 12, 6)
            };
            deleteButton.SetResourceReference(Button.ForegroundProperty, "ErrorBrush");
            deleteButton.Click += (s, e) =>
            {
                var result = MessageBox.Show(
                    $"确定要删除主题\"{theme.Name}\"吗？\n此操作无法撤销。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        CustomThemeManager.Delete(theme.Id);
                        MessageBox.Show("主题已删除", "成功",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        parentDialog.Close();
                        // 重新打开管理窗口以刷新列表
                        ManageCustomThemes_Click(this, new RoutedEventArgs());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除失败: {ex.Message}", "错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            };
            buttonsPanel.Children.Add(deleteButton);

            Grid.SetColumn(buttonsPanel, 1);
            grid.Children.Add(buttonsPanel);

            card.Child = grid;
            return card;
        }

        /// <summary>
        /// 根据主题ID获取对应的emoji图标
        /// </summary>
        private string GetThemeEmoji(string themeId)
        {
            return themeId switch
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
        }

        /// <summary>
        /// 主题ComboBox选择变化事件
        /// </summary>
        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            if (_themeComboBox.SelectedItem is not ThemeComboBoxItem selectedItem) return;
            if (selectedItem.ThemeId == "Separator")
            {
                // 如果选中了分隔线，恢复之前的选择
                e.Handled = true;
                return;
            }

            if (selectedItem.ThemeId == "FollowSystem")
            {
                ThemeManager.EnableSystemThemeFollowing();
            }
            else
            {
                ThemeManager.DisableSystemThemeFollowing();
                ThemeManager.SetTheme(selectedItem.ThemeId, animate: _animationsEnabledCheckBox?.IsChecked ?? true);
            }

            SaveSettings();
        }

        private void ApplyAccentColor(string hexColor)
        {
            try
            {
                // 1. 确定基准主题 (如果是Light/Dark等内置主题，以此为基准；如果是已有自定义主题，以此为基准)
                // 简化逻辑：总是基于当前正在运行的主题颜色创建/更新一个名为 "我的自定义主题" 的配置

                var currentId = ConfigurationService.Instance.GetSnapshot().ThemeMode;
                string baseTheme = "Light";

                if (currentId == "Dark" || currentId == "Sunset" || currentId == "Ocean" || currentId == "Purple" || currentId == "Night")
                    baseTheme = "Dark";
                else if (currentId != "FollowSystem")
                    baseTheme = "Light"; // 默认为Light

                // 2. 创建一个基于当前视觉状态的自定义主题
                // 我们使用 "QuickCustom" 作为专用ID来存储这种快速修改
                var theme = CustomThemeManager.CreateFromCurrent("我的自定义主题", baseTheme);
                theme.Id = "QuickCustomTheme";

                // 3. 覆盖强调色相关的所有画笔
                // 简单的算法：悬停变亮，按下变暗
                var baseColor = (Color)ColorConverter.ConvertFromString(hexColor);

                theme.Colors["AccentDefaultBrush"] = hexColor;
                theme.Colors["AccentHoverBrush"] = ChangeColorBrightness(baseColor, 0.2f); // 亮20%
                theme.Colors["AccentPressedBrush"] = ChangeColorBrightness(baseColor, -0.2f); // 暗20%
                theme.Colors["AccentSelectedBrush"] = hexColor;
                theme.Colors["ControlFocusBrush"] = hexColor;
                theme.Colors["BorderFocusBrush"] = hexColor;
                theme.Colors["ForegroundOnAccentBrush"] = "#FFFFFF"; // 假设强调色总是深色，配白字

                // 4. 保存并应用
                CustomThemeManager.Save(theme);
                CustomThemeManager.Apply(theme);

                // 5. 更新配置为使用该自定义主题
                // 我们需要确保ComboBox选中它（如果不在列表中，需添加）
                UpdateThemeComboBoxSelection(theme);

                // 保存设置
                ConfigurationService.Instance.Update(config => config.ThemeMode = theme.Id);

                // 提示用户
                // MessageBox.Show("强调色已更新！", "提示", MessageBoxButton.OK, MessageBoxImage.Information); 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"应用颜色失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetTheme_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 恢复到跟随系统，或者默认为Light
                var defaultTheme = "FollowSystem";

                // 2. 更新配置
                ConfigurationService.Instance.Update(config => config.ThemeMode = defaultTheme);

                // 3. 触发主题切换逻辑 (LoadSettings会处理ComboBox选中，SelectionChanged会触发ThemeManager)
                LoadSettings();

                // 4. 强制刷新ComboBox选定项的事件以应用主题
                if (_themeComboBox.SelectedItem is ThemeComboBoxItem item && item.ThemeId == defaultTheme)
                {
                    ThemeManager.EnableSystemThemeFollowing(); // 显式调用以防ComboBox没触发
                }

                MessageBox.Show("主题已恢复默认设置。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateThemeComboBoxSelection(CustomTheme customTheme)
        {
            // 检查ComboBox中是否已有该项
            bool found = false;
            foreach (ThemeComboBoxItem item in _themeComboBox.Items)
            {
                if (item.ThemeId == customTheme.Id)
                {
                    _themeComboBox.SelectedItem = item;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                // 如果是新创建的自定义主题，添加到列表
                var newItem = new ThemeComboBoxItem
                {
                    DisplayName = "🎨 " + customTheme.Name,
                    ThemeId = customTheme.Id,
                    Description = "用户自定义主题"
                };

                // 插入到'创建自定义主题'分隔线之前，或者直接添加到最后
                _themeComboBox.Items.Add(newItem);
                _themeComboBox.SelectedItem = newItem;
            }
        }

        /// <summary>
        /// 创建单个颜色调节块UI
        /// </summary>
        private FrameworkElement CreateColorBlockUi(string key, string name)
        {
            var container = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 12, 8), Width = 70 };

            var border = new Border
            {
                Width = 48,
                Height = 32,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                // Background = Brushes.Transparent, // Removed
                Tag = key // 存储key
            };
            // Use DynamicResource for auto-updates!
            border.SetResourceReference(Border.BackgroundProperty, key);
            border.SetResourceReference(Border.BorderBrushProperty, "BorderDefaultBrush");

            // 点击事件
            border.MouseLeftButtonUp += (s, e) =>
            {
                _editingColorKey = key;
                OpenColorPicker(border);
            };

            // 存储引用以便后续更新
            _colorBlockMap[key] = border;

            var label = new TextBlock
            {
                Text = name,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
                Height = 28
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");

            container.Children.Add(border);
            container.Children.Add(label);

            return container;
        }

        private void InitializeColorPickerPopup()
        {
            _colorPicker = new ColorPickerControl { Width = 280, Height = 350 };
            _colorPicker.ColorChanged += OnColorPickerChanged;

            var border = new Border
            {
                Child = _colorPicker,
                CornerRadius = new CornerRadius(8),
                Background = Brushes.White,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.3 }
            };

            _colorPickerPopup = new Popup
            {
                Child = border,
                StaysOpen = false,
                Placement = PlacementMode.Bottom,
                PopupAnimation = PopupAnimation.Fade,
                AllowsTransparency = true
            };
        }

        private void OpenColorPicker(Border targetBlock)
        {
            if (_colorPickerPopup == null || _colorPicker == null) return;

            // 获取当前颜色
            if (targetBlock.Background is SolidColorBrush brush)
            {
                _colorPicker.SelectedColor = brush.Color;
            }

            _colorPickerPopup.PlacementTarget = targetBlock;
            _colorPickerPopup.IsOpen = true;
        }

        private void OnColorPickerChanged(object sender, Color newColor)
        {
            if (string.IsNullOrEmpty(_editingColorKey)) return;

            UpdateSingleColor(_editingColorKey, newColor);

            // UI update handled by DynamicResource
        }

        private void UpdateSingleColor(string key, Color color)
        {
            try
            {
                // 1. 确定基准主题
                var currentId = ConfigurationService.Instance.GetSnapshot().ThemeMode;
                string baseTheme = "Light";

                if (currentId == "Dark" || currentId == "Sunset" || currentId == "Ocean" || currentId == "Purple" || currentId == "Night")
                    baseTheme = "Dark";
                else if (currentId != "FollowSystem" && currentId != "QuickCustomTheme")
                    baseTheme = "Light";
                // 如果当前已经是自定义主题，baseTheme其实不重要， CreateFromCurrent会处理

                // 2. 创建或更新 "QuickCustomTheme"
                // 始终使用 CreateFromCurrent 以捕获当前所有修改过的资源值
                var theme = CustomThemeManager.CreateFromCurrent("我的自定义主题", baseTheme);
                theme.Id = "QuickCustomTheme";

                // 3. 应用新颜色
                var hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                theme.Colors[key] = hexColor;

                // 4. 保存并应用
                CustomThemeManager.Save(theme);
                CustomThemeManager.Apply(theme);

                // 5. 更新配置
                UpdateThemeComboBoxSelection(theme);
                ConfigurationService.Instance.Update(config => config.ThemeMode = theme.Id);

                // UI update is handled automatically by DynamicResource bindings on the border
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Apply color failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新所有颜色块的显示以匹配当前资源
        /// </summary>
        // Removed RefreshColorBlocks as it is replaced by DynamicResource binding

        /// <summary>
        /// 调整颜色亮度
        /// factor: >0 变亮, <0 变暗
        /// </summary>
        private string ChangeColorBrightness(Color color, float factor)
        {
            float r = (float)color.R;
            float g = (float)color.G;
            float b = (float)color.B;

            if (factor < 0)
            {
                factor = 1 + factor;
                r *= factor;
                g *= factor;
                b *= factor;
            }
            else
            {
                r = (255 - r) * factor + r;
                g = (255 - g) * factor + g;
                b = (255 - b) * factor + b;
            }

            return $"#{((byte)r):X2}{((byte)g):X2}{((byte)b):X2}";
        }
    }

    /// <summary>
    /// 主题ComboBox项
    /// </summary>
    public class ThemeComboBoxItem
    {
        public string DisplayName { get; set; }
        public string ThemeId { get; set; }
        public string Description { get; set; }
        public bool IsEnabled { get; set; } = true;

        public override string ToString()
        {
            return DisplayName;
        }
    }
}
