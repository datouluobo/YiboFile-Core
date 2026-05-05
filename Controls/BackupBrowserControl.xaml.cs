using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.FileOperations.RecycleBin;
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
                var recycleService = App.ServiceProvider.GetService<IRecycleBinService>();
                var messageBus = App.ServiceProvider.GetService<YiboFile.ViewModels.Messaging.IMessageBus>();
                if (recycleService != null)
                {
                    var vm = new RecycleBinViewModel(recycleService, messageBus);
                    this.DataContext = vm;

                    // Auto load on Visible changed
                    this.IsVisibleChanged += async (s, e) =>
                    {
                        if (this.IsVisible && !vm.IsLoading)
                        {
                            await vm.LoadAsync();
                        }
                    };
                }
            }
        }
    }
}
