using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using YiboFile.Services.UI;

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

        public enum DialogButtons
        {
            Ok,
            OkCancel,
            YesNo,
            YesNoCancel
        }

        public AppDialogResult Result { get; private set; } = AppDialogResult.None;

        public ConfirmDialog()
        {
            InitializeComponent();
            this.KeyDown += ConfirmDialog_KeyDown;
            this.MouseLeftButtonDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) this.DragMove(); };
        }

        public static AppDialogResult Show(string message, string title = "确认", 
            DialogType type = DialogType.Warning, 
            DialogButtons buttons = DialogButtons.OkCancel,
            Window owner = null)
        {
            var dialog = new ConfirmDialog
            {
                Owner = owner ?? Application.Current.MainWindow
            };

            dialog.ConfirmTitle.Text = title;
            dialog.MessageTextBlock.Text = message;

            // 设置按钮可见性
            switch (buttons)
            {
                case DialogButtons.Ok:
                    dialog.ConfirmButton.Visibility = Visibility.Visible;
                    dialog.CancelButton.Visibility = Visibility.Collapsed;
                    dialog.YesButton.Visibility = Visibility.Collapsed;
                    dialog.NoButton.Visibility = Visibility.Collapsed;
                    break;
                case DialogButtons.OkCancel:
                    dialog.ConfirmButton.Visibility = Visibility.Visible;
                    dialog.CancelButton.Visibility = Visibility.Visible;
                    dialog.YesButton.Visibility = Visibility.Collapsed;
                    dialog.NoButton.Visibility = Visibility.Collapsed;
                    break;
                case DialogButtons.YesNo:
                    dialog.ConfirmButton.Visibility = Visibility.Collapsed;
                    dialog.CancelButton.Visibility = Visibility.Collapsed;
                    dialog.YesButton.Visibility = Visibility.Visible;
                    dialog.NoButton.Visibility = Visibility.Visible;
                    break;
                case DialogButtons.YesNoCancel:
                    dialog.ConfirmButton.Visibility = Visibility.Collapsed;
                    dialog.CancelButton.Visibility = Visibility.Visible;
                    dialog.YesButton.Visibility = Visibility.Visible;
                    dialog.NoButton.Visibility = Visibility.Visible;
                    break;
            }

            // 根据类型设置图标和确认按钮主色调
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

            dialog.ShowDialog();
            return dialog.Result;
        }

        // 兼容旧版 bool 返回的方法
        public static bool Show(string message, string title = "确认", DialogType type = DialogType.Warning, Window owner = null, bool showCancel = true)
        {
            var res = Show(message, title, type, showCancel ? DialogButtons.OkCancel : DialogButtons.Ok, owner);
            return res == AppDialogResult.OK;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Result = AppDialogResult.OK;
            this.DialogResult = true;
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Result = AppDialogResult.Cancel;
            this.DialogResult = false;
            this.Close();
        }

        private void Yes_Click(object sender, RoutedEventArgs e)
        {
            Result = AppDialogResult.Yes;
            this.DialogResult = true;
            this.Close();
        }

        private void No_Click(object sender, RoutedEventArgs e)
        {
            Result = AppDialogResult.No;
            this.DialogResult = false;
            this.Close();
        }

        private void ConfirmDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ConfirmButton.Visibility == Visibility.Visible) Confirm_Click(sender, e);
                else if (YesButton.Visibility == Visibility.Visible) Yes_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Cancel_Click(sender, e);
            }
        }
    }
}
