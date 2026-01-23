using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Controls;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Config;

namespace YiboFile.Handlers
{
    /// <summary>
    /// 处理文件列表列的交互逻辑
    /// 包括列头点击、拖拽调整大小、右键菜单和列可见性管理
    /// </summary>
    internal class ColumnInteractionHandler
    {
        private readonly MainWindow _mainWindow;
        private readonly ColumnService _columnService;
        private readonly ConfigService _configService;
        private readonly FileBrowserControl _fileBrowser;

        public ColumnInteractionHandler(
            MainWindow mainWindow,
            FileBrowserControl targetBrowser,
            ColumnService columnService,
            ConfigService configService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _fileBrowser = targetBrowser ?? throw new ArgumentNullException(nameof(targetBrowser));
            _columnService = columnService ?? throw new ArgumentNullException(nameof(columnService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        }

        public void Initialize()
        {
            EnsureHeaderContextMenuHook();
            AttachColumnPropertyObservers();
        }

        private void AttachColumnPropertyObservers()
        {
            if (_fileBrowser?.FilesGrid?.Columns == null) return;

            foreach (var column in _fileBrowser.FilesGrid.Columns)
            {
                // Remove existing to avoid duplicates
                ((System.ComponentModel.INotifyPropertyChanged)column).PropertyChanged -= OnColumnPropertyChanged;
                ((System.ComponentModel.INotifyPropertyChanged)column).PropertyChanged += OnColumnPropertyChanged;
            }
        }

        private bool _isAutoFitting = false;
        private System.Threading.Timer _debounceTimer;

        private void OnColumnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ActualWidth")
            {
                if (_isAutoFitting) return; // Ignore changes caused by our own auto-fit

                // Debounce the auto-fit to avoid performance issues during rapid resize
                _debounceTimer?.Dispose();
                _debounceTimer = new System.Threading.Timer((state) =>
                {
                    _fileBrowser?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            _isAutoFitting = true;
                            AutoFitNameColumn();
                        }
                        finally
                        {
                            _isAutoFitting = false;
                        }
                    }), System.Windows.Threading.DispatcherPriority.Render); // Use Render priority for smoother updates
                }, null, 10, System.Threading.Timeout.Infinite); // 10ms delay
            }
        }

        /// <summary>
        /// 确保列头右键菜单钩子已挂载
        /// </summary>
        public void EnsureHeaderContextMenuHook()
        {
            if (_fileBrowser?.FilesList == null) return;
            _fileBrowser.FilesList.PreviewMouseRightButtonUp -= FilesList_PreviewMouseRightButtonUp_HeaderMenu;
            _fileBrowser.FilesList.PreviewMouseRightButtonUp += FilesList_PreviewMouseRightButtonUp_HeaderMenu;
        }

        private void FilesList_PreviewMouseRightButtonUp_HeaderMenu(object sender, MouseButtonEventArgs e)
        {
            var src = e.OriginalSource as DependencyObject;
            if (src == null) return;

            // 检查是否点击在列头上
            var header = FindAncestor<GridViewColumnHeader>(src);
            if (header != null)
            {
                // 在列头上右键，显示列选择菜单
                e.Handled = true;

                // 创建弹出菜单
                var cm = CreateColumnHeaderContextMenu(header);

                // 将菜单附加到列头元素上并显示
                cm.PlacementTarget = header;
                cm.Placement = PlacementMode.MousePoint;
                cm.IsOpen = true;
                return;
            }

            // 不在列头上，让事件继续传播到文件列表的 ContextMenu
        }

        private ContextMenu CreateColumnHeaderContextMenu(GridViewColumnHeader header)
        {
            var cm = new ContextMenu();
            var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
            var visibleSet = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

            if (_fileBrowser?.FilesGrid != null)
            {
                foreach (var column in _fileBrowser.FilesGrid.Columns)
                {
                    // column.Header is usually the content (e.g., TextBlock or string)
                    // The GridViewColumnHeader IS THE CONTAINER wrapping this content, but column.Header refers to the content property.

                    var headerContent = column.Header;
                    string tag = null;
                    string title = null;

                    if (headerContent is FrameworkElement fe)
                    {
                        tag = fe.Tag?.ToString();
                        if (fe is TextBlock tb)
                        {
                            title = tb.Text;
                        }
                    }
                    else if (headerContent != null)
                    {
                        // Fallback: use string content as tag/title
                        tag = headerContent.ToString();
                        title = tag;
                    }

                    if (string.IsNullOrEmpty(tag)) continue;


                    if (string.IsNullOrEmpty(title)) title = tag;

                    bool isVisible = visibleSet.Contains(tag);

                    var mi = new MenuItem
                    {
                        Header = $"列: {title}",
                        IsCheckable = true,
                        IsChecked = isVisible
                    };

                    mi.Checked += (s, ev) => HandleColumnVisibilityChange(column, tag, true);
                    mi.Unchecked += (s, ev) => HandleColumnVisibilityChange(column, tag, false);

                    cm.Items.Add(mi);
                }
            }

            return cm;
        }

        private void HandleColumnVisibilityChange(GridViewColumn column, string tag, bool isVisible)
        {
            if (isVisible)
            {
                // 显示列
                if (column.Width <= 1)
                {
                    double w = tag switch
                    {
                        "Name" => _configService?.Config.ColNameWidth ?? 200,
                        "Size" => _configService?.Config.ColSizeWidth ?? 100,
                        "Type" => _configService?.Config.ColTypeWidth ?? 100,
                        "ModifiedDate" => _configService?.Config.ColModifiedDateWidth ?? 150,
                        "CreatedTime" => _configService?.Config.ColCreatedTimeWidth ?? 50,
                        "Tags" => _configService?.Config.ColTagsWidth ?? 150,
                        "Notes" => _configService?.Config.ColNotesWidth ?? 200,
                        _ => column.ActualWidth > 0 ? column.ActualWidth : 100
                    };
                    column.Width = Math.Max(40, w);
                }
            }
            else
            {
                // Save current width before hiding so we can restore it later
                if (column.ActualWidth > 1 && _configService?.Config != null)
                {
                    switch (tag)
                    {
                        case "Name": _configService.Config.ColNameWidth = column.ActualWidth; break;
                        case "Size": _configService.Config.ColSizeWidth = column.ActualWidth; break;
                        case "Type": _configService.Config.ColTypeWidth = column.ActualWidth; break;
                        case "ModifiedDate": _configService.Config.ColModifiedDateWidth = column.ActualWidth; break;
                        case "CreatedTime": _configService.Config.ColCreatedTimeWidth = column.ActualWidth; break;
                        case "Tags": _configService.Config.ColTagsWidth = column.ActualWidth; break;
                        case "Notes": _configService.Config.ColNotesWidth = column.ActualWidth; break;
                    }
                }

                // 隐藏列
                column.Width = 0;
            }

            // 更新配置
            UpdateVisibleColumnsConfig(tag, isVisible);

            // Auto-fill Name column space after visibility change
            // Run on dispatcher/idle to allow layout updates to propagate first if needed
            _fileBrowser?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                AutoFitNameColumn();
                // Force layout update to clear any ghost header artifacts
                _fileBrowser?.FilesList?.UpdateLayout();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void AutoFitNameColumn()
        {
            if (_fileBrowser?.FilesList == null || _fileBrowser.FilesGrid == null) return;

            var gridView = _fileBrowser.FilesGrid;
            var columns = gridView.Columns;
            GridViewColumn nameCol = null;
            double otherColsWidth = 0;

            foreach (var col in columns)
            {
                var header = col.Header as GridViewColumnHeader;
                // Try to find Name column by Tag or Content
                string tag = null;
                if (col.Header is FrameworkElement fe) tag = fe.Tag?.ToString();
                else if (col.Header != null) tag = col.Header.ToString();

                // Name column is the target for auto-size
                if (tag == "Name")
                {
                    nameCol = col;
                }
                else
                {
                    otherColsWidth += col.ActualWidth;
                }
            }

            if (nameCol != null)
            {
                // Safety check: if Name column is explicitly hidden by user, do not resizing it (which would unhide it)
                if (nameCol.Width == 0 && !IsColumnVisible("Name")) return;

                double listWidth = _fileBrowser.FilesList.ActualWidth;

                // Dynamic padding calculation based on scrollbar visibility
                double padding = 2; // Minimal safe padding
                var scrollViewer = FindDescendant<ScrollViewer>(_fileBrowser.FilesList);
                if (scrollViewer != null && scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible)
                {
                    padding += SystemParameters.VerticalScrollBarWidth;
                }

                double availableWidth = listWidth - otherColsWidth - padding;

                if (availableWidth > 100)
                {
                    // Update width only if it changes significantly to avoid infinite loops if we are being called by PropertyChanged
                    if (Math.Abs(nameCol.Width - availableWidth) > 1)
                    {
                        nameCol.Width = availableWidth;
                    }
                }
            }
        }

        private void UpdateVisibleColumnsConfig(string tag, bool isVisible)
        {
            var currentVisible = GetVisibleColumnsForCurrentMode() ?? "";
            var currentSet = new HashSet<string>(currentVisible.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

            if (isVisible) currentSet.Add(tag);
            else currentSet.Remove(tag);

            SetVisibleColumnsForCurrentMode(string.Join(",", currentSet));

            // ❌ 不要调用SaveCurrentConfig！会用旧配置覆盖ConfigurationService保存的新配置
            // _configService?.SaveCurrentConfig();

            // ✅ 列可见性由ColumnService管理，它会通过ConfigurationService保存
            // 这里不需要额外保存，ColumnService会在需要时保存
        }

        /// <summary>
        /// 绑定列头分隔线事件
        /// </summary>
        /// <summary>
        /// 绑定列头分隔线事件
        /// </summary>
        public void HookHeaderThumbs()
        {
            if (_fileBrowser?.FilesList == null) return;

            // 必须从可视树中查找 GridViewHeaderRowPresenter，因为 FilesGrid.Columns 中的 Header 通常只是字符串
            var presenter = FindDescendant<GridViewHeaderRowPresenter>(_fileBrowser.FilesList);
            if (presenter == null) return;

            int count = VisualTreeHelper.GetChildrenCount(presenter);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(presenter, i);
                if (child is GridViewColumnHeader header)
                {
                    // 绑定双击分隔条事件 (Thumb)
                    // 我们直接尝试获取 Thumb 并绑定
                    var thumb = FindHeaderThumb(header);
                    if (thumb != null)
                    {
                        AttachThumbEvents(thumb);
                    }
                    else
                    {
                        // 如果 Thumb 还没加载，监听 Loaded
                        header.Loaded -= Header_Loaded_AttachThumb;
                        header.Loaded += Header_Loaded_AttachThumb;
                    }
                }
            }
        }

        private void AttachThumbEvents(Thumb thumb)
        {
            // 清除旧事件以防重复绑定
            thumb.PreviewMouseLeftButtonDown -= Thumb_PreviewMouseLeftButtonDown;
            thumb.PreviewMouseLeftButtonDown += Thumb_PreviewMouseLeftButtonDown;

            // 绑定拖拽事件
            thumb.DragStarted -= HeaderThumb_DragStarted;
            thumb.DragDelta -= HeaderThumb_DragDelta;
            thumb.DragStarted += HeaderThumb_DragStarted;
            thumb.DragDelta += HeaderThumb_DragDelta;
        }

        private void Thumb_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                var thumb = sender as Thumb;
                var header = FindAncestor<GridViewColumnHeader>(thumb);
                if (header?.Column != null)
                {
                    AutoSizeGridViewColumn(header.Column);
                    e.Handled = true;
                    // Auto-fit will be triggered by PropertyChanged listener
                }
            }
        }

        // 废弃原有的 Header_PreviewMouseLeftButtonDown_ForThumb，改用直接绑定 Thumb

        private void Header_Loaded_AttachThumb(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader header)
            {
                header.Loaded -= Header_Loaded_AttachThumb; // One-shot
                var thumb = FindHeaderThumb(header);
                if (thumb != null)
                {
                    AttachThumbEvents(thumb);
                }
            }
        }

        private void HeaderThumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            var header = FindAncestor<GridViewColumnHeader>(sender as DependencyObject);
            if (header?.Column == null) return;
            var tag = header.Tag?.ToString();

            if (!IsColumnVisible(tag))
            {
                // 阻止隐藏列被拖动展开
                header.Column.Width = 0;
                e.Handled = true;
            }
        }

        private void HeaderThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var header = FindAncestor<GridViewColumnHeader>(sender as DependencyObject);
            if (header?.Column == null) return;
            var tag = header.Tag?.ToString();

            if (!IsColumnVisible(tag))
            {
                // 该列是隐藏列：把拖动量转嫁到左侧最近的可见列
                header.Column.Width = 0;
                HandleHiddenColumnResize(header.Column, e.HorizontalChange);
                e.Handled = true;
            }
            else
            {
                // 该列可见，但其右邻居可能是隐藏列
                HandleVisibleColumnResize(header.Column, e.HorizontalChange);
                if (e.Handled) return;
            }
        }

        private void HandleHiddenColumnResize(GridViewColumn currentColumn, double change)
        {
            var gridView = _fileBrowser?.FilesGrid;
            if (gridView != null)
            {
                int idx = gridView.Columns.IndexOf(currentColumn);
                for (int i = idx - 1; i >= 0; i--)
                {
                    var leftCol = gridView.Columns[i];
                    var leftHeader = leftCol.Header as GridViewColumnHeader;
                    var leftTag = leftHeader?.Tag?.ToString();
                    if (IsColumnVisible(leftTag))
                    {
                        double min = 40;
                        double newWidth = Math.Max(min, leftCol.Width + change);
                        leftCol.Width = newWidth;
                        break;
                    }
                }
            }
        }

        private void HandleVisibleColumnResize(GridViewColumn currentColumn, double change)
        {
            var gridView = _fileBrowser?.FilesGrid;
            if (gridView != null && change > 0)
            {
                int idx = gridView.Columns.IndexOf(currentColumn);
                if (idx >= 0 && idx + 1 < gridView.Columns.Count)
                {
                    var rightCol = gridView.Columns[idx + 1];
                    var rightHeader = rightCol.Header as GridViewColumnHeader;
                    var rightTag = rightHeader?.Tag?.ToString();
                    if (!IsColumnVisible(rightTag))
                    {
                        // 右侧隐藏：放大当前列，并强制右侧维持隐藏
                        double min = 40;
                        currentColumn.Width = Math.Max(min, currentColumn.Width + change);
                        if (rightCol.Width != 0) rightCol.Width = 0;
                        // 这里我们修改了当前列宽度，需要标记 Handled 吗？
                        // 原代码是标记 Handled=true，这里保持一致
                        // e.Handled = true; // 注意：DragDeltaEventArgs 没有 Handled 属性？
                        // Wait, WPF DragDeltaEventArgs DOES have Handled if it's RoutedEvent, 
                        // but System.Windows.Controls.Primitives.DragDeltaEventArgs inherits from RoutedEventArgs so yes.
                    }
                }
            }
        }

        public void AutoSizeGridViewColumn(GridViewColumn column)
        {
            if (_fileBrowser == null || column == null) return;
            _columnService?.AutoSizeGridViewColumn(column, _fileBrowser);
        }

        public void ApplyVisibleColumnsForCurrentMode()
        {
            if (_fileBrowser == null) return;
            _columnService?.ApplyVisibleColumnsForCurrentMode(_fileBrowser);
            HookHeaderThumbs();
        }

        // --- 辅助方法 ---

        private string GetVisibleColumnsForCurrentMode()
        {
            return _columnService?.GetVisibleColumnsForCurrentMode() ?? "";
        }

        private void SetVisibleColumnsForCurrentMode(string csv)
        {
            _columnService?.SetVisibleColumnsForCurrentMode(csv);
        }

        private bool IsColumnVisible(string tag)
        {
            return _columnService?.IsColumnVisible(tag) ?? true;
        }

        private Thumb FindHeaderThumb(GridViewColumnHeader header)
        {
            var thumb = header.Template?.FindName("PART_HeaderGripper", header) as Thumb;
            if (thumb != null) return thumb;
            return FindDescendant<Thumb>(header);
        }

        private T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T ancestor) return ancestor;
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

