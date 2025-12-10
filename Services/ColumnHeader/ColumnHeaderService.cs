using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OoiMRR.Services.ColumnHeader
{
    /// <summary>
    /// 列头控制服务
    /// 负责列宽度管理、列显示/隐藏、列头交互和列排序
    /// </summary>
    public class ColumnHeaderService
    {
        private readonly AppConfig _config;
        private readonly Func<string> _getCurrentModeKey;
        private readonly Action<string, bool> _applyColumnVisibilityToFileList;

        // 排序状态
        private string _lastSortColumn = "";
        private bool _sortAscending = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">应用配置对象</param>
        /// <param name="getCurrentModeKey">获取当前导航模式的函数</param>
        /// <param name="applyColumnVisibilityToFileList">应用列可见性到 FileListControl 的函数</param>
        public ColumnHeaderService(
            AppConfig config,
            Func<string> getCurrentModeKey,
            Action<string, bool> applyColumnVisibilityToFileList = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _getCurrentModeKey = getCurrentModeKey ?? throw new ArgumentNullException(nameof(getCurrentModeKey));
            _applyColumnVisibilityToFileList = applyColumnVisibilityToFileList;
        }

        #region 列宽度管理

        /// <summary>
        /// 记住列宽度到配置
        /// </summary>
        public void RememberColumnWidth(string tag, GridViewColumn column)
        {
            if (string.IsNullOrEmpty(tag) || column == null) return;
            var width = column.ActualWidth > 0 ? column.ActualWidth : column.Width;
            if (width <= 0) return;

            switch (tag)
            {
                case "Name":
                    _config.ColNameWidth = width;
                    break;
                case "Size":
                    _config.ColSizeWidth = width;
                    break;
                case "Type":
                    _config.ColTypeWidth = width;
                    break;
                case "ModifiedDate":
                    _config.ColModifiedDateWidth = width;
                    break;
                case "CreatedTime":
                    _config.ColCreatedTimeWidth = width;
                    break;
                case "Tags":
                    _config.ColTagsWidth = width;
                    break;
                case "Notes":
                    _config.ColNotesWidth = width;
                    break;
            }
        }

        /// <summary>
        /// 从配置解析列宽度
        /// </summary>
        public double ResolveColumnWidth(string tag, GridViewColumn column)
        {
            double width = tag switch
            {
                "Name" => _config.ColNameWidth,
                "Size" => _config.ColSizeWidth,
                "Type" => _config.ColTypeWidth,
                "ModifiedDate" => _config.ColModifiedDateWidth,
                "CreatedTime" => _config.ColCreatedTimeWidth,
                "Tags" => _config.ColTagsWidth,
                "Notes" => _config.ColNotesWidth,
                _ => 0
            };

            if (width <= 0 && column != null)
            {
                var fallback = column.ActualWidth > 0 ? column.ActualWidth : column.Width;
                width = fallback > 0 ? fallback : 100;
            }

            return width;
        }

        /// <summary>
        /// 加载列宽度和顺序
        /// </summary>
        public void LoadColumnWidths(GridView gridView)
        {
            if (gridView == null || _config == null) return;

            try
            {
                var columns = gridView.Columns;
                if (columns.Count >= 7)
                {
                    // 创建列名到列的映射
                    var columnMap = new Dictionary<string, GridViewColumn>
                    {
                        { "Name", columns[0] },
                        { "Size", columns[1] },
                        { "Type", columns[2] },
                        { "ModifiedDate", columns[3] },
                        { "CreatedTime", columns[4] },
                        { "Tags", columns[5] },
                        { "Notes", columns[6] }
                    };

                    // 加载保存的列顺序
                    if (!string.IsNullOrEmpty(_config.ColumnOrder))
                    {
                        var savedOrder = _config.ColumnOrder.Split(',');
                        var newColumns = new List<GridViewColumn>();

                        foreach (var colName in savedOrder)
                        {
                            var trimmedName = colName.Trim();
                            if (columnMap.ContainsKey(trimmedName))
                            {
                                newColumns.Add(columnMap[trimmedName]);
                            }
                        }

                        // 添加未在顺序中的列（向后兼容）
                        foreach (var kvp in columnMap)
                        {
                            if (!savedOrder.Any(s => s.Trim() == kvp.Key))
                            {
                                newColumns.Add(kvp.Value);
                            }
                        }

                        // 重新排序列
                        if (newColumns.Count == columns.Count)
                        {
                            gridView.Columns.Clear();
                            foreach (var col in newColumns)
                            {
                                gridView.Columns.Add(col);
                            }
                        }
                    }

                    // 加载列宽度并结合"可见列"配置（隐藏列保持宽度为0）
                    var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
                    var visibleSet = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                    foreach (var column in gridView.Columns)
                    {
                        var header = column.Header as GridViewColumnHeader;
                        if (header?.Tag != null)
                        {
                            var tag = header.Tag.ToString();
                            bool shouldShow = visibleSet.Contains(tag);
                            if (!shouldShow)
                            {
                                RememberColumnWidth(tag, column);
                                column.Width = 0; // 隐藏
                                continue;
                            }

                            double width = tag switch
                            {
                                "Name" => _config.ColNameWidth,
                                "Size" => _config.ColSizeWidth,
                                "Type" => _config.ColTypeWidth,
                                "ModifiedDate" => _config.ColModifiedDateWidth,
                                "CreatedTime" => _config.ColCreatedTimeWidth,
                                "Tags" => _config.ColTagsWidth,
                                "Notes" => _config.ColNotesWidth,
                                _ => 0
                            };
                            if (width > 0) column.Width = width;
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 保存列宽度和顺序
        /// </summary>
        public void SaveColumnWidths(GridView gridView)
        {
            if (gridView == null || _config == null) return;

            try
            {
                var columns = gridView.Columns;
                if (columns.Count >= 7)
                {
                    // 保存列顺序
                    var columnOrder = new List<string>();
                    foreach (var column in columns)
                    {
                        var header = column.Header as GridViewColumnHeader;
                        if (header?.Tag != null)
                        {
                            columnOrder.Add(header.Tag.ToString());
                        }
                    }
                    _config.ColumnOrder = string.Join(",", columnOrder);

                    // 保存列宽度（按Tag保存）
                    foreach (var column in columns)
                    {
                        var header = column.Header as GridViewColumnHeader;
                        if (header?.Tag != null)
                        {
                            var tag = header.Tag.ToString();
                            var width = column.ActualWidth;

                            // 如果列被隐藏（宽度=0），不要覆盖之前保存的宽度
                            if (width <= 0) continue;

                            switch (tag)
                            {
                                case "Name":
                                    _config.ColNameWidth = width;
                                    break;
                                case "Size":
                                    _config.ColSizeWidth = width;
                                    break;
                                case "Type":
                                    _config.ColTypeWidth = width;
                                    break;
                                case "ModifiedDate":
                                    _config.ColModifiedDateWidth = width;
                                    break;
                                case "CreatedTime":
                                    _config.ColCreatedTimeWidth = width;
                                    break;
                                case "Tags":
                                    _config.ColTagsWidth = width;
                                    break;
                                case "Notes":
                                    _config.ColNotesWidth = width;
                                    break;
                            }
                        }
                    }

                    ConfigManager.Save(_config);
                }
            }
            catch { }
        }

        #endregion

        #region 列可见性管理

        /// <summary>
        /// 获取当前模式的可见列配置
        /// </summary>
        public string GetVisibleColumnsForCurrentMode()
        {
            var key = _getCurrentModeKey();
            return key switch
            {
                "Library" => _config.VisibleColumns_Library,
                "Tag" => _config.VisibleColumns_Tag,
                _ => _config.VisibleColumns_Path
            };
        }

        /// <summary>
        /// 设置当前模式的可见列配置
        /// </summary>
        public void SetVisibleColumnsForCurrentMode(string csv)
        {
            var key = _getCurrentModeKey();
            switch (key)
            {
                case "Library":
                    _config.VisibleColumns_Library = csv;
                    break;
                case "Tag":
                    _config.VisibleColumns_Tag = csv;
                    break;
                default:
                    _config.VisibleColumns_Path = csv;
                    break;
            }
        }

        /// <summary>
        /// 应用可见列配置到 GridView
        /// </summary>
        public void ApplyVisibleColumnsForCurrentMode(GridView gridView, Action hookHeaderThumbs = null)
        {
            if (gridView == null) return;
            var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
            var set = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            foreach (var column in gridView.Columns)
            {
                var header = column.Header as GridViewColumnHeader;
                var tag = header?.Tag?.ToString();
                if (string.IsNullOrEmpty(tag)) continue;
                bool shouldShow = set.Contains(tag);
                if (shouldShow)
                {
                    // 恢复保存的宽度
                    double w = ResolveColumnWidth(tag, column);
                    column.Width = Math.Max(40, w);
                }
                else
                {
                    // 折叠
                    RememberColumnWidth(tag, column);
                    column.Width = 0;
                }
            }
            hookHeaderThumbs?.Invoke();
        }

        /// <summary>
        /// 检查列是否可见
        /// </summary>
        public bool IsColumnVisible(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return true;
            var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
            var set = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            return set.Contains(tag);
        }

        #endregion

        #region 列头右键菜单

        /// <summary>
        /// 显示列头右键菜单
        /// </summary>
        public void ShowHeaderContextMenu(
            GridViewColumnHeader header,
            GridView gridView,
            Action<string, bool> onColumnVisibilityChanged = null)
        {
            if (header == null || gridView == null) return;

            var cm = new ContextMenu();
            var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
            var visibleSet = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

            foreach (var column in gridView.Columns)
            {
                var colHeader = column.Header as GridViewColumnHeader;
                var tag = colHeader?.Tag?.ToString();
                if (string.IsNullOrEmpty(tag)) continue;

                // 避免闭包引用 foreach 变量，复制到局部变量
                var columnLocal = column;
                var tagLocal = tag;
                var titleLocal = colHeader?.Content?.ToString() ?? tagLocal;
                bool isVisible = visibleSet.Contains(tagLocal);

                var mi = new MenuItem
                {
                    Header = $"列: {titleLocal}",
                    IsCheckable = true,
                    IsChecked = isVisible
                };

                mi.Checked += (s, ev) =>
                {
                    // 显示列
                    if (columnLocal.Width <= 1)
                    {
                        double w = ResolveColumnWidth(tagLocal, columnLocal);
                        columnLocal.Width = Math.Max(40, w);
                    }
                    // 更新配置
                    var currentVisible = GetVisibleColumnsForCurrentMode() ?? "";
                    var currentSet = new HashSet<string>(currentVisible.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                    currentSet.Add(tagLocal);
                    SetVisibleColumnsForCurrentMode(string.Join(",", currentSet));
                    ConfigManager.Save(_config);

                    // 立即应用并保存列宽
                    ApplyVisibleColumnsForCurrentMode(gridView, null);
                    SaveColumnWidths(gridView);

                    // 分组视图同步
                    onColumnVisibilityChanged?.Invoke(tagLocal, true);
                };

                mi.Unchecked += (s, ev) =>
                {
                    // 隐藏列
                    RememberColumnWidth(tagLocal, columnLocal);
                    columnLocal.Width = 0;
                    // 更新配置
                    var currentVisible = GetVisibleColumnsForCurrentMode() ?? "";
                    var currentSet = new HashSet<string>(currentVisible.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                    currentSet.Remove(tagLocal);
                    SetVisibleColumnsForCurrentMode(string.Join(",", currentSet));
                    ConfigManager.Save(_config);

                    // 立即应用并保存列宽
                    ApplyVisibleColumnsForCurrentMode(gridView, null);
                    SaveColumnWidths(gridView);

                    // 分组视图同步
                    onColumnVisibilityChanged?.Invoke(tagLocal, false);
                };

                cm.Items.Add(mi);
            }

            cm.PlacementTarget = header;
            cm.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            cm.IsOpen = true;
        }

        #endregion

        #region 列头拖拽调整

        /// <summary>
        /// 绑定列头分隔线事件
        /// </summary>
        public void HookHeaderThumbs(
            GridView gridView,
            System.Windows.Controls.Primitives.DragStartedEventHandler dragStartedHandler,
            System.Windows.Controls.Primitives.DragDeltaEventHandler dragDeltaHandler,
            MouseButtonEventHandler previewMouseLeftButtonDownHandler)
        {
            if (gridView == null) return;
            foreach (var column in gridView.Columns)
            {
                if (column.Header is GridViewColumnHeader header)
                {
                    if (previewMouseLeftButtonDownHandler != null)
                    {
                        header.PreviewMouseLeftButtonDown -= previewMouseLeftButtonDownHandler;
                        header.PreviewMouseLeftButtonDown += previewMouseLeftButtonDownHandler;
                    }

                    // 确保模板应用后再挂载Thumb事件
                    header.Loaded -= (s, e) => Header_Loaded_AttachThumb(s, e, dragStartedHandler, dragDeltaHandler);
                    header.Loaded += (s, e) => Header_Loaded_AttachThumb(s, e, dragStartedHandler, dragDeltaHandler);
                }
            }
        }

        private void Header_Loaded_AttachThumb(
            object sender,
            RoutedEventArgs e,
            System.Windows.Controls.Primitives.DragStartedEventHandler dragStartedHandler,
            System.Windows.Controls.Primitives.DragDeltaEventHandler dragDeltaHandler)
        {
            if (sender is GridViewColumnHeader header)
            {
                var thumb = FindHeaderThumb(header);
                if (thumb != null)
                {
                    if (dragStartedHandler != null)
                    {
                        thumb.DragStarted -= dragStartedHandler;
                        thumb.DragStarted += dragStartedHandler;
                    }
                    if (dragDeltaHandler != null)
                    {
                        thumb.DragDelta -= dragDeltaHandler;
                        thumb.DragDelta += dragDeltaHandler;
                    }
                }
            }
        }

        /// <summary>
        /// 查找列头分隔线控件
        /// </summary>
        public System.Windows.Controls.Primitives.Thumb FindHeaderThumb(GridViewColumnHeader header)
        {
            // 先尝试按模板名
            var thumb = header.Template?.FindName("PART_HeaderGripper", header) as System.Windows.Controls.Primitives.Thumb;
            if (thumb != null) return thumb;
            // 否则在视觉树中查找
            return FindDescendant<System.Windows.Controls.Primitives.Thumb>(header);
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

        /// <summary>
        /// 处理列头分隔线拖拽开始
        /// </summary>
        public void HandleHeaderThumbDragStarted(
            object sender,
            System.Windows.Controls.Primitives.DragStartedEventArgs e,
            GridView gridView)
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

        /// <summary>
        /// 处理列头分隔线拖拽过程
        /// </summary>
        public void HandleHeaderThumbDragDelta(
            object sender,
            System.Windows.Controls.Primitives.DragDeltaEventArgs e,
            GridView gridView)
        {
            var header = FindAncestor<GridViewColumnHeader>(sender as DependencyObject);
            if (header?.Column == null) return;
            var tag = header.Tag?.ToString();
            if (!IsColumnVisible(tag))
            {
                // 该列是隐藏列：把拖动量转嫁到左侧最近的可见列，避免用户感觉被阻塞
                header.Column.Width = 0; // 自身保持隐藏

                // 在列集合中找到当前列索引
                if (gridView != null)
                {
                    int idx = gridView.Columns.IndexOf(header.Column);
                    // 向左寻找最近的可见列
                    for (int i = idx - 1; i >= 0; i--)
                    {
                        var leftCol = gridView.Columns[i];
                        var leftHeader = leftCol.Header as GridViewColumnHeader;
                        var leftTag = leftHeader?.Tag?.ToString();
                        if (IsColumnVisible(leftTag))
                        {
                            double min = 40; // 最小宽度保护
                            double newWidth = Math.Max(min, leftCol.Width + e.HorizontalChange);
                            leftCol.Width = newWidth;
                            e.Handled = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                // 该列可见，但其右邻居可能是隐藏列：当向右拖动时，把扩展量施加到当前列，同时保持右邻居为0
                if (gridView != null && e.HorizontalChange > 0)
                {
                    int idx = gridView.Columns.IndexOf(header.Column);
                    if (idx >= 0 && idx + 1 < gridView.Columns.Count)
                    {
                        var rightCol = gridView.Columns[idx + 1];
                        var rightHeader = rightCol.Header as GridViewColumnHeader;
                        var rightTag = rightHeader?.Tag?.ToString();
                        if (!IsColumnVisible(rightTag))
                        {
                            // 右侧隐藏：放大当前列，并强制右侧维持隐藏
                            double min = 40;
                            header.Column.Width = Math.Max(min, header.Column.Width + e.HorizontalChange);
                            if (rightCol.Width != 0) rightCol.Width = 0;
                            e.Handled = true;
                        }
                    }
                }
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

        #endregion

        #region 自动适配列宽

        /// <summary>
        /// 根据内容自动调整列宽
        /// </summary>
        public void AutoSizeGridViewColumn(
            GridViewColumn column,
            ListView listView)
        {
            if (listView == null || column == null) return;

            // 若该列在当前模式被设置为隐藏，禁止在双击时把它显示出来
            var headerForTag = column.Header as GridViewColumnHeader;
            var tagName = headerForTag?.Tag?.ToString();
            if (!string.IsNullOrEmpty(tagName))
            {
                var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
                var visibleSet = new HashSet<string>(
                    visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.OrdinalIgnoreCase);
                if (!visibleSet.Contains(tagName))
                {
                    // 保持隐藏状态，直接返回
                    return;
                }
            }

            double padding = 24; // 预留左右内边距和排序箭头空间
            double maxWidth = 0;

            // 列头文本宽度
            var header = column.Header as GridViewColumnHeader;
            var headerText = header?.Content?.ToString() ?? "";
            maxWidth = Math.Max(maxWidth, MeasureTextWidth(headerText, listView) + padding);

            // 各行文本宽度
            foreach (var item in listView.Items)
            {
                string cellText = GetCellTextForColumn(item, column, header);
                maxWidth = Math.Max(maxWidth, MeasureTextWidth(cellText, listView) + padding);
            }

            // 最小宽度保护
            if (maxWidth < 50) maxWidth = 50;
            column.Width = Math.Ceiling(maxWidth);
        }

        private string GetCellTextForColumn(object item, GridViewColumn column, GridViewColumnHeader header)
        {
            if (item == null) return "";

            // 优先使用 DisplayMemberBinding
            if (column.DisplayMemberBinding is System.Windows.Data.Binding binding && binding.Path != null)
            {
                var prop = item.GetType().GetProperty(binding.Path.Path);
                var val = prop?.GetValue(item);
                return val?.ToString() ?? "";
            }

            // 退化：使用列头 Tag 作为属性名尝试
            var propName = header?.Tag?.ToString();
            if (!string.IsNullOrEmpty(propName))
            {
                var prop2 = item.GetType().GetProperty(propName);
                var val2 = prop2?.GetValue(item);
                if (val2 != null) return val2.ToString();
            }

            return "";
        }

        private double MeasureTextWidth(string text, ListView listView)
        {
            var tb = new TextBlock
            {
                Text = text ?? "",
                FontSize = listView?.FontSize ?? 12,
                FontFamily = listView?.FontFamily
            };
            tb.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
            return tb.DesiredSize.Width;
        }

        #endregion

        #region 列排序

        /// <summary>
        /// 处理列头点击（排序）
        /// </summary>
        public void HandleColumnHeaderClick(
            GridViewColumnHeader header,
            GridView gridView,
            List<FileSystemItem> files,
            Action<List<FileSystemItem>> updateFilesSource)
        {
            if (header == null || header.Tag == null)
                return;

            var columnName = header.Tag.ToString();

            // 如果点击同一列，切换排序方向；否则默认升序
            if (_lastSortColumn == columnName)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _lastSortColumn = columnName;
                _sortAscending = true;
            }

            // 应用排序
            var sortedFiles = SortFiles(files);
            updateFilesSource?.Invoke(sortedFiles);

            // 更新列头显示排序指示器
            UpdateSortIndicators(header, gridView);
        }

        /// <summary>
        /// 排序文件列表
        /// </summary>
        public List<FileSystemItem> SortFiles(List<FileSystemItem> files)
        {
            if (files == null || files.Count == 0)
                return files ?? new List<FileSystemItem>();

            // 分离文件夹和文件
            var directories = files.Where(f => f.IsDirectory).ToList();
            var fileItems = files.Where(f => !f.IsDirectory).ToList();

            // 对文件夹和文件分别排序
            directories = SortList(directories);
            fileItems = SortList(fileItems);

            // 合并：文件夹在前，文件在后
            var result = new List<FileSystemItem>();
            result.AddRange(directories);
            result.AddRange(fileItems);
            return result;
        }

        /// <summary>
        /// 按指定列名和顺序排序文件列表
        /// </summary>
        /// <param name="files">要排序的文件列表</param>
        /// <param name="column">列名</param>
        /// <param name="ascending">是否升序</param>
        /// <returns>排序后的文件列表</returns>
        public List<FileSystemItem> SortFiles(List<FileSystemItem> files, string column, bool ascending)
        {
            _lastSortColumn = column ?? _lastSortColumn;
            _sortAscending = ascending;
            return SortFiles(files);
        }

        private List<FileSystemItem> SortList(List<FileSystemItem> items)
        {
            if (items == null || items.Count == 0) return items;

            IEnumerable<FileSystemItem> sorted = items;

            switch (_lastSortColumn)
            {
                case "Name":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    break;

                case "Size":
                    sorted = _sortAscending
                        ? items.OrderBy(f => ParseSize(f.Size))
                        : items.OrderByDescending(f => ParseSize(f.Size));
                    break;

                case "Type":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Type, StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(f => f.Type, StringComparer.OrdinalIgnoreCase);
                    break;

                case "ModifiedDate":
                    sorted = _sortAscending
                        ? items.OrderBy(f => ParseDate(f.ModifiedDate))
                        : items.OrderByDescending(f => ParseDate(f.ModifiedDate));
                    break;

                case "CreatedTime":
                    sorted = _sortAscending
                        ? items.OrderBy(f => ParseTimeAgo(f.CreatedTime))
                        : items.OrderByDescending(f => ParseTimeAgo(f.CreatedTime));
                    break;

                case "Tags":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Tags ?? "", StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(f => f.Tags ?? "", StringComparer.OrdinalIgnoreCase);
                    break;

                case "Notes":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Notes ?? "", StringComparer.OrdinalIgnoreCase)
                        : items.OrderByDescending(f => f.Notes ?? "", StringComparer.OrdinalIgnoreCase);
                    break;
            }

            return sorted.ToList();
        }

        private long ParseSize(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr) || sizeStr == "-" || sizeStr == "计算中...")
                return 0;

            // 移除空格
            sizeStr = sizeStr.Replace(" ", "");

            try
            {
                // 提取数字和单位
                var number = new string(sizeStr.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
                var unit = sizeStr.Length > number.Length
                    ? sizeStr.Substring(number.Length).ToUpper()
                    : sizeStr.ToUpper();

                if (string.IsNullOrEmpty(number))
                    return 0;

                double value = double.Parse(number);

                // 转换为字节
                switch (unit)
                {
                    case "B":
                        return (long)value;
                    case "KB":
                        return (long)(value * 1024);
                    case "MB":
                        return (long)(value * 1024 * 1024);
                    case "GB":
                        return (long)(value * 1024 * 1024 * 1024);
                    case "TB":
                        return (long)(value * 1024 * 1024 * 1024 * 1024);
                    default:
                        return (long)value;
                }
            }
            catch
            {
                return 0;
            }
        }

        private DateTime ParseDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out DateTime result))
                return result;
            return DateTime.MinValue;
        }

        private long ParseTimeAgo(string timeStr)
        {
            if (string.IsNullOrEmpty(timeStr))
                return long.MaxValue;

            try
            {
                // 提取数字
                var number = new string(timeStr.TakeWhile(c => char.IsDigit(c)).ToArray());
                if (string.IsNullOrEmpty(number))
                    return long.MaxValue;

                long value = long.Parse(number);

                // 根据单位转换为秒
                if (timeStr.EndsWith("s"))
                    return value;
                else if (timeStr.EndsWith("m"))
                    return value * 60;
                else if (timeStr.EndsWith("h"))
                    return value * 3600;
                else if (timeStr.EndsWith("d"))
                    return value * 86400;
                else if (timeStr.EndsWith("mo"))
                    return value * 2592000; // 30天
                else if (timeStr.EndsWith("y"))
                    return value * 31536000; // 365天

                return long.MaxValue;
            }
            catch
            {
                return long.MaxValue;
            }
        }

        /// <summary>
        /// 更新排序指示器
        /// </summary>
        public void UpdateSortIndicators(GridViewColumnHeader clickedHeader, GridView gridView)
        {
            // 清除所有列头的排序指示器
            foreach (var column in gridView.Columns)
            {
                var header = column.Header as GridViewColumnHeader;
                if (header != null && header.Tag != null)
                {
                    var content = header.Content.ToString();
                    // 移除现有的排序符号
                    content = content.Replace(" ▲", "").Replace(" ▼", "");
                    header.Content = content;
                }
            }

            // 为当前列添加排序指示器
            if (clickedHeader != null)
            {
                var content = clickedHeader.Content.ToString();
                content = content.Replace(" ▲", "").Replace(" ▼", "");
                clickedHeader.Content = content + (_sortAscending ? " ▲" : " ▼");
            }
        }

        #endregion
    }
}





