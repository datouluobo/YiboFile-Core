using System;
using System.Windows;
using System.Windows.Controls;

namespace OoiMRR
{
    /// <summary>
    /// MainWindow 驱动器功能
    /// </summary>
    public partial class MainWindow
    {
        #region 驱动器功能

        internal void LoadDrives()
        {
            if (DrivesListBox == null) return;
            _quickAccessService.LoadDrives(DrivesListBox, _fileListService.FormatFileSize);
        }

        private void DrivesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DrivesListBox?.SelectedItem != null)
            {
                // 清除其他导航区域的选择
                ClearOtherNavigationSelections("Drives");
            }
        }

        #endregion
    }
}
