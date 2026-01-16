using System;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile.Controls
{
    /// <summary>
    /// Title action bar with mode-aware button groups.
    /// </summary>
    public partial class TitleActionBar : UserControl
    {
        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
            "Mode",
            typeof(string),
            typeof(TitleActionBar),
            new PropertyMetadata("Path", OnModeChanged));

        public string Mode
        {
            get => (string)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public event RoutedEventHandler NewFolderClicked;
        public event RoutedEventHandler NewFileClicked;
        public event RoutedEventHandler CopyClicked;
        public event RoutedEventHandler PasteClicked;
        public event RoutedEventHandler DeleteClicked;
        public event RoutedEventHandler RefreshClicked;
        public event RoutedEventHandler NewTagClicked;
        public event RoutedEventHandler ManageTagsClicked;
        public event RoutedEventHandler BatchAddTagsClicked;
        public event RoutedEventHandler TagStatisticsClicked;

        public TitleActionBar()
        {
            InitializeComponent();
            UpdateModeVisibility();
        }

        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TitleActionBar control)
            {
                control.UpdateModeVisibility();
            }
        }

        private void UpdateModeVisibility()
        {
            var currentMode = (Mode ?? "Path").Trim();
            var showAll = string.Equals(currentMode, "All", StringComparison.OrdinalIgnoreCase);

            SetPanelVisibility(PathPanel, showAll || string.Equals(currentMode, "Path", StringComparison.OrdinalIgnoreCase));
            SetPanelVisibility(LibraryPanel, showAll || string.Equals(currentMode, "Library", StringComparison.OrdinalIgnoreCase));
            SetPanelVisibility(TagPanel, showAll || string.Equals(currentMode, "Tag", StringComparison.OrdinalIgnoreCase));
            SetPanelVisibility(SearchPanel, showAll || string.Equals(currentMode, "Search", StringComparison.OrdinalIgnoreCase));
        }

        private static void SetPanelVisibility(UIElement element, bool isVisible)
        {
            if (element != null)
            {
                element.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OnNewFolderClicked(object sender, RoutedEventArgs e) => NewFolderClicked?.Invoke(this, e);
        private void OnNewFileClicked(object sender, RoutedEventArgs e) => NewFileClicked?.Invoke(this, e);
        private void OnCopyClicked(object sender, RoutedEventArgs e) => CopyClicked?.Invoke(this, e);
        private void OnPasteClicked(object sender, RoutedEventArgs e) => PasteClicked?.Invoke(this, e);
        private void OnDeleteClicked(object sender, RoutedEventArgs e) => DeleteClicked?.Invoke(this, e);
        private void OnRefreshClicked(object sender, RoutedEventArgs e) => RefreshClicked?.Invoke(this, e);
        private void OnNewTagClicked(object sender, RoutedEventArgs e) => NewTagClicked?.Invoke(this, e);
        private void OnManageTagsClicked(object sender, RoutedEventArgs e) => ManageTagsClicked?.Invoke(this, e);
        private void OnBatchAddTagsClicked(object sender, RoutedEventArgs e) => BatchAddTagsClicked?.Invoke(this, e);
        private void OnTagStatisticsClicked(object sender, RoutedEventArgs e) => TagStatisticsClicked?.Invoke(this, e);
    }
}


















