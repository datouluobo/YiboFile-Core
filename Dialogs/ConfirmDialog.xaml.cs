using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YiboFile.Dialogs
{
    public partial class ConfirmDialog : Window
    {
        public enum DialogType
        {
            Warning,
            Error,
            Info,
            Question
        }

        public bool Result { get; private set; } = false;

        public ConfirmDialog()
        {
            InitializeComponent();
            this.KeyDown += ConfirmDialog_KeyDown;
            this.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) this.DragMove(); };
        }

        public static bool Show(string message, string title = "确认", DialogType type = DialogType.Warning, Window owner = null, bool showCancel = true)
        {
            var dialog = new ConfirmDialog
            {
                Owner = owner
            };

            dialog.ConfirmTitle.Text = title;
            dialog.MessageTextBlock.Text = message;

            // 根据类型设置图标和按钮颜色
            switch (type)
            {
                case DialogType.Warning:
                    dialog.IconTextBlock.Text = "⚠️";
                    dialog.SetConfirmButtonColor("#FF9800", "#F57C00", "#EF6C00");
                    break;
                case DialogType.Error:
                    dialog.IconTextBlock.Text = "❌";
                    dialog.SetConfirmButtonColor("#F44336", "#E53935", "#D32F2F");
                    break;
                case DialogType.Info:
                    dialog.IconTextBlock.Text = "ℹ️";
                    dialog.SetConfirmButtonColor("#2196F3", "#1976D2", "#1565C0");
                    break;
                case DialogType.Question:
                    dialog.IconTextBlock.Text = "❓";
                    dialog.SetConfirmButtonColor("#2196F3", "#1976D2", "#1565C0");
                    break;
            }

            // 取消按钮可选
            if (!showCancel)
            {
                dialog.CancelButton.Visibility = Visibility.Collapsed;
            }

            dialog.ShowDialog();
            return dialog.Result;
        }

        private void SetConfirmButtonColor(string normal, string hover, string pressed)
        {
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(normal))));
            style.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(Button.HeightProperty, 32.0));
            style.Setters.Add(new Setter(Button.MinWidthProperty, 80.0));
            style.Setters.Add(new Setter(Button.MarginProperty, new Thickness(8, 0, 0, 0)));

            var template = new ControlTemplate(typeof(Button));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            factory.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding("BorderThickness") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            factory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding("BorderBrush") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            factory.AppendChild(contentPresenter);

            template.VisualTree = factory;
            style.Setters.Add(new Setter(Button.TemplateProperty, template));

            var mouseOverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            mouseOverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(hover))));
            style.Triggers.Add(mouseOverTrigger);

            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(pressed))));
            style.Triggers.Add(pressedTrigger);

            ConfirmButton.Style = style;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            this.Close();
        }

        private void ConfirmDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Confirm_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }
    }
}


