using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using YiboFile.Services.ClipboardHistory;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;

namespace YiboFile.ViewModels
{
    public class ClipboardViewModel : BaseViewModel
    {
        private readonly ClipboardHistoryService _historyService;
        private readonly AppConfig _config;
        private readonly IMessageBus _messageBus;

        private string _searchText = string.Empty;
        private ClipboardFilterType _activeFilter = ClipboardFilterType.All;
        private ClipboardHistoryItem _selectedItem;
        private bool _isSettingsOpen;
        private ICollectionView _historyView;

        public ClipboardViewModel()
        {
            _historyService = ClipboardHistoryService.Instance;
            _config = Services.Config.ConfigurationService.Instance.Config;
            _messageBus = App.ServiceProvider?.GetService(typeof(IMessageBus)) as IMessageBus;

            // Create a collection view for filtering
            _historyView = CollectionViewSource.GetDefaultView(_historyService.History);
            _historyView.Filter = FilterItem;
            _historyView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ClipboardHistoryItem.TimeGroup)));
            _historyView.SortDescriptions.Add(new SortDescription(nameof(ClipboardHistoryItem.Timestamp), ListSortDirection.Descending));

            DeleteCommand = new RelayCommand<ClipboardHistoryItem>(DeleteItem);
            ClearCommand = new RelayCommand(ClearHistory);
            PasteCommand = new RelayCommand<ClipboardHistoryItem>(PasteItem);
            TogglePinCommand = new RelayCommand<ClipboardHistoryItem>(TogglePin);
            SetFilterCommand = new RelayCommand<string>(SetFilter);
            OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = !IsSettingsOpen);
        }

        public ICollectionView HistoryView => _historyView;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _historyView.Refresh();
                }
            }
        }

        public ClipboardFilterType ActiveFilter
        {
            get => _activeFilter;
            set
            {
                if (SetProperty(ref _activeFilter, value))
                {
                    _historyView.Refresh();
                    OnPropertyChanged(nameof(IsFilterAll));
                    OnPropertyChanged(nameof(IsFilterFiles));
                    OnPropertyChanged(nameof(IsFilterImages));
                    OnPropertyChanged(nameof(IsFilterText));
                    OnPropertyChanged(nameof(IsFilterPinned));
                }
            }
        }

        public bool IsFilterAll => ActiveFilter == ClipboardFilterType.All;
        public bool IsFilterFiles => ActiveFilter == ClipboardFilterType.Files;
        public bool IsFilterImages => ActiveFilter == ClipboardFilterType.Images;
        public bool IsFilterText => ActiveFilter == ClipboardFilterType.Text;
        public bool IsFilterPinned => ActiveFilter == ClipboardFilterType.Pinned;

        public ClipboardHistoryItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value))
                {
                    UpdatePreview();
                }
            }
        }

        public bool IsSettingsOpen
        {
            get => _isSettingsOpen;
            set => SetProperty(ref _isSettingsOpen, value);
        }

        public int TotalCount => _historyService.History.Count;

        public string StatusText
        {
            get
            {
                var count = _historyService.History.Count;
                var cleanInfo = _config.ClipboardAutoClean
                    ? $" · 自动清理: {_config.ClipboardRetentionDays}天"
                    : "";
                return $"{count} 条记录 · 双击粘贴{cleanInfo}";
            }
        }

        // ── 设置属性（直接绑定配置） ──

        public int MaxHistory
        {
            get => _config.ClipboardMaxHistory;
            set
            {
                _config.ClipboardMaxHistory = Math.Clamp(value, 5, 500);
                Services.Config.ConfigurationService.Instance.SaveNow();
                OnPropertyChanged();
            }
        }

        public bool AutoClean
        {
            get => _config.ClipboardAutoClean;
            set
            {
                _config.ClipboardAutoClean = value;
                Services.Config.ConfigurationService.Instance.SaveNow();
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public int RetentionDays
        {
            get => _config.ClipboardRetentionDays;
            set
            {
                _config.ClipboardRetentionDays = Math.Clamp(value, 1, 365);
                Services.Config.ConfigurationService.Instance.SaveNow();
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
            }
        }

        public bool PersistHistory
        {
            get => _config.ClipboardPersistHistory;
            set
            {
                _config.ClipboardPersistHistory = value;
                Services.Config.ConfigurationService.Instance.SaveNow();
                OnPropertyChanged();
            }
        }

        public bool CaptureFiles { get => _config.ClipboardCaptureFiles; set { _config.ClipboardCaptureFiles = value; Services.Config.ConfigurationService.Instance.SaveNow(); OnPropertyChanged(); } }
        public bool CaptureImages { get => _config.ClipboardCaptureImages; set { _config.ClipboardCaptureImages = value; Services.Config.ConfigurationService.Instance.SaveNow(); OnPropertyChanged(); } }
        public bool CaptureText { get => _config.ClipboardCaptureText; set { _config.ClipboardCaptureText = value; Services.Config.ConfigurationService.Instance.SaveNow(); OnPropertyChanged(); } }
        public bool CaptureScreenshots { get => _config.ClipboardCaptureScreenshots; set { _config.ClipboardCaptureScreenshots = value; Services.Config.ConfigurationService.Instance.SaveNow(); OnPropertyChanged(); } }

        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand TogglePinCommand { get; }
        public ICommand SetFilterCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        /// <summary>
        /// Event triggered when an item is pasted
        /// </summary>
        public event Action<ClipboardHistoryItem> ItemPasted;
        
        // ── 预览推送（复用右侧文件预览面板，与 BackupViewModel 同模式） ──

        private void UpdatePreview()
        {
            if (_messageBus == null) return;

            var item = SelectedItem;
            if (item != null)
            {
                // 优先使用文件路径（文件/图片类型）
                string previewPath = item.FirstFilePath;
                if (!string.IsNullOrEmpty(previewPath) && System.IO.File.Exists(previewPath))
                {
                    var dummyItem = new Models.FileSystemItem
                    {
                        Path = previewPath,
                        Name = System.IO.Path.GetFileName(previewPath),
                        IsDirectory = false
                    };
                    _messageBus.Publish(new FileSelectionChangedMessage(
                        new List<Models.FileSystemItem> { dummyItem },
                        RequestPreview: true,
                        Pane: Services.Navigation.PaneId.Main,
                        ShowNotes: false));
                    return;
                }
            }

            // 无选中项 或 无可预览文件 → 清空预览
            _messageBus.Publish(new FileSelectionChangedMessage(
                new List<Models.FileSystemItem>(),
                RequestPreview: true,
                Pane: Services.Navigation.PaneId.Main,
                ShowNotes: false));
        }

        private bool FilterItem(object obj)
        {
            if (obj is not ClipboardHistoryItem item) return false;

            // 分类过滤
            bool passFilter = _activeFilter switch
            {
                ClipboardFilterType.All => true,
                ClipboardFilterType.Files => item.Type == ClipboardItemType.Files && !item.IsImage,
                ClipboardFilterType.Images => item.IsImage || item.Type == ClipboardItemType.Image,
                ClipboardFilterType.Text => item.Type == ClipboardItemType.Text,
                ClipboardFilterType.Pinned => item.IsPinned,
                _ => true
            };
            if (!passFilter) return false;

            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            // Search in preview text
            return item.Preview.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DeleteItem(ClipboardHistoryItem item)
        {
            if (item != null)
            {
                _historyService.RemoveItem(item);
                RefreshCounters();
            }
        }

        private void ClearHistory()
        {
            _historyService.ClearHistory();
            RefreshCounters();
        }

        private void PasteItem(ClipboardHistoryItem item)
        {
            if (item != null && _historyService.SetToClipboard(item))
            {
                ItemPasted?.Invoke(item);
            }
        }

        private void TogglePin(ClipboardHistoryItem item)
        {
            if (item != null) _historyService.TogglePin(item);
            if (_activeFilter == ClipboardFilterType.Pinned)
                _historyView.Refresh();
        }

        private void SetFilter(string filterName)
        {
            if (Enum.TryParse<ClipboardFilterType>(filterName, out var filter))
                ActiveFilter = filter;
        }

        private void RefreshCounters()
        {
            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(StatusText));
        }
    }
}
