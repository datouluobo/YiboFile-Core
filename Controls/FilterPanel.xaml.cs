using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using YiboFile.Services.Search;

namespace YiboFile.Controls
{
    public partial class FilterPanel : UserControl
    {
        public event EventHandler FilterChanged;
        private SearchOptions _currentOptions;
        private bool _isUpdatingUI;

        public FilterPanel()
        {
            InitializeComponent();
            this.Loaded += (s, e) => UpdateUI();
        }

        public static readonly DependencyProperty OptionsProperty =
            DependencyProperty.Register(nameof(Options), typeof(SearchOptions), typeof(FilterPanel),
                new PropertyMetadata(null, OnOptionsChanged));

        public SearchOptions Options
        {
            get => (SearchOptions)GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        private static void OnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FilterPanel panel && e.NewValue is SearchOptions options)
            {
                panel.Initialize(options);
            }
        }

        public void Initialize(SearchOptions options)
        {
            _currentOptions = options;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_currentOptions == null || ScopeFolderBtn == null) return;
            _isUpdatingUI = true;

            // Scope
            ScopeFolderBtn.IsChecked = _currentOptions.SearchFolders && !_currentOptions.SearchNames && !_currentOptions.SearchNotes;
            ScopeFilenameBtn.IsChecked = _currentOptions.SearchNames && !_currentOptions.SearchFolders && !_currentOptions.SearchNotes;
            ScopeNotesBtn.IsChecked = !_currentOptions.SearchNames && !_currentOptions.SearchFolders && _currentOptions.SearchNotes;
            ScopeAllBtn.IsChecked = _currentOptions.SearchNames && _currentOptions.SearchNotes && _currentOptions.SearchFolders;

            // Date
            DateAllBtn.IsChecked = _currentOptions.DateRange == DateRangeFilter.All;
            DateTodayBtn.IsChecked = _currentOptions.DateRange == DateRangeFilter.Today;
            DateWeekBtn.IsChecked = _currentOptions.DateRange == DateRangeFilter.ThisWeek;
            DateMonthBtn.IsChecked = _currentOptions.DateRange == DateRangeFilter.ThisMonth;
            DateYearBtn.IsChecked = _currentOptions.DateRange == DateRangeFilter.ThisYear;

            // Type
            TypeAllBtn.IsChecked = _currentOptions.Type == FileTypeFilter.All;
            TypeImageBtn.IsChecked = _currentOptions.Type == FileTypeFilter.Images;
            TypeVideoBtn.IsChecked = _currentOptions.Type == FileTypeFilter.Videos;
            TypeAudioBtn.IsChecked = _currentOptions.Type == FileTypeFilter.Audio;
            TypeDocBtn.IsChecked = _currentOptions.Type == FileTypeFilter.Documents;

            // Size
            SizeAllBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.All;
            SizeTinyBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Tiny;
            SizeSmallBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Small;
            SizeMediumBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Medium;
            SizeLargeBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Large;
            SizeHugeBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Huge;

            // Image Size & Duration (Visibility & State)
            bool isImage = _currentOptions.Type == FileTypeFilter.Images;
            bool isVideo = _currentOptions.Type == FileTypeFilter.Videos;
            bool isAudio = _currentOptions.Type == FileTypeFilter.Audio;

            ImageSizeHeader.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;
            ImageSizeContent.Visibility = isImage ? Visibility.Visible : Visibility.Collapsed;

            // Show duration for both Video and Audio
            bool showDuration = isVideo || isAudio;
            DurationHeader.Visibility = showDuration ? Visibility.Visible : Visibility.Collapsed;
            DurationContent.Visibility = showDuration ? Visibility.Visible : Visibility.Collapsed;

            if (isImage)
            {
                ImgSizeAllBtn.IsChecked = _currentOptions.ImageSize == ImageDimensionFilter.All;
                ImgSizeSmallBtn.IsChecked = _currentOptions.ImageSize == ImageDimensionFilter.Small;
                ImgSizeMediumBtn.IsChecked = _currentOptions.ImageSize == ImageDimensionFilter.Medium;
                ImgSizeLargeBtn.IsChecked = _currentOptions.ImageSize == ImageDimensionFilter.Large;
                ImgSizeHugeBtn.IsChecked = _currentOptions.ImageSize == ImageDimensionFilter.Huge;
            }

            if (showDuration)
            {
                DurationAllBtn.IsChecked = _currentOptions.Duration == AudioDurationFilter.All;
                DurationShortBtn.IsChecked = _currentOptions.Duration == AudioDurationFilter.Short;
                DurationMediumBtn.IsChecked = _currentOptions.Duration == AudioDurationFilter.Medium;
                DurationLongBtn.IsChecked = _currentOptions.Duration == AudioDurationFilter.Long;
                DurationVeryLongBtn.IsChecked = _currentOptions.Duration == AudioDurationFilter.VeryLong;
            }

            // Path
            PathCurrentBtn.IsChecked = _currentOptions.PathRange == PathRangeFilter.CurrentDrive;
            PathAllBtn.IsChecked = _currentOptions.PathRange == PathRangeFilter.AllDrives;

            _isUpdatingUI = false;
        }

        private void NotifyChanged()
        {
            if (!_isUpdatingUI)
            {
                FilterChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void Scope_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var btn = sender as ToggleButton;
            if (btn == null || btn.Tag == null) return;

            string tag = btn.Tag.ToString();

            if (tag == "Folder")
            {
                _currentOptions.Mode = SearchMode.Folder;
                _currentOptions.SearchFolders = true;
                _currentOptions.SearchNames = false;
                _currentOptions.SearchNotes = false;

                // Optional: Auto-select Type=Folder for convenience?
                // Given the user specifically asked for "Scope: Folder", they likely want to search folders.
                _currentOptions.Type = FileTypeFilter.Folders;
            }
            else if (tag == "FileName")
            {
                _currentOptions.Mode = SearchMode.FileName;
                _currentOptions.SearchFolders = false;
                _currentOptions.SearchNames = true;
                _currentOptions.SearchNotes = false;

                // Reset Type to All to ensure files are visible (unless user manually restricts type later)
                // If we don't reset, switching from "Folder" scope (Type=Folder) to "File" scope would show nothing.
                if (_currentOptions.Type == FileTypeFilter.Folders)
                {
                    _currentOptions.Type = FileTypeFilter.All;
                }
            }
            else if (tag == "Notes")
            {
                _currentOptions.Mode = SearchMode.Notes;
                _currentOptions.SearchFolders = false;
                _currentOptions.SearchNames = false;
                _currentOptions.SearchNotes = true;

                // Similarly, ensure we don't block notes visibility due to conflicting type
                if (_currentOptions.Type == FileTypeFilter.Folders)
                {
                    _currentOptions.Type = FileTypeFilter.All;
                }
            }
            else if (tag == "AllScope")
            {
                _currentOptions.Mode = SearchMode.All;
                _currentOptions.SearchFolders = true;
                _currentOptions.SearchNames = true;
                _currentOptions.SearchNotes = true;
            }

            UpdateUI(); // Refresh radio state
            NotifyChanged();
        }

        private void Date_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var btn = sender as ToggleButton;
            if (btn == null || btn.Tag == null) return;

            if (Enum.TryParse<DateRangeFilter>(btn.Tag.ToString(), out var result))
            {
                _currentOptions.DateRange = result;
                UpdateUI();
                NotifyChanged();
            }
        }

        private void Type_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var btn = sender as ToggleButton;
            if (btn == null || btn.Tag == null) return;

            if (Enum.TryParse<FileTypeFilter>(btn.Tag.ToString(), out var result))
            {
                _currentOptions.Type = result;
                UpdateUI();
                NotifyChanged();
            }
        }

        private void Size_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var btn = sender as ToggleButton;
            if (btn == null || btn.Tag == null) return;

            if (Enum.TryParse<SizeRangeFilter>(btn.Tag.ToString(), out var result))
            {
                _currentOptions.SizeRange = result;
                UpdateUI();
                NotifyChanged();
            }
        }

        private void Path_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var btn = sender as ToggleButton;
            if (btn == null || btn.Tag == null) return;

            if (Enum.TryParse<PathRangeFilter>(btn.Tag.ToString(), out var result))
            {
                _currentOptions.PathRange = result;
                UpdateUI();
                NotifyChanged();
            }
        }

        private void ImgSize_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var btn = sender as ToggleButton;
            if (btn == null || btn.Tag == null) return;

            if (Enum.TryParse<ImageDimensionFilter>(btn.Tag.ToString(), out var result))
            {
                _currentOptions.ImageSize = result;
                UpdateUI();
                NotifyChanged();
            }
        }

        private void Duration_Click(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI) return;
            var btn = sender as ToggleButton;
            if (btn == null || btn.Tag == null) return;

            if (Enum.TryParse<AudioDurationFilter>(btn.Tag.ToString(), out var result))
            {
                _currentOptions.Duration = result;
                UpdateUI();
                NotifyChanged();
            }
        }
    }
}

