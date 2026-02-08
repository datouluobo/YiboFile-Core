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
                    dialog.ConfirmButton.SetResourceReference(Control.BackgroundProperty, "StatusWarningBrush");
                    break;
                case DialogType.Error:
                    dialog.IconTextBlock.Text = "❌";
                    dialog.ConfirmButton.SetResourceReference(Control.BackgroundProperty, "StatusErrorBrush");
                    break;
                case DialogType.Info:
                    dialog.IconTextBlock.Text = "ℹ️";
                    dialog.ConfirmButton.SetResourceReference(Control.BackgroundProperty, "StatusInfoBrush");
                    break;
                case DialogType.Question:
                    dialog.IconTextBlock.Text = "❓";
                    dialog.ConfirmButton.SetResourceReference(Control.BackgroundProperty, "AccentBrush");
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


