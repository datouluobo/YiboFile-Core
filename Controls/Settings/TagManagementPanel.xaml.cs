using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using YiboFile.ViewModels.Settings;

namespace YiboFile.Controls.Settings
{
    public partial class TagManagementPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        private TagSettingsViewModel _viewModel;

        public TagManagementPanel()
        {
            InitializeComponent();
            this.DataContextChanged += OnDataContextChanged;

            if (this.DataContext == null)
            {
                var vm = new TagSettingsViewModel();
                this.DataContext = vm;
            }
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is TagSettingsViewModel vm)
            {
                if (_viewModel != null)
                {
                    _viewModel.RenameTagGroupRequested -= ViewModel_RenameTagGroupRequested;
                    _viewModel.RenameTagRequested -= ViewModel_RenameTagRequested;
                    _viewModel.UpdateTagColorRequested -= ViewModel_UpdateTagColorRequested;
                }

                _viewModel = vm;
                
                if (_viewModel != null)
                {
                    _viewModel.RenameTagGroupRequested += ViewModel_RenameTagGroupRequested;
                    _viewModel.RenameTagRequested += ViewModel_RenameTagRequested;
                    _viewModel.UpdateTagColorRequested += ViewModel_UpdateTagColorRequested;
                    _viewModel.RefreshTagGroups();
                }
            }
        }

        private void ViewModel_RenameTagRequested(object sender, YiboFile.ViewModels.TagItemManageViewModel e)
        {
            var input = new YiboFile.Controls.Dialogs.InputDialog("重命名标签", "请输入新的标签名称:", e.Name) { Owner = Window.GetWindow(this) };
            if (input.ShowDialog() == true) _viewModel.RenameTag(e, input.InputText);
        }

        private void ViewModel_UpdateTagColorRequested(object sender, YiboFile.ViewModels.TagItemManageViewModel e)
        {
            var dialog = new YiboFile.Controls.Dialogs.ColorSelectionDialog(e.Color) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true) _viewModel.UpdateTagColor(e, dialog.SelectedColor);
        }

        private void ViewModel_RenameTagGroupRequested(object sender, YiboFile.ViewModels.TagGroupManageViewModel e)
        {
            var input = new YiboFile.Controls.Dialogs.InputDialog("重命名分组", "请输入新的分组名称:", e.Name) { Owner = Window.GetWindow(this) };
            if (input.ShowDialog() == true) _viewModel.RenameTagGroup(e, input.InputText);
        }

        public void LoadSettings() => _viewModel?.RefreshTagGroups();
        public void SaveSettings() { }
    }

    public class TagNullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool isNull = value == null;
            return (Invert ? isNull : !isNull) ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}
