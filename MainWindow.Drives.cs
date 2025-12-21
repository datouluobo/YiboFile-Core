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
            // 事件处理已由QuickAccessService内部处理，此方法保留以兼容现有代码
        }

        #endregion
    }
}
