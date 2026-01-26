using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using YiboFile.Controls;

namespace YiboFile.Services.Core
{
    /// <summary>
    /// Service for managing and displaying global toast notifications.
    /// </summary>
    public class NotificationService
    {
        private static NotificationService _instance;
        private Panel _notificationContainer;

        public static NotificationService Instance => _instance ??= new NotificationService();

        private NotificationService()
        {
        }

        /// <summary>
        /// Initializes the service with a container (StackPanel) to host notifications.
        /// </summary>
        public void Initialize(Panel container)
        {
            _notificationContainer = container;
        }

        /// <summary>
        /// Shows a notification toast.
        /// </summary>
        public static void Show(string message, NotificationType type = NotificationType.Info, int durationSeconds = 3)
        {
            if (Instance._notificationContainer == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var toast = new ToastNotificationControl();
                toast.Closed += (s, e) =>
                {
                    Instance._notificationContainer.Children.Remove(toast);
                };

                // Add to container first (bottom-up usually if VerticalAlignment=Bottom)
                Instance._notificationContainer.Children.Add(toast);

                toast.Show(message, type, durationSeconds);
            });
        }

        public static void ShowSuccess(string message) => Show(message, NotificationType.Success);
        public static void ShowError(string message) => Show(message, NotificationType.Error, 4);
        public static void ShowWarning(string message) => Show(message, NotificationType.Warning);
        public static void ShowInfo(string message) => Show(message, NotificationType.Info);
    }
}

