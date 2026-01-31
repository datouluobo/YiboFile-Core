using System;
using YiboFile.Models;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using YiboFile.Controls;
using YiboFile.Services.FileList;
using YiboFile.Services.Navigation;

namespace YiboFile.Services.FileInfo
{
    /// <summary>
    /// 文件信息显示服务
    /// 负责显示文件和文件夹的详细信息
    /// </summary>
    public class FileInfoService
    {
        #region 依赖字段

        private readonly FileBrowserControl _fileBrowser;
        private readonly FileListService _fileListService;
        private readonly YiboFile.Services.Navigation.NavigationCoordinator _navigationCoordinator;
        private readonly YiboFile.Services.Features.ITagService _tagService;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 FileInfoService
        /// </summary>
        /// <param name="fileBrowser">文件浏览器控件</param>
        /// <param name="fileListService">文件列表服务</param>
        /// <param name="navigationCoordinator">导航协调器</param>
        /// <param name="tagService">标签服务</param>
        public FileInfoService(FileBrowserControl fileBrowser, FileListService fileListService, YiboFile.Services.Navigation.NavigationCoordinator navigationCoordinator, YiboFile.Services.Features.ITagService tagService = null)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _fileListService = fileListService ?? throw new ArgumentNullException(nameof(fileListService));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _tagService = tagService ?? App.ServiceProvider?.GetService(typeof(YiboFile.Services.Features.ITagService)) as YiboFile.Services.Features.ITagService;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 显示文件或文件夹的详细信息
        /// </summary>
        /// <param name="item">文件系统项</param>
        public void ShowFileInfo(FileSystemItem item)
        {
            if (_fileBrowser?.FileInfoPanelControl == null) return;
            _fileBrowser.FileInfoPanelControl.Children.Clear();

            if (item == null) return;

            if (item.IsDirectory)
            {
                ShowDirectoryInfo(item);
            }
            else
            {
                ShowFileDetails(item);
            }
        }

        /// <summary>
        /// 显示库的详细信息
        /// </summary>
        /// <param name="library">库对象</param>
        public void ShowLibraryInfo(Library library)
        {
            if (_fileBrowser?.FileInfoPanelControl == null || library == null) return;
            _fileBrowser.FileInfoPanelControl.Children.Clear();

            var infoItems = new System.Collections.Generic.List<(string label, string value)>
            {
                ("名称", library.Name),
                ("类型", "库"),
                ("包含位置", library.Paths != null && library.Paths.Count > 0
                    ? string.Join(Environment.NewLine, library.Paths)
                    : "未添加位置")
            };

            foreach (var (label, value) in infoItems)
            {
                var panel = CreateInfoPanel(label, value);
                _fileBrowser.FileInfoPanelControl.Children.Add(panel);
            }
        }

        /// <summary>
        /// 获取图片的尺寸信息
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <returns>尺寸字符串，格式为 "宽度 × 高度 像素"，失败返回 null</returns>
        public string GetImageDimensions(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                    return null;

                var extension = System.IO.Path.GetExtension(imagePath)?.ToLowerInvariant();

                // 优先使用 Magick.NET（支持更多格式，包括 SVG 和 PSD）
                try
                {
                    using (var image = new ImageMagick.MagickImage(imagePath))
                    {
                        return $"{image.Width} × {image.Height} 像素";
                    }
                }
                catch
                {
                    // 如果 Magick.NET 失败，尝试使用 System.Drawing.Image（仅支持常见格式）
                    try
                    {
                        using (var image = System.Drawing.Image.FromFile(imagePath))
                        {
                            return $"{image.Width} × {image.Height} 像素";
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 显示文件夹详细信息
        /// </summary>
        /// <param name="item">文件夹项</param>
        /// <summary>
        /// 显示文件夹详细信息
        /// </summary>
        /// <param name="item">文件夹项</param>
        private void ShowDirectoryInfo(FileSystemItem item)
        {
            try
            {
                // 先显示基本信息，统计数据暂时显示"计算中..."
                if (_fileBrowser?.FileInfoPanelControl != null)
                {
                    var infoItems = new System.Collections.Generic.List<(string label, string value)>
                    {
                        ("名称", item.Name),
                        ("路径", item.Path),
                        ("类型", "文件夹"),
                        ("修改日期", item.ModifiedDate)
                        // ("标签", item.Tags)
                    };

                    foreach (var (label, value) in infoItems)
                    {
                        var panel = CreateInfoPanel(label, value);
                        _fileBrowser.FileInfoPanelControl.Children.Add(panel);
                    }

                    // Tags Panel
                    var dirTagsPanel = CreateTagsPanel("标签", item.Tags);
                    _fileBrowser.FileInfoPanelControl.Children.Add(dirTagsPanel);

                    // 创建占位符面板用于后续更新
                    var filesCountPanel = CreateInfoPanel("文件数", "计算中...");
                    var dirsCountPanel = CreateInfoPanel("文件夹数", "计算中...");
                    var totalSizePanel = CreateInfoPanel("总大小", "计算中...");

                    _fileBrowser.FileInfoPanelControl.Children.Insert(3, filesCountPanel);
                    _fileBrowser.FileInfoPanelControl.Children.Insert(4, dirsCountPanel);
                    _fileBrowser.FileInfoPanelControl.Children.Insert(5, totalSizePanel);

                    // 异步计算统计信息
                    Task.Run(() =>
                    {
                        try
                        {
                            var files = Directory.GetFiles(item.Path);
                            var directories = Directory.GetDirectories(item.Path);
                            long totalSize = 0;

                            // 仅计算顶层文件大小，避免递归遍历导致过度卡顿
                            // 如需递归，建议使用 FileListService.CalculateFolderSizeAsync 的逻辑
                            foreach (var file in files)
                            {
                                try
                                {
                                    totalSize += new System.IO.FileInfo(file).Length;
                                }
                                catch { }
                            }

                            var filesCountStr = files.Length.ToString();
                            var dirsCountStr = directories.Length.ToString();
                            var totalSizeStr = _fileListService.FormatFileSize(totalSize);

                            // 更新UI
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                // 检查当前面板是否仍对应同一个路径（避免快速切换导致的错乱）
                                // 这里简单更新，如果面板已被清空则可能抛出异常或无效
                                try
                                {
                                    UpdateValueInPanel(filesCountPanel, filesCountStr);
                                    UpdateValueInPanel(dirsCountPanel, dirsCountStr);
                                    UpdateValueInPanel(totalSizePanel, totalSizeStr);
                                }
                                catch { }
                            });
                        }
                        catch (Exception)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                try
                                {
                                    UpdateValueInPanel(filesCountPanel, "-");
                                    UpdateValueInPanel(dirsCountPanel, "-");
                                    UpdateValueInPanel(totalSizePanel, "-");
                                }
                                catch { }
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                var errorPanel = CreateErrorPanel(ex.Message);
                if (_fileBrowser?.FileInfoPanelControl != null)
                    _fileBrowser.FileInfoPanelControl.Children.Add(errorPanel);
            }
        }

        private void UpdateValueInPanel(StackPanel panel, string newValue)
        {
            if (panel.Children.Count > 1 && panel.Children[1] is TextBlock valueBlock)
            {
                valueBlock.Text = newValue;
            }
        }

        /// <summary>
        /// 显示文件详细信息
        /// </summary>
        /// <param name="item">文件项</param>
        private void ShowFileDetails(FileSystemItem item)
        {
            var infoItems = new System.Collections.Generic.List<(string label, string value)>
            {
                ("名称", item.Name),
                ("路径", item.Path),
                ("类型", item.Type),
                ("大小", item.Size),
                ("修改日期", item.ModifiedDate)
                // ("标签", item.Tags) // Handled by CreateTagsPanel
            };

            var fileExtension = System.IO.Path.GetExtension(item.Path)?.ToLowerInvariant();

            // 如果是视频或音频文件，添加时长信息
            if (!string.IsNullOrEmpty(fileExtension) &&
                (YiboFile.Services.Search.SearchFilterService.VideoExtensions.Contains(fileExtension) ||
                 YiboFile.Services.Search.SearchFilterService.AudioExtensions.Contains(fileExtension)))
            {
                if (item.DurationMs > 0)
                {
                    TimeSpan t = TimeSpan.FromMilliseconds(item.DurationMs);
                    // Format as HH:mm:ss or mm:ss
                    string durationStr = (t.TotalHours >= 1) ? t.ToString(@"hh\:mm\:ss") : t.ToString(@"mm\:ss");
                    infoItems.Insert(4, ("时长", durationStr)); // 在"大小"(index 3)之后插入? No, "修改日期" is index 4. Insert at 4 puts it before "修改日期". 
                                                              // Let's insert after Size (index 3). So index 4.
                }
            }

            // 如果是图片文件，添加尺寸信息
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif", ".svg", ".psd", ".ico" };
            if (!string.IsNullOrEmpty(fileExtension) && imageExtensions.Contains(fileExtension))
            {
                try
                {
                    string imageSize = GetImageDimensions(item.Path);
                    if (!string.IsNullOrEmpty(imageSize))
                    {
                        // Ensure insertion index is correct. 
                        // If Duration inserted at 4, Size is 3. Duration is 4. Modified is 5.
                        // Insert dimensions after Size (or Duration). 
                        // Let's just find "大小" index or append.
                        // Ideally consistent order: Name, Path, Type, Size, [Dimensions/Duration], Modified.
                        // Size is index 3. Insert at 4 for Dimensions/Duration.
                        // If both exist (rare for image/video overlap?), one pushes other.

                        infoItems.Insert(4, ("尺寸", imageSize));
                    }
                }
                catch
                {
                    // 获取尺寸失败，忽略
                }
            }

            foreach (var (label, value) in infoItems)
            {
                var panel = CreateInfoPanel(label, value);
                if (_fileBrowser?.FileInfoPanelControl != null)
                    _fileBrowser.FileInfoPanelControl.Children.Add(panel);
            }

            // Display Tags
            if (_fileBrowser?.FileInfoPanelControl != null)
            {
                var tagsPanel = CreateTagsPanel("标签", item.Tags);
                _fileBrowser.FileInfoPanelControl.Children.Add(tagsPanel);
            }
        }

        /// <summary>
        /// 创建信息显示面板
        /// </summary>
        /// <param name="label">标签</param>
        /// <param name="value">值</param>
        /// <returns>信息面板</returns>
        private StackPanel CreateInfoPanel(string label, string value)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            var labelText = new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.Bold, Width = 80 };
            labelText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            panel.Children.Add(labelText);

            var valueText = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap };
            valueText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
            panel.Children.Add(valueText);

            return panel;
        }

        private StackPanel CreateTagsPanel(string label, string tagsString)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            var labelText = new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.Bold, Width = 80, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 0, 0) };
            labelText.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            panel.Children.Add(labelText);

            if (string.IsNullOrWhiteSpace(tagsString))
            {
                var valueText = new TextBlock { Text = "-", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                valueText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
                panel.Children.Add(valueText);
            }
            else
            {
                var tagsWrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
                var tags = tagsString.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var tag in tags)
                {
                    var cleanTag = tag.Trim();
                    if (string.IsNullOrEmpty(cleanTag)) continue;

                    string tagColor = null;
                    try { if (_tagService != null) tagColor = _tagService.GetTagColorByName(cleanTag); } catch { }
                    bool hasColor = !string.IsNullOrEmpty(tagColor);

                    var tagBorder = new Border
                    {
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 2, 6, 2),
                        Margin = new Thickness(0, 0, 6, 4),
                        Background = TagViewModel.GetColorBrush(cleanTag, tagColor),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)), // #CCCCCC
                        BorderThickness = new Thickness(hasColor ? 0 : 1),
                        Cursor = Cursors.Hand
                    };

                    // Add hover effect
                    tagBorder.MouseEnter += (s, e) => tagBorder.Opacity = 0.8;
                    tagBorder.MouseLeave += (s, e) => tagBorder.Opacity = 1.0;

                    // Add click handler
                    tagBorder.MouseLeftButtonUp += (s, e) =>
                    {
                        _navigationCoordinator.HandlePathNavigation(
                            $"tag://{cleanTag}",
                            NavigationSource.AddressBar,
                            ClickType.LeftClick
                        );
                    };

                    var tagText = new TextBlock
                    {
                        Text = cleanTag,
                        FontSize = 11,
                        // Ensure text is dark gray for better contrast on pastel backgrounds
                        Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40))
                    };

                    tagBorder.Child = tagText;
                    tagsWrapPanel.Children.Add(tagBorder);
                }
                panel.Children.Add(tagsWrapPanel);
            }

            return panel;
        }

        private Brush GetTagBrush(string tag)
        {
            // First check DB for explicit color
            string dbColor = null;
            try
            {
                if (_tagService != null) dbColor = _tagService.GetTagColorByName(tag);
            }
            catch { }

            // Use TagViewModel's shared logic (Explicit Color -> Hash Pastel -> Gray)
            return TagViewModel.GetColorBrush(tag, dbColor);
        }

        /// <summary>
        /// 创建错误显示面板
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <returns>错误面板</returns>
        private StackPanel CreateErrorPanel(string errorMessage)
        {
            var errorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            var errorLabel = new TextBlock { Text = "错误: ", FontWeight = FontWeights.Bold, Width = 80 };
            errorLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
            errorPanel.Children.Add(errorLabel);

            var errorText = new TextBlock { Text = errorMessage, TextWrapping = TextWrapping.Wrap };
            errorText.SetResourceReference(TextBlock.ForegroundProperty, "ErrorBrush");
            errorPanel.Children.Add(errorText);

            return errorPanel;
        }

        #endregion
    }
}


