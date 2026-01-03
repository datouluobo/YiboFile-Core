using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using OoiMRR.Controls;

namespace OoiMRR
{
    /// <summary>
    /// 布局模式切换功能
    /// </summary>
    public partial class MainWindow
    {
        #region 布局模式枚举和字段

        /// <summary>
        /// 布局模式
        /// </summary>
        private enum LayoutMode
        {
            Focus,  // 专注模式：折叠左右
            Work,   // 工作模式：左导航+文件列表
            Full    // 完整模式：三栏完整
        }

        private LayoutMode _currentLayoutMode = LayoutMode.Full;

        #endregion

        #region 布局模式切换

        /// <summary>
        /// 切换布局模式（供 KeyboardEventHandler 调用）
        /// </summary>
        internal void SwitchLayoutModeByIndex(int modeIndex)
        {
            if (modeIndex >= 0 && modeIndex <= 2)
            {
                SwitchLayoutMode((LayoutMode)modeIndex);
            }
        }

        /// <summary>
        /// 切换布局模式
        /// </summary>
        private void SwitchLayoutMode(LayoutMode mode)
        {
            if (_currentLayoutMode == mode) return;

            _currentLayoutMode = mode;

            // 更新按钮激活状态
            LayoutFocusBtn.Tag = mode == LayoutMode.Focus ? "Active" : null;
            LayoutWorkBtn.Tag = mode == LayoutMode.Work ? "Active" : null;
            LayoutFullBtn.Tag = mode == LayoutMode.Full ? "Active" : null;

            // 应用布局
            ApplyLayout(mode);

            // TODO: 实现配置持久化
        }

        /// <summary>
        /// 应用布局（调用现有的 CollapsibleGridSplitter 方法）
        /// </summary>
        private void ApplyLayout(LayoutMode mode)
        {
            switch (mode)
            {
                case LayoutMode.Focus:
                    // 专注模式：折叠左+右
                    EnsureCollapsed(SplitterLeft, true);   // 折叠左侧
                    EnsureCollapsed(SplitterRight, false); // 折叠右侧
                    break;

                case LayoutMode.Work:
                    // 工作模式：展开左，折叠右
                    EnsureExpanded(SplitterLeft, true);    // 展开左侧
                    EnsureCollapsed(SplitterRight, false); // 折叠右侧
                    break;

                case LayoutMode.Full:
                    // 完整模式：展开全部
                    EnsureExpanded(SplitterLeft, true);    // 展开左侧
                    EnsureExpanded(SplitterRight, false);  // 展开右侧
                    break;
            }
        }

        /// <summary>
        /// 确保指定面板已折叠（如果未折叠则折叠）
        /// </summary>
        private void EnsureCollapsed(CollapsibleGridSplitter splitter, bool isPrevious)
        {
            bool isCollapsed = isPrevious ? splitter.IsPreviousCollapsed : splitter.IsNextCollapsed;

            if (!isCollapsed)
            {
                // 模拟点击折叠按钮
                SimulateButtonClick(splitter, isPrevious);
            }
        }

        /// <summary>
        /// 确保指定面板已展开（如果已折叠则展开）
        /// </summary>
        private void EnsureExpanded(CollapsibleGridSplitter splitter, bool isPrevious)
        {
            bool isCollapsed = isPrevious ? splitter.IsPreviousCollapsed : splitter.IsNextCollapsed;

            if (isCollapsed)
            {
                // 模拟点击展开按钮
                SimulateButtonClick(splitter, isPrevious);
            }
        }

        /// <summary>
        /// 模拟点击分割器的折叠/展开按钮
        /// </summary>
        private void SimulateButtonClick(CollapsibleGridSplitter splitter, bool isPrevious)
        {
            try
            {
                var buttonName = isPrevious ? "PART_CollapsePreviousButton" : "PART_CollapseNextButton";
                var button = splitter.Template?.FindName(buttonName, splitter) as Button;

                if (button != null && button.Visibility == Visibility.Visible)
                {
                    // 触发按钮的点击事件
                    button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
            }
            catch
            {
                // 忽略错误
            }
        }

        #endregion

        #region 布局按钮事件处理

        private void LayoutFocus_Click(object sender, RoutedEventArgs e)
        {
            SwitchLayoutMode(LayoutMode.Focus);
        }

        private void LayoutWork_Click(object sender, RoutedEventArgs e)
        {
            SwitchLayoutMode(LayoutMode.Work);
        }

        private void LayoutFull_Click(object sender, RoutedEventArgs e)
        {
            SwitchLayoutMode(LayoutMode.Full);
        }

        #endregion

        #region 布局模式恢复

        /// <summary>
        /// 恢复保存的布局模式（暂未实现持久化）
        /// </summary>
        private void RestoreLayoutMode()
        {
            // TODO: 实现配置持久化后再恢复
            // 当前默认使用完整模式
        }

        #endregion
    }
}
