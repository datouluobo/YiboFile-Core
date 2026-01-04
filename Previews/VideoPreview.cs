using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using FFMpegCore;
using OoiMRR.Controls;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 视频文件预览
    /// </summary>
    public class VideoPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                // 使用 Grid 布局
                var mainGrid = new Grid
                {
                    Background = Brushes.White
                };

                // 定义行：标题行 + 视频播放器 + 进度条 + 控制按钮行
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 统一工具栏
                var toolbar = new TextPreviewToolbar
                {
                    FileName = Path.GetFileName(filePath),
                    FileIcon = "🎬",
                    ShowSearch = false,
                    ShowWordWrap = false,
                    ShowEncoding = false,
                    ShowViewToggle = false,
                    ShowFormat = false
                };

                toolbar.OpenExternalRequested += (s, e) => PreviewHelper.OpenInDefaultApp(filePath);

                // 检查文件格式
                var ext = Path.GetExtension(filePath)?.ToLower();
                bool isRealMediaFormat = ext == ".rmvb" || ext == ".rm";

                // 如果是rmvb/rm格式，添加转码按钮
                if (isRealMediaFormat)
                {
                    var convertButton = new Button
                    {
                        Content = "🔄 转换为MP4格式",
                        Padding = new Thickness(12, 6, 12, 6),
                        Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)),
                        Foreground = Brushes.White,
                        BorderThickness = new Thickness(0),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        FontSize = 13
                    };

                    convertButton.Click += async (s, e) =>
                    {
                        try
                        {
                            convertButton.IsEnabled = false;
                            convertButton.Content = "⏳ 转换中...";

                            try
                            {
                                // 生成输出路径（同目录，同名）
                                string directory = Path.GetDirectoryName(filePath);
                                string baseName = Path.GetFileNameWithoutExtension(filePath);
                                string outputPath = Path.Combine(directory, baseName + ".mp4");

                                // 如果文件已存在，添加序号
                                outputPath = GetUniqueFilePath(outputPath);

                                // 在后台线程执行转换
                                string errorMessage = null;
                                bool success = await Task.Run(() =>
                                {
                                    // 先检查是否有缓存文件
                                    string cachePath = GetCachedTranscodePath(filePath);
                                    if (!string.IsNullOrEmpty(cachePath) && File.Exists(cachePath))
                                    {
                                        try
                                        {
                                            // 尝试移动（剪切）缓存文件到目标目录
                                            File.Move(cachePath, outputPath);
                                            return true;
                                        }
                                        catch (IOException)
                                        {
                                            // 如果移动失败（可能文件被占用），则复制文件
                                            try
                                            {
                                                File.Copy(cachePath, outputPath, true);
                                                return true;
                                            }
                                            catch (Exception ex)
                                            {
                                                errorMessage = $"无法复制缓存文件: {ex.Message}";
                                                return false;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            errorMessage = $"无法移动缓存文件: {ex.Message}";
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        // 没有缓存，执行转码
                                        return ConvertVideoToMp4(filePath, outputPath, out errorMessage);
                                    }
                                });

                                if (success)
                                {
                                    convertButton.Content = "✅ 转换成功";
                                }
                                else
                                {
                                    MessageBox.Show(
                                        errorMessage ?? "转换失败",
                                        "转换错误",
                                        MessageBoxButton.OK,
                                        MessageBoxImage.Error);
                                    convertButton.IsEnabled = true;
                                    convertButton.Content = "🔄 转换为MP4格式";
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                                convertButton.IsEnabled = true;
                                convertButton.Content = "🔄 转换为MP4格式";
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"转换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            convertButton.IsEnabled = true;
                            convertButton.Content = "🔄 转换为MP4格式";
                        }
                    };

                    toolbar.CustomActionContent = convertButton;
                }

                // 将 toolbar 赋值给 titlePanel 变量，保持与后续代码兼容（全屏切换逻辑）
                var titlePanel = toolbar;
                Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

                // 创建容器用于应用旋转Transform
                var videoContainer = new Viewbox
                {
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // 创建 MediaElement 播放视频
                var mediaElement = new MediaElement
                {
                    LoadedBehavior = MediaState.Manual,
                    UnloadedBehavior = MediaState.Manual,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Volume = 0.5 // 默认音量50%
                };

                // 保存临时文件路径（用于清理）
                string tempMp4Path = null;

                // 添加清理机制：当预览被卸载时停止播放并清理资源
                mainGrid.Unloaded += (s, e) =>
                {
                    // 暂时注释掉Unloaded清理，因为窗口由于Restore等操作可能会导致临时的Unloaded触发
                    // 这会导致预览丢失。资源的最终清理交由 PreviewService.CleanupPreviousPreview 处理
                    /*
                    try
                    {
                        // 停止播放
                        mediaElement.Stop();
                        mediaElement.Close();
                        mediaElement.Source = null;

                        // 注意：不删除缓存文件，因为它是可重用的
                        // 缓存文件会在文件被修改后自动失效（因为缓存文件名基于文件修改时间）
                    }
                    catch { }
                    */
                };

                // 先显示加载提示，避免UI卡住
                var loadingText = new TextBlock
                {
                    Text = "⏳ 正在加载视频...",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                    Foreground = Brushes.Gray
                };
                videoContainer.Child = loadingText;

                Grid.SetRow(videoContainer, 1);
                mainGrid.Children.Add(videoContainer);

                // 异步加载视频，避免阻塞UI线程
                Task.Run(() =>
                {
                    Application.Current?.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
                    {
                        try
                        {
                            if (isRealMediaFormat)
                            {
                                // RealMedia格式（rmvb/rm）使用临时后台转码
                                // 先检查缓存文件是否存在
                                string cachePath = GetCachedTranscodePath(filePath);

                                if (File.Exists(cachePath))
                                {
                                    // 缓存文件存在，直接使用
                                    tempMp4Path = cachePath;
                                    Application.Current?.Dispatcher.BeginInvoke(() =>
                                    {
                                        try
                                        {
                                            mediaElement.Source = new Uri(tempMp4Path);
                                            videoContainer.Child = mediaElement;
                                        }
                                        catch (Exception ex)
                                        {
                                            var errorText = new TextBlock
                                            {
                                                Text = $"无法加载转码后的视频: {ex.Message}",
                                                HorizontalAlignment = HorizontalAlignment.Center,
                                                VerticalAlignment = VerticalAlignment.Center,
                                                TextWrapping = TextWrapping.Wrap,
                                                Foreground = Brushes.Red,
                                                Margin = new Thickness(20)
                                            };
                                            videoContainer.Child = errorText;
                                        }
                                    });
                                }
                                else
                                {
                                    // 缓存文件不存在，需要转码
                                    // 显示转码进度面板
                                    var transcodePanel = new StackPanel
                                    {
                                        Orientation = Orientation.Vertical,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Margin = new Thickness(30)
                                    };

                                    var progressText = new TextBlock
                                    {
                                        Text = "正在转码视频以支持预览...",
                                        FontSize = 14,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        Margin = new Thickness(0, 0, 0, 10)
                                    };
                                    transcodePanel.Children.Add(progressText);

                                    var progressBar = new ProgressBar
                                    {
                                        Width = 400,
                                        Height = 20,
                                        Minimum = 0,
                                        Maximum = 100,
                                        Value = 0,
                                        HorizontalAlignment = HorizontalAlignment.Center
                                    };
                                    transcodePanel.Children.Add(progressBar);

                                    var progressPercent = new TextBlock
                                    {
                                        Text = "0%",
                                        FontSize = 12,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        Margin = new Thickness(0, 5, 0, 0),
                                        Foreground = Brushes.Gray
                                    };
                                    transcodePanel.Children.Add(progressPercent);

                                    videoContainer.Child = transcodePanel;

                                    // 后台转码（在Task.Run内部定义tempMp4Path，避免闭包问题）
                                    Task.Run(() =>
                                    {
                                        try
                                        {
                                            // 使用缓存路径作为临时MP4文件
                                            tempMp4Path = cachePath;

                                            // 执行转码（带进度回调）
                                            bool success = ConvertVideoToMp4WithProgress(filePath, tempMp4Path, (progress) =>
                                            {
                                                Application.Current?.Dispatcher.BeginInvoke(() =>
                                                {
                                                    progressBar.Value = progress;
                                                    progressPercent.Text = $"{progress:F1}%";
                                                });
                                            }, out string errorMsg);

                                            if (success && File.Exists(tempMp4Path))
                                            {
                                                // 转码成功，加载到MediaElement
                                                Application.Current?.Dispatcher.BeginInvoke(() =>
                                                {
                                                    try
                                                    {
                                                        mediaElement.Source = new Uri(tempMp4Path);
                                                        videoContainer.Child = mediaElement;

                                                        // 视频播放结束后不删除缓存文件（保留供下次使用）
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        var errorText = new TextBlock
                                                        {
                                                            Text = $"无法加载转码后的视频: {ex.Message}",
                                                            HorizontalAlignment = HorizontalAlignment.Center,
                                                            VerticalAlignment = VerticalAlignment.Center,
                                                            TextWrapping = TextWrapping.Wrap,
                                                            Foreground = Brushes.Red,
                                                            Margin = new Thickness(20)
                                                        };
                                                        videoContainer.Child = errorText;
                                                    }
                                                });
                                            }
                                            else
                                            {
                                                // 转码失败，显示错误
                                                Application.Current?.Dispatcher.BeginInvoke(() =>
                                                {
                                                    var errorText = new TextBlock
                                                    {
                                                        Text = $"转码失败: {errorMsg}\n请使用默认播放器打开",
                                                        HorizontalAlignment = HorizontalAlignment.Center,
                                                        VerticalAlignment = VerticalAlignment.Center,
                                                        TextWrapping = TextWrapping.Wrap,
                                                        Foreground = Brushes.Red,
                                                        Margin = new Thickness(20)
                                                    };
                                                    videoContainer.Child = errorText;
                                                });
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Application.Current?.Dispatcher.BeginInvoke(() =>
                                            {
                                                var errorText = new TextBlock
                                                {
                                                    Text = $"转码失败: {ex.Message}",
                                                    HorizontalAlignment = HorizontalAlignment.Center,
                                                    VerticalAlignment = VerticalAlignment.Center,
                                                    TextWrapping = TextWrapping.Wrap,
                                                    Foreground = Brushes.Red,
                                                    Margin = new Thickness(20)
                                                };
                                                videoContainer.Child = errorText;
                                            });
                                        }
                                    });
                                }
                            }
                            else
                            {
                                // 其他格式，正常加载到MediaElement
                                mediaElement.Source = new Uri(filePath);
                                videoContainer.Child = mediaElement;
                            }
                        }
                        catch (Exception ex)
                        {
                            // 如果设置Source失败，显示错误信息
                            var errorText = new TextBlock
                            {
                                Text = $"无法加载视频: {ex.Message}\n请使用默认播放器打开",
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                TextWrapping = TextWrapping.Wrap,
                                Foreground = Brushes.Red,
                                Margin = new Thickness(20)
                            };
                            videoContainer.Child = errorText;
                        }
                    }));
                });

                // 当前旋转角度（用于手动旋转）
                double currentRotationAngle = 0;



                // 进度条和时间显示区域
                var progressPanel = new Grid
                {
                    Background = (Brush)Application.Current.FindResource("AppBackgroundBrush"),
                    Margin = new Thickness(0, 5, 0, 0)
                };

                progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // 当前时间
                var currentTimeText = new TextBlock
                {
                    Text = "00:00",
                    Foreground = (Brush)Application.Current.FindResource("PreviewTextPrimaryBrush"),
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 4, 8, 4)
                };
                Grid.SetColumn(currentTimeText, 0);
                progressPanel.Children.Add(currentTimeText);

                // 进度条
                var progressSlider = new Slider
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 4, 5, 4)
                };
                Grid.SetColumn(progressSlider, 1);
                progressPanel.Children.Add(progressSlider);

                // 总时长
                var totalTimeText = new TextBlock
                {
                    Text = "00:00",
                    Foreground = (Brush)Application.Current.FindResource("PreviewTextPrimaryBrush"),
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 4, 8, 4)
                };
                Grid.SetColumn(totalTimeText, 2);
                progressPanel.Children.Add(totalTimeText);

                Grid.SetRow(progressPanel, 2);
                mainGrid.Children.Add(progressPanel);

                // 控制按钮区域（响应式）
                var controlPanel = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = (Brush)Application.Current.FindResource("AppBackgroundBrush"),
                    Margin = new Thickness(0, 5, 0, 5)
                };
                controlPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                controlPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var playPauseButton = new Button
                {
                    Content = "▶️ 播放",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var stopButton = new Button
                {
                    Content = "⏹️ 停止",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var volumeText = new TextBlock
                {
                    Text = "🔊",
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)Application.Current.FindResource("PreviewTextPrimaryBrush"),
                    Margin = new Thickness(10, 0, 5, 0)
                };

                var volumeSlider = new Slider
                {
                    Minimum = 0,
                    Maximum = 1,
                    Value = 0.5,
                    Width = 80,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                var openButton = new Button
                {
                    Content = "🔓 默认播放器",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    MinWidth = 110,
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var rewind5Button = new Button
                {
                    Content = "⏪ 5s",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var forward5Button = new Button
                {
                    Content = "⏩ 5s",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var fullscreenButton = new Button
                {
                    Content = "⛶ 全屏",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var rotateButton = new Button
                {
                    Content = "🔄 旋转",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                var speedCombo = new ComboBox
                {
                    Width = 70,
                    Margin = new Thickness(5),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsEditable = false
                };
                var si05 = new ComboBoxItem { Content = "0.5×", Tag = 0.5 };
                var si10 = new ComboBoxItem { Content = "1×", Tag = 1.0 };
                var si15 = new ComboBoxItem { Content = "1.5×", Tag = 1.5 };
                var si20 = new ComboBoxItem { Content = "2×", Tag = 2.0 };
                speedCombo.Items.Add(si05);
                speedCombo.Items.Add(si10);
                speedCombo.Items.Add(si15);
                speedCombo.Items.Add(si20);
                speedCombo.SelectedItem = si10;

                bool isPlaying = false;
                bool isDraggingProgress = false;
                bool isFullscreen = false;
                var buttonStyleResource = Application.Current.TryFindResource("ModernButtonStyle") as Style;
                playPauseButton.Style = buttonStyleResource;
                stopButton.Style = buttonStyleResource;
                openButton.Style = buttonStyleResource;
                rewind5Button.Style = buttonStyleResource;
                forward5Button.Style = buttonStyleResource;
                fullscreenButton.Style = buttonStyleResource;
                var comboStyle = Application.Current.TryFindResource("ModernComboBoxStyle") as Style;
                if (comboStyle != null) speedCombo.Style = comboStyle;

                // 左侧控件（播放、停止、快退/快进、音量、倍速）使用 WrapPanel 以在窄宽度下自动换行
                var leftControls = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetColumn(leftControls, 0);

                // 右侧控件（旋转、全屏、默认播放器）始终右对齐并保留空间
                var rightControls = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                Grid.SetColumn(rightControls, 1);

                var clickOverlay = new Border
                {
                    Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                clickOverlay.MouseLeftButtonDown += (s, e) =>
                {
                    if (isPlaying)
                    {
                        mediaElement.Pause();
                        playPauseButton.Content = "▶️ 播放";
                        isPlaying = false;
                    }
                    else
                    {
                        mediaElement.Play();
                        playPauseButton.Content = "⏸️ 暂停";
                        isPlaying = true;
                    }
                };
                Grid.SetRow(clickOverlay, 1);
                mainGrid.Children.Add(clickOverlay);

                mediaElement.MediaOpened += (s, e) =>
                {
                    if (mediaElement.NaturalDuration.HasTimeSpan)
                    {
                        var duration = mediaElement.NaturalDuration.TimeSpan;
                        totalTimeText.Text = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
                        progressSlider.Maximum = duration.TotalSeconds;
                    }
                };

                // 处理媒体加载失败的情况
                mediaElement.MediaFailed += (s, e) =>
                {
                    // 如果媒体加载失败，显示错误信息
                    var errorText = new TextBlock
                    {
                        Text = $"无法加载视频: {e.ErrorException?.Message ?? "未知错误"}\n请使用默认播放器打开",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.Red,
                        Margin = new Thickness(20)
                    };
                    videoContainer.Child = errorText;
                };



                playPauseButton.Click += (s, e) =>
                {
                    if (isPlaying)
                    {
                        mediaElement.Pause();
                        playPauseButton.Content = "▶️ 播放";
                        isPlaying = false;
                    }
                    else
                    {
                        mediaElement.Play();
                        playPauseButton.Content = "⏸️ 暂停";
                        isPlaying = true;
                    }
                };

                stopButton.Click += (s, e) =>
                {
                    mediaElement.Stop();
                    playPauseButton.Content = "▶️ 播放";
                    isPlaying = false;
                    progressSlider.Value = 0;
                    currentTimeText.Text = "00:00";
                };

                volumeSlider.ValueChanged += (s, e) =>
                {
                    mediaElement.Volume = volumeSlider.Value;
                    if (volumeSlider.Value == 0)
                        volumeText.Text = "🔇";
                    else if (volumeSlider.Value < 0.5)
                        volumeText.Text = "🔉";
                    else
                        volumeText.Text = "🔊";
                };

                rewind5Button.Click += (s, e) =>
                {
                    var pos = mediaElement.Position - TimeSpan.FromSeconds(5);
                    if (pos < TimeSpan.Zero) pos = TimeSpan.Zero;
                    mediaElement.Position = pos;
                };
                forward5Button.Click += (s, e) =>
                {
                    var duration = mediaElement.NaturalDuration.HasTimeSpan ? mediaElement.NaturalDuration.TimeSpan : TimeSpan.MaxValue;
                    var pos = mediaElement.Position + TimeSpan.FromSeconds(5);
                    if (pos > duration) pos = duration;
                    mediaElement.Position = pos;
                };

                speedCombo.SelectionChanged += (s, e) =>
                {
                    if (speedCombo.SelectedItem is ComboBoxItem item && item.Tag is double rate)
                        mediaElement.SpeedRatio = rate;
                };

                // 进度条拖动事件
                progressSlider.PreviewMouseDown += (s, e) => isDraggingProgress = true;
                progressSlider.PreviewMouseUp += (s, e) =>
                {
                    isDraggingProgress = false;
                    if (mediaElement.NaturalDuration.HasTimeSpan)
                    {
                        var duration = mediaElement.NaturalDuration.TimeSpan;
                        mediaElement.Position = TimeSpan.FromSeconds(progressSlider.Value);
                    }
                };

                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                timer.Tick += (s, e) =>
                {
                    if (!isDraggingProgress && mediaElement.NaturalDuration.HasTimeSpan)
                    {
                        var position = mediaElement.Position;
                        var duration = mediaElement.NaturalDuration.TimeSpan;

                        currentTimeText.Text = $"{(int)position.TotalMinutes:D2}:{position.Seconds:D2}";

                        if (duration.TotalSeconds > 0)
                        {
                            progressSlider.Value = position.TotalSeconds;
                        }
                    }
                };
                timer.Start();

                // 视频结束事件（循环播放）
                mediaElement.MediaEnded += (s, e) =>
                {
                    mediaElement.Position = TimeSpan.Zero;
                    mediaElement.Play();
                    isPlaying = true;
                    playPauseButton.Content = "⏸️ 暂停";
                };

                openButton.Click += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"无法打开文件: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                };

                fullscreenButton.Click += (s, e) =>
                {
                    var window = Window.GetWindow(mainGrid);
                    if (window == null) return;
                    isFullscreen = !isFullscreen;
                    if (isFullscreen)
                    {
                        window.Tag = new { window.WindowStyle, window.WindowState, window.Topmost };
                        titlePanel.Visibility = Visibility.Collapsed;
                        progressPanel.Visibility = Visibility.Collapsed;
                        controlPanel.Visibility = Visibility.Collapsed;
                        window.WindowStyle = WindowStyle.None;
                        window.WindowState = WindowState.Maximized;
                        window.Topmost = true;
                        fullscreenButton.Content = "⛶ 退出全屏";
                    }
                    else
                    {
                        var prev = window.Tag;
                        titlePanel.Visibility = Visibility.Visible;
                        progressPanel.Visibility = Visibility.Visible;
                        controlPanel.Visibility = Visibility.Visible;
                        window.WindowStyle = WindowStyle.SingleBorderWindow;
                        window.WindowState = WindowState.Normal;
                        window.Topmost = false;
                        fullscreenButton.Content = "⛶ 全屏";
                    }
                };

                // 旋转按钮点击事件 - 顺时针旋转90度
                rotateButton.Click += (s, e) =>
                {
                    // 每次点击顺时针旋转90度
                    currentRotationAngle = (currentRotationAngle + 90) % 360;

                    // 应用旋转Transform
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(new RotateTransform(currentRotationAngle));
                    videoContainer.RenderTransform = transformGroup;
                    videoContainer.RenderTransformOrigin = new Point(0.5, 0.5);
                };

                mainGrid.Focusable = true;
                mainGrid.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Space)
                    {
                        playPauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.Left)
                    {
                        rewind5Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.Right)
                    {
                        forward5Button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.Up)
                    {
                        var v = Math.Min(1.0, volumeSlider.Value + 0.05);
                        volumeSlider.Value = v;
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.Down)
                    {
                        var v = Math.Max(0.0, volumeSlider.Value - 0.05);
                        volumeSlider.Value = v;
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.F)
                    {
                        fullscreenButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.Escape && isFullscreen)
                    {
                        fullscreenButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                };

                mediaElement.MouseLeftButtonDown += (s, e) =>
                {
                    playPauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };

                leftControls.Children.Add(playPauseButton);
                leftControls.Children.Add(stopButton);
                leftControls.Children.Add(rewind5Button);
                leftControls.Children.Add(forward5Button);
                leftControls.Children.Add(volumeText);
                leftControls.Children.Add(volumeSlider);
                leftControls.Children.Add(speedCombo);

                rightControls.Children.Add(rotateButton);
                rightControls.Children.Add(fullscreenButton);
                rightControls.Children.Add(openButton);

                controlPanel.Children.Add(leftControls);
                controlPanel.Children.Add(rightControls);
                Grid.SetRow(controlPanel, 3);
                mainGrid.Children.Add(controlPanel);

                return mainGrid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载视频: {ex.Message}");
            }
        }

        /// <summary>
        /// 将视频文件转换为MP4格式
        /// </summary>
        private bool ConvertVideoToMp4(string inputPath, string outputPath, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                string ffmpegPath = GetFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
                {
                    errorMessage = "未找到 FFmpeg。\n\n转换视频需要 FFmpeg。";
                    return false;
                }

                // FFmpeg转码命令：使用H.264编码，保持原始质量
                string arguments = $"-i \"{inputPath}\" -c:v libx264 -preset medium -crf 23 -c:a aac -b:a 128k -y \"{outputPath}\"";

                bool success = RunFFmpegCommand(ffmpegPath, arguments, out string stdout, out string stderr);

                if (!success)
                {
                    errorMessage = $"转换失败: {stderr}\n\n请确保：\n1. FFmpeg 已正确安装\n2. 文件未被其他程序占用\n3. 有足够的磁盘空间";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"转换失败: {ex.Message}\n\n请确保：\n1. FFmpeg 已正确安装\n2. 文件未被其他程序占用\n3. 有足够的磁盘空间";
                return false;
            }
        }

        /// <summary>
        /// 转码视频并报告进度
        /// </summary>
        private bool ConvertVideoToMp4WithProgress(string inputPath, string outputPath, Action<double> progressCallback, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                string ffmpegPath = GetFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath) || !File.Exists(ffmpegPath))
                {
                    errorMessage = "未找到 FFmpeg";
                    return false;
                }

                // 先获取视频时长
                double duration = GetVideoDuration(inputPath);
                if (duration <= 0)
                {
                    errorMessage = "无法获取视频时长";
                    return false;
                }

                // FFmpeg转码命令，使用进度输出
                string arguments = $"-i \"{inputPath}\" -c:v libx264 -preset medium -crf 23 -c:a aac -b:a 128k -progress pipe:1 -y \"{outputPath}\"";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        errorMessage = "无法启动FFmpeg进程";
                        return false;
                    }

                    // 解析进度输出
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            // FFmpeg进度格式: time=00:00:05.00 bitrate=...
                            var match = Regex.Match(e.Data, @"time=(\d+):(\d+):(\d+\.\d+)");
                            if (match.Success)
                            {
                                double hours = double.Parse(match.Groups[1].Value);
                                double minutes = double.Parse(match.Groups[2].Value);
                                double seconds = double.Parse(match.Groups[3].Value);
                                double currentTime = hours * 3600 + minutes * 60 + seconds;

                                if (duration > 0)
                                {
                                    double progress = (currentTime / duration) * 100;
                                    progressCallback?.Invoke(Math.Min(100, Math.Max(0, progress)));
                                }
                            }
                        }
                    };

                    process.BeginErrorReadLine();

                    bool finished = process.WaitForExit(300000); // 最多5分钟

                    if (!finished)
                    {
                        process.Kill();
                        errorMessage = "转码超时";
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        errorMessage = "转码失败";
                        return false;
                    }

                    return File.Exists(outputPath);
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 获取视频时长（秒）
        /// </summary>
        private double GetVideoDuration(string videoPath)
        {
            try
            {
                string ffprobePath = GetFFprobePath();
                if (string.IsNullOrEmpty(ffprobePath))
                    return 0;

                string arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"";

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffprobePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                        return 0;

                    string output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                        return 0;
                    }

                    if (double.TryParse(output.Trim(), out double duration))
                        return duration;
                }
            }
            catch { }

            return 0;
        }

        /// <summary>
        /// 获取FFmpeg可执行文件路径
        /// </summary>
        private string GetFFmpegPath()
        {
            try
            {
                var options = GlobalFFOptions.Current;
                if (options != null && !string.IsNullOrEmpty(options.BinaryFolder))
                {
                    string ffmpegPath = Path.Combine(options.BinaryFolder, "ffmpeg.exe");
                    if (File.Exists(ffmpegPath))
                    {
                        return ffmpegPath;
                    }
                }

                // 回退：从系统PATH查找
                return "ffmpeg";
            }
            catch
            {
                return "ffmpeg";
            }
        }

        /// <summary>
        /// 获取FFprobe可执行文件路径
        /// </summary>
        private string GetFFprobePath()
        {
            try
            {
                var options = GlobalFFOptions.Current;
                if (options != null && !string.IsNullOrEmpty(options.BinaryFolder))
                {
                    string ffprobePath = Path.Combine(options.BinaryFolder, "ffprobe.exe");
                    if (File.Exists(ffprobePath))
                    {
                        return ffprobePath;
                    }
                }

                // 回退：从系统PATH查找
                return "ffprobe";
            }
            catch
            {
                return "ffprobe";
            }
        }

        /// <summary>
        /// 运行FFmpeg命令并捕获输出
        /// </summary>
        private bool RunFFmpegCommand(string ffmpegPath, string arguments, out string stdout, out string stderr)
        {
            stdout = string.Empty;
            stderr = string.Empty;

            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        return false;
                    }

                    // 异步读取输出
                    var stdoutBuilder = new StringBuilder();
                    var stderrBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            stdoutBuilder.AppendLine(e.Data);
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                            stderrBuilder.AppendLine(e.Data);
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 等待进程完成（最多10分钟，转码可能需要较长时间）
                    bool finished = process.WaitForExit(600000);

                    if (!finished)
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                        return false;
                    }

                    stdout = stdoutBuilder.ToString().Trim();
                    stderr = stderrBuilder.ToString().Trim();

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                stderr = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 获取转码缓存文件路径（基于文件路径和修改时间）
        /// </summary>
        private string GetCachedTranscodePath(string filePath)
        {
            try
            {
                // 获取文件信息
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                    return null;

                // 使用文件路径和修改时间生成哈希值
                string hashInput = $"{filePath}_{fileInfo.LastWriteTime.Ticks}";
                byte[] hashBytes = SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(hashInput));
                string hashStr = BitConverter.ToString(hashBytes).Replace("-", "");
                string hash = hashStr.Length >= 16 ? hashStr.Substring(0, 16) : hashStr;

                // 生成缓存文件名
                string cacheDir = Path.Combine(Path.GetTempPath(), "OoiMRR_VideoCache");
                if (!Directory.Exists(cacheDir))
                {
                    Directory.CreateDirectory(cacheDir);
                }

                return Path.Combine(cacheDir, $"preview_{hash}.mp4");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取唯一文件路径（如果文件已存在，添加序号）
        /// </summary>
        private string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            string directory = Path.GetDirectoryName(filePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int counter = 1;
            string newFilePath;
            do
            {
                newFilePath = Path.Combine(directory, $"{fileNameWithoutExtension}({counter}){extension}");
                counter++;
            }
            while (File.Exists(newFilePath));

            return newFilePath;
        }
    }
}
