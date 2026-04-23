using System;
using System.Windows;
using YiboFile.Dialogs;

namespace YiboFile
{
    /// <summary>
    /// 对话框服务（静态包装类）
    /// </summary>
    [Obsolete("请通过注入 IDialogService 使用对话框。此静态类仅保留用于向后兼容，后续版本中将移除。")]
    public static class DialogService
    {
        private static YiboFile.Services.UI.IDialogService _instance 
            => App.ServiceProvider?.GetService(typeof(YiboFile.Services.UI.IDialogService)) as YiboFile.Services.UI.IDialogService;

        public static void Info(string message, string title = "提示", Window owner = null)
        {
            if (_instance != null) _instance.ShowInfo(message, title);
            else ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Info, owner, showCancel: false);
        }

        public static void Warning(string message, string title = "提示", Window owner = null)
        {
            if (_instance != null) _instance.ShowWarning(message, title);
            else ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Warning, owner, showCancel: false);
        }

        public static void Error(string message, string title = "错误", Window owner = null)
        {
            if (_instance != null) _instance.ShowError(message, title);
            else ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Error, owner, showCancel: false);
        }

        public static bool Ask(string message, string title = "确认", Window owner = null)
        {
            if (_instance != null) return _instance.Confirm(message, title);
            return ConfirmDialog.Show(message, title, ConfirmDialog.DialogType.Question, owner, showCancel: true);
        }

        public static string ShowInput(string prompt, string defaultText = "", string title = "输入", bool selectFileNameOnly = false, Window owner = null)
        {
            if (_instance != null) return _instance.ShowInput(prompt, defaultText, title, selectFileNameOnly);
            
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










