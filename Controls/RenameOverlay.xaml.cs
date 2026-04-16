using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using YiboFile.Controls.Helpers;

namespace YiboFile.Controls
{
    public class RenameConfirmedEventArgs : EventArgs
    {
        // 留空，通过 DataContext 取得操作目标
    }

    public partial class RenameOverlay : UserControl
    {
        #region 核心属性

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register("Text", typeof(string), typeof(RenameOverlay),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty IsOpenProperty =
            DependencyProperty.Register("IsOpen", typeof(bool), typeof(RenameOverlay),
                new PropertyMetadata(false, OnIsOpenChanged));

        public bool IsOpen
        {
            get => (bool)GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public static readonly DependencyProperty PlacementTargetProperty =
            DependencyProperty.Register("PlacementTarget", typeof(UIElement), typeof(RenameOverlay));

        public UIElement PlacementTarget
        {
            get => (UIElement)GetValue(PlacementTargetProperty);
            set => SetValue(PlacementTargetProperty, value);
        }

        public static readonly DependencyProperty IsDirectoryProperty =
            DependencyProperty.Register("IsDirectory", typeof(bool), typeof(RenameOverlay), new PropertyMetadata(false));

        public bool IsDirectory
        {
            get => (bool)GetValue(IsDirectoryProperty);
            set => SetValue(IsDirectoryProperty, value);
        }

        #endregion

        #region 尺寸参数

        public static readonly DependencyProperty MinTextWidthProperty =
            DependencyProperty.Register("MinTextWidth", typeof(double), typeof(RenameOverlay), new PropertyMetadata(200.0));

        public double MinTextWidth
        {
            get => (double)GetValue(MinTextWidthProperty);
            set => SetValue(MinTextWidthProperty, value);
        }

        public static readonly DependencyProperty MaxTextWidthProperty =
            DependencyProperty.Register("MaxTextWidth", typeof(double), typeof(RenameOverlay), new PropertyMetadata(500.0));

        public double MaxTextWidth
        {
            get => (double)GetValue(MaxTextWidthProperty);
            set => SetValue(MaxTextWidthProperty, value);
        }

        #endregion

        #region 字体/排版对齐属性（与原 TextBlock 对齐）

        public static readonly DependencyProperty TextFontSizeProperty =
            DependencyProperty.Register("TextFontSize", typeof(double), typeof(RenameOverlay), new PropertyMetadata(12.0));

        /// <summary>
        /// TextBox 字号，应与原 TextBlock 的 FontSize 保持一致
        /// </summary>
        public double TextFontSize
        {
            get => (double)GetValue(TextFontSizeProperty);
            set => SetValue(TextFontSizeProperty, value);
        }

        public static readonly DependencyProperty TextFontWeightProperty =
            DependencyProperty.Register("TextFontWeight", typeof(FontWeight), typeof(RenameOverlay),
                new PropertyMetadata(FontWeights.Normal));

        /// <summary>
        /// TextBox 字重，应与原 TextBlock 的 FontWeight 保持一致
        /// </summary>
        public FontWeight TextFontWeight
        {
            get => (FontWeight)GetValue(TextFontWeightProperty);
            set => SetValue(TextFontWeightProperty, value);
        }

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(RenameOverlay),
                new PropertyMetadata(TextAlignment.Left));

        /// <summary>
        /// TextBox 文本对齐方式，应与原 TextBlock 的 TextAlignment 一致
        /// </summary>
        public TextAlignment TextAlignment
        {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public static readonly DependencyProperty TextPaddingProperty =
            DependencyProperty.Register("TextPadding", typeof(Thickness), typeof(RenameOverlay),
                new PropertyMetadata(new Thickness(1, 1, 1, 1)));

        /// <summary>
        /// TextBox 内边距，用于精确补偿边框和渲染偏移，使文本起始位置与原 TextBlock 对齐。
        /// 默认值 1,1 是 WPF TextBox 模板的最小合理内边距。
        /// </summary>
        public Thickness TextPadding
        {
            get => (Thickness)GetValue(TextPaddingProperty);
            set => SetValue(TextPaddingProperty, value);
        }

        #endregion

        #region Popup 偏移（精确定位）

        public static readonly DependencyProperty PopupHorizontalOffsetProperty =
            DependencyProperty.Register("PopupHorizontalOffset", typeof(double), typeof(RenameOverlay),
                new PropertyMetadata(-1.0));

        /// <summary>
        /// Popup 水平偏移量。值应为 -(Border.BorderThickness.Left) 以补偿边框。
        /// </summary>
        public double PopupHorizontalOffset
        {
            get => (double)GetValue(PopupHorizontalOffsetProperty);
            set => SetValue(PopupHorizontalOffsetProperty, value);
        }

        public static readonly DependencyProperty PopupVerticalOffsetProperty =
            DependencyProperty.Register("PopupVerticalOffset", typeof(double), typeof(RenameOverlay),
                new PropertyMetadata(-1.0));

        /// <summary>
        /// Popup 垂直偏移量。值应为 -(Border.BorderThickness.Top) 以补偿边框。
        /// </summary>
        public double PopupVerticalOffset
        {
            get => (double)GetValue(PopupVerticalOffsetProperty);
            set => SetValue(PopupVerticalOffsetProperty, value);
        }

        #endregion

        #region 行为配置

        public static readonly DependencyProperty LostFocusBehaviorProperty =
            DependencyProperty.Register("LostFocusBehavior", typeof(string), typeof(RenameOverlay), new PropertyMetadata("Commit"));

        public string LostFocusBehavior
        {
            get => (string)GetValue(LostFocusBehaviorProperty);
            set => SetValue(LostFocusBehaviorProperty, value);
        }

        #endregion

        #region 事件

        public event EventHandler<RenameConfirmedEventArgs> RenameConfirmed;
        public event EventHandler RenameCancelled;

        #endregion

        [ThreadStatic]
        private static bool _isProcessing;

        /// <summary>原始文件名，用于判断是否变化</summary>
        private string _originalName;

        public RenameOverlay()
        {
            InitializeComponent();

            RenameTextBox.KeyDown += OnTextBoxKeyDown;
            RenameTextBox.LostFocus += OnTextBoxLostFocus;
            RenameTextBox.TextChanged += OnTextBoxTextChanged;
            ConfirmButton.Click += (s, e) => DoCommit();
            CancelButton.Click += (s, e) => DoCancel();
        }

        #region IsOpen 变更 → 焦点/选中

        private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RenameOverlay overlay && (bool)e.NewValue)
            {
                overlay._originalName = overlay.Text;

                overlay.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!overlay.IsOpen) return;

                    overlay.RenameTextBox.Focus();
                    Keyboard.Focus(overlay.RenameTextBox);
                    FocusManager.SetFocusedElement(
                        FocusManager.GetFocusScope(overlay.RenameTextBox), overlay.RenameTextBox);

                    if (!string.IsNullOrEmpty(overlay.RenameTextBox.Text))
                    {
                        var text = overlay.RenameTextBox.Text;
                        if (overlay.IsDirectory)
                        {
                            overlay.RenameTextBox.SelectAll();
                        }
                        else
                        {
                            int lastDotIndex = text.LastIndexOf('.');
                            if (lastDotIndex > 0)
                                overlay.RenameTextBox.Select(0, lastDotIndex);
                            else
                                overlay.RenameTextBox.SelectAll();
                        }
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        #endregion

        #region 键盘/焦点/文本变更 事件

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (ConfirmButton.IsEnabled)
                    DoCommit();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DoCancel();
            }
        }

        /// <summary>拦截 TextBox 上的鼠标点击事件，确保 TextBox 获得焦点</summary>
        private void OnTextBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 确保点击时 TextBox 获得焦点（在某些 WPF 场景下 Popup 内部点击可能不会自动焦点）
            System.Diagnostics.Debug.WriteLine($"[RenameOverlay] TextBox.PreviewMouseDown - Source:{e.Source}, OriginalSource:{e.OriginalSource}");
            
            // 直接设置焦点，而不是使用 Dispatcher.BeginInvoke，避免焦点丢失
            if (RenameTextBox.IsVisible)
            {
                System.Diagnostics.Debug.WriteLine("[RenameOverlay] Setting focus to TextBox");
                RenameTextBox.Focus();
                Keyboard.Focus(RenameTextBox);
                System.Diagnostics.Debug.WriteLine($"[RenameOverlay] TextBox IsFocused: {RenameTextBox.IsFocused}, IsKeyboardFocused: {RenameTextBox.IsKeyboardFocused}");
            }
            
            // 不设置 e.Handled，让文本框能够处理自己的鼠标事件
            // 我们已经在 FileListMouseHandler 中添加了检查，防止事件冒泡导致退出编辑模式
        }

        /// <summary>拦截 Popup 边框上的鼠标点击，防止事件穿透到下层控件</summary>
        private void OnPopupBorderPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[RenameOverlay] PopupBorder.PreviewMouseDown - Source:{e.Source}, OriginalSource:{e.OriginalSource}");
            
            // 检查点击是否在 Popup 内部的交互元素上
            var source = e.OriginalSource as DependencyObject;
            if (source != null)
            {
                System.Diagnostics.Debug.WriteLine($"[RenameOverlay] OriginalSource type: {source.GetType().Name}");
                // 如果点击在 TextBox、Button 或它们的子元素上，不设置 e.Handled，让文本框能够处理自己的鼠标事件
                if (source == RenameTextBox || 
                    source == ConfirmButton || 
                    source == CancelButton ||
                    IsDescendantOf(RenameTextBox, source) ||
                    IsDescendantOf(ConfirmButton, source) ||
                    IsDescendantOf(CancelButton, source))
                {
                    System.Diagnostics.Debug.WriteLine("[RenameOverlay] Click on TextBox/Button detected, not setting e.Handled to allow TextBox to handle events");
                    // 不设置 e.Handled，让文本框能够处理自己的鼠标事件
                    return;
                }
            }
            
            // 其他情况（如点击在 Border 背景上），阻止事件穿透
            System.Diagnostics.Debug.WriteLine("[RenameOverlay] Click on border background, setting e.Handled = true");
            e.Handled = true;
            System.Diagnostics.Debug.WriteLine("[RenameOverlay] 拦截了穿透点击事件");
        }

        /// <summary>拦截 Popup 边框上的鼠标释放</summary>
        private void OnPopupBorderPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[RenameOverlay] PopupBorder.PreviewMouseUp - Source:{e.Source}, OriginalSource:{e.OriginalSource}");
            
            // 检查点击是否在 Popup 内部的交互元素上
            var source = e.OriginalSource as DependencyObject;
            if (source != null)
            {
                System.Diagnostics.Debug.WriteLine($"[RenameOverlay] MouseUp OriginalSource type: {source.GetType().Name}");
                // 如果点击在 TextBox、Button 或它们的子元素上，不设置 e.Handled，让文本框能够处理自己的鼠标事件
                if (source == RenameTextBox || 
                    source == ConfirmButton || 
                    source == CancelButton ||
                    IsDescendantOf(RenameTextBox, source) ||
                    IsDescendantOf(ConfirmButton, source) ||
                    IsDescendantOf(CancelButton, source))
                {
                    System.Diagnostics.Debug.WriteLine("[RenameOverlay] MouseUp on TextBox/Button detected, not setting e.Handled to allow TextBox to handle events");
                    // 不设置 e.Handled，让文本框能够处理自己的鼠标事件
                    return;
                }
            }
            
            // 其他情况（如点击在 Border 背景上），阻止事件穿透
            System.Diagnostics.Debug.WriteLine("[RenameOverlay] MouseUp on border background, setting e.Handled = true");
            e.Handled = true;
            System.Diagnostics.Debug.WriteLine("[RenameOverlay] 拦截了穿透点击事件 (MouseUp)");
        }

        private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            if (_isProcessing) return;

            if (IsOpen)
            {
                // 多重焦点检查：Popup 使用 AllowsTransparency="True" 创建独立 HWND，
                // FocusManager.GetFocusedElement 无法正确跟踪跨 HWND 的焦点。
                // 改用 Keyboard.FocusedElement + IsKeyboardFocused 做冗余判断。

                // 检查1：TextBox 自身是否仍持有键盘焦点（最可靠）
                if (RenameTextBox.IsKeyboardFocused)
                    return;

                // 检查2：键盘焦点是否在 Popup 内部的其他元素上（如确认/取消按钮）
                var keyboardFocused = Keyboard.FocusedElement as DependencyObject;
                if (keyboardFocused != null && IsDescendantOf(PopupBorder, keyboardFocused))
                    return;

                // 检查3：FocusManager 兜底（兼容某些特殊焦点作用域场景）
                var focusScope = FocusManager.GetFocusScope(RenameTextBox);
                if (focusScope != null)
                {
                    var fmFocused = FocusManager.GetFocusedElement(focusScope) as DependencyObject;
                    if (fmFocused != null && IsDescendantOf(PopupBorder, fmFocused))
                        return;
                }

                // 实时从配置服务读取，避免 DataContext 绑定的值在设置变更后未及时刷新
                var configService = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<YiboFile.Services.Config.IConfigurationService>(App.ServiceProvider);
                string behavior = configService?.GetSnapshot()?.RenameLostFocusBehavior ?? "Commit";

                if (behavior == "Commit" && ConfirmButton.IsEnabled)
                    DoCommit();
                else
                    DoCancel();
            }
        }

        private void OnTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            var currentText = RenameTextBox.Text;
            bool isDifferent = currentText != _originalName;

            var (isValid, errorMessage) = FileNameValidator.Validate(currentText);

            // 更新校验错误提示
            ValidationErrorText.Text = errorMessage ?? string.Empty;
            ValidationErrorText.Visibility = isValid ? Visibility.Collapsed : Visibility.Visible;

            // 确认按钮：合法且有变化才可用
            ConfirmButton.IsEnabled = isValid && isDifferent;

            // 边框颜色反馈
            if (!isValid)
            {
                PopupBorder.BorderBrush = TryFindResource("ErrorBrush") as Brush
                    ?? new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)); // fallback red
                    
                if (currentText.Length > 0 && currentText != _originalName)
                {
                    TriggerShakeAnimation();
                }
            }
            else
            {
                PopupBorder.BorderBrush = TryFindResource("BorderFocusBrush") as Brush
                    ?? SystemColors.HighlightBrush;
            }
        }

        private void TriggerShakeAnimation()
        {
            var shakeTransform = new System.Windows.Media.TranslateTransform();
            RenameTextBox.RenderTransform = shakeTransform;

            var animation = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames();
            animation.Duration = TimeSpan.FromMilliseconds(300);

            animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(0))));
            animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-3, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))));
            animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(3, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
            animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(-2, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))));
            animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(2, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
            animation.KeyFrames.Add(new System.Windows.Media.Animation.LinearDoubleKeyFrame(0, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));

            shakeTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, animation);
        }

        #endregion

        #region 提交/取消

        private void DoCommit()
        {
            if (_isProcessing) return;
            _isProcessing = true;
            Text = RenameTextBox.Text;
            RenameConfirmed?.Invoke(this, new RenameConfirmedEventArgs());
            _isProcessing = false;
        }

        private void DoCancel()
        {
            if (_isProcessing) return;
            _isProcessing = true;
            RenameCancelled?.Invoke(this, EventArgs.Empty);
            _isProcessing = false;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 检查 descendant 是否是 ancestor 的视觉后代
        /// </summary>
        private static bool IsDescendantOf(DependencyObject? ancestor, DependencyObject descendant)
        {
            if (ancestor == null || descendant == null) return false;

            var current = System.Windows.Media.VisualTreeHelper.GetParent(descendant);
            while (current != null)
            {
                if (current == ancestor) return true;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        #endregion
    }
}
