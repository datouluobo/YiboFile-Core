using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YiboFile.Models;
using YiboFile.Services.Theming;

namespace YiboFile.Windows
{
    /// <summary>
    /// 主题颜色编辑窗口
    /// </summary>
    public partial class ThemeColorEditorWindow : Window
    {
        private CustomTheme _theme;
        private string _selectedColorKey;
        private Dictionary<string, Border> _colorPreviews = new Dictionary<string, Border>();

        // 颜色分组定义
        private static readonly Dictionary<string, List<(string Key, string Name)>> ColorGroups = new Dictionary<string, List<(string, string)>>
        {
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

        public CustomTheme Result { get; private set; }

        public ThemeColorEditorWindow(string baseTheme = "Light")
        {
            InitializeComponent();
            Initialize(baseTheme);
        }

        public ThemeColorEditorWindow(CustomTheme existingTheme)
        {
            InitializeComponent();
            _theme = existingTheme;
            ThemeNameTextBox.Text = _theme.Name;
            Initialize(_theme.BaseTheme);
        }

        private void Initialize(string baseTheme)
        {
            if (_theme == null)
            {
                // 创建新主题
                _theme = CustomThemeManager.CreateFromCurrent("新主题", baseTheme);
            }

            BuildColorList();

            // 颜色选择器事件
            ColorPicker.ColorChanged += ColorPicker_ColorChanged;

            // 选择第一个颜色
            if (_colorPreviews.Count > 0)
            {
                SelectColor(ColorGroups.First().Value.First().Key);
            }
        }

        private void BuildColorList()
        {
            ColorListPanel.Children.Clear();

            foreach (var group in ColorGroups)
            {
                // 分组标题
                var groupHeader = new TextBlock
                {
                    Text = group.Key,
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)FindResource("ForegroundPrimaryBrush"),
                    Margin = new Thickness(0, group.Key == ColorGroups.Keys.First() ? 0 : 16, 0, 8)
                };
                ColorListPanel.Children.Add(groupHeader);

                // 颜色项
                foreach (var (key, name) in group.Value)
                {
                    var colorItem = CreateColorItem(key, name);
                    ColorListPanel.Children.Add(colorItem);
                }
            }
        }

        private Border CreateColorItem(string colorKey, string colorName)
        {
            var container = new Border
            {
                Style = (Style)FindResource("ColorItemStyle"),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

            // 颜色名称
            var nameText = new TextBlock
            {
                Text = colorName,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("ForegroundPrimaryBrush")
            };
            Grid.SetColumn(nameText, 0);
            grid.Children.Add(nameText);

            // 颜色预览块
            var previewBorder = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)FindResource("BorderDefaultBrush"),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // 设置颜色
            if (_theme.Colors.ContainsKey(colorKey))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(_theme.Colors[colorKey]);
                    previewBorder.Background = new SolidColorBrush(color);
                }
                catch
                {
                    previewBorder.Background = Brushes.Gray;
                }
            }

            _colorPreviews[colorKey] = previewBorder;

            Grid.SetColumn(previewBorder, 1);
            grid.Children.Add(previewBorder);

            container.Child = grid;

            // 点击事件
            container.MouseLeftButtonUp += (s, e) => SelectColor(colorKey);

            return container;
        }

        private void SelectColor(string colorKey)
        {
            _selectedColorKey = colorKey;

            // 更新颜色选择器
            if (_theme.Colors.ContainsKey(colorKey))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(_theme.Colors[colorKey]);
                    ColorPicker.SelectedColor = color;
                }
                catch
                {
                    // 使用默认颜色
                }
            }

            // 高亮选中的项
            foreach (var kvp in _colorPreviews)
            {
                var border = FindParentBorder(kvp.Value);
                if (border != null)
                {
                    border.BorderThickness = kvp.Key == colorKey ? new Thickness(2) : new Thickness(1);
                    border.BorderBrush = kvp.Key == colorKey
                        ? (Brush)FindResource("AccentDefaultBrush")
                        : (Brush)FindResource("BorderDefaultBrush");
                }
            }
        }

        private Border FindParentBorder(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is Border))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as Border;
        }

        private void ColorPicker_ColorChanged(object sender, Color newColor)
        {
            if (string.IsNullOrEmpty(_selectedColorKey))
                return;

            // 更新主题颜色
            var hexColor = $"#{newColor.R:X2}{newColor.G:X2}{newColor.B:X2}";
            _theme.Colors[_selectedColorKey] = hexColor;

            // 更新预览块
            if (_colorPreviews.ContainsKey(_selectedColorKey))
            {
                _colorPreviews[_selectedColorKey].Background = new SolidColorBrush(newColor);
            }

            // 实时应用到预览（如果需要）
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            // 这里可以实时更新预览容器中的颜色
            // 暂时简单处理
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _theme.Name = ThemeNameTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(_theme.Name))
            {
                MessageBox.Show("请输入主题名称", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 更新预览颜色
                _theme.PreviewPrimaryColor = _theme.Colors.ContainsKey("AccentDefaultBrush")
                    ? _theme.Colors["AccentDefaultBrush"] : "#007BFF";
                _theme.PreviewBackgroundColor = _theme.Colors.ContainsKey("BackgroundPrimaryBrush")
                    ? _theme.Colors["BackgroundPrimaryBrush"] : "#FFFFFF";
                _theme.PreviewSurfaceColor = _theme.Colors.ContainsKey("BackgroundSecondaryBrush")
                    ? _theme.Colors["BackgroundSecondaryBrush"] : "#F8F9FA";
                _theme.PreviewTextColor = _theme.Colors.ContainsKey("ForegroundPrimaryBrush")
                    ? _theme.Colors["ForegroundPrimaryBrush"] : "#212529";

                CustomThemeManager.Save(_theme);
                Result = _theme;
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

