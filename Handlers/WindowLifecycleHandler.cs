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
            try
            {
                string fullMsg = $"{DateTime.Now:O} [WindowLifecycleHandler] {msg}";
                System.Diagnostics.Debug.WriteLine(fullMsg);
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window_debug.log"),
                    fullMsg + "\n");
            }
            catch { }
        }

        public void HandleClosing(CancelEventArgs e)
        {
            // 窗口关闭前统一保存所有状态（窗口大小/位置、分割线、导航、标签页）
            try
            {
                LogDebug($"HandleClosing Entry: WindowState={_mainWindow.WindowState}");

                // 1. 显式保存最大化状态 (Bypassing potential issues in WindowStateManager)
                bool isMaximized = _mainWindow.WindowState == WindowState.Maximized;
                LogDebug($"Updating Config IsMaximized={isMaximized}");
                YiboFile.Services.Config.ConfigurationService.Instance.Update(c => c.IsMaximized = isMaximized);

                // 2. 保存窗口其他状态 (SaveAllState calls ConfigurationService.Update internally)
                // 使用 force: true 确保在程序关闭时强制保存，即使初始化未完成
                LogDebug("Calling SaveAllState(force: true)");
                _windowStateManager?.SaveAllState(force: true);

                // 3. 强制保存到磁盘 (SaveNow)
                LogDebug("Calling SaveNow()");
                YiboFile.Services.Config.ConfigurationService.Instance.SaveNow();

                // 执行备份清理（程序退出循环）

                YiboFile.Services.FileOperations.Undo.BackupCleanupService.Cleanup();

                // 🔥 BUG FIX: 不要调用SaveCurrentConfig！
                // ConfigService保存的是启动时加载的旧_config，会覆盖ConfigurationService刚保存的新配置！
                // ConfigurationService和WindowStateManager已经负责保存所有设置，不需要重复保存
                // _configService?.SaveCurrentConfig();  // ❌ 注释掉，避免覆盖
            }
            catch (Exception ex)
            {
                LogDebug($"HandleClosing Exception: {ex.Message}");
                // 关闭阶段不再向外抛异常，避免影响程序退出
            }
        }

        public void HandleSizeChanged(SizeChangedEventArgs e)
        {
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
            if (_mainWindow.FileBrowser == null || _mainWindow.IsSplitterDragging) return;
            _columnService?.AdjustListViewColumnWidths(_mainWindow.FileBrowser);
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

                    double newLeft = Math.Max(_mainWindow.ColLeft.MinWidth, currentLeft * scale);
                    double newRight = Math.Max(_mainWindow.ColRight.MinWidth, currentRight * scale);

                    // 如果因为 MinWidth 限制导致仍超出，可能需要再次调整(简单起见这里假设 minTotal 检查已保证有解)
                    // 为防万一，再次检查 total > minTotal，上面的 minTotal 已经包含了 Center.Min

                    _mainWindow.ColLeft.Width = new GridLength(newLeft);
                    _mainWindow.ColRight.Width = new GridLength(newRight);
                }
            }
            else
            {
                // 空间不足，按比例压缩列宽（仅在极端窗口缩小时触发）
                // 确保 sum > 0 且 scale 有效，避免除以零或 NaN 异常
                if (sum > 0)
                {
                    double scale = total / sum;
                    if (!double.IsNaN(scale) && !double.IsInfinity(scale))
                    {
                        double newLeft = left * scale;
                        if (!double.IsNaN(newLeft) && !double.IsInfinity(newLeft))
                        {
                            _mainWindow.ColLeft.Width = new GridLength(Math.Max(_mainWindow.ColLeft.MinWidth, newLeft));
                        }
                    }
                }
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
                _mainWindow.TitleBarMaxRestoreButton.SetResourceReference(Button.ContentProperty, isMax ? "Icon_Window_Restore" : "Icon_Window_Maximize");
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

