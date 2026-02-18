using System;
using System.Windows;
using System.Windows.Input;
using YiboFile.Services;
using YiboFile.ViewModels;
using System.Windows.Forms;

namespace YiboFile
{
    public partial class LibraryManagementWindow : Window, ILibraryManagementDialogService
    {
        public LibraryManagementWindow()
        {
            InitializeComponent();

            var repository = App.ServiceProvider?.GetService(typeof(YiboFile.Services.Data.Repositories.ILibraryRepository)) as YiboFile.Services.Data.Repositories.ILibraryRepository;
            var libraryService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.LibraryService)) as YiboFile.Services.LibraryService;

            DataContext = new LibraryManagementViewModel(libraryService, repository, this);

            this.KeyDown += LibraryManagementWindow_KeyDown;
        }

        private void LibraryManagementWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        #region ILibraryManagementDialogService Implementation

        public void ShowError(string message)
        {
            // Use ViewModel property binding for inline error, or fallback to DialogService
            // But ViewModel calls this for Dialog.
            DialogService.Error(message, owner: this);
        }

        public void ShowInfo(string message)
        {
            DialogService.Info(message, owner: this);
        }

        public void ShowWarning(string message)
        {
            DialogService.Warning(message, owner: this);
        }

        public bool Confirm(string message, string title)
        {
            return System.Windows.MessageBox.Show(
                   message,
                   title,
                   MessageBoxButton.YesNo,
                   MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public string ShowInputDialog(string title, string prompt, string defaultText)
        {
            var dialog = new Controls.Dialogs.InputDialog(title, prompt, defaultText);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                return dialog.InputText;
            }
            return null;
        }

        public string ShowFolderBrowserDialog(string title)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = title;
                dialog.ShowNewFolderButton = false;
                dialog.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
            }
            return null;
        }

        #endregion
    }
}
