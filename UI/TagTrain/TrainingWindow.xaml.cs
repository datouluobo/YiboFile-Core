using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.Data.Sqlite;
using TagTrain.Services;
using Screen = System.Windows.Forms.Screen;

namespace TagTrain.UI
{
    /// <summary>
    /// TrainingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class TrainingWindow : Window
    {
        private ImageTagTrainer _trainer;
        private List<string> _imageList;
        private int _currentImageIndex = -1;
        private bool _isTraining = false;
        private string _imageDirectory = "";
        private Dictionary<int, string> _tagCache = new Dictionary<int, string>();
        private List<Services.TagPredictionResult> _currentPredictions = new List<Services.TagPredictionResult>();
        private string _currentImagePath = "";
        private int _tagsPerRow = 5;
        private double _predictionThreshold = 50.0;
        private string _tagSortMode = "Count"; // 排序模式：Count(使用次数), Name(字母顺序), Prediction(预测度)

        // 辅助属性：通过TagPanelControl访问UI元素
        private WrapPanel TagInputPanel => TagPanelControl?.TagInputPanel;
        private TextBox TagInputTextBox => TagPanelControl?.TagInputTextBox;
        private System.Windows.Controls.Primitives.Popup TagAutocompletePopup => TagPanelControl?.TagAutocompletePopup;
        private ListBox TagAutocompleteListBox => TagPanelControl?.TagAutocompleteListBox;
        private Border TagInputBorder => TagPanelControl?.TagInputBorder;
        private Button StartTrainingBtn => TagPanelControl?.StartTrainingBtn;
        private Button ConfirmAIPredictionBtn => TagPanelControl?.ConfirmAIPredictionBtn;
        private System.Windows.Shapes.Ellipse ModelStatusIndicator => TagPanelControl?.ModelStatusIndicator;
        private TextBlock ModelStatusText => TagPanelControl?.ModelStatusText;
        private TextBlock StatusText => TagPanelControl?.StatusText;
        private Grid TrainingProgressGrid => TagPanelControl?.TrainingProgressGrid;
        private ProgressBar TrainingProgressBar => TagPanelControl?.TrainingProgressBar;
        private TextBlock TrainingStageText => TagPanelControl?.TrainingStageText;
        private TextBlock TrainingProgressText => TagPanelControl?.TrainingProgressText;
        private Button RetrainModelBtn => TagPanelControl?.RetrainModelBtn;
        private Button ConsolidateTagsBtn => TagPanelControl?.ConsolidateTagsBtn;
        private Button BatchOperationBtn => TagPanelControl?.BatchOperationBtn;
        private Button TrainingStatusBtn => TagPanelControl?.TrainingStatusBtn;
        private Button CancelTrainingBtn => TagPanelControl?.CancelTrainingBtn;
        private Button ConfigBtn => TagPanelControl?.ConfigBtn;
        private ScrollViewer TagsScrollViewer => TagPanelControl?.TagsScrollViewer;
        private StackPanel ExistingTagsPanel => TagPanelControl?.ExistingTagsPanel;

        private bool _windowPositionLoaded = false;
        private double? _cachedTestAccuracy = null;
        private DateTime _lastTestTime = DateTime.MinValue;
        private bool _isTesting = false;
        private System.Threading.CancellationTokenSource _imageCountCancellation = null;
        private int? _cachedImageCount = null;
        private DateTime _lastImageCountTime = DateTime.MinValue;
        private List<string> _autocompleteSuggestions = new List<string>();
        private int _selectedAutocompleteIndex = -1;
        private bool _isTabForCompletion = false; // Tab键状态：false=补完，true=确认
        private CancellationTokenSource _trainingCancellation = null;
        // 训练中阶段的进度平滑（将50%缓慢推进到79%，改善长时间停留的观感）
        private System.Windows.Threading.DispatcherTimer _progressSmoother = null;
        private int _lastProgress = 0;
        private System.Windows.Threading.DispatcherTimer _scrollBarHideTimer = null;
        private System.Windows.Threading.DispatcherTimer _splitterHideTimer = null;
        private bool _isWheelScrolling = false;

        public TrainingWindow()
        {
            InitializeComponent();
            _trainer = new ImageTagTrainer();
            
            // 尝试加载模型，如果失败会在训练时重新创建
            try
            {
                _trainer.LoadModel();
            }
            catch (Exception)
            {
                // 模型加载失败不影响程序运行，可以在训练时重新创建
            }
            
            // 加载设置（不包括窗口位置，窗口位置在SourceInitialized中加载）
            LoadSettings();
            UpdateStatistics();
            UpdateStatus("就绪 - 请点击'开始训练'");
            
            // 延迟更新模型状态指示器，确保UI元素已初始化
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateModelStatus();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
            
            // 初始化TagPanel
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (TagPanelControl != null)
                    {
                        // 连接TagPanel事件
                        TagPanelControl.TagClicked += TagPanelControl_TagClicked;
                        TagPanelControl.CategoryManagementRequested += TagPanelControl_CategoryManagementRequested;
                        TagPanelControl.TagsRefreshed += TagPanelControl_TagsRefreshed;
                        
                        // 初始化补完列表的 PlacementTarget
                        if (TagPanelControl.TagAutocompletePopup != null && TagPanelControl.TagInputBorder != null)
                        {
                            TagPanelControl.TagAutocompletePopup.PlacementTarget = TagPanelControl.TagInputBorder;
                        }
                        
                        // 设置预测结果
                        TagPanelControl.CurrentPredictions = _currentPredictions;
                        
                        // 加载标签
                        LoadTags();
                        TagPanelControl.LoadExistingTags();
                        
                        // 设置排序下拉框
                        if (TagPanelControl.TagSortComboBox != null)
                        {
                            foreach (ComboBoxItem item in TagPanelControl.TagSortComboBox.Items)
                            {
                                if (item.Tag?.ToString() == _tagSortMode)
                                {
                                    TagPanelControl.TagSortComboBox.SelectedItem = item;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // 忽略UI初始化时的异常，避免影响程序运行
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            // 在窗口完全初始化后加载窗口位置
            if (!_windowPositionLoaded)
            {
                LoadWindowPosition();
                _windowPositionLoaded = true;
            }
        }

        private void LoadSettings()
        {
            // 从统一配置管理器加载设置
            _tagsPerRow = SettingsManager.GetTagsPerRow();
            _predictionThreshold = SettingsManager.GetPredictionThreshold();
            _tagSortMode = SettingsManager.GetTagSortMode();
            
            // 加载图片目录
            _imageDirectory = SettingsManager.GetImageDirectory();
        }

        private void LoadWindowPosition()
        {
            SettingsManager.LoadWindowPosition("TrainingWindow", this);
        }

        private void SaveSettings()
        {
            // 只在窗口已加载时保存，避免在初始化时保存无效值
            if (!this.IsLoaded)
                return;
                
            // 使用统一配置管理器保存设置
            SettingsManager.SetTagsPerRow(_tagsPerRow);
            SettingsManager.SetPredictionThreshold(_predictionThreshold);
            SettingsManager.SetTagSortMode(_tagSortMode);
            SettingsManager.SaveWindowPosition("TrainingWindow", this);
        }

        private void TagSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TagPanelControl?.TagSortComboBox?.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is string sortMode)
            {
                _tagSortMode = sortMode;
                SaveSettings();
                LoadExistingTags();
            }
        }

        // TagPanel事件处理
        private void TagPanelControl_TagClicked(string tagName, bool forceNewTab)
        {
            ApplyPredictionTag(tagName);
        }

        private void TagPanelControl_CategoryManagementRequested()
        {
            OpenCategoryManagement();
        }

        private void TagPanelControl_TagsRefreshed()
        {
            UpdateStatistics();
        }

        private void LoadTags()
        {
            // 加载已有标签到列表
            LoadExistingTags();
        }

        private void LoadExistingTags()
        {
            // 委托给TagPanel
            if (TagPanelControl != null)
            {
                TagPanelControl.CurrentPredictions = _currentPredictions;
                TagPanelControl.LoadExistingTags();
            }
        }

        // 保留原方法作为兼容（已废弃，实际调用TagPanel）
        private void LoadExistingTags_Old()
        {
            // 这个方法已废弃，不再使用 - 所有逻辑已移到TagPanel中
            return;
        }

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

            // 创建标签Border
            var border = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.LightBlue,
                BorderThickness = new Thickness(1),
                Background = exceedsThreshold
                    ? System.Windows.Media.Brushes.Orange 
                    : System.Windows.Media.Brushes.AliceBlue,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 0, 8, 5),
                Cursor = Cursors.Hand,
                Tag = tagName,
                Width = itemWidth,
                Focusable = false,
                IsHitTestVisible = true
            };

            // 添加鼠标悬停效果
            border.MouseEnter += (s, e) =>
            {
                border.Background = System.Windows.Media.Brushes.LightSkyBlue;
                border.BorderBrush = System.Windows.Media.Brushes.DodgerBlue;
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = exceedsThreshold
                    ? System.Windows.Media.Brushes.Orange 
                    : System.Windows.Media.Brushes.AliceBlue;
                border.BorderBrush = System.Windows.Media.Brushes.LightBlue;
            };

            // 点击事件
            border.PreviewMouseLeftButtonDown += (s, e) =>
            {
                                ApplyPredictionTag(tagName);
                e.Handled = true;
            };
            
            border.MouseLeftButtonDown += (s, e) =>
            {
                                ApplyPredictionTag(tagName);
                e.Handled = true;
            };
            
            border.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                                        ApplyPredictionTag(tagName);
                    e.Handled = true;
                }
            };

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
            
            // 获取所有分组和当前标签的分组
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
                                                // 刷新标签列表以反映分组变化
                                                LoadExistingTags();
                                            }
                                            else
                                            {
                                                                                                DataManager.RemoveTagFromCategory(tagId, categoryId);
                                                // 刷新标签列表以反映分组变化
                                                LoadExistingTags();
                                            }
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
                            OpenCategoryManagement();
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
                            OpenCategoryManagement();
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
            
            border.Focusable = false;

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
                        ? System.Windows.Media.Brushes.DarkOrange 
                        : System.Windows.Media.Brushes.DarkGray,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    IsHitTestVisible = false
                });
            }

            border.Child = stackPanel;
            return border;
        }

        private void EditTagName(string oldTagName)
        {
            // 创建输入对话框
            var inputDialog = new Window
            {
                Title = "修改标签名称",
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
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

            // 设置焦点到文本框并选中所有文本
            textBox.Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            if (inputDialog.ShowDialog() == true && dialogResult && !string.IsNullOrWhiteSpace(newTagName))
            {
                if (newTagName == oldTagName)
                {
                    return; // 名称未改变
                }

                try
                {
                    // 更新标签名称（只修改名称，保留所有数据）
                    bool success = DataManager.UpdateTagName(oldTagName, newTagName);
                    
                    if (success)
                    {
                        // 刷新标签缓存
                        _tagCache = DataManager.GetAllTagNames();
                        
                        // 刷新显示
                        LoadExistingTags();
                        UpdateStatistics();
                        
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

        private void DeleteTagByName(string tagName)
        {
            // 查找所有对应的TagId（因为同名标签可能对应多个TagId）
            var tagIds = _tagCache.Where(kv => kv.Value == tagName).Select(kv => kv.Key).ToList();
            
            if (tagIds.Count > 0)
            {
                var result = MessageBox.Show(
                    $"确定要删除标签 \"{tagName}\" 吗？\n这将删除所有使用该标签的训练数据（共{tagIds.Count}个标签ID）。",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 删除所有相关的TagId
                    foreach (var tagId in tagIds)
                    {
                        DataManager.DeleteTag(tagId);
                    }
                    
                    LoadExistingTags();
                    UpdateStatistics();
                }
            }
        }

        private void StartTraining_Click(object sender, RoutedEventArgs e)
        {
            // 开关：如果正在训练，则停止；否则开始
            if (_isTraining)
            {
                _isTraining = false;
                TagPanelControl?.StartTrainingBtn?.SetValue(Button.ContentProperty, "▶️ 开始训练");
                UpdateStatus("已停止");
                // 停止时立即复位进度显示，允许立即再次开始
                if (TagPanelControl?.TrainingProgressGrid != null) TagPanelControl.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                if (TagPanelControl?.TrainingProgressBar != null) TagPanelControl.TrainingProgressBar.Value = 0;
                if (TagPanelControl?.TrainingStageText != null) TagPanelControl.TrainingStageText.Text = "已停止";
                if (TagPanelControl?.TrainingProgressText != null) TagPanelControl.TrainingProgressText.Text = "";
                _progressSmoother?.Stop();
                return;
            }

            if (string.IsNullOrEmpty(_imageDirectory))
            {
                var configWindow = new ConfigWindow();
                if (configWindow.ShowDialog() == true)
                {
                    var oldDirectory = _imageDirectory;
                    _imageDirectory = configWindow.ImageDirectory;
                    
                    // 如果图片目录已更改，清除图片数量缓存
                    if (!string.IsNullOrEmpty(_imageDirectory) && 
                        !_imageDirectory.Equals(oldDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        _cachedImageCount = null;
                        _lastImageCountTime = DateTime.MinValue;
                    }
                    
                    // 更新设置（从设置窗口读取）
                    _tagsPerRow = configWindow.TagsPerRow;
                    _predictionThreshold = configWindow.PredictionThreshold;
                    
                    // 刷新标签显示
                    LoadExistingTags();
                    
                    // 延迟更新标签宽度，确保面板已渲染
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateTagWidths();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
                else
                {
                    return;
                }
            }

            if (string.IsNullOrEmpty(_imageDirectory) || !Directory.Exists(_imageDirectory))
            {
                MessageBox.Show("请先配置有效的图片目录", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                Config_Click(null, null);
                return;
            }

            // 获取未标注的图片列表
            _imageList = DataManager.GetUnlabeledImages(_imageDirectory);
            
            if (_imageList.Count == 0)
            {
                MessageBox.Show("该目录下没有未标注的图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isTraining = true;
            if (StartTrainingBtn != null) StartTrainingBtn.Content = "⏹️ 停止";
            
            LoadNextImage();
            UpdateStatistics();
            UpdateStatus("训练中...");
                // 展开进度面板
                if (TagPanelControl?.TrainingProgressGrid != null) TagPanelControl.TrainingProgressGrid.Visibility = Visibility.Visible;
        }

        // 原暂停功能已移除（即时自动保存，无需暂停）

        private void LoadNextImage()
        {
            if (_imageList == null || _imageList.Count == 0)
            {
                MessageBox.Show("没有更多图片了", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                _isTraining = false;
                if (StartTrainingBtn != null) StartTrainingBtn.Content = "▶️ 开始训练";
                return;
            }

            // 随机选择一张图片
            var random = new Random();
            _currentImageIndex = random.Next(_imageList.Count);
            var imagePath = _imageList[_currentImageIndex];

            try
            {
                // 加载图片 - 使用文件流方式确保路径中的特殊字符能正确处理
                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException($"图片文件不存在: {imagePath}");
                }
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                // 使用文件流加载，避免URI路径解析问题（特别是包含特殊字符的路径）
                bitmap.StreamSource = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                // 不设置DecodePixelWidth/Height，让图片保持原始尺寸，由Stretch属性控制显示
                bitmap.EndInit();
                bitmap.Freeze();

                ImageDisplay.Source = bitmap;
                // 图片使用Stretch="Uniform"会自动适应窗口大小
                NoImageText.Visibility = Visibility.Collapsed;
                
                // 显示图片索引和完整路径
                var imageIndex = _currentImageIndex + 1;
                var totalCount = _imageList.Count;
                CurrentImageText.Text = $"第 {imageIndex}/{totalCount} 张: {imagePath}";

                // 显示AI预测结果
                ShowPredictions(imagePath);

                // 加载当前图片的已有标签
                LoadCurrentImageTags(imagePath);

                // 清空标签输入
                ClearTagInput();
                TagPanelControl?.TagInputTextBox?.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                // 跳过这张图片，加载下一张
                _imageList.RemoveAt(_currentImageIndex);
                if (_isTraining)
                {
                    LoadNextImage();
                }
            }
        }

        private void ShowPredictions(string imagePath)
        {
            if (TagPanelControl == null) return;
            
            TagPanelControl.PredictionPanel.Children.Clear();
            TagPanelControl.NoPredictionText.Visibility = Visibility.Visible;
            _currentImagePath = imagePath;
            _currentPredictions.Clear();

            // 更新模型状态
            UpdateModelStatus();

            if (!_trainer.ModelExists())
            {
                TagPanelControl.NoPredictionText.Text = "模型未训练，无法预测";
                TagPanelControl.ConfirmAIPredictionBtn.IsEnabled = false;
                return;
            }

            var predictions = _trainer.PredictTags(imagePath);
            _currentPredictions = predictions;
            TagPanelControl.CurrentPredictions = predictions;
            
            if (predictions.Count == 0)
            {
                TagPanelControl.NoPredictionText.Text = "暂无预测结果";
                TagPanelControl.ConfirmAIPredictionBtn.IsEnabled = false;
                return;
            }

            TagPanelControl.NoPredictionText.Visibility = Visibility.Collapsed;
            if (TagPanelControl?.ConfirmAIPredictionBtn != null) TagPanelControl.ConfirmAIPredictionBtn.IsEnabled = true;

            // 刷新已有标签列表以显示预测结果
            LoadExistingTags();

            // 按标签名称整合预测结果（只保留每个标签名称的最高置信度）
            var consolidatedPredictions = predictions
                .GroupBy(p => 
                {
                    var tagName = _tagCache.ContainsKey(p.TagId) 
                        ? _tagCache[p.TagId] 
                        : DataManager.GetTagName(p.TagId) ?? $"标签{p.TagId}";
                    return tagName;
                })
                .Select(g => new
                {
                    TagName = g.Key,
                    MaxConfidence = g.Max(p => p.Confidence),
                    TagId = g.OrderByDescending(p => p.Confidence).First().TagId
                })
                .OrderByDescending(p => p.MaxConfidence)
                .ToList();

            foreach (var pred in consolidatedPredictions)
            {
                var tagText = pred.TagName;
                
                var border = new Border
                {
                    BorderBrush = System.Windows.Media.Brushes.LightBlue,
                    BorderThickness = new Thickness(1),
                    Background = System.Windows.Media.Brushes.AliceBlue,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 3, 6, 3),
                    Margin = new Thickness(0, 0, 5, 5),
                    Cursor = Cursors.Hand,
                    Tag = tagText, // 存储标签名称以便点击时使用
                    Focusable = false,  // 确保可以接收鼠标事件
                    IsHitTestVisible = true  // 明确设置为true，确保可以接收鼠标事件
                };

                // 添加鼠标悬停效果
                border.MouseEnter += (s, e) =>
                {
                    border.Background = System.Windows.Media.Brushes.LightSkyBlue;
                    border.BorderBrush = System.Windows.Media.Brushes.DodgerBlue;
                };
                border.MouseLeave += (s, e) =>
                {
                    border.Background = System.Windows.Media.Brushes.AliceBlue;
                    border.BorderBrush = System.Windows.Media.Brushes.LightBlue;
                };

                // 同时使用PreviewMouseLeftButtonDown和MouseLeftButtonDown确保点击事件能够触发
                border.PreviewMouseLeftButtonDown += (s, e) =>
                {
                                        ApplyPredictionTag(tagText);
                    e.Handled = true;
                };
                
                border.MouseLeftButtonDown += (s, e) =>
                {
                                        ApplyPredictionTag(tagText);
                    e.Handled = true;
                };
                
                // 添加MouseDown事件作为备用
                border.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                                                ApplyPredictionTag(tagText);
                        e.Handled = true;
                    }
                };
                
                // 右键菜单：修改或删除（预测结果中的标签也可以修改）
                border.ContextMenu = new ContextMenu();
                var editMenuItem = new MenuItem
                {
                    Header = "✏️ 修改标签名称",
                    Tag = tagText
                };
                editMenuItem.Click += (s, e) =>
                {
                    EditTagName(tagText);
                };
                
                var deleteMenuItem = new MenuItem
                {
                    Header = "🗑️ 删除标签",
                    Tag = tagText
                };
                deleteMenuItem.Click += (s, e) =>
                {
                    DeleteTagByName(tagText);
                };
                
                border.ContextMenu.Items.Add(editMenuItem);
                border.ContextMenu.Items.Add(deleteMenuItem);
                
                // 确保Border可以接收鼠标事件
                border.Focusable = false;

                var stackPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal,
                    IsHitTestVisible = false  // 允许鼠标事件穿透到Border
                };
                
                stackPanel.Children.Add(new TextBlock
                {
                    Text = tagText,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false  // 允许鼠标事件穿透到Border
                });

                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"{pred.MaxConfidence:P0}",
                    Foreground = System.Windows.Media.Brushes.Gray,
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false  // 允许鼠标事件穿透到Border
                });

                border.Child = stackPanel;
                TagPanelControl?.PredictionPanel?.Children.Add(border);
            }
        }

        private void ExistingTagsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            // 面板加载后，如果已有标签，重新计算宽度
            if (TagPanelControl?.ExistingTagsPanel?.Children.Count > 0)
            {
                UpdateTagWidths();
            }
        }

        private void UpdateTagWidths()
        {
            // 标签宽度现在由TagPanel内部管理，此方法已废弃
            return;
        }

        private void ApplyPredictionTag(string tagName)
        {
            // 只有在训练状态下才允许添加标签到输入框
            if (!_isTraining)
            {
                                return;
            }
            
                        // 检查是否已存在
            bool exists = false;
            foreach (var child in TagInputPanel.Children)
            {
                if (child is Border border && border.Tag is string existingTag && existingTag == tagName)
                {
                    exists = true;
                                        break;
                }
            }
            
            if (!exists)
            {
                                var tagBorder = CreateTagBorder(tagName);
                                // 将标签Border添加到输入面板（在输入框之前）
                TagInputPanel.Children.Insert(TagInputPanel.Children.Count - 1, tagBorder);
                
                                TagPanelControl?.TagInputTextBox?.Focus();
            }
        }

        private void ConfirmTag_Click(object sender, RoutedEventArgs e)
        {
            // 如果输入框中有未确认的文字，放弃该内容
            if (!string.IsNullOrWhiteSpace(TagInputTextBox.Text))
            {
                TagInputTextBox.Text = "";
                TagAutocompletePopup.IsOpen = false;
            }
            
            // 获取所有已输入的标签
            var tagNames = GetInputTags();
            
            if (tagNames.Count == 0)
            {
                MessageBox.Show("请输入标签", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentImageIndex < 0 || _currentImageIndex >= _imageList.Count)
            {
                return;
            }

            var imagePath = _imageList[_currentImageIndex];

            // 保存每个标签
            foreach (var tagName in tagNames)
            {
                // 获取或创建TagId（确保同名标签使用相同的TagId）
                var tagId = DataManager.GetOrCreateTagId(tagName);

                // 保存训练样本和标签名称
                DataManager.SaveTrainingSample(imagePath, tagId, isManual: true, tagName: tagName);
                _tagCache[tagId] = tagName;
            }

            // 更新已有标签列表
            LoadExistingTags();

            // 更新进度和统计
            UpdateProgress();
            UpdateStatistics();

            // 从列表中移除已标注的图片
            _imageList.RemoveAt(_currentImageIndex);

            // 加载下一张图片
            if (_isTraining && _imageList.Count > 0)
            {
                LoadNextImage();
            }
            else if (_imageList.Count == 0)
            {
                MessageBox.Show("所有图片已标注完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                _isTraining = false;
                StartTrainingBtn.Content = "▶️ 开始训练";
            }
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            if (_currentImageIndex >= 0 && _currentImageIndex < _imageList.Count)
            {
                _imageList.RemoveAt(_currentImageIndex);
            }

            if (_isTraining && _imageList.Count > 0)
            {
                LoadNextImage();
            }
        }

        private void ConfirmAIPrediction_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPredictions == null || _currentPredictions.Count == 0)
            {
                MessageBox.Show("没有AI预测结果可确认", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_currentImagePath) || !System.IO.File.Exists(_currentImagePath))
            {
                MessageBox.Show("当前图片路径无效", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 获取所有预测的标签名称
            var tagNames = new List<string>();
            foreach (var pred in _currentPredictions)
            {
                var tagText = _tagCache.ContainsKey(pred.TagId) 
                    ? _tagCache[pred.TagId] 
                    : DataManager.GetTagName(pred.TagId) ?? $"标签{pred.TagId}";
                
                if (!tagNames.Contains(tagText))
                {
                    tagNames.Add(tagText);
                }
            }

            if (tagNames.Count == 0)
            {
                MessageBox.Show("无法获取预测标签名称", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 保存每个预测标签作为训练数据
            foreach (var tagName in tagNames)
            {
                // 获取或创建TagId（确保同名标签使用相同的TagId）
                var tagId = DataManager.GetOrCreateTagId(tagName);

                // 保存训练样本（标记为手动标注，因为用户确认了预测结果）
                DataManager.SaveTrainingSample(_currentImagePath, tagId, isManual: true, tagName: tagName);
                _tagCache[tagId] = tagName;
            }

            // 更新已有标签列表
            LoadExistingTags();

            // 更新进度和统计
            UpdateProgress();
            UpdateStatistics();

            // 从列表中移除已标注的图片
            if (_currentImageIndex >= 0 && _currentImageIndex < _imageList.Count)
            {
                _imageList.RemoveAt(_currentImageIndex);
                if (_currentImageIndex >= _imageList.Count)
                {
                    _currentImageIndex = _imageList.Count - 1;
                }
            }

            // 清空预测结果
            _currentPredictions.Clear();
            if (TagPanelControl != null)
            {
                TagPanelControl.PredictionPanel.Children.Clear();
                TagPanelControl.NoPredictionText.Visibility = Visibility.Visible;
                TagPanelControl.NoPredictionText.Text = "预测结果已确认并保存";
                TagPanelControl.ConfirmAIPredictionBtn.IsEnabled = false;
                TagPanelControl.CurrentPredictions = new List<Services.TagPredictionResult>();
            }

            // 加载下一张图片
            if (_isTraining && _imageList.Count > 0)
            {
                LoadNextImage();
            }
            else if (_imageList.Count == 0)
            {
                _isTraining = false;
                StartTrainingBtn.Content = "▶️ 开始训练";
                if (TagPanelControl?.StatusText != null) TagPanelControl.StatusText.Text = "所有图片已标注完成";
                MessageBox.Show("所有图片已标注完成！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void TagInputTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
                        if (TagInputTextBox == null)
            {
                                return;
            }
            
            var text = TagInputTextBox.Text;
                        // 根据文本内容动态调整输入框宽度
            var border = TagInputTextBox.Parent as Border;
            if (border != null)
            {
                if (string.IsNullOrEmpty(text))
                {
                    border.Width = double.NaN; // 使用MinWidth
                    // 隐藏补完列表
                    TagAutocompletePopup.IsOpen = false;
                    _isTabForCompletion = false;
                }
                else
                {
                    // 使用FormattedText更准确地测量文本宽度
                    var formattedText = new System.Windows.Media.FormattedText(
                        text,
                        System.Globalization.CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        new System.Windows.Media.Typeface(
                            TagInputTextBox.FontFamily,
                            TagInputTextBox.FontStyle,
                            TagInputTextBox.FontWeight,
                            TagInputTextBox.FontStretch),
                        TagInputTextBox.FontSize,
                        System.Windows.Media.Brushes.Black,
                        VisualTreeHelper.GetDpi(TagInputTextBox).PixelsPerDip);

                    // 加上内边距（左右各7px）、边框（左右各1px）和额外空间（确保光标可见）
                    var width = formattedText.Width + 20; // 文本宽度 + 内边距 + 边框 + 额外空间
                    width = Math.Max(150, Math.Min(400, width)); // 限制在150-400之间，增加最大宽度
                    border.Width = width;
                }
            }
            
            // 无论 border 是否为 null，都更新自动补完建议
            if (!string.IsNullOrEmpty(text))
            {
                UpdateAutocompleteSuggestions(text);
            }
            else
            {
                TagAutocompletePopup.IsOpen = false;
                _isTabForCompletion = false;
            }
        }

        /// <summary>
        /// 更新自动补完建议
        /// </summary>
        private void UpdateAutocompleteSuggestions(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                TagAutocompletePopup.IsOpen = false;
                _autocompleteSuggestions.Clear();
                _selectedAutocompleteIndex = -1;
                _isTabForCompletion = false;
                return;
            }

            // 获取所有已有标签名称
            var allTagNames = _tagCache.Values.Distinct().ToList();
            
            // 调试信息
            System.Diagnostics.Debug.WriteLine($"[Autocomplete] 输入: '{input}', 总标签数: {allTagNames.Count}, 标签列表: {string.Join(", ", allTagNames.Take(10))}");
            
            // 获取已输入的标签（避免重复建议）
            var existingTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in TagInputPanel.Children)
            {
                if (child is Border border && border.Tag is string tag)
                {
                    existingTags.Add(tag);
                }
            }

            // 匹配标签（不区分大小写，包含匹配）
            var inputLower = input.ToLower();
            var matches = allTagNames
                .Where(tag => !existingTags.Contains(tag) && 
                             tag.ToLower().Contains(inputLower) && 
                             !tag.Equals(input, StringComparison.OrdinalIgnoreCase))
                .OrderBy(tag => tag.ToLower().StartsWith(inputLower) ? 0 : 1) // 以输入开头的优先
                .ThenBy(tag => tag.Length) // 然后按长度排序
                .Take(10) // 最多显示10个建议
                .ToList();
            
                        _autocompleteSuggestions = matches;
            _selectedAutocompleteIndex = -1;

            if (matches.Count > 0)
            {
                // 确保 PlacementTarget 已设置
                if (TagAutocompletePopup.PlacementTarget == null)
                {
                    TagAutocompletePopup.PlacementTarget = TagInputBorder;
                }
                
                // 更新补完列表
                TagAutocompleteListBox.ItemsSource = matches;
                TagAutocompleteListBox.SelectedIndex = -1;
                
                // 设置补完列表宽度与输入框一致
                var border = TagInputTextBox.Parent as Border;
                if (border != null)
                {
                    // 如果宽度还未计算，使用最小宽度
                    var width = border.ActualWidth > 0 ? border.ActualWidth : border.MinWidth;
                    TagAutocompletePopup.MinWidth = Math.Max(150, width);
                }
                
                // 强制更新布局以确保宽度正确
                TagInputBorder.UpdateLayout();
                
                // 确保 PlacementTarget 已设置
                if (TagAutocompletePopup.PlacementTarget == null)
                {
                    TagAutocompletePopup.PlacementTarget = TagInputBorder;
                }
                
                // 显示补完列表 - 使用延迟确保 UI 已更新
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    TagAutocompletePopup.IsOpen = true;
                    _isTabForCompletion = true; // 有建议时，Tab键用于补完
                    
                    // 强制刷新 Popup
                    TagAutocompletePopup.UpdateLayout();
                    
                    System.Diagnostics.Debug.WriteLine($"[Autocomplete] 显示 {matches.Count} 个补完建议: {string.Join(", ", matches.Take(5))}, Popup.IsOpen={TagAutocompletePopup.IsOpen}, PlacementTarget={TagAutocompletePopup.PlacementTarget != null}");
                }), System.Windows.Threading.DispatcherPriority.Render);
            }
            else
            {
                // 没有匹配项，隐藏补完列表
                TagAutocompletePopup.IsOpen = false;
                _isTabForCompletion = false;
            }
        }

        /// <summary>
        /// PreviewKeyDown 事件处理 - 用于处理方向键导航
        /// </summary>
        private void TagInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 只处理方向键，其他键交给 KeyDown 处理
            if (e.Key == Key.Down || e.Key == Key.Up)
            {
                // 如果补完列表打开，处理方向键导航
                if (TagAutocompletePopup.IsOpen && TagAutocompleteListBox.ItemsSource != null)
                {
                    // 确保 ItemsSource 和 _autocompleteSuggestions 同步
                    if (TagAutocompleteListBox.ItemsSource is System.Collections.IEnumerable items)
                    {
                        var currentItems = items.Cast<string>().ToList();
                        if (currentItems.Count > 0)
                        {
                            _autocompleteSuggestions = currentItems;
                            
                            // 获取当前选中索引
                            var currentIndex = TagAutocompleteListBox.SelectedIndex;
                            if (currentIndex < 0) currentIndex = -1;
                            
                            int newIndex;
                            if (e.Key == Key.Down)
                            {
                                // 下键：选择下一个
                                newIndex = currentIndex < 0 ? 0 : Math.Min(currentIndex + 1, currentItems.Count - 1);
                            }
                            else // Key.Up
                            {
                                // 上键：选择上一个
                                newIndex = currentIndex < 0 ? currentItems.Count - 1 : Math.Max(currentIndex - 1, 0);
                            }
                            
                            // 直接同步更新选中项（PreviewKeyDown 已在 UI 线程）
                            TagAutocompleteListBox.SelectedIndex = newIndex;
                            _selectedAutocompleteIndex = newIndex;
                            
                            // 滚动到可见位置
                            if (newIndex >= 0 && newIndex < currentItems.Count)
                            {
                                TagAutocompleteListBox.ScrollIntoView(currentItems[newIndex]);
                            }
                            
                            // 强制更新布局
                            TagAutocompleteListBox.UpdateLayout();
                            
                            e.Handled = true;
                            System.Diagnostics.Debug.WriteLine($"[Autocomplete] 方向键导航: {e.Key}, 新索引: {newIndex}, 选中项: {(newIndex >= 0 && newIndex < currentItems.Count ? currentItems[newIndex] : "无")}");
                        }
                    }
                }
                else if (!string.IsNullOrWhiteSpace(TagInputTextBox.Text))
                {
                    // 补完列表未打开，但有输入，先打开补完列表
                    UpdateAutocompleteSuggestions(TagInputTextBox.Text);
                    
                    // 等待补完列表打开
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (TagAutocompletePopup.IsOpen && _autocompleteSuggestions.Count > 0)
                        {
                            // 现在处理方向键
                            int newIndex;
                            if (e.Key == Key.Down)
                            {
                                newIndex = 0;
                            }
                            else // Key.Up
                            {
                                newIndex = _autocompleteSuggestions.Count - 1;
                            }
                            
                            TagAutocompleteListBox.SelectedIndex = newIndex;
                            _selectedAutocompleteIndex = newIndex;
                            TagAutocompleteListBox.ScrollIntoView(_autocompleteSuggestions[newIndex]);
                            TagAutocompleteListBox.UpdateLayout();
                                                    }
                    }), System.Windows.Threading.DispatcherPriority.Input);
                    
                    e.Handled = true;
                }
            }
        }

        private void TagInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                // Tab键处理：第一次补完，第二次确认
                if (_isTabForCompletion && TagAutocompletePopup.IsOpen && _autocompleteSuggestions.Count > 0)
                {
                    // 有补完建议，执行补完
                    CompleteTag();
                    e.Handled = true;
                }
                else
                {
                    // 没有补完建议或已补完，确认并添加标签
                    AddTagFromInput();
                    e.Handled = true;
                }
            }
            // 方向键已在 PreviewKeyDown 中处理，这里不再处理
            else if (e.Key == Key.Enter)
            {
                // Enter键处理逻辑：
                // 1. 如果有选中的补完项，先补完
                if (TagAutocompletePopup.IsOpen && _selectedAutocompleteIndex >= 0 && _selectedAutocompleteIndex < _autocompleteSuggestions.Count)
                {
                    CompleteTag();
                    e.Handled = true;
                    return;
                }
                
                // 2. 如果输入框有文本，处理输入的文字（正在输入中）
                if (!string.IsNullOrWhiteSpace(TagInputTextBox.Text))
                {
                    var inputText = TagInputTextBox.Text.Trim();
                    
                    // 检查该tag是否已经在已选定的标签列表中
                    bool tagExists = false;
                    foreach (var child in TagInputPanel.Children)
                    {
                        if (child is Border border && border.Tag is string existingTag && 
                            existingTag.Equals(inputText, StringComparison.OrdinalIgnoreCase))
                        {
                            tagExists = true;
                            break;
                        }
                    }
                    
                    if (tagExists)
                    {
                        // 如果tag已存在，直接选定（不重复添加），清空输入框，继续输入下一个tag
                        TagInputTextBox.Text = "";
                        TagPanelControl?.TagInputTextBox?.Focus();
                        e.Handled = true;
                        return;
                    }
                    else
                    {
                        // 如果tag不存在，添加、选定该tag，清空输入框，继续输入下一个tag
                        AddTagFromInput();
                        TagPanelControl?.TagInputTextBox?.Focus();
                        e.Handled = true;
                        return;
                    }
                }
                
                // 3. 输入框为空，按之前的逻辑判断
                // 检查是否有已选定的标签
                var inputTags = GetInputTags();
                if (inputTags.Count > 0)
                {
                    // 有已选定的标签，确认标签并下一张
                    ConfirmTag_Click(null, null);
                    e.Handled = true;
                    return;
                }
                
                // 4. 没有已选定的标签，检查是否有AI推荐的标签（橙色，超过阈值的）
                if (HasAIRecommendedTags())
                {
                    // 有AI推荐，确认AI预测
                    ConfirmAIPrediction_Click(null, null);
                    e.Handled = true;
                    return;
                }
                
                // 5. 没有已选定的标签，也没有AI推荐，跳过
                Skip_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // ESC键：关闭补完列表
                if (TagAutocompletePopup.IsOpen)
                {
                    TagAutocompletePopup.IsOpen = false;
                    _isTabForCompletion = false;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Back && string.IsNullOrEmpty(TagInputTextBox.Text))
            {
                // 如果输入框为空，按Backspace删除最后一个标签
                RemoveLastTag();
                e.Handled = true;
            }
            else
            {
                // 其他键：重置Tab状态
                _isTabForCompletion = false;
            }
        }

        /// <summary>
        /// 执行标签补完
        /// </summary>
        private void CompleteTag()
        {
            if (_selectedAutocompleteIndex >= 0 && _selectedAutocompleteIndex < _autocompleteSuggestions.Count)
            {
                // 使用选中的建议
                var selectedTag = _autocompleteSuggestions[_selectedAutocompleteIndex];
                TagInputTextBox.Text = selectedTag;
                TagInputTextBox.CaretIndex = selectedTag.Length;
            }
            else if (_autocompleteSuggestions.Count > 0)
            {
                // 如果没有选中项，使用第一个建议
                var firstTag = _autocompleteSuggestions[0];
                TagInputTextBox.Text = firstTag;
                TagInputTextBox.CaretIndex = firstTag.Length;
            }

            // 关闭补完列表，Tab键下次用于确认
            TagAutocompletePopup.IsOpen = false;
            _isTabForCompletion = false;
            _selectedAutocompleteIndex = -1;
            TagInputTextBox.Focus();
        }

        private void TagInputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // 确保输入框在最后
            var border = TagInputTextBox.Parent as Border;
            if (border != null)
            {
                TagInputPanel.Children.Remove(border);
                TagInputPanel.Children.Add(border);
            }
            
            // 更新边框样式（焦点效果）
            if (border != null)
            {
                border.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(74, 144, 226));
                border.BorderThickness = new Thickness(2);
            }
        }

        private void TagInputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 延迟检查，避免立即关闭补完列表
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // 如果焦点转移到补完列表，不关闭补完列表
                if (TagAutocompleteListBox.IsFocused || TagAutocompletePopup.IsMouseOver || TagAutocompleteListBox.IsMouseOver)
                {
                    return;
                }

                // 关闭补完列表
                TagAutocompletePopup.IsOpen = false;
                _isTabForCompletion = false;

                // 失去焦点时，如果有文本，自动添加标签
                if (!string.IsNullOrWhiteSpace(TagInputTextBox.Text))
                {
                    AddTagFromInput();
                }
                
                // 恢复边框样式
                var border = TagInputTextBox.Parent as Border;
                if (border != null)
                {
                    border.BorderBrush = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(176, 176, 176));
                    border.BorderThickness = new Thickness(1);
                }
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void AddTagFromInput()
        {
            var tagName = TagInputTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(tagName))
                return;

            // 关闭补完列表
            TagAutocompletePopup.IsOpen = false;
            _isTabForCompletion = false;
            _selectedAutocompleteIndex = -1;

            // 检查是否已存在
            foreach (var child in TagInputPanel.Children)
            {
                if (child is Border border && border.Tag is string existingTag && existingTag == tagName)
                {
                    TagInputTextBox.Text = "";
                    TagPanelControl?.TagInputTextBox?.Focus();
                    return;
                }
            }

            // 创建标签显示
            var tagBorder = CreateTagBorder(tagName);
            TagInputPanel.Children.Insert(TagInputPanel.Children.Count - 1, tagBorder);
            
            TagInputTextBox.Text = "";
            TagInputTextBox.Focus();
        }

        private Border CreateTagBorder(string tagName)
        {
            var border = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(230, 240, 255)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(100, 150, 220)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(10, 5, 5, 5),
                Margin = new Thickness(0, 0, 6, 6),
                Tag = tagName
            };
            
            // 添加轻微阴影效果
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = System.Windows.Media.Colors.Gray,
                Direction = 270,
                ShadowDepth = 1.5,
                BlurRadius = 3,
                Opacity = 0.3
            };

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var textBlock = new TextBlock
            {
                Text = tagName,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(50, 100, 180))
            };

            var deleteButton = new Button
            {
                Content = "✕",
                Width = 20,
                Height = 20,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(200, 50, 50)),
                Cursor = Cursors.Hand,
                Tag = tagName,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            // 鼠标悬停效果
            deleteButton.MouseEnter += (s, e) =>
            {
                deleteButton.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(255, 200, 200));
                deleteButton.Foreground = System.Windows.Media.Brushes.DarkRed;
            };
            deleteButton.MouseLeave += (s, e) =>
            {
                deleteButton.Background = System.Windows.Media.Brushes.Transparent;
                deleteButton.Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(200, 50, 50));
            };
            deleteButton.Click += (s, e) =>
            {
                TagInputPanel.Children.Remove(border);
            };

            stackPanel.Children.Add(textBlock);
            stackPanel.Children.Add(deleteButton);
            border.Child = stackPanel;

            return border;
        }

        private void RemoveLastTag()
        {
            // 移除最后一个标签（除了输入框）
            for (int i = TagInputPanel.Children.Count - 2; i >= 0; i--)
            {
                if (TagInputPanel.Children[i] is Border)
                {
                    TagInputPanel.Children.RemoveAt(i);
                    break;
                }
            }
        }

        private List<string> GetInputTags()
        {
            var tags = new List<string>();
            foreach (var child in TagInputPanel.Children)
            {
                if (child is Border border && border.Tag is string tagName)
                {
                    tags.Add(tagName);
                }
            }
            return tags;
        }

        /// <summary>
        /// 检查是否有AI推荐的标签（橙色，超过阈值的）
        /// </summary>
        private bool HasAIRecommendedTags()
        {
            if (_currentPredictions == null || _currentPredictions.Count == 0)
            {
                return false;
            }

            // 检查是否有超过阈值的预测
            foreach (var pred in _currentPredictions)
            {
                var confidencePercent = pred.Confidence * 100.0;
                if (confidencePercent >= _predictionThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearTagInput()
        {
            // 清除所有标签，只保留输入框
            var childrenToRemove = new List<UIElement>();
            foreach (UIElement child in TagInputPanel.Children)
            {
                // 保留输入框的Border，移除标签的Border
                if (child is Border border && border.Name != "TagInputBorder")
                {
                    childrenToRemove.Add(child);
                }
            }
            foreach (var child in childrenToRemove)
            {
                TagInputPanel.Children.Remove(child);
            }
            TagInputTextBox.Text = "";
            
            // 恢复输入框边框样式
            var inputBorder = TagInputTextBox.Parent as Border;
            if (inputBorder != null)
            {
                inputBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(176, 176, 176));
                inputBorder.BorderThickness = new Thickness(1);
            }
        }

        private void UpdateProgress()
        {
            var stats = DataManager.GetStatistics();
            ProgressText.Text = $"{stats.ManualSamples}/{stats.TotalSamples}";
            
            // 计算准确率（简化：使用已标注样本数）
            if (stats.TotalSamples > 0)
            {
                var accuracy = (double)stats.ManualSamples / stats.TotalSamples * 100;
                AccuracyText.Text = $"{accuracy:F1}%";
            }
        }

        private void UpdateModelStatus()
        {
            try
            {
                if (TagPanelControl?.ModelStatusIndicator == null || TagPanelControl?.ModelStatusText == null)
                    return;

                bool modelExists = _trainer.ModelExists();
                bool modelLoaded = _trainer.IsModelLoaded();

                if (modelExists && modelLoaded)
                {
                    // 使用缓存的测试结果，如果缓存过期或不存在，则在后台更新
                    if (_cachedTestAccuracy.HasValue && 
                        (DateTime.Now - _lastTestTime).TotalSeconds < 30) // 缓存30秒
                    {
                        UpdateStatusDisplay(_cachedTestAccuracy.Value);
                    }
                    else if (!_isTesting)
                    {
                        // 在后台执行测试
                        _isTesting = true;
                        Task.Run(() =>
                        {
                            try
                            {
                                var testAccuracy = GetQuickTestAccuracy();
                                Dispatcher.Invoke(() =>
                                {
                                    _cachedTestAccuracy = testAccuracy;
                                    _lastTestTime = DateTime.Now;
                                    _isTesting = false;
                                    
                                    if (testAccuracy.HasValue)
                                    {
                                        UpdateStatusDisplay(testAccuracy.Value);
                                    }
                                    else
                                    {
                                        // 绿色：模型正常，但未测试
                                        TagPanelControl.ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                                        TagPanelControl.ModelStatusText.Text = "运行正常";
                                        TagPanelControl.ModelStatusText.Foreground = new SolidColorBrush(Colors.Green);
                                    }
                                });
                            }
                            catch
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    _isTesting = false;
                                    // 绿色：模型正常，但测试失败
                                    TagPanelControl.ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                                    TagPanelControl.ModelStatusText.Text = "运行正常";
                                    TagPanelControl.ModelStatusText.Foreground = new SolidColorBrush(Colors.Green);
                                });
                            }
                        });
                        
                        // 显示测试中状态
                        TagPanelControl.ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Gray);
                        TagPanelControl.ModelStatusText.Text = "测试中...";
                        if (TagPanelControl?.ModelStatusText != null) TagPanelControl.ModelStatusText.Foreground = new SolidColorBrush(Colors.Gray);
                    }
                    else
                    {
                        // 测试进行中，显示缓存结果或默认状态
                        if (_cachedTestAccuracy.HasValue)
                        {
                            UpdateStatusDisplay(_cachedTestAccuracy.Value);
                        }
                        else
                        {
                            if (TagPanelControl?.ModelStatusIndicator != null) TagPanelControl.ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Gray);
                            if (TagPanelControl?.ModelStatusText != null)
                            {
                                TagPanelControl.ModelStatusText.Text = "测试中...";
                                TagPanelControl.ModelStatusText.Foreground = new SolidColorBrush(Colors.Gray);
                            }
                        }
                    }
                }
                else if (modelExists && !modelLoaded)
                {
                    // 黄色：模型存在但未加载
                    ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                    ModelStatusText.Text = "未加载";
                    ModelStatusText.Foreground = new SolidColorBrush(Colors.Orange);
                }
                else
                {
                    // 红色：模型不存在
                    ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                    ModelStatusText.Text = "未训练";
                    ModelStatusText.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
            catch (Exception)
            {
            }
        }

        private double? GetQuickTestAccuracy()
        {
            try
            {
                if (!_trainer.ModelExists() || !_trainer.IsModelLoaded())
                    return null;

                var trainingData = DataManager.LoadAllTrainingData();
                if (trainingData.Count == 0)
                    return null;

                // 快速测试：只测试3张图片以提高速度
                var random = new Random();
                var testSamples = trainingData
                    .Where(t => t.IsManual && File.Exists(t.ImagePath))
                    .OrderBy(x => random.Next())
                    .Take(3)
                    .ToList();

                if (testSamples.Count == 0)
                    return null;

                int successCount = 0;
                int totalCount = 0;

                foreach (var sample in testSamples)
                {
                    try
                    {
                        var predictions = _trainer.PredictTags(sample.ImagePath);
                        totalCount++;
                        
                        if (predictions.Any(p => p.TagId == sample.TagId))
                        {
                            successCount++;
                        }
                    }
                    catch
                    {
                        // 忽略单个测试失败
                    }
                }

                if (totalCount > 0)
                {
                    return (double)successCount / totalCount * 100;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        private void UpdateStatusDisplay(double testAccuracy)
        {
            if (ModelStatusIndicator == null || ModelStatusText == null)
                return;

            // 根据测试准确率显示不同状态
            if (testAccuracy >= 60)
            {
                // 绿色：准确率良好
                ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Green);
                ModelStatusText.Text = $"运行正常 ({testAccuracy:F0}%)";
                ModelStatusText.Foreground = new SolidColorBrush(Colors.Green);
            }
            else if (testAccuracy >= 30)
            {
                // 黄色：准确率较低
                ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                ModelStatusText.Text = $"准确率低 ({testAccuracy:F0}%)";
                ModelStatusText.Foreground = new SolidColorBrush(Colors.Orange);
            }
            else if (testAccuracy > 0)
            {
                // 橙色：准确率很低
                ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Orange);
                ModelStatusText.Text = $"准确率很低 ({testAccuracy:F0}%)";
                ModelStatusText.Foreground = new SolidColorBrush(Colors.Orange);
            }
            else
            {
                // 红色：无法预测
                ModelStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                ModelStatusText.Text = "无法预测 (0%)";
                ModelStatusText.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void UpdateStatistics()
        {
            var stats = DataManager.GetStatistics();
            StatsText.Text = $"总样本: {stats.TotalSamples} | 手动标注: {stats.ManualSamples} | 唯一图片: {stats.UniqueImages} | 唯一标签: {stats.UniqueTags}";
            
            // 异步更新目录图片数量（带防卡死机制）
            UpdateDirectoryImageCountAsync();
        }

        /// <summary>
        /// 异步更新目录图片数量（带防卡死机制）
        /// </summary>
        private async void UpdateDirectoryImageCountAsync()
        {
            // 如果正在计算，取消之前的计算
            if (_imageCountCancellation != null)
            {
                _imageCountCancellation.Cancel();
                _imageCountCancellation.Dispose();
            }

            // 如果缓存有效（5分钟内），直接使用缓存
            if (_cachedImageCount.HasValue && 
                (DateTime.Now - _lastImageCountTime).TotalMinutes < 5 &&
                !string.IsNullOrEmpty(_imageDirectory) &&
                Directory.Exists(_imageDirectory))
            {
                DirectoryImageCountText.Text = _cachedImageCount.Value.ToString("N0");
                return;
            }

            // 如果没有设置图片目录，显示提示
            if (string.IsNullOrEmpty(_imageDirectory) || !Directory.Exists(_imageDirectory))
            {
                DirectoryImageCountText.Text = "-";
                return;
            }

            // 创建新的取消令牌
            _imageCountCancellation = new System.Threading.CancellationTokenSource();
            var cancellationToken = _imageCountCancellation.Token;

            try
            {
                DirectoryImageCountText.Text = "计算中...";
                
                // 在后台线程中统计图片数量
                var imageCount = await Task.Run(() =>
                {
                    return CountImagesInDirectory(_imageDirectory, cancellationToken);
                }, cancellationToken);

                // 检查是否被取消
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                // 更新显示和缓存
                _cachedImageCount = imageCount;
                _lastImageCountTime = DateTime.Now;
                
                if (DirectoryImageCountText != null)
                {
                    DirectoryImageCountText.Text = imageCount.HasValue 
                        ? imageCount.Value.ToString("N0") 
                        : "超时";
                }
            }
            catch (OperationCanceledException)
            {
                // 计算被取消，不更新显示
                if (DirectoryImageCountText != null)
                {
                    DirectoryImageCountText.Text = "已取消";
                }
            }
            catch (Exception)
            {
                if (DirectoryImageCountText != null)
                {
                    DirectoryImageCountText.Text = "错误";
                }
            }
            finally
            {
                if (_imageCountCancellation != null)
                {
                    _imageCountCancellation.Dispose();
                    _imageCountCancellation = null;
                }
            }
        }

        /// <summary>
        /// 统计目录中的图片数量（带超时和取消机制）
        /// </summary>
        private int? CountImagesInDirectory(string directory, System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp"
                };

                int count = 0;
                var maxCount = 100000; // 最大统计数量，防止数量过大
                var timeout = TimeSpan.FromSeconds(10); // 10秒超时
                var startTime = DateTime.Now;

                // 使用队列进行广度优先搜索，避免递归导致栈溢出
                var directoriesToProcess = new Queue<string>();
                directoriesToProcess.Enqueue(directory);

                while (directoriesToProcess.Count > 0)
                {
                    // 检查取消令牌
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return null;
                    }

                    // 检查超时
                    if (DateTime.Now - startTime > timeout)
                    {
                                                return null;
                    }

                    // 检查数量上限
                    if (count >= maxCount)
                    {
                                                return maxCount; // 返回上限值，表示还有很多
                    }

                    var currentDir = directoriesToProcess.Dequeue();

                    try
                    {
                        // 统计当前目录的图片文件
                        var files = Directory.GetFiles(currentDir, "*.*", SearchOption.TopDirectoryOnly);
                        foreach (var file in files)
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return null;
                            }

                            var ext = Path.GetExtension(file);
                            if (imageExtensions.Contains(ext))
                            {
                                count++;
                                
                                // 每处理1000个文件检查一次超时和取消
                                if (count % 1000 == 0)
                                {
                                    if (DateTime.Now - startTime > timeout)
                                    {
                                        return null;
                                    }
                                    if (cancellationToken.IsCancellationRequested)
                                    {
                                        return null;
                                    }
                                }
                            }
                        }

                        // 添加子目录到队列
                        try
                        {
                            var subDirs = Directory.GetDirectories(currentDir);
                            foreach (var subDir in subDirs)
                            {
                                if (cancellationToken.IsCancellationRequested)
                                {
                                    return null;
                                }
                                directoriesToProcess.Enqueue(subDir);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            // 无权限访问的目录，跳过
                                                    }
                        catch (Exception)
                        {
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 无权限访问的目录，跳过
                                            }
                    catch (Exception)
                    {
                    }
                }

                return count;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void UpdateStatus(string status)
        {
            StatusText.Text = status;
        }

        private void ConsolidateTags_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "此操作将整合所有同名标签到一个TagId，保留所有训练数据。\n\n" +
                "注意：此操作不可逆，建议先备份数据库。是否继续？",
                "确认清理", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                UpdateStatus("正在清理重复标签...");
                ConsolidateTagsBtn.IsEnabled = false;

                // 在后台线程执行清理操作
                Task.Run(() =>
                {
                    try
                    {
                        var (mergedGroups, updatedSamples, deletedTagIds) = DataManager.ConsolidateDuplicateTags();

                        Dispatcher.Invoke(() =>
                        {
                            ConsolidateTagsBtn.IsEnabled = true;
                            
                            if (mergedGroups > 0)
                            {
                                MessageBox.Show(
                                    $"清理完成！\n\n" +
                                    $"合并的标签组数: {mergedGroups}\n" +
                                    $"更新的训练数据: {updatedSamples} 条\n" +
                                    $"删除的重复TagId: {deletedTagIds} 个",
                                    "清理完成", MessageBoxButton.OK, MessageBoxImage.Information);
                                
                                // 刷新标签缓存和显示
                                _tagCache = DataManager.GetAllTagNames();
                                LoadExistingTags();
                                UpdateStatistics();
                                UpdateStatus("清理完成");
                            }
                            else
                            {
                                MessageBox.Show("未发现重复标签，无需清理。", "提示", 
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                                UpdateStatus("未发现重复标签");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ConsolidateTagsBtn.IsEnabled = true;
                            MessageBox.Show($"清理失败: {ex.Message}", "错误", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            UpdateStatus($"清理失败: {ex.Message}");
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                ConsolidateTagsBtn.IsEnabled = true;
                MessageBox.Show($"清理失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus($"清理失败: {ex.Message}");
            }
        }

        private void CategoryManagement_Click(object sender, RoutedEventArgs e)
        {
            OpenCategoryManagement();
        }

        private void OpenCategoryManagement()
        {
            try
            {
                var window = new CategoryManagementWindow
                {
                    Owner = this
                };
                window.ShowDialog();
                
                // 刷新标签列表以显示新的分组
                LoadExistingTags();
            }
            catch (Exception)
            {
                MessageBox.Show("打开分组管理窗口失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Config_Click(object sender, RoutedEventArgs e)
        {
            var configWindow = new ConfigWindow();
            if (configWindow.ShowDialog() == true)
            {
                var oldDirectory = _imageDirectory;
                _imageDirectory = configWindow.ImageDirectory;
                
                // 如果图片目录已更改，清除图片数量缓存
                if (!string.IsNullOrEmpty(_imageDirectory) && 
                    !_imageDirectory.Equals(oldDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    _cachedImageCount = null;
                    _lastImageCountTime = DateTime.MinValue;
                }
                
                // 更新设置
                _tagsPerRow = configWindow.TagsPerRow;
                _predictionThreshold = configWindow.PredictionThreshold;
                
                // 刷新标签显示
                LoadExistingTags();
                
                // 延迟更新标签宽度，确保面板已渲染
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateTagWidths();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }


        private void RetrainModel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "重新训练模型将使用所有已标注的数据，可能需要较长时间。是否继续？",
                "确认", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // 如果已有训练在进行，先取消
            if (_trainingCancellation != null && !_trainingCancellation.IsCancellationRequested)
            {
                MessageBox.Show("已有训练正在进行，请先取消当前训练", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 创建新的取消令牌
            _trainingCancellation = new CancellationTokenSource();
            var progress = new Progress<TrainingProgress>(UpdateTrainingProgress);

            // 显示进度条
            TrainingProgressGrid.Visibility = Visibility.Visible;
            TrainingProgressBar.Value = 0;
            TrainingStageText.Text = "";
            TrainingProgressText.Text = "";
            // 重置进度平滑定时器
            _progressSmoother?.Stop();
            _progressSmoother = null;

            UpdateStatus("训练模型中，请稍候...");
            StartTrainingBtn.IsEnabled = false;
            RetrainModelBtn.IsEnabled = false;
            CancelTrainingBtn.IsEnabled = true;

            // 在后台线程中执行训练，避免UI卡死
            Task.Run(() =>
            {
                try
                {
                    var samples = DataManager.LoadAllTrainingData();
                    
                    // 添加调试信息
                                        System.Diagnostics.Debug.WriteLine($"模型保存路径: {_trainer.GetModelPath()}");
                    
                    var trainingResult = _trainer.TrainModel(samples, progress, _trainingCancellation.Token);
                    
                    // 回到UI线程更新界面
                    Dispatcher.Invoke(() =>
                    {
                        // 隐藏进度条
                        TrainingProgressGrid.Visibility = Visibility.Collapsed;

                        if (trainingResult.Success)
                        {
                            // 验证模型文件是否真的创建了
                            var modelPath = _trainer.GetModelPath();
                            if (File.Exists(modelPath))
                            {
                                var fileInfo = new FileInfo(modelPath);
                                MessageBox.Show(
                                    $"模型训练完成！\n使用样本数: {trainingResult.SampleCount}\n模型文件: {modelPath}\n文件大小: {fileInfo.Length} 字节",
                                    "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"训练报告成功，但模型文件未找到！\n路径: {modelPath}\n\n请查看调试输出获取更多信息",
                                    "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                            
                            // 重新加载模型
                            _trainer.LoadModel();
                            // 清除测试缓存，强制重新测试
                            _cachedTestAccuracy = null;
                            _lastTestTime = DateTime.MinValue;
                            UpdateModelStatus();
                        }
                        else
                        {
                            // 显示详细错误信息
                            var errorMsg = $"模型训练失败: {trainingResult.Message}";
                                                        // 如果是取消，不显示错误对话框
                            if (trainingResult.Message != "训练已取消")
                            {
                                MessageBox.Show(
                                    errorMsg + "\n\n请查看Visual Studio的输出窗口（调试输出）获取更多信息",
                                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            UpdateModelStatus();
                        }
                        
                        UpdateStatus("就绪");
                        StartTrainingBtn.IsEnabled = true;
                        RetrainModelBtn.IsEnabled = true;
                        CancelTrainingBtn.IsEnabled = false;
                        _trainingCancellation = null;
                    });
                }
                catch (Exception ex)
                {
                    var errorMsg = $"训练出错: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}";
                                        if (ex.InnerException != null)
                    {
                                                errorMsg += $"\n\n内部异常: {ex.InnerException.Message}";
                    }
                    
                    Dispatcher.Invoke(() =>
                    {
                        // 隐藏进度条
                        TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        MessageBox.Show(errorMsg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        UpdateStatus("就绪");
                        StartTrainingBtn.IsEnabled = true;
                        RetrainModelBtn.IsEnabled = true;
                        CancelTrainingBtn.IsEnabled = false;
                        _trainingCancellation = null;
                    });
                }
            });
        }

        private void CancelTraining_Click(object sender, RoutedEventArgs e)
        {
            if (_trainingCancellation != null && !_trainingCancellation.IsCancellationRequested)
            {
                var result = MessageBox.Show(
                    "确定要取消当前训练吗？",
                    "确认取消", MessageBoxButton.YesNo, MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    _trainingCancellation.Cancel();
                    UpdateStatus("正在取消训练...");
                    CancelTrainingBtn.IsEnabled = false;
                }
            }
        }

        private void UpdateTrainingProgress(TrainingProgress progress)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _lastProgress = progress.Progress;
                TrainingStageText.Text = progress.Stage;
                
                // 在"训练中"阶段做平滑推进到79%（每5秒增加1%）
                if (string.Equals(progress.Stage, "训练中", StringComparison.OrdinalIgnoreCase))
                {
                    // 初始化定时器（如果还没有创建）
                    if (_progressSmoother == null)
                    {
                        _progressSmoother = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromSeconds(5)
                        };
                        _progressSmoother.Tick += (s, e) =>
                        {
                            var v = (int)TrainingProgressBar.Value;
                            if (v < 79)
                            {
                                TrainingProgressBar.Value = v + 1;
                                TrainingProgressText.Text = $"{(int)TrainingProgressBar.Value}% - 训练中...";
                            }
                            else
                            {
                                _progressSmoother?.Stop();
                            }
                        };
                    }
                    
                    // 只在第一次进入"训练中"阶段时设置进度条为50%，之后由定时器控制
                    if (!_progressSmoother.IsEnabled)
                    {
                        TrainingProgressBar.Value = progress.Progress;
                        TrainingProgressText.Text = $"{progress.Progress}% - {progress.Message}";
                        _progressSmoother.Start();
                    }
                    // 如果定时器已经在运行，不覆盖进度条的值，让定时器继续推进
                }
                else
                {
                    // 其他阶段：停止定时器，直接设置进度条值
                    _progressSmoother?.Stop();
                    TrainingProgressBar.Value = progress.Progress;
                    TrainingProgressText.Text = $"{progress.Progress}% - {progress.Message}";
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        private void CurrentImageText_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_imageList == null || _imageList.Count == 0)
            {
                MessageBox.Show("没有可跳转的图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 创建跳转对话框
            var jumpDialog = new Window
            {
                Title = "跳转到指定图片",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            var labelText = new TextBlock
            {
                Text = $"请输入图片序号 (1-{_imageList.Count}):",
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(labelText);

            var indexInput = new TextBox
            {
                Height = 30,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(indexInput);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var okBtn = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okBtn.Click += (s, args) =>
            {
                jumpDialog.DialogResult = true;
                jumpDialog.Close();
            };
            buttonPanel.Children.Add(okBtn);

            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30
            };
            cancelBtn.Click += (s, args) =>
            {
                jumpDialog.DialogResult = false;
                jumpDialog.Close();
            };
            buttonPanel.Children.Add(cancelBtn);

            stackPanel.Children.Add(buttonPanel);
            jumpDialog.Content = stackPanel;

            // 设置焦点
            jumpDialog.Loaded += (s, args) => indexInput.Focus();

            if (jumpDialog.ShowDialog() == true)
            {
                if (int.TryParse(indexInput.Text.Trim(), out int targetIndex))
                {
                    if (targetIndex >= 1 && targetIndex <= _imageList.Count)
                    {
                        _currentImageIndex = targetIndex - 1;
                        LoadImageByIndex(_currentImageIndex);
                    }
                    else
                    {
                        MessageBox.Show($"请输入 1 到 {_imageList.Count} 之间的数字", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageBox.Show("请输入有效的数字", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void LoadImageByIndex(int index)
        {
            if (_imageList == null || index < 0 || index >= _imageList.Count)
            {
                return;
            }

            _currentImageIndex = index;
            var imagePath = _imageList[_currentImageIndex];

            try
            {
                // 加载图片 - 使用文件流方式确保路径中的特殊字符能正确处理
                if (!File.Exists(imagePath))
                {
                    throw new FileNotFoundException($"图片文件不存在: {imagePath}");
                }
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                // 使用文件流加载，避免URI路径解析问题（特别是包含特殊字符的路径）
                bitmap.StreamSource = new FileStream(imagePath, FileMode.Open, FileAccess.Read);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                ImageDisplay.Source = bitmap;
                NoImageText.Visibility = Visibility.Collapsed;
                
                // 显示图片索引和完整路径
                var imageIndex = _currentImageIndex + 1;
                var totalCount = _imageList.Count;
                CurrentImageText.Text = $"第 {imageIndex}/{totalCount} 张: {imagePath}";

                // 显示AI预测结果
                ShowPredictions(imagePath);

                // 加载当前图片的已有标签
                LoadCurrentImageTags(imagePath);

                // 清空标签输入
                ClearTagInput();
                TagPanelControl?.TagInputTextBox?.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载图片失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BatchOperation_Click(object sender, RoutedEventArgs e)
        {
            if (_imageList == null || _imageList.Count == 0)
            {
                MessageBox.Show("没有可操作的图片列表，请先开始训练", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 创建批量操作对话框
            var dialog = new Window
            {
                Title = "批量操作",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            var titleText = new TextBlock
            {
                Text = $"当前有 {_imageList.Count} 张未标注图片",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 20)
            };
            stackPanel.Children.Add(titleText);

            var confirmAIPredictionBtn = new Button
            {
                Content = "🤖 批量确认 AI 预测结果",
                Height = 40,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };
            confirmAIPredictionBtn.Click += (s, args) =>
            {
                dialog.DialogResult = true;
                dialog.Tag = "ConfirmAIPrediction";
                dialog.Close();
            };
            stackPanel.Children.Add(confirmAIPredictionBtn);

            var addTagBtn = new Button
            {
                Content = "➕ 批量添加标签",
                Height = 40,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };
            addTagBtn.Click += (s, args) =>
            {
                dialog.DialogResult = true;
                dialog.Tag = "AddTag";
                dialog.Close();
            };
            stackPanel.Children.Add(addTagBtn);

            var deleteTagBtn = new Button
            {
                Content = "➖ 批量删除标签",
                Height = 40,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };
            deleteTagBtn.Click += (s, args) =>
            {
                dialog.DialogResult = true;
                dialog.Tag = "DeleteTag";
                dialog.Close();
            };
            stackPanel.Children.Add(deleteTagBtn);

            var cancelBtn = new Button
            {
                Content = "取消",
                Height = 35,
                FontSize = 13,
                Margin = new Thickness(0, 10, 0, 0)
            };
            cancelBtn.Click += (s, args) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };
            stackPanel.Children.Add(cancelBtn);

            dialog.Content = stackPanel;

            if (dialog.ShowDialog() == true)
            {
                var operation = dialog.Tag?.ToString();
                switch (operation)
                {
                    case "ConfirmAIPrediction":
                        BatchConfirmAIPrediction();
                        break;
                    case "AddTag":
                        BatchAddTag();
                        break;
                    case "DeleteTag":
                        BatchDeleteTag();
                        break;
                }
            }
        }

        private void BatchConfirmAIPrediction()
        {
            if (_imageList == null || _imageList.Count == 0)
            {
                MessageBox.Show("没有可操作的图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确定要批量确认所有 {_imageList.Count} 张图片的 AI 预测结果吗？\n\n这将自动为每张图片确认并保存 AI 预测的标签。",
                "确认批量操作", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // 在后台线程中执行批量操作
            Task.Run(() =>
            {
                int successCount = 0;
                int failCount = 0;

                try
                {
                    Dispatcher.Invoke(() =>
                    {
                        UpdateStatus($"批量确认中... (0/{_imageList.Count})");
                    });

                    // 创建图片列表的副本，避免在循环中修改原列表
                    var imageListCopy = new List<string>(_imageList);

                    // 遍历所有图片
                    for (int i = 0; i < imageListCopy.Count; i++)
                    {
                        var imagePath = imageListCopy[i];
                        
                        try
                        {
                            // 获取该图片的 AI 预测结果（需要在UI线程中调用，因为ML.NET可能不是线程安全的）
                            List<Services.TagPredictionResult> predictions = null;
                            Dispatcher.Invoke(() =>
                            {
                                predictions = _trainer.PredictTags(imagePath);
                            });
                            
                            if (predictions != null && predictions.Count > 0)
                            {
                                // 获取所有预测的标签名称
                                var tagNames = new List<string>();
                                foreach (var pred in predictions)
                                {
                                    string tagText = null;
                                    Dispatcher.Invoke(() =>
                                    {
                                        tagText = _tagCache.ContainsKey(pred.TagId)
                                            ? _tagCache[pred.TagId]
                                            : DataManager.GetTagName(pred.TagId) ?? $"标签{pred.TagId}";
                                    });

                                    if (!string.IsNullOrEmpty(tagText) && !tagNames.Contains(tagText))
                                    {
                                        tagNames.Add(tagText);
                                    }
                                }

                                // 保存每个预测标签作为训练数据
                                foreach (var tagName in tagNames)
                                {
                                    var tagId = DataManager.GetOrCreateTagId(tagName);
                                    DataManager.SaveTrainingSample(imagePath, tagId, isManual: true, tagName: tagName);
                                    Dispatcher.Invoke(() =>
                                    {
                                        _tagCache[tagId] = tagName;
                                    });
                                }

                                successCount++;
                            }
                            else
                            {
                                failCount++;
                            }
                        }
                        catch (Exception)
                        {
                            failCount++;
                        }

                        // 更新状态
                        if (i % 10 == 0 || i == imageListCopy.Count - 1)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                UpdateStatus($"批量确认中... ({i + 1}/{imageListCopy.Count})");
                            });
                        }
                    }

                    // 在UI线程中更新UI
                    Dispatcher.Invoke(() =>
                    {
                        // 重新获取未标注图片列表（已标注的会被排除）
                        _imageList = DataManager.GetUnlabeledImages(_imageDirectory);

                        // 更新统计
                        UpdateStatistics();
                        LoadExistingTags();

                        // 恢复当前图片显示
                        if (_imageList.Count > 0)
                        {
                            _currentImageIndex = 0;
                            LoadNextImage();
                        }
                        else
                        {
                            _isTraining = false;
                            StartTrainingBtn.Content = "▶️ 开始训练";
                            UpdateStatus("所有图片已标注完成");
                        }

                        MessageBox.Show(
                            $"批量确认完成！\n成功: {successCount} 张\n失败: {failCount} 张",
                            "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            $"批量确认出错: {ex.Message}",
                            "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        private void BatchAddTag()
        {
            if (_imageList == null || _imageList.Count == 0)
            {
                MessageBox.Show("没有可操作的图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 输入标签对话框
            var inputDialog = new Window
            {
                Title = "批量添加标签",
                Width = 350,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            var labelText = new TextBlock
            {
                Text = $"为 {_imageList.Count} 张图片添加标签（多个标签用逗号分隔）:",
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(labelText);

            var tagInput = new TextBox
            {
                Height = 30,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 15)
            };
            stackPanel.Children.Add(tagInput);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var okBtn = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okBtn.Click += (s, args) =>
            {
                inputDialog.DialogResult = true;
                inputDialog.Close();
            };
            buttonPanel.Children.Add(okBtn);

            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30
            };
            cancelBtn.Click += (s, args) =>
            {
                inputDialog.DialogResult = false;
                inputDialog.Close();
            };
            buttonPanel.Children.Add(cancelBtn);

            stackPanel.Children.Add(buttonPanel);
            inputDialog.Content = stackPanel;

            // 设置焦点
            inputDialog.Loaded += (s, args) => tagInput.Focus();

            if (inputDialog.ShowDialog() == true)
            {
                var tagInputText = tagInput.Text.Trim();
                if (string.IsNullOrEmpty(tagInputText))
                {
                    MessageBox.Show("请输入标签", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var tagNames = tagInputText.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                if (tagNames.Count == 0)
                {
                    MessageBox.Show("请输入有效的标签", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show(
                    $"确定要为所有 {_imageList.Count} 张图片添加以下标签吗？\n\n{string.Join(", ", tagNames)}",
                    "确认批量操作", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // 在后台线程中执行批量操作
                Task.Run(() =>
                {
                    int successCount = 0;

                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UpdateStatus($"批量添加标签中... (0/{_imageList.Count})");
                        });

                        // 创建图片列表的副本
                        var imageListCopy = new List<string>(_imageList);

                        for (int i = 0; i < imageListCopy.Count; i++)
                        {
                            var imagePath = imageListCopy[i];
                            try
                            {
                                foreach (var tagName in tagNames)
                                {
                                    var tagId = DataManager.GetOrCreateTagId(tagName);
                                    DataManager.SaveTrainingSample(imagePath, tagId, isManual: true, tagName: tagName);
                                    Dispatcher.Invoke(() =>
                                    {
                                        _tagCache[tagId] = tagName;
                                    });
                                }
                                successCount++;
                            }
                    catch (Exception)
                    {
                    }

                            // 更新状态
                            if (i % 10 == 0 || i == imageListCopy.Count - 1)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    UpdateStatus($"批量添加标签中... ({i + 1}/{imageListCopy.Count})");
                                });
                            }
                        }

                        // 在UI线程中更新UI
                        Dispatcher.Invoke(() =>
                        {
                            // 重新获取未标注图片列表
                            _imageList = DataManager.GetUnlabeledImages(_imageDirectory);

                            // 更新统计
                            UpdateStatistics();
                            LoadExistingTags();

                            // 恢复当前图片显示
                            if (_imageList.Count > 0)
                            {
                                _currentImageIndex = 0;
                                LoadNextImage();
                            }
                            else
                            {
                                _isTraining = false;
                                StartTrainingBtn.Content = "▶️ 开始训练";
                                UpdateStatus("所有图片已标注完成");
                            }

                            MessageBox.Show(
                                $"批量添加标签完成！\n成功: {successCount} 张",
                                "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"批量添加标签出错: {ex.Message}",
                                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
        }

        private void BatchDeleteTag()
        {
            if (_imageList == null || _imageList.Count == 0)
            {
                MessageBox.Show("没有可操作的图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 获取所有标签列表
            var allTagNames = DataManager.GetAllTagNames();
            if (allTagNames.Count == 0)
            {
                MessageBox.Show("没有可删除的标签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 选择标签对话框
            var selectDialog = new Window
            {
                Title = "批量删除标签",
                Width = 400,
                Height = 400,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            var labelText = new TextBlock
            {
                Text = $"从 {_imageList.Count} 张图片中删除标签:",
                Margin = new Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(labelText);

            var listBox = new ListBox
            {
                Height = 250,
                SelectionMode = SelectionMode.Multiple
            };

            // 获取每个标签的使用次数（从数据库查询）
            var tagCounts = new Dictionary<int, int>();
            var dbPath = DataManager.GetDatabasePath();
            var connectionString = $"Data Source={dbPath}";
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "SELECT TagId, COUNT(*) FROM TrainingData GROUP BY TagId";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tagCounts[reader.GetInt32(0)] = reader.GetInt32(1);
                    }
                }
            }

            // 按使用次数排序显示
            foreach (var kvp in allTagNames.OrderByDescending(x => tagCounts.GetValueOrDefault(x.Key, 0)))
            {
                var count = tagCounts.GetValueOrDefault(kvp.Key, 0);
                var item = new ListBoxItem
                {
                    Content = $"{kvp.Value} (使用次数: {count})",
                    Tag = kvp.Key // 存储 TagId
                };
                listBox.Items.Add(item);
            }

            stackPanel.Children.Add(listBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };

            var okBtn = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            okBtn.Click += (s, args) =>
            {
                selectDialog.DialogResult = true;
                selectDialog.Close();
            };
            buttonPanel.Children.Add(okBtn);

            var cancelBtn = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 30
            };
            cancelBtn.Click += (s, args) =>
            {
                selectDialog.DialogResult = false;
                selectDialog.Close();
            };
            buttonPanel.Children.Add(cancelBtn);

            stackPanel.Children.Add(buttonPanel);
            selectDialog.Content = stackPanel;

            if (selectDialog.ShowDialog() == true)
            {
                var selectedTagIds = listBox.SelectedItems.Cast<ListBoxItem>()
                    .Select(item => (int)item.Tag)
                    .ToList();

                if (selectedTagIds.Count == 0)
                {
                    MessageBox.Show("请选择要删除的标签", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectedTagNames = selectedTagIds.Select(id => 
                {
                    var name = DataManager.GetTagName(id);
                    return name ?? $"标签{id}";
                }).ToList();

                var result = MessageBox.Show(
                    $"确定要从所有 {_imageList.Count} 张图片中删除以下标签吗？\n\n{string.Join(", ", selectedTagNames)}",
                    "确认批量操作", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // 在后台线程中执行批量操作
                Task.Run(() =>
                {
                    int successCount = 0;

                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            UpdateStatus($"批量删除标签中... (0/{_imageList.Count})");
                        });

                        // 创建图片列表的副本
                        var imageListCopy = new List<string>(_imageList);

                        for (int i = 0; i < imageListCopy.Count; i++)
                        {
                            var imagePath = imageListCopy[i];
                            try
                            {
                                foreach (var tagId in selectedTagIds)
                                {
                                    DataManager.DeleteTrainingSample(imagePath, tagId);
                                }
                                successCount++;
                            }
                    catch (Exception)
                    {
                    }

                            // 更新状态
                            if (i % 10 == 0 || i == imageListCopy.Count - 1)
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    UpdateStatus($"批量删除标签中... ({i + 1}/{imageListCopy.Count})");
                                });
                            }
                        }

                        // 在UI线程中更新UI
                        Dispatcher.Invoke(() =>
                        {
                            // 更新统计
                            UpdateStatistics();
                            LoadExistingTags();

                            // 恢复当前图片显示
                            if (_imageList.Count > 0 && _currentImageIndex < _imageList.Count)
                            {
                                LoadNextImage();
                            }

                            MessageBox.Show(
                                $"批量删除标签完成！\n成功: {successCount} 张",
                                "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(
                                $"批量删除标签出错: {ex.Message}",
                                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
        }

        private void TrainingStatus_Click(object sender, RoutedEventArgs e)
        {
            var statusWindow = new TrainingStatusWindow(_trainer);
            statusWindow.Owner = this;
            statusWindow.ShowDialog();
        }

        private void LoadCurrentImageTags(string imagePath)
        {
            // 获取当前图片的已有标签
            // 由于改为WrapPanel显示，标签会显示预测百分比，不再需要选中状态
        }

        private void DeleteTag_Click(object sender, RoutedEventArgs e)
        {
            // 从按钮的Tag获取标签名称
            if (sender is Button button && button.Tag is string tagName)
            {
                var result = MessageBox.Show(
                    $"确定要删除标签 \"{tagName}\" 吗？\n这将删除所有使用该标签的训练数据。",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // 找到对应的TagId
                    var tagId = _tagCache.FirstOrDefault(x => x.Value == tagName).Key;
                    
                    if (tagId == 0)
                    {
                        // 如果缓存中没有，尝试从训练数据中查找
                        var trainingData = DataManager.LoadAllTrainingData();
                        var tagGroup = trainingData
                            .Where(t => t.IsManual)
                            .GroupBy(t => t.TagId)
                            .FirstOrDefault(g => 
                                (_tagCache.ContainsKey(g.Key) && _tagCache[g.Key] == tagName) ||
                                (!_tagCache.ContainsKey(g.Key) && $"标签{g.Key}" == tagName));
                        
                        if (tagGroup != null)
                        {
                            tagId = tagGroup.Key;
                        }
                    }
                    
                    if (tagId != 0)
                    {
                        // 删除该标签的所有训练数据
                        DeleteTagData(tagId);
                        
                        // 从缓存中移除
                        if (_tagCache.ContainsKey(tagId))
                        {
                            _tagCache.Remove(tagId);
                        }
                        
                        // 重新加载标签列表
                        LoadExistingTags();
                        LoadTags();
                    }
                    else
                    {
                        MessageBox.Show("未找到要删除的标签", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }


        private void DeleteTagData(int tagId)
        {
            // 从数据库删除该标签的所有训练数据
            try
            {
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TagTrain", "training.db");
                
                if (!File.Exists(dbPath))
                    return;

                using (var connection = new SqliteConnection($"Data Source={dbPath}"))
                {
                    connection.Open();
                    var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "DELETE FROM TrainingData WHERE TagId = @tagId";
                    deleteCommand.Parameters.AddWithValue("@tagId", tagId);
                    deleteCommand.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除标签数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 窗口大小变化时，延迟更新标签宽度，确保UI已渲染
            // 使用Background优先级避免阻塞UI线程，并减少调试器警告
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (ExistingTagsPanel != null && ExistingTagsPanel.Children.Count > 0)
                    {
                        UpdateTagWidths();
                    }
                }
                catch (Exception)
                {
                    // 忽略UI更新时的异常，避免影响程序运行
                                    }
                // 图片使用Stretch="Uniform"会自动适应窗口大小，无需手动处理
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            // 窗口位置变化时保存（仅在非最大化状态下）
            if (this.WindowState == WindowState.Normal && this.IsLoaded)
            {
                SaveSettings();
                            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            // 窗口状态变化时保存
            if (this.IsLoaded)
            {
                SaveSettings();
                            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // 关闭时立即保存窗口状态和位置
            SaveSettings();
            
            // 如果有正在训练的状态，自动停止训练
            if (_isTraining)
            {
                _isTraining = false;
                StartTrainingBtn.Content = "▶️ 开始训练";
                UpdateStatus("训练已停止");
                            }

            // 取消图片数量统计任务
            if (_imageCountCancellation != null)
            {
                _imageCountCancellation.Cancel();
                _imageCountCancellation.Dispose();
                _imageCountCancellation = null;
            }

            // 使用统一配置管理器保存设置和窗口位置
            SaveSettings();

            // 数据是实时保存的，无需额外保存操作
        }

        /// <summary>
        /// 补完列表双击事件：选择并补完标签
        /// </summary>
        private void TagAutocompleteListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TagAutocompleteListBox.SelectedItem is string selectedTag)
            {
                TagInputTextBox.Text = selectedTag;
                TagInputTextBox.CaretIndex = selectedTag.Length;
                TagAutocompletePopup.IsOpen = false;
                _isTabForCompletion = false;
                TagPanelControl?.TagInputTextBox?.Focus();
            }
        }

        /// <summary>
        /// 补完列表键盘事件处理
        /// </summary>
        private void TagAutocompleteListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Enter键：补完选中的标签
                if (TagAutocompleteListBox.SelectedItem is string selectedTag)
                {
                    TagInputTextBox.Text = selectedTag;
                    TagInputTextBox.CaretIndex = selectedTag.Length;
                    TagAutocompletePopup.IsOpen = false;
                    _isTabForCompletion = false;
                    TagPanelControl?.TagInputTextBox?.Focus();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                // ESC键：关闭补完列表
                TagAutocompletePopup.IsOpen = false;
                _isTabForCompletion = false;
                TagPanelControl?.TagInputTextBox?.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                // Tab键：补完并确认
                if (TagAutocompleteListBox.SelectedItem is string selectedTag)
                {
                    TagInputTextBox.Text = selectedTag;
                    TagInputTextBox.CaretIndex = selectedTag.Length;
                }
                TagAutocompletePopup.IsOpen = false;
                _isTabForCompletion = false;
                // 继续处理Tab键，触发确认添加
                AddTagFromInput();
                e.Handled = true;
            }
        }

        // ========== 滚动条和分割器显示/隐藏控制 ==========
        
        private void TagsScrollViewer_MouseMove(object sender, MouseEventArgs e)
        {
            if (TagsScrollViewer == null) return;

            // 检测鼠标是否在滚动条区域（右侧边缘约20像素内）
            var position = e.GetPosition(TagsScrollViewer);
            var scrollBarAreaWidth = 20; // 滚动条区域宽度
            
            // 检查是否有垂直滚动条（如果有内容需要滚动）
            bool hasVerticalScrollBar = TagsScrollViewer.ComputedVerticalScrollBarVisibility == System.Windows.Visibility.Visible ||
                                       TagsScrollViewer.ScrollableHeight > 0;
            
            if (hasVerticalScrollBar)
            {
                // 如果鼠标在右侧滚动条区域，显示滚动条
                if (position.X >= TagsScrollViewer.ActualWidth - scrollBarAreaWidth)
                {
                    TagsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                    StartScrollBarHideTimer();
                }
                else
                {
                    // 鼠标不在滚动条区域，立即启动隐藏定时器
                    if (!_isWheelScrolling)
                    {
                        StartScrollBarHideTimer();
                    }
                }
            }
            else
            {
                // 没有滚动条，直接隐藏
                TagsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
        }

        private void TagsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (TagsScrollViewer == null) return;

            // 滚轮滚动时显示滚动条
            _isWheelScrolling = true;
            TagsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            
            // 重置隐藏定时器
            StartScrollBarHideTimer();
            
            // 延迟重置滚轮滚动标志
            System.Windows.Threading.DispatcherTimer wheelTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            wheelTimer.Tick += (s, args) =>
            {
                _isWheelScrolling = false;
                wheelTimer.Stop();
            };
            wheelTimer.Start();
        }

        private void StartScrollBarHideTimer()
        {
            if (_scrollBarHideTimer == null)
            {
                _scrollBarHideTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1000)
                };
                _scrollBarHideTimer.Tick += (s, args) =>
                {
                    if (TagsScrollViewer != null && !_isWheelScrolling)
                    {
                        // 使用 HitTest 检查鼠标是否真的在 ScrollViewer 内
                        var mousePosition = Mouse.GetPosition(this);
                        var hitTestResult = VisualTreeHelper.HitTest(this, mousePosition);
                        
                        bool isMouseOverScrollViewer = false;
                        bool isMouseOverScrollBar = false;
                        
                        if (hitTestResult != null)
                        {
                            var element = hitTestResult.VisualHit;
                            while (element != null)
                            {
                                if (element == TagsScrollViewer || 
                                    (element is FrameworkElement fe && fe.Name == "TagsScrollViewer"))
                                {
                                    isMouseOverScrollViewer = true;
                                    // 检查是否在滚动条区域
                                    var position = Mouse.GetPosition(TagsScrollViewer);
                                    var scrollBarAreaWidth = 20;
                                    if (position.X >= TagsScrollViewer.ActualWidth - scrollBarAreaWidth)
                                    {
                                        isMouseOverScrollBar = true;
                                    }
                                    break;
                                }
                                element = VisualTreeHelper.GetParent(element);
                            }
                        }
                        
                        // 如果鼠标不在 ScrollViewer 内，或者不在滚动条区域，隐藏滚动条
                        if (!isMouseOverScrollViewer || !isMouseOverScrollBar)
                        {
                            TagsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                        }
                    }
                    _scrollBarHideTimer.Stop();
                };
            }
            _scrollBarHideTimer.Stop();
            _scrollBarHideTimer.Start();
        }

        private void TagsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // 如果是滚轮滚动导致的滚动，显示滚动条
            if (_isWheelScrolling && TagsScrollViewer != null)
            {
                TagsScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }
            
            // 重置隐藏定时器
            if (!_isWheelScrolling)
            {
                StartScrollBarHideTimer();
            }
        }

        private void MainGridSplitter_MouseEnter(object sender, MouseEventArgs e)
        {
            ShowGridSplitter();
            ResetSplitterHideTimer();
        }

        private void MainGridSplitter_MouseLeave(object sender, MouseEventArgs e)
        {
            ResetSplitterHideTimer();
        }

        private void MainGridSplitter_MouseMove(object sender, MouseEventArgs e)
        {
            ShowGridSplitter();
            ResetSplitterHideTimer();
        }

        private void LeftPanel_MouseMove(object sender, MouseEventArgs e)
        {
            // 检查鼠标是否接近分割器（左侧边缘20像素内）
            var position = e.GetPosition(this);
            var leftPanel = sender as FrameworkElement;
            if (leftPanel != null)
            {
                var leftPanelBounds = new Rect(leftPanel.TranslatePoint(new System.Windows.Point(0, 0), this), 
                    new System.Windows.Size(leftPanel.ActualWidth, leftPanel.ActualHeight));
                
                // 如果鼠标在左侧面板的右边缘20像素内，显示分割器
                if (position.X >= leftPanelBounds.Right - 20 && position.X <= leftPanelBounds.Right + 5)
                {
                    ShowGridSplitter();
                    ResetSplitterHideTimer();
                }
                else if (position.X < leftPanelBounds.Right - 20)
                {
                    // 鼠标远离分割器，延迟隐藏
                    ResetSplitterHideTimer();
                }
            }
        }

        private void ResetSplitterHideTimer()
        {
            if (_splitterHideTimer == null)
            {
                _splitterHideTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                _splitterHideTimer.Tick += (s, args) =>
                {
                    _splitterHideTimer.Stop();
                    // 检查鼠标是否还在分割器附近（左侧面板右边缘20像素内或分割器上）
                    var position = Mouse.GetPosition(this);
                    var leftPanel = this.FindName("LeftPanel") as FrameworkElement;
                    if (leftPanel != null)
                    {
                        var leftPanelBounds = new Rect(leftPanel.TranslatePoint(new System.Windows.Point(0, 0), this), 
                            new System.Windows.Size(leftPanel.ActualWidth, leftPanel.ActualHeight));
                        
                        // 如果鼠标不在分割器附近（左侧面板右边缘20像素内或分割器上），隐藏
                        if (position.X < leftPanelBounds.Right - 20 || position.X > leftPanelBounds.Right + 5)
                        {
                            // 检查是否在分割器上
                            var hitTestResult = VisualTreeHelper.HitTest(this, position);
                            if (hitTestResult != null)
                            {
                                var element = hitTestResult.VisualHit;
                                bool isOnSplitter = false;
                                while (element != null)
                                {
                                    if (element == MainGridSplitter || 
                                        (element is FrameworkElement fe && fe.Name == "MainGridSplitter"))
                                    {
                                        isOnSplitter = true;
                                        break;
                                    }
                                    element = VisualTreeHelper.GetParent(element);
                                }
                                if (!isOnSplitter)
                                {
                                    HideGridSplitter();
                                }
                            }
                            else
                            {
                                HideGridSplitter();
                            }
                        }
                    }
                    else
                    {
                        HideGridSplitter();
                    }
                };
            }
            _splitterHideTimer.Stop();
            _splitterHideTimer.Start();
        }

        private void ShowGridSplitter()
        {
            if (MainGridSplitter != null)
            {
                MainGridSplitter.Opacity = 1.0;
            }
        }

        private void HideGridSplitter()
        {
            if (MainGridSplitter != null)
            {
                MainGridSplitter.Opacity = 0.0;
            }
        }
    }
}

