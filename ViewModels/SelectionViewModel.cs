using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using YiboFile.Models;
using YiboFile.Services.FileList;
using YiboFile.Services.Navigation;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.ViewModels
{
    public class SelectionViewModel : BaseViewModel
    {
        private readonly IMessageBus _messageBus;
        public bool IsSecondary { get; set; }
        private readonly FolderSizeCalculationService _folderSizeService;

        private ObservableCollection<FileSystemItem> _selectedItems = new ObservableCollection<FileSystemItem>();
        private FileSystemItem _selectedItem;
        private readonly System.Collections.Generic.HashSet<string> _calculatedPaths = new System.Collections.Generic.HashSet<string>();


        public event EventHandler SelectionChanged;

        protected new void OnPropertyChanged(string propertyName)
        {
            base.OnPropertyChanged(propertyName);
        }

        public SelectionViewModel(IMessageBus messageBus, bool isSecondary)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            IsSecondary = isSecondary;
            _folderSizeService = App.ServiceProvider.GetService<FolderSizeCalculationService>();
        }

        public ObservableCollection<FileSystemItem> SelectedItems
        {
            get => _selectedItems;
            private set => SetProperty(ref _selectedItems, value);
        }

        public FileSystemItem SelectedItem
        {
            get => _selectedItem;
            private set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(IsSingleSelection));
                    OnPropertyChanged(nameof(IsNoSelection));
                }
            }
        }

        public bool HasSelection => SelectedItems != null && SelectedItems.Count > 0;
        public bool IsSingleSelection => SelectedItems != null && SelectedItems.Count == 1;
        public bool IsNoSelection => SelectedItems == null || SelectedItems.Count == 0;

        public void UpdateSelection(IList items, string currentPath)
        {
            _selectedItems.Clear();
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item is FileSystemItem fsItem)
                    {
                        _selectedItems.Add(fsItem);
                    }
                }
            }
            SelectedItem = _selectedItems.FirstOrDefault();

            // 触发属性变更通知以更新菜单项的可见性
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsSingleSelection));
            OnPropertyChanged(nameof(IsNoSelection));

            SelectionChanged?.Invoke(this, EventArgs.Empty);

            // 发送消息以便其他模块（如预览面板）同步
            var paneId = IsSecondary ? PaneId.Second : PaneId.Main;
            if (SelectedItem != null)
            {
                // 如果只选择了一个项，请求预览
                _messageBus.Publish(new FileSelectionChangedMessage(SelectedItems.ToList(), true, paneId));

                // 如果是文件夹且大小未计算，触发计算
                if (SelectedItem.IsDirectory && (string.IsNullOrEmpty(SelectedItem.Size) || SelectedItem.Size == "-" || SelectedItem.Size == "计算中..."))
                {
                    if (!_calculatedPaths.Contains(SelectedItem.Path))
                    {
                        _calculatedPaths.Add(SelectedItem.Path);
                        _folderSizeService?.CalculateAndUpdateFolderSizeAsync(SelectedItem.Path);
                    }
                }
            }
            else
            {
                // 无选择时通知
                _messageBus.Publish(new FileSelectionChangedMessage(null, true, paneId));
            }
        }

        public void ClearSelection()
        {
            _selectedItems.Clear();
            SelectedItem = null;

            // 触发属性变更通知以更新菜单项的可见性
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsSingleSelection));
            OnPropertyChanged(nameof(IsNoSelection));

            SelectionChanged?.Invoke(this, EventArgs.Empty);

            var paneId = IsSecondary ? PaneId.Second : PaneId.Main;
            _messageBus.Publish(new FileSelectionChangedMessage(null, true, paneId));
        }

        public void SelectAll(System.Collections.Generic.IEnumerable<FileSystemItem> allItems)
        {
            if (allItems == null) return;

            _selectedItems.Clear();
            foreach (var item in allItems)
            {
                _selectedItems.Add(item);
            }
            SelectedItem = _selectedItems.FirstOrDefault();

            // 触发属性变更通知以更新菜单项的可见性
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(IsSingleSelection));
            OnPropertyChanged(nameof(IsNoSelection));

            SelectionChanged?.Invoke(this, EventArgs.Empty);
            var paneId = IsSecondary ? PaneId.Second : PaneId.Main;
            _messageBus.Publish(new FileSelectionChangedMessage(SelectedItems.ToList(), true, paneId));
        }
    }
}
