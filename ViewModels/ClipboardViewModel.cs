using System;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using YiboFile.Services.ClipboardHistory;

namespace YiboFile.ViewModels
{
    public class ClipboardViewModel : BaseViewModel
    {
        private readonly ClipboardHistoryService _historyService;
        private string _searchText = string.Empty;
        private ICollectionView _historyView;

        public ClipboardViewModel()
        {
            _historyService = ClipboardHistoryService.Instance;

            // Create a collection view for filtering
            _historyView = CollectionViewSource.GetDefaultView(_historyService.History);
            _historyView.Filter = FilterItem;

            DeleteCommand = new RelayCommand<ClipboardHistoryItem>(DeleteItem);
            ClearCommand = new RelayCommand(ClearHistory);
            PasteCommand = new RelayCommand<ClipboardHistoryItem>(PasteItem);
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

        public ICommand DeleteCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand PasteCommand { get; }

        /// <summary>
        /// Event triggered when an item is pasted
        /// </summary>
        public event Action<ClipboardHistoryItem> ItemPasted;

        private bool FilterItem(object obj)
        {
            if (obj is not ClipboardHistoryItem item) return false;

            if (string.IsNullOrWhiteSpace(SearchText)) return true;

            // Search in preview text
            return item.Preview.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DeleteItem(ClipboardHistoryItem item)
        {
            if (item != null)
            {
                _historyService.RemoveItem(item);
            }
        }

        private void ClearHistory()
        {
            _historyService.ClearHistory();
        }

        private void PasteItem(ClipboardHistoryItem item)
        {
            if (item != null && _historyService.SetToClipboard(item))
            {
                ItemPasted?.Invoke(item);
            }
        }
    }
}
