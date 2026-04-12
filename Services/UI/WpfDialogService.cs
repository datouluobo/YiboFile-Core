using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using YiboFile.Dialogs;
using YiboFile.Controls.Dialogs;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace YiboFile.Services.UI
{
    /// <summary>
    /// WPF 对话框服务实现
    /// </summary>
    public class WpfDialogService : IDialogService
    {
        private Window GetActiveWindow()
        {
            return Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) 
                   ?? Application.Current.MainWindow;
        }

        private T InvokeOnUI<T>(Func<T> action)
        {
            if (Application.Current.Dispatcher.CheckAccess())
                return action();
            return Application.Current.Dispatcher.Invoke(action);
        }

        private void InvokeOnUI(Action action)
        {
            if (Application.Current.Dispatcher.CheckAccess())
                action();
            else
                Application.Current.Dispatcher.Invoke(action);
        }

        public void ShowInfo(string message, string title = "提示")
        {
            InvokeOnUI(() => ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Info, GetActiveWindow(), false));
        }

        public void ShowWarning(string message, string title = "警告")
        {
            InvokeOnUI(() => ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Warning, GetActiveWindow(), false));
        }

        public void ShowError(string message, string title = "错误")
        {
            InvokeOnUI(() => ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Error, GetActiveWindow(), false));
        }

        public bool Confirm(string message, string title = "确认", DialogMessageType type = DialogMessageType.Question)
        {
            var dialogType = MapType(type);
            return InvokeOnUI(() => ConfirmDialog.Show(message, title, dialogType, ConfirmDialog.DialogButtons.OkCancel, GetActiveWindow())) == AppDialogResult.OK;
        }

        public AppDialogResult AskYesNoCancel(string message, string title = "确认", DialogMessageType type = DialogMessageType.Question)
        {
            var dialogType = MapType(type);
            return InvokeOnUI(() => ConfirmDialog.Show(message, title, dialogType, ConfirmDialog.DialogButtons.YesNoCancel, GetActiveWindow()));
        }

        public string ShowInput(string prompt, string defaultText = "", string title = "输入", bool selectFileNameOnly = false)
        {
            return InvokeOnUI(() =>
            {
                var dialog = new PathInputDialog(prompt)
                {
                    InputText = defaultText,
                    SelectFileNameOnly = selectFileNameOnly,
                    Owner = GetActiveWindow(),
                    Title = title
                };

                if (dialog.ShowDialog() == true)
                {
                    return dialog.InputText;
                }
                return null;
            });
        }

        private ConfirmDialog.DialogType MapType(DialogMessageType type)
        {
            return type switch
            {
                DialogMessageType.Info => ConfirmDialog.DialogType.Info,
                DialogMessageType.Warning => ConfirmDialog.DialogType.Warning,
                DialogMessageType.Error => ConfirmDialog.DialogType.Error,
                DialogMessageType.Question => ConfirmDialog.DialogType.Question,
                _ => ConfirmDialog.DialogType.Info
            };
        }

        public string ShowFolderBrowser(string title, string initialPath = null)
        {
            return InvokeOnUI(() =>
            {
                var dialog = new Forms.FolderBrowserDialog
                {
                    Description = title,
                    SelectedPath = initialPath ?? string.Empty,
                    UseDescriptionForTitle = true
                };

                if (dialog.ShowDialog() == Forms.DialogResult.OK)
                {
                    return dialog.SelectedPath;
                }
                return null;
            });
        }

        public string ShowOpenFileDialog(string title, string filter)
        {
            return InvokeOnUI(() =>
            {
                var dialog = new OpenFileDialog
                {
                    Title = title,
                    Filter = filter
                };

                if (dialog.ShowDialog(GetActiveWindow()) == true)
                {
                    return dialog.FileName;
                }
                return null;
            });
        }

        public string ShowSaveFileDialog(string title, string filter, string defaultFileName = "")
        {
            return InvokeOnUI(() =>
            {
                var dialog = new SaveFileDialog
                {
                    Title = title,
                    Filter = filter,
                    FileName = defaultFileName
                };

                if (dialog.ShowDialog(GetActiveWindow()) == true)
                {
                    return dialog.FileName;
                }
                return null;
            });
        }
        public async Task<(ConflictResolution resolution, bool applyToAll)> ShowConflictDialogAsync(string fileName, bool isMultiple)
        {
            return await InvokeOnUI(async () =>
            {
                var dialog = new ConflictResolutionDialog { Owner = GetActiveWindow() };
                dialog.SetFileName(fileName);
                dialog.SetMultipleMode(isMultiple);
                
                if (dialog.ShowDialog() == true)
                {
                    return (dialog.Resolution, dialog.ApplyToAll);
                }
                return (ConflictResolution.CancelAll, false);
            });
        }

        public void ShowTagManagementDialog()
        {
            InvokeOnUI(() =>
            {
                var dialog = new TagManagementDialog { Owner = GetActiveWindow() };
                dialog.ShowDialog();
            });
        }

        public int? ShowTagSelectionDialog()
        {
            return InvokeOnUI<int?>(() =>
            {
                var dialog = new TagSelectionDialog { Owner = GetActiveWindow() };
                if (dialog.ShowDialog() == true)
                {
                    return dialog.SelectedTagId;
                }
                return null;
            });
        }
    }
}
