using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Controls;
using System.Windows.Controls.Primitives;
using YiboFile.Services.UI.ColumnManagement;
using YiboFile.Services.Config;

namespace YiboFile.Services.ColumnManagement
{
    /// <summary>
    /// 列管理服务（整合版）
    /// 整合了ColumnService和ColumnHeaderService的所有功能
    /// 负责列排序、列宽度管理、列可见性管理、列头交互（右键菜单、拖拽调整等）
    /// </summary>
    public class ColumnService
    {
        private Func<string> _getCurrentModeKey;
        private AppConfig Config => ConfigurationService.Instance.Config;

        // 排序状态
        private string _lastSortColumn = "Name";
        private bool _sortAscending = true;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ColumnService()
        {
            var config = ConfigurationService.Instance.Config;

            // Initialize sort state from config
            if (!string.IsNullOrEmpty(config.SortColumn))
            {
                _lastSortColumn = config.SortColumn;
            }
            if (!string.IsNullOrEmpty(config.SortDirection))
            {
                _sortAscending = config.SortDirection == "Ascending";
            }
        }

        /// <summary>
        /// 初始化上下文依赖（委托）
        /// </summary>
        public void Initialize(Func<string> getCurrentModeKey)
        {
            _getCurrentModeKey = getCurrentModeKey;
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
            ConfigurationService.Instance.Update(cfg =>
            {
                cfg.SortColumn = _lastSortColumn;
                cfg.SortDirection = _sortAscending ? "Ascending" : "Descending";
            });
            ConfigurationService.Instance.SaveNow();

            // 应用排序
            var sortedFiles = SortFiles(files);

            updateFilesSource?.Invoke(sortedFiles);

            // 更新列头显示排序指示器
            UpdateSortIndicators(header, gridView);
        }

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

        /// <summary>
        /// 自定义文件排序比较器
        /// </summary>
        private class FileSystemItemComparer : IComparer<FileSystemItem>
        {
            private readonly string _column;
            private readonly int _direction;

            public FileSystemItemComparer(string column, bool ascending)
            {
                _column = column;
                _direction = ascending ? 1 : -1;
            }

            public int Compare(FileSystemItem x, FileSystemItem y)
            {
                if (ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
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
                        result = string.Compare(x.Tags ?? "", y.Tags ?? "", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "Notes":
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

            foreach (var column in gridView.Columns)
            {
                var header = column.Header as GridViewColumnHeader;
                if (header != null)
                {
                    ColumnHeaderSortHelper.SetSortDirection(header, null);
                    if (header.Content != null)
                    {
                        var content = header.Content.ToString();
                        content = content.Replace(" ▲", "").Replace(" ▼", "");
                        header.Content = content;
                    }
                }
            }

            if (clickedHeader != null)
            {
                string sortDirection = _sortAscending ? "Ascending" : "Descending";
                ColumnHeaderSortHelper.SetSortDirection(clickedHeader, sortDirection);
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

            ConfigurationService.Instance.Update(cfg =>
            {
                switch (tag)
                {
                    case "Name": cfg.ColNameWidth = width; break;
                    case "Size": cfg.ColSizeWidth = width; break;
                    case "Type": cfg.ColTypeWidth = width; break;
                    case "ModifiedDate": cfg.ColModifiedDateWidth = width; break;
                    case "CreatedTime": cfg.ColCreatedTimeWidth = width; break;
                }
            });
        }

        /// <summary>
        /// 从配置解析列宽度
        /// </summary>
        private double ResolveColumnWidth(string tag, GridViewColumn column)
        {
            double width = tag switch
            {
                "Name" => Config.ColNameWidth,
                "Size" => Config.ColSizeWidth,
                "Type" => Config.ColTypeWidth,
                "ModifiedDate" => Config.ColModifiedDateWidth,
                "CreatedTime" => Config.ColCreatedTimeWidth,
                "Tags" => Config.ColTagsWidth,
                "Notes" => Config.ColNotesWidth,
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
            if (fileBrowser?.FilesGrid == null) return;

            var gridView = fileBrowser.FilesGrid;

            try
            {
                var columns = gridView.Columns;
                if (columns.Count >= 7)
                {
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

                    if (!string.IsNullOrEmpty(Config.ColumnOrder))
                    {
                        var savedOrder = Config.ColumnOrder.Split(',');
                        var newColumns = new List<GridViewColumn>();

                        foreach (var colName in savedOrder)
                        {
                            var trimmedName = colName.Trim();
                            if (columnMap.ContainsKey(trimmedName))
                            {
                                newColumns.Add(columnMap[trimmedName]);
                            }
                        }

                        foreach (var kvp in columnMap)
                        {
                            if (!savedOrder.Any(s => s.Trim() == kvp.Key))
                            {
                                newColumns.Add(kvp.Value);
                            }
                        }

                        if (newColumns.Count == columns.Count)
                        {
                            gridView.Columns.Clear();
                            foreach (var col in newColumns)
                            {
                                gridView.Columns.Add(col);
                            }
                        }
                    }

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
                                column.Width = 0;
                                continue;
                            }

                            double width = ResolveColumnWidth(tag, column);
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
            if (fileBrowser?.FilesGrid == null) return;

            var gridView = fileBrowser.FilesGrid;

            try
            {
                var columns = gridView.Columns;
                if (columns.Count >= 7)
                {
                    var columnOrder = new List<string>();
                    foreach (var column in columns)
                    {
                        var header = column.Header as GridViewColumnHeader;
                        if (header?.Tag != null)
                        {
                            columnOrder.Add(header.Tag.ToString());
                        }
                    }

                    ConfigurationService.Instance.Update(c => c.ColumnOrder = string.Join(",", columnOrder));

                    foreach (var column in columns)
                    {
                        var header = column.Header as GridViewColumnHeader;
                        if (header?.Tag != null)
                        {
                            var tag = header.Tag.ToString();
                            var width = column.ActualWidth;
                            if (width <= 0) continue;

                            ConfigurationService.Instance.Update(cfg =>
                            {
                                switch (tag)
                                {
                                    case "Name": cfg.ColNameWidth = width; break;
                                    case "Size": cfg.ColSizeWidth = width; break;
                                    case "Type": cfg.ColTypeWidth = width; break;
                                    case "ModifiedDate": cfg.ColModifiedDateWidth = width; break;
                                    case "CreatedTime": cfg.ColCreatedTimeWidth = width; break;
                                }
                            });
                        }
                    }

                    ConfigurationService.Instance.SaveNow();
                }
            }
            catch { }
        }

        #endregion

        #region 自动适配列宽

        public void AutoSizeGridViewColumn(GridViewColumn column, FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesList == null || column == null) return;

            var listView = fileBrowser.FilesList;
            var headerForTag = column.Header as GridViewColumnHeader;
            var tagName = headerForTag?.Tag?.ToString();
            if (!string.IsNullOrEmpty(tagName))
            {
                if (!IsColumnVisible(tagName)) return;
            }

            double padding = 24;
            double maxWidth = 0;

            var header = column.Header as GridViewColumnHeader;
            var headerText = header?.Content?.ToString() ?? "";
            maxWidth = Math.Max(maxWidth, MeasureTextWidth(headerText, listView) + padding);

            foreach (var item in listView.Items)
            {
                string cellText = GetCellTextForColumn(item, column, header);
                maxWidth = Math.Max(maxWidth, MeasureTextWidth(cellText, listView) + padding);
            }

            if (maxWidth < 50) maxWidth = 50;
            column.Width = Math.Ceiling(maxWidth);
        }

        private string GetCellTextForColumn(object item, GridViewColumn column, GridViewColumnHeader header)
        {
            if (item == null) return "";
            if (column.DisplayMemberBinding is System.Windows.Data.Binding binding && binding.Path != null)
            {
                var prop = item.GetType().GetProperty(binding.Path.Path);
                var val = prop?.GetValue(item);
                return val?.ToString() ?? "";
            }
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
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return tb.DesiredSize.Width;
        }

        #endregion

        #region 调整列表视图列宽度

        public void AdjustListViewColumnWidths(FileBrowserControl fileBrowser)
        {
            if (fileBrowser?.FilesGrid == null) return;
            try
            {
                LoadColumnWidths(fileBrowser);
            }
            catch { }
        }

        #endregion

        #region 列可见性管理

        public string GetVisibleColumnsForCurrentMode()
        {
            var key = _getCurrentModeKey?.Invoke();
            return key switch
            {
                "Library" => Config.VisibleColumns_Library,
                "Tag" => Config.VisibleColumns_Tag,
                _ => Config.VisibleColumns_Path
            };
        }

        public void SetVisibleColumnsForCurrentMode(string csv)
        {
            var key = _getCurrentModeKey?.Invoke();
            ConfigurationService.Instance.Update(cfg =>
            {
                switch (key)
                {
                    case "Library": cfg.VisibleColumns_Library = csv; break;
                    case "Tag": cfg.VisibleColumns_Tag = csv; break;
                    default: cfg.VisibleColumns_Path = csv; break;
                }
            });
        }

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
                    double w = ResolveColumnWidth(tag, column);
                    column.Width = Math.Max(40, w);
                }
                else
                {
                    RememberColumnWidth(tag, column);
                    column.Width = 0;
                }
            }
        }

        public bool IsColumnVisible(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return true;
            var visibleCsv = GetVisibleColumnsForCurrentMode() ?? "";
            var set = new HashSet<string>(visibleCsv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            return set.Contains(tag);
        }

        #endregion
    }
}
