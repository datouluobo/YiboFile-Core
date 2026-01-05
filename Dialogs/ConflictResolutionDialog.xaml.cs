using System.Windows;

namespace OoiMRR.Dialogs
{
    /// <summary>
    /// 文件冲突解决方式
    /// </summary>
    public enum ConflictResolution
    {
        /// <summary>覆盖现有文件</summary>
        Overwrite,
        /// <summary>跳过此文件</summary>
        Skip,
        /// <summary>重命名新文件</summary>
        Rename,
        /// <summary>取消整个操作</summary>
        CancelAll
    }

    /// <summary>
    /// 冲突解决对话框
    /// </summary>
    public partial class ConflictResolutionDialog : Window
    {
        /// <summary>
        /// 用户选择的解决方式
        /// </summary>
        public ConflictResolution Resolution { get; private set; } = ConflictResolution.Skip;

        /// <summary>
        /// 是否应用到所有冲突
        /// </summary>
        public bool ApplyToAll { get; private set; } = false;

        public ConflictResolutionDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置冲突的文件名
        /// </summary>
        public void SetFileName(string fileName)
        {
            FileNameText.Text = fileName;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (OverwriteOption.IsChecked == true)
                Resolution = ConflictResolution.Overwrite;
            else if (SkipOption.IsChecked == true)
                Resolution = ConflictResolution.Skip;
            else if (RenameOption.IsChecked == true)
                Resolution = ConflictResolution.Rename;

            ApplyToAll = ApplyToAllCheckBox.IsChecked == true;
            DialogResult = true;
            Close();
        }

        private void CancelAll_Click(object sender, RoutedEventArgs e)
        {
            Resolution = ConflictResolution.CancelAll;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 显示对话框并获取用户选择
        /// </summary>
        public static (ConflictResolution resolution, bool applyToAll) Show(Window owner, string fileName)
        {
            var dialog = new ConflictResolutionDialog
            {
                Owner = owner
            };
            dialog.SetFileName(fileName);

            if (dialog.ShowDialog() == true)
            {
                return (dialog.Resolution, dialog.ApplyToAll);
            }

            return (ConflictResolution.CancelAll, false);
        }
    }
}
