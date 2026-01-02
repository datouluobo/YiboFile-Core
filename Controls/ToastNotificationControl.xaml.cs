using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace OoiMRR.Controls
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
                    IconText.Text = "✓"; // Checkmark
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(40, 167, 69)); // Green
                    break;
                case NotificationType.Warning:
                    IconText.Text = "⚠️"; // Warning
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Amber
                    break;
                case NotificationType.Error:
                    IconText.Text = "✕"; // Cross
                    IconText.Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red
                    break;
                case NotificationType.Info:
                default:
                    IconText.Text = "ℹ"; // Info
                    IconText.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                    break;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
