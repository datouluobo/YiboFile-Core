using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace YiboFile.Services
{
    public static class UiLayoutMcp
    {
        private static MainWindow GetMainWindow()
        {
            return Application.Current?.MainWindow as MainWindow;
        }

        public static bool SetPanelWidths(double? left = null, double? center = null, double? right = null)
        {
            var mw = GetMainWindow();
            if (mw == null) return false;
            mw.Dispatcher.Invoke(() =>
            {
                if (left.HasValue)
                {
                    if (left.Value <= 0)
                    {
                        mw.ColLeft.MinWidth = 0;
                        mw.ColLeft.Width = new GridLength(0);
                    }
                    else
                    {
                        mw.ColLeft.MinWidth = Math.Min(mw.ColLeft.MinWidth, left.Value);
                        mw.ColLeft.Width = new GridLength(left.Value);
                    }
                }
                if (center.HasValue)
                {
                    var v = Math.Max(mw.ColCenter.MinWidth, center.Value);
                    mw.ColCenter.Width = new GridLength(v);
                }
                if (right.HasValue)
                {
                    if (right.Value <= 0)
                    {
                        mw.ColRight.MinWidth = 0;
                        mw.ColRight.Width = new GridLength(0);
                        mw.RightPanel.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        mw.RightPanel.Visibility = Visibility.Visible;
                        mw.ColRight.MinWidth = Math.Min(mw.ColRight.MinWidth, right.Value);
                        mw.ColRight.Width = new GridLength(right.Value);
                    }
                }
            }, DispatcherPriority.Normal);
            return true;
        }

        public static bool ToggleRightPanel(bool visible)
        {
            var mw = GetMainWindow();
            if (mw == null) return false;
            mw.Dispatcher.Invoke(() =>
            {
                mw.RightPanel.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                if (visible)
                {
                    mw.ColRight.MinWidth = Math.Max(mw.ColRight.MinWidth, 360);
                    mw.ColRight.Width = new GridLength(Math.Max(360, mw.ColRight.MinWidth));
                }
                else
                {
                    mw.ColRight.MinWidth = 0;
                    mw.ColRight.Width = new GridLength(0);
                }
            }, DispatcherPriority.Normal);
            return true;
        }

        public static bool ToggleNavPanel(bool visible)
        {
            var mw = GetMainWindow();
            if (mw == null) return false;
            mw.Dispatcher.Invoke(() =>
            {
                var v = visible ? Visibility.Visible : Visibility.Collapsed;
                if (mw.NavigationPanelControl != null)
                {
                    mw.NavigationPanelControl.Visibility = v;
                }
                if (!visible)
                {
                    mw.ColLeft.MinWidth = 0;
                    mw.ColLeft.Width = new GridLength(0);
                }
                else
                {
                    if (mw.ColLeft.Width.Value <= 0)
                    {
                        mw.ColLeft.MinWidth = Math.Max(mw.ColLeft.MinWidth, 220);
                        mw.ColLeft.Width = new GridLength(Math.Max(300, mw.ColLeft.MinWidth));
                    }
                }
            }, DispatcherPriority.Normal);
            return true;
        }

        public static bool SetTabsVisible(bool visible)
        {
            var mw = GetMainWindow();
            if (mw == null || mw.FileBrowser == null) return false;
            mw.Dispatcher.Invoke(() => { mw.FileBrowser.TabsVisible = visible; }, DispatcherPriority.Normal);
            return true;
        }

        // 视图切换功能已移除，将在后期重做
        public static bool SetViewMode(string mode)
        {
            return false;
        }

        // 缩略图大小设置功能已移除，将在后期重做
        public static bool SetThumbnailSize(double size)
        {
            return false;
        }

        public static bool SetWindowState(string state)
        {
            var mw = GetMainWindow();
            if (mw == null) return false;
            mw.Dispatcher.Invoke(() =>
            {
                var s = (state ?? "").Trim().ToLowerInvariant();
                if (s == "max" || s == "maximized" || s == "最大化") mw.WindowState = WindowState.Maximized;
                else if (s == "min" || s == "minimized" || s == "最小化") mw.WindowState = WindowState.Minimized;
                else mw.WindowState = WindowState.Normal;
            }, DispatcherPriority.Normal);
            return true;
        }
    }
}
