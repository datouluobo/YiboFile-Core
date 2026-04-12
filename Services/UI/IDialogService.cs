using System;
using System.Threading.Tasks;

namespace YiboFile.Services.UI
{
    public enum ConflictResolution
    {
        Skip,
        Overwrite,
        Rename,
        CancelAll
    }
    /// <summary>
    /// 对话框服务契约。
    /// 所有弹窗交互均通过此接口进行，ViewModel/Service 层不得直接引用任何 Window 或 MessageBox。
    /// </summary>
    public interface IDialogService
    {
        /// <summary>显示信息提示（仅"确定"按钮）</summary>
        void ShowInfo(string message, string title = "提示");

        /// <summary>显示警告提示（仅"确定"按钮）</summary>
        void ShowWarning(string message, string title = "警告");

        /// <summary>显示错误提示（仅"确定"按钮）</summary>
        void ShowError(string message, string title = "错误");

        /// <summary>显示确认对话框（"确定"/"取消"），返回 true=确定</summary>
        bool Confirm(string message, string title = "确认", DialogMessageType type = DialogMessageType.Question);

        /// <summary>显示三态确认对话框（Yes/No/Cancel）</summary>
        AppDialogResult AskYesNoCancel(string message, string title = "确认", DialogMessageType type = DialogMessageType.Question);

        /// <summary>显示文本输入对话框，返回用户输入或 null（取消）</summary>
        string ShowInput(string prompt, string defaultText = "", string title = "输入", bool selectFileNameOnly = false);

        /// <summary>显示文件夹选择对话框</summary>
        string ShowFolderBrowser(string title, string initialPath = null);

        /// <summary>显示打开文件对话框</summary>
        string ShowOpenFileDialog(string title, string filter);

        /// <summary>显示保存文件对话框</summary>
        string ShowSaveFileDialog(string title, string filter, string defaultFileName = "");

        /// <summary>显示文件冲突处理对话框</summary>
        Task<(ConflictResolution resolution, bool applyToAll)> ShowConflictDialogAsync(string fileName, bool isMultiple);

        /// <summary>显示标签管理对话框</summary>
        void ShowTagManagementDialog();

        /// <summary>显示标签选择对话框，返回选中的标签 ID 或 null</summary>
        int? ShowTagSelectionDialog();
    }
}
