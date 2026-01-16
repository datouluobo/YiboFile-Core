using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YiboFile.Previews
{
    /// <summary>
    /// 图片预览工具栏辅助类
    /// 提供统一的缩放、旋转、全屏工具栏
    /// </summary>
    public static class ImageToolbarHelper
    {
        /// <summary>
        /// 创建图片工具栏配置
        /// </summary>
        public class ToolbarConfig
        {
            public Image TargetImage { get; set; }
            public ScaleTransform ScaleTransform { get; set; }
            public RotateTransform RotateTransform { get; set; }
            public UIElement TitlePanel { get; set; }
            public Grid ParentGrid { get; set; }
            public Action<bool> OnFitToWindowChanged { get; set; }
        }

        /// <summary>
        /// 创建完整的工具栏
        /// </summary>
        public static StackPanel CreateToolbar(ToolbarConfig config)
        {
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                Margin = new Thickness(10, 8, 10, 8),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            double currentScale = 1.0;
            double currentRotation = 0;
            bool isFitToWindow = true;

            // 放大按钮
            var zoomInBtn = CreateToolButton("🔍+", "放大", "放大 (Ctrl+Plus)");
            zoomInBtn.Click += (s, e) =>
            {
                if (isFitToWindow && config.TargetImage != null)
                {
                    config.TargetImage.Stretch = Stretch.None;
                    isFitToWindow = false;
                    config.OnFitToWindowChanged?.Invoke(false);
                }
                currentScale *= 1.2;
                if (config.ScaleTransform != null)
                {
                    config.ScaleTransform.ScaleX = currentScale;
                    config.ScaleTransform.ScaleY = currentScale;
                }
            };
            toolbar.Children.Add(zoomInBtn);

            // 缩小按钮
            var zoomOutBtn = CreateToolButton("🔍-", "缩小", "缩小 (Ctrl+Minus)");
            zoomOutBtn.Click += (s, e) =>
            {
                if (isFitToWindow && config.TargetImage != null)
                {
                    config.TargetImage.Stretch = Stretch.None;
                    isFitToWindow = false;
                    config.OnFitToWindowChanged?.Invoke(false);
                }
                currentScale /= 1.2;
                if (currentScale < 0.1) currentScale = 0.1;
                if (config.ScaleTransform != null)
                {
                    config.ScaleTransform.ScaleX = currentScale;
                    config.ScaleTransform.ScaleY = currentScale;
                }
            };
            toolbar.Children.Add(zoomOutBtn);

            // 原始大小按钮
            var resetBtn = CreateToolButton("1:1", "原始", "原始大小 (Ctrl+0)");
            resetBtn.Click += (s, e) =>
            {
                if (config.TargetImage != null)
                {
                    config.TargetImage.Stretch = Stretch.None;
                }
                currentScale = 1.0;
                if (config.ScaleTransform != null)
                {
                    config.ScaleTransform.ScaleX = 1.0;
                    config.ScaleTransform.ScaleY = 1.0;
                }
                isFitToWindow = false;
                config.OnFitToWindowChanged?.Invoke(false);
            };
            toolbar.Children.Add(resetBtn);

            // 适应窗口按钮
            var fitBtn = CreateToolButton("⊡", "适应", "适应窗口 (Ctrl+F)");
            fitBtn.Click += (s, e) =>
            {
                if (config.TargetImage != null)
                {
                    config.TargetImage.Stretch = Stretch.Uniform;
                }
                currentScale = 1.0;
                if (config.ScaleTransform != null)
                {
                    config.ScaleTransform.ScaleX = 1.0;
                    config.ScaleTransform.ScaleY = 1.0;
                }
                isFitToWindow = true;
                config.OnFitToWindowChanged?.Invoke(true);
            };
            toolbar.Children.Add(fitBtn);

            // 旋转按钮
            var rotateBtn = CreateToolButton("🔄", "旋转", "顺时针旋转90° (R)");
            rotateBtn.Click += (s, e) =>
            {
                currentRotation = (currentRotation + 90) % 360;
                if (config.RotateTransform != null)
                {
                    config.RotateTransform.Angle = currentRotation;
                }
            };
            toolbar.Children.Add(rotateBtn);

            // 全屏按钮
            bool isFullscreen = false;
            var fullscreenBtn = CreateToolButton("⛶", "全屏", "全屏查看 (F11)");
            fullscreenBtn.Click += (s, e) =>
            {
                var window = Window.GetWindow(config.ParentGrid);
                if (window == null) return;

                isFullscreen = !isFullscreen;
                if (isFullscreen)
                {
                    if (config.TitlePanel != null) config.TitlePanel.Visibility = Visibility.Collapsed;
                    toolbar.Visibility = Visibility.Collapsed;
                    window.WindowStyle = WindowStyle.None;
                    window.WindowState = WindowState.Maximized;
                    window.Topmost = true;
                    UpdateButtonText(fullscreenBtn, "退出");
                }
                else
                {
                    if (config.TitlePanel != null) config.TitlePanel.Visibility = Visibility.Visible;
                    toolbar.Visibility = Visibility.Visible;
                    window.WindowStyle = WindowStyle.SingleBorderWindow;
                    window.WindowState = WindowState.Normal;
                    window.Topmost = false;
                    UpdateButtonText(fullscreenBtn, "全屏");
                }
            };
            toolbar.Children.Add(fullscreenBtn);

            // 添加快捷键支持到ParentGrid
            if (config.ParentGrid != null)
            {
                config.ParentGrid.Focusable = true;
                config.ParentGrid.PreviewKeyDown += (s, e) =>
                {
                    if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
                    {
                        if (e.Key == System.Windows.Input.Key.OemPlus || e.Key == System.Windows.Input.Key.Add)
                        {
                            zoomInBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            e.Handled = true;
                        }
                        else if (e.Key == System.Windows.Input.Key.OemMinus || e.Key == System.Windows.Input.Key.Subtract)
                        {
                            zoomOutBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            e.Handled = true;
                        }
                        else if (e.Key == System.Windows.Input.Key.D0 || e.Key == System.Windows.Input.Key.NumPad0)
                        {
                            resetBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            e.Handled = true;
                        }
                        else if (e.Key == System.Windows.Input.Key.F)
                        {
                            fitBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            e.Handled = true;
                        }
                    }
                    else if (e.Key == System.Windows.Input.Key.R)
                    {
                        rotateBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.F11)
                    {
                        fullscreenBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                    else if (e.Key == System.Windows.Input.Key.Escape && isFullscreen)
                    {
                        fullscreenBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        e.Handled = true;
                    }
                };
            }

            return toolbar;
        }

        /// <summary>
        /// 创建工具栏按钮
        /// </summary>
        private static Button CreateToolButton(string icon, string text, string tooltip)
        {
            var btnStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };

            var labelText = new TextBlock
            {
                Text = text,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(50, 50, 50))
            };

            btnStack.Children.Add(iconText);
            btnStack.Children.Add(labelText);

            return new Button
            {
                Content = btnStack,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = tooltip,
                MinWidth = 0
            };
        }

        /// <summary>
        /// 更新按钮文本
        /// </summary>
        private static void UpdateButtonText(Button button, string newText)
        {
            if (button.Content is StackPanel stack && stack.Children.Count > 1)
            {
                if (stack.Children[1] is TextBlock textBlock)
                {
                    textBlock.Text = newText;
                }
            }
        }
    }
}

