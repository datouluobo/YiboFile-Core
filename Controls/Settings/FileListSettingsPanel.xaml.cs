using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class FileListSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;
        private FileListSettingsViewModel _viewModel;

        public FileListSettingsPanel()
        {
            InitializeComponent();
            _viewModel = new FileListSettingsViewModel();
            this.DataContext = _viewModel;

            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(FileListSettingsViewModel.ColTagsWidth) ||
                    e.PropertyName == nameof(FileListSettingsViewModel.ColNotesWidth))
                {
                    RefreshFileListColumns();
                }
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            };
        }

        private void AdjustValue(double current, double delta, double min, double max, Action<double> setter)
        {
            setter(Math.Clamp(current + delta, min, max));
        }

        private void TagsWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.ColTagsWidth, 5, 50, 500, v => _viewModel.ColTagsWidth = v);
        private void TagsWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.ColTagsWidth, -5, 50, 500, v => _viewModel.ColTagsWidth = v);
        private void NotesWidthUp_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.ColNotesWidth, 5, 100, 800, v => _viewModel.ColNotesWidth = v);
        private void NotesWidthDown_Click(object sender, RoutedEventArgs e) => AdjustValue(_viewModel.ColNotesWidth, -5, 100, 800, v => _viewModel.ColNotesWidth = v);

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void RefreshFileListColumns()
        {
            try
            {
                if (Application.Current.MainWindow is MainWindow mainWindow &&
                    mainWindow.FindName("FileBrowser") is FileBrowserControl fileBrowser &&
                    fileBrowser.GetFileListControl() is var fileListControl && fileListControl != null)
                {
                    fileListControl.LoadColumnWidths();
                }
            }
            catch { }
        }

        public void LoadSettings() => _viewModel?.LoadFromConfig();
        public void SaveSettings() { }
    }
}
