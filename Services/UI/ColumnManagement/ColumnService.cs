using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OoiMRR.Controls;
using OoiMRR.Services;

namespace OoiMRR.Services.ColumnManagement
{
    /// <summary>
    /// 列管理服务
    /// 负责列排序、列宽度管理、列可见性管理
    /// </summary>
    public class ColumnService
    {
        private readonly AppConfig _config;
        private readonly Func<string> _getCurrentModeKey;
        private readonly Action _saveConfig;

        // 排序状态
        private string _lastSortColumn = "Name";
        private bool _sortAscending = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">应用配置对象</param>
        /// <param name="getCurrentModeKey">获取当前导航模式的函数</param>
        /// <param name="saveConfig">保存配置的回调函数</param>
        public ColumnService(
            AppConfig config,
            Func<string> getCurrentModeKey,
            Action saveConfig = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _getCurrentModeKey = getCurrentModeKey ?? throw new ArgumentNullException(nameof(getCurrentModeKey));
            _saveConfig = saveConfig;
        }

        #region 排序功能

        /// <summary>
        /// 获取当前排序列
        /// </summary>
        public string LastSortColumn => _lastSortColumn;

        /// <summary>
        /// 获取当前排序方向
        /// </summary>
        public bool SortAscending => _sortAscending;

        /// <summary>
        /// 处理列头点击事件，进行排序
        /// </summary>
        public void HandleColumnHeaderClick(
            GridViewColumnHeader header,
            List<FileSystemItem> files,
            Action<List<FileSystemItem>> updateFilesSource,
            GridView gridView)
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
        /// 排序文件列表（分离文件夹和文件）
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
        public List<FileSystemItem> SortFiles(List<FileSystemItem> files, string column, bool ascending)
        {
            _lastSortColumn = column ?? _lastSortColumn;
            _sortAscending = ascending;
            return SortFiles(files);
        }

        /// <summary>
        /// 排序文件列表（单个列表）
        /// </summary>
        private List<FileSystemItem> SortList(List<FileSystemItem> items)
        {
            if (items == null || items.Count == 0)
                return items ?? new List<FileSystemItem>();

            IEnumerable<FileSystemItem> sorted = items;

            switch (_lastSortColumn)
            {
                case "Name":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                        : items.OrderByDescending(f => f.Name, StringComparer.CurrentCultureIgnoreCase);
                    break;

                case "Size":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.SizeBytes)
                        : items.OrderByDescending(f => f.SizeBytes);
                    break;

                case "Type":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Type, StringComparer.CurrentCultureIgnoreCase)
                        : items.OrderByDescending(f => f.Type, StringComparer.CurrentCultureIgnoreCase);
                    break;

                case "ModifiedDate":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.ModifiedDateTime)
                        : items.OrderByDescending(f => f.ModifiedDateTime);
                    break;

                case "CreatedTime":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.CreatedDateTime)
                        : items.OrderByDescending(f => f.CreatedDateTime);
                    break;

                case "Tags":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Tags ?? "", StringComparer.CurrentCultureIgnoreCase)
                        : items.OrderByDescending(f => f.Tags ?? "", StringComparer.CurrentCultureIgnoreCase);
                    break;

                case "Notes":
                    sorted = _sortAscending
                        ? items.OrderBy(f => f.Notes ?? "", StringComparer.CurrentCultureIgnoreCase)
                        : items.OrderByDescending(f => f.Notes ?? "", StringComparer.CurrentCultureIgnoreCase);
                    break;
            }

            return sorted.ToList();
        }

        /// <summary>
        /// 更新列头的排序指示器
        /// </summary>
        public void UpdateSortIndicators(GridViewColumnHeader clickedHeader, GridView gridView)
        {
            if (gridView == null) return;

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

        #region 列宽度管理

        /// <summary>
        /// 加载列宽度和顺序
        /// </summary>
        public void LoadColumnWidths(FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesGrid == null || _config == null) return;

            try
            {
                var columns = fileBrowser.FilesGrid.Columns;
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
                            fileBrowser.FilesGrid.Columns.Clear();
                            foreach (var col in newColumns)
                            {
                                fileBrowser.FilesGrid.Columns.Add(col);
                            }
                        }
                    }

                    // 加载列宽度并结合"可见列"配置（隐藏列保持宽度为0）
                    var visibleCsv = GetVisibleColumnsForCurrentMode();
                    var visibleSet = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                    foreach (var column in fileBrowser.FilesGrid.Columns)
                    {
                        var header = column.Header as GridViewColumnHeader;
                        if (header?.Tag != null)
                        {
                            var tag = header.Tag.ToString();
                            bool shouldShow = visibleSet.Contains(tag);
                            if (!shouldShow)
                            {
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
                            if (IsValidWidth(width) && width > 0) 
                            { 
                                column.Width = width; 
                            }
                            else if (width < 0)
                            {
                                // Reset invalid width (protect against ArgumentException)
                                column.Width = 100; 
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 保存列宽度和顺序
        /// </summary>
        public void SaveColumnWidths(FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesGrid == null || _config == null) return;

            try
            {
                var columns = fileBrowser.FilesGrid.Columns;
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

                    _saveConfig?.Invoke();
                }
            }
            catch { }
        }

        /// <summary>
        /// 自动调整列宽度以适应内容
        /// </summary>
        public void AutoSizeGridViewColumn(GridViewColumn column, FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesList == null || column == null) return;

            // 若该列在当前模式被设置为隐藏，禁止在双击时把它显示出来
            var headerForTag = column.Header as GridViewColumnHeader;
            var tagName = headerForTag?.Tag?.ToString();
            if (!string.IsNullOrEmpty(tagName))
            {
                var visibleCsv = GetVisibleColumnsForCurrentMode();
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
            maxWidth = Math.Max(maxWidth, MeasureTextWidth(headerText, fileBrowser.FilesList) + padding);

            // 各行文本宽度
            foreach (var item in fileBrowser.FilesList.Items)
            {
                string cellText = GetCellTextForColumn(item, column, header);
                maxWidth = Math.Max(maxWidth, MeasureTextWidth(cellText, fileBrowser.FilesList) + padding);
            }

            // 最小宽度保护
            if (maxWidth < 50) maxWidth = 50;
            column.Width = Math.Ceiling(maxWidth);
        }

        /// <summary>
        /// 获取列的单元格文本
        /// </summary>
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

        /// <summary>
        /// 测量文本宽度
        /// </summary>
        private double MeasureTextWidth(string text, ListView listView)
        {
            var tb = new TextBlock
            {
                Text = text ?? "",
                FontSize = listView?.FontSize ?? 12,
                FontFamily = listView?.FontFamily
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return tb.DesiredSize.Width;
        }

        /// <summary>
        /// 调整列表视图列宽度
        /// </summary>
        public void AdjustListViewColumnWidths(FileBrowserControl fileBrowser)
        {
            if (fileBrowser == null || fileBrowser.FilesList == null) return;

            var gridView = fileBrowser.FilesGrid;
            if (gridView == null || gridView.Columns.Count < 5) return;

            // 若有隐藏列，需尊重"可见列"配置：隐藏列保持宽度0，不参与自适应
            var visibleCsv = GetVisibleColumnsForCurrentMode();
            var visible = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            bool showName = visible.Contains("Name");
            bool showSize = visible.Contains("Size");
            bool showType = visible.Contains("Type");
            bool showModified = visible.Contains("ModifiedDate");
            bool showTags = visible.Contains("Tags");

            // 获取可用宽度（减去名称、修改日期、标签列的宽度和边距）
            double availableWidth = fileBrowser.FilesList.ActualWidth - 50; // 减去一些边距和滚动条

            // 名称列固定宽度
            double nameColWidth = showName ? 200 : 0;
            // 修改日期列固定宽度
            double modifiedDateColWidth = showModified ? 150 : 0;
            // 标签列固定宽度
            double tagsColWidth = showTags ? 150 : 0;

            // 计算剩余可用宽度
            double remainingWidth = availableWidth - nameColWidth - modifiedDateColWidth - tagsColWidth;

            // 设置最小宽度
            double minSizeColWidth = 80;
            double minTypeColWidth = 80;

            // 获取当前大小列和类型列的实际宽度，而不是使用硬编码的默认值
            double sizeColWidth = showSize ? (gridView.Columns.Count >= 2 && gridView.Columns[1].ActualWidth > 0 ? gridView.Columns[1].ActualWidth : 100) : 0;
            double typeColWidth = showType ? (gridView.Columns.Count >= 3 && gridView.Columns[2].ActualWidth > 0 ? gridView.Columns[2].ActualWidth : 100) : 0;

            if (remainingWidth < sizeColWidth + typeColWidth && showSize && showType)
            {
                // 空间不足，需要压缩
                if (remainingWidth >= minSizeColWidth + minTypeColWidth)
                {
                    // 可以容纳最小宽度，先压缩类型列
                    double minTotal = minSizeColWidth + minTypeColWidth;
                    double extraWidth = remainingWidth - minTotal;

                    // 先压缩类型列
                    double typeShrink = Math.Max(0, typeColWidth - minTypeColWidth);
                    double typeCanShrink = Math.Min(typeShrink, extraWidth);
                    typeColWidth -= typeCanShrink;

                    // 如果还有空间，给大小列
                    if (typeCanShrink < extraWidth)
                    {
                        sizeColWidth = minSizeColWidth + (extraWidth - typeCanShrink);
                    }
                    else
                    {
                        sizeColWidth = minSizeColWidth;
                    }
                }
                else
                {
                    // 空间不足，都设置为最小宽度
                    sizeColWidth = minSizeColWidth;
                    typeColWidth = minTypeColWidth;
                }
            }

            // 按列Tag应用，隐藏列保持0
            // 索引与Tag对应：0-Name,1-Size,2-Type,3-ModifiedDate,4-CreatedTime,5-Tags,6-Notes
            if (gridView.Columns.Count >= 1 && IsValidWidth(nameColWidth)) gridView.Columns[0].Width = nameColWidth;           // Name
            if (gridView.Columns.Count >= 2 && IsValidWidth(sizeColWidth)) gridView.Columns[1].Width = sizeColWidth;           // Size
            if (gridView.Columns.Count >= 3 && IsValidWidth(typeColWidth)) gridView.Columns[2].Width = typeColWidth;           // Type
            if (gridView.Columns.Count >= 4 && IsValidWidth(modifiedDateColWidth)) gridView.Columns[3].Width = modifiedDateColWidth;   // ModifiedDate
            // 不调整 CreatedTime（[4]），由用户控制；若隐藏则保持0
            if (gridView.Columns.Count >= 6 && IsValidWidth(tagsColWidth)) gridView.Columns[5].Width = tagsColWidth;           // Tags
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
        /// 应用可见列配置到文件列表
        /// </summary>
        public void ApplyVisibleColumnsForCurrentMode(FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesGrid == null) return;
            var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
            var set = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            foreach (var column in fileBrowser.FilesGrid.Columns)
            {
                var header = column.Header as GridViewColumnHeader;
                var tag = header?.Tag?.ToString();
                if (string.IsNullOrEmpty(tag)) continue;
                bool shouldShow = set.Contains(tag);
                if (shouldShow)
                {
                    // 恢复保存的宽度
                    double w = tag switch
                    {
                        "Name" => _config.ColNameWidth,
                        "Size" => _config.ColSizeWidth,
                        "Type" => _config.ColTypeWidth,
                        "ModifiedDate" => _config.ColModifiedDateWidth,
                        "CreatedTime" => _config.ColCreatedTimeWidth,
                        "Tags" => _config.ColTagsWidth,
                        "Notes" => _config.ColNotesWidth,
                        _ => column.ActualWidth > 0 ? column.ActualWidth : 100
                    };
                    
                    if (IsValidWidth(w))
                    {
                        column.Width = Math.Max(40, w);
                    }
                    else
                    {
                        column.Width = 100;
                    }
                }
                else
                {
                    // 折叠
                    column.Width = 0;
                }
            }
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

        private bool IsValidWidth(double width)
        {
            return !double.IsNaN(width) && !double.IsInfinity(width) && width >= 0;
        }

        #endregion
    }
}





