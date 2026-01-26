using System;
using System.Windows;
using System.Windows.Controls;

namespace YiboFile
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
            // 清除其他导航区域的选择（无论当前选择是否为null）
            ClearOtherNavigationSelections("QuickAccess");
        }

        #endregion
    }
}

