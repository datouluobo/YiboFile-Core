using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            // 7Z文件使用7-Zip工具读取文件列表
            else if (extension == ".7z")
            {
                return Create7zPreview(filePath);
            }
            // RAR文件使用7-Zip工具读取文件列表（7-Zip支持RAR格式）
            else if (extension == ".rar")
            {
                return CreateRarPreview(filePath);
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
                // 使用Grid布局以支持填充剩余空间（与7Z预览保持一致）
                var mainGrid = new Grid
                {
                    Background = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    ClipToBounds = true
                };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 统计信息
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 文件列表（填充剩余空间）

                // 标题栏
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("📦", $"ZIP 压缩包: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

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
                    var errorPanel = PreviewHelper.CreateErrorPreview($"无法读取压缩包: {ex.Message}");
                    Grid.SetRow(errorPanel as UIElement, 2);
                    mainGrid.Children.Add(errorPanel as UIElement);
                    return mainGrid;
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

                Grid.SetRow(infoPanel, 1);
                mainGrid.Children.Add(infoPanel);

                // 文件列表
                if (fileList.Count > 0)
                {
                    var listView = new ListView
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
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

                    Grid.SetRow(listView, 2);
                    mainGrid.Children.Add(listView);
                }
                else
                {
                    var emptyPanel = PreviewHelper.CreateEmptyPreview("压缩包为空");
                    Grid.SetRow(emptyPanel as UIElement, 2);
                    mainGrid.Children.Add(emptyPanel as UIElement);
                }

                return mainGrid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取ZIP文件: {ex.Message}");
            }
        }

        #endregion

        #region 7Z 预览

        private UIElement Create7zPreview(string filePath)
        {
            try
            {
                // 使用Grid布局以支持填充剩余空间
                var mainGrid = new Grid
                {
                    Background = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    ClipToBounds = true
                };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 统计信息
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 文件列表（填充剩余空间）

                // 标题栏
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("📦", $"7Z 压缩包: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

                // 读取7Z文件列表
                var fileList = new List<(string name, long size)>();
                long totalSize = 0;
                int fileCount = 0;
                int folderCount = 0;

                try
                {
                    // 查找7-Zip可执行文件
                    var sevenZipPath = FindSevenZipPath();
                    if (string.IsNullOrEmpty(sevenZipPath))
                    {
                        var errorPanel = PreviewHelper.CreateErrorPreview("无法找到 7-Zip 工具\n请确保已安装 7-Zip 或将其放置在 Dependencies\\7-Zip 目录中。");
                        Grid.SetRow(errorPanel as UIElement, 2);
                        mainGrid.Children.Add(errorPanel as UIElement);
                        return mainGrid;
                    }

                    // 注册编码提供程序以支持 GBK 等编码
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                    // 使用7-Zip列出文件内容（使用-slt参数获取更详细的列表格式）
                    var arguments = $"l -slt \"{filePath}\"";
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = sevenZipPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        WorkingDirectory = Path.GetDirectoryName(sevenZipPath)
                    };

                    byte[] outputBytes = null;
                    string output = "";
                    string error = "";

                    using (var process = Process.Start(processInfo))
                    {
                        if (process != null)
                        {
                            // 读取原始字节以支持多种编码
                            using (var ms = new MemoryStream())
                            {
                                var buffer = new byte[4096];
                                int bytesRead;
                                while ((bytesRead = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    ms.Write(buffer, 0, bytesRead);
                                }
                                outputBytes = ms.ToArray();
                            }

                            error = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            if (process.ExitCode != 0 && process.ExitCode != 1)
                            {
                                throw new Exception($"7-Zip 列出文件失败，退出代码: {process.ExitCode}\n错误信息: {error}");
                            }
                        }
                    }

                    // 尝试多种编码解析输出
                    var encodings = new List<Encoding>();
                    encodings.Add(Encoding.UTF8);
                    try { encodings.Add(Encoding.GetEncoding("GBK")); } catch { }
                    try { encodings.Add(Encoding.GetEncoding("GB2312")); } catch { }
                    try { encodings.Add(Encoding.GetEncoding("GB18030")); } catch { }
                    encodings.Add(Encoding.Default);

                    bool parsed = false;
                    int bestScore = int.MaxValue;
                    string bestOutput = "";

                    foreach (var encoding in encodings)
                    {
                        try
                        {
                            var testOutput = encoding.GetString(outputBytes);
                            // 计算编码质量分数（无效字符越少越好）
                            int score = CountInvalidChars(testOutput);

                            // 检查是否包含7z输出特征（Path =, Size = 等）
                            if (testOutput.Contains("Path = ") && testOutput.Contains("Size = "))
                            {
                                if (score < bestScore)
                                {
                                    bestScore = score;
                                    bestOutput = testOutput;
                                    parsed = true;
                                }
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (parsed)
                    {
                        output = bestOutput;
                    }
                    else if (outputBytes != null)
                    {
                        // 如果所有编码都失败，使用UTF-8作为默认
                        output = Encoding.UTF8.GetString(outputBytes);
                    }

                    // 解析7-Zip输出（-slt格式是键值对格式，更容易解析）
                    Parse7zOutputSlt(output, fileList, ref fileCount, ref folderCount, ref totalSize);

                    // 对文件列表按路径排序，保持文件夹结构
                    fileList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    var errorPanel = PreviewHelper.CreateErrorPreview($"无法读取压缩包: {ex.Message}");
                    Grid.SetRow(errorPanel as UIElement, 2);
                    mainGrid.Children.Add(errorPanel as UIElement);
                    return mainGrid;
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

                Grid.SetRow(infoPanel, 1);
                mainGrid.Children.Add(infoPanel);

                // 文件列表
                if (fileList.Count > 0)
                {
                    var listView = new ListView
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
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

                    Grid.SetRow(listView, 2);
                    mainGrid.Children.Add(listView);
                }
                else
                {
                    var emptyPanel = PreviewHelper.CreateEmptyPreview("压缩包为空");
                    Grid.SetRow(emptyPanel as UIElement, 2);
                    mainGrid.Children.Add(emptyPanel as UIElement);
                }

                return mainGrid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取7Z文件: {ex.Message}");
            }
        }

        private void Parse7zOutputSlt(string output, List<(string name, long size)> fileList, ref int fileCount, ref int folderCount, ref long totalSize)
        {
            if (string.IsNullOrEmpty(output))
                return;

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            string currentPath = null;
            long currentSize = 0;
            bool isDirectory = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (string.IsNullOrEmpty(trimmedLine))
                {
                    // 空行表示一个文件条目结束
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        if (isDirectory)
                        {
                            folderCount++;
                        }
                        else
                        {
                            fileCount++;
                            fileList.Add((currentPath, currentSize));
                            totalSize += currentSize;
                        }
                        currentPath = null;
                        currentSize = 0;
                        isDirectory = false;
                    }
                    continue;
                }

                // 解析键值对格式
                if (trimmedLine.StartsWith("Path = "))
                {
                    currentPath = trimmedLine.Substring(7).Trim();
                }
                else if (trimmedLine.StartsWith("Size = "))
                {
                    var sizeStr = trimmedLine.Substring(7).Trim();
                    if (long.TryParse(sizeStr, out long size))
                    {
                        currentSize = size;
                    }
                }
                else if (trimmedLine.StartsWith("Attributes = "))
                {
                    var attrs = trimmedLine.Substring(12).Trim();
                    isDirectory = attrs.StartsWith("D") || attrs.Contains(" directory");
                }
            }

            // 处理最后一个条目
            if (!string.IsNullOrEmpty(currentPath))
            {
                if (isDirectory)
                {
                    folderCount++;
                }
                else
                {
                    fileCount++;
                    fileList.Add((currentPath, currentSize));
                    totalSize += currentSize;
                }
            }
        }

        private int CountInvalidChars(string text)
        {
            if (string.IsNullOrEmpty(text))
                return int.MaxValue;

            int invalidCount = 0;
            foreach (char c in text)
            {
                // 检查是否为替换字符（U+FFFD）或控制字符（除了常见的换行符等）
                if (c == '\uFFFD' || (char.IsControl(c) && c != '\r' && c != '\n' && c != '\t'))
                {
                    invalidCount++;
                }
            }

            return invalidCount;
        }

        private string FindSevenZipPath()
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7-Zip", "7z.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Dependencies", "7-Zip", "7zG.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7zG.exe"),
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files\7-Zip\7zG.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7zG.exe"
            };

            return possiblePaths.FirstOrDefault(File.Exists);
        }

        #endregion

        #region RAR 预览

        private UIElement CreateRarPreview(string filePath)
        {
            try
            {
                // 使用Grid布局以支持填充剩余空间（与ZIP/7Z预览保持一致）
                var mainGrid = new Grid
                {
                    Background = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    ClipToBounds = true
                };
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 标题栏
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 统计信息
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 文件列表（填充剩余空间）

                // 标题栏
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("📦", $"RAR 压缩包: {Path.GetFileName(filePath)}", buttons);
                Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

                // 读取RAR文件列表（使用7-Zip工具，因为7-Zip支持RAR格式）
                var fileList = new List<(string name, long size)>();
                long totalSize = 0;
                int fileCount = 0;
                int folderCount = 0;

                try
                {
                    // 查找7-Zip可执行文件
                    var sevenZipPath = FindSevenZipPath();
                    if (string.IsNullOrEmpty(sevenZipPath))
                    {
                        var errorText = new TextBlock
                        {
                            Text = "无法找到 7-Zip 工具。请确保已安装 7-Zip 或将其放置在 Dependencies\\7-Zip 目录中。",
                            Foreground = Brushes.Red,
                            Margin = new Thickness(10),
                            TextWrapping = TextWrapping.Wrap
                        };
                        Grid.SetRow(errorText, 2);
                        mainGrid.Children.Add(errorText);
                        return mainGrid;
                    }

                    // 注册编码提供程序以支持 GBK 等编码
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                    // 使用7-Zip列出RAR文件内容（使用-slt参数获取更详细的列表格式）
                    var arguments = $"l -slt \"{filePath}\"";
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = sevenZipPath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                        WorkingDirectory = Path.GetDirectoryName(sevenZipPath)
                    };

                    byte[] outputBytes = null;
                    string output = "";
                    string error = "";

                    using (var process = Process.Start(processInfo))
                    {
                        if (process != null)
                        {
                            // 读取原始字节以支持多种编码
                            using (var ms = new MemoryStream())
                            {
                                var buffer = new byte[4096];
                                int bytesRead;
                                while ((bytesRead = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    ms.Write(buffer, 0, bytesRead);
                                }
                                outputBytes = ms.ToArray();
                            }

                            error = process.StandardError.ReadToEnd();
                            process.WaitForExit();

                            if (process.ExitCode != 0 && process.ExitCode != 1)
                            {
                                throw new Exception($"7-Zip 列出RAR文件失败，退出代码: {process.ExitCode}\n错误信息: {error}");
                            }
                        }
                    }

                    // 尝试多种编码解析输出
                    var encodings = new List<Encoding>();
                    encodings.Add(Encoding.UTF8);
                    try { encodings.Add(Encoding.GetEncoding("GBK")); } catch { }
                    try { encodings.Add(Encoding.GetEncoding("GB2312")); } catch { }
                    try { encodings.Add(Encoding.GetEncoding("GB18030")); } catch { }
                    encodings.Add(Encoding.Default);

                    bool parsed = false;
                    int bestScore = int.MaxValue;
                    string bestOutput = "";

                    foreach (var encoding in encodings)
                    {
                        try
                        {
                            var testOutput = encoding.GetString(outputBytes);
                            // 计算编码质量分数（无效字符越少越好）
                            int score = CountInvalidChars(testOutput);

                            // 检查是否包含7z输出特征（Path =, Size = 等）
                            if (testOutput.Contains("Path = ") && testOutput.Contains("Size = "))
                            {
                                if (score < bestScore)
                                {
                                    bestScore = score;
                                    bestOutput = testOutput;
                                    parsed = true;
                                }
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (parsed)
                    {
                        output = bestOutput;
                    }
                    else if (outputBytes != null)
                    {
                        // 如果所有编码都失败，使用UTF-8作为默认
                        output = Encoding.UTF8.GetString(outputBytes);
                    }

                    // 解析7-Zip输出（-slt格式是键值对格式，更容易解析）
                    Parse7zOutputSlt(output, fileList, ref fileCount, ref folderCount, ref totalSize);

                    // 对文件列表按路径排序，保持文件夹结构
                    fileList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    var errorText = new TextBlock
                    {
                        Text = $"无法读取压缩包: {ex.Message}",
                        Foreground = Brushes.Red,
                        Margin = new Thickness(10),
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetRow(errorText, 2);
                    mainGrid.Children.Add(errorText);
                    return mainGrid;
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

                Grid.SetRow(infoPanel, 1);
                mainGrid.Children.Add(infoPanel);

                // 文件列表
                if (fileList.Count > 0)
                {
                    var listView = new ListView
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch
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

                    Grid.SetRow(listView, 2);
                    mainGrid.Children.Add(listView);
                }
                else
                {
                    var emptyPanel = PreviewHelper.CreateEmptyPreview("压缩包为空");
                    Grid.SetRow(emptyPanel as UIElement, 2);
                    mainGrid.Children.Add(emptyPanel as UIElement);
                }

                return mainGrid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法读取RAR文件: {ex.Message}");
            }
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

            var button = PreviewHelper.CreateOpenButton(filePath);
            button.Margin = new Thickness(10);
            panel.Children.Add(button);

            return panel;
        }

        #endregion
    }
}

