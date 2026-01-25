using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Backup;
using YiboFile.ViewModels;

namespace YiboFile.Controls
{
    public partial class BackupBrowserControl : UserControl
    {
        public BackupBrowserControl()
        {
            InitializeComponent();

            // Resolve ViewModel
            if (App.ServiceProvider != null)
            {
                var backupService = App.ServiceProvider.GetService<IBackupService>();
                if (backupService != null)
                {
                    var vm = new BackupViewModel(backupService);
                    this.DataContext = vm;

                    // Auto load on Visible changed (e.g. when switched to)
                    this.IsVisibleChanged += async (s, e) =>
                    {
                        if (this.IsVisible && !vm.IsLoading)
                        {
                            await vm.LoadBackupsAsync();
                        }
                    };
                }
            }
        }
    }
}
