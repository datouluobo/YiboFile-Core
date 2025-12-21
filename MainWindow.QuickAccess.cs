using System;
using System.Windows;
using System.Windows.Controls;

namespace OoiMRR
{
    /// <summary>
    /// MainWindow 快速访问功能
    /// </summary>
    public partial class MainWindow
    {
        #region 快速访问

        internal void LoadQuickAccess()
        {
            if (QuickAccessListBox == null) return;
            _quickAccessService.LoadQuickAccess(QuickAccessListBox);
        }

        private void QuickAccessListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 事件处理已由QuickAccessService内部处理，此方法保留以兼容现有代码
        }

        #endregion
    }
}
