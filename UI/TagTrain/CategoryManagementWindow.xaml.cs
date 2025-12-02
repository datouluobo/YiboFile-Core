using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TagTrain.Services;

namespace TagTrain.UI
{
    public partial class CategoryManagementWindow : Window
    {
        private List<DataManager.TagCategory> _categories = new List<DataManager.TagCategory>();
        private Dictionary<int, int> _categoryTagCounts = new Dictionary<int, int>();

        public CategoryManagementWindow()
        {
            InitializeComponent();
            LoadCategories();
        }

        private void LoadCategories()
        {
            try
            {
                _categories = DataManager.GetAllCategories();
                
                // Ensure proper sort order values (fix if all have same value)
                NormalizeSortOrders();
                
                // 计算每个分组的标签数量
                _categoryTagCounts.Clear();
                foreach (var category in _categories)
                {
                    var tags = DataManager.GetCategoryTags(category.Id);
                    _categoryTagCounts[category.Id] = tags.Count;
                }

                // 为每个分组添加TagCount属性用于显示
                var categoriesWithCount = _categories.Select(c => new CategoryDisplayItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Color = c.Color ?? "#808080",
                    TagCount = _categoryTagCounts.GetValueOrDefault(c.Id, 0)
                }).ToList();

                CategoriesListBox.ItemsSource = categoriesWithCount;
            }
            catch (Exception)
            {
                MessageBox.Show("加载分组列表失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NormalizeSortOrders()
        {
            if (_categories.Count <= 1) return;
            
            // Check if all have same sort order
            var distinctOrders = _categories.Select(c => c.SortOrder).Distinct().Count();
            if (distinctOrders < _categories.Count)
            {
                // Assign sequential sort orders
                for (int i = 0; i < _categories.Count; i++)
                {
                    if (_categories[i].SortOrder != i)
                    {
                        DataManager.UpdateCategory(_categories[i].Id, _categories[i].Name, _categories[i].Color, i);
                        _categories[i].SortOrder = i;
                    }
                }
            }
        }

        private void CategoriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = CategoriesListBox.SelectedItem != null;
            EditCategoryBtn.IsEnabled = hasSelection;
            DeleteCategoryBtn.IsEnabled = hasSelection;
            
            // Update move buttons based on position
            int selectedIndex = CategoriesListBox.SelectedIndex;
            int itemCount = CategoriesListBox.Items.Count;
            MoveUpBtn.IsEnabled = hasSelection && selectedIndex > 0;
            MoveDownBtn.IsEnabled = hasSelection && selectedIndex < itemCount - 1;
        }

        private class CategoryDisplayItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Color { get; set; }
            public int TagCount { get; set; }
        }

        private void NewCategory_Click(object sender, RoutedEventArgs e)
        {
            CreateNewCategory();
        }

        private void NewCategoryNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CreateNewCategory();
            }
        }

        private void CreateNewCategory()
        {
            var categoryName = NewCategoryNameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                MessageBox.Show("请输入分组名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                NewCategoryNameTextBox.Focus();
                return;
            }

            try
            {
                // 生成不重复的随机颜色
                var uniqueColor = GenerateUniqueColor();
                // 新分组排在最后
                int maxOrder = _categories.Count > 0 ? _categories.Max(c => c.SortOrder) + 1 : 0;
                var categoryId = DataManager.CreateCategory(categoryName, uniqueColor, maxOrder);
                
                // 清空输入框
                NewCategoryNameTextBox.Text = "";
                
                LoadCategories();
                
                // 选中新创建的分组
                var item = CategoriesListBox.Items.Cast<CategoryDisplayItem>()
                    .FirstOrDefault(x => x.Id == categoryId);
                if (item != null)
                {
                    CategoriesListBox.SelectedItem = item;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"创建分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesListBox.SelectedItem == null) return;

            var selected = CategoriesListBox.SelectedItem as CategoryDisplayItem;
            if (selected == null) return;
            int categoryId = selected.Id;
            
            var category = DataManager.GetCategory(categoryId);
            if (category == null) return;

            var dialog = new CategoryEditDialog(category) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // 编辑时保持原有颜色
                    DataManager.UpdateCategory(categoryId, dialog.CategoryName, category.Color);
                    LoadCategories();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"更新分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesListBox.SelectedItem == null) return;

            var selected = CategoriesListBox.SelectedItem as CategoryDisplayItem;
            if (selected == null) return;
            int categoryId = selected.Id;
            string categoryName = selected.Name;

            var result = MessageBox.Show(
                $"确定要删除分组 \"{categoryName}\" 吗？\n\n删除后，该分组下的标签将变为未分组状态。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    DataManager.DeleteCategory(categoryId);
                    LoadCategories();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"删除分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            MoveCategory(-1);
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            MoveCategory(1);
        }

        private void MoveCategory(int direction)
        {
            if (CategoriesListBox.SelectedItem == null) return;
            
            int selectedIndex = CategoriesListBox.SelectedIndex;
            int newIndex = selectedIndex + direction;
            
            if (newIndex < 0 || newIndex >= _categories.Count) return;

            try
            {
                // Get the two categories to swap
                var currentCategory = _categories[selectedIndex];
                var targetCategory = _categories[newIndex];

                // Swap their sort orders
                int tempOrder = currentCategory.SortOrder;
                DataManager.UpdateCategory(currentCategory.Id, currentCategory.Name, currentCategory.Color, targetCategory.SortOrder);
                DataManager.UpdateCategory(targetCategory.Id, targetCategory.Name, targetCategory.Color, tempOrder);

                // Reload and reselect
                LoadCategories();
                
                // Reselect the moved item at new position
                if (newIndex >= 0 && newIndex < CategoriesListBox.Items.Count)
                {
                    CategoriesListBox.SelectedIndex = newIndex;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"移动分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 生成不重复的随机颜色
        /// </summary>
        private string GenerateUniqueColor()
        {
            var existingCategories = DataManager.GetAllCategories();
            var existingColors = existingCategories
                .Where(c => !string.IsNullOrEmpty(c.Color))
                .Select(c => c.Color.ToUpper())
                .ToHashSet();

            var random = new Random();
            string newColor;
            int attempts = 0;
            const int maxAttempts = 1000;

            // 预定义一些好看的颜色
            var predefinedColors = new[]
            {
                "#FF6B6B", "#4ECDC4", "#45B7D1", "#FFA07A", "#98D8C8",
                "#F7DC6F", "#BB8FCE", "#85C1E2", "#F8B739", "#52BE80",
                "#E74C3C", "#3498DB", "#9B59B6", "#1ABC9C", "#F39C12",
                "#E67E22", "#34495E", "#16A085", "#27AE60", "#2980B9",
                "#8E44AD", "#C0392B", "#D35400", "#7F8C8D", "#2ECC71"
            };

            // 先尝试使用预定义颜色
            foreach (var color in predefinedColors)
            {
                if (!existingColors.Contains(color.ToUpper()))
                {
                    return color;
                }
            }

            // 如果预定义颜色都用完了，生成随机颜色
            do
            {
                var r = random.Next(100, 256);
                var g = random.Next(100, 256);
                var b = random.Next(100, 256);
                newColor = $"#{r:X2}{g:X2}{b:X2}";
                attempts++;
            }
            while (existingColors.Contains(newColor) && attempts < maxAttempts);

            return newColor;
        }
    }

    /// <summary>
    /// 分组编辑对话框
    /// </summary>
    public partial class CategoryEditDialog : Window
    {
        public string CategoryName { get; private set; }

        public CategoryEditDialog(DataManager.TagCategory existingCategory)
        {
            InitializeComponent();
            
            if (existingCategory != null)
            {
                CategoryName = existingCategory.Name;
                NameTextBox.Text = CategoryName;
                Title = "编辑分组";
            }
            else
            {
                Title = "新建分组";
            }
        }

        private void InitializeComponent()
        {
            Width = 450;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = System.Windows.Media.Brushes.Transparent;

            // 外层边框（圆角、阴影）
            var border = new Border
            {
                Background = System.Windows.Media.Brushes.White,
                CornerRadius = new CornerRadius(12),
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E0E0E0"))
            };
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Black,
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.2
            };

            var mainGrid = new Grid { Margin = new Thickness(25, 20, 25, 20) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 分组名称标签
            var nameLabel = new TextBlock 
            { 
                Text = "分组名称:", 
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#333333")),
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(nameLabel, 0);
            mainGrid.Children.Add(nameLabel);

            // 输入框
            NameTextBox = new TextBox 
            { 
                FontSize = 13,
                Padding = new Thickness(10, 8, 10, 8),
                Height = 36,
                Margin = new Thickness(0, 0, 0, 20),
                VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC"))
            };
            
            // 设置输入框样式（圆角）
            var textBoxStyle = new Style(typeof(TextBox));
            var template = new ControlTemplate(typeof(TextBox));
            var borderTemplate = new FrameworkElementFactory(typeof(Border));
            borderTemplate.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(TextBox.BackgroundProperty));
            borderTemplate.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(TextBox.BorderBrushProperty));
            borderTemplate.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(TextBox.BorderThicknessProperty));
            borderTemplate.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
            scrollViewer.SetValue(FrameworkElement.NameProperty, "PART_ContentHost");
            scrollViewer.SetValue(FrameworkElement.MarginProperty, new TemplateBindingExtension(TextBox.PaddingProperty));
            borderTemplate.AppendChild(scrollViewer);
            template.VisualTree = borderTemplate;
            textBoxStyle.Setters.Add(new Setter(TextBox.TemplateProperty, template));
            
            // 焦点时边框变蓝
            var focusTrigger = new Trigger { Property = UIElement.IsFocusedProperty, Value = true };
            focusTrigger.Setters.Add(new Setter(TextBox.BorderBrushProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3"))));
            textBoxStyle.Triggers.Add(focusTrigger);
            
            NameTextBox.Style = textBoxStyle;
            Grid.SetRow(NameTextBox, 1);
            mainGrid.Children.Add(NameTextBox);

            // 按钮
            var buttonPanel = new StackPanel 
            { 
                Orientation = Orientation.Horizontal, 
                HorizontalAlignment = HorizontalAlignment.Right
            };
            
            // 取消按钮
            var cancelButton = CreateStyledButton("取消", false);
            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
            cancelButton.Margin = new Thickness(0, 0, 10, 0);
            buttonPanel.Children.Add(cancelButton);
            
            // 确定按钮
            var okButton = CreateStyledButton("确定", true);
            okButton.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(NameTextBox.Text))
                {
                    MessageBox.Show("请输入分组名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                CategoryName = NameTextBox.Text.Trim();
                DialogResult = true;
                Close();
            };
            buttonPanel.Children.Add(okButton);

            Grid.SetRow(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            border.Child = mainGrid;
            Content = border;

            // 添加键盘事件处理
            KeyDown += CategoryEditDialog_KeyDown;
            
            // 设置默认按钮
            okButton.IsDefault = true;
            cancelButton.IsCancel = true;
        }

        private Button CreateStyledButton(string content, bool isPrimary)
        {
            var button = new Button 
            { 
                Content = content, 
                Width = 90, 
                Height = 36,
                FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(0)
            };

            var style = new Style(typeof(Button));
            
            if (isPrimary)
            {
                style.Setters.Add(new Setter(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2196F3"))));
                style.Setters.Add(new Setter(Button.ForegroundProperty, System.Windows.Media.Brushes.White));
            }
            else
            {
                style.Setters.Add(new Setter(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F5F5"))));
                style.Setters.Add(new Setter(Button.ForegroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#666666"))));
            }

            // 按钮模板（圆角）
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));

            // 鼠标悬停效果
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            if (isPrimary)
            {
                hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1976D2"))));
            }
            else
            {
                hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E0E0E0"))));
            }
            style.Triggers.Add(hoverTrigger);

            // 按下效果
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            if (isPrimary)
            {
                pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1565C0"))));
            }
            else
            {
                pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#D0D0D0"))));
            }
            style.Triggers.Add(pressedTrigger);

            button.Style = style;
            return button;
        }

        private void CategoryEditDialog_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (!string.IsNullOrWhiteSpace(NameTextBox.Text))
                {
                    CategoryName = NameTextBox.Text.Trim();
                    DialogResult = true;
                    Close();
                }
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private TextBox NameTextBox;
    }

    /// <summary>
    /// 颜色转换器
    /// </summary>
    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string colorStr)
            {
                try
                {
                    var color = System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                    if (color != null)
                    {
                        return (System.Windows.Media.Color)color;
                    }
                }
                catch { }
            }
            return System.Windows.Media.Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

