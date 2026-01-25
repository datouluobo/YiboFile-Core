using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YiboFile.Controls
{
    public partial class NavigationRailControl : UserControl
    {
        // Events to notify the main window
        public event EventHandler<string> NavigationModeChanged;
        public event EventHandler LayoutFocusRequested;
        public event EventHandler LayoutWorkRequested;
        public event EventHandler LayoutFullRequested;
        public event EventHandler DualListToggleRequested;
        public event EventHandler SettingsRequested;
        public event EventHandler AboutRequested;

        // Exposed properties for NavigationModeService
        public Button PathButton => NavPathBtn;
        public Button LibraryButton => NavLibraryBtn;
        public Button TagButton => NavTagBtn;
        public Button FocusModeButton => LayoutFocusBtn;
        public Button WorkModeButton => LayoutWorkBtn;
        public Button FullModeButton => LayoutFullBtn;
        public Button DualListButton => DualListToggleBtn;
        public Button SettingsButton => SettingsBtn;
        public Button AboutButton => AboutBtn;

        public NavigationRailControl()
        {
            InitializeComponent();
            // Default active
            SetActiveButton(NavPathBtn);

            // Set visibility for tag feature
            if (NavTagBtn != null)
            {
                NavTagBtn.Visibility = App.IsTagTrainAvailable ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void NavPathBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(NavPathBtn);
            NavigationModeChanged?.Invoke(this, "Path");
        }

        private void NavLibraryBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(NavLibraryBtn);
            NavigationModeChanged?.Invoke(this, "Library");
        }

        private void NavTagBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(NavTagBtn);
            NavigationModeChanged?.Invoke(this, "Tag");
        }

        private void TaskQueueBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(TaskQueueBtn);
            NavigationModeChanged?.Invoke(this, "Tasks");
        }

        private void BackupBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(BackupBtn);
            NavigationModeChanged?.Invoke(this, "Backup");
        }

        private void ClipboardBtn_Click(object sender, RoutedEventArgs e)
        {
            SetActiveButton(ClipboardBtn);
            NavigationModeChanged?.Invoke(this, "Clipboard");
        }

        private void LayoutFocusBtn_Click(object sender, RoutedEventArgs e)
        {
            LayoutFocusRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LayoutWorkBtn_Click(object sender, RoutedEventArgs e)
        {
            LayoutWorkRequested?.Invoke(this, EventArgs.Empty);
        }

        private void LayoutFullBtn_Click(object sender, RoutedEventArgs e)
        {
            LayoutFullRequested?.Invoke(this, EventArgs.Empty);
        }

        private void DualListToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            DualListToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            SettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void AboutBtn_Click(object sender, RoutedEventArgs e)
        {
            AboutRequested?.Invoke(this, EventArgs.Empty);
        }

        private void SetActiveButton(Button activeButton)
        {
            // Reset all top/middle buttons
            NavPathBtn.Tag = null;
            NavLibraryBtn.Tag = null;
            NavTagBtn.Tag = null;
            TaskQueueBtn.Tag = null;
            BackupBtn.Tag = null;
            ClipboardBtn.Tag = null;
            // Layout buttons are handled separately to show active mode
            LayoutFocusBtn.Tag = null;
            LayoutWorkBtn.Tag = null;
            LayoutFullBtn.Tag = null;

            // Set active
            if (activeButton != null)
            {
                activeButton.Tag = "Active";
            }
        }

        // Method to externally set active state (e.g. if loaded from config)
        public void SetActiveMode(string mode)
        {
            switch (mode)
            {
                case "Path": SetActiveButton(NavPathBtn); break;
                case "Library": SetActiveButton(NavLibraryBtn); break;
                case "Tag": SetActiveButton(NavTagBtn); break;
                case "Tasks": SetActiveButton(TaskQueueBtn); break;
                case "Backup": SetActiveButton(BackupBtn); break;
                case "Clipboard": SetActiveButton(ClipboardBtn); break;
            }
        }
    }
}
