using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace YiboFile.Controls
{
    /// <summary>
    /// Notification Type Enum
    /// </summary>
    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Interaction logic for ToastNotificationControl.xaml
    /// </summary>
    public partial class ToastNotificationControl : UserControl
    {
        private DispatcherTimer _autoCloseTimer;
        public event EventHandler Closed;

        public ToastNotificationControl()
        {
            InitializeComponent();
        }

        public void Show(string message, NotificationType type, int durationSeconds = 3)
        {
            MessageText.Text = message;
            ConfigureType(type);

            // Play Show Animation
            var sb = (Storyboard)FindResource("ShowAnimation");
            sb.Begin(this);

            // Start Auto-close Timer
            if (durationSeconds > 0)
            {
                _autoCloseTimer = new DispatcherTimer();
                _autoCloseTimer.Interval = TimeSpan.FromSeconds(durationSeconds);
                _autoCloseTimer.Tick += (s, e) =>
                {
                    _autoCloseTimer.Stop();
                    Close();
                };
                _autoCloseTimer.Start();
            }
        }

        public void Close()
        {
            if (_autoCloseTimer != null)
            {
                _autoCloseTimer.Stop();
            }

            // Play Hide Animation
            var sb = (Storyboard)FindResource("HideAnimation");
            sb.Completed += (s, e) =>
            {
                Closed?.Invoke(this, EventArgs.Empty);
            };
            sb.Begin(this);
        }

        private void ConfigureType(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Success:
                    IconImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Icon_Success");
                    break;
                case NotificationType.Warning:
                    IconImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Icon_Warning");
                    break;
                case NotificationType.Error:
                    IconImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Icon_Error");
                    break;
                case NotificationType.Info:
                default:
                    IconImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, "Icon_Info");
                    break;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}

