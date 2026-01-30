using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Controls;
using YiboFile.Services;
using YiboFile.Services.Core;
using YiboFile.Services.Navigation;


namespace YiboFile.Handlers
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
        private readonly Action<string, bool, bool?> _createTabAction;
        private readonly PaneId _paneId;


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
            Action<string, bool, bool?> createTabAction, // Added explicit CreateTab action
            PaneId paneId = PaneId.Main)

        {
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _navigationCoordinator = navigationCoordinator ?? throw new ArgumentNullException(nameof(navigationCoordinator));
            _showFileInfo = showFileInfo ?? throw new ArgumentNullException(nameof(showFileInfo));
            _loadFilePreview = loadFilePreview ?? throw new ArgumentNullException(nameof(loadFilePreview));
            _loadFileNotes = loadFileNotes ?? throw new ArgumentNullException(nameof(loadFileNotes));
            _calculateFolderSizeImmediately = calculateFolderSizeImmediately ?? throw new ArgumentNullException(nameof(calculateFolderSizeImmediately));
            _clearPreviewAndInfo = clearPreviewAndInfo ?? throw new ArgumentNullException(nameof(clearPreviewAndInfo));

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
            _createTabAction = createTabAction ?? throw new ArgumentNullException(nameof(createTabAction));
            _paneId = paneId;

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

                // 标签页AI预测已移除 - Phase 2将重新实现
                // try { ... } catch { }

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
                        // Special Handling for Library Cards
                        if (selectedItem.Type == "Lib")
                        {
                            _navigateToPath(selectedItem.Path);
                            e.Handled = true;
                            return;
                        }

                        if (selectedItem.IsDirectory)
                        {
                            // 文件夹导航：全部交给中枢处理。中枢会自动根据点击类型识别是否需要开新标签。
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationSource.FolderClick, NavigationCoordinator.GetClickType(e), pane: _paneId);
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            HandleFileOpen(selectedItem);
                            e.Handled = true;
                            return;
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
                    HandleFileOpen(backupItem);
                    e.Handled = true;
                }
            }
        }

        private void HandleFileOpen(FileSystemItem item)
        {
            // Special handling for execution of files inside archive
            if (ProtocolManager.Parse(item.Path).Type == ProtocolType.Archive)
            {
                MessageBox.Show("暂不支持直接打开压缩包内的文件。\n请先解压后再试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // CHECK FOR ARCHIVE FILE
            var ext = Path.GetExtension(item.Path);
            if (!string.IsNullOrEmpty(ext))
            {
                var extLower = ext.ToLowerInvariant();
                if (extLower == ".zip" || extLower == ".7z" || extLower == ".rar" || extLower == ".tar" || extLower == ".gz")
                {
                    // Navigate into archive
                    // Format: zip://PathToZip|
                    string archiveUrl = $"{ProtocolManager.ZipProtocol}{item.Path}|";
                    // If the path itself is a virtual path (e.g. inside a zip), use it as is?
                    // Actually, if we are inside a zip, item.Path is zip://.../foo.zip
                    // We want zip://zip://.../foo.zip|
                    // Wait, our ProtocolManager handles nested?
                    // ArchiveService expects standard path or zip:// path.
                    _navigateToPath(archiveUrl);
                    return;
                }
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // 确保点击列表（包括空白区域）时获取焦点
            // 这解决了副地址栏点击空白处无法退出编辑模式的问题
            if (!listView.IsFocused)
            {
                listView.Focus();
            }

            // 显式退出地址栏编辑模式（如果存在）
            // 这是一个双重保障，防止 Focus() 无法触发 AddressBar 的 LostFocus
            var fileBrowser = FindAncestor<FileBrowserControl>(listView);
            if (fileBrowser != null && fileBrowser.AddressBarControl != null)
            {
                if (fileBrowser.AddressBarControl.IsEditMode)
                {
                    fileBrowser.AddressBarControl.SwitchToBreadcrumbMode();
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
                            // 中键点击：交给中枢处理
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationSource.FolderClick, NavigationCoordinator.GetClickType(e), pane: _paneId);
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
            if (!string.IsNullOrEmpty(currentPath))
            {
                // Support both directories and virtual archive paths
                if (Directory.Exists(currentPath) || ProtocolManager.IsVirtual(currentPath))
                {
                    // Use _navigationCoordinator logic if possible, or simple parent logic
                    // But here we need to Navigate Up.
                    // If we depend on MainWindow handler, we can just return and let it bubble?
                    // But typically we handle it here.

                    // Actually, let's just delegate to the bound action if possible, OR fix the logic.
                    // Since specific NavigateUp logic is complex for archives, we rely on the _navigateBack or similar?
                    // No, _navigateBack is History Back.
                    // We need 'Up'.

                    // The simplest fix: If it's an archive, we invoke the 'Up' logic via the event bubbling 
                    // to MainWindow? 
                    // Wait, if we set Handled=true here, we MUST do the work.
                    // If we DON'T set Handled=true, MainWindow handler (from FileBrowserControl) will catch it.

                    // So, if it is a directory, we handle it. If it is an archive, we ALSO handle it?
                    // Let's rely on event bubbling for 'Smart' Up navigation if we can't contextually decide here.
                    // BUT previous code handled it for Directories.

                    if (ProtocolManager.Parse(currentPath).Type == ProtocolType.Archive)
                    {
                        // Let it bubble to MainWindow which has access to NavigationService.NavigateUp()
                        return;
                    }

                    var parentPath = Directory.GetParent(currentPath);
                    if (parentPath != null)
                    {
                        _navigateToPath(parentPath.FullName);
                        e.Handled = true;
                    }
                }
            }
        }

        private void FilesListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null)
                return;

            // 如果正在重命名，跳过所有快捷键处理，让 TextBox 处理输入
            if (_fileBrowser?.FilesSelectedItem is FileSystemItem renamingItem && renamingItem.IsRenaming)
            {
                // Enter 和 Escape 由 TextBox 的 KeyDown 处理
                // 其他键（包括 Ctrl+A, Ctrl+C 等）也传递给 TextBox
                return;
            }

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
                            _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationSource.FolderClick, ClickType.LeftClick);
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
                    // 如果正在重命名，不拦截 Enter 键，让 TextBox 处理
                    if (selectedItem.IsRenaming)
                    {
                        return; // 不设置 e.Handled，让事件继续传播到 TextBox
                    }

                    if (selectedItem.IsDirectory)
                    {
                        _navigationCoordinator.HandlePathNavigation(selectedItem.Path, NavigationSource.FolderClick, ClickType.LeftClick);
                    }
                    else
                    {
                        // 检查是否为归档文件或其他特殊协议
                        var protocolInfo = Services.Core.ProtocolManager.Parse(selectedItem.Path);
                        if (protocolInfo.Type == Services.Core.ProtocolType.Archive)
                        {
                            MessageBox.Show("暂不支持直接打开压缩包内的文件。\n请先解压后再试。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                            e.Handled = true;
                            return;
                        }

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


