using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 压缩文件预览
    /// </summary>
    public class ArchivePreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            
            // ZIP文件可以使用.NET内置支持读取文件列表
            if (extension == ".zip")
            {
                return CreateZipPreview(filePath);
            }
            // RAR和7Z需要第三方库支持，显示提示信息
            else if (extension == ".rar" || extension == ".7z")
            {
                return CreateRar7zPreview(filePath, extension);
            }
            // 其他压缩格式显示通用预览
            else
            {
                return CreateGenericArchivePreview(filePath);
            }
        }

        #region ZIP 预览

        private UIElement CreateZipPreview(string filePath)
        {
            try
            {
                var mainPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Background = Brushes.White
                };

                // 标题栏
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("📦", $"ZIP 压缩包: {Path.GetFileName(filePath)}", buttons);
                mainPanel.Children.Add(titlePanel);

                // 读取ZIP文件列表
                var fileList = new List<(string name, long size)>();
                long totalSize = 0;
                int fileCount = 0;
                int folderCount = 0;

                try
                {
                    // 注册编码提供程序以支持 GBK 等编码
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    
                    // 尝试使用 GBK 编码打开 ZIP 文件（适用于中文 Windows 创建的 ZIP）
                    ZipArchive archive = null;
                    try
                    {
                        // 先尝试使用 GBK 编码
                        archive = ZipFile.Open(filePath, ZipArchiveMode.Read, Encoding.GetEncoding("GBK"));
                    }
                    catch
                    {
                        // 如果失败，使用默认编码（UTF-8）
                        archive = ZipFile.OpenRead(filePath);
                    }

                    using (archive)
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                // 文件夹
                                folderCount++;
                            }
                            else
                            {
                                // 文件
                                fileCount++;
                                fileList.Add((entry.FullName, entry.Length));
                                totalSize += entry.Length;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    mainPanel.Children.Add(new TextBlock
                    {
                        Text = $"无法读取压缩包: {ex.Message}",
                        Foreground = Brushes.Red,
                        Margin = new Thickness(10),
                        TextWrapping = TextWrapping.Wrap
                    });
                    return mainPanel;
                }

                // 统计信息
                var infoPanel = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(248, 248, 248)),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 5, 0, 10)
                };

                infoPanel.Child = new TextBlock
                {
                    Text = $"文件数: {fileCount} | 文件夹数: {folderCount} | 总大小: {PreviewHelper.FormatFileSize(totalSize)}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
                };

                mainPanel.Children.Add(infoPanel);

                // 文件列表
                if (fileList.Count > 0)
                {
                    var listView = new ListView
                    {
                        MaxHeight = 400,
                        Margin = new Thickness(10, 0, 10, 10)
                    };

                    var gridView = new GridView();
                    
                    gridView.Columns.Add(new GridViewColumn
                    {
                        Header = "文件名",
                        Width = 300,
                        DisplayMemberBinding = new System.Windows.Data.Binding("FileName")
                    });

                    gridView.Columns.Add(new GridViewColumn
                    {
                        Header = "大小",
                        Width = 100,
                        DisplayMemberBinding = new System.Windows.Data.Binding("FileSize")
                    });

                    listView.View = gridView;

                    // 转换文件列表为显示格式
                    var displayList = fileList.Select(f => new
                    {
                        FileName = f.name,
                        FileSize = f.size > 0 ? PreviewHelper.FormatFileSize(f.size) : "-"
                    }).ToList();

                    listView.ItemsSource = displayList;

                    var scrollViewer = new ScrollViewer
                    {
                        Content = listView,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        MaxHeight = 400
                    };

                    mainPanel.Children.Add(scrollViewer);
                }
                else
                {
                    mainPanel.Children.Add(new TextBlock
                    {
                        Text = "压缩包为空",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = Brushes.Gray,
                        Margin = new Thickness(10, 20, 10, 10)
                    });
                }

                // 按钮区域
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(10)
                };

                var openButton = PreviewHelper.CreateOpenButton(filePath, "🔓 打开压缩包");
                buttonPanel.Children.Add(openButton);
                mainPanel.Children.Add(buttonPanel);

                return mainPanel;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取ZIP文件: {ex.Message}");
            }
        }

        #endregion

        #region RAR/7Z 预览

        private UIElement CreateRar7zPreview(string filePath, string extension)
        {
            var formatName = extension == ".rar" ? "RAR" : "7Z";

            var mainPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // 标题
            var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
            var titlePanel = PreviewHelper.CreateTitlePanel("📦", $"{formatName} 压缩包: {Path.GetFileName(filePath)}", buttons);
            mainPanel.Children.Add(titlePanel);

            // 图标
            var icon = new TextBlock
            {
                Text = "ℹ️",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 20, 0, 20)
            };
            mainPanel.Children.Add(icon);

            // 提示信息
            var infoText = new TextBlock
            {
                Text = $"{formatName} 格式需要使用专门的解压软件才能查看文件列表。\n\n.NET 内置不支持 {formatName} 格式的解压，\n建议使用 WinRAR、7-Zip 或其他解压软件打开。",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                Margin = new Thickness(20, 0, 20, 20)
            };
            mainPanel.Children.Add(infoText);

            // 打开按钮
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var openButton = PreviewHelper.CreateOpenButton(filePath, "🔓 打开压缩包");
            buttonPanel.Children.Add(openButton);
            mainPanel.Children.Add(buttonPanel);

            return mainPanel;
        }

        #endregion

        #region 通用压缩文件预览

        private UIElement CreateGenericArchivePreview(string filePath)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(new TextBlock
            {
                Text = "📦 压缩文件",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(10)
            });

            panel.Children.Add(new TextBlock
            {
                Text = Path.GetFileName(filePath),
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10)
            });

            var button = PreviewHelper.CreateOpenButton(filePath, "使用默认程序打开");
            button.Margin = new Thickness(10);
            panel.Children.Add(button);

            return panel;
        }

        #endregion
    }
}

