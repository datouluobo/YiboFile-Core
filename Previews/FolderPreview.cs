using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Controls;

namespace YiboFile.Previews
{
    /// <summary>
    /// 文件夹预览 - 显示与左侧一样的文件列表
    /// </summary>
    public class FolderPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string folderPath)
        {
            try
            {
                // 创建文件列表数据
                var items = new List<FolderItemInfo>();

                // 添加文件夹
                var directories = Directory.GetDirectories(folderPath);
                foreach (var dir in directories)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    items.Add(new FolderItemInfo
                    {
                        Name = dirInfo.Name,
                        FullPath = dirInfo.FullName,
                        Type = "文件夹",
                        Size = "-",
                        ModifiedDate = dirInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                        CreatedTime = FormatTimeAgo(dirInfo.CreationTime),
                        IsDirectory = true
                    });
                }

                // 添加文件
                var files = Directory.GetFiles(folderPath);
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    items.Add(new FolderItemInfo
                    {
                        Name = fileInfo.Name,
                        FullPath = fileInfo.FullName,
                        Type = Path.GetExtension(file),
                        Size = PreviewHelper.FormatFileSize(fileInfo.Length),
                        ModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                        CreatedTime = FormatTimeAgo(fileInfo.CreationTime),
                        IsDirectory = false
                    });
                }

                // 创建与左侧一样的ListView
                var listView = new ListView
                {
                    ItemsSource = items
                };
                listView.SetResourceReference(Control.BackgroundProperty, "AppBackgroundBrush");
                listView.ItemContainerStyle = Application.Current.FindResource("FileListItemStyle") as Style;

                var gridView = new GridView();
                gridView.ColumnHeaderContainerStyle = Application.Current.FindResource("FileListColumnHeaderStyle") as Style;

                // 与左侧完全一致的列定义
                var colName = new GridViewColumn
                {
                    Header = "名称",
                    Width = 200,
                    DisplayMemberBinding = new System.Windows.Data.Binding("Name")
                };
                gridView.Columns.Add(colName);

                var colSize = new GridViewColumn
                {
                    Header = "大小",
                    Width = 100,
                    DisplayMemberBinding = new System.Windows.Data.Binding("Size"),
                    HeaderContainerStyle = Application.Current.FindResource("NonResizableFileColumnHeaderStyle") as Style
                };
                gridView.Columns.Add(colSize);

                var colType = new GridViewColumn
                {
                    Header = "类型",
                    Width = 100,
                    DisplayMemberBinding = new System.Windows.Data.Binding("Type"),
                    HeaderContainerStyle = Application.Current.FindResource("NonResizableFileColumnHeaderStyle") as Style
                };
                gridView.Columns.Add(colType);

                var colModified = new GridViewColumn
                {
                    Header = "修改日期",
                    Width = 150,
                    DisplayMemberBinding = new System.Windows.Data.Binding("ModifiedDate"),
                    HeaderContainerStyle = Application.Current.FindResource("NonResizableFileColumnHeaderStyle") as Style
                };
                gridView.Columns.Add(colModified);

                var colCreated = new GridViewColumn
                {
                    Header = "创建",
                    Width = 50,
                    DisplayMemberBinding = new System.Windows.Data.Binding("CreatedTime"),
                    HeaderContainerStyle = Application.Current.FindResource("NonResizableFileColumnHeaderStyle") as Style
                };
                gridView.Columns.Add(colCreated);

                listView.View = gridView;

                // 右键菜单：列显示/隐藏
                var cm = new ContextMenu();
                cm.Items.Add(CreateColumnToggleMenuItem("名称", colName, 200));
                cm.Items.Add(CreateColumnToggleMenuItem("大小", colSize, 100));
                cm.Items.Add(CreateColumnToggleMenuItem("类型", colType, 100));
                cm.Items.Add(CreateColumnToggleMenuItem("修改日期", colModified, 150));
                cm.Items.Add(CreateColumnToggleMenuItem("创建", colCreated, 50));
                listView.ContextMenu = cm;

                // 添加双击事件处理
                listView.MouseDoubleClick += (s, e) =>
                {
                    if (listView.SelectedItem is FolderItemInfo selectedItem)
                    {
                        if (selectedItem.IsDirectory)
                        {
                            // 文件夹：在新标签页中打开
                            // 通过静态回调通知主窗口打开新标签页
                            if (PreviewFactory.OnOpenFolderInNewTab != null)
                            {
                                PreviewFactory.OnOpenFolderInNewTab(selectedItem.FullPath);
                            }
                        }
                        else
                        {
                            // 文件：使用系统默认程序打开
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = selectedItem.FullPath,
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                };

                // 创建主容器
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 标题栏
                // 统一工具栏
                var toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(folderPath),
                    FileIcon = "📁",
                    ShowSearch = false,
                    ShowWordWrap = false,
                    ShowEncoding = false,
                    ShowViewToggle = false,
                    ShowFormat = false
                };
                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenFolderInExplorer(folderPath);

                Grid.SetRow(toolbar, 0);
                grid.Children.Add(toolbar);

                if (items.Count == 0)
                {
                    var emptyText = new TextBlock
                    {
                        Text = "文件夹为空",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.Gray,
                        FontSize = 14
                    };
                    Grid.SetRow(emptyText, 1);
                    grid.Children.Add(emptyText);
                    return grid;
                }

                Grid.SetRow(listView, 1);
                grid.Children.Add(listView);

                return grid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取文件夹: {ex.Message}");
            }
        }

        private string FormatTimeAgo(DateTime createdTime)
        {
            var timeSpan = DateTime.Now - createdTime;

            if (timeSpan.TotalSeconds < 60)
                return $"{(int)timeSpan.TotalSeconds}s";

            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes}m";

            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours}h";

            if (timeSpan.TotalDays < 30)
                return $"{(int)timeSpan.TotalDays}d";

            if (timeSpan.TotalDays < 365)
                return $"{(int)(timeSpan.TotalDays / 30)}mo";

            return $"{(int)(timeSpan.TotalDays / 365)}y";
        }

        private MenuItem CreateColumnToggleMenuItem(string title, GridViewColumn column, double defaultWidth)
        {
            var mi = new MenuItem
            {
                Header = $"列: {title}",
                IsCheckable = true,
                IsChecked = true
            };
            mi.Checked += (s, e) =>
            {
                // 恢复默认宽度（若之前被隐藏成0）
                if (column.Width <= 1)
                {
                    column.Width = defaultWidth;
                }
            };
            mi.Unchecked += (s, e) =>
            {
                // 隐藏列：将宽度置0
                column.Width = 0;
            };
            return mi;
        }

        private class FolderItemInfo
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public string Type { get; set; }
            public string Size { get; set; }
            public string ModifiedDate { get; set; }
            public string CreatedTime { get; set; }
            public bool IsDirectory { get; set; }
            public string Notes { get; set; } // Binding fix
        }
    }
}

