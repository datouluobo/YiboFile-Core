using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using YiboFile.Services;
using YiboFile.Services.ColumnManagement;
using YiboFile.Services.Config;
using YiboFile.Interfaces;

namespace YiboFile.Handlers
{
    public class WindowLifecycleHandler
    {
        private readonly IShellWindow _mainWindow;
        private readonly Services.WindowStateManager _windowStateManager;
        private readonly Services.ColumnManagement.ColumnService _columnService;


        // Legacy fields removed: _isPseudoMaximized, _restoreBounds

        public WindowLifecycleHandler(IShellWindow mainWindow, WindowStateManager windowStateManager, ColumnService columnService)
        {
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _windowStateManager = windowStateManager;
            _columnService = columnService;
        }


        private static void LogDebug(string msg)
        {
            // Debug logging disabled for production/cleanliness
            try
            {
                string fullMsg = $"{DateTime.Now:O} [WindowLifecycleHandler] {msg}";
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_debug.log"), fullMsg + "\n");
            }
            catch { }
        }

        public void HandleClosing(CancelEventArgs e)
        {
            // 窗口关闭前统一保存所有状态（窗口大小/位置、分割线、导航、标签页）
            try
            {
                // 1. 显式保存最大化状态
                bool isMaximized = _mainWindow.WindowState == WindowState.Maximized;
                YiboFile.Services.Config.ConfigurationService.Instance.Update(c => c.IsMaximized = isMaximized);

                // 2. 保存窗口其他状态（force: true 确保在程序关闭时强制保存）
                _windowStateManager?.SaveAllState(force: true);

                // 3. 强制保存到磁盘
                YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();

                // 4. 执行备份清理
                YiboFile.Services.FileOperations.Undo.BackupCleanupService.Cleanup();
            }
            catch (Exception ex)
            {
                LogDebug($"HandleClosing Exception: {ex.Message}");
            }
        }

        public void HandleSizeChanged(SizeChangedEventArgs e)
        {
            // 窗口未完全可见时跳过列宽的重新计算，防止中间的小尺寸（如初始化或动画过渡时）覆盖持久化设置
            if (!_mainWindow.IsLoaded || _mainWindow.WindowState == WindowState.Minimized || _mainWindow.RootGrid.ActualWidth < 100) return;

            // 跳过首次渲染的 SizeChanged (PreviousSize = 0)，避免干扰 Initializer 设置的初始持久化布局
            if (e.PreviousSize.Width == 0 || e.PreviousSize.Height == 0) return;

            // 调整列宽适应新窗口大小
            AdjustColumnWidths();

            // 窗口大小变化时不立即保存，避免覆盖分割线拖拽的保存
            // 保存会在下次用户操作时进行
        }

        public void HandleLocationChanged(EventArgs e)
        {
            // 保存窗口位置
            // Fix: 仅保存窗口位置和状态，不要调用 SaveAllState。
            // SaveAllState 会重新计算列宽和面板可见性，在窗口最大化/还原的过渡动画期间，
            // ActualWidth 可能不稳定（例如未触发布局更新），导致 IsRightPanelVisible 错误地被保存为 false。
            if (_windowStateManager != null && _mainWindow.IsLoaded)
            {
                _windowStateManager.SaveWindowState();
            }
        }

        public void HandleListViewSizeChanged(SizeChangedEventArgs e)
        {
            AdjustListViewColumnWidths();
        }

        private void AdjustListViewColumnWidths()
        {
            if (_mainWindow.IsSplitterDragging) return;
            if (_mainWindow.FileBrowser != null) 
                _columnService?.AdjustListViewColumnWidths(_mainWindow.FileBrowser);
            if (_mainWindow.SecondFileBrowser != null) 
                _columnService?.AdjustListViewColumnWidths(_mainWindow.SecondFileBrowser);
        }

        public void AdjustColumnWidths()
        {
            if (_mainWindow.RootGrid == null) return;

            double total = _mainWindow.RootGrid.ActualWidth - _mainWindow.ColRail.ActualWidth - 10; // 减去Rail (60) 和 两个分割器 (5+5)
            double left = _mainWindow.ColLeft.ActualWidth;
            double center = _mainWindow.ColCenter.ActualWidth;
            double right = _mainWindow.ColRight.ActualWidth;
            double sum = left + center + right;

            // 检查是否有足够的空间容纳所有列的最小宽度
            double minTotal = _mainWindow.ColLeft.MinWidth + _mainWindow.ColCenter.MinWidth + _mainWindow.ColRight.MinWidth;

            if (total > minTotal)
            {
                // 空间充足，确保中间列为 Star
                if (!_mainWindow.ColCenter.Width.IsStar)
                {
                    _mainWindow.ColCenter.Width = new GridLength(1, GridUnitType.Star);
                }

                // Fix: 即使总空间大于最小总宽度，也可能小于当前设定的"列宽之和" (例如用户把左右拉得很宽)
                // 这会导致 Grid 内容超出窗口区域，从而导致右上角按钮被裁剪。
                // 必须检查并压缩 Left/Right 以适应新窗口。

                double currentLeft = _mainWindow.ColLeft.Width.IsAbsolute ? _mainWindow.ColLeft.Width.Value : _mainWindow.ColLeft.ActualWidth;
                double currentRight = _mainWindow.ColRight.Width.IsAbsolute ? _mainWindow.ColRight.Width.Value : _mainWindow.ColRight.ActualWidth;

                // 给中间列保留最小宽度
                double maxAvailableForSides = total - _mainWindow.ColCenter.MinWidth;
                double currentSidesSum = currentLeft + currentRight;

                if (currentSidesSum > maxAvailableForSides && currentSidesSum > 0)
                {
                    // 需要压缩左右列
                    double scale = maxAvailableForSides / currentSidesSum;

                    double newLeft = currentLeft * scale;
                    double newRight = currentRight * scale;
                    double minLeft = _mainWindow.ColLeft.MinWidth;
                    double minRight = _mainWindow.ColRight.MinWidth;

                    // 截断再平衡：若一方触底，把剩余需缩减的空间全压给另一方
                    if (newLeft < minLeft)
                    {
                        newLeft = minLeft;
                        newRight = maxAvailableForSides - newLeft;
                    }
                    else if (newRight < minRight)
                    {
                        newRight = minRight;
                        newLeft = maxAvailableForSides - newRight;
                    }

                    // 最终安全网：避免任何一方跌破配置底线（虽然此时可能轻微挤占中间列一丝，但受窗口级别 MinWidth 保护极少发生）
                    newLeft = Math.Max(minLeft, newLeft);
                    newRight = Math.Max(minRight, newRight);

                    _mainWindow.ColLeft.Width = new GridLength(newLeft);
                    _mainWindow.ColRight.Width = new GridLength(newRight);
                }
            }
            else
            {
                // 空间严重不足时（比如非正常大小跳水），全部赋予最小宽度兜底，由 WPF 自身排版缓冲承受。
                _mainWindow.ColLeft.Width = new GridLength(_mainWindow.ColLeft.MinWidth);
                _mainWindow.ColRight.Width = new GridLength(_mainWindow.ColRight.MinWidth);
            }
        }

        public void EnsureColumnMinWidths()
        {
            // 强制检查并应用所有列的最小宽度约束
            if (_mainWindow.RootGrid == null) return;

            // 获取当前实际宽度
            double leftActual = _mainWindow.ColLeft.ActualWidth;
            double centerActual = _mainWindow.ColCenter.ActualWidth;
            double rightActual = _mainWindow.ColRight.ActualWidth;

            double minLeft = _mainWindow.ColLeft.MinWidth;
            double minCenter = _mainWindow.ColCenter.MinWidth;
            double minRight = _mainWindow.ColRight.MinWidth;

            bool needAdjust = false;

            // 检查列2（中间列）是否小于最小宽度
            // 检查列2（中间列）是否小于最小宽度
            // Fix: 不要在代码中强制设置 Width，因为这会覆盖 Star Sizing。
            // MinWidth 在 XAML 中已定义，Grid 会自动处理。
            // if (centerActual < minCenter)
            // {
            //    _mainWindow.ColCenter.Width = new GridLength(minCenter);
            //    needAdjust = true;
            // }

            // 检查列3（右侧面板）是否小于最小宽度
            if (rightActual < minRight)
            {
                // 计算可用空间
                double totalWidth = _mainWindow.RootGrid.ActualWidth - _mainWindow.ColRail.ActualWidth - 10; // 减去Rail和分割器
                double availableWidth = totalWidth - minLeft - (centerActual >= minCenter ? centerActual : minCenter);

                // 确保右侧面板至少达到最小宽度
                if (availableWidth >= minRight)
                {
                    _mainWindow.ColRight.Width = new GridLength(minRight);
                    needAdjust = true;
                }
                else
                {
                    // 空间不足，需要重新分配
                    AdjustColumnWidths();
                    return;
                }
            }

            // 检查列1（左侧列）
            if (leftActual < minLeft)
            {
                _mainWindow.ColLeft.Width = new GridLength(minLeft);
                needAdjust = true;
            }

            // 如果需要调整，触发布局更新
            if (needAdjust)
            {
                _mainWindow.UpdateLayout();
            }
        }

        public void HandleMinimize()
        {
            _mainWindow.WindowState = WindowState.Minimized;
        }

        public void HandleMaximize()
        {
            if (_mainWindow.WindowState == WindowState.Maximized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }
            else
            {
                _mainWindow.WindowState = WindowState.Maximized;
            }
            UpdateWindowStateUI();
        }

        public void HandleClose()
        {
            _mainWindow.Close();
        }

        // HandleTitleBarMouseDown removed as it is handled by WindowChrome

        public void HandleControlButtonsMouseDown(MouseButtonEventArgs e, object sender)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            if (e.ClickCount == 2)
            {
                HandleMaximize();
                e.Handled = true;
                return;
            }

            bool isMaximized = _mainWindow.WindowState == WindowState.Maximized;

            var element = sender as UIElement;
            if (element == null) return;

            // 命中测试：如果点击的是按钮，则让按钮自己处理
            var hit = VisualTreeHelper.HitTest(element, e.GetPosition(element));
            if (hit != null)
            {
                var current = hit.VisualHit;
                while (current != null && current != element)
                {
                    if (current is Button)
                    {
                        // 点击在按钮上，不做拖动处理
                        return;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }

            // 非按钮区域：仅在非最大化时允许拖动窗口
            if (!isMaximized)
            {
                try
                {
                    _mainWindow.DragMove();
                }
                catch
                {
                }
            }
        }

        public void UpdateWindowStateUI()
        {
            bool isMax = _mainWindow.WindowState == WindowState.Maximized;

            // 更新主窗口右上角按钮图标
            if (_mainWindow.TitleBarMaxRestoreButton != null)
            {
                // Segoe MDL2 Assets: Maximize E922, Restore E923
                // Refactored to use DynamicResource for multi-icon support
                _mainWindow.TitleBarMaxRestoreImage?.SetResourceReference(System.Windows.Controls.Image.SourceProperty, isMax ? "Icon_Window_Restore" : "Icon_Window_Maximize");
                _mainWindow.TitleBarMaxRestoreButton.ToolTip = isMax ? "还原" : "最大化";
            }
        }





        internal static class NativeMethods
        {
            public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
            public const int SWP_NOSIZE = 0x0001;
            public const int SWP_NOMOVE = 0x0002;
            public const int SWP_NOZORDER = 0x0004;
            public const int SWP_FRAMECHANGED = 0x0020;

            [DllImport("user32.dll")]
            public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

            [DllImport("user32.dll")]
            public static extern int GetSystemMetrics(int nIndex);

            [DllImport("user32.dll", SetLastError = true)]
            public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

            [DllImport("dwmapi.dll")]
            public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS pMarInset);

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
            public struct MONITORINFO
            {
                public int cbSize;
                public RECT rcMonitor;
                public RECT rcWork;
                public int dwFlags;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MARGINS
            {
                public int cxLeftWidth;
                public int cxRightWidth;
                public int cyTopHeight;
                public int cyBottomHeight;
            }
        }
    }
}

