using System.Windows;
using YiboFile.Dialogs;

namespace YiboFile
{
    public static class DialogService
    {
        public static void Info(string message, string title = "提示", Window owner = null)
        {
            ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Info, owner, showCancel: false);
        }

        public static void Warning(string message, string title = "提示", Window owner = null)
        {
            ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Warning, owner, showCancel: false);
        }

        public static void Error(string message, string title = "错误", Window owner = null)
        {
            ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Error, owner, showCancel: false);
        }

        public static bool Ask(string message, string title = "确认", Window owner = null)
        {
            return ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Question, owner, showCancel: true);
        }

        public static string ShowInput(string prompt, string defaultText = "", string title = "输入", bool selectFileNameOnly = false, Window owner = null)
        {
            var dialog = new PathInputDialog(prompt)
            {
                InputText = defaultText,
                SelectFileNameOnly = selectFileNameOnly,
                Owner = owner,
                Title = title
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.InputText;
            }
            return null;
        }
    }
}










