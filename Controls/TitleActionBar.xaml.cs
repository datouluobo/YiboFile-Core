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

    }
}


















