using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
                    Background = Brushes.Black
                };

                // 定义行：标题行 + 视频播放器 + 进度条 + 控制按钮行
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 标题区域
                var titlePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Margin = new Thickness(0, 0, 0, 5)
                };

                var titleIcon = new TextBlock
                {
                    Text = "🎬",
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White,
                    Margin = new Thickness(8, 6, 4, 6)
                };

                var titleText = new TextBlock
                {
                    Text = "视频文件",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White
                };

                var fileInfo = new TextBlock
                {
                    Text = $"· {Path.GetFileName(filePath)}",
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                    Margin = new Thickness(8, 6, 8, 6)
                };

                titlePanel.Children.Add(titleIcon);
                titlePanel.Children.Add(titleText);
                titlePanel.Children.Add(fileInfo);

                Grid.SetRow(titlePanel, 0);
                mainGrid.Children.Add(titlePanel);

                // 创建 MediaElement 播放视频
                var mediaElement = new MediaElement
                {
                    Source = new Uri(filePath),
                    LoadedBehavior = MediaState.Manual,
                    UnloadedBehavior = MediaState.Manual,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Volume = 0.5 // 默认音量50%
                };

                Grid.SetRow(mediaElement, 1);
                mainGrid.Children.Add(mediaElement);

                // 进度条和时间显示区域
                var progressPanel = new Grid
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Margin = new Thickness(0, 5, 0, 0)
                };

                progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // 当前时间
                var currentTimeText = new TextBlock
                {
                    Text = "00:00",
                    Foreground = Brushes.White,
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
                    Foreground = Brushes.White,
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 4, 8, 4)
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
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                    Margin = new Thickness(0, 5, 0, 5)
                };

                // 播放/暂停按钮
                var playPauseButton = new Button
                {
                    Content = "▶️ 播放",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                // 停止按钮
                var stopButton = new Button
                {
                    Content = "⏹️ 停止",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                // 音量图标
                var volumeText = new TextBlock
                {
                    Text = "🔊",
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White,
                    Margin = new Thickness(10, 0, 5, 0)
                };

                // 音量滑块
                var volumeSlider = new Slider
                {
                    Minimum = 0,
                    Maximum = 1,
                    Value = 0.5,
                    Width = 80,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 10, 0)
                };

                // 默认播放器打开按钮
                var openButton = new Button
                {
                    Content = "🔓 默认播放器",
                    Margin = new Thickness(5),
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = new SolidColorBrush(Color.FromRgb(96, 125, 139)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                // 状态变量
                bool isPlaying = false;
                bool isDraggingProgress = false;

                // 媒体打开事件
                mediaElement.MediaOpened += (s, e) =>
                {
                    if (mediaElement.NaturalDuration.HasTimeSpan)
                    {
                        var duration = mediaElement.NaturalDuration.TimeSpan;
                        totalTimeText.Text = $"{(int)duration.TotalMinutes:D2}:{duration.Seconds:D2}";
                        progressSlider.Maximum = duration.TotalSeconds;
                    }
                };

                // 按钮样式
                var buttonStyle = new Style(typeof(Button));
                buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(33, 150, 243))));
                buttonStyle.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
                buttonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
                
                var hoverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0, 102, 204))));
                buttonStyle.Triggers.Add(hoverTrigger);
                
                playPauseButton.Style = buttonStyle;
                stopButton.Style = buttonStyle;
                openButton.Style = buttonStyle;

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

                // 音量控制事件
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

                // 视频结束事件（循环播放）
                mediaElement.MediaEnded += (s, e) =>
                {
                    mediaElement.Position = TimeSpan.Zero;
                    mediaElement.Play();
                    isPlaying = true;
                    playPauseButton.Content = "⏸️ 暂停";
                };

                // 默认播放器打开事件
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
                return PreviewHelper.CreateErrorPreview($"无法加载视频: {ex.Message}");
            }
        }
    }
}

