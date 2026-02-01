using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace YiboFile.Controls
{
    public class AudioPreviewControl : UserControl
    {
        [DllImport("winmm.dll")]
        private static extern int MciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        private Grid _mainGrid;
        private MediaElement _mediaElement;
        private bool _isMidi;
        private string _midiAlias;
        private long _midiLengthMs;
        private bool _isPlaying;
        private bool _isDraggingProgress;
        private DispatcherTimer _timer;

        // UI Elements
        private TextBlock _currentTimeText;
        private TextBlock _totalTimeText;
        private Slider _progressSlider;
        private Button _playPauseButton;
        private Button _stopButton;
        private TextBlock _volumeText;
        private Slider _volumeSlider;

        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register("FilePath", typeof(string), typeof(AudioPreviewControl), new PropertyMetadata(null, OnFilePathChanged));

        public string FilePath
        {
            get { return (string)GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }

        private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (AudioPreviewControl)d;
            control.LoadAudio((string)e.NewValue);
        }

        public AudioPreviewControl()
        {
            InitializeUI();
            this.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAndCleanup();
        }

        private void StopAndCleanup()
        {
            _timer?.Stop();
            if (_mediaElement != null)
            {
                try { _mediaElement.Stop(); _mediaElement.Close(); } catch { }
            }
            if (!string.IsNullOrEmpty(_midiAlias))
            {
                try { MciSendString($"close {_midiAlias}", null, 0, IntPtr.Zero); } catch { }
                _midiAlias = null;
            }
            _isPlaying = false;
        }

        private void InitializeUI()
        {
            _mainGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };

            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar placeholder (handled by VM/View)
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Info
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Progress
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Controls
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // MediaElement holder

            this.Content = _mainGrid;
        }

        private void LoadAudio(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            StopAndCleanup();
            _mainGrid.Children.Clear();

            // 1. Info Panel
            var infoPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(15, 10, 15, 10) };
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
                Text = $"文件大小: {FormatFileSize(fileInfo.Length)}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Margin = new Thickness(0, 0, 0, 5)
            };
            infoPanel.Children.Add(fileNameText);
            infoPanel.Children.Add(fileSizeText);
            Grid.SetRow(infoPanel, 1);
            _mainGrid.Children.Add(infoPanel);

            // 2. Media Logic
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            _isMidi = ext == ".mid" || ext == ".midi";

            _mediaElement = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Manual,
                Volume = 0.5,
                Visibility = Visibility.Collapsed
            };
            Grid.SetRow(_mediaElement, 4);
            _mainGrid.Children.Add(_mediaElement);

            bool mediaReady = false;

            try
            {
                if (_isMidi)
                {
                    _midiAlias = $"mid_{Guid.NewGuid():N}";
                    var openCode = MciSendString($"open \"{filePath}\" type sequencer alias {_midiAlias}", null, 0, IntPtr.Zero);
                    if (openCode == 0)
                    {
                        MciSendString($"set {_midiAlias} time format milliseconds", null, 0, IntPtr.Zero);
                        var lenBuf = new StringBuilder(32);
                        MciSendString($"status {_midiAlias} length", lenBuf, lenBuf.Capacity, IntPtr.Zero);
                        long.TryParse(lenBuf.ToString(), out _midiLengthMs);
                        mediaReady = true;
                    }
                }
                else
                {
                    _mediaElement.Source = new Uri(filePath);
                    mediaReady = true;
                }
            }
            catch
            {
                mediaReady = false;
            }

            // 3. Progress Panel
            var progressPanel = new Grid { Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)), Margin = new Thickness(15, 5, 15, 5) };
            progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            progressPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _currentTimeText = new TextBlock { Text = "00:00", Foreground = Brushes.Gray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0), MinWidth = 45 };
            Grid.SetColumn(_currentTimeText, 0); progressPanel.Children.Add(_currentTimeText);

            _progressSlider = new Slider { Minimum = 0, Maximum = 100, Value = 0, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) };
            Grid.SetColumn(_progressSlider, 1); progressPanel.Children.Add(_progressSlider);

            _totalTimeText = new TextBlock { Text = "00:00", Foreground = Brushes.Gray, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), MinWidth = 45 };
            Grid.SetColumn(_totalTimeText, 2); progressPanel.Children.Add(_totalTimeText);

            Grid.SetRow(progressPanel, 2);
            _mainGrid.Children.Add(progressPanel);

            // 4. Controls Panel
            var controlPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(15, 10, 15, 15) };

            _playPauseButton = CreateButton("▶️ 播放", new SolidColorBrush(Color.FromRgb(33, 150, 243)));
            _stopButton = CreateButton("⏹️ 停止", new SolidColorBrush(Color.FromRgb(96, 125, 139)));

            _volumeText = new TextBlock { Text = "🔊", FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 5, 0) };
            _volumeSlider = new Slider { Minimum = 0, Maximum = 1, Value = 0.5, Width = 100, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 15, 0) };

            controlPanel.Children.Add(_playPauseButton);
            controlPanel.Children.Add(_stopButton);
            controlPanel.Children.Add(_volumeText);
            controlPanel.Children.Add(_volumeSlider);

            Grid.SetRow(controlPanel, 3);
            _mainGrid.Children.Add(controlPanel);

            if (!mediaReady && !_isMidi)
            {
                // Error state handled by events mostly, but can preempt here
            }

            SetupEvents(mediaReady);
        }

        private Button CreateButton(string text, Brush bg)
        {
            return new Button
            {
                Content = text,
                Margin = new Thickness(5),
                Padding = new Thickness(15, 8, 15, 8),
                Background = bg,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontSize = 13
            };
        }

        private void SetupEvents(bool mediaReady)
        {
            if (_isMidi)
            {
                _volumeSlider.IsEnabled = false;
                _volumeText.Text = "🔈";
                if (_midiLengthMs > 0)
                {
                    _totalTimeText.Text = FormatTime(_midiLengthMs / 1000.0);
                    _progressSlider.Maximum = _midiLengthMs / 1000.0;
                }

                _playPauseButton.Click += (s, e) =>
                {
                    if (_isPlaying)
                    {
                        MciSendString($"pause {_midiAlias}", null, 0, IntPtr.Zero);
                        _playPauseButton.Content = "▶️ 播放";
                        _isPlaying = false;
                    }
                    else
                    {
                        MciSendString($"play {_midiAlias}", null, 0, IntPtr.Zero);
                        _playPauseButton.Content = "⏸️ 暂停";
                        _isPlaying = true;
                    }
                };

                _stopButton.Click += (s, e) =>
                {
                    MciSendString($"stop {_midiAlias}", null, 0, IntPtr.Zero);
                    MciSendString($"seek {_midiAlias} to 0", null, 0, IntPtr.Zero);
                    _playPauseButton.Content = "▶️ 播放";
                    _isPlaying = false;
                    _progressSlider.Value = 0;
                    _currentTimeText.Text = "00:00";
                };

                _progressSlider.PreviewMouseDown += (s, e) => _isDraggingProgress = true;
                _progressSlider.PreviewMouseUp += (s, e) =>
                {
                    _isDraggingProgress = false;
                    var ms = (int)(_progressSlider.Value * 1000);
                    MciSendString($"seek {_midiAlias} to {ms}", null, 0, IntPtr.Zero);
                };
            }
            else
            {
                _mediaElement.MediaOpened += (s, e) =>
                {
                    if (_mediaElement.NaturalDuration.HasTimeSpan)
                    {
                        var d = _mediaElement.NaturalDuration.TimeSpan;
                        _totalTimeText.Text = $"{(int)d.TotalMinutes:D2}:{d.Seconds:D2}";
                        _progressSlider.Maximum = d.TotalSeconds;
                    }
                };

                _mediaElement.MediaFailed += (s, e) =>
                {
                    _playPauseButton.IsEnabled = false;
                    _playPauseButton.Content = "⚠️ 格式不支持";
                };

                _playPauseButton.Click += (s, e) =>
                {
                    if (_isPlaying) { _mediaElement.Pause(); _playPauseButton.Content = "▶️ 播放"; _isPlaying = false; }
                    else { _mediaElement.Play(); _playPauseButton.Content = "⏸️ 暂停"; _isPlaying = true; }
                };

                _stopButton.Click += (s, e) =>
                {
                    _mediaElement.Stop();
                    _playPauseButton.Content = "▶️ 播放";
                    _isPlaying = false;
                    _progressSlider.Value = 0;
                    _currentTimeText.Text = "00:00";
                };

                _volumeSlider.ValueChanged += (s, e) =>
                {
                    _mediaElement.Volume = _volumeSlider.Value;
                    _volumeText.Text = _mediaElement.Volume == 0 ? "🔇" : (_mediaElement.Volume < 0.5 ? "🔉" : "🔊");
                };

                _progressSlider.PreviewMouseDown += (s, e) => _isDraggingProgress = true;
                _progressSlider.PreviewMouseUp += (s, e) =>
                {
                    _isDraggingProgress = false;
                    if (_mediaElement.NaturalDuration.HasTimeSpan)
                        _mediaElement.Position = TimeSpan.FromSeconds(_progressSlider.Value);
                };

                _mediaElement.MediaEnded += (s, e) =>
                {
                    _mediaElement.Position = TimeSpan.Zero;
                    _mediaElement.Play();
                };
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (s, e) =>
            {
                if (!_isDraggingProgress)
                {
                    if (_isMidi)
                    {
                        var buf = new StringBuilder(32);
                        MciSendString($"status {_midiAlias} position", buf, buf.Capacity, IntPtr.Zero);
                        if (long.TryParse(buf.ToString(), out var ms))
                        {
                            _currentTimeText.Text = FormatTime(ms / 1000.0);
                            _progressSlider.Value = ms / 1000.0;
                        }
                    }
                    else if (_mediaElement.NaturalDuration.HasTimeSpan)
                    {
                        var pos = _mediaElement.Position;
                        _currentTimeText.Text = $"{(int)pos.TotalMinutes:D2}:{pos.Seconds:D2}";
                        _progressSlider.Value = pos.TotalSeconds;
                    }
                }
            };
            _timer.Start();
        }

        private string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) { order++; len = len / 1024; }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
