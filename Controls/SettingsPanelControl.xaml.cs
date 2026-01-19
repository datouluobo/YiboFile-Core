using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile.Controls
{
    public partial class SettingsPanelControl : UserControl
    {
        public event EventHandler CloseRequested;
        public event EventHandler SettingsChanged;

        private UserControl _currentSettingsPanel;
        private string _currentCategory = "General";
        private bool _isInitialized = false;

        public SettingsPanelControl()
        {
            InitializeComponent();

            // 延迟加载，确保所有控件都已初始化
            this.Loaded += SettingsPanelControl_Loaded;
        }

        private void SettingsPanelControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 移除事件，避免重复加载
            this.Loaded -= SettingsPanelControl_Loaded;

            // 标记为已初始化
            _isInitialized = true;

            // 现在可以安全地加载分类
            if (ContentPanel != null && CategoryListBox != null)
            {
                // 设置默认选中项
                if (CategoryListBox.Items.Count > 0)
                {
                    CategoryListBox.SelectedIndex = 0;
                }
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CategoryListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 避免在初始化过程中触发
            if (!_isInitialized || ContentPanel == null) return;

            if (CategoryListBox.SelectedItem is ListBoxItem selectedItem)
            {
                var category = selectedItem.Tag?.ToString() ?? "General";
                LoadCategory(category);
            }
        }

        private void LoadCategory(string category)
        {
            if (ContentPanel == null) return;

            _currentCategory = category;

            ContentPanel.Children.Clear();
            if (_currentSettingsPanel != null)
            {
                // 取消订阅旧面板的事件
                if (_currentSettingsPanel is ISettingsPanel oldPanel)
                {
                    oldPanel.SettingsChanged -= OnSettingsPanelChanged;
                }
                _currentSettingsPanel = null;
            }

            UserControl panel = category switch
            {
                "General" => new Settings.GeneralSettingsPanel(),
                "Appearance" => new Settings.AppearanceSettingsPanel(),
                "Search" => new Settings.SearchSettingsPanel(),
                "FileList" => new Settings.FileListSettingsPanel(),
                "Path" => new Settings.PathSettingsPanel(),
                "Library" => new Settings.LibrarySettingsPanel(),
                "Tag" => new Settings.GeneralSettingsPanel(), // Placeholder for Phase 2
                // "TagTrain" => new Settings.TagTrainSettingsPanel(), // Phase 2
                "TagTrain" => new Settings.GeneralSettingsPanel(), // Placeholder
                "Hotkeys" => new Settings.HotkeySettingsPanel(),
                _ => new Settings.GeneralSettingsPanel()
            };

            _currentSettingsPanel = panel;

            // 订阅新面板的设置改变事件
            if (panel is ISettingsPanel settingsPanel)
            {
                settingsPanel.SettingsChanged += OnSettingsPanelChanged;
                // 移除LoadSettings调用 - Panel在构造函数中已经调用了LoadSettings
                // 每次切换tab时重新Load会覆盖用户未保存的修改！
            }

            ContentPanel.Children.Add(panel);
        }

        private void OnSettingsPanelChanged(object sender, EventArgs e)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.ToLower().Trim();
            if (string.IsNullOrEmpty(searchText))
            {
                foreach (ListBoxItem item in CategoryListBox.Items)
                {
                    item.Visibility = Visibility.Visible;
                }
                return;
            }

            foreach (ListBoxItem item in CategoryListBox.Items)
            {
                var content = item.Content?.ToString()?.ToLower() ?? "";
                item.Visibility = content.Contains(searchText) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void SaveAllSettings()
        {
            if (_currentSettingsPanel is ISettingsPanel settingsPanel)
            {
                settingsPanel.SaveSettings();
            }
        }

        public void LoadAllSettings()
        {
            if (_currentSettingsPanel is ISettingsPanel settingsPanel)
            {
                settingsPanel.LoadSettings();
            }
        }

        public void SelectCategory(string category)
        {
            if (CategoryListBox == null) return;

            foreach (ListBoxItem item in CategoryListBox.Items)
            {
                if (item.Tag?.ToString() == category)
                {
                    CategoryListBox.SelectedItem = item;
                    // LoadCategory will be called by SelectionChanged handler
                    break;
                }
            }
        }
    }

    public interface ISettingsPanel
    {
        void SaveSettings();
        void LoadSettings();
        event EventHandler SettingsChanged;
    }
}


