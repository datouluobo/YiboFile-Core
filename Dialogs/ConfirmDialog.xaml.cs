using System;
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
            try
            {
                var normalBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(normal));
                if (normalBrush.CanFreeze) normalBrush.Freeze();

                ConfirmButton.Background = normalBrush;
                // 注意：由于我们使用的是 XAML 中的静态样式，直接修改 Background 
                // 只会改变默认背景，触发器效果可能消失，但这能彻底避免 XamlParseException
            }
            catch (Exception)
            {
                // 如果转换失败，至少保证不崩
                ConfirmButton.Background = Brushes.Orange;
            }
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


