using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TagTrain.UI;
using TagTrain.Services;
using OoiMRR.Controls;
using OoiMRR.Services;
using OoiMRR;
using TagType = OoiMRR.Tag;

namespace OoiMRR.Services.TagTrain
{
    /// <summary>
    /// TagTrain 事件处理器
    /// 处理所有 TagTrain 相关的事件，包括标签管理、训练、配置等
    /// </summary>
    public class TagTrainEventHandler
    {
        private readonly TagPanel _tagBrowsePanel;
        private readonly TagPanel _tagEditPanel;
        private readonly FileBrowserControl _fileBrowser;
        private readonly Window _ownerWindow;
        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        // 回调函数
        private readonly Func<TagClickMode> _getTagClickMode;
        private readonly Action<TagClickMode> _setTagClickMode;
        private readonly Func<bool> _getTagTrainIsTraining;
        private readonly Action<bool> _setTagTrainIsTraining;
        private readonly Func<CancellationTokenSource> _getTagTrainTrainingCancellation;
        private readonly Action<CancellationTokenSource> _setTagTrainTrainingCancellation;
        private readonly Func<TagType> _getCurrentTagFilter;
        private readonly Action _loadTagTrainExistingTags;
        private readonly Action _updateTagTrainModelStatus;
        private readonly Action _loadFiles;
        private readonly Action<TagType> _filterByTag;
        private readonly Action<List<string>> _restoreSelectionByPaths;

        // 标签点击模式
        public enum TagClickMode { Browse, Edit }
        public TagClickMode CurrentMode => _getTagClickMode();

        public TagTrainEventHandler(
            TagPanel tagBrowsePanel,
            TagPanel tagEditPanel,
            FileBrowserControl fileBrowser,
            Window ownerWindow,
            System.Windows.Threading.Dispatcher dispatcher,
            Func<TagClickMode> getTagClickMode,
            Action<TagClickMode> setTagClickMode,
            Func<bool> getTagTrainIsTraining,
            Action<bool> setTagTrainIsTraining,
            Func<CancellationTokenSource> getTagTrainTrainingCancellation,
            Action<CancellationTokenSource> setTagTrainTrainingCancellation,
            Func<TagType> getCurrentTagFilter,
            Action loadTagTrainExistingTags,
            Action updateTagTrainModelStatus,
            Action loadFiles,
            Action<TagType> filterByTag,
            Action<List<string>> restoreSelectionByPaths)
        {
            _tagBrowsePanel = tagBrowsePanel;
            _tagEditPanel = tagEditPanel;
            _fileBrowser = fileBrowser;
            _ownerWindow = ownerWindow;
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _getTagClickMode = getTagClickMode ?? throw new ArgumentNullException(nameof(getTagClickMode));
            _setTagClickMode = setTagClickMode ?? throw new ArgumentNullException(nameof(setTagClickMode));
            _getTagTrainIsTraining = getTagTrainIsTraining ?? throw new ArgumentNullException(nameof(getTagTrainIsTraining));
            _setTagTrainIsTraining = setTagTrainIsTraining ?? throw new ArgumentNullException(nameof(setTagTrainIsTraining));
            _getTagTrainTrainingCancellation = getTagTrainTrainingCancellation ?? throw new ArgumentNullException(nameof(getTagTrainTrainingCancellation));
            _setTagTrainTrainingCancellation = setTagTrainTrainingCancellation ?? throw new ArgumentNullException(nameof(setTagTrainTrainingCancellation));
            _getCurrentTagFilter = getCurrentTagFilter ?? throw new ArgumentNullException(nameof(getCurrentTagFilter));
            _loadTagTrainExistingTags = loadTagTrainExistingTags ?? throw new ArgumentNullException(nameof(loadTagTrainExistingTags));
            _updateTagTrainModelStatus = updateTagTrainModelStatus ?? throw new ArgumentNullException(nameof(updateTagTrainModelStatus));
            _loadFiles = loadFiles ?? throw new ArgumentNullException(nameof(loadFiles));
            _filterByTag = filterByTag ?? throw new ArgumentNullException(nameof(filterByTag));
            _restoreSelectionByPaths = restoreSelectionByPaths ?? throw new ArgumentNullException(nameof(restoreSelectionByPaths));
        }

        // ========== TagTrain 训练面板事件处理方法 ==========

        public void TagTrainTagSortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 重新加载标签列表（应用新的排序方式）
            _loadTagTrainExistingTags();
        }

        public void TagTrainTagInputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO: 实现标签输入自动补完功能
        }

        public void TagTrainTagInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            // TODO: 实现标签输入键盘导航功能
        }

        public void TagClickModeBtn_Click(object sender, RoutedEventArgs e)
        {
            var currentMode = _getTagClickMode();
            var newMode = currentMode == TagClickMode.Browse ? TagClickMode.Edit : TagClickMode.Browse;
            _setTagClickMode(newMode);

            try
            {
                if (sender is Button btn)
                {
                    btn.Content = newMode == TagClickMode.Browse ? "👁" : "✏️";
                    btn.ToolTip = newMode == TagClickMode.Browse
                        ? "切换到编辑模式：显示完整TagTrain训练面板"
                        : "切换到浏览模式：只显示标签列表";
                }

                // 切换浏览/编辑模式的显示
                SwitchTagMode();

                // 根据模式调整相关按钮显示/隐藏
                ApplyTagClickModeVisibility();
            }
            catch (Exception ex)
            {
            }
        }

        // 切换标签浏览/编辑模式
        private void SwitchTagMode()
        {
            try
            {
                if (_tagBrowsePanel != null && _tagEditPanel != null)
                {
                    var mode = _getTagClickMode();
                    if (mode == TagClickMode.Browse)
                    {
                        _tagBrowsePanel.Visibility = Visibility.Visible;
                        _tagEditPanel.Visibility = Visibility.Collapsed;
                        // 加载浏览模式的标签列表
                        if (_tagBrowsePanel.Mode != TagPanel.DisplayMode.Browse)
                        {
                            _tagBrowsePanel.Mode = TagPanel.DisplayMode.Browse;
                        }
                        _tagBrowsePanel.LoadExistingTags();
                    }
                    else
                    {
                        _tagBrowsePanel.Visibility = Visibility.Collapsed;
                        _tagEditPanel.Visibility = Visibility.Visible;
                        // 加载编辑模式的标签列表
                        if (_tagEditPanel.Mode != TagPanel.DisplayMode.Edit)
                        {
                            _tagEditPanel.Mode = TagPanel.DisplayMode.Edit;
                        }
                        _tagEditPanel.LoadExistingTags();
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        // 分组管理按钮点击
        public void TagCategoryManageBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenCategoryManagement();
        }

        // 浏览模式头部的分组管理按钮
        public void TagBrowseCategoryManagement_Click(object sender, RoutedEventArgs e)
        {
            OpenCategoryManagement();
        }

        // 打开分组管理窗口（统一方法）
        public void OpenCategoryManagement()
        {
            try
            {
                if (!App.IsTagTrainAvailable)
                {
                    MessageBox.Show("TagTrain 不可用，无法打开分组管理。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var window = new CategoryManagementWindow
                {
                    Owner = _ownerWindow
                };
                window.ShowDialog();

                // 刷新标签列表
                var mode = _getTagClickMode();
                if (mode == TagClickMode.Browse && _tagBrowsePanel != null)
                {
                    _tagBrowsePanel.LoadExistingTags();
                }
                else if (_tagEditPanel != null)
                {
                    _tagEditPanel.LoadExistingTags();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开分组管理失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 修改标签名称
        public void EditTagName(int tagId, string oldTagName)
        {
            try
            {
                // 获取标签名称（如果传入的是ID）
                if (string.IsNullOrEmpty(oldTagName))
                {
                    oldTagName = DataManager.GetTagName(tagId);
                    if (string.IsNullOrEmpty(oldTagName))
                    {
                        MessageBox.Show("无法获取标签名称", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // 创建输入对话框
                var inputDialog = new Window
                {
                    Title = "修改标签名称",
                    Width = 400,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = _ownerWindow,
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
                        // 更新标签名称
                        bool success = DataManager.UpdateTagName(oldTagName, newTagName);

                        if (success)
                        {
                            // 刷新标签列表
                            var mode = _getTagClickMode();
                            if (mode == TagClickMode.Browse)
                            {
                                _tagBrowsePanel?.LoadExistingTags();
                            }
                            else
                            {
                                _loadTagTrainExistingTags();
                            }

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
            catch (Exception ex)
            {
                MessageBox.Show($"打开修改对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 删除标签（根据ID）
        public void DeleteTagById(int tagId, string tagName)
        {
            try
            {
                // 获取标签名称（如果传入的是ID）
                if (string.IsNullOrEmpty(tagName))
                {
                    tagName = DataManager.GetTagName(tagId);
                    if (string.IsNullOrEmpty(tagName))
                    {
                        tagName = $"标签{tagId}";
                    }
                }

                var result = MessageBox.Show(
                    $"确定要删除标签 \"{tagName}\" 吗？\n这将删除所有使用该标签的训练数据。",
                    "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        DataManager.DeleteTag(tagId);

                        // 刷新标签列表
                        var mode = _getTagClickMode();
                        if (mode == TagClickMode.Browse)
                        {
                            _tagBrowsePanel?.LoadExistingTags();
                        }
                        else
                        {
                            _loadTagTrainExistingTags();
                        }

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

        // 根据浏览/编辑模式控制某些按钮的显示/隐藏
        private void ApplyTagClickModeVisibility()
        {
            try
            {
                // 设计规则：
                // - 浏览模式：显示"批量操作""训练情况"
                // - 编辑模式：隐藏"批量操作""训练情况"，以免分散注意力
                // 这些按钮现在由TagPanel内部管理，此方法已废弃
            }
            catch { }
        }

        public void TagTrainTagInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // TODO: 实现标签输入预览按键处理
        }

        public void TagTrainTagInputTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // TODO: 实现标签输入框获得焦点时的处理
        }

        public void TagTrainTagInputTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // TODO: 实现标签输入框失去焦点时的处理
        }

        public void TagTrainTagAutocompleteListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // TODO: 实现自动补完列表双击选择功能
        }

        public void TagTrainTagAutocompleteListBox_KeyDown(object sender, KeyEventArgs e)
        {
            // TODO: 实现自动补完列表键盘导航功能
        }

        public void TagTrainConfirmTag_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var selectedBefore = _fileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().Select(i => i.Path).ToList() ?? new List<string>();
                var text = _tagEditPanel?.TagInputTextBox?.Text ?? "";
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

                var selectedItems = _fileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
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

                // 刷新界面
                _loadTagTrainExistingTags();
                var currentTagFilter = _getCurrentTagFilter();
                if (currentTagFilter != null)
                {
                    _filterByTag(currentTagFilter);
                    _restoreSelectionByPaths(selectedBefore);
                }
                else
                {
                    _loadFiles();
                    _restoreSelectionByPaths(selectedBefore);
                }

                if (_tagEditPanel?.TagInputTextBox != null)
                    _tagEditPanel.TagInputTextBox.Text = "";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"确认标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void TagTrainConfirmAIPrediction_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var selectedBefore = _fileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().Select(i => i.Path).ToList() ?? new List<string>();
                var selectedItems = _fileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
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

                    var predictions = OoiMRRIntegration.PredictTagsForImage(it.Path) ?? new List<TagPredictionResult>();
                    // 选取 Top3 且置信度 >= 0.5
                    foreach (var p in predictions
                                 .OrderByDescending(x => x.Confidence)
                                 .Take(3)
                                 .Where(x => x.Confidence >= 0.5f))
                    {
                        OoiMRRIntegration.AddTagToFile(it.Path, p.TagId);
                    }
                }

                _loadTagTrainExistingTags();
                var currentTagFilter = _getCurrentTagFilter();
                if (currentTagFilter != null)
                {
                    _filterByTag(currentTagFilter);
                    _restoreSelectionByPaths(selectedBefore);
                }
                else
                {
                    _loadFiles();
                    _restoreSelectionByPaths(selectedBefore);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"确认AI预测失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void TagTrainSkip_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_tagEditPanel?.TagInputTextBox != null)
                    _tagEditPanel.TagInputTextBox.Text = "";

                // 选中下一个文件项（如果存在）
                if (_fileBrowser?.FilesList != null && _fileBrowser.FilesList.Items.Count > 0)
                {
                    var idx = _fileBrowser.FilesList.SelectedIndex;
                    var next = Math.Min(Math.Max(idx + 1, 0), _fileBrowser.FilesList.Items.Count - 1);
                    if (next != idx)
                    {
                        _fileBrowser.FilesList.SelectedIndex = next;
                        _fileBrowser.FilesList.ScrollIntoView(_fileBrowser.FilesList.SelectedItem);
                    }
                }
            }
            catch { }
        }

        public void TagTrainStartTraining_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            // 切换：正在训练则作为"停止"处理
            if (_getTagTrainIsTraining())
            {
                TagTrainCancelTraining_Click(sender, e);
                return;
            }

            var cancellation = new CancellationTokenSource();
            _setTagTrainTrainingCancellation(cancellation);
            var progress = new Progress<TrainingProgress>(UpdateTagTrainTrainingProgress);
            _setTagTrainIsTraining(true);

            if (_tagEditPanel?.TrainingProgressGrid != null) _tagEditPanel.TrainingProgressGrid.Visibility = Visibility.Visible;
            if (_tagEditPanel?.TrainingProgressBar != null) _tagEditPanel.TrainingProgressBar.Value = 0;
            if (_tagEditPanel?.TrainingStageText != null) _tagEditPanel.TrainingStageText.Text = "";
            if (_tagEditPanel?.TrainingProgressText != null) _tagEditPanel.TrainingProgressText.Text = "";
            if (_tagEditPanel?.StartTrainingBtn != null) _tagEditPanel.StartTrainingBtn.Content = "⏹️ 停止训练";
            if (_tagEditPanel?.RetrainModelBtn != null) _tagEditPanel.RetrainModelBtn.IsEnabled = false;

            Task.Run(() =>
            {
                try
                {
                    var result = OoiMRRIntegration.TriggerIncrementalTraining(false, progress, cancellation.Token);
                    _dispatcher.Invoke(() =>
                    {
                        _setTagTrainIsTraining(false);
                        if (_tagEditPanel?.TrainingProgressGrid != null) _tagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        if (_tagEditPanel?.StartTrainingBtn != null) _tagEditPanel.StartTrainingBtn.Content = "▶️ 开始训练";
                        if (_tagEditPanel?.RetrainModelBtn != null) _tagEditPanel.RetrainModelBtn.IsEnabled = true;

                        if (!(result.Success == false && (result.Message ?? "").Contains("已取消")))
                        {
                            if (result.Success)
                                DialogService.Info("训练完成", "成功", _ownerWindow);
                            else
                                DialogService.Error($"训练失败：{result.Message}", "错误", _ownerWindow);
                        }

                        _updateTagTrainModelStatus();
                    });
                }
                catch (Exception ex)
                {
                    _dispatcher.Invoke(() =>
                    {
                        _setTagTrainIsTraining(false);
                        if (_tagEditPanel?.TrainingProgressGrid != null) _tagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        if (_tagEditPanel?.StartTrainingBtn != null) _tagEditPanel.StartTrainingBtn.Content = "▶️ 开始训练";
                        if (_tagEditPanel?.RetrainModelBtn != null) _tagEditPanel.RetrainModelBtn.IsEnabled = true;
                        DialogService.Error($"训练出错: {ex.Message}", "错误", _ownerWindow);
                        _updateTagTrainModelStatus();
                    });
                }
            });
        }

        public void TagTrainPause_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_getTagTrainIsTraining() && _getTagTrainTrainingCancellation() != null && !_getTagTrainTrainingCancellation().IsCancellationRequested)
                {
                    _getTagTrainTrainingCancellation().Cancel();
                }
            }
            catch { }
        }

        public void TagTrainRetrainModel_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            if (_getTagTrainIsTraining())
            {
                MessageBox.Show("已有训练正在进行，请先取消或等待完成。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var cancellation = new CancellationTokenSource();
            _setTagTrainTrainingCancellation(cancellation);
            var progress = new Progress<TrainingProgress>(UpdateTagTrainTrainingProgress);
            _setTagTrainIsTraining(true);

            if (_tagEditPanel?.TrainingProgressGrid != null) _tagEditPanel.TrainingProgressGrid.Visibility = Visibility.Visible;
            if (_tagEditPanel?.TrainingProgressBar != null) _tagEditPanel.TrainingProgressBar.Value = 0;
            if (_tagEditPanel?.TrainingStageText != null) _tagEditPanel.TrainingStageText.Text = "";
            if (_tagEditPanel?.TrainingProgressText != null) _tagEditPanel.TrainingProgressText.Text = "";
            if (_tagEditPanel?.StartTrainingBtn != null) _tagEditPanel.StartTrainingBtn.IsEnabled = false;
            if (_tagEditPanel?.RetrainModelBtn != null) _tagEditPanel.RetrainModelBtn.IsEnabled = false;

            Task.Run(() =>
            {
                try
                {
                    var result = OoiMRRIntegration.TriggerIncrementalTraining(true, progress, cancellation.Token);
                    _dispatcher.Invoke(() =>
                    {
                        _setTagTrainIsTraining(false);
                        if (_tagEditPanel?.TrainingProgressGrid != null) _tagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        if (_tagEditPanel?.StartTrainingBtn != null) _tagEditPanel.StartTrainingBtn.IsEnabled = true;
                        if (_tagEditPanel?.RetrainModelBtn != null) _tagEditPanel.RetrainModelBtn.IsEnabled = true;
                        if (!(result.Success == false && (result.Message ?? "").Contains("已取消")))
                        {
                            MessageBox.Show(result.Success ? "重新训练完成" : $"重新训练失败：{result.Message}",
                                result.Success ? "成功" : "错误",
                                MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
                        }
                        _updateTagTrainModelStatus();
                    });
                }
                catch (Exception ex)
                {
                    _dispatcher.Invoke(() =>
                    {
                        _setTagTrainIsTraining(false);
                        if (_tagEditPanel?.TrainingProgressGrid != null) _tagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        if (_tagEditPanel?.StartTrainingBtn != null) _tagEditPanel.StartTrainingBtn.IsEnabled = true;
                        if (_tagEditPanel?.RetrainModelBtn != null) _tagEditPanel.RetrainModelBtn.IsEnabled = true;
                        MessageBox.Show($"重新训练出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        _updateTagTrainModelStatus();
                    });
                }
            });
        }

        public void TagTrainCancelTraining_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_getTagTrainIsTraining() && _getTagTrainTrainingCancellation() != null && !_getTagTrainTrainingCancellation().IsCancellationRequested)
                {
                    _getTagTrainTrainingCancellation().Cancel();
                    // 立即重置界面与状态，避免用户感知为"仍在运行"
                    _setTagTrainIsTraining(false);
                    if (_tagEditPanel?.StartTrainingBtn != null) _tagEditPanel.StartTrainingBtn.Content = "▶️ 开始训练";
                    if (_tagEditPanel?.TrainingProgressGrid != null) _tagEditPanel.TrainingProgressGrid.Visibility = Visibility.Collapsed;
                    if (_tagEditPanel?.TrainingProgressBar != null) _tagEditPanel.TrainingProgressBar.Value = 0;
                    if (_tagEditPanel?.TrainingStageText != null) _tagEditPanel.TrainingStageText.Text = "已停止";
                    if (_tagEditPanel?.TrainingProgressText != null) _tagEditPanel.TrainingProgressText.Text = "";
                    // 允许重新开始
                    if (_tagEditPanel?.RetrainModelBtn != null) _tagEditPanel.RetrainModelBtn.IsEnabled = true;
                }
            }
            catch { }
        }

        public void TagTrainBatchOperation_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("批量操作功能暂未实现。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void TagTrainTrainingStatus_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var stats = OoiMRRIntegration.GetStatistics();
                MessageBox.Show(
                    $"训练样本: {stats.TotalSamples}\n手动样本: {stats.ManualSamples}\n唯一图片: {stats.UniqueImages}\n唯一标签: {stats.UniqueTags}",
                    "训练情况", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取训练情况失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void TagTrainConsolidateTags_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable) return;
            try
            {
                var result = OoiMRRIntegration.ConsolidateDuplicateTags();
                MessageBox.Show(
                    $"合并组数: {result.MergedGroups}\n更新样本: {result.UpdatedSamples}\n删除标签: {result.DeletedTagIds}",
                    "清理重复标签", MessageBoxButton.OK, MessageBoxImage.Information);
                _loadTagTrainExistingTags();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"清理重复标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateTagTrainTrainingProgress(TrainingProgress progress)
        {
            try
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_tagEditPanel?.TrainingStageText != null) _tagEditPanel.TrainingStageText.Text = progress?.Stage ?? "";
                    if (_tagEditPanel?.TrainingProgressBar != null) _tagEditPanel.TrainingProgressBar.Value = progress?.Progress ?? 0;
                    if (_tagEditPanel?.TrainingProgressText != null) _tagEditPanel.TrainingProgressText.Text =
                        $"{(progress?.Progress ?? 0)}% - {(progress?.Message ?? "")}";
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
            catch { }
        }

        public void TagTrainConfig_Click(object sender, RoutedEventArgs e)
        {
            // TODO: 实现设置功能（可以打开TagTrain的设置窗口）
            try
            {
                if (App.IsTagTrainAvailable)
                {
                    // 可以尝试打开TagTrain的配置窗口
                    try { SettingsManager.ClearCache(); } catch { }
                    var configWindow = new ConfigWindow();
                    var result = configWindow.ShowDialog();

                    // 保存设置后自动刷新左侧标签，无需重启
                    if (result == true)
                    {
                        // 清理 TagTrain 缓存，确保使用最新设置路径
                        try
                        {
                            SettingsManager.ClearCache();
                            DataManager.ClearDatabasePathCache();

                            // 同步保存到 OoiMRR 自己的配置，便于下次启动前置设置
                            var cfg = ConfigManager.Load();
                            var storageDir = SettingsManager.GetDataStorageDirectory();
                            cfg.TagTrainDataDirectory = storageDir;

                            // 关键：把 DataStorageDirectory 写入默认 settings.txt，
                            // 让下次 LoadSettings 能从默认文件定位到新的目录
                            SettingsManager.SetDataStorageDirectory(storageDir);
                            ConfigManager.Save(cfg);
                        }
                        catch { }

                        // 重新加载状态与标签
                        _updateTagTrainModelStatus();
                        _loadTagTrainExistingTags();
                    }
                }
                else
                {
                    MessageBox.Show("TagTrain 不可用，无法打开设置。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开设置失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}




