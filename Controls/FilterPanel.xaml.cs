using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OoiMRR.Services.Search;

namespace OoiMRR.Controls
{
    public partial class FilterPanel : UserControl
    {
        public event EventHandler FilterChanged;
        private SearchOptions _currentOptions;
        private bool _isUpdatingUI;

        public FilterPanel()
        {
            InitializeComponent();
        }

        public void Initialize(SearchOptions options)
        {
            _currentOptions = options;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_currentOptions == null) return;
            _isUpdatingUI = true;

            // Scope
            ScopeFilenameBtn.IsChecked = _currentOptions.SearchNames && !_currentOptions.SearchNotes;
            ScopeNotesBtn.IsChecked = !_currentOptions.SearchNames && _currentOptions.SearchNotes;
            ScopeAllBtn.IsChecked = _currentOptions.SearchNames && _currentOptions.SearchNotes;

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
            TypeDocBtn.IsChecked = _currentOptions.Type == FileTypeFilter.Documents;
            TypeFolderBtn.IsChecked = _currentOptions.Type == FileTypeFilter.Folders;

            // Size
            SizeAllBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.All;
            SizeTinyBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Tiny;
            SizeSmallBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Small;
            SizeMediumBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Medium;
            SizeLargeBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Large;
            SizeHugeBtn.IsChecked = _currentOptions.SizeRange == SizeRangeFilter.Huge;

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

            if (tag == "FileName")
            {
                _currentOptions.Mode = SearchMode.FileName;
                _currentOptions.SearchNames = true;
                _currentOptions.SearchNotes = false;
            }
            else if (tag == "Notes")
            {
                _currentOptions.Mode = SearchMode.Notes;
                _currentOptions.SearchNames = false;
                _currentOptions.SearchNotes = true;
            }
            else if (tag == "AllScope")
            {
                _currentOptions.Mode = SearchMode.FileName; // Default mode or implies both?
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
    }
}
