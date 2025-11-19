using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OoiMRR.Previews
{
    /// <summary>
    /// 音频文件预览
    /// </summary>
    public class AudioPreview : IPreviewProvider
    {
        public UIElement CreatePreview(string filePath)
        {
            try
            {
                // 使用 Grid 布局
                var mainGrid = new Grid
                {
                    Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
                };

                // 定义行：标题行 + 音频信息 + 进度条 + 控制按钮行
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // 标题区域
                var titlePanel = PreviewHelper.CreateTitlePanel("🎵", "音频文件");
                Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

                // 文件信息区域
                var infoPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin = new Thickness(15, 10, 15, 10)
                };

                var fileNameText = new TextBlock
                {
                    Text = Path.GetFileName(filePath),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                var fileInfo = new FileInfo(filePath);
                var fileSizeText = new TextBlock
                {
                    Text = $"文件大小: {PreviewHelper.FormatFileSize(fileInfo.Length)}",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                    Margin = new Thickness(0, 0, 0, 5)
                };

                infoPanel.Children.Add(fileNameText);
                infoPanel.Children.Add(fileSizeText);

                Grid.SetRow(infoPanel, 1);
                mainGrid.Children.Add(infoPanel);

                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    return PreviewHelper.CreateErrorPreview($"音频文件不存在: {filePath}");
                }
                
                // 确保使用绝对路径
                if (!Path.IsPathRooted(filePath))
                {
                    filePath = Path.GetFullPath(filePath);
                }

                // 创建 MediaElement 播放音频（隐藏，只用于播放）
                var mediaElement = new MediaElement
                {
                    LoadedBehavior = MediaState.Manual,
                    UnloadedBehavior = MediaState.Manual,
                    Volume = 0.5, // 默认音量50%
                    Visibility = Visibility.Collapsed // 隐藏MediaElement，只用于播放
                };

                Grid.SetRow(mediaElement, 4);
                mainGrid.Children.Add(mediaElement);

                // 标记MediaElement是否可用
                bool mediaElementAvailable = false;
                
                // 设置MediaElement的Source（MediaElement只支持Uri，不支持文件流）
                // 注意：需要在添加到Grid之后再设置Source，确保MediaElement已加载
                // MediaElement可能不支持某些格式（如FLAC），但不影响显示预览界面
                try
                {
                    // MediaElement直接使用本地文件路径的Uri（与VideoPreview保持一致）
                    mediaElement.Source = new Uri(filePath);
                    mediaElementAvailable = true;
                }
                catch
                {
                    // MediaElement不支持此格式，但继续显示预览界面
                    mediaElementAvailable = false;
                }

                // 进度条和时间显示区域
                var progressPanel = new Grid
                {
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                    Margin = new Thickness(15, 5, 15, 5)
                };

                progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // 当前时间
                var currentTimeText = new TextBlock
                {
                    Text = "00:00",
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0),
                    MinWidth = 45
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
                    Margin = new Thickness(5, 0, 5, 0)
                };
                Grid.SetColumn(progressSlider, 1);
                progressPanel.Children.Add(progressSlider);

                // 总时长
                var totalTimeText = new TextBlock
                {
                    Text = "00:00",
                    Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0),
                    MinWidth = 45
                };
                Grid.SetColumn(totalTimeText, 2);
                progressPanel.Children.Add(totalTimeText);

                Grid.SetRow(progressPanel, 2);
                mainGrid.Children.Add(progressPanel);

                // 控制按钮区域
                var controlPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(15, 10, 15, 15)
                };

                // 播放/暂停按钮
                var playPauseButton = new Button
                {
                    Content = "▶️ 播放",
                    Margin = new Thickness(5),
                    Padding = new Thickness(15, 8, 15, 8),
                    Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontSize = 13
                };

                // 停止按钮
                var stopButton = new Button
                {
                    Content = "⏹️ 停止",
                    Margin = new Thickness(5),
                    Padding = new Thickness(15, 8, 15, 8),
                    Background = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontSize = 13
                };

                // 音量图标
                var volumeText = new TextBlock
                {
                    Text = "🔊",
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(15, 0, 5, 0)
                };

                // 音量滑块
                var volumeSlider = new Slider
                {
                    Minimum = 0,
                    Maximum = 1,
                    Value = 0.5,
                    Width = 100,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 15, 0)
                };

                // 默认播放器打开按钮
                var openButton = new Button
                {
                    Content = "🔓 默认播放器",
                    Margin = new Thickness(5),
                    Padding = new Thickness(15, 8, 15, 8),
                    Background = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    FontSize = 13
                };

                // 状态变量
                bool isPlaying = false;
                bool isDraggingProgress = false;

                // 媒体打开事件（仅在MediaElement可用时）
                if (mediaElementAvailable)
                {
                    mediaElement.MediaOpened += (s, e) =>
                    {
                        if (mediaElement.NaturalDuration.HasTimeSpan)
                        {
                            var duration = mediaElement.NaturalDuration.TimeSpan;
                            totalTimeText.Text = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
                            progressSlider.Maximum = duration.TotalSeconds;
                        }
                    };
                    
                    // 媒体加载失败事件
                    mediaElement.MediaFailed += (s, e) =>
                    {
                        // MediaElement不支持此格式，禁用播放功能
                        playPauseButton.IsEnabled = false;
                        playPauseButton.Content = "⚠️ 格式不支持";
                        playPauseButton.ToolTip = "此音频格式不支持内置播放，请使用默认播放器打开";
                        stopButton.IsEnabled = false;
                        volumeSlider.IsEnabled = false;
                        progressSlider.IsEnabled = false;
                        
                        // 显示提示信息
                        var formatWarning = new TextBlock
                        {
                            Text = "⚠️ 此音频格式不支持内置播放器，请使用默认播放器打开",
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(15, 5, 15, 10),
                            TextWrapping = TextWrapping.Wrap
                        };
                        Grid.SetRow(formatWarning, 1);
                        mainGrid.Children.Insert(mainGrid.Children.Count - 1, formatWarning);
                    };
                }

                // 按钮样式
                var buttonStyle = new Style(typeof(Button));
                buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                buttonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
                buttonStyle.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));
                
                var playHoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                playHoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 102, 204))));
                buttonStyle.Triggers.Add(playHoverTrigger);
                
                playPauseButton.Style = buttonStyle;
                playPauseButton.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));

                var stopButtonStyle = new Style(typeof(Button));
                stopButtonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                stopButtonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
                stopButtonStyle.Setters.Add(new Setter(Button.CursorProperty, System.Windows.Input.Cursors.Hand));
                var stopHoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                stopHoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(69, 90, 100))));
                stopButtonStyle.Triggers.Add(stopHoverTrigger);
                stopButton.Style = stopButtonStyle;
                stopButton.Background = new SolidColorBrush(Color.FromRgb(96, 125, 139));

                openButton.Style = stopButtonStyle;
                openButton.Background = new SolidColorBrush(Color.FromRgb(96, 125, 139));

                // 如果MediaElement不可用，禁用播放按钮并显示提示
                if (!mediaElementAvailable)
                {
                    playPauseButton.IsEnabled = false;
                    playPauseButton.Content = "⚠️ 格式不支持";
                    playPauseButton.ToolTip = "此音频格式不支持内置播放，请使用默认播放器打开";
                    stopButton.IsEnabled = false;
                    volumeSlider.IsEnabled = false;
                    progressSlider.IsEnabled = false;
                    
                    // 添加提示信息
                    var formatWarning = new TextBlock
                    {
                        Text = "⚠️ 此音频格式不支持内置播放器，请使用默认播放器打开",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(15, 5, 15, 10),
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetRow(formatWarning, 1);
                    mainGrid.Children.Insert(mainGrid.Children.Count - 1, formatWarning);
                }
                else
                {
                    // 播放/暂停按钮事件
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

                    // 停止按钮事件
                    stopButton.Click += (s, e) =>
                    {
                        mediaElement.Stop();
                        playPauseButton.Content = "▶️ 播放";
                        isPlaying = false;
                        progressSlider.Value = 0;
                        currentTimeText.Text = "00:00";
                    };
                }

                // 音量控制事件（仅在MediaElement可用时）
                if (mediaElementAvailable)
                {
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

                    // 定时器更新进度
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

                    // 音频结束事件（循环播放）
                    mediaElement.MediaEnded += (s, e) =>
                    {
                        mediaElement.Position = TimeSpan.Zero;
                        mediaElement.Play();
                        isPlaying = true;
                        playPauseButton.Content = "⏸️ 暂停";
                    };
                }

                // 默认播放器打开事件
                openButton.Click += (s, e) =>
                {
                    try
                    {
                        // 如果MediaElement可用且正在播放，先停止播放
                        if (mediaElementAvailable && isPlaying)
                        {
                            mediaElement.Stop();
                            playPauseButton.Content = "▶️ 播放";
                            isPlaying = false;
                        }
                        
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

                controlPanel.Children.Add(playPauseButton);
                controlPanel.Children.Add(stopButton);
                controlPanel.Children.Add(volumeText);
                controlPanel.Children.Add(volumeSlider);
                controlPanel.Children.Add(openButton);

                Grid.SetRow(controlPanel, 3);
                mainGrid.Children.Add(controlPanel);

                return mainGrid;
            }
            catch (Exception ex)
            {
                return PreviewHelper.CreateErrorPreview($"无法加载音频: {ex.Message}");
            }
        }
    }
}
