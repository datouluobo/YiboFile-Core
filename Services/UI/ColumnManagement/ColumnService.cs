using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Controls;
using System.Windows.Controls.Primitives;
using YiboFile.Services.UI.ColumnManagement;

namespace YiboFile.Services.ColumnManagement
{
    /// <summary>
    /// 列管理服务（整合版）
    /// 整合了ColumnService和ColumnHeaderService的所有功能
    /// 负责列排序、列宽度管理、列可见性管理、列头交互（右键菜单、拖拽调整等）
    /// </summary>
    public class ColumnService
    {
        private readonly AppConfig _config;
        private Func<string> _getCurrentModeKey;
        private Action _saveConfig;

        // 排序状态
        private string _lastSortColumn = "Name";
        private bool _sortAscending = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">应用配置对象</param>
        public ColumnService(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Initialize sort state from config
            if (!string.IsNullOrEmpty(_config.SortColumn))
            {
                _lastSortColumn = _config.SortColumn;
            }
            if (!string.IsNullOrEmpty(_config.SortDirection))
            {
                _sortAscending = _config.SortDirection == "Ascending";
            }
        }

        /// <summary>
        /// 初始化上下文依赖（委托）
        /// </summary>
        public void Initialize(Func<string> getCurrentModeKey, Action saveConfig)
        {
            _getCurrentModeKey = getCurrentModeKey;
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
        /// 使用兼容ColumnService的API签名
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

            // 更新配置并保存
            _config.SortColumn = _lastSortColumn;
            _config.SortDirection = _sortAscending ? "Ascending" : "Descending";
            _saveConfig?.Invoke();

            // 应用排序
            var sortedFiles = SortFiles(files);

            updateFilesSource?.Invoke(sortedFiles);

            // 更新列头显示排序指示器
            UpdateSortIndicators(header, gridView);
        }

        /// <summary>
        /// 排序文件列表（分离文件夹和文件）
        /// </summary>
        /// <summary>
        /// 排序文件列表（分离文件夹和文件）
        /// 使用List.Sort进行原地排序，减少内存分配
        /// </summary>
        public List<FileSystemItem> SortFiles(List<FileSystemItem> files)
        {
            if (files == null || files.Count == 0)
                return files ?? new List<FileSystemItem>();

            // 分离文件夹和文件
            // 此处仍需遍历一次，但后续排序效率更高
            var directories = new List<FileSystemItem>(files.Count / 3);
            var fileItems = new List<FileSystemItem>(files.Count);

            foreach (var item in files)
            {
                if (item.IsDirectory)
                {
                    directories.Add(item);
                }
                else
                {
                    fileItems.Add(item);
                }
            }

            // 使用自定义比较器进行原地排序
            var comparer = new FileSystemItemComparer(_lastSortColumn, _sortAscending);

            directories.Sort(comparer);
            fileItems.Sort(comparer);

            // 合并：文件夹在前，文件在后
            // 复用传入的list以减少内存分配（如果允许修改传入list）
            //这里为了安全，还是创建新list返回，如需进一步优化可考虑原地修改传入list
            var result = new List<FileSystemItem>(files.Count);
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

        // 移除 SortList 方法，因为已集成到 SortFiles 中

        /// <summary>
        /// 自定义文件排序比较器
        /// </summary>
        private class FileSystemItemComparer : IComparer<FileSystemItem>
        {
            private readonly string _column;
            private readonly bool _ascending;
            private readonly int _direction;

            public FileSystemItemComparer(string column, bool ascending)
            {
                _column = column;
                _ascending = ascending;
                _direction = ascending ? 1 : -1;
            }

            public int Compare(FileSystemItem x, FileSystemItem y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1; // null在小端
                if (y == null) return 1;

                int result = 0;

                switch (_column)
                {
                    case "Name":
                        result = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                        break;

                    case "Size":
                        result = x.SizeBytes.CompareTo(y.SizeBytes);
                        break;

                    case "Type":
                        result = string.Compare(x.Type, y.Type, StringComparison.OrdinalIgnoreCase);
                        break;

                    case "ModifiedDate":
                        result = x.ModifiedDateTime.CompareTo(y.ModifiedDateTime);
                        break;

                    case "CreatedTime":
                        result = x.CreatedDateTime.CompareTo(y.CreatedDateTime);
                        break;

                    case "Tags":
                        // Tags 可能为 null
                        result = string.Compare(x.Tags ?? "", y.Tags ?? "", StringComparison.OrdinalIgnoreCase);
                        break;

                    case "Notes":
                        // Notes 可能为 null
                        result = string.Compare(x.Notes ?? "", y.Notes ?? "", StringComparison.OrdinalIgnoreCase);
                        break;

                    default:
                        result = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                        break;
                }

                return result * _direction;
            }
        }

        /// <summary>
        /// 更新列头的排序指示器
        /// </summary>
        public void UpdateSortIndicators(GridViewColumnHeader clickedHeader, GridView gridView)
        {
            if (gridView == null) return;

            // 清除所有列头的排序指示器（附加属性）
            foreach (var column in gridView.Columns)
            {
                var header = column.Header as GridViewColumnHeader;
                if (header != null)
                {
                    //清除附加属性
                    ColumnHeaderSortHelper.SetSortDirection(header, null);

                    // 保留对旧逻辑的兼容：移除文本符号
                    if (header.Content != null)
                    {
                        var content = header.Content.ToString();
                        content = content.Replace(" ▲", "").Replace(" ▼", "");
                        header.Content = content;
                    }
                }
            }

            // 为当前列设置排序指示器
            if (clickedHeader != null)
            {
                // 设置附加属性用于XAML绑定
                string sortDirection = _sortAscending ? "Ascending" : "Descending";
                ColumnHeaderSortHelper.SetSortDirection(clickedHeader, sortDirection);

                // 保留对旧逻辑的兼容（文本符号，如果用户喜欢可以保留）
                // 注释掉以使用纯视觉箭头
                /*
                if (clickedHeader.Content != null)
                {
                    var content = clickedHeader.Content.ToString();
                    content = content.Replace(" ▲", "").Replace(" ▼", "");
                    var newContent = content + (_sortAscending ? " ▲" : " ▼");
                    clickedHeader.Content = newContent;
                }
                */
            }
        }

        #endregion

        #region 列宽度管理

        /// <summary>
        /// 记住列宽度到配置
        /// </summary>
        private void RememberColumnWidth(string tag, GridViewColumn column)
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
                    // Tags列宽度由设置面板管理
                    // _config.ColTagsWidth = width;
                    break;
                case "Notes":
                    // Notes列宽度由设置面板管理
                    // _config.ColNotesWidth = width;
                    break;
            }
        }

        /// <summary>
        /// 从配置解析列宽度
        /// </summary>
        private double ResolveColumnWidth(string tag, GridViewColumn column)
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
        public void LoadColumnWidths(FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesGrid == null || _config == null) return;

            var gridView = fileBrowser.FilesGrid;

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

                    // 加载列宽度并结合"可见列"配置
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
        public void SaveColumnWidths(FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesGrid == null || _config == null) return;

            var gridView = fileBrowser.FilesGrid;

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

                    // 保存列宽度
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
                                    // Tags列宽度由设置面板管理，不在这里保存以防覆盖用户设置
                                    // _config.ColTagsWidth = width;
                                    break;
                                case "Notes":
                                    // Notes列宽度由设置面板管理，不在这里保存以防覆盖用户设置
                                    // _config.ColNotesWidth = width;
                                    break;
                            }
                        }
                    }

                    _saveConfig?.Invoke();
                }
            }
            catch { }
        }

        #endregion

        #region 自动适配列宽

        /// <summary>
        /// 自动调整列宽度以适应内容
        /// </summary>
        public void AutoSizeGridViewColumn(GridViewColumn column, FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesList == null || column == null) return;

            var listView = fileBrowser.FilesList;

            // 若该列在当前模式被设置为隐藏，禁止在双击时把它显示出来
            var headerForTag = column.Header as GridViewColumnHeader;
            var tagName = headerForTag?.Tag?.ToString();
            if (!string.IsNullOrEmpty(tagName))
            {
                if (!IsColumnVisible(tagName))
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

        #endregion

        #region 调整列表视图列宽度

        /// <summary>
        /// 调整列表视图列宽度（FileBrowserControl版本）
        /// </summary>
        public void AdjustListViewColumnWidths(FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesGrid == null) return;

            var gridView = fileBrowser.FilesGrid;
            var columns = gridView.Columns;
            if (columns.Count == 0) return;

            try
            {
                // 类似ColumnHeaderService的逻辑，但适配FileBrowserControl
                LoadColumnWidths(fileBrowser);
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
        /// 应用可见列配置到文件列表
        /// </summary>
        public void ApplyVisibleColumnsForCurrentMode(FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesGrid == null) return;

            var gridView = fileBrowser.FilesGrid;
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
            return width > 0 && !double.IsNaN(width) && !double.IsInfinity(width);
        }

        #endregion
    }
}

