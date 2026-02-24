using System;
using System.Windows;
using System.Windows.Controls;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class LibrarySettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        private LibrarySettingsViewModel _viewModel;

        public LibrarySettingsPanel()
        {
            InitializeComponent();
            _viewModel = new LibrarySettingsViewModel();
            this.DataContext = _viewModel;
        }
        public void LoadSettings()
        {
            _viewModel?.LoadFromConfig();
            _viewModel?.RefreshLibraries();
        }

        public void SaveSettings() { }
    }
}
