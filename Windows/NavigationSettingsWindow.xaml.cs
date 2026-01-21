using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Controls.Settings;

namespace YiboFile.Windows
{
    public partial class NavigationSettingsWindow : Window
    {
        private PathSettingsPanel _pathPanel;
        private LibraryManagementPanel _libraryPanel;
        private TagManagementPanel _tagPanel;

        public NavigationSettingsWindow(string initialTab = "Path")
        {
            InitializeComponent();
            SelectTab(initialTab);
        }

        public void SelectTab(string tabTag)
        {
            foreach (TabItem item in MainTabControl.Items)
            {
                if (item.Tag?.ToString() == tabTag)
                {
                    MainTabControl.SelectedItem = item;
                    break;
                }
            }
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl && MainTabControl.SelectedItem is TabItem selectedItem)
            {
                string tag = selectedItem.Tag?.ToString();
                switch (tag)
                {
                    case "Path":
                        if (_pathPanel == null) _pathPanel = new PathSettingsPanel();
                        TabContentArea.Content = _pathPanel;
                        _pathPanel.LoadSettings();
                        break;
                    case "Library":
                        if (_libraryPanel == null) _libraryPanel = new LibraryManagementPanel();
                        TabContentArea.Content = _libraryPanel;
                        break;
                    case "Tag":
                        if (_tagPanel == null) _tagPanel = new TagManagementPanel();
                        TabContentArea.Content = _tagPanel;
                        _tagPanel.LoadSettings();
                        break;
                }
            }
        }
    }
}
