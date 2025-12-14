using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OoiMRR.Controls;
using TagTrain.UI;
using OoiMRR.Services.FileList;
using OoiMRR.Services.FileNotes;

namespace OoiMRR.Services.Tag
{
    /// <summary>
    /// 标签 UI 处理上下文接口
    /// 用于解耦 MainWindow 状态与 TagUIHandler 逻辑
    /// </summary>
    public interface ITagUIHandlerContext
    {
        FileBrowserControl FileBrowser { get; }
        Dispatcher Dispatcher { get; }
        Window OwnerWindow { get; }
        Func<OoiMRR.Tag> GetCurrentTagFilter { get; }
        Action<OoiMRR.Tag> SetCurrentTagFilter { get; }
        Func<List<FileSystemItem>> GetCurrentFiles { get; }
        Action<List<FileSystemItem>> SetCurrentFiles { get; }
        Func<Library> GetCurrentLibrary { get; }
        Func<string> GetCurrentPath { get; }
        Func<bool> GetIsUpdatingTagSelection { get; }
        Func<List<int>, List<string>> OrderTagNames { get; }
        Action<OoiMRR.Tag, List<FileSystemItem>> UpdateTagFilesUI { get; }
        Action LoadFiles { get; }
        Action<Library> LoadLibraryFiles { get; }
        Action LoadCurrentDirectory { get; }
        Action LoadTags { get; }
        Func<System.Windows.Controls.Grid> GetNavTagContent { get; }
        Func<FileListService> GetFileListService { get; }
    }

    /// <summary>
    /// 标签 UI 处理
    /// 负责标签相关的 UI 交互处理，包括标签过滤、创建、管理等
    /// </summary>
    public class TagUIHandler
    {
        private readonly ITagUIHandlerContext _context;

        /// <summary>
        /// 初始化标签 UI 处理器
        /// </summary>
        /// <param name="context">标签 UI 处理上下文</param>
        public TagUIHandler(ITagUIHandlerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>
        /// 按标签过滤文件
        /// </summary>
        /// <param name="tag">要过滤的标签</param>
        public void FilterByTag(OoiMRR.Tag tag)
        {
            if (tag == null)
            {
                System.Diagnostics.Debug.WriteLine("FilterByTag: tag 为 null");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"FilterByTag: 开始过滤标签: {tag.Name} (Id: {tag.Id})");
                _context.SetCurrentTagFilter(tag);

                // TagsListBox已移除，标签选择现在由TagTrain面板处理
                // 后续可以在TagTrain面板中高亮选中的标签

                // 从 TagTrain 获取该标签的文件路径
                var taggedPaths = App.IsTagTrainAvailable 
                    ? (OoiMRRIntegration.GetFilePathsByTag(tag.Id) ?? new List<string>())
                    : new List<string>();
                
                System.Diagnostics.Debug.WriteLine($"FilterByTag: 获取到 {taggedPaths.Count} 个文件路径");
                
                var tagFiles = new List<FileSystemItem>();
                var fileListService = _context.GetFileListService();

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
                            Size = isDirectory ? "" : fileListService.FormatFileSize(new System.IO.FileInfo(path).Length),
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
                                var fileTagNames = _context.OrderTagNames(fileTagIds);
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
                        var notes = FileNotesService.GetFileNotes(path);
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
                if (!_context.Dispatcher.CheckAccess())
                {
                    _context.Dispatcher.Invoke(() => _context.UpdateTagFilesUI(tag, tagFiles));
                }
                else
                {
                    _context.UpdateTagFilesUI(tag, tagFiles);
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
        /// 新建标签按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        public void NewTag_Click(object sender, RoutedEventArgs e)
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
                    System.Diagnostics.Debug.WriteLine($"NewTag_Click: 开始创建标签: {dialog.TagName}");
                    
                    // 使用 TagTrain 创建标签
                    var tagId = OoiMRRIntegration.GetOrCreateTagId(dialog.TagName);
                    System.Diagnostics.Debug.WriteLine($"NewTag_Click: 返回的标签ID: {tagId}");
                    
                    if (tagId > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"NewTag_Click: 标签创建成功，重新加载标签列表");
                        _context.LoadTags();
                        MessageBox.Show($"标签 \"{dialog.TagName}\" 创建成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"NewTag_Click: 标签创建失败，tagId = {tagId}");
                        MessageBox.Show("创建标签失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"NewTag_Click: 创建标签异常: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"NewTag_Click: 堆栈跟踪: {ex.StackTrace}");
                    MessageBox.Show($"创建标签失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 管理标签按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        public void ManageTags_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable)
            {
                MessageBox.Show("TagTrain 不可用，无法打开标签训练工具。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            // 打开 TagTrain 训练窗口（作为独立窗口打开）
            try
            {
                var trainingWindow = new TrainingWindow
                {
                    Owner = _context.OwnerWindow
                };
                trainingWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开标签训练工具失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 添加标签到文件按钮点击事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">路由事件参数</param>
        public void AddTagToFile_Click(object sender, RoutedEventArgs e)
        {
            if (!App.IsTagTrainAvailable)
            {
                MessageBox.Show("TagTrain 不可用，无法添加标签。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            OpenTagDialogForSelectedItems();
        }

        /// <summary>
        /// 为选中的项目打开标签选择对话框
        /// </summary>
        public void OpenTagDialogForSelectedItems()
        {
            if (_context.FileBrowser == null)
                return;

            var selectedItems = _context.FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要添加标签的文件或文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sharedTagIds = GetSharedTagIds(selectedItems);
            var dialog = new TagSelectionDialog(sharedTagIds)
            {
                Owner = _context.OwnerWindow
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

                var currentTagFilter = _context.GetCurrentTagFilter();
                if (currentTagFilter != null)
                {
                    FilterByTag(currentTagFilter);
                }
                else
                {
                    _context.LoadFiles();
                }
            }
        }

        /// <summary>
        /// 获取多个文件共享的标签ID列表
        /// </summary>
        /// <param name="items">文件系统项列表</param>
        /// <returns>共享的标签ID列表</returns>
        public List<int> GetSharedTagIds(List<FileSystemItem> items)
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

        /// <summary>
        /// 标签列表选择变化事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">选择变化事件参数</param>
        public void TagsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // TagsListBox已移除，此方法保留用于兼容性
            // 标签选择现在由TagTrain面板处理，后续可以从TagTrain面板获取选中的标签
            if (_context.GetIsUpdatingTagSelection())
                return;

            // 如果_currentTagFilter有值，使用它来过滤文件
            var currentTagFilter = _context.GetCurrentTagFilter();
            if (currentTagFilter != null)
            {
                FilterByTag(currentTagFilter);
            }
            else
            {
                // 没有选中标签，清空过滤
                var currentLibrary = _context.GetCurrentLibrary();
                if (currentLibrary != null)
                {
                    _context.LoadLibraryFiles(currentLibrary);
                }
                else
                {
                    var currentPath = _context.GetCurrentPath();
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        _context.LoadCurrentDirectory();
                    }
                }
            }
        }
    }
}

