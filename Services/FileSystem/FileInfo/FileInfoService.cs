using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OoiMRR.Controls;
using OoiMRR.Services.FileList;

namespace OoiMRR.Services.FileInfo
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

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 FileInfoService
        /// </summary>
        /// <param name="fileBrowser">文件浏览器控件</param>
        /// <param name="fileListService">文件列表服务</param>
        public FileInfoService(FileBrowserControl fileBrowser, FileListService fileListService)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _fileListService = fileListService ?? throw new ArgumentNullException(nameof(fileListService));
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

            System.Diagnostics.Debug.WriteLine($"[FileInfoService] ShowFileInfo called for item: {item?.Name}");
            _fileBrowser.FileInfoPanelControl.Children.Clear();

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
        private void ShowDirectoryInfo(FileSystemItem item)
        {
            try
            {
                var files = Directory.GetFiles(item.Path);
                var directories = Directory.GetDirectories(item.Path);
                long totalSize = files.Sum(f => new System.IO.FileInfo(f).Length);

                var infoItems = new[]
                {
                    ("名称", item.Name),
                    ("路径", item.Path),
                    ("类型", "文件夹"),
                    ("文件数", files.Length.ToString()),
                    ("文件夹数", directories.Length.ToString()),
                    ("总大小", _fileListService.FormatFileSize(totalSize)),
                    ("修改日期", item.ModifiedDate),
                    ("标签", item.Tags)
                };

                foreach (var (label, value) in infoItems)
                {
                    var panel = CreateInfoPanel(label, value);
                    if (_fileBrowser?.FileInfoPanelControl != null)
                        _fileBrowser.FileInfoPanelControl.Children.Add(panel);
                }
            }
            catch (Exception ex)
            {
                var errorPanel = CreateErrorPanel(ex.Message);
                if (_fileBrowser?.FileInfoPanelControl != null)
                    _fileBrowser.FileInfoPanelControl.Children.Add(errorPanel);
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
                ("修改日期", item.ModifiedDate),
                ("标签", item.Tags)
            };

            // 如果是图片文件，添加尺寸信息
            var fileExtension = System.IO.Path.GetExtension(item.Path)?.ToLowerInvariant();
            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif", ".svg", ".psd", ".ico" };
            if (!string.IsNullOrEmpty(fileExtension) && imageExtensions.Contains(fileExtension))
            {
                try
                {
                    string imageSize = GetImageDimensions(item.Path);
                    if (!string.IsNullOrEmpty(imageSize))
                    {
                        infoItems.Insert(4, ("尺寸", imageSize)); // 在"大小"之后插入
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
            panel.Children.Add(new TextBlock { Text = $"{label}: ", FontWeight = FontWeights.Bold, Width = 80 });
            panel.Children.Add(new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap });
            return panel;
        }

        /// <summary>
        /// 创建错误显示面板
        /// </summary>
        /// <param name="errorMessage">错误消息</param>
        /// <returns>错误面板</returns>
        private StackPanel CreateErrorPanel(string errorMessage)
        {
            var errorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            errorPanel.Children.Add(new TextBlock { Text = "错误: ", FontWeight = FontWeights.Bold, Width = 80 });
            errorPanel.Children.Add(new TextBlock { Text = errorMessage, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Red });
            return errorPanel;
        }

        #endregion
    }
}

