using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;
using YiboFile;

namespace YiboFile.Controls
{
    /// <summary>
    /// TabManagerControl.xaml 的交互逻辑
    /// 标签页管理控件的 UI 容器
    /// 业务逻辑已移至 TabService
    /// </summary>
    public partial class TabManagerControl : UserControl
    {
        private Window _parentWindow;
        private Button _newTabButton;

        /// <summary>
        /// 新建标签页请求事件
        /// </summary>
        public event EventHandler NewTabRequested;

        public TabManagerControl()
        {
            InitializeComponent();
            TabScrollViewer.PreviewMouseWheel += TabScrollViewer_PreviewMouseWheel;
            CreateAndAddNewTabButton();
        }

        private void TabScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta != 0)
            {
                TabScrollViewer.ScrollToHorizontalOffset(TabScrollViewer.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }

        /// <summary>
        /// 关闭覆盖层请求事件
        /// </summary>
        public event EventHandler CloseOverlayRequested;

        public void RaiseCloseOverlayRequested()
        {
            CloseOverlayRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 创建并添加新建标签页按钮到TabsPanel末尾
        /// </summary>
        private void CreateAndAddNewTabButton()
        {
            _newTabButton = new Button
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(2, 0, 2, 0),
                ToolTip = "新建标签页",
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            // 确保按钮在该区域可点击 (WindowChrome)
            _newTabButton.SetValue(WindowChrome.IsHitTestVisibleInChromeProperty, true);

            // Create TextBlock for Icon
            var iconBlock = new TextBlock
            {
                FontSize = 16,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            iconBlock.SetResourceReference(TextBlock.TextProperty, "Icon_Add");
            iconBlock.SetResourceReference(TextBlock.FontFamilyProperty, "IconFontFamily");

            _newTabButton.Content = iconBlock;

            // 设置样式
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(BorderBrushProperty, Brushes.Transparent));

            var template = new ControlTemplate(typeof(Button));

            // 边框容器
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6)); // 与标签页一致
            factory.SetValue(Border.PaddingProperty, new Thickness(0));

            // 内容呈现
            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(contentPresenter);

            template.VisualTree = factory;
            style.Setters.Add(new Setter(TemplateProperty, template));

            // Trigger: 悬停效果
            var mouseOverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            mouseOverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new DynamicResourceExtension("ControlHoverBrush")));
            style.Triggers.Add(mouseOverTrigger);

            // Trigger: 按下效果
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new DynamicResourceExtension("ControlPressedBrush")));
            style.Triggers.Add(pressedTrigger);

            _newTabButton.Style = style;
            _newTabButton.Click += NewTabButton_Click;

            // 添加到专用容器，使其在滚动时依然可见
            var host = FindName("NewTabButtonHost") as Border;
            if (host != null)
            {
                host.Child = _newTabButton;
            }
            else
            {
                TabsPanel.Children.Add(_newTabButton);
            }
        }

        /// <summary>
        /// 确保新建标签页按钮始终在最后
        /// TabService在添加/移除标签页后应调用此方法
        /// </summary>
        public void EnsureNewTabButtonLast()
        {
            var host = FindName("NewTabButtonHost") as Border;
            if (host != null)
            {
                // Ensure button is in host
                if (host.Child != _newTabButton)
                {
                    host.Child = _newTabButton;
                }

                // Ensure host is in TabsPanel
                if (!TabsPanel.Children.Contains(host))
                {
                    TabsPanel.Children.Add(host);
                }

                // Ensure host is at the end of TabsPanel
                int lastIndex = TabsPanel.Children.Count - 1;
                if (TabsPanel.Children.IndexOf(host) != lastIndex)
                {
                    TabsPanel.Children.Remove(host);
                    TabsPanel.Children.Add(host);
                }
            }
        }

        /// <summary>
        /// 新建标签页按钮点击事件
        /// </summary>
        private void NewTabButton_Click(object sender, RoutedEventArgs e)
        {
            CloseOverlayRequested?.Invoke(this, EventArgs.Empty);
            NewTabRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 设置父窗口（用于对话框等）
        /// </summary>
        public void SetParentWindow(Window window)
        {
            _parentWindow = window;
        }

        /// <summary>
        /// 标签页面板（XAML引用）
        /// </summary>
        public StackPanel TabsPanelControl => TabsPanel;

        /// <summary>
        /// 标签页边框容器（XAML引用）
        /// </summary>
        public Border TabsBorderControl => TabsBorder;
    }
}

