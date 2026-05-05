using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using YiboFile.Services.FileOperations.RecycleBin;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.ViewModels.Previews;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using System.Collections;

namespace YiboFile.ViewModels
{
    /// <summary>回收站按日期分组</summary>
    public class RecycleBinDayGroup
    {
        public string Date { get; set; }
        public ObservableCollection<RecycleBinItem> Items { get; set; } = new ObservableCollection<RecycleBinItem>();
        public string CountDisplay => Items.Count > 0 ? $"({Items.Count})" : "";
    }

    public class RecycleBinViewModel : BaseViewModel
    {
        private readonly IRecycleBinService _recycleBinService;
        private readonly IMessageBus _messageBus;
        private readonly Services.UI.IDialogService _dialogService;
        private bool _isLoading;
        private RecycleBinDayGroup _selectedGroup;
        private RecycleBinItem _selectedItem;
        private int _totalItems;

        public RecycleBinViewModel(
            IRecycleBinService recycleBinService,
            IMessageBus messageBus = null,
            Services.UI.IDialogService dialogService = null)
        {
            _recycleBinService = recycleBinService ?? throw new ArgumentNullException(nameof(recycleBinService));
            _messageBus = messageBus;
            _dialogService = dialogService ?? App.ServiceProvider?.GetService<Services.UI.IDialogService>();
        }

        public ObservableCollection<RecycleBinDayGroup> Groups { get; set; }
            = new ObservableCollection<RecycleBinDayGroup>();

        public RecycleBinDayGroup SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (SetProperty(ref _selectedGroup, value))
                    OnPropertyChanged(nameof(SelectedItems));
            }
        }

        public RecycleBinItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                    UpdatePreview();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public int TotalItems
        {
            get => _totalItems;
            set => SetProperty(ref _totalItems, value);
        }

        public ObservableCollection<RecycleBinItem> SelectedItems =>
            SelectedGroup?.Items ?? new ObservableCollection<RecycleBinItem>();

        // ── Commands ────────────────────────────────────────────

        public ICommand LoadCommand => _loadCmd ??= new RelayCommand(async () => await LoadAsync());
        private ICommand _loadCmd;

        public ICommand RestoreCommand => _restoreCmd ??= new RelayCommand<RecycleBinItem>(async (item) => await RestoreAsync(item));
        private ICommand _restoreCmd;

        public ICommand DeleteCommand => _deleteCmd ??= new RelayCommand<RecycleBinItem>(async (item) => await DeleteAsync(item));
        private ICommand _deleteCmd;

        public ICommand EmptyBinCommand => _emptyCmd ??= new RelayCommand(async () => await EmptyBinAsync());
        private ICommand _emptyCmd;

        // ── Operations ──────────────────────────────────────────

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var items = await Task.Run(() => _recycleBinService.ListItems());
                Groups.Clear();

                // Group by date
                var grouped = items
                    .GroupBy(i => i.DeletionTime.Date)
                    .OrderByDescending(g => g.Key);

                foreach (var group in grouped)
                {
                    var dayGroup = new RecycleBinDayGroup
                    {
                        Date = group.Key.ToString("yyyy-MM-dd"),
                        Items = new ObservableCollection<RecycleBinItem>(group.OrderByDescending(i => i.DeletionTime))
                    };
                    Groups.Add(dayGroup);
                }

                TotalItems = items.Count;
                if (Groups.Count > 0) SelectedGroup = Groups[0];
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RestoreAsync(RecycleBinItem item)
        {
            if (item == null) return;
            try
            {
                bool ok = await Task.Run(() =>
                {
                    var path = _recycleBinService.Restore(item.OriginalPath);
                    return path != null;
                });

                if (ok)
                {
                    RemoveItem(item);
                    _dialogService?.ShowInfo($"已还原: {item.Name}");
                }
                else
                {
                    _dialogService?.ShowError($"还原失败: {item.Name} (可能在回收站中已不存在)");
                }
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"还原失败: {ex.Message}");
            }
        }

        private async Task DeleteAsync(RecycleBinItem item)
        {
            if (item == null) return;
            if (_dialogService?.Confirm($"确定要永久删除 \"{item.Name}\" 吗？", "确认删除") != true)
                return;

            try
            {
                // Permanent delete: delete the actual file in the recycle bin
                if (!string.IsNullOrEmpty(item.BackupPath))
                {
                    await Task.Run(() =>
                    {
                        if (item.IsDirectory && System.IO.Directory.Exists(item.BackupPath))
                            System.IO.Directory.Delete(item.BackupPath, true);
                        else if (System.IO.File.Exists(item.BackupPath))
                            System.IO.File.Delete(item.BackupPath);
                    });
                }
                RemoveItem(item);
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"删除失败: {ex.Message}");
            }
        }

        private async Task EmptyBinAsync()
        {
            if (_dialogService?.Confirm("确定要清空回收站吗？此操作不可撤销。", "清空回收站") != true)
                return;

            IsLoading = true;
            try
            {
                bool ok = await Task.Run(() => _recycleBinService.Empty());
                if (ok)
                {
                    Groups.Clear();
                    TotalItems = 0;
                    _dialogService?.ShowInfo("回收站已清空");
                }
                else
                {
                    _dialogService?.ShowError("清空回收站失败");
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void RemoveItem(RecycleBinItem item)
        {
            foreach (var group in Groups)
            {
                if (group.Items.Remove(item))
                {
                    OnPropertyChanged(nameof(SelectedItems));
                    if (group.Items.Count == 0)
                    {
                        Groups.Remove(group);
                        if (Groups.Count > 0) SelectedGroup = Groups[0];
                    }
                    TotalItems = Groups.Sum(g => g.Items.Count);
                    return;
                }
            }
        }

        private void UpdatePreview()
        {
            if (_messageBus == null) return;

            bool exists = false;
            if (SelectedItem != null && !string.IsNullOrEmpty(SelectedItem.BackupPath))
            {
                exists = SelectedItem.IsDirectory
                    ? System.IO.Directory.Exists(SelectedItem.BackupPath)
                    : System.IO.File.Exists(SelectedItem.BackupPath);
            }

            if (SelectedItem != null && exists)
            {
                var dummyItem = new Models.FileSystemItem
                {
                    Path = SelectedItem.BackupPath,
                    Name = SelectedItem.Name,
                    IsDirectory = SelectedItem.IsDirectory
                };
                _messageBus.Publish(new FileSelectionChangedMessage(
                    new List<Models.FileSystemItem> { dummyItem },
                    RequestPreview: true,
                    Pane: Services.Navigation.PaneId.Main,
                    ShowNotes: false));
            }
            else
            {
                _messageBus.Publish(new FileSelectionChangedMessage(
                    new List<Models.FileSystemItem>(),
                    RequestPreview: true,
                    Pane: Services.Navigation.PaneId.Main,
                    ShowNotes: false));
            }
        }
    }
}
