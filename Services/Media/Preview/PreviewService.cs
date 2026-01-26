using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using YiboFile.Controls;

namespace YiboFile.Services.Preview
{
    /// <summary>
    /// 文件预览服务
    /// 负责管理文件预览的加载、清除和事件处理
    /// </summary>
    public class PreviewService
    {
        #region 依赖字段

        private readonly RightPanelControl _rightPanel;
        private readonly FileBrowserControl _fileBrowser;
        private readonly Dispatcher _dispatcher;
        private readonly Action _loadCurrentDirectoryCallback;
        private readonly Action<string> _createTabCallback;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化 PreviewService
        /// </summary>
        /// <param name="rightPanel">右侧面板控件</param>
        /// <param name="fileBrowser">文件浏览器控件</param>
        /// <param name="dispatcher">UI线程调度器</param>
        /// <param name="loadCurrentDirectoryCallback">加载当前目录的回调</param>
        /// <param name="createTabCallback">创建标签页的回调</param>
        public PreviewService(
            RightPanelControl rightPanel,
            FileBrowserControl fileBrowser,
            Dispatcher dispatcher,
            Action loadCurrentDirectoryCallback,
            Action<string> createTabCallback)
        {
            _rightPanel = rightPanel ?? throw new ArgumentNullException(nameof(rightPanel));
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _loadCurrentDirectoryCallback = loadCurrentDirectoryCallback ?? throw new ArgumentNullException(nameof(loadCurrentDirectoryCallback));
            _createTabCallback = createTabCallback ?? throw new ArgumentNullException(nameof(createTabCallback));
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 加载文件预览
        /// </summary>
        /// <param name="item">文件系统项</param>
        public void LoadFilePreview(FileSystemItem item)
        {
            if (_rightPanel?.PreviewGrid == null) return;

            // 如果面板不可见（例如在双列表模式下），则不进行加载以节省资源
            if (_rightPanel.Visibility != Visibility.Visible) return;

            _dispatcher.Invoke(() =>
            {
                var mainWindow = Application.Current.MainWindow;
                var mainWindowPanel = mainWindow?.FindName("RightPanel") as RightPanelControl;
                if (mainWindowPanel != null)
                {
                }
            });

            // 先清理之前的预览资源（特别是视频的MediaElement）
            CleanupPreviousPreview();

            // 获取文件扩展名
            var fileExtension = Path.GetExtension(item.Path)?.ToLowerInvariant();

            // 1. 检查是否是Markdown文件
            var markdownExtensions = new[] { ".md", ".markdown" };
            if (!item.IsDirectory && !string.IsNullOrEmpty(fileExtension) && markdownExtensions.Contains(fileExtension))
            {
                // 清理PreviewGrid中的其他预览元素
                ClearPreviewGridForEditor();

                // 显示Markdown编辑器
                var markdownEditor = new YiboFile.Controls.MarkdownEditorControl();
                markdownEditor.LoadMarkdown(item.Path);
                _rightPanel.PreviewGrid.Children.Add(markdownEditor);

                // 隐藏默认预览文本
                if (_rightPanel.DefaultPreviewText != null)
                {
                    _rightPanel.DefaultPreviewText.Visibility = Visibility.Collapsed;
                }
                return;
            }



            // 4. 检查其他可编辑的文本文件
            var editableTextExtensions = new[]
            {
                ".txt", ".cs", ".cpp", ".h", ".hpp", ".c", ".py", ".js", ".ts",
                ".css", ".json", ".sql", ".php",
                ".java", ".go", ".rs", ".rb", ".sh", ".bat", ".ps1", ".yaml", ".yml",
                ".config", ".ini", ".log"
            };
            if (!item.IsDirectory && !string.IsNullOrEmpty(fileExtension) && editableTextExtensions.Contains(fileExtension))
            {
                // 清理PreviewGrid中的其他预览元素
                ClearPreviewGridForEditor();

                // 显示文本编辑器
                var textEditor = new YiboFile.Controls.TextEditorControl();
                textEditor.LoadFile(item.Path);
                _rightPanel.PreviewGrid.Children.Add(textEditor);

                // 隐藏默认预览文本
                if (_rightPanel.DefaultPreviewText != null)
                {
                    _rightPanel.DefaultPreviewText.Visibility = Visibility.Collapsed;
                }
                return;
            }

            // 对于所有文件（包括图片），先清除图片预览，然后使用PreviewFactory统一路由
            _rightPanel.ClearImagePreview();

            // 确保ImagePreviewBorder不会遮挡预览内容
            if (_rightPanel.ImagePreviewBorder != null)
            {
                _rightPanel.ImagePreviewBorder.Visibility = Visibility.Collapsed;
                Panel.SetZIndex(_rightPanel.ImagePreviewBorder, 0);
            }

            // 清理PreviewGrid中的其他预览元素（保留DefaultPreviewText和ImagePreviewBorder）
            if (_rightPanel.PreviewGrid != null)
            {
                for (int i = _rightPanel.PreviewGrid.Children.Count - 1; i >= 0; i--)
                {
                    var child = _rightPanel.PreviewGrid.Children[i];
                    // 保留DefaultPreviewText和ImagePreviewBorder，清除其他元素
                    if (child != _rightPanel.DefaultPreviewText && child != _rightPanel.ImagePreviewBorder)
                    {
                        _rightPanel.PreviewGrid.Children.RemoveAt(i);
                    }
                }
            }

            try
            {
                // 设置刷新回调
                YiboFile.Previews.PreviewFactory.OnFileListRefreshRequested = () =>
                {
                    _dispatcher.Invoke(() =>
                    {
                        _loadCurrentDirectoryCallback?.Invoke();
                    });
                };

                // 设置在新标签页中打开文件夹的回调
                YiboFile.Previews.PreviewFactory.OnOpenFolderInNewTab = (folderPath) =>
                {
                    _dispatcher.Invoke(() =>
                    {
                        _createTabCallback?.Invoke(folderPath);
                    });
                };

                // PreviewFactory 会自动处理文件夹和文件
                var previewElement = YiboFile.Previews.PreviewFactory.CreatePreview(item.Path);
                if (previewElement != null)
                {
                    // 确保预览元素在ImagePreviewBorder之上 - 使用更高的ZIndex
                    Panel.SetZIndex(previewElement, 10);
                    _rightPanel.PreviewGrid.Children.Add(previewElement);
                    // 隐藏默认预览文本
                    var defaultText = _rightPanel.PreviewGrid.Children.OfType<TextBlock>()
                        .FirstOrDefault(tb => tb.Name == "DefaultPreviewText");
                    if (defaultText != null)
                    {
                        defaultText.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    // 如果预览元素为null，显示默认提示
                    var defaultText = new TextBlock
                    {
                        Text = "无法创建预览",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = System.Windows.Media.Brushes.Gray,
                        FontSize = 14
                    };
                    _rightPanel.PreviewGrid.Children.Add(defaultText);
                }

                // 延迟绑定按钮事件，确保UI元素已完全加载
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    // 为预览元素中的按钮绑定事件
                    AttachPreviewButtonEvents(previewElement, item.Path);
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                _rightPanel.PreviewGrid.Children.Add(new TextBlock
                {
                    Text = $"预览失败: {ex.Message}",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = System.Windows.Media.Brushes.Red
                });
            }
        }

        /// <summary>
        /// 清除预览内容
        /// </summary>
        public void ClearPreview()
        {
            // 清除图片预览
            if (_rightPanel != null)
            {
                _rightPanel.ClearImagePreview();
            }

            // 清除其他预览：不要清空 Children，避免移除默认预览结构（DefaultPreviewText、ImagePreviewBorder）
            if (_rightPanel?.PreviewGrid != null)
            {
                // 清除所有预览元素（保留 DefaultPreviewText 和 ImagePreviewBorder）
                for (int i = _rightPanel.PreviewGrid.Children.Count - 1; i >= 0; i--)
                {
                    var child = _rightPanel.PreviewGrid.Children[i];
                    // 保留 DefaultPreviewText 和 ImagePreviewBorder，清除其他元素（包括 SVG、PSD 等预览）
                    if (child != _rightPanel.DefaultPreviewText && child != _rightPanel.ImagePreviewBorder)
                    {
                        _rightPanel.PreviewGrid.Children.RemoveAt(i);
                    }
                }

                // 显示默认提示文本
                var defaultText = _rightPanel.PreviewGrid.Children.OfType<TextBlock>()
                    .FirstOrDefault(tb => tb.Name == "DefaultPreviewText");
                if (defaultText != null)
                {
                    defaultText.Visibility = Visibility.Visible;
                }

                // 同时确保图片预览边框处于隐藏状态
                var imageBorder = _rightPanel.PreviewGrid.Children.OfType<Border>()
                    .FirstOrDefault(b => b.Name == "ImagePreviewBorder");
                if (imageBorder != null)
                {
                    imageBorder.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// 处理预览区打开文件请求
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void HandlePreviewOpenFileRequest(string filePath)
        {
            // 预览区打开文件请求 - 在当前预览区显示文件内容
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                LoadFilePreview(new FileSystemItem
                {
                    Path = filePath,
                    Name = Path.GetFileName(filePath),
                    IsDirectory = false
                });
            }
        }

        /// <summary>
        /// 处理预览区中键点击请求
        /// </summary>
        /// <param name="selectedItem">当前选中的文件项</param>
        public void HandlePreviewMiddleClickRequest(FileSystemItem selectedItem)
        {
            // 预览区中键打开文件
            if (selectedItem != null && !selectedItem.IsDirectory)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = selectedItem.Path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 清理之前的预览资源
        /// </summary>
        private void CleanupPreviousPreview()
        {
            if (_rightPanel?.PreviewGrid == null) return;

            // 查找并停止所有MediaElement（视频预览）
            var mediaElements = FindVisualChildren<System.Windows.Controls.MediaElement>(_rightPanel.PreviewGrid).ToList();
            foreach (var mediaElement in mediaElements)
            {
                try
                {
                    mediaElement.Stop();
                    mediaElement.Source = null;
                    mediaElement.Close();
                }
                catch
                {
                    // 忽略清理错误
                }
            }

            // 查找并停止所有DispatcherTimer（视频预览的定时器）
            // 注意：DispatcherTimer无法直接从UI元素中查找，需要在VideoPreview中管理
            // 这里只清理MediaElement即可
        }

        /// <summary>
        /// 为预览元素中的按钮绑定事件
        /// </summary>
        /// <param name="element">预览元素</param>
        /// <param name="filePath">文件路径</param>
        private void AttachPreviewButtonEvents(UIElement element, string filePath)
        {
            // 递归查找所有按钮并绑定事件
            if (element == null) return;

            var allElements = FindVisualChildren<Button>(element).ToList();

            foreach (var button in allElements)
            {
                if (button.Tag is string tagValue)
                {
                    // 检查是否是"打开文件夹"按钮
                    if (tagValue.StartsWith("OpenFolder:"))
                    {
                        string folderPath = tagValue.Length > "OpenFolder:".Length
                            ? tagValue.Substring("OpenFolder:".Length)
                            : "";

                        // 清除可能存在的旧事件处理程序
                        button.Click -= Button_OpenFolderClick;

                        // 创建新的事件处理程序
                        RoutedEventHandler handler = (s, e) =>
                        {
                            e.Handled = true; // 标记事件已处理

                            try
                            {
                                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                                {
                                    // 在新标签页中打开文件夹
                                    _createTabCallback?.Invoke(folderPath);
                                }
                                else
                                {
                                    MessageBox.Show($"文件夹路径不存在: {folderPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"无法打开文件夹: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        };

                        button.Click += handler;
                        continue;
                    }

                    // 原有的预览区打开按钮逻辑
                    if (tagValue == filePath)
                    {
                        string content = button.Content?.ToString() ?? "";
                        if (content.Contains("预览区打开"))
                        {
                            button.Click -= PreviewButton_Click;
                            button.Click += PreviewButton_Click;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 预览区打开按钮点击处理
        /// </summary>
        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            // 预览区打开按钮点击 - 在预览区中重新加载文件
            var button = sender as Button;
            var filePath = button?.Tag as string;
            if (!string.IsNullOrEmpty(filePath))
            {
                HandlePreviewOpenFileRequest(filePath);
            }
        }

        /// <summary>
        /// 打开文件夹按钮点击处理（占位方法，用于清除事件）
        /// </summary>
        private void Button_OpenFolderClick(object sender, RoutedEventArgs e)
        {
            // 这个方法不会被使用，只是为了能够清除事件
        }

        /// <summary>
        /// 递归查找视觉树中的子元素
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        /// <summary>
        /// 清理PreviewGrid中的预览元素，为编辑器做准备
        /// </summary>
        private void ClearPreviewGridForEditor()
        {
            if (_rightPanel?.PreviewGrid == null) return;

            // 清理PreviewGrid中的其他预览元素（保留DefaultPreviewText和ImagePreviewBorder）
            for (int i = _rightPanel.PreviewGrid.Children.Count - 1; i >= 0; i--)
            {
                var child = _rightPanel.PreviewGrid.Children[i];
                // 保留DefaultPreviewText和ImagePreviewBorder，清除其他元素
                if (child != _rightPanel.DefaultPreviewText && child != _rightPanel.ImagePreviewBorder)
                {
                    _rightPanel.PreviewGrid.Children.RemoveAt(i);
                }
            }
        }

        #endregion
    }
}


