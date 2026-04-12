using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YiboFile.Models;

namespace YiboFile.Controls.Helpers
{
    /// <summary>
    /// Windows 风格慢双击重命名行为
    /// 
    /// 算法：
    /// 1. MouseDown 在已选中的 ListViewItem 上时记录候选项
    /// 2. MouseUp 在同一 Item 上触发延迟计时器（间隔 = 系统双击时间）
    /// 3. 如果在计时器到期前发生双击（DoubleClick），取消计时器（双击=打开）
    /// 4. 计时器到期且无第二次点击 → 进入重命名模式
    /// </summary>
    public class SlowClickRenameBehavior
    {
        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        private readonly DispatcherTimer _timer;
        private FileSystemItem _candidateItem;
        private ListViewItem _candidateContainer;
        private DateTime _selectionTimestamp;
        private bool _wasAlreadySelected;

        /// <summary>
        /// 最小等待时间：用户首次选中后需要等待此时间，才允许慢单击触发重命名。
        /// 避免"首次选中→立即松手"直接进入重命名的误触。
        /// </summary>
        private static readonly TimeSpan MinSelectionAge = TimeSpan.FromMilliseconds(600);

        public SlowClickRenameBehavior()
        {
            _timer = new DispatcherTimer
            {
                // 使用系统双击间隔 + 50ms 安全边际作为延迟时间
                Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime() + 50)
            };
            _timer.Tick += OnTimerTick;
        }

        /// <summary>
        /// 在 PreviewMouseLeftButtonDown 时调用
        /// </summary>
        public void OnMouseDown(ListView listView, MouseButtonEventArgs e)
        {
            // 快速双击 → 取消慢单击候选
            if (e.ClickCount >= 2)
            {
                Cancel();
                return;
            }

            // 仅处理左键单击
            if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1)
            {
                Cancel();
                return;
            }

            // 如果正在重命名，不要介入
            var hitItem = HitTestForItem(listView, e);
            if (hitItem?.DataContext is FileSystemItem fsItem && fsItem.IsRenaming)
            {
                Cancel();
                return;
            }

            // 检查：该项是否在本次点击之前就已被选中？
            if (hitItem != null && hitItem.IsSelected)
            {
                // 记录候选，但不立即启动计时器
                _candidateItem = hitItem.DataContext as FileSystemItem;
                _candidateContainer = hitItem;
                _wasAlreadySelected = true;
            }
            else
            {
                // 点到了未选中的项 或 空白区 → 重置
                Cancel();
            }
        }

        /// <summary>
        /// 在 MouseLeftButtonUp 时调用
        /// </summary>
        public void OnMouseUp(ListView listView, MouseButtonEventArgs e)
        {
            if (!_wasAlreadySelected || _candidateItem == null)
                return;

            // 确认松手时仍在同一个 Item 上
            var hitItem = HitTestForItem(listView, e);
            if (hitItem != _candidateContainer)
            {
                Cancel();
                return;
            }

            // 如果是多选操作（Ctrl/Shift），不触发
            if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != ModifierKeys.None)
            {
                Cancel();
                return;
            }

            // 仅当只有 1 个选中项时才能触发慢单击重命名
            if (listView.SelectedItems.Count != 1)
            {
                Cancel();
                return;
            }

            // 仅当点击在文件名区域时才触发
            if (!IsHitOnFileName(listView, e))
            {
                Cancel();
                return;
            }

            // 记录选中时间戳（用于在计时器回调中判断一个完整的"慢单击"周期）
            _selectionTimestamp = DateTime.UtcNow;
            _timer.Start();
        }

        private static bool IsHitOnFileName(ListView listView, MouseButtonEventArgs e)
        {
            var hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);
            if (hitResult == null) return false;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listView)
            {
                if (current is TextBlock tb && !string.IsNullOrEmpty(tb.Name) && tb.Name.Contains("FileName"))
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        /// <summary>
        /// 在 DoubleClick / PreviewMouseDoubleClick 时调用以取消延迟
        /// </summary>
        public void OnDoubleClick()
        {
            Cancel();
        }

        /// <summary>
        /// 外部强制取消（如导航、刷新、失焦等）
        /// </summary>
        public void Cancel()
        {
            _timer.Stop();
            _candidateItem = null;
            _candidateContainer = null;
            _wasAlreadySelected = false;
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _timer.Stop();

            var item = _candidateItem;
            _candidateItem = null;
            _candidateContainer = null;
            _wasAlreadySelected = false;

            if (item == null || item.IsRenaming)
                return;

            // 安全检查：确保在计时器运行期间，该项仍然处于选中状态
            // （用户可能在倒计时期间通过键盘切换了选中项）

            // 触发重命名
            item.RenameText = item.Name;
            item.IsRenaming = true;
        }

        /// <summary>
        /// 在指定的鼠标事件位置上命中测试 ListViewItem
        /// </summary>
        private static ListViewItem HitTestForItem(ListView listView, MouseButtonEventArgs e)
        {
            var hitPoint = e.GetPosition(listView);
            var hitResult = VisualTreeHelper.HitTest(listView, hitPoint);
            if (hitResult == null) return null;

            DependencyObject current = hitResult.VisualHit;
            while (current != null && current != listView)
            {
                if (current is ListViewItem lvi)
                    return lvi;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
