using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace YiboFile.Controls
{
    /// <summary>
    /// PreviewAttachmentControl.xaml 的交互逻辑
    /// </summary>
    public partial class PreviewAttachmentControl : UserControl
    {
        // PreviewGrid 和 NotesTextBox 由 XAML 自动生成

        public event RoutedEventHandler WindowMinimize;
        public event RoutedEventHandler WindowMaximize;
        public event RoutedEventHandler WindowClose;
        public event MouseButtonEventHandler TitleBarMouseDown;
        // Preview events removed as they are now handled by ViewModel/Messages

        public PreviewAttachmentControl()
        {
            InitializeComponent();
        }

        public void SetMaximizedVisual(bool isMax)
        {
            // 按钮已移到主窗口，此方法已废弃但保留以避免破坏接口
        }

        private void WindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowMinimize?.Invoke(sender, e);
        }

        private void WindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowMaximize?.Invoke(sender, e);
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            WindowClose?.Invoke(sender, e);
        }

        private void TitleBarArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            TitleBarMouseDown?.Invoke(sender, e);
        }

        public event EventHandler<double> NotesHeightChanged;

        private Canvas _globalIndicator;
        private System.Windows.Shapes.Line _globalLine;

        private void GridSplitter_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                _globalIndicator = window.FindName("GlobalSnapIndicator") as Canvas;
                _globalLine = window.FindName("GlobalSnapLine") as System.Windows.Shapes.Line;
            }
        }

        private void GridSplitter_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window == null) return;

            if (this.Content is Grid rootGrid && rootGrid.RowDefinitions.Count > 3)
            {
                var row3 = rootGrid.RowDefinitions[3];
                
                // 手动计算新高度实现实时拖拽
                double newHeight = row3.ActualHeight - e.VerticalChange;
                if (newHeight < row3.MinHeight) newHeight = row3.MinHeight;
                if (newHeight > rootGrid.ActualHeight - 60) newHeight = rootGrid.ActualHeight - 60; // 留出上方空间

                bool snapped = false;

                // 与 FileBrowserControl 中的底部面板对齐
                var browsers = FindVisualChildren<FileBrowserControl>(window).ToList();
                foreach (var browser in browsers)
                {
                    if (browser.Content is System.Windows.Controls.Border focusBorder && 
                        focusBorder.Child is System.Windows.Controls.Grid browserGrid && 
                        browserGrid.RowDefinitions.Count > 5)
                    {
                        double browserBottomHeight = browserGrid.RowDefinitions[5].ActualHeight;
                        if (Math.Abs(newHeight - browserBottomHeight) < 15) // Snap threshold
                        {
                            newHeight = browserBottomHeight;
                            snapped = true;

                            if (_globalIndicator != null && _globalLine != null)
                            {
                                var otherSplitter = browser.FindName("BottomGridSplitter") as FrameworkElement;
                                if (otherSplitter != null)
                                {
                                    Point p = otherSplitter.TransformToAncestor(window).Transform(new Point(0, otherSplitter.ActualHeight / 2));
                                    Canvas.SetTop(_globalLine, p.Y);
                                    _globalIndicator.Visibility = Visibility.Visible;
                                }
                            }
                            break;
                        }
                    }
                }

                if (!snapped && _globalIndicator != null)
                {
                    _globalIndicator.Visibility = Visibility.Collapsed;
                }

                row3.Height = new GridLength(newHeight);
            }
            e.Handled = true;
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (_globalIndicator != null)
            {
                _globalIndicator.Visibility = Visibility.Collapsed;
            }

            if (this.Content is Grid rootGrid && rootGrid.RowDefinitions.Count > 3)
            {
                var height = rootGrid.RowDefinitions[3].Height.Value;
                NotesHeightChanged?.Invoke(this, height);
            }
        }
        
        private System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                if (child != null && child is T t) yield return t;
                foreach (T childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
            }
        }
    }
}
