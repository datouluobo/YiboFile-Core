using System.Windows;
using YiboFile.Services.UI;

namespace YiboFile.Dialogs
{

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
            // Legacy - kept for compatibility
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
        /// 是否有多个文件（用于显示"全部"选项）
        /// </summary>
        public void SetMultipleMode(bool isMultiple)
        {
            if (AllFilesPanel != null)
            {
                AllFilesPanel.Visibility = isMultiple ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 直接按钮点击 - 一次点击直达
        /// </summary>
        private void DirectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
            {
                ApplyToAll = tag.EndsWith("All");
                string action = ApplyToAll ? tag.Replace("All", "") : tag;

                Resolution = action switch
                {
                    "Overwrite" => ConflictResolution.Overwrite,
                    "Skip" => ConflictResolution.Skip,
                    "Rename" => ConflictResolution.Rename,
                    _ => ConflictResolution.Skip
                };

                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// 显示对话框并获取用户选择
        /// </summary>
        public static (ConflictResolution resolution, bool applyToAll) Show(Window owner, string fileName, bool isMultiple = false)
        {
            var dialog = new ConflictResolutionDialog
            {
                Owner = owner
            };
            dialog.SetFileName(fileName);
            dialog.SetMultipleMode(isMultiple);

            if (dialog.ShowDialog() == true)
            {
                return (dialog.Resolution, dialog.ApplyToAll);
            }

            return (ConflictResolution.CancelAll, false);
        }
    }
}

