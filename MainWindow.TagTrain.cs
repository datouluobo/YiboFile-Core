using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OoiMRR.Services;
using OoiMRR.Services.Navigation;

namespace OoiMRR
{
    /// <summary>
    /// MainWindow 的 TagTrain 集成功能
    /// </summary>
    public partial class MainWindow
    {
        #region Tag 相关字段

        // 这些字段已在 MainWindow.xaml.cs 中定义，这里只是注释说明
        // internal Tag _currentTagFilter = null;
        // internal bool _isUpdatingTagSelection = false;
        // private Services.Tag.TagUIHandler _tagUIHandler;
        // internal Services.TagTrain.TagTrainEventHandler _tagTrainEventHandler;
        // internal enum TagClickMode { Browse, Edit }
        // internal TagClickMode _tagClickMode = TagClickMode.Browse;
        // internal CancellationTokenSource _tagTrainTrainingCancellation = null;
        // internal bool _tagTrainIsTraining = false;

        #endregion

        #region Tag 列表和UI管理

        /// <summary>
        /// 加载标签列表
        /// </summary>
        internal void LoadTags()
        {
            // TagsListBox已移除，标签加载现在由TagTrain面板处理
            // 调用TagTrain面板的初始化
            InitializeTagTrainPanel();
        }

        /// <summary>
        /// 初始化TagTrain训练面板
        /// </summary>
        private void InitializeTagTrainPanel()
        {
            if (!App.IsTagTrainAvailable)
            {
                return;
            }

            try
            {
                // 初始化浏览模式的TagPanel
                if (TagBrowsePanel != null)
                {
                    TagBrowsePanel.Mode = TagTrain.UI.TagPanel.DisplayMode.Browse;
                    TagBrowsePanel.CategoryManagementRequested += TagBrowsePanel_CategoryManagementRequested;
                    TagBrowsePanel.LoadExistingTags();
                }

                // 初始化编辑模式的TagPanel
                if (TagEditPanel != null)
                {
                    TagEditPanel.Mode = TagTrain.UI.TagPanel.DisplayMode.Edit;
                    TagEditPanel.TagClicked += TagEditPanel_TagClicked;
                    TagEditPanel.CategoryManagementRequested += TagEditPanel_CategoryManagementRequested;
                    TagEditPanel.ConfirmTagRequested += TagTrainConfirmTag_Click;
                    TagEditPanel.ConfirmAIPredictionRequested += TagTrainConfirmAIPrediction_Click;
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 更新TagTrain模型状态（已废弃）
        /// </summary>
        internal void UpdateTagTrainModelStatus()
        {
            // 模型状态现在由TagPanel内部管理，此方法已废弃
            return;
        }

        /// <summary>
        /// 加载TagTrain已有标签列表
        /// </summary>
        internal void LoadTagTrainExistingTags()
        {
            // 刷新左侧浏览面板（显示标签计数）
            TagBrowsePanel?.LoadExistingTags();
            // 刷新右侧编辑面板
            TagEditPanel?.LoadExistingTags();
        }

        #endregion

        #region Tag 事件处理

        /// <summary>
        /// 浏览模式：标签点击事件 - 打开标签对应的文件
        /// </summary>
        private void TagBrowsePanel_TagClicked(string tagName, bool forceNewTab)
        {
            try
            {
                // 通过标签名称获取标签ID，确保能正确识别已存在的标签页
                int tagId = OoiMRRIntegration.GetOrCreateTagId(tagName);
                if (tagId > 0)
                {
                    var tag = new Tag { Id = tagId, Name = tagName };
                    var clickType = forceNewTab ? NavigationCoordinator.ClickType.MiddleClick : NavigationCoordinator.ClickType.LeftClick;
                    _navigationCoordinator.HandleTagNavigation(tag, clickType);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 浏览模式：打开分组管理
        /// </summary>
        private void TagBrowsePanel_CategoryManagementRequested()
        {
            _tagTrainEventHandler?.OpenCategoryManagement();
        }

        /// <summary>
        /// 编辑模式：标签点击事件 - 应用标签到当前选中的图片
        /// </summary>
        private void TagEditPanel_TagClicked(string tagName, bool forceNewTab)
        {
            if (!App.IsTagTrainAvailable) return;
            if (string.IsNullOrWhiteSpace(tagName)) return;

            try
            {
                // 获取当前选中的文件
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    MessageBox.Show("请先选择要添加标签的图片文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 只处理图片文件
                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
                var imageFiles = selectedItems.Where(item =>
                    !item.IsDirectory &&
                    imageExtensions.Contains(System.IO.Path.GetExtension(item.Path).ToLowerInvariant())
                ).ToList();

                if (imageFiles.Count == 0)
                {
                    MessageBox.Show("所选文件中没有图片文件。\n\n只有图片文件（jpg, png, bmp, gif, webp）可以添加标签。",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 获取或创建标签ID
                var tagId = OoiMRRIntegration.GetOrCreateTagId(tagName);
                if (tagId <= 0)
                {
                    MessageBox.Show($"创建标签 \"{tagName}\" 失败。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 添加标签到所有选中的图片
                foreach (var file in imageFiles)
                {
                    OoiMRRIntegration.AddTagToFile(file.Path, tagId);
                }

                // 刷新标签列表
                LoadTagTrainExistingTags();

                MessageBox.Show($"成功为 {imageFiles.Count} 个文件添加标签 \"{tagName}\"。",
                    "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 编辑模式：打开分组管理
        /// </summary>
        private void TagEditPanel_CategoryManagementRequested()
        {
            _tagTrainEventHandler?.OpenCategoryManagement();
        }

        private void TagsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _tagUIHandler?.TagsListBox_SelectionChanged(sender, e);
        }

        private void TagBrowseSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_tagClickMode == TagClickMode.Browse)
            {
                TagBrowsePanel?.LoadExistingTags();
            }
        }

        #endregion

        #region Tag UI 创建和更新

        /// <summary>
        /// 创建浏览模式的标签边框（与TagTrain样式一致）
        /// </summary>
        private Border CreateBrowseModeTagBorder(OoiMRR.Services.TagInfo tagInfo, int count)
        {
            var tagName = tagInfo.Name ?? $"标签{tagInfo.Id}";

            var border = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.LightBlue,
                BorderThickness = new Thickness(1),
                Background = System.Windows.Media.Brushes.AliceBlue,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 3, 6, 3),
                Margin = new Thickness(0, 0, 8, 5),
                Cursor = Cursors.Hand,
                Tag = tagInfo.Id,
                Focusable = false,
                IsHitTestVisible = true
            };

            border.MouseEnter += (s, e) =>
            {
                border.Background = System.Windows.Media.Brushes.LightSkyBlue;
                border.BorderBrush = System.Windows.Media.Brushes.DodgerBlue;
            };

            border.MouseLeave += (s, e) =>
            {
                if (_currentTagFilter != null && _currentTagFilter.Id == tagInfo.Id)
                {
                    border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
                    border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
                }
                else
                {
                    border.Background = System.Windows.Media.Brushes.AliceBlue;
                    border.BorderBrush = System.Windows.Media.Brushes.LightBlue;
                }
            };

            if (_currentTagFilter != null && _currentTagFilter.Id == tagInfo.Id)
            {
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
                border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
            }

            border.MouseLeftButtonDown += (s, e) =>
            {
                var tag = new Tag { Id = tagInfo.Id, Name = tagName };
                OpenTagInTab(tag);
            };

            // 右键菜单
            border.ContextMenu = CreateTagContextMenu(tagInfo, tagName);

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
                VerticalAlignment = VerticalAlignment.Center
            });

            if (count > 0)
            {
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"({count})",
                    Foreground = System.Windows.Media.Brushes.DarkGray,
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            border.Child = stackPanel;
            return border;
        }

        /// <summary>
        /// 创建标签右键菜单
        /// </summary>
        private ContextMenu CreateTagContextMenu(OoiMRR.Services.TagInfo tagInfo, string tagName)
        {
            var contextMenu = new ContextMenu();

            // 修改标签名称
            var editMenuItem = new MenuItem
            {
                Header = "✏️ 修改标签名称",
                Tag = new { TagId = tagInfo.Id, TagName = tagName }
            };
            editMenuItem.Click += (s, e) => EditTagName(tagInfo.Id, tagName);

            // 分配到分组
            var assignToCategoryMenuItem = new MenuItem { Header = "📁 分配到分组" };
            PopulateTagCategoryMenu(assignToCategoryMenuItem, tagInfo.Id, tagName);

            // 删除标签
            var deleteMenuItem = new MenuItem
            {
                Header = "🗑️ 删除标签",
                Tag = new { TagId = tagInfo.Id, TagName = tagName }
            };
            deleteMenuItem.Click += (s, e) => DeleteTagById(tagInfo.Id, tagName);

            contextMenu.Items.Add(editMenuItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(assignToCategoryMenuItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(deleteMenuItem);

            return contextMenu;
        }

        /// <summary>
        /// 填充标签分组菜单
        /// </summary>
        private void PopulateTagCategoryMenu(MenuItem assignToCategoryMenuItem, int tagId, string tagName)
        {
            try
            {
                var categories = TagTrain.Services.DataManager.GetAllCategories();
                var currentCategories = TagTrain.Services.DataManager.GetTagCategories(tagId);

                if (categories.Count > 0)
                {
                    foreach (var category in categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name))
                    {
                        var categoryMenuItem = new MenuItem
                        {
                            Header = category.Name,
                            Tag = new { TagId = tagId, CategoryId = category.Id, TagName = tagName },
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
                                    var tid = (int)tagIdProp.GetValue(menuItem.Tag);
                                    var categoryId = (int)categoryIdProp.GetValue(menuItem.Tag);

                                    try
                                    {
                                        if (menuItem.IsChecked)
                                            TagTrain.Services.DataManager.AssignTagToCategory(tid, categoryId);
                                        else
                                            TagTrain.Services.DataManager.RemoveTagFromCategory(tid, categoryId);

                                        TagBrowsePanel?.LoadExistingTags();
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
                    var manageCategoryMenuItem = new MenuItem { Header = "管理分组..." };
                    manageCategoryMenuItem.Click += (s, e) =>
                    {
                        _tagTrainEventHandler?.OpenCategoryManagement();
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
                        _tagTrainEventHandler?.OpenCategoryManagement();
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

        #endregion

        #region Tag 文件和UI更新

        /// <summary>
        /// 更新标签文件UI
        /// </summary>
        internal void UpdateTagFilesUI(Tag tag, List<FileSystemItem> tagFiles)
        {
            try
            {
                _currentFiles = tagFiles;
                if (FileBrowser != null)
                {
                    FileBrowser.FilesItemsSource = null;
                    FileBrowser.FilesItemsSource = _currentFiles;
                }

                if (FileBrowser != null)
                {
                    FileBrowser.AddressText = "";
                    FileBrowser.IsAddressReadOnly = false;
                    FileBrowser.SetTagBreadcrumb(tag.Name);
                }

                if (tagFiles.Count == 0)
                {
                    ShowEmptyStateMessage("该标签下没有文件。\\n\\n提示：只有图片文件（jpg, png, bmp, gif, webp）可以添加标签。");
                }
                else
                {
                    HideEmptyStateMessage();
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 更新标签页面文件UI
        /// </summary>
        private void UpdateTagPageFilesUI(string path, List<FileSystemItem> files)
        {
            try
            {
                _currentFiles = files;
                if (FileBrowser != null)
                {
                    FileBrowser.FilesItemsSource = null;
                    FileBrowser.FilesItemsSource = _currentFiles;
                }

                if (FileBrowser != null)
                {
                    FileBrowser.AddressText = path;
                    FileBrowser.IsAddressReadOnly = false;
                    FileBrowser.UpdateBreadcrumb(path);
                }

                HideEmptyStateMessage();
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 高亮活动标签
        /// </summary>
        private void HighlightActiveTagChip(int tagId)
        {
            try
            {
                if (TagEditPanel?.ExistingTagsPanel == null) return;

                foreach (var child in TagEditPanel.ExistingTagsPanel.Children)
                {
                    if (child is Border border)
                    {
                        bool isMatch = border.Tag is int bid && bid == tagId;
                        if (isMatch)
                        {
                            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7));
                            border.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0));
                        }
                        else
                        {
                            border.Background = System.Windows.Media.Brushes.AliceBlue;
                            border.BorderBrush = System.Windows.Media.Brushes.LightBlue;
                        }

                        if (border.Child is StackPanel sp && sp.Children.Count > 0 && sp.Children[0] is TextBlock tb)
                        {
                            if (isMatch)
                            {
                                tb.FontWeight = FontWeights.SemiBold;
                                var fg = this.FindResource("HighlightForegroundBrush") as SolidColorBrush;
                                tb.Foreground = fg ?? System.Windows.Media.Brushes.Black;
                            }
                            else
                            {
                                tb.FontWeight = FontWeights.Bold;
                                tb.Foreground = System.Windows.Media.Brushes.Black;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Tag 操作方法

        /// <summary>
        /// 按标签过滤
        /// </summary>
        internal void FilterByTag(Tag tag)
        {
            _tagUIHandler?.FilterByTag(tag);
        }

        private void NewTag_Click(object sender, RoutedEventArgs e)
        {
            _tagUIHandler?.NewTag_Click(sender, e);
        }

        private void ManageTags_Click(object sender, RoutedEventArgs e)
        {
            _tagUIHandler?.ManageTags_Click(sender, e);
        }

        private void AddTagToFile_Click(object sender, RoutedEventArgs e)
        {
            _tagUIHandler?.AddTagToFile_Click(sender, e);
        }

        private void OpenTagDialogForSelectedItems()
        {
            _tagUIHandler?.OpenTagDialogForSelectedItems();
        }

        // Commented out: These methods don't exist in TagUIHandler
        // internal void BatchAddTags_Click(object sender, RoutedEventArgs e)
        // {
        //     _tagUIHandler?.BatchAddTags_Click(sender, e);
        // }

        // internal void TagStatistics_Click(object sender, RoutedEventArgs e)
        // {
        //     _tagUIHandler?.TagStatistics_Click(sender, e);
        // }

        #endregion

        #region Tag 编辑和删除

        /// <summary>
        /// 编辑标签名称
        /// </summary>
        private void EditTagName(int tagId, string oldTagName)
        {
            try
            {
                if (string.IsNullOrEmpty(oldTagName))
                {
                    oldTagName = TagTrain.Services.DataManager.GetTagName(tagId);
                    if (string.IsNullOrEmpty(oldTagName))
                    {
                        MessageBox.Show("无法获取标签名称", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

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

                var textBlock = new TextBlock
                {
                    Text = "请输入新的标签名称：",
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
                    if (newTagName == oldTagName) return;

                    try
                    {
                        bool success = TagTrain.Services.DataManager.UpdateTagName(oldTagName, newTagName);

                        if (success)
                        {
                            if (_tagClickMode == TagClickMode.Browse)
                                TagBrowsePanel?.LoadExistingTags();
                            else
                                LoadTagTrainExistingTags();

                            MessageBox.Show($"标签名称已从 \"{oldTagName}\" 修改为 \"{newTagName}\"。\\n所有训练数据已保留。",
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
            catch (Exception ex)
            {
                MessageBox.Show($"打开修改对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 删除标签（根据ID）
        /// </summary>
        private void DeleteTagById(int tagId, string tagName)
        {
            try
            {
                if (string.IsNullOrEmpty(tagName))
                {
                    tagName = TagTrain.Services.DataManager.GetTagName(tagId);
                    if (string.IsNullOrEmpty(tagName))
                        tagName = $"标签{tagId}";
                }

                var result = MessageBox.Show(
                    $"确定要删除标签 \"{tagName}\" 吗？\\n这将删除所有使用该标签的训练数据。",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        TagTrain.Services.DataManager.DeleteTag(tagId);

                        if (_tagClickMode == TagClickMode.Browse)
                            TagBrowsePanel?.LoadExistingTags();
                        else
                            LoadTagTrainExistingTags();

                        MessageBox.Show($"标签 \"{tagName}\" 已删除。", "删除成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除标签时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region TagTrain 训练相关

        private void TagClickModeBtn_Click(object sender, RoutedEventArgs e)
            => _tagTrainEventHandler?.TagClickModeBtn_Click(sender, e);

        private void TagCategoryManageBtn_Click(object sender, RoutedEventArgs e)
            => _tagTrainEventHandler?.TagCategoryManageBtn_Click(sender, e);

        private void TagBrowseCategoryManagement_Click(object sender, RoutedEventArgs e)
            => _tagTrainEventHandler?.TagBrowseCategoryManagement_Click(sender, e);

        private void ApplyTagClickModeVisibility()
        {
            // 这些按钮现在由TagPanel内部管理，此方法已废弃
        }

        private void TagTrainTagSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void TagTrainTagInputTextBox_TextChanged(object sender, TextChangedEventArgs e) { }
        private void TagTrainTagInputTextBox_KeyDown(object sender, KeyEventArgs e) { }
        private void TagTrainTagInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e) { }
        private void TagTrainTagInputTextBox_GotFocus(object sender, RoutedEventArgs e) { }
        private void TagTrainTagInputTextBox_LostFocus(object sender, RoutedEventArgs e) { }
        private void TagTrainTagAutocompleteListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) { }
        private void TagTrainTagAutocompleteListBox_KeyDown(object sender, KeyEventArgs e) { }

        /// <summary>
        /// 确认打标签
        /// </summary>
        private void TagTrainConfirmTag_Click()
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var selectedBefore = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().Select(i => i.Path).ToList() ?? new List<string>();
                var text = TagEditPanel?.TagInputTextBox?.Text ?? "";
                var tagNames = (text ?? "")
                    .Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (tagNames.Count == 0)
                {
                    MessageBox.Show("请输入至少一个标签名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    MessageBox.Show("请先选择要打标签的图片文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
                foreach (var name in tagNames)
                {
                    var tagId = OoiMRRIntegration.GetOrCreateTagId(name);
                    if (tagId <= 0) continue;

                    foreach (var it in selectedItems)
                    {
                        if (!it.IsDirectory && imageExtensions.Contains(System.IO.Path.GetExtension(it.Path).ToLowerInvariant()))
                        {
                            OoiMRRIntegration.AddTagToFile(it.Path, tagId);
                        }
                    }
                }

                LoadTagTrainExistingTags();
                if (_currentTagFilter != null)
                {
                    FilterByTag(_currentTagFilter);
                    // RestoreSelectionByPaths(selectedBefore); // Method doesn't exist
                }
                else
                {
                    LoadFiles();
                    // RestoreSelectionByPaths(selectedBefore); // Method doesn't exist
                }

                if (TagEditPanel?.TagInputTextBox != null)
                    TagEditPanel.TagInputTextBox.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"确认标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 确认AI预测
        /// </summary>
        private void TagTrainConfirmAIPrediction_Click()
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var selectedBefore = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().Select(i => i.Path).ToList() ?? new List<string>();
                var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
                if (selectedItems.Count == 0)
                {
                    MessageBox.Show("请先选择图片后再确认AI预测。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
                foreach (var it in selectedItems)
                {
                    if (it.IsDirectory) continue;
                    if (!imageExtensions.Contains(System.IO.Path.GetExtension(it.Path).ToLowerInvariant())) continue;

                    var predictions = OoiMRRIntegration.PredictTagsForImage(it.Path) ?? new List<TagTrain.Services.TagPredictionResult>();
                    foreach (var p in predictions
                                 .OrderByDescending(x => x.Confidence)
                                 .Take(3)
                                 .Where(x => x.Confidence >= 0.5f))
                    {
                        OoiMRRIntegration.AddTagToFile(it.Path, p.TagId);
                    }
                }

                LoadTagTrainExistingTags();
                if (_currentTagFilter != null)
                {
                    FilterByTag(_currentTagFilter);
                    // RestoreSelectionByPaths(selectedBefore); // Method doesn't exist
                }
                else
                {
                    LoadFiles();
                    // RestoreSelectionByPaths(selectedBefore); // Method doesn't exist
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"确认AI预测失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 计算希望的标签项宽度（已废弃）
        /// </summary>
        private double GetDesiredTagItemWidth()
        {
            try
            {
                int perRow = TagTrain.Services.SettingsManager.GetTagsPerRow();
                if (perRow <= 0) perRow = 5;

                double containerWidth = 300;
                double gap = 8;
                double totalGap = gap * perRow;
                double safe = 1;
                double width = Math.Floor((containerWidth - totalGap - safe) / perRow);
                return Math.Max(120, width);
            }
            catch
            {
                return 150;
            }
        }

        /// <summary>
        /// 更新标签布局（已废弃）
        /// </summary>
        private void UpdateTagTrainExistingTagsLayout()
        {
            // 标签布局现在由TagPanel内部管理，此方法已废弃
        }

        /// <summary>
        /// 面板尺寸变化时自适应（已废弃）
        /// </summary>
        private void TagTrainExistingTagsPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 已废弃
        }

        /// <summary>
        /// 根据标签名称生成颜色
        /// </summary>
        private string GenerateTagColor(string tagName)
        {
            if (string.IsNullOrEmpty(tagName))
                return "#FF0000";

            int hash = tagName.GetHashCode();
            if (hash < 0) hash = -hash;

            int hue = hash % 360;
            var color = HslToRgb(hue, 0.7, 0.5);
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        /// <summary>
        /// HSL 转 RGB
        /// </summary>
        private System.Drawing.Color HslToRgb(int h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c / 2;

            double r = 0, g = 0, b = 0;
            if (h >= 0 && h < 60)
            {
                r = c; g = x; b = 0;
            }
            else if (h >= 60 && h < 120)
            {
                r = x; g = c; b = 0;
            }
            else if (h >= 120 && h < 180)
            {
                r = 0; g = c; b = x;
            }
            else if (h >= 180 && h < 240)
            {
                r = 0; g = x; b = c;
            }
            else if (h >= 240 && h < 300)
            {
                r = x; g = 0; b = c;
            }
            else if (h >= 300 && h < 360)
            {
                r = c; g = 0; b = x;
            }

            return System.Drawing.Color.FromArgb(
                (int)((r + m) * 255),
                (int)((g + m) * 255),
                (int)((b + m) * 255));
        }

        #endregion
    }
}
