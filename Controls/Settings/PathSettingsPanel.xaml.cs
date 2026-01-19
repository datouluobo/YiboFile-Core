using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YiboFile.Services.Config;

namespace YiboFile.Controls.Settings
{
    public partial class PathSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        public class SectionItem
        {
            public string Key { get; set; }
            public string DisplayName { get; set; }
        }

        private ObservableCollection<SectionItem> _sections = new ObservableCollection<SectionItem>();
        private bool _isInitialized = false;

        public PathSettingsPanel()
        {
            InitializeComponent();
            SectionsListBox.ItemsSource = _sections;
            LoadSettings();
            _isInitialized = true;
        }

        public void LoadSettings()
        {
            var order = ConfigurationService.Instance.Get(c => c.NavigationSectionsOrder);
            if (order == null || order.Count == 0)
            {
                order = new List<string> { "QuickAccess", "Drives", "FolderFavorites", "FileFavorites" };
            }

            var safeKeys = new HashSet<string> { "QuickAccess", "Drives", "FolderFavorites", "FileFavorites" };

            _sections.Clear();
            foreach (var key in order)
            {
                if (safeKeys.Contains(key))
                {
                    _sections.Add(new SectionItem { Key = key, DisplayName = GetDisplayName(key) });
                }
            }

            // Ensure all known sections are present (in case config is old)
            var allKeys = new[] { "QuickAccess", "Drives", "FolderFavorites", "FileFavorites" };
            foreach (var key in allKeys)
            {
                if (!_sections.Any(s => s.Key == key))
                {
                    _sections.Add(new SectionItem { Key = key, DisplayName = GetDisplayName(key) });
                }
            }
        }

        private string GetDisplayName(string key)
        {
            switch (key)
            {
                case "QuickAccess": return "快速访问";
                case "Drives": return "此电脑 (驱动器)";
                case "FolderFavorites": return "收藏夹 (文件夹)";
                case "FileFavorites": return "收藏夹 (文件)";
                default: return key;
            }
        }

        public void SaveSettings()
        {
            if (!_isInitialized) return;

            var newOrder = _sections.Select(s => s.Key).ToList();
            ConfigurationService.Instance.Set(c => c.NavigationSectionsOrder, newOrder);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            int index = SectionsListBox.SelectedIndex;
            if (index > 0)
            {
                var item = _sections[index];
                _sections.RemoveAt(index);
                _sections.Insert(index - 1, item);
                SectionsListBox.SelectedIndex = index - 1;
                SaveSettings(); // Auto save
            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            int index = SectionsListBox.SelectedIndex;
            if (index >= 0 && index < _sections.Count - 1)
            {
                var item = _sections[index];
                _sections.RemoveAt(index);
                _sections.Insert(index + 1, item);
                SectionsListBox.SelectedIndex = index + 1;
                SaveSettings(); // Auto save
            }
        }
    }
}
