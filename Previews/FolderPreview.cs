using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OoiMRR.Previews
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
                    ItemsSource = items,
                    Background = Brushes.White
                };

                var gridView = new GridView();
                
                // 与左侧完全一致的列定义
                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "名称",
                    Width = 200,
                    DisplayMemberBinding = new System.Windows.Data.Binding("Name")
                });

                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "大小",
                    Width = 100,
                    DisplayMemberBinding = new System.Windows.Data.Binding("Size")
                });

                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "类型",
                    Width = 100,
                    DisplayMemberBinding = new System.Windows.Data.Binding("Type")
                });

                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "修改日期",
                    Width = 150,
                    DisplayMemberBinding = new System.Windows.Data.Binding("ModifiedDate")
                });

                gridView.Columns.Add(new GridViewColumn
                {
                    Header = "创建",
                    Width = 50,
                    DisplayMemberBinding = new System.Windows.Data.Binding("CreatedTime")
                });

                listView.View = gridView;

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

                if (items.Count == 0)
                {
                    return new TextBlock
                    {
                        Text = "文件夹为空",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.Gray,
                        FontSize = 14
                    };
                }

                return listView;
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

        private class FolderItemInfo
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public string Type { get; set; }
            public string Size { get; set; }
            public string ModifiedDate { get; set; }
            public string CreatedTime { get; set; }
            public bool IsDirectory { get; set; }
        }
    }
}
