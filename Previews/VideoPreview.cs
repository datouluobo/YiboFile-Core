using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using YiboFile.Controls;
using MediaPlayer = LibVLCSharp.Shared.MediaPlayer;
using System.Windows.Markup;
using System.Collections.Generic;
using System.Windows.Data;

namespace YiboFile.Previews
{
    /// <summary>
    /// 视频文件预览 (LibVLCSharp) - 增强版 (Loading + Floating UI)
    /// </summary>
    public class VideoPreview : IPreviewProvider
    {
        private static LibVLC _libVLC;
        private static bool _isInitialized = false;
        private static readonly object _initLock = new object();
        private static readonly Dictionary<string, long> _playbackHistory = new Dictionary<string, long>();



        public UIElement CreatePreview(string filePath)
        {

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
                Width = 200,
                Height = 4,
                BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
                Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215))
            };
            var loadingText = new TextBlock
            {
                Text = "正在加载视频...",
                Foreground = Brushes.LightGray,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
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

            // Action to update status text only (since progress is indeterminate)
            var reportStatus = new Action<string>((msg) =>
            {
                if (cts.IsCancellationRequested) return;
                try
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (cts.IsCancellationRequested) return;
                            loadingText.Text = msg;
                        }
                        catch { }
                    }));
                }
                catch { }
            });

            mainGrid.Unloaded += (s, e) =>
            {

                cts.Cancel();
            };

            Task.Run(() =>
            {
                try
                {
                    if (cts.IsCancellationRequested) return;
                    reportStatus("正在加载核心组件...");

                    InitializeLibVLC_CoreOnly();

                    if (cts.IsCancellationRequested) return;

                    reportStatus("正在初始化播放引擎...");
                    EnsureLibVLCInstance();

                    if (cts.IsCancellationRequested) return;

                    var mediaPlayer = new MediaPlayer(_libVLC);

                    reportStatus("正在解析媒体文件...");
                    var media = new Media(_libVLC, filePath, FromType.FromPath);
                    media.Parse(MediaParseOptions.ParseLocal);
                    mediaPlayer.Media = media;
                    media.Dispose(); // MediaPlayer keeps a reference

                    reportStatus("正在构建播放界面...");

                    // Create controls content on UI thread? No, create data here? 
                    // Controls are UI elements, must be created on UI thread.

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (cts.IsCancellationRequested)
                        {
                            mediaPlayer.Dispose();
                            return;
                        }

                        // === WriteableBitmap-based video rendering (no Airspace issues) ===
                        WriteableBitmap videoBitmap = null;
                        Image videoImage = new Image { Stretch = Stretch.Uniform };
                        mainGrid.Children.Add(videoImage);
                        mainGrid.Focusable = true;  // Allow focus for keyboard input

                        // Video buffer management
                        IntPtr videoBuffer = IntPtr.Zero;
                        int videoWidth = 0, videoHeight = 0;
                        int videoPitch = 0;

                        // Video format callback - called when video starts to set up dimensions
                        // Video format callback - called when video starts to set up dimensions
                        MediaPlayer.LibVLCVideoFormatCb videoFormatCb = (ref IntPtr opaque, IntPtr chroma, ref uint width, ref uint height, ref uint pitches, ref uint lines) =>
                        {
                            // Request RV32 format (BGRA32, compatible with WriteableBitmap)
                            Marshal.Copy(new byte[] { (byte)'R', (byte)'V', (byte)'3', (byte)'2' }, 0, chroma, 4);

                            videoWidth = (int)width;
                            videoHeight = (int)height;
                            videoPitch = (int)width * 4; // 4 bytes per pixel (BGRA)

                            // Set pitch and lines for the first plane (RV32 has only one plane)
                            pitches = (uint)videoPitch;
                            lines = (uint)height;

                            // Allocate video buffer
                            int bufferSize = videoPitch * videoHeight;
                            videoBuffer = Marshal.AllocHGlobal(bufferSize);

                            // Create WriteableBitmap on UI thread
                            Application.Current?.Dispatcher?.Invoke(() =>
                            {
                                try
                                {
                                    videoBitmap = new WriteableBitmap(videoWidth, videoHeight, 96, 96, PixelFormats.Bgra32, null);
                                    videoImage.Source = videoBitmap;
                                }
                                catch { }
                            });

                            return 1; // Return 1 buffer
                        };

                        // Cleanup callback
                        MediaPlayer.LibVLCVideoCleanupCb videoCleanupCb = (ref IntPtr opaque) =>
                        {
                            if (videoBuffer != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(videoBuffer);
                                videoBuffer = IntPtr.Zero;
                            }
                        };

                        // Lock callback - return buffer pointer for LibVLC to write to
                        MediaPlayer.LibVLCVideoLockCb videoLockCb = (IntPtr opaque, IntPtr planes) =>
                        {
                            Marshal.WriteIntPtr(planes, videoBuffer);
                            return IntPtr.Zero;
                        };

                        // Unlock callback
                        MediaPlayer.LibVLCVideoUnlockCb videoUnlockCb = (IntPtr opaque, IntPtr picture, IntPtr planes) =>
                        {
                            // Nothing to do here
                        };

                        // Display callback - copy buffer to WriteableBitmap (safe memory copy)
                        MediaPlayer.LibVLCVideoDisplayCb videoDisplayCb = (IntPtr opaque, IntPtr picture) =>
                        {
                            if (videoBitmap == null || videoBuffer == IntPtr.Zero || videoWidth == 0 || videoHeight == 0) return;

                            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (videoBitmap == null) return;

                                    videoBitmap.Lock();

                                    // Safe memory copy using Marshal.Copy (no unsafe required)
                                    int totalBytes = videoPitch * videoHeight;
                                    byte[] buffer = new byte[totalBytes];
                                    Marshal.Copy(videoBuffer, buffer, 0, totalBytes);
                                    Marshal.Copy(buffer, 0, videoBitmap.BackBuffer, totalBytes);

                                    videoBitmap.AddDirtyRect(new Int32Rect(0, 0, videoWidth, videoHeight));
                                    videoBitmap.Unlock();
                                }
                                catch { }
                            }));
                        };

                        // Set video callbacks on MediaPlayer
                        mediaPlayer.SetVideoFormatCallbacks(videoFormatCb, videoCleanupCb);
                        mediaPlayer.SetVideoCallbacks(videoLockCb, videoUnlockCb, videoDisplayCb);


                        // UI Overlay (Pure WPF) - Fixes Z-index issues by being part of the window visual tree
                        var uiOverlay = new Grid { IsHitTestVisible = true }; // MUST be true for controls to work!
                        Panel.SetZIndex(uiOverlay, 100);
                        mainGrid.Children.Add(uiOverlay);

                        // Click Overlay (Transparent, for double/single click)
                        var clickOverlay = new Grid { Background = Brushes.Transparent, IsHitTestVisible = true };
                        Panel.SetZIndex(clickOverlay, 50);
                        mainGrid.Children.Add(clickOverlay);

                        ControlsData controls = CreateControlsContent(mediaPlayer);

                        // Controls Container (Border)
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



                        // Interactions
                        Window fullscreenWindow = null;
                        object originalParent = null;

                        clickOverlay.MouseLeftButtonDown += (s, e) =>
                        {
                            if (e.ClickCount == 2)
                            {
                                // Double Click -> Toggle Fullscreen (Fake Fullscreen by reparenting)
                                e.Handled = true;
                                if (fullscreenWindow == null)
                                {
                                    // Enter Fullscreen
                                    var parent = mainGrid.Parent as FrameworkElement;
                                    if (parent != null)
                                    {
                                        // Specific handling for typical parents (Grid, Border, ContentPresenter)
                                        if (parent is Panel panel)
                                        {
                                            originalParent = panel;
                                            panel.Children.Remove(mainGrid);
                                        }
                                        else if (parent is ContentPresenter cp)
                                        {
                                            originalParent = cp;
                                            cp.Content = null;
                                        }
                                        else if (parent is Border border)
                                        {
                                            originalParent = border;
                                            border.Child = null;
                                        }
                                        else if (parent is ContentControl cc)
                                        {
                                            originalParent = cc;
                                            cc.Content = null;
                                        }

                                        if (originalParent != null)
                                        {
                                            fullscreenWindow = new Window
                                            {
                                                WindowStyle = WindowStyle.None,
                                                WindowState = WindowState.Maximized,
                                                Topmost = true,
                                                Background = Brushes.Black,
                                                Content = mainGrid
                                            };
                                            fullscreenWindow.Closed += (sender, args) =>
                                            {
                                                // Ensure restoration if closed by other means (Alt+F4)
                                                if (fullscreenWindow != null)
                                                {
                                                    fullscreenWindow.Content = null;
                                                    if (originalParent is Panel p) p.Children.Add(mainGrid);
                                                    else if (originalParent is ContentPresenter cp) cp.Content = mainGrid;
                                                    else if (originalParent is Border b) b.Child = mainGrid;
                                                    else if (originalParent is ContentControl cc) cc.Content = mainGrid;
                                                    fullscreenWindow = null;
                                                }
                                            };
                                            fullscreenWindow.Show();
                                            // Focus main grid to capture keys
                                            mainGrid.Focus();
                                        }
                                    }
                                }
                                else
                                {
                                    // Exit Fullscreen
                                    fullscreenWindow.Close(); // This triggers the Closed event which restores parent
                                    fullscreenWindow = null; // Set to null after closing
                                }
                            }
                        };

                        clickOverlay.MouseLeftButtonUp += (s, e) =>
                        {
                            if (controlsBorder.IsMouseOver) return;
                            e.Handled = true;
                            Task.Run(() => { try { if (mediaPlayer.IsPlaying) mediaPlayer.Pause(); else mediaPlayer.Play(); } catch { } });
                        };

                        clickOverlay.MouseMove += (s, e) => { }; // Keep for potential hover logic

                        // Space Key and Focus Logic
                        mainGrid.Focusable = true;
                        mainGrid.Loaded += (s, e) => mainGrid.Focus();
                        mainGrid.PreviewMouseDown += (s, e) => mainGrid.Focus();
                        mainGrid.PreviewKeyDown += (s, e) =>
                        {
                            if (e.Key == Key.Space)
                            {
                                e.Handled = true;
                                Task.Run(() => { try { if (mediaPlayer.IsPlaying) mediaPlayer.Pause(); else mediaPlayer.Play(); } catch { } });
                            }
                        };

                        // Auto-Resume: Save playback position
                        mainGrid.Unloaded += (s, e) =>
                        {
                            if (mediaPlayer.Length > 0 && mediaPlayer.Time > 0)
                                _playbackHistory[filePath] = mediaPlayer.Time;
                        };

                        // Restore playback position if history exists
                        if (_playbackHistory.TryGetValue(filePath, out long savedTime))
                        {
                            Task.Run(async () => { await Task.Delay(200); try { mediaPlayer.Time = savedTime; } catch { } });
                        }

                        SetupInteraction(mainGrid, controlsBorder, clickOverlay, mediaPlayer, loadingGrid, controls);

                        mediaPlayer.Buffering += (s, e) =>
                        {
                            try
                            {
                                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    try
                                    {
                                        if (loadingGrid.Visibility == Visibility.Visible)
                                        {
                                            loadingText.Text = $"正在缓冲... ({e.Cache}%)";
                                        }
                                    }
                                    catch { }
                                }));
                            }
                            catch { }
                        };


                        try
                        {
                            mediaPlayer.Play();

                            // Restore playback position
                            if (_playbackHistory.TryGetValue(filePath, out long savedTimeRestore))
                            {
                                Task.Run(async () =>
                                {
                                    await Task.Delay(200);
                                    mediaPlayer.Time = savedTimeRestore;
                                });
                            }

                            reportStatus("准备就绪");
                        }
                        catch (Exception)
                        {
                            // Playback Failed
                        }

                        mainGrid.Unloaded += (s, e) =>
                        {
                            Task.Run(() =>
                            {
                                try { mediaPlayer?.Stop(); mediaPlayer?.Dispose(); } catch { }
                            });
                        };
                    });
                }
                catch (Exception ex)
                {
                    // Init Error

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

                string appPath = AppDomain.CurrentDomain.BaseDirectory;
                string libvlcPath = Path.Combine(appPath, "Dependencies", "libvlc", "win-x64");

                if (!Directory.Exists(libvlcPath))
                {
                    throw new DirectoryNotFoundException($"LibVLC not found at {libvlcPath}");
                }

                try
                {
                    Core.Initialize(libvlcPath);
                }
                catch (Exception)
                {
                    // Core.Initialize Failed
                }
            }
        }

        private void EnsureLibVLCInstance()
        {
            if (_isInitialized && _libVLC != null) return;

            lock (_initLock)
            {
                if (_isInitialized && _libVLC != null) return;

                try
                {
                    _libVLC = new LibVLC("--no-osd");
                    _isInitialized = true;
                }
                catch (Exception)
                {
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
            public TextBlock VolIcon;
            public Button VolBtn;
            public Slider VolSlider;
            public Action StopTimer;
        }

        private ControlsData CreateControlsContent(MediaPlayer player)
        {
            // Main Container
            var grid = new Grid
            {
                Width = 560,
                Height = 44
            };

            // Grid Columns: PlayPause | -15 | -3 | Time | Slider | +3 | +15 | VolumeSlider | VolumeBtn
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Play
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // -15
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // -3
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });    // Time
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Slider
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // +3
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // +15
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Volume Btn
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Volume Slider

            Button CreateBtn(string text, Action onClick)
            {
                var b = new Button { Style = CreateTransparentButtonStyle(), Content = new TextBlock { Text = text, Foreground = Brushes.White, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }, Cursor = Cursors.Hand };
                b.Click += (s, e) => onClick();
                return b;
            }

            // 1. Play/Pause
            var playBtn = new Button { Style = CreateTransparentButtonStyle(), Width = 34, Height = 34, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var playIcon = new TextBlock { Text = "\uE769", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            playBtn.Content = playIcon;
            playBtn.Click += (s, e) => { Task.Run(() => { try { if (player.IsPlaying) player.Pause(); else player.Play(); } catch { } }); };
            Grid.SetColumn(playBtn, 0); grid.Children.Add(playBtn);

            // 2. Seek Buttons - run asynchronously to prevent UI freeze
            var btnM15 = CreateBtn("-15", () => Task.Run(() => { try { player.Time = Math.Max(0, player.Time - 15000); } catch { } }));
            Grid.SetColumn(btnM15, 1);
            grid.Children.Add(btnM15);

            var btnM3 = CreateBtn("-3", () => Task.Run(() => { try { player.Time = Math.Max(0, player.Time - 3000); } catch { } }));
            Grid.SetColumn(btnM3, 2);
            grid.Children.Add(btnM3);

            // 3. Time Text
            var timeText = new TextBlock { Text = "00:00 / 00:00", Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)), FontSize = 12, FontFamily = new FontFamily("Segoe UI"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
            Grid.SetColumn(timeText, 3); grid.Children.Add(timeText);

            // 4. Slider
            var slider = new Slider { VerticalAlignment = VerticalAlignment.Center, IsMoveToPointEnabled = true, Margin = new Thickness(5, 0, 5, 0), Style = CreateSlimSliderStyle() };
            Grid.SetColumn(slider, 4); grid.Children.Add(slider);

            // 5. Seek Buttons - run asynchronously to prevent UI freeze
            var btnP3 = CreateBtn("+3", () => Task.Run(() => { try { player.Time = Math.Min(player.Length, player.Time + 3000); } catch { } }));
            Grid.SetColumn(btnP3, 5);
            grid.Children.Add(btnP3);

            var btnP15 = CreateBtn("+15", () => Task.Run(() => { try { player.Time = Math.Min(player.Length, player.Time + 15000); } catch { } }));
            Grid.SetColumn(btnP15, 6);
            grid.Children.Add(btnP15);

            // 6. Volume Button (Mute Toggle)
            var volBtn = new Button { Style = CreateTransparentButtonStyle(), Width = 34, Height = 34, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var volIcon = new TextBlock { Text = "\uE767", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 16, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            volBtn.Content = volIcon;
            volBtn.Click += (s, e) =>
            {
                bool newMuteState = !player.Mute;
                player.Mute = newMuteState;
                volIcon.Text = newMuteState ? "\uE74F" : "\uE767";
                volIcon.Foreground = newMuteState ? Brushes.Gray : Brushes.White;
            };
            Grid.SetColumn(volBtn, 7);
            grid.Children.Add(volBtn);

            // 7. Volume Slider (Horizontal)
            var volSlider = new Slider
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 70,
                Minimum = 0,
                Maximum = 100,
                Value = player.Volume,
                IsMoveToPointEnabled = true,
                Foreground = Brushes.White,
                Style = CreateSlimSliderStyle(), // Reuse the slim style
                Margin = new Thickness(5, 0, 5, 0)
            };
            Grid.SetColumn(volSlider, 8);
            grid.Children.Add(volSlider);

            // Volume Logic
            Action<int> setVolume = (v) =>
            {
                v = Math.Max(0, Math.Min(100, v));
                player.Volume = v;
                player.Mute = false;
                volSlider.Value = v;

                if (v == 0)
                {
                    volIcon.Text = "\uE74F"; // Mute icon
                    volIcon.Foreground = Brushes.Gray;
                }
                else
                {
                    volIcon.Text = "\uE767"; // Volume icon
                    volIcon.Foreground = Brushes.White;
                }
            };

            volSlider.ValueChanged += (s, e) => setVolume((int)e.NewValue);

            // MouseWheel support
            volBtn.PreviewMouseWheel += (s, e) => { setVolume(player.Volume + (e.Delta > 0 ? 5 : -5)); e.Handled = true; };
            volSlider.PreviewMouseWheel += (s, e) => { setVolume(player.Volume + (e.Delta > 0 ? 5 : -5)); e.Handled = true; };

            return new ControlsData
            {
                RootElement = grid,
                TimeSlider = slider,
                TimeText = timeText,
                PlayIcon = playIcon,
                VolIcon = volIcon,
                VolBtn = volBtn,
                VolSlider = volSlider
            };
        }

        private void Log(string message) { /* Debugging Removed */ }

        private void SetupInteraction(Grid mainGrid, Border controlsBorder, Grid clickOverlay, MediaPlayer player, Grid loadingGrid, ControlsData controls)
        {
            // Sync UI State
            bool muteUpdating = false; // Flag to prevent recursive updates
            EventHandler<EventArgs> updateState = (s, e) => Application.Current?.Dispatcher?.Invoke(() =>
            {
                loadingGrid.Visibility = Visibility.Collapsed;
                controls.PlayIcon.Text = player.IsPlaying ? "\uE768" : "\uE769";
            });
            player.Playing += updateState;
            player.Paused += updateState;
            player.Stopped += updateState;
            player.Muted += (s, e) => Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (muteUpdating) return;
                muteUpdating = true;
                try
                {
                    controls.VolIcon.Text = player.Mute ? "\uE74F" : "\uE767";
                    controls.VolIcon.Foreground = player.Mute ? Brushes.Gray : Brushes.White;
                }
                finally { muteUpdating = false; }
            });

            // Reliability: Fix disappearing timer logic
            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            hideTimer.Tick += (s, e) =>
            {
                if (controlsBorder.IsMouseOver || controls.RootElement.IsMouseOver) return; // Don't hide if mouse hovering controls
                controlsBorder.Visibility = Visibility.Collapsed;
                hideTimer.Stop();
            };

            void ShowControls()
            {
                if (controlsBorder.Visibility != Visibility.Visible) controlsBorder.Visibility = Visibility.Visible;
                hideTimer.Stop();
                hideTimer.Start();
            }

            // No more Airspace issues! clickOverlay now works directly since we use WriteableBitmap
            clickOverlay.MouseLeftButtonUp += (s, e) =>
            {
                // Don't toggle if clicking on controls
                if (controls.RootElement.IsMouseOver) return;

                System.Diagnostics.Debug.WriteLine($"[VideoPreview] Click on video! IsPlaying={player.IsPlaying}");
                e.Handled = true;
                Task.Run(() =>
                {
                    try { if (player.IsPlaying) player.Pause(); else player.Play(); }
                    catch { /* Player may be disposed */ }
                });
            };

            clickOverlay.MouseMove += (s, e) => ShowControls();
            clickOverlay.MouseEnter += (s, e) => ShowControls();

            // Check mouse over main window using Mouse.GetPosition (for redundancy)
            var mouseCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            mouseCheckTimer.Tick += (s, e) =>
            {
                if (Mouse.LeftButton == MouseButtonState.Pressed) return;

                try
                {
                    Point p = Mouse.GetPosition(mainGrid);
                    bool inVideoArea = p.X >= 0 && p.Y >= 0 && p.X <= mainGrid.ActualWidth && p.Y <= mainGrid.ActualHeight;
                    if (inVideoArea || controls.RootElement.IsMouseOver)
                    {
                        if (controlsBorder.Visibility != Visibility.Visible) ShowControls();
                        else
                        {
                            // Reset hide timer if mouse is still moving/hovering
                            // Logic in Tick handles IsMouseOver check, so just extending life here is fine
                            if (controls.RootElement.IsMouseOver)
                            {
                                hideTimer.Stop();
                                hideTimer.Start();
                            }
                        }
                    }
                }
                catch { }
            };
            mouseCheckTimer.Start();

            // Z-Order Fix: Close controls popup when app loses focus
            EventHandler deactivateHandler = (s, e) => { controlsBorder.Visibility = Visibility.Collapsed; };
            EventHandler activateHandler = (s, e) => { /* popup will be shown by mouse move */ };
            Application.Current.Deactivated += deactivateHandler;
            Application.Current.Activated += activateHandler;


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
                    });
                }
            };

            // Scroll to Seek on main overlay - async to prevent freeze
            clickOverlay.PreviewMouseWheel += (s, e) =>
            {
                if (totalTime > 0)
                {
                    long currentTime = player.Time;
                    long newTime = currentTime + (e.Delta > 0 ? 5000 : -5000);
                    newTime = Math.Max(0, Math.Min(totalTime, newTime));
                    Task.Run(() => { try { player.Time = newTime; } catch { } });
                    ShowControls();
                }
            };

            controls.TimeSlider.PreviewMouseDown += (s, e) => { isDragging = true; hideTimer.Stop(); };
            controls.TimeSlider.PreviewMouseUp += (s, e) =>
            {
                isDragging = false;
                long newTime = (long)controls.TimeSlider.Value;
                Task.Run(() => { try { player.Time = newTime; } catch { } }); // Async to prevent freeze
                hideTimer.Start();
            };

            controls.StopTimer = () =>
            {
                mouseCheckTimer.Stop();
                hideTimer.Stop();
                if (Application.Current != null)
                {
                    Application.Current.Deactivated -= deactivateHandler;
                    Application.Current.Activated -= activateHandler;
                }
            };

            mainGrid.Unloaded += (s, e) => controls.StopTimer();
        }

        private Style CreateTransparentButtonStyle()
        {
            string xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Padding' Value='0'/>
    <Setter Property='Cursor' Value='Hand'/>
    <Setter Property='Template'>
        <Setter.Value>
            <ControlTemplate TargetType='Button'>
                <Border Background='{TemplateBinding Background}'>
                    <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                </Border>
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
                // Fallback to simple style if parse fails
                var style = new Style(typeof(Button));
                style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
                return style;
            }
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
                                        <Border Height='2' Background='#FFFFFF' CornerRadius='1' VerticalAlignment='Center' IsHitTestVisible='False'/>
                                    </ControlTemplate>
                                </RepeatButton.Template>
                            </RepeatButton>
                        </Track.DecreaseRepeatButton>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Command='Slider.IncreaseLarge'>
                                <RepeatButton.Template>
                                    <ControlTemplate TargetType='RepeatButton'>
                                        <Border Height='2' Background='#66888888' CornerRadius='1' VerticalAlignment='Center' IsHitTestVisible='False'/>
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

        private Style CreateVerticalSlimSliderStyle()
        {
            string xaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' 
       xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
       TargetType='Slider'>
    <Setter Property='Template'>
<Setter.Value>
    <ControlTemplate TargetType='Slider'>
        <Grid Background='Transparent' Width='30' HorizontalAlignment='Center'>
            <Track x:Name='PART_Track'>
                <Track.DecreaseRepeatButton>
                    <RepeatButton Command='Slider.DecreaseLarge'>
                        <RepeatButton.Template>
                            <ControlTemplate TargetType='RepeatButton'>
                                <Border Width='4' Background='#66888888' CornerRadius='2' HorizontalAlignment='Center' IsHitTestVisible='False'/>
                            </ControlTemplate>
                        </RepeatButton.Template>
                    </RepeatButton>
                </Track.DecreaseRepeatButton>
                <Track.IncreaseRepeatButton>
                    <RepeatButton Command='Slider.IncreaseLarge'>
                        <RepeatButton.Template>
                            <ControlTemplate TargetType='RepeatButton'>
                                <Border Width='4' Background='#FFFFFF' CornerRadius='2' HorizontalAlignment='Center' IsHitTestVisible='False'/>
                            </ControlTemplate>
                        </RepeatButton.Template>
                    </RepeatButton>
                </Track.IncreaseRepeatButton>
                <Track.Thumb>
                    <Thumb>
                        <Thumb.Template>
                            <ControlTemplate TargetType='Thumb'>
                                <Grid>
                                    <Ellipse Width='16' Height='16' Fill='White' StrokeThickness='0'>
                                        <Ellipse.Effect>
                                            <DropShadowEffect ShadowDepth='1' BlurRadius='4' Opacity='0.3'/>
                                        </Ellipse.Effect>
                                    </Ellipse>
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

