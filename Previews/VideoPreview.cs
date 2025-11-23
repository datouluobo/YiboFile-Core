using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
                    Background = Brushes.White
                };

                // 定义行：标题行 + 视频播放器 + 进度条 + 控制按钮行
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                // 标题区域
                var buttons = new List<Button> { PreviewHelper.CreateOpenButton(filePath) };
                var titlePanel = PreviewHelper.CreateTitlePanel("🎬", $"视频文件: {Path.GetFileName(filePath)}", buttons);
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
                    Background = (Brush)Application.Current.FindResource("PreviewPanelBackgroundBrush"),
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

                // 控制按钮区域
                var controlPanel = new UniformGrid
                {
                    Columns = 10,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = (Brush)Application.Current.FindResource("PreviewPanelBackgroundBrush"),
                    Margin = new Thickness(0, 5, 0, 5)
                };

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

                controlPanel.Children.Add(playPauseButton);
                controlPanel.Children.Add(stopButton);
                controlPanel.Children.Add(rewind5Button);
                controlPanel.Children.Add(forward5Button);
                controlPanel.Children.Add(volumeText);
                controlPanel.Children.Add(volumeSlider);
                controlPanel.Children.Add(speedCombo);
                controlPanel.Children.Add(fullscreenButton);
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
