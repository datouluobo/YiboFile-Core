using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OoiMRR.Controls;
using OoiMRR.Services;
using OoiMRR.Services.Navigation;
using TagTrain.UI;

namespace OoiMRR.Handlers
{
    /// <summary>
    /// 文件列表事件处理器
    /// 统一处理文件列表的所有鼠标、键盘事件，参考 Windows 资源管理器行为
    /// </summary>
    public class FileListEventHandler
    {
        private readonly FileBrowserControl _fileBrowser;
        private readonly NavigationCoordinator _navigationCoordinator;
        private readonly Action<FileSystemItem> _showFileInfo;
        private readonly Action<FileSystemItem> _loadFilePreview;
        private readonly Action<FileSystemItem> _loadFileNotes;
        private readonly Action<string> _calculateFolderSizeImmediately;
        private readonly Action _clearPreviewAndInfo;
        private readonly Action<List<TagTrain.Services.TagPredictionResult>> _renderPredictionResults;
        private readonly Func<bool> _isLibraryMode;
        private readonly Action<string> _switchNavigationMode;
        private readonly Action<string> _navigateToPath;
        private readonly Action _navigateBack;
        private readonly Action<GridViewColumn> _autoSizeGridViewColumn;
        private readonly Func<string> _getCurrentPath;
        private readonly Action _copyClick;
        private readonly Action _pasteClick;
        private readonly Action _cutClick;
        private readonly Action _deleteClick;
        private readonly Action _renameClick;
        private readonly Action _refreshClick;
        private readonly Action _showPropertiesClick;
        private readonly Func<object, object> _getTagEditPanel;
        private readonly Func<object, object> _getNavTagContent;

        private System.Windows.Point _mouseDownPoint;
        private bool _isMouseDownOnListView = false;
        private bool _isMouseDownOnColumnHeader = false;

        public FileListEventHandler(
            FileBrowserControl fileBrowser,
            NavigationCoordinator navigationCoordinator,
            Action<FileSystemItem> showFileInfo,
            Action<FileSystemItem> loadFilePreview,
            Action<FileSystemItem> loadFileNotes,
            Action<string> calculateFolderSizeImmediately,
            Action clearPreviewAndInfo,
            Action<List<TagTrain.Services.TagPredictionResult>> renderPredictionResults,
            Func<bool> isLibraryMode,
            Action<string> switchNavigationMode,
            Action<string> navigateToPath,
            Action navigateBack,
            Action<GridViewColumn> autoSizeGridViewColumn,
            Func<string> getCurrentPath,
            Action copyClick,
            Action pasteClick,
            Action cutClick,
            Action deleteClick,
            Action renameClick,
            Action refreshClick,
            Action showPropertiesClick,
            Func<object, object> getTagEditPanel,
            Func<object, object> getNavTagContent)
        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _showFileInfo = showFileInfo ?? throw new ArgumentNullException(nameof(showFileInfo));
            _loadFilePreview = loadFilePreview ?? throw new ArgumentNullException(nameof(loadFilePreview));
            _loadFileNotes = loadFileNotes ?? throw new ArgumentNullException(nameof(loadFileNotes));
            _calculateFolderSizeImmediately = calculateFolderSizeImmediately ?? throw new ArgumentNullException(nameof(calculateFolderSizeImmediately));
            _clearPreviewAndInfo = clearPreviewAndInfo ?? throw new ArgumentNullException(nameof(clearPreviewAndInfo));
            _renderPredictionResults = renderPredictionResults ?? throw new ArgumentNullException(nameof(renderPredictionResults));
            _isLibraryMode = isLibraryMode ?? throw new ArgumentNullException(nameof(isLibraryMode));
            _switchNavigationMode = switchNavigationMode ?? throw new ArgumentNullException(nameof(switchNavigationMode));
            _navigateToPath = navigateToPath ?? throw new ArgumentNullException(nameof(navigateToPath));
            _navigateBack = navigateBack ?? throw new ArgumentNullException(nameof(navigateBack));
            _autoSizeGridViewColumn = autoSizeGridViewColumn ?? throw new ArgumentNullException(nameof(autoSizeGridViewColumn));
            _getCurrentPath = getCurrentPath ?? throw new ArgumentNullException(nameof(getCurrentPath));
            _copyClick = copyClick ?? throw new ArgumentNullException(nameof(copyClick));
            _pasteClick = pasteClick ?? throw new ArgumentNullException(nameof(pasteClick));
            _cutClick = cutClick ?? throw new ArgumentNullException(nameof(cutClick));
            _deleteClick = deleteClick ?? throw new ArgumentNullException(nameof(deleteClick));
            _renameClick = renameClick ?? throw new ArgumentNullException(nameof(renameClick));
            _refreshClick = refreshClick ?? throw new ArgumentNullException(nameof(refreshClick));
            _showPropertiesClick = showPropertiesClick ?? throw new ArgumentNullException(nameof(showPropertiesClick));
            _getTagEditPanel = getTagEditPanel;
            _getNavTagContent = getNavTagContent;
        }

        /// <summary>
        /// 初始化事件绑定
        /// </summary>
        public void Initialize(ListView filesList)
        {
            if (filesList == null) return;

            filesList.SelectionChanged += FilesListView_SelectionChanged;
            filesList.PreviewMouseDoubleClick += FilesListView_PreviewMouseDoubleClick;
            filesList.MouseDoubleClick += FilesListView_MouseDoubleClick;
            filesList.PreviewMouseLeftButtonDown += FilesListView_PreviewMouseLeftButtonDown;
            filesList.PreviewMouseDown += FilesListView_PreviewMouseDown;
            filesList.MouseLeftButtonUp += FilesListView_MouseLeftButtonUp;
            filesList.PreviewMouseDoubleClick += FilesListView_PreviewMouseDoubleClickForBlank;
            filesList.PreviewKeyDown += FilesListView_PreviewKeyDown;
        }

        private void FilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
            {
                _showFileInfo(selectedItem);
                _loadFilePreview(selectedItem);
                _loadFileNotes(selectedItem);

                // 标签页：对图片执行AI预测并渲染到预测面板
                try
                {
                    var navTagContent = _getNavTagContent?.Invoke(null);
                    if (navTagContent != null && navTagContent is FrameworkElement navTag && navTag.Visibility == Visibility.Visible)
                    {
                        var ext = System.IO.Path.GetExtension(selectedItem.Path)?.ToLowerInvariant();
                        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tiff", ".tif" };
                        if (!selectedItem.IsDirectory && !string.IsNullOrEmpty(ext) && imageExtensions.Contains(ext))
                        {
                            var tagEditPanel = _getTagEditPanel?.Invoke(null);
                            if (tagEditPanel != null && tagEditPanel is TagPanel panel)
                            {
                                panel.PredictionPanel.Children.Clear();
                                panel.NoPredictionText.Visibility = Visibility.Visible;
                                panel.NoPredictionText.Text = "预测中...";
                            }

                            Task.Run(() =>
                            {
                                var preds = OoiMRRIntegration.PredictTagsForImage(selectedItem.Path) ?? new List<TagTrain.Services.TagPredictionResult>();
                                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    _renderPredictionResults(preds);
                                }), System.Windows.Threading.DispatcherPriority.Background);
                            });
                        }
                        else
                        {
                            _renderPredictionResults(new List<TagTrain.Services.TagPredictionResult>());
                        }
                    }
                }
                catch { }

                // 如果选中的是文件夹且大小未计算，立即计算
                if (selectedItem.IsDirectory)
                {
                    if (string.IsNullOrEmpty(selectedItem.Size) ||
                        selectedItem.Size == "-" ||
                        selectedItem.Size == "计算中...")
                    {
                        _calculateFolderSizeImmediately(selectedItem.Path);
                    }
                }
            }
            else
            {
                _clearPreviewAndInfo();
            }
        }

        private void FilesListView_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 如果双击发生在列头或分隔线，拦截，不进行文件/文件夹打开
            var src = e.OriginalSource as DependencyObject;
            if (src != null)
            {
                if (FindAncestor<GridViewColumnHeader>(src) != null ||
                    FindAncestor<System.Windows.Controls.Primitives.Thumb>(src) != null)
                {
                    e.Handled = true;
                    return;
                }
            }
            // Preview 事件优先处理
            HandleDoubleClick(e);
        }

        private void FilesListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 如果双击发生在列头或分隔线，拦截
            var src = e.OriginalSource as DependencyObject;
            if (src != null)
            {
                if (FindAncestor<GridViewColumnHeader>(src) != null ||
                    FindAncestor<System.Windows.Controls.Primitives.Thumb>(src) != null)
                {
                    e.Handled = true;
                    return;
                }
            }
            // 备用处理（如果 Preview 事件没有被处理）
            HandleDoubleClick(e);
        }

        private void HandleDoubleClick(MouseButtonEventArgs e)
        {
            if (_fileBrowser?.FilesList == null) return;
            var hitResult = VisualTreeHelper.HitTest(_fileBrowser.FilesList, e.GetPosition(_fileBrowser.FilesList));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != _fileBrowser.FilesList)
            {
                if (current is System.Windows.Controls.ListViewItem item)
                {
                    if (item.Content is FileSystemItem selectedItem)
                    {
                        if (selectedItem.IsDirectory)
                        {
                            if (_isLibraryMode())
                            {
                                _switchNavigationMode("Path");
                            }

                            // 双击时，只有中键才在新标签页打开，Ctrl+左键双击应该正常打开（不阻止多选）
                            // 检查是否是中键双击
                            if (e.ChangedButton == MouseButton.Middle)
                            {
                                _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.MiddleClick);
                            }
                            else
                            {
                                // 普通双击或Ctrl+左键双击，都在当前标签页打开
                                _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.LeftClick);
                            }
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = selectedItem.Path,
                                    UseShellExecute = true
                                });
                                e.Handled = true;
                                return;
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
                current = VisualTreeHelper.GetParent(current);
            }

            // 备用：使用选中项
            if (_fileBrowser?.FilesSelectedItem is FileSystemItem backupItem)
            {
                if (backupItem.IsDirectory)
                {
                    if (Directory.Exists(backupItem.Path))
                    {
                        if (_isLibraryMode())
                        {
                            _switchNavigationMode("Path");
                        }
                        _navigateToPath(backupItem.Path);
                        e.Handled = true;
                    }
                }
                else
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = backupItem.Path,
                            UseShellExecute = true
                        });
                        e.Handled = true;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void FilesListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
            {
                _isMouseDownOnListView = false;
                return;
            }

            // 支持双击列头分隔线（Thumb）自动适配列宽
            if (e.ClickCount == 2)
            {
                var src = e.OriginalSource as DependencyObject;
                if (src != null)
                {
                    var header = FindAncestor<GridViewColumnHeader>(src);
                    var thumb = FindAncestor<System.Windows.Controls.Primitives.Thumb>(src);
                    if (header != null && thumb != null && header.Column != null)
                    {
                        _autoSizeGridViewColumn(header.Column);
                        e.Handled = true;
                        return;
                    }
                }
            }

            System.Windows.Point hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);

            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;
                int depth = 0;
                while (current != null && current != listView && depth < 10)
                {
                    if (current is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }

                    if (current.GetType().Name.Contains("Thumb") || current.GetType().Name == "Thumb")
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }

                    var parent = VisualTreeHelper.GetParent(current);
                    if (parent is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }

                    current = parent;
                    depth++;
                }

                if (listView.View is GridView gridView && gridView.Columns.Count > 0)
                {
                    if (hitPoint.Y < 30)
                    {
                        _isMouseDownOnListView = false;
                        _isMouseDownOnColumnHeader = true;
                        return;
                    }
                }
            }

            _mouseDownPoint = e.GetPosition(listView);
            _isMouseDownOnListView = true;
            _isMouseDownOnColumnHeader = false;

            bool isListViewItem = false;
            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listView)
                {
                    if (current is System.Windows.Controls.ListViewItem)
                    {
                        isListViewItem = true;
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            // 只有在点击空白区域且没有按 Ctrl/Shift 时才清除选择
            // Ctrl+左键和 Shift+左键用于多选，不应该清除选择
            if (!isListViewItem && e.ChangedButton == MouseButton.Left)
            {
                // 检查是否按了 Ctrl 或 Shift（用于多选）
                bool isMultiSelect = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control ||
                                     (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                
                if (!isMultiSelect)
                {
                    listView.SelectedItem = null;
                    listView.SelectedItems.Clear();
                }
            }
            
            // 重要：不要阻止 Ctrl+左键的默认多选行为
            // ListView 默认支持 Ctrl+左键多选，我们不应该设置 e.Handled = true
        }

        private void FilesListView_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 只处理中键点击（打开新标签页），Ctrl+左键用于多选，不在这里处理
            if (e.ChangedButton != MouseButton.Middle) return;

            var listView = sender as ListView;
            if (listView == null) return;

            var hitResult = VisualTreeHelper.HitTest(listView, e.GetPosition(listView));
            if (hitResult == null) return;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listView)
            {
                if (current is System.Windows.Controls.ListViewItem item)
                {
                    if (item.Content is FileSystemItem selectedItem)
                    {
                        if (selectedItem.IsDirectory)
                        {
                            // 中键点击：在新标签页打开文件夹
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.MiddleClick);
                            e.Handled = true;
                            return;
                        }
                    }
                    break;
                }
                current = VisualTreeHelper.GetParent(current);
            }
        }

        private void FilesListView_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isMouseDownOnColumnHeader)
            {
                _isMouseDownOnColumnHeader = false;
                _isMouseDownOnListView = false;
                return;
            }

            if (!_isMouseDownOnListView)
                return;

            var listView = sender as ListView;
            if (listView == null)
            {
                _isMouseDownOnListView = false;
                return;
            }

            var originalSource = e.OriginalSource as DependencyObject;
            DependencyObject checkSource = originalSource;
            while (checkSource != null)
            {
                if (checkSource is GridViewColumnHeader)
                {
                    _isMouseDownOnListView = false;
                    return;
                }
                if (checkSource.GetType().Name.Contains("Thumb") || checkSource.GetType().Name == "Thumb")
                {
                    _isMouseDownOnListView = false;
                    return;
                }
                checkSource = VisualTreeHelper.GetParent(checkSource);
            }

            System.Windows.Point mouseUpPoint = e.GetPosition(listView);
            double distance = Math.Sqrt(Math.Pow(mouseUpPoint.X - _mouseDownPoint.X, 2) +
                                      Math.Pow(mouseUpPoint.Y - _mouseDownPoint.Y, 2));

            if (distance > SystemParameters.MinimumHorizontalDragDistance)
            {
                _isMouseDownOnListView = false;
                return;
            }

            if (mouseUpPoint.Y < 30)
            {
                _isMouseDownOnListView = false;
                return;
            }

            System.Windows.Point hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);

            if (hitResult != null)
            {
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listView)
                {
                    if (current is ListViewItem)
                    {
                        _isMouseDownOnListView = false;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                current = hitResult.VisualHit;
                while (current != null)
                {
                    if (current is GridViewColumnHeader)
                    {
                        _isMouseDownOnListView = false;
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }

                if (listView.SelectedItems.Count > 0)
                {
                    listView.SelectedItems.Clear();
                }
            }

            _isMouseDownOnListView = false;
            _isMouseDownOnColumnHeader = false;
        }

        private void FilesListView_PreviewMouseDoubleClickForBlank(object sender, MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null) return;
            var thumbAncestor = FindAncestor<System.Windows.Controls.Primitives.Thumb>(originalSource);
            if (thumbAncestor != null)
            {
                var thumbHeader = FindAncestor<GridViewColumnHeader>(originalSource);
                if (thumbHeader?.Column != null)
                {
                    _autoSizeGridViewColumn(thumbHeader.Column);
                    e.Handled = true;
                    return;
                }
            }

            var header = FindAncestor<GridViewColumnHeader>(originalSource);
            if (header != null)
            {
                e.Handled = true;
                return;
            }

            var listViewItem = FindAncestor<ListViewItem>(originalSource);
            if (listViewItem != null) return;

            if (_fileBrowser?.FilesList == null) return;
            var hitResult = VisualTreeHelper.HitTest(_fileBrowser.FilesList, e.GetPosition(_fileBrowser.FilesList));
            if (hitResult != null && FindAncestor<ListViewItem>(hitResult.VisualHit) != null)
                return;

            var currentPath = _getCurrentPath();
            if (!string.IsNullOrEmpty(currentPath) && Directory.Exists(currentPath))
            {
                var parentPath = Directory.GetParent(currentPath);
                if (parentPath != null)
                {
                    _navigateToPath(parentPath.FullName);
                    e.Handled = true;
                }
            }
        }

        private void FilesListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
                return;

            // Ctrl+A - 全选
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                listView.SelectAll();
                e.Handled = true;
                return;
            }

            // Ctrl+C - 复制
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _copyClick();
                e.Handled = true;
                return;
            }

            // Ctrl+V - 粘贴
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _pasteClick();
                e.Handled = true;
                return;
            }

            // Ctrl+X - 剪切
            if (e.Key == Key.X && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _cutClick();
                e.Handled = true;
                return;
            }

            // Delete - 删除
            if (e.Key == Key.Delete)
            {
                _deleteClick();
                e.Handled = true;
                return;
            }

            // F2 - 重命名
            if (e.Key == Key.F2)
            {
                _renameClick();
                e.Handled = true;
                return;
            }

            // F5 - 刷新
            if (e.Key == Key.F5)
            {
                _refreshClick();
                e.Handled = true;
                return;
            }

            // Alt+Enter - 属性
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                _showPropertiesClick();
                e.Handled = true;
                return;
            }

            // Backspace - 返回上一级
            if (e.Key == Key.Back)
            {
                _navigateBack();
                e.Handled = true;
                return;
            }

            // 处理方向键
            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Home || e.Key == Key.End)
            {
                if (listView.Items.Count == 0)
                    return;

                int currentIndex = listView.SelectedIndex;
                var wrapPanel = FindDescendant<WrapPanel>(listView);
                bool isTilesView = listView.View == null && wrapPanel != null;
                int columns = 1;
                if (isTilesView)
                {
                    double itemWidth = wrapPanel.ItemWidth > 0 ? wrapPanel.ItemWidth : (wrapPanel.Children.Count > 0 ? ((FrameworkElement)wrapPanel.Children[0]).ActualWidth : 160);
                    double viewportWidth = wrapPanel.ActualWidth > 0 ? wrapPanel.ActualWidth : listView.ActualWidth;
                    columns = Math.Max(1, (int)Math.Floor(viewportWidth / Math.Max(1, itemWidth)));
                }

                if (e.Key == Key.Down)
                {
                    if (isTilesView)
                    {
                        int next = currentIndex + columns;
                        if (next < listView.Items.Count)
                        {
                            listView.SelectedIndex = next;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                        else
                        {
                            listView.SelectedIndex = listView.Items.Count - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (currentIndex < listView.Items.Count - 1)
                        {
                            listView.SelectedIndex = currentIndex + 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    if (isTilesView)
                    {
                        int prev = currentIndex - columns;
                        if (prev >= 0)
                        {
                            listView.SelectedIndex = prev;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                        else
                        {
                            listView.SelectedIndex = 0;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (currentIndex > 0)
                        {
                            listView.SelectedIndex = currentIndex - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Home)
                {
                    listView.SelectedIndex = 0;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.End)
                {
                    listView.SelectedIndex = listView.Items.Count - 1;
                    listView.ScrollIntoView(listView.SelectedItem);
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    if (isTilesView)
                    {
                        if (currentIndex > 0)
                        {
                            listView.SelectedIndex = currentIndex - 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        _navigateBack();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    if (isTilesView)
                    {
                        if (currentIndex < listView.Items.Count - 1)
                        {
                            listView.SelectedIndex = currentIndex + 1;
                            listView.ScrollIntoView(listView.SelectedItem);
                        }
                    }
                    else
                    {
                        if (listView.SelectedItem is FileSystemItem selectedItem && selectedItem.IsDirectory)
                        {
                            if (_isLibraryMode())
                            {
                                _switchNavigationMode("Path");
                            }
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.LeftClick);
                        }
                    }
                    e.Handled = true;
                }
            }
            // 处理 Enter 键打开文件/文件夹
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (_fileBrowser?.FilesSelectedItem is FileSystemItem selectedItem)
                {
                    if (selectedItem.IsDirectory)
                    {
                        if (_isLibraryMode())
                        {
                            _switchNavigationMode("Path");
                        }
                        _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationCoordinator.NavigationSource.FileList, NavigationCoordinator.ClickType.LeftClick);
                    }
                    else
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
                e.Handled = true;
            }
        }

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private T FindDescendant<T>(DependencyObject d) where T : DependencyObject
        {
            if (d == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(d);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(d, i);
                if (child is T t) return t;
                var deeper = FindDescendant<T>(child);
                if (deeper != null) return deeper;
            }
            return null;
        }
    }
}

