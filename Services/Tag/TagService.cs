using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using OoiMRR.Controls;
using TagTrain.UI;

namespace OoiMRR.Services.Tag
{
    /// <summary>
    /// 标签管理服务
    /// 负责标签的加载、创建、管理以及标签筛选功能
    /// </summary>
    public class TagService
    {
        #region 事件定义

        /// <summary>
        /// 标签筛选请求事件
        /// </summary>
        public event EventHandler<TagFilterEventArgs> TagFilterRequested;

        /// <summary>
        /// 标签页创建请求事件
        /// </summary>
        public event EventHandler<OoiMRR.Tag> TagTabRequested;

        /// <summary>
        /// 标签列表已加载事件
        /// </summary>
        public event EventHandler TagsLoaded;

        /// <summary>
        /// 文件列表更新请求事件
        /// </summary>
#pragma warning disable 0067 // 事件暂未使用，保留扩展
        public event EventHandler<List<FileSystemItem>> FilesUpdateRequested;
#pragma warning restore 0067

        #endregion

        #region 私有字段

        private readonly Dispatcher _dispatcher;
        private OoiMRR.Tag _currentTagFilter = null;

        #endregion

        #region 构造函数

        public TagService(Dispatcher dispatcher = null)
        {
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 加载标签（初始化TagTrain面板）
        /// </summary>
        public void LoadTags(TagPanel browsePanel, TagPanel editPanel)
        {
            if (!App.IsTagTrainAvailable)
            {
                System.Diagnostics.Debug.WriteLine("LoadTags: TagTrain 不可用");
                return;
            }

            _dispatcher.Invoke(() =>
            {
                try
                {
                    // 初始化浏览模式的TagPanel
                    if (browsePanel != null)
                    {
                        browsePanel.Mode = TagPanel.DisplayMode.Browse;
                        browsePanel.TagClicked += TagBrowsePanel_TagClicked;
                        browsePanel.CategoryManagementRequested += TagBrowsePanel_CategoryManagementRequested;
                        browsePanel.LoadExistingTags();
                    }

                    // 初始化编辑模式的TagPanel
                    if (editPanel != null)
                    {
                        editPanel.Mode = TagPanel.DisplayMode.Edit;
                        editPanel.TagClicked += TagEditPanel_TagClicked;
                        editPanel.CategoryManagementRequested += TagEditPanel_CategoryManagementRequested;
                    }

                    TagsLoaded?.Invoke(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadTags: 初始化失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 创建新标签
        /// </summary>
        public void NewTag(Window ownerWindow)
        {
            if (!App.IsTagTrainAvailable)
            {
                MessageBox.Show("TagTrain 不可用，无法创建标签。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new TagDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"NewTag: 开始创建标签: {dialog.TagName}");

                    // 使用 TagTrain 创建标签
                    var tagId = OoiMRRIntegration.GetOrCreateTagId(dialog.TagName);
                    System.Diagnostics.Debug.WriteLine($"NewTag: 返回的标签ID: {tagId}");

                    if (tagId > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"NewTag: 标签创建成功，重新加载标签列表");
                        TagsLoaded?.Invoke(this, EventArgs.Empty);
                        MessageBox.Show($"标签 \"{dialog.TagName}\" 创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"NewTag: 标签创建失败，tagId = {tagId}");
                        MessageBox.Show("创建标签失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"NewTag: 创建标签异常: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"NewTag: 堆栈跟踪: {ex.StackTrace}");
                    MessageBox.Show($"创建标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 管理标签（打开训练窗口）
        /// </summary>
        public void ManageTags(Window ownerWindow)
        {
            if (!App.IsTagTrainAvailable)
            {
                MessageBox.Show("TagTrain 不可用，无法打开标签训练工具。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 打开 TagTrain 训练窗口（作为独立窗口打开）
            try
            {
                var trainingWindow = new TagTrain.UI.TrainingWindow
                {
                    Owner = ownerWindow
                };
                trainingWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开标签训练工具失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 为文件添加标签
        /// </summary>
        public void AddTagToFile(List<FileSystemItem> selectedItems, Window ownerWindow, Action refreshCallback)
        {
            if (!App.IsTagTrainAvailable)
            {
                MessageBox.Show("TagTrain 不可用，无法添加标签。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (selectedItems == null || selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要添加标签的文件或文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sharedTagIds = GetSharedTagIds(selectedItems);
            var dialog = new TagSelectionDialog(sharedTagIds)
            {
                Owner = ownerWindow
            };

            if (dialog.ShowDialog() == true)
            {
                if (App.IsTagTrainAvailable)
                {
                    foreach (var item in selectedItems)
                    {
                        // 从 TagTrain 获取现有标签
                        var existingTagIdsList = OoiMRRIntegration.GetFileTagIds(item.Path);
                        var existingTagIds = (existingTagIdsList != null) ? existingTagIdsList.ToHashSet() : new HashSet<int>();
                        var desiredTagIds = new HashSet<int>(dialog.SelectedTagIds ?? new List<int>());

                        // 删除不再需要的标签
                        foreach (var tagId in existingTagIds.Except(desiredTagIds).ToList())
                        {
                            // 只删除图片文件的标签（TagTrain 只处理图片）
                            var ext = Path.GetExtension(item.Path).ToLower();
                            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
                            if (imageExtensions.Contains(ext))
                            {
                                OoiMRRIntegration.RemoveTagFromFile(item.Path, tagId);
                            }
                        }

                        // 添加新标签
                        foreach (var tagId in desiredTagIds.Except(existingTagIds))
                        {
                            // 只添加图片文件的标签（TagTrain 只处理图片）
                            var ext = Path.GetExtension(item.Path).ToLower();
                            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
                            if (imageExtensions.Contains(ext))
                            {
                                OoiMRRIntegration.AddTagToFile(item.Path, tagId);
                            }
                        }
                    }
                }

                refreshCallback?.Invoke();
            }
        }

        /// <summary>
        /// 批量添加标签
        /// </summary>
        public void BatchAddTags(List<FileSystemItem> selectedItems, Window ownerWindow, Action refreshCallback)
        {
            AddTagToFile(selectedItems, ownerWindow, refreshCallback);
        }

        /// <summary>
        /// 显示标签统计
        /// </summary>
        public void TagStatistics()
        {
            if (!App.IsTagTrainAvailable)
            {
                MessageBox.Show("TagTrain 不可用，无法查看标签统计。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var stats = OoiMRRIntegration.GetStatistics();
                var modelExists = OoiMRRIntegration.ModelExists();
                var modelPath = OoiMRRIntegration.GetModelPath();

                var message = $"标签统计信息\n\n" +
                              $"总标签数: {stats.UniqueTags}\n" +
                              $"总样本数: {stats.TotalSamples}\n" +
                              $"手动标注: {stats.ManualSamples}\n" +
                              $"唯一图片: {stats.UniqueImages}\n\n" +
                              $"模型状态: {(modelExists ? "已加载" : "未训练")}\n" +
                              $"模型路径: {modelPath}";

                MessageBox.Show(message, "标签统计", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取标签统计失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 根据标签筛选文件
        /// </summary>
        public void FilterByTag(OoiMRR.Tag tag, Func<long, string> formatFileSize)
        {
            if (tag == null)
            {
                System.Diagnostics.Debug.WriteLine("FilterByTag: tag 为 null");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"FilterByTag: 开始过滤标签: {tag.Name} (Id: {tag.Id})");
                _currentTagFilter = tag;

                // 从 TagTrain 获取该标签的文件路径
                var taggedPaths = App.IsTagTrainAvailable
                    ? (OoiMRRIntegration.GetFilePathsByTag(tag.Id) ?? new List<string>())
                    : new List<string>();

                System.Diagnostics.Debug.WriteLine($"FilterByTag: 获取到 {taggedPaths.Count} 个文件路径");

                var tagFiles = new List<FileSystemItem>();

                foreach (var path in taggedPaths)
                {
                    try
                    {
                        bool isDirectory = Directory.Exists(path);
                        bool isFile = File.Exists(path);
                        if (!isDirectory && !isFile)
                            continue;

                        var item = new FileSystemItem
                        {
                            Name = Path.GetFileName(path),
                            Path = path,
                            IsDirectory = isDirectory,
                            Type = isDirectory ? "文件夹" : Path.GetExtension(path),
                            Size = isDirectory ? "" : formatFileSize(new FileInfo(path).Length),
                            ModifiedDate = isDirectory ?
                                Directory.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm") :
                                File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm"),
                            Notes = ""
                        };

                        // 从 TagTrain 获取文件的标签
                        if (App.IsTagTrainAvailable)
                        {
                            var fileTagIds = OoiMRRIntegration.GetFileTagIds(path);
                            if (fileTagIds != null && fileTagIds.Count > 0)
                            {
                                var fileTagNames = OrderTagNames(fileTagIds);
                                item.Tags = string.Join(", ", fileTagNames);
                            }
                            else
                            {
                                item.Tags = "";
                            }
                        }
                        else
                        {
                            item.Tags = "";
                        }

                        // 从 OoiMRR 获取备注（如果存在）
                        var notes = DatabaseManager.GetFileNotes(path);
                        if (!string.IsNullOrEmpty(notes))
                        {
                            var firstLine = notes.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                            item.Notes = firstLine.Length > 100 ? firstLine.Substring(0, 100) + "..." : firstLine;
                        }

                        tagFiles.Add(item);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (IOException) { }
                }

                System.Diagnostics.Debug.WriteLine($"FilterByTag: 处理完成，共 {tagFiles.Count} 个文件");

                // 确保在UI线程更新
                if (!_dispatcher.CheckAccess())
                {
                    _dispatcher.Invoke(() =>
                    {
                        TagFilterRequested?.Invoke(this, new TagFilterEventArgs(tag, tagFiles));
                    });
                }
                else
                {
                    TagFilterRequested?.Invoke(this, new TagFilterEventArgs(tag, tagFiles));
                }

                System.Diagnostics.Debug.WriteLine("FilterByTag: 完成");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FilterByTag: 发生错误: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"FilterByTag: 堆栈跟踪: {ex.StackTrace}");
                MessageBox.Show($"过滤标签时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 打开标签在标签页中
        /// </summary>
        public void OpenTagInTab(OoiMRR.Tag tag, bool forceNewTab = false)
        {
            if (tag == null || string.IsNullOrWhiteSpace(tag.Name)) return;

            TagTabRequested?.Invoke(this, tag);
        }

        /// <summary>
        /// 打开分组管理
        /// </summary>
        public void OpenCategoryManagement(Window ownerWindow, TagPanel browsePanel, TagPanel editPanel, TagClickMode tagClickMode)
        {
            try
            {
                if (!App.IsTagTrainAvailable)
                {
                    MessageBox.Show("TagTrain 不可用，无法打开分组管理。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var window = new TagTrain.UI.CategoryManagementWindow
                {
                    Owner = ownerWindow
                };
                window.ShowDialog();

                // 刷新标签列表
                if (tagClickMode == TagClickMode.Browse && browsePanel != null)
                {
                    browsePanel.LoadExistingTags();
                }
                else if (editPanel != null)
                {
                    editPanel.LoadExistingTags();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开分组管理失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 编辑标签名称
        /// </summary>
        public void EditTagName(int tagId, string oldTagName, Window ownerWindow, TagPanel browsePanel, TagPanel editPanel, TagClickMode tagClickMode)
        {
            try
            {
                // 获取标签名称（如果传入的是ID）
                if (string.IsNullOrEmpty(oldTagName))
                {
                    oldTagName = TagTrain.Services.DataManager.GetTagName(tagId);
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
                    Owner = ownerWindow,
                    ResizeMode = ResizeMode.NoResize
                };

                var grid = new System.Windows.Controls.Grid();
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });
                grid.Margin = new Thickness(0);

                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = $"请输入新的标签名称：",
                    Margin = new Thickness(15, 20, 15, 10),
                    VerticalAlignment = VerticalAlignment.Top
                };
                System.Windows.Controls.Grid.SetRow(textBlock, 0);

                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = oldTagName,
                    Margin = new Thickness(15, 0, 15, 15),
                    FontSize = 14,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Height = 30
                };
                System.Windows.Controls.Grid.SetRow(textBox, 1);

                var buttonPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 15, 15)
                };
                System.Windows.Controls.Grid.SetRow(buttonPanel, 2);

                var okButton = new System.Windows.Controls.Button
                {
                    Content = "确定",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };

                var cancelButton = new System.Windows.Controls.Button
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
                        bool success = TagTrain.Services.DataManager.UpdateTagName(oldTagName, newTagName);

                        if (success)
                        {
                            // 刷新标签列表
                            if (tagClickMode == TagClickMode.Browse)
                            {
                                browsePanel?.LoadExistingTags();
                            }
                            else
                            {
                                editPanel?.LoadExistingTags();
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

        /// <summary>
        /// 删除标签
        /// </summary>
        public void DeleteTagById(int tagId, string tagName, TagPanel browsePanel, TagPanel editPanel, TagClickMode tagClickMode)
        {
            try
            {
                // 获取标签名称（如果传入的是ID）
                if (string.IsNullOrEmpty(tagName))
                {
                    tagName = TagTrain.Services.DataManager.GetTagName(tagId);
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
                        TagTrain.Services.DataManager.DeleteTag(tagId);

                        // 刷新标签列表
                        if (tagClickMode == TagClickMode.Browse)
                        {
                            browsePanel?.LoadExistingTags();
                        }
                        else
                        {
                            editPanel?.LoadExistingTags();
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

        /// <summary>
        /// 获取当前标签筛选器
        /// </summary>
        public OoiMRR.Tag GetCurrentTagFilter()
        {
            return _currentTagFilter;
        }

        #endregion

        #region 事件处理

        private void TagBrowsePanel_TagClicked(string tagName, bool forceNewTab)
        {
            try
            {
                // 通过标签名称获取标签ID，确保能正确识别已存在的标签页
                int tagId = OoiMRRIntegration.GetOrCreateTagId(tagName);
                if (tagId > 0)
                {
                    var tag = new OoiMRR.Tag { Id = tagId, Name = tagName };
                    OpenTagInTab(tag, forceNewTab);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TagBrowsePanel_TagClicked error: {ex.Message}");
            }
        }

        private void TagBrowsePanel_CategoryManagementRequested()
        {
            // 通过事件通知主窗口打开分组管理
            // 实际调用由主窗口处理
        }

        private void TagEditPanel_TagClicked(string tagName, bool forceNewTab)
        {
            // TODO: 实现编辑模式的标签点击功能（应用标签到当前图片）
            System.Diagnostics.Debug.WriteLine($"TagEditPanel_TagClicked: {tagName}, forceNewTab: {forceNewTab}");
        }

        private void TagEditPanel_CategoryManagementRequested()
        {
            // 通过事件通知主窗口打开分组管理
            // 实际调用由主窗口处理
        }

        #endregion

        #region 辅助方法

        private List<int> GetSharedTagIds(List<FileSystemItem> items)
        {
            if (items == null || items.Count == 0)
                return new List<int>();

            try
            {
                // 从 TagTrain 获取第一个文件的标签
                var firstTagIds = OoiMRRIntegration.GetFileTagIds(items[0].Path);
                if (firstTagIds == null || firstTagIds.Count == 0)
                    return new List<int>();

                var initial = firstTagIds.ToHashSet();
                foreach (var item in items.Skip(1))
                {
                    var itemTagIds = OoiMRRIntegration.GetFileTagIds(item.Path);
                    if (itemTagIds == null || itemTagIds.Count == 0)
                    {
                        initial.Clear();
                        break;
                    }
                    initial.IntersectWith(itemTagIds);
                }

                return initial.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取共享标签失败: {ex.Message}");
                return new List<int>();
            }
        }

        private List<string> OrderTagNames(List<int> tagIds)
        {
            try
            {
                var pairs = tagIds
                    .Select(id => new { Id = id, Name = OoiMRRIntegration.GetTagName(id) })
                    .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                    .ToList();

                // 当前过滤标签优先，其余按名称升序
                if (_currentTagFilter != null)
                {
                    var currentTagPair = pairs.FirstOrDefault(p => p.Id == _currentTagFilter.Id);
                    if (currentTagPair != null)
                    {
                        pairs.Remove(currentTagPair);
                        pairs.Insert(0, currentTagPair);
                    }
                }

                var remaining = pairs
                    .Where(p => _currentTagFilter == null || p.Id != _currentTagFilter.Id)
                    .OrderBy(p => p.Name)
                    .ToList();

                return pairs.Take(1).Concat(remaining).Select(p => p.Name).ToList();
            }
            catch
            {
                return tagIds.Select(id => OoiMRRIntegration.GetTagName(id)).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            }
        }

        #endregion
    }

    #region 事件参数类

    /// <summary>
    /// 标签筛选事件参数
    /// </summary>
    public class TagFilterEventArgs : EventArgs
    {
        public OoiMRR.Tag Tag { get; }
        public List<FileSystemItem> Files { get; }

        public TagFilterEventArgs(OoiMRR.Tag tag, List<FileSystemItem> files)
        {
            Tag = tag;
            Files = files;
        }
    }

    /// <summary>
    /// 标签点击模式枚举
    /// </summary>
    public enum TagClickMode
    {
        Browse,
        Edit
    }

    #endregion
}

