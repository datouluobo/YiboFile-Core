using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using OoiMRR.Controls;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using System.Windows.Markup; // For XamlReader

namespace OoiMRR.Previews
{
    /// <summary>
    /// 视频文件预览 (LibVLCSharp) - 增强版 (Loading + Floating UI)
    /// </summary>
    public class VideoPreview : IPreviewProvider
    {
        private static LibVLC _libVLC;
        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();

        private void Log(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "videopreview_debug.txt"), $"{DateTime.Now}: {message}\n");
            }
            catch { }
        }

        public UIElement CreatePreview(string filePath)
        {
            Log($"Creating preview for: {filePath}");
            var mainGrid = new Grid { Background = Brushes.Black, Focusable = true };

            var loadingGrid = new Grid
            {
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Panel.SetZIndex(loadingGrid, 100);

            var loadingBar = new ProgressBar
            {
                IsIndeterminate = true,
                Width = 150,
                Height = 4,
                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215))
            };
            var loadingText = new TextBlock
            {
                Text = "正在准备播放引擎...",
                Foreground = Brushes.Gray,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 0)
            };
            var loadingStack = new StackPanel();
            loadingStack.Children.Add(loadingBar);
            loadingStack.Children.Add(loadingText);
            loadingGrid.Children.Add(loadingStack);
            mainGrid.Children.Add(loadingGrid);

            var controlsOverlay = new Grid
            {
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20),
                Opacity = 0,
                IsHitTestVisible = true
            };
            Panel.SetZIndex(controlsOverlay, 200);

            var controlsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.3 }
            };
            controlsOverlay.Children.Add(controlsBorder);
            mainGrid.Children.Add(controlsOverlay);

            var cts = new CancellationTokenSource();

            var reportProgress = new Action<double, string>((val, msg) =>
            {
                if (cts.IsCancellationRequested) return;
                try
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (cts.IsCancellationRequested) return;
                        loadingBar.IsIndeterminate = false;
                        loadingBar.Value = val;
                        loadingText.Text = $"{msg} ({(int)val}%)";
                    });
                }
                catch { }
            });

            mainGrid.Unloaded += (s, e) =>
            {
                Log("Preview Unloaded. Cancelling init...");
                cts.Cancel();
            };

            Task.Run(() =>
            {
                try
                {
                    if (cts.IsCancellationRequested) return;
                    reportProgress(10, "正在加载核心组件...");

                    InitializeLibVLC_CoreOnly();

                    if (cts.IsCancellationRequested) return;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (cts.IsCancellationRequested) return;
                        try
                        {
                            reportProgress(40, "正在初始化播放引擎...");
                            EnsureLibVLCInstance();

                            var mediaPlayer = new MediaPlayer(_libVLC);

                            reportProgress(60, "正在解析媒体文件...");
                            var media = new Media(_libVLC, filePath, FromType.FromPath);
                            media.Parse(MediaParseOptions.ParseLocal);
                            mediaPlayer.Media = media;
                            media.Dispose(); // MediaPlayer keeps a reference

                            reportProgress(80, "正在创建渲染界面...");
                            var videoView = new VideoView { MediaPlayer = mediaPlayer };
                            mainGrid.Children.Add(videoView);
                            Panel.SetZIndex(videoView, 0);

                            ControlsData controls = CreateControlsContent(mediaPlayer);
                            controlsBorder.Child = controls.RootElement;

                            SetupInteraction(mainGrid, controlsOverlay, mediaPlayer, loadingGrid, controls);

                            mediaPlayer.Buffering += (s, e) =>
                            {
                                try
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        if (loadingGrid.Visibility == Visibility.Visible)
                                        {
                                            double bufferHigh = 80 + (e.Cache * 0.19);
                                            loadingBar.Value = bufferHigh;
                                            loadingText.Text = $"正在缓冲... ({(int)bufferHigh}%)";
                                        }
                                    });
                                }
                                catch { }
                            };

                            Log("Starting Playback...");
                            mediaPlayer.Play();
                            reportProgress(100, "准备就绪");

                            mainGrid.Unloaded += (s, e) =>
                            {
                                controls.StopTimer();
                                Task.Run(() =>
                                {
                                    try { mediaPlayer?.Stop(); mediaPlayer?.Dispose(); } catch { }
                                });
                                videoView?.Dispose();
                            };
                        }
                        catch (Exception uiEx)
                        {
                            Log($"UI Thread Init Error: {uiEx}");
                            loadingText.Text = "初始化失败: " + uiEx.Message;
                            loadingBar.Foreground = Brushes.Red;
                            loadingBar.IsIndeterminate = false;
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log($"Init Error: {ex}");
                    if (!cts.IsCancellationRequested)
                    {
                        try
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                loadingText.Text = "初始化失败: " + ex.Message;
                                loadingBar.IsIndeterminate = false;
                                loadingBar.Value = 0;
                                loadingBar.Foreground = Brushes.Red;
                            });
                        }
                        catch { }
                    }
                }
            }, cts.Token);

            return mainGrid;
        }

        private void InitializeLibVLC_CoreOnly()
        {
            if (_isInitialized) return;

            lock (_initLock)
            {
                if (_isInitialized) return;

                Log("Initializing LibVLC Core (DLLs)...");
                string appPath = AppDomain.CurrentDomain.BaseDirectory;
                string libvlcPath = Path.Combine(appPath, "Dependencies", "libvlc", "win-x64");

                if (!Directory.Exists(libvlcPath)) throw new DirectoryNotFoundException($"LibVLC not found at {libvlcPath}");

                try
                {
                    Core.Initialize(libvlcPath);
                    Log("Core.Initialize Done.");
                }
                catch (Exception ex)
                {
                    Log($"Core.Initialize Failed: {ex}");
                    throw;
                }
            }
        }

        private void EnsureLibVLCInstance()
        {
            if (_isInitialized && _libVLC != null) return;

            lock (_initLock)
            {
                if (_isInitialized && _libVLC != null) return;

                Log("Instantiating LibVLC (UI Thread)...");
                try
                {
                    _libVLC = new LibVLC();
                    _isInitialized = true;
                    Log("LibVLC Instance Created.");
                }
                catch (Exception ex)
                {
                    Log($"new LibVLC() Failed: {ex}");
                    throw;
                }
            }
        }

        private class ControlsData
        {
            public FrameworkElement RootElement;
            public Slider TimeSlider;
            public TextBlock TimeText;
            public TextBlock PlayIcon;
            public void StopTimer() { }
        }

        private ControlsData CreateControlsContent(MediaPlayer player)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            var playBtn = new Button
            {
                Style = CreateTransparentButtonStyle(),
                Width = 30,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var playIcon = new TextBlock
            {
                Text = "\uE769",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            playBtn.Content = playIcon;
            playBtn.Click += (s, e) =>
            {
                if (player.IsPlaying) player.Pause();
                else player.Play();
            };
            panel.Children.Add(playBtn);

            var timeText = new TextBlock
            {
                Text = "00:00 / 00:00",
                Foreground = Brushes.White,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas, Courier New"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            panel.Children.Add(timeText);

            var slider = new Slider
            {
                Width = 200,
                VerticalAlignment = VerticalAlignment.Center,
                IsMoveToPointEnabled = true,
                Style = CreateSlimSliderStyle()
            };
            panel.Children.Add(slider);

            var volBtn = new Button
            {
                Style = CreateTransparentButtonStyle(),
                Width = 30,
                Height = 30,
                Margin = new Thickness(8, 0, 0, 0),
                Content = new TextBlock
                {
                    Text = "\uE767",
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 14,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            };
            volBtn.Click += (s, e) => { player.Mute = !player.Mute; };
            panel.Children.Add(volBtn);

            return new ControlsData
            {
                RootElement = panel,
                TimeSlider = slider,
                TimeText = timeText,
                PlayIcon = playIcon
            };
        }

        private void SetupInteraction(Grid mainGrid, Grid overlay, MediaPlayer player, Grid loadingGrid, ControlsData controls)
        {
            player.Playing += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    loadingGrid.Visibility = Visibility.Collapsed;
                    controls.PlayIcon.Text = "\uE769";
                });
            };

            player.Paused += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() => controls.PlayIcon.Text = "\uE768");
            };

            player.Stopped += (s, e) =>
            {
                Application.Current.Dispatcher.Invoke(() => controls.PlayIcon.Text = "\uE768");
            };

            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };

            void ShowControls()
            {
                if (overlay.Opacity < 0.1)
                {
                    var fadeIn = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200));
                    overlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                }
                hideTimer.Stop();
                hideTimer.Start();
            }

            void HideControls()
            {
                var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(500));
                overlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }

            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                HideControls();
            };

            mainGrid.MouseMove += (s, e) => ShowControls();
            mainGrid.MouseEnter += (s, e) => ShowControls();
            mainGrid.MouseLeave += (s, e) => HideControls();

            bool isDragging = false;
            long totalTime = 0;

            player.LengthChanged += (s, e) =>
            {
                totalTime = e.Length;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    controls.TimeSlider.Maximum = totalTime;
                });
            };

            player.TimeChanged += (s, e) =>
            {
                if (!isDragging)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var t = TimeSpan.FromMilliseconds(e.Time);
                        var tot = TimeSpan.FromMilliseconds(totalTime);
                        controls.TimeText.Text = $"{t:mm\\:ss} / {tot:mm\\:ss}";
                        controls.TimeSlider.Value = e.Time;
                    });
                }
            };

            controls.TimeSlider.PreviewMouseDown += (s, e) => isDragging = true;
            controls.TimeSlider.PreviewMouseUp += (s, e) =>
            {
                isDragging = false;
                player.Time = (long)controls.TimeSlider.Value;
            };
        }

        private Style CreateTransparentButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Button.CursorProperty, Cursors.Hand));

            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(contentFactory);
            template.VisualTree = factory;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));
            return style;
        }

        private Style CreateSlimSliderStyle()
        {
            string xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' 
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='Slider'>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='Slider'>
                <Grid Background='Transparent' Height='20' VerticalAlignment='Center'>
                    <Track x:Name='PART_Track'>
                        <Track.DecreaseRepeatButton>
                            <RepeatButton Command='Slider.DecreaseLarge'>
                                <RepeatButton.Template>
                                    <ControlTemplate TargetType='RepeatButton'>
                                        <Border Height='2' Background='#FFFFFF' CornerRadius='1' VerticalAlignment='Center' HitTestVisible='False'/>
                                    </ControlTemplate>
                                </RepeatButton.Template>
                            </RepeatButton>
                        </Track.DecreaseRepeatButton>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Command='Slider.IncreaseLarge'>
                                <RepeatButton.Template>
                                    <ControlTemplate TargetType='RepeatButton'>
                                        <Border Height='2' Background='#66888888' CornerRadius='1' VerticalAlignment='Center' HitTestVisible='False'/>
                                    </ControlTemplate>
                                </RepeatButton.Template>
                            </RepeatButton>
                        </Track.IncreaseRepeatButton>
                        <Track.Thumb>
                            <Thumb>
                                <Thumb.Template>
                                    <ControlTemplate TargetType='Thumb'>
                                        <Grid>
                                            <Ellipse Width='10' Height='10' Fill='White' StrokeThickness='0'/>
                                        </Grid>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";
            try
            {
                return (Style)XamlReader.Parse(xaml);
            }
            catch
            {
                return null;
            }
        }
    }
}
