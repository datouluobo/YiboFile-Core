using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace YiboFile.Controls
{
    public class VideoPreviewControl : UserControl
    {
        private static LibVLC _libVLC;
        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();
        private static readonly Dictionary<string, long> _playbackHistory = new Dictionary<string, long>();

        private Grid _mainGrid;
        private Grid _loadingGrid;
        private ProgressBar _loadingBar;
        private TextBlock _loadingText;
        private CancellationTokenSource _cts;
        private MediaPlayer _mediaPlayer;

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register("FilePath", typeof(string), typeof(VideoPreviewControl), new PropertyMetadata(null, OnFilePathChanged));

        public string FilePath
        {
            get { return (string)GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (VideoPreviewControl)d;
            control.LoadVideo((string)e.NewValue);
        }

        public VideoPreviewControl()
        {
            InitializeUI();
            this.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            DisposePlayer();
        }

        private void DisposePlayer()
        {
            if (_mediaPlayer != null)
            {
                var player = _mediaPlayer;
                _mediaPlayer = null;
                Task.Run(() =>
                {
                    try
                    {
                        player.Stop();
                        player.Dispose();
                    }
                    catch { }
                });
            }
        }

        private void InitializeUI()
        {
            _mainGrid = new Grid { Background = Brushes.Black, Focusable = true };

            // Loading UI
            _loadingGrid = new Grid
            {
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Panel.SetZIndex(_loadingGrid, 100);

            _loadingBar = new ProgressBar
            {
                IsIndeterminate = true,
                Width = 200,
                Height = 4,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215))
            };
            _loadingText = new TextBlock
            {
                Text = "正在加载视频...",
                Foreground = Brushes.LightGray,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };
            var loadingStack = new StackPanel();
            loadingStack.Children.Add(_loadingBar);
            loadingStack.Children.Add(_loadingText);
            _loadingGrid.Children.Add(loadingStack);
            _mainGrid.Children.Add(_loadingGrid);

            this.Content = _mainGrid;
        }

        private void LoadVideo(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            // Reset UI
            _mainGrid.Children.Clear();
            _mainGrid.Children.Add(_loadingGrid);
            _loadingGrid.Visibility = Visibility.Visible;
            _loadingText.Text = "准备加载...";

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            // Cleanup previous player
            DisposePlayer();

            var token = _cts.Token;

            // Action to update status text
            var reportStatus = new Action<string>((msg) =>
            {
                if (token.IsCancellationRequested) return;
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!token.IsCancellationRequested) _loadingText.Text = msg;
                }));
            });

            Task.Run(() =>
            {
                try
                {
                    if (token.IsCancellationRequested) return;
                    reportStatus("正在加载核心组件...");
                    InitializeLibVLC_CoreOnly();

                    if (token.IsCancellationRequested) return;
                    reportStatus("正在初始化播放引擎...");
                    EnsureLibVLCInstance();

                    if (token.IsCancellationRequested) return;

                    var mediaPlayer = new MediaPlayer(_libVLC);
                    _mediaPlayer = mediaPlayer;

                    reportStatus("正在解析媒体文件...");
                    using (var media = new Media(_libVLC, filePath, FromType.FromPath))
                    {
                        media.Parse(MediaParseOptions.ParseLocal);
                        mediaPlayer.Media = media;
                    }

                    if (token.IsCancellationRequested)
                    {
                        mediaPlayer.Dispose();
                        return;
                    }

                    reportStatus("正在构建播放界面...");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        SetupPlayerUI(filePath, mediaPlayer);
                    });
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            _loadingText.Text = "初始化失败: " + ex.Message;
                            _loadingBar.IsIndeterminate = false;
                        });
                    }
                }
            }, token);
        }

        private void SetupPlayerUI(string filePath, MediaPlayer mediaPlayer)
        {
            // === WriteableBitmap-based video rendering ===
            WriteableBitmap videoBitmap = null;
            Image videoImage = new Image { Stretch = Stretch.Uniform };
            _mainGrid.Children.Add(videoImage);
            _mainGrid.Focusable = true;

            IntPtr videoBuffer = IntPtr.Zero;
            int videoWidth = 0, videoHeight = 0, videoPitch = 0;

            MediaPlayer.LibVLCVideoFormatCb videoFormatCb = (ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines) =>
            {
                Marshal.Copy(new byte[] { (byte)'R', (byte)'V', (byte)'3', (byte)'2' }, 0, chroma, 4);
                videoWidth = (int)width;
                videoHeight = (int)height;
                videoPitch = (int)width * 4;
                pitches = (uint)videoPitch;
                lines = (uint)height;
                int bufferSize = videoPitch * videoHeight;
                videoBuffer = Marshal.AllocHGlobal(bufferSize);

                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    videoBitmap = new WriteableBitmap(videoWidth, videoHeight, 96, 96, PixelFormats.Bgra32, null);
                    videoImage.Source = videoBitmap;
                });
                return 1;
            };

            MediaPlayer.LibVLCVideoCleanupCb videoCleanupCb = (ref IntPtr opaque) =>
            {
                if (videoBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(videoBuffer);
                    videoBuffer = IntPtr.Zero;
                }
            };

            MediaPlayer.LibVLCVideoLockCb videoLockCb = (IntPtr opaque, IntPtr planes) =>
            {
                Marshal.WriteIntPtr(planes, videoBuffer);
                return IntPtr.Zero;
            };

            MediaPlayer.LibVLCVideoUnlockCb videoUnlockCb = (IntPtr opaque, IntPtr picture, IntPtr planes) => { };

            MediaPlayer.LibVLCVideoDisplayCb videoDisplayCb = (IntPtr opaque, IntPtr picture) =>
            {
                if (videoBitmap == null || videoBuffer == IntPtr.Zero) return;
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    if (videoBitmap == null) return;
                    try
                    {
                        videoBitmap.Lock();
                        int totalBytes = videoPitch * videoHeight;
                        byte[] buffer = new byte[totalBytes]; // Optimize: use shared buffer or unsafe copy
                        Marshal.Copy(videoBuffer, buffer, 0, totalBytes);
                        Marshal.Copy(buffer, 0, videoBitmap.BackBuffer, totalBytes);
                        videoBitmap.AddDirtyRect(new Int32Rect(0, 0, videoWidth, videoHeight));
                        videoBitmap.Unlock();
                    }
                    catch { }
                }));
            };

            mediaPlayer.SetVideoFormatCallbacks(videoFormatCb, videoCleanupCb);
            mediaPlayer.SetVideoCallbacks(videoLockCb, videoUnlockCb, videoDisplayCb);

            // UI Overlay
            var uiOverlay = new Grid { IsHitTestVisible = true };
            Panel.SetZIndex(uiOverlay, 100);
            _mainGrid.Children.Add(uiOverlay);

            var clickOverlay = new Grid { Background = Brushes.Transparent, IsHitTestVisible = true };
            Panel.SetZIndex(clickOverlay, 50);
            _mainGrid.Children.Add(clickOverlay);

            ControlsData controls = CreateControlsContent(mediaPlayer);

            var controlsBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 6, 12, 6),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 10, ShadowDepth = 2, Opacity = 0.3 },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 40),
                IsHitTestVisible = true
            };
            controlsBorder.Child = controls.RootElement;
            uiOverlay.Children.Add(controlsBorder);

            SetupInteraction(_mainGrid, controlsBorder, clickOverlay, mediaPlayer, _loadingGrid, controls);

            mediaPlayer.Buffering += (s, e) =>
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_loadingGrid.Visibility == Visibility.Visible)
                        _loadingText.Text = $"正在缓冲... ({e.Cache}%)";
                }));
            };

            Task.Run(() =>
            {
                mediaPlayer.Play();
                if (_playbackHistory.TryGetValue(filePath, out long savedTime))
                {
                    Task.Delay(200).Wait();
                    mediaPlayer.Time = savedTime;
                }
            });

            // Auto save history on unload (handled by OnUnloaded partially, but mediaPlayer might be null then)
            // We can use a property to track current valid media player time
        }

        private void InitializeLibVLC_CoreOnly()
        {
            if (_isInitialized) return;
            lock (_initLock)
            {
                if (_isInitialized) return;
                string appPath = AppDomain.CurrentDomain.BaseDirectory;
                string libvlcPath = Path.Combine(appPath, "Dependencies", "libvlc", "win-x64");
                if (Directory.Exists(libvlcPath))
                {
                    try { Core.Initialize(libvlcPath); } catch { }
                }
            }
        }

        private void EnsureLibVLCInstance()
        {
            if (_isInitialized && _libVLC != null) return;
            lock (_initLock)
            {
                if (_isInitialized && _libVLC != null) return;
                _libVLC = new LibVLC("--no-osd");
                _isInitialized = true;
            }
        }

        private class ControlsData
        {
            public FrameworkElement RootElement;
            public Slider TimeSlider;
            public TextBlock TimeText;
            public TextBlock PlayIcon;
            public TextBlock VolIcon;
            public Button VolBtn;
            public Slider VolSlider;
            public Action StopTimer;
        }

        private Style CreateTransparentButtonStyle()
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Button.TemplateProperty, CreateControlTemplate()));
            return style;
        }

        private ControlTemplate CreateControlTemplate()
        {
            // Simple template
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            var content = new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(content);
            template.VisualTree = border;
            return template;
        }

        private Style CreateSlimSliderStyle()
        {
            // Simplified for brevity, standard slider in WPF usually works okay-ish, or just use default
            return null;
        }

        private ControlsData CreateControlsContent(MediaPlayer player)
        {
            var grid = new Grid { Width = 560, Height = 44 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            Button CreateBtn(string text, Action onClick)
            {
                var b = new Button { Style = CreateTransparentButtonStyle(), Content = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, Cursor = Cursors.Hand };
                b.Click += (s, e) => onClick();
                return b;
            }

            var playBtn = new Button { Style = CreateTransparentButtonStyle(), Width = 34, Height = 34, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var playIcon = new TextBlock { Text = "\uE769", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            playBtn.Content = playIcon;
            playBtn.Click += (s, e) => { Task.Run(() => { try { if (player.IsPlaying) player.Pause(); else player.Play(); } catch { } }); };
            Grid.SetColumn(playBtn, 0); grid.Children.Add(playBtn);

            var btnM15 = CreateBtn("-15", () => Task.Run(() => { try { player.Time = Math.Max(0, player.Time - 15000); } catch { } }));
            Grid.SetColumn(btnM15, 1); grid.Children.Add(btnM15);

            var btnM3 = CreateBtn("-3", () => Task.Run(() => { try { player.Time = Math.Max(0, player.Time - 3000); } catch { } }));
            Grid.SetColumn(btnM3, 2); grid.Children.Add(btnM3);

            var timeText = new TextBlock { Text = "00:00 / 00:00", Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)), FontSize = 12, FontFamily = new FontFamily("Segoe UI"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
            Grid.SetColumn(timeText, 3); grid.Children.Add(timeText);

            var slider = new Slider { VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true, Margin = new Thickness(5, 0, 5, 0) };
            Grid.SetColumn(slider, 4); grid.Children.Add(slider);

            var btnP3 = CreateBtn("+3", () => Task.Run(() => { try { player.Time = Math.Min(player.Length, player.Time + 3000); } catch { } }));
            Grid.SetColumn(btnP3, 5); grid.Children.Add(btnP3);

            var btnP15 = CreateBtn("+15", () => Task.Run(() => { try { player.Time = Math.Min(player.Length, player.Time + 15000); } catch { } }));
            Grid.SetColumn(btnP15, 6); grid.Children.Add(btnP15);

            var volBtn = new Button { Style = CreateTransparentButtonStyle(), Width = 34, Height = 34, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var volIcon = new TextBlock { Text = "\uE767", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            volBtn.Content = volIcon;
            Grid.SetColumn(volBtn, 7); grid.Children.Add(volBtn);

            var volSlider = new Slider { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Width = 70, Minimum = 0, Maximum = 100, Value = player.Volume, IsMoveToPointEnabled = true, Margin = new Thickness(5, 0, 5, 0) };
            Grid.SetColumn(volSlider, 8); grid.Children.Add(volSlider);

            bool isUpdatingVolume = false;

            void UpdateVolumeUI()
            {
                int volume = 0;
                bool isMute = false;
                try
                {
                    volume = player.Volume;
                    isMute = player.Mute;
                }
                catch { return; }

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        isUpdatingVolume = true;
                        volSlider.Value = volume;
                        if (isMute || volume == 0) volIcon.Text = "\uE74F";
                        else if (volume < 33) volIcon.Text = "\uE992";
                        else if (volume < 66) volIcon.Text = "\uE993";
                        else volIcon.Text = "\uE767";
                    }
                    catch { }
                    finally { isUpdatingVolume = false; }
                }));
            }

            void UpdatePlayStateUI()
            {
                bool isPlaying = false;
                try { isPlaying = player.IsPlaying; } catch { return; }

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // If playing, show Pause button (\uE769), else Show Play button (\uE768)
                        playIcon.Text = isPlaying ? "\uE769" : "\uE768";
                    }
                    catch { }
                }));
            }

            player.VolumeChanged += (s, e) => UpdateVolumeUI();
            player.Muted += (s, e) => UpdateVolumeUI();
            player.Unmuted += (s, e) => UpdateVolumeUI();

            player.Playing += (s, e) => UpdatePlayStateUI();
            player.Paused += (s, e) => UpdatePlayStateUI();
            player.Stopped += (s, e) => UpdatePlayStateUI();

            volSlider.ValueChanged += (s, e) =>
            {
                if (isUpdatingVolume) return;
                player.Volume = (int)e.NewValue;
                player.Mute = false;
            };
            volBtn.Click += (s, e) => { player.Mute = !player.Mute; UpdateVolumeUI(); };

            volBtn.PreviewMouseWheel += (s, e) => { volSlider.Value = Math.Clamp(volSlider.Value + (e.Delta > 0 ? 5 : -5), 0, 100); e.Handled = true; };
            volSlider.PreviewMouseWheel += (s, e) => { volSlider.Value = Math.Clamp(volSlider.Value + (e.Delta > 0 ? 5 : -5), 0, 100); e.Handled = true; };

            // Initial UI state
            UpdateVolumeUI();
            UpdatePlayStateUI();

            return new ControlsData { RootElement = grid, TimeSlider = slider, TimeText = timeText, PlayIcon = playIcon, VolIcon = volIcon, VolBtn = volBtn, VolSlider = volSlider };
        }

        private void SetupInteraction(Grid mainGrid, Border controlsBorder, Grid clickOverlay, MediaPlayer player, Grid loadingGrid, ControlsData controls)
        {
            EventHandler<EventArgs> updateState = (s, e) => Application.Current?.Dispatcher?.Invoke(() =>
            {
                loadingGrid.Visibility = Visibility.Collapsed;
                controls.PlayIcon.Text = player.IsPlaying ? "\uE768" : "\uE769";
            });
            player.Playing += updateState;
            player.Paused += updateState;
            player.Stopped += updateState;

            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            hideTimer.Tick += (s, e) =>
            {
                if (!controlsBorder.IsMouseOver && !controls.RootElement.IsMouseOver) controlsBorder.Visibility = Visibility.Collapsed;
                hideTimer.Stop();
            };

            void ShowControls() { controlsBorder.Visibility = Visibility.Visible; hideTimer.Stop(); hideTimer.Start(); }

            clickOverlay.MouseLeftButtonUp += (s, e) =>
            {
                if (controls.RootElement.IsMouseOver) return;
                Task.Run(() => { try { if (player.IsPlaying) player.Pause(); else player.Play(); } catch { } });
            };
            clickOverlay.MouseMove += (s, e) => ShowControls();

            // Mouse idle check
            var mouseCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            mouseCheckTimer.Tick += (s, e) =>
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed) return;
                try
                {
                    if (controlsBorder.Visibility == Visibility.Visible && !controlsBorder.IsMouseOver && !controls.RootElement.IsMouseOver && !clickOverlay.IsMouseOver)
                    {
                        // Let hideTimer handle hiding
                    }
                    else if (clickOverlay.IsMouseOver)
                    {
                        ShowControls();
                    }
                }
                catch { }
            };
            mouseCheckTimer.Start();

            bool isDragging = false;
            long totalTime = 0;
            player.LengthChanged += (s, e) => { totalTime = e.Length; Application.Current?.Dispatcher?.Invoke(() => { if (totalTime > 0) controls.TimeSlider.Maximum = totalTime; }); };

            player.TimeChanged += (s, e) =>
            {
                if (!isDragging && totalTime > 0)
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        var t = TimeSpan.FromMilliseconds(e.Time);
                        controls.TimeText.Text = $"{t:mm\\:ss} / {TimeSpan.FromMilliseconds(totalTime):mm\\:ss}";
                        controls.TimeSlider.Value = e.Time;
                        _playbackHistory[FilePath] = e.Time;
                    });
                }
            };

            controls.TimeSlider.PreviewMouseDown += (s, e) => { isDragging = true; hideTimer.Stop(); };
            controls.TimeSlider.PreviewMouseUp += (s, e) =>
            {
                isDragging = false;
                long newTime = (long)controls.TimeSlider.Value;
                Task.Run(() => player.Time = newTime);
                hideTimer.Start();
            };

            controls.StopTimer = () => { mouseCheckTimer.Stop(); hideTimer.Stop(); };
        }
    }
}
