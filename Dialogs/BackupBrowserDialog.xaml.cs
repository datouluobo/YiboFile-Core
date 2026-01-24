using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Backup;
using YiboFile.ViewModels;

namespace YiboFile.Dialogs
{
    public partial class BackupBrowserDialog : Window
    {
        private AppConfig _config;
        public BackupBrowserDialog()
        {
            InitializeComponent();

            // Resolve Config and ViewModel
            if (App.ServiceProvider != null)
            {
                _config = App.ServiceProvider.GetService<AppConfig>();
                if (_config != null)
                {
                    this.Width = _config.BackupBrowserWidth;
                    this.Height = _config.BackupBrowserHeight;
                }

                var backupService = App.ServiceProvider.GetService<IBackupService>();
                if (backupService != null)
                {
                    var vm = new BackupViewModel(backupService);
                    this.DataContext = vm;

                    // Auto load
                    this.Loaded += async (s, e) => await vm.LoadBackupsAsync();
                }
            }

            this.Closing += BackupBrowserDialog_Closing;
        }

        private void BackupBrowserDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_config != null)
            {
                _config.BackupBrowserWidth = this.ActualWidth;
                _config.BackupBrowserHeight = this.ActualHeight;
                YiboFile.ConfigManager.Save(_config);
            }
        }
    }
}
