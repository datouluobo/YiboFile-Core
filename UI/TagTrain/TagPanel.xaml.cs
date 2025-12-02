using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TagTrain.Services;

namespace TagTrain.UI
{
    /// <summary>
    /// TagPanel.xaml 的交互逻辑
    /// 可复用的标签显示面板，支持浏览、编辑和完整模式
    /// </summary>
    public partial class TagPanel : UserControl
    {
        /// <summary>
        /// 显示模式枚举
        /// </summary>
        public enum DisplayMode
        {
            /// <summary>
            /// 浏览模式：只显示标签列表，无AI预测和训练按钮
            /// </summary>
            Browse,
            /// <summary>
            /// 编辑模式：完整功能（AI预测、标签输入、训练按钮）
            /// </summary>
            Edit,
            /// <summary>
            /// 完整模式：TagTrain独立窗口使用
            /// </summary>
            Full
        }

        private DisplayMode _displayMode = DisplayMode.Full;
        private Dictionary<int, string> _tagCache = new Dictionary<int, string>();
        private List<Services.TagPredictionResult> _currentPredictions = new List<Services.TagPredictionResult>();
        private int _tagsPerRow = 5;
        private double _predictionThreshold = 50.0;
        private string _tagSortMode = "Count";

        // 事件：标签点击（参数：标签名称，是否强制新标签页）
        public event Action<string, bool> TagClicked;
        // 事件：标签刷新完成
        public event Action TagsRefreshed;
        // 事件：需要打开分组管理
        public event Action CategoryManagementRequested;

        /// <summary>
        /// 显示模式属性
        /// </summary>
        public DisplayMode Mode
        {
            get => _displayMode;
            set
            {
                _displayMode = value;
                UpdateDisplayMode();
            }
        }

        /// <summary>
        /// 当前预测结果
        /// </summary>
        public List<Services.TagPredictionResult> CurrentPredictions
        {
            get => _currentPredictions;
            set
            {
                _currentPredictions = value ?? new List<Services.TagPredictionResult>();
                LoadExistingTags();
            }
        }

        /// <summary>
        /// 预测阈值
        /// </summary>
        public double PredictionThreshold
        {
            get => _predictionThreshold;
            set => _predictionThreshold = value;
        }

        // 暴露UI元素供外部访问（通过partial class自动生成的字段）

        public TagPanel()
        {
            InitializeComponent();
            UpdateDisplayMode();
        }

        /// <summary>
        /// 根据显示模式更新UI可见性
        /// </summary>
        private void UpdateDisplayMode()
        {
            if (AIPredictionGroupBox == null) return;

            switch (_displayMode)
            {
                case DisplayMode.Browse:
                    // 浏览模式：隐藏AI预测、标签输入、训练按钮
                    AIPredictionGroupBox.Visibility = Visibility.Collapsed;
                    TagInputGroupBox.Visibility = Visibility.Collapsed;
                    TrainingButtonsPanel.Visibility = Visibility.Collapsed;
                    TrainingProgressGrid.Visibility = Visibility.Collapsed;
                    break;
                case DisplayMode.Edit:
                    // 编辑模式：显示所有功能
                    AIPredictionGroupBox.Visibility = Visibility.Visible;
                    TagInputGroupBox.Visibility = Visibility.Visible;
                    TrainingButtonsPanel.Visibility = Visibility.Visible;
                    TrainingProgressGrid.Visibility = Visibility.Collapsed;
                    break;
                case DisplayMode.Full:
                    // 完整模式：显示所有功能
                    AIPredictionGroupBox.Visibility = Visibility.Visible;
                    TagInputGroupBox.Visibility = Visibility.Visible;
                    TrainingButtonsPanel.Visibility = Visibility.Visible;
                    TrainingProgressGrid.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>
        /// 加载标签列表
        /// </summary>
        public void LoadExistingTags()
        {
            if (ExistingTagsPanel == null) return;

            ExistingTagsPanel.Children.Clear();

            // 从数据库加载所有标签名称
            var tagNames = DataManager.GetAllTagNames();
            _tagCache = tagNames;

            // 从训练数据中提取已手动标注的标签
            var trainingData = DataManager.LoadAllTrainingData();

            // 先创建预测结果字典（TagId -> Confidence），排序时需要用到
            var predictionDict = new Dictionary<int, float>();
            if (_currentPredictions != null && _currentPredictions.Count > 0)
            {
                foreach (var pred in _currentPredictions)
                {
                    if (!predictionDict.ContainsKey(pred.TagId) || predictionDict[pred.TagId] < pred.Confidence)
                    {
                        predictionDict[pred.TagId] = pred.Confidence;
                    }
                }
            }

            // 先按TagName分组，整合同名标签
            var existingTagsQuery = trainingData
                .Where(t => t.IsManual)
                .GroupBy(t => DataManager.GetTagName(t.TagId) ?? $"标签{t.TagId}")
                .Select(g => new
                {
                    TagName = g.Key,
                    TagIds = g.Select(t => t.TagId).Distinct().ToList(),
                    Count = g.Count()
                });

            // 根据排序模式排序
            IEnumerable<dynamic> sortedTags;
            switch (_tagSortMode)
            {
                case "Name":
                    sortedTags = existingTagsQuery.OrderBy(x => x.TagName);
                    break;
                case "Prediction":
                    sortedTags = existingTagsQuery.OrderByDescending(x =>
                    {
                        float maxConfidence = 0f;
                        foreach (var tagId in x.TagIds)
                        {
                            if (predictionDict.ContainsKey(tagId) && predictionDict[tagId] > maxConfidence)
                            {
                                maxConfidence = predictionDict[tagId];
                            }
                        }
                        return maxConfidence;
                    });
                    break;
                case "Count":
                default:
                    sortedTags = existingTagsQuery.OrderByDescending(x => x.Count);
                    break;
            }

            var existingTags = sortedTags.ToList();

            // 计算每个标签的宽度
            double panelWidth = 0;
            var scrollViewer = ExistingTagsPanel.Parent as ScrollViewer;
            if (scrollViewer != null)
            {
                panelWidth = scrollViewer.ActualWidth;
                panelWidth -= 17; // 预留滚动条宽度
            }

            if (panelWidth <= 0)
            {
                var parent = ExistingTagsPanel.Parent as FrameworkElement;
                if (parent != null)
                {
                    panelWidth = parent.ActualWidth;
                }
            }

            if (panelWidth <= 0)
            {
                panelWidth = 450;
            }

            var spacing = 8.0;
            var padding = 16.0;
            double itemWidth;

            if (_tagsPerRow == 1)
            {
                itemWidth = panelWidth * 0.9;
            }
            else if (_tagsPerRow == 2)
            {
                itemWidth = (panelWidth - padding - spacing) / 2;
            }
            else
            {
                itemWidth = (panelWidth - padding - (_tagsPerRow - 1) * spacing) / _tagsPerRow;
            }

            if (_tagsPerRow > 2)
            {
                if (itemWidth < 80) itemWidth = 80;
                if (itemWidth > 200) itemWidth = 200;
            }
            else
            {
                if (itemWidth < 100) itemWidth = 100;
                var maxWidth = panelWidth * 0.95;
                if (itemWidth > maxWidth) itemWidth = maxWidth;
            }

            // 按分组组织标签
            try
            {
                var categories = DataManager.GetAllCategories().OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
                var tagsByCategory = new Dictionary<int, List<dynamic>>();
                var ungroupedTags = new List<dynamic>();

                foreach (var tagInfo in existingTags)
                {
                    var tagIds = (List<int>)tagInfo.TagIds;
                    var firstTagId = tagIds.Count > 0 ? tagIds[0] : 0;

                    if (firstTagId != 0)
                    {
                        var tagCategories = DataManager.GetTagCategories(firstTagId);
                        if (tagCategories.Count > 0)
                        {
                            foreach (var categoryId in tagCategories)
                            {
                                if (!tagsByCategory.ContainsKey(categoryId))
                                {
                                    tagsByCategory[categoryId] = new List<dynamic>();
                                }
                                if (!tagsByCategory[categoryId].Any(t => ((List<int>)t.TagIds)[0] == firstTagId))
                                {
                                    tagsByCategory[categoryId].Add(tagInfo);
                                }
                            }
                        }
                        else
                        {
                            ungroupedTags.Add(tagInfo);
                        }
                    }
                    else
                    {
                        ungroupedTags.Add(tagInfo);
                    }
                }

                // 为每个分组创建Expander
                foreach (var category in categories)
                {
                    if (tagsByCategory.ContainsKey(category.Id) && tagsByCategory[category.Id].Count > 0)
                    {
                        Brush categoryBrush = Brushes.DarkSlateGray;
                        if (!string.IsNullOrEmpty(category.Color))
                        {
                            try
                            {
                                var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(category.Color);
                                categoryBrush = new SolidColorBrush(color);
                            }
                            catch { }
                        }

                        var expander = new Expander
                        {
                            Header = $"📁 {category.Name} ({tagsByCategory[category.Id].Count})",
                            FontWeight = FontWeights.Bold,
                            FontSize = 13,
                            Margin = new Thickness(0, 4, 0, 4),
                            IsExpanded = true,
                            Foreground = categoryBrush
                        };

                        var categoryTagsPanel = new WrapPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(0, 4, 0, 4)
                        };

                        foreach (var tagInfo in tagsByCategory[category.Id])
                        {
                            var tagBorder = CreateTagBorder(tagInfo, itemWidth, predictionDict);
                            categoryTagsPanel.Children.Add(tagBorder);
                        }

                        expander.Content = categoryTagsPanel;
                        ExistingTagsPanel.Children.Add(expander);
                    }
                }

                // 显示未分组的标签
                if (ungroupedTags.Count > 0)
                {
                    var ungroupedExpander = new Expander
                    {
                        Header = $"📋 未分组 ({ungroupedTags.Count})",
                        FontWeight = FontWeights.Bold,
                        FontSize = 13,
                        Margin = new Thickness(0, 4, 0, 4),
                        IsExpanded = true,
                        Foreground = Brushes.Gray
                    };

                    var ungroupedTagsPanel = new WrapPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 4, 0, 4)
                    };

                    foreach (var tagInfo in ungroupedTags)
                    {
                        var tagBorder = CreateTagBorder(tagInfo, itemWidth, predictionDict);
                        ungroupedTagsPanel.Children.Add(tagBorder);
                    }

                    ungroupedExpander.Content = ungroupedTagsPanel;
                    ExistingTagsPanel.Children.Add(ungroupedExpander);
                }
            }
            catch (Exception)
            {
                // 回退到原来的显示方式
                var fallbackPanel = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var tagInfo in existingTags)
                {
                    var tagBorder = CreateTagBorder(tagInfo, itemWidth, predictionDict);
                    fallbackPanel.Children.Add(tagBorder);
                }
                ExistingTagsPanel.Children.Add(fallbackPanel);
            }

            TagsRefreshed?.Invoke();
        }

        /// <summary>
        /// 创建标签边框
        /// </summary>
        private Border CreateTagBorder(dynamic tagInfo, double itemWidth, Dictionary<int, float> predictionDict)
        {
            var tagName = tagInfo.TagName;
            var tagIds = (List<int>)tagInfo.TagIds;

            // 查找该标签名称对应的所有TagId中的最高预测置信度
            float maxConfidence = 0f;
            bool hasPrediction = false;
            foreach (var tagId in tagIds)
            {
                if (predictionDict.ContainsKey(tagId))
                {
                    hasPrediction = true;
                    if (predictionDict[tagId] > maxConfidence)
                    {
                        maxConfidence = predictionDict[tagId];
                    }
                }
            }

            var confidencePercent = maxConfidence * 100.0;
            var exceedsThreshold = hasPrediction && confidencePercent >= _predictionThreshold;

            // 创建标签Border（统一颜色：AliceBlue背景，LightBlue边框）
            var border = new Border
            {
                BorderBrush = Brushes.LightBlue,
                BorderThickness = new Thickness(1),
                Background = Brushes.AliceBlue,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 0, 8, 5),
                Cursor = Cursors.Hand,
                Tag = tagName,
                Width = itemWidth,
                Focusable = false,
                IsHitTestVisible = true
            };

            // 鼠标悬停效果
            border.MouseEnter += (s, e) =>
            {
                border.Background = Brushes.LightSkyBlue;
                border.BorderBrush = Brushes.DodgerBlue;
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = Brushes.AliceBlue;
                border.BorderBrush = Brushes.LightBlue;
            };

            // 点击事件：根据模式处理
            if (_displayMode == DisplayMode.Browse)
            {
                // 浏览模式：触发TagClicked事件
                border.MouseLeftButtonDown += (s, e) =>
                {
                    // 检测Ctrl键或鼠标中键，强制打开新标签页
                    bool forceNewTab = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                    TagClicked?.Invoke(tagName, forceNewTab);
                    e.Handled = true;
                };
                
                // 鼠标中键点击
                border.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Middle)
                    {
                        TagClicked?.Invoke(tagName, true);
                        e.Handled = true;
                    }
                };
            }
            else
            {
                // 编辑/完整模式：应用标签（需要外部处理）
                border.MouseLeftButtonDown += (s, e) =>
                {
                    TagClicked?.Invoke(tagName, false);
                    e.Handled = true;
                };
            }

            // 右键菜单：修改、分配到分组或删除
            border.ContextMenu = new ContextMenu();
            var editMenuItem = new MenuItem
            {
                Header = "✏️ 修改标签名称",
                Tag = tagName
            };
            editMenuItem.Click += (s, e) =>
            {
                EditTagName(tagName);
            };

            // 创建"分配到分组"子菜单
            var assignToCategoryMenuItem = new MenuItem
            {
                Header = "📁 分配到分组"
            };

            var firstTagId = tagIds.Count > 0 ? tagIds[0] : 0;
            if (firstTagId != 0)
            {
                try
                {
                    var categories = DataManager.GetAllCategories();
                    var currentCategories = DataManager.GetTagCategories(firstTagId);

                    if (categories.Count > 0)
                    {
                        foreach (var category in categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
                        {
                            var categoryMenuItem = new MenuItem
                            {
                                Header = category.Name,
                                Tag = new { TagId = firstTagId, CategoryId = category.Id, TagName = tagName },
                                IsCheckable = true,
                                IsChecked = currentCategories.Contains(category.Id)
                            };

                            categoryMenuItem.Click += (s, e) =>
                            {
                                var menuItem = s as MenuItem;
                                if (menuItem?.Tag != null)
                                {
                                    var tagType = menuItem.Tag.GetType();
                                    var tagIdProp = tagType.GetProperty("TagId");
                                    var categoryIdProp = tagType.GetProperty("CategoryId");

                                    if (tagIdProp != null && categoryIdProp != null)
                                    {
                                        var tagId = (int)tagIdProp.GetValue(menuItem.Tag);
                                        var categoryId = (int)categoryIdProp.GetValue(menuItem.Tag);

                                        try
                                        {
                                            if (menuItem.IsChecked)
                                            {
                                                DataManager.AssignTagToCategory(tagId, categoryId);
                                            }
                                            else
                                            {
                                                DataManager.RemoveTagFromCategory(tagId, categoryId);
                                            }
                                            LoadExistingTags();
                                        }
                                        catch (Exception ex)
                                        {
                                            MessageBox.Show($"分组操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                            menuItem.IsChecked = !menuItem.IsChecked;
                                        }
                                    }
                                }
                                e.Handled = true;
                            };

                            assignToCategoryMenuItem.Items.Add(categoryMenuItem);
                        }

                        assignToCategoryMenuItem.Items.Add(new Separator());
                        var manageCategoryMenuItem = new MenuItem
                        {
                            Header = "管理分组..."
                        };
                        manageCategoryMenuItem.Click += (s, e) =>
                        {
                            CategoryManagementRequested?.Invoke();
                            e.Handled = true;
                        };
                        assignToCategoryMenuItem.Items.Add(manageCategoryMenuItem);
                    }
                    else
                    {
                        var noCategoryMenuItem = new MenuItem
                        {
                            Header = "（暂无分组，点击创建）",
                            IsEnabled = true
                        };
                        noCategoryMenuItem.Click += (s, e) =>
                        {
                            CategoryManagementRequested?.Invoke();
                            e.Handled = true;
                        };
                        assignToCategoryMenuItem.Items.Add(noCategoryMenuItem);
                    }
                }
                catch (Exception ex)
                {
                    var errorMenuItem = new MenuItem
                    {
                        Header = $"加载失败: {ex.Message}",
                        IsEnabled = false
                    };
                    assignToCategoryMenuItem.Items.Add(errorMenuItem);
                }
            }
            else
            {
                var noTagIdMenuItem = new MenuItem
                {
                    Header = "（无法获取标签ID）",
                    IsEnabled = false
                };
                assignToCategoryMenuItem.Items.Add(noTagIdMenuItem);
            }

            var deleteMenuItem = new MenuItem
            {
                Header = "🗑️ 删除标签",
                Tag = tagName
            };
            deleteMenuItem.Click += (s, e) =>
            {
                DeleteTagByName(tagName);
            };

            border.ContextMenu.Items.Add(editMenuItem);
            border.ContextMenu.Items.Add(new Separator());
            border.ContextMenu.Items.Add(assignToCategoryMenuItem);
            border.ContextMenu.Items.Add(new Separator());
            border.ContextMenu.Items.Add(deleteMenuItem);

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                IsHitTestVisible = false
            };

            stackPanel.Children.Add(new TextBlock
            {
                Text = tagName,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            });

            if (hasPrediction)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"{(int)confidencePercent}%",
                    Foreground = exceedsThreshold
                        ? Brushes.DarkOrange
                        : Brushes.DarkGray,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                });
            }

            var count = tagInfo.Count;
            if (count > 0)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"({count})",
                    Foreground = Brushes.DarkGray,
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
                });
            }

            border.Child = stackPanel;
            return border;
        }

        /// <summary>
        /// 修改标签名称
        /// </summary>
        private void EditTagName(string oldTagName)
        {
            var inputDialog = new Window
            {
                Title = "修改标签名称",
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.Margin = new Thickness(0);

            var textBlock = new TextBlock
            {
                Text = $"请输入新的标签名称：",
                Margin = new Thickness(15, 20, 15, 10),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetRow(textBlock, 0);

            var textBox = new TextBox
            {
                Text = oldTagName,
                Margin = new Thickness(15, 0, 15, 15),
                FontSize = 14,
                VerticalContentAlignment = VerticalAlignment.Center,
                Height = 30
            };
            Grid.SetRow(textBox, 1);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 15, 15)
            };
            Grid.SetRow(buttonPanel, 2);

            var okButton = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };

            var cancelButton = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            string newTagName = null;
            bool dialogResult = false;

            okButton.Click += (s, e) =>
            {
                newTagName = textBox.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(newTagName))
                {
                    dialogResult = true;
                    inputDialog.DialogResult = true;
                    inputDialog.Close();
                }
                else
                {
                    MessageBox.Show("标签名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            cancelButton.Click += (s, e) =>
            {
                inputDialog.DialogResult = false;
                inputDialog.Close();
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            grid.Children.Add(textBlock);
            grid.Children.Add(textBox);
            grid.Children.Add(buttonPanel);

            inputDialog.Content = grid;

            textBox.Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            if (inputDialog.ShowDialog() == true && dialogResult && !string.IsNullOrWhiteSpace(newTagName))
            {
                if (newTagName == oldTagName)
                {
                    return;
                }

                try
                {
                    bool success = DataManager.UpdateTagName(oldTagName, newTagName);
                    if (success)
                    {
                        _tagCache = DataManager.GetAllTagNames();
                        LoadExistingTags();
                        MessageBox.Show($"标签名称已从 \"{oldTagName}\" 修改为 \"{newTagName}\"。\n所有训练数据已保留。",
                            "修改成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"修改失败：新标签名称 \"{newTagName}\" 已存在或旧标签不存在。",
                            "修改失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"修改标签名称时发生错误：{ex.Message}",
                        "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 删除标签
        /// </summary>
        private void DeleteTagByName(string tagName)
        {
            var tagIds = _tagCache.Where(kv => kv.Value == tagName).Select(kv => kv.Key).ToList();

            if (tagIds.Count > 0)
            {
                var result = MessageBox.Show(
                    $"确定要删除标签 \"{tagName}\" 吗？\n这将删除所有使用该标签的训练数据（共{tagIds.Count}个标签ID）。",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var tagId in tagIds)
                    {
                        DataManager.DeleteTag(tagId);
                    }

                    LoadExistingTags();
                }
            }
        }

        // 事件处理器（占位，由外部实现）
        private void TagSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TagSortComboBox?.SelectedItem is ComboBoxItem item && item.Tag != null)
            {
                _tagSortMode = item.Tag.ToString();
                LoadExistingTags();
            }
        }

        private void ExistingTagsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            LoadExistingTags();
        }

        private void TagsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e) { }
        private void TagsScrollViewer_MouseMove(object sender, MouseEventArgs e) { }
        private void TagsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) { }
        private void TagInputTextBox_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TagInputTextBox_KeyDown(object sender, KeyEventArgs e) { }
        private void TagInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e) { }
        private void TagInputTextBox_GotFocus(object sender, RoutedEventArgs e) { }
        private void TagInputTextBox_LostFocus(object sender, RoutedEventArgs e) { }
        private void TagAutocompleteListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) { }
        private void TagAutocompleteListBox_KeyDown(object sender, KeyEventArgs e) { }
        private void ConfirmTag_Click(object sender, RoutedEventArgs e) { }
        private void ConfirmAIPrediction_Click(object sender, RoutedEventArgs e) { }
        private void Skip_Click(object sender, RoutedEventArgs e) { }
        private void StartTraining_Click(object sender, RoutedEventArgs e) { }
        private void RetrainModel_Click(object sender, RoutedEventArgs e) { }
        private void CancelTraining_Click(object sender, RoutedEventArgs e) { }
        private void BatchOperation_Click(object sender, RoutedEventArgs e) { }
        private void TrainingStatus_Click(object sender, RoutedEventArgs e) { }
        private void ConsolidateTags_Click(object sender, RoutedEventArgs e) { }
        private void CategoryManagement_Click(object sender, RoutedEventArgs e)
        {
            CategoryManagementRequested?.Invoke();
        }
        private void Config_Click(object sender, RoutedEventArgs e) { }
    }
}

