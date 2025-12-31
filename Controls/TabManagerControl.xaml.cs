using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OoiMRR;

namespace OoiMRR.Controls
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
        /// 文件拖放事件
        /// </summary>
        public event Action<string[], string, bool> FileDropped;

        /// <summary>
        /// 新建标签页请求事件
        /// </summary>
        public event EventHandler NewTabRequested;

        public TabManagerControl()
        {
            InitializeComponent();
            CreateAndAddNewTabButton();
        }

        /// <summary>
        /// 创建并添加新建标签页按钮到TabsPanel末尾
        /// </summary>
        private void CreateAndAddNewTabButton()
        {
            // 使用 Path 图标
            var path = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5F6368")),
                Stretch = Stretch.Uniform,
                Width = 12,
                Height = 12,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                IsHitTestVisible = false // 确保鼠标事件穿透到Button
            };

            _newTabButton = new Button
            {
                Content = path,
                Width = 32,
                Height = 32,
                Margin = new Thickness(2, 0, 2, 0),
                ToolTip = "新建标签页",
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

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
            mouseOverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DADCE0"))));
            style.Triggers.Add(mouseOverTrigger);

            // Trigger: 按下效果
            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D2D4D7"))));
            style.Triggers.Add(pressedTrigger);

            _newTabButton.Style = style;
            _newTabButton.Click += NewTabButton_Click;

            // 添加到TabsPanel末尾
            TabsPanel.Children.Add(_newTabButton);
        }

        /// <summary>
        /// 确保新建标签页按钮始终在最后
        /// TabService在添加/移除标签页后应调用此方法
        /// </summary>
        public void EnsureNewTabButtonLast()
        {
            if (_newTabButton != null)
            {
                // 如果已经在Children中,先移除
                if (TabsPanel.Children.Contains(_newTabButton))
                {
                    TabsPanel.Children.Remove(_newTabButton);
                }
                // 总是添加到最后
                TabsPanel.Children.Add(_newTabButton);
            }
        }

        /// <summary>
        /// 新建标签页按钮点击事件
        /// </summary>
        private void NewTabButton_Click(object sender, RoutedEventArgs e)
        {
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
