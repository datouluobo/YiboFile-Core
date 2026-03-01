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
        private YiboFile.Services.Config.IConfigurationService _configService;
        public BackupBrowserDialog()
        {
            InitializeComponent();

            // Resolve Config and ViewModel
            if (App.ServiceProvider != null)
            {
                _configService = App.ServiceProvider.GetService<YiboFile.Services.Config.IConfigurationService>();
                if (_configService != null)
                {
                    _config = _configService.Config;
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
            if (_configService != null)
            {
                _configService.Update(c =>
                {
                    c.BackupBrowserWidth = this.ActualWidth;
                    c.BackupBrowserHeight = this.ActualHeight;
                });
            }
        }
    }
}
