using System.Windows.Controls;
using YiboFile.Controls.Settings;

namespace YiboFile.Controls
{
    /// <summary>
    /// 路径/库/标签管理面板。
    /// 原 NavigationSettingsWindow 的内容，从独立 Window 转为 UserControl，
    /// 以便嵌入标签页系统。
    /// </summary>
    public partial class ManagementPanelControl : UserControl
    {
        private PathSettingsPanel _pathPanel;
        private LibraryManagementPanel _libraryPanel;
        private TagManagementPanel _tagPanel;

        public ManagementPanelControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 选中指定的子标签页。
        /// </summary>
        /// <param name="tabTag">标签标识：Path / Library / Tag</param>
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
