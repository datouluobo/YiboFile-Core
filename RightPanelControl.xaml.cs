using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace YiboFile
{
    /// <summary>
    /// RightPanelControl.xaml 的交互逻辑
    /// </summary>
    public partial class RightPanelControl : UserControl
    {
        // PreviewGrid 和 NotesTextBox 由 XAML 自动生成

        public event RoutedEventHandler WindowMinimize;
        public event RoutedEventHandler WindowMaximize;
        public event RoutedEventHandler WindowClose;
        public event MouseButtonEventHandler TitleBarMouseDown;
        // Preview events removed as they are now handled by ViewModel/Messages

        public RightPanelControl()
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

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (this.Content is Grid rootGrid && rootGrid.RowDefinitions.Count > 3)
            {
                // Note: Row indices might have changed. 
                // In XAML, Notes is Row 3. So RowDefinitions[3].Height.
                // Previous code accessed RowDefinitions[4], was that correct?
                // XAML RowDefinitions: 0=Title, 1=Preview, 2=Splitter, 3=Notes. Total 4.
                // So RowDefinitions[3] is correct.

                if (rootGrid.RowDefinitions.Count > 3)
                {
                    var height = rootGrid.RowDefinitions[3].Height.Value;
                    NotesHeightChanged?.Invoke(this, height);
                }
            }
        }
    }
}
