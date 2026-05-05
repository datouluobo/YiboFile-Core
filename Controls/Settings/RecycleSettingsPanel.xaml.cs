using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.Controls.Settings
{
    public partial class RecycleSettingsPanel : UserControl, ISettingsPanel
    {
        public event EventHandler SettingsChanged;

        public RecycleSettingsPanel()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshStatus();
        }

        public void LoadSettings() { }
        public void SaveSettings() { }

        private void RefreshStatus()
        {
            uint itemCount = 0;
            long totalSize = 0;

            try
            {
                foreach (var drive in Environment.GetLogicalDrives())
                {
                    var info = new SHQUERYRBINFO();
                    info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
                    int hr = SHQueryRecycleBin(drive, ref info);
                    if (hr == 0)
                    {
                        itemCount += (uint)info.i64NumItems;
                        totalSize += info.i64Size;
                    }
                }
            }
            catch { }

            ItemCountText.Text = $"{itemCount} 项";
            SizeText.Text = itemCount > 0 ? FormatSize(totalSize) : "0 B";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OpenRecycleTabButton_Click(object sender, RoutedEventArgs e)
        {
            // 导航到本程序的回收站标签页
            var messageBus = App.ServiceProvider?.GetService<IMessageBus>();
            if (messageBus != null)
            {
                messageBus.Publish(new OpenContentTabMessage(YiboFile.Services.Tabs.TabContentTypes.Backup));
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("F2") + " GB";
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO
        {
            public int cbSize;
            public long i64Size;
            public long i64NumItems;
        }
    }
}
