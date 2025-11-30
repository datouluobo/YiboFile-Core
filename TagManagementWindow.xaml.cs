using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OoiMRR.Services;
using TagTrain.Services;

namespace OoiMRR
{
    public partial class TagManagementWindow : Window
    {
        private List<TagInfo> _tags;
        private CancellationTokenSource _trainingCancellation = null;

        public TagManagementWindow()
        {
            InitializeComponent();
            LoadTags();
            LoadStatistics();
            CheckModelStatus();
            this.KeyDown += TagManagementWindow_KeyDown;
        }

        private void TagManagementWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                Close();
            }
        }

        private void LoadTags()
        {
            try
            {
                _tags = OoiMRRIntegration.GetAllTags(OoiMRR.Services.OoiMRRIntegration.TagSortMode.Name);
                TagsListBox.ItemsSource = _tags;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadStatistics()
        {
            try
            {
                var stats = OoiMRRIntegration.GetStatistics();
                TotalTagsText.Text = $"总标签数: {_tags?.Count ?? 0}";
                TotalSamplesText.Text = $"总样本数: {stats.TotalSamples}";
                ManualSamplesText.Text = $"手动标注: {stats.ManualSamples}";
                UniqueImagesText.Text = $"唯一图片: {stats.UniqueImages}";
            }
            catch{
                            }
        }

        private void CheckModelStatus()
        {
            try
            {
                var modelExists = OoiMRRIntegration.ModelExists();
                if (modelExists)
                {
                    ModelStatusText.Text = "✓ 模型已加载";
                    ModelStatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
                else
                {
                    ModelStatusText.Text = "✗ 模型未训练";
                    ModelStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                }
            }
            catch (Exception ex)
            {
                ModelStatusText.Text = $"检查模型状态失败: {ex.Message}";
                ModelStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void TagsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 可以在这里添加标签选择改变的处理
        }

        private void NewTag_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new PathInputDialog("请输入标签名称:");
            if (dialog.ShowDialog() == true)
            {
                var name = dialog.InputText.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("标签名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var tagId = OoiMRRIntegration.GetOrCreateTagId(name);
                    if (tagId > 0)
                    {
                        LoadTags();
                        LoadStatistics();
                        // 选中新创建的标签
                        var newTag = _tags.FirstOrDefault(t => t.Id == tagId);
                        if (newTag != null)
                        {
                            TagsListBox.SelectedItem = newTag;
                        }
                    }
                    else
                    {
                        MessageBox.Show("创建标签失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"创建标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RenameTag_Click(object sender, RoutedEventArgs e)
        {
            if (TagsListBox.SelectedItem is TagInfo selectedTag)
            {
                var dialog = new PathInputDialog("请输入新的标签名称:");
                dialog.InputText = selectedTag.Name;
                if (dialog.ShowDialog() == true)
                {
                    var newName = dialog.InputText.Trim();
                    if (string.IsNullOrEmpty(newName))
                    {
                        MessageBox.Show("标签名称不能为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    try
                    {
                        if (OoiMRRIntegration.UpdateTagName(selectedTag.Name, newName))
                        {
                            LoadTags();
                            LoadStatistics();
                            // 选中重命名后的标签
                            var renamedTag = _tags.FirstOrDefault(t => t.Name == newName);
                            if (renamedTag != null)
                            {
                                TagsListBox.SelectedItem = renamedTag;
                            }
                        }
                        else
                        {
                            MessageBox.Show("重命名标签失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"重命名标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("请先选择一个标签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteTag_Click(object sender, RoutedEventArgs e)
        {
            if (TagsListBox.SelectedItem is TagInfo selectedTag)
            {
                var result = MessageBox.Show(
                    $"确定要删除标签 \"{selectedTag.Name}\" 吗？\n这将删除标签及其所有训练数据（{selectedTag.Count} 条记录）。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (OoiMRRIntegration.DeleteTag(selectedTag.Id))
                        {
                            LoadTags();
                            LoadStatistics();
                            TagsListBox.SelectedItem = null;
                        }
                        else
                        {
                            MessageBox.Show("删除标签失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"删除标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("请先选择一个标签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ConsolidateTags_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "确定要清理重复标签吗？\n这将合并同名标签，保留所有训练数据。",
                "确认清理",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var consolidateResult = OoiMRRIntegration.ConsolidateDuplicateTags();
                    MessageBox.Show(
                        $"清理完成！\n合并标签组: {consolidateResult.MergedGroups}\n更新样本数: {consolidateResult.UpdatedSamples}\n删除重复标签: {consolidateResult.DeletedTagIds}",
                        "完成",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    LoadTags();
                    LoadStatistics();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清理重复标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

            RetrainModelBtn.IsEnabled = false;

            // 在后台线程中执行训练，避免UI卡死
            Task.Run(() =>
            {
                try
                {
                    // 注意：OoiMRRIntegration.TriggerIncrementalTraining 需要支持进度和取消
                    // 暂时使用现有接口，后续可以扩展
                    var trainingResult = OoiMRRIntegration.TriggerIncrementalTraining(forceRetrain: true);

                    // 回到UI线程更新界面
                    Dispatcher.Invoke(() =>
                    {
                        // 隐藏进度条
                        TrainingProgressGrid.Visibility = Visibility.Collapsed;

                        if (trainingResult.Success)
                        {
                            MessageBox.Show(
                                $"模型训练完成！\n使用样本数: {trainingResult.SampleCount}",
                                "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                            CheckModelStatus();
                        }
                        else
                        {
                            MessageBox.Show(
                                $"模型训练失败: {trainingResult.Message}",
                                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        RetrainModelBtn.IsEnabled = true;
                        _trainingCancellation = null;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        TrainingProgressGrid.Visibility = Visibility.Collapsed;
                        MessageBox.Show($"训练出错: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        RetrainModelBtn.IsEnabled = true;
                        _trainingCancellation = null;
                    });
                }
            });
        }

        private void TrainingStatus_Click(object sender, RoutedEventArgs e)
        {
            // 打开 TagTrain 的训练状态窗口
            try
            {
                // 需要使用反射或直接引用 TagTrain.UI.TrainingStatusWindow
                // 暂时显示统计信息
                var stats = OoiMRRIntegration.GetStatistics();
                var modelExists = OoiMRRIntegration.ModelExists();
                var modelPath = OoiMRRIntegration.GetModelPath();

                var message = $"模型状态: {(modelExists ? "已加载" : "未训练")}\n" +
                              $"模型路径: {modelPath}\n\n" +
                              $"训练统计:\n" +
                              $"总样本数: {stats.TotalSamples}\n" +
                              $"手动标注: {stats.ManualSamples}\n" +
                              $"唯一图片: {stats.UniqueImages}\n" +
                              $"唯一标签: {stats.UniqueTags}";

                MessageBox.Show(message, "训练情况", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取训练情况失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateTrainingProgress(TrainingProgress progress)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TrainingStageText.Text = progress.Stage;
                TrainingProgressBar.Value = progress.Progress;
                TrainingProgressText.Text = $"{progress.Progress}% - {progress.Message}";
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
