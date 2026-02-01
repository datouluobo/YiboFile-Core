using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using YiboFile.ViewModels;
using YiboFile.Previews;

namespace YiboFile.ViewModels.Previews
{
    public class MediaPreviewViewModel : BasePreviewViewModel
    {
        private bool _isVideo;
        public bool IsVideo
        {
            get => _isVideo;
            set { if (SetProperty(ref _isVideo, value)) OnPropertyChanged(nameof(IsAudio)); }
        }

        public bool IsAudio => !IsVideo;

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetProperty(ref _isPlaying, value);
        }

        private TimeSpan _currentTime;
        public TimeSpan CurrentTime
        {
            get => _currentTime;
            set => SetProperty(ref _currentTime, value);
        }

        private TimeSpan _totalTime;
        public TimeSpan TotalTime
        {
            get => _totalTime;
            set => SetProperty(ref _totalTime, value);
        }

        private double _volume = 0.5;
        public double Volume
        {
            get => _volume;
            set => SetProperty(ref _volume, value);
        }

        private bool _isMuted;
        public bool IsMuted
        {
            get => _isMuted;
            set => SetProperty(ref _isMuted, value);
        }

        private string _timeDisplay = "00:00 / 00:00";
        public string TimeDisplay
        {
            get => _timeDisplay;
            set => SetProperty(ref _timeDisplay, value);
        }

        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand SeekCommand { get; }
        public ICommand VolumeCommand { get; }
        public ICommand MuteCommand { get; }

        public MediaPreviewViewModel()
        {
            PlayPauseCommand = new RelayCommand(TogglePlay);
            StopCommand = new RelayCommand(Stop);
            MuteCommand = new RelayCommand(ToggleMute);
            OpenExternalCommand = new RelayCommand(() => PreviewHelper.OpenInDefaultApp(FilePath));
        }

        public async Task LoadAsync(string filePath)
        {
            FilePath = filePath;
            Title = Path.GetFileName(filePath);
            Icon = "🎵"; // Default, might be changed to 🎬 for video
            IsLoading = true;

            // Media loading is often handled by the control, 
            // but we can provide the initial state here.
            await Task.Yield();
            IsLoading = false;
        }

        private void TogglePlay()
        {
            IsPlaying = !IsPlaying;
        }

        private void Stop()
        {
            IsPlaying = false;
            CurrentTime = TimeSpan.Zero;
        }

        private void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        public void UpdatePosition(TimeSpan current, TimeSpan total)
        {
            CurrentTime = current;
            TotalTime = total;
            TimeDisplay = $"{FormatTime(current)} / {FormatTime(total)}";
        }

        private string FormatTime(TimeSpan ts)
        {
            if (ts.TotalHours >= 1)
                return ts.ToString(@"hh\:mm\:ss");
            return ts.ToString(@"mm\:ss");
        }
    }
}
