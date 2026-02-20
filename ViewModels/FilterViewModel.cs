using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.Services.Navigation;
using YiboFile.Services.Search;
using YiboFile.Services.Core;
using YiboFile.ViewModels.Messaging;

namespace YiboFile.ViewModels
{
    public class FilterViewModel : BaseViewModel
    {
        private readonly IMessageBus _messageBus;
        private readonly SearchService _searchService;
        private readonly SearchCacheService _searchCacheService;
        private readonly Dispatcher _dispatcher;

        private SearchOptions _searchOptions = new SearchOptions();
        private bool _isFilterPanelVisible;
        private bool _isLoadMoreVisible;
        private bool _isSearching;
        private string _searchStatusText;
        private int _searchOffset;

        // 事件
        public event EventHandler FilterChanged;
        public event EventHandler<List<FileSystemItem>> MoreResultsLoaded;

        // 命令
        public ICommand ApplyFilterCommand { get; }
        public ICommand ToggleFilterPanelCommand { get; }
        public ICommand LoadMoreCommand { get; }

        public FilterViewModel(IMessageBus messageBus, SearchService searchService, SearchCacheService searchCacheService)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            _searchService = searchService;
            _searchCacheService = searchCacheService;
            _dispatcher = Dispatcher.CurrentDispatcher;

            ApplyFilterCommand = new RelayCommand(NotifyFilterChanged);
            ToggleFilterPanelCommand = new RelayCommand(() => IsFilterPanelVisible = !IsFilterPanelVisible);

            // 上下文 (路径) 必须作为参数传递
            LoadMoreCommand = new RelayCommand<string>(ExecuteLoadMore);
        }

        public SearchOptions SearchOptions
        {
            get => _searchOptions;
            set
            {
                if (SetProperty(ref _searchOptions, value))
                {
                    NotifyFilterChanged();
                }
            }
        }

        public bool IsFilterPanelVisible
        {
            get => _isFilterPanelVisible;
            set => SetProperty(ref _isFilterPanelVisible, value);
        }

        public bool IsLoadMoreVisible
        {
            get => _isLoadMoreVisible;
            set => SetProperty(ref _isLoadMoreVisible, value);
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public string SearchStatusText
        {
            get => _searchStatusText;
            set => SetProperty(ref _searchStatusText, value);
        }

        public int SearchOffset
        {
            get => _searchOffset;
            set => _searchOffset = value;
        }

        public bool IsFilterActive
        {
            get
            {
                if (_searchOptions == null) return false;
                return _searchOptions.Type != FileTypeFilter.All
                    || _searchOptions.DateRange != DateRangeFilter.All
                    || _searchOptions.SizeRange != SizeRangeFilter.All
                    || _searchOptions.ImageSize != ImageDimensionFilter.All
                    || _searchOptions.Duration != AudioDurationFilter.All;
            }
        }

        private void NotifyFilterChanged()
        {
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }

        private async void ExecuteLoadMore(string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath)) return;
            await LoadMoreAsync(currentPath);
        }

        public async Task LoadMoreAsync(string currentPath)
        {
            if (_searchService == null || _searchCacheService == null) return;

            try
            {
                var protocolInfo = ProtocolManager.Parse(currentPath);
                if (protocolInfo.Type != ProtocolType.Search) return;

                var keyword = protocolInfo.TargetPath;
                if (string.IsNullOrEmpty(keyword)) return;

                // 从缓存获取当前偏移量
                var cacheKey = $"search://{keyword}";
                var cache = _searchCacheService.GetCache(cacheKey);
                if (cache == null || !cache.HasMore) return;

                IsSearching = true;
                SearchStatusText = "正在加载更多结果...";

                var moreResult = await Task.Run(() => _searchService.LoadMore(keyword, cache.Offset, _searchOptions, currentPath));

                // 检查 moreResult 和 Items 是否为空 
                if (moreResult != null && moreResult.Items != null && moreResult.Items.Count > 0)
                {
                    await _dispatcher.InvokeAsync(() =>
                    {
                        var items = new List<FileSystemItem>(moreResult.Items);
                        _searchOffset = moreResult.Offset;
                        IsLoadMoreVisible = moreResult.HasMore;

                        MoreResultsLoaded?.Invoke(this, items);
                    });
                }
                else if (moreResult != null)
                {
                    // Items 为空但 moreResult 不为空，更新状态
                    await _dispatcher.InvokeAsync(() =>
                    {
                        _searchOffset = moreResult.Offset;
                        IsLoadMoreVisible = moreResult.HasMore;
                    });
                }
            }
            catch (Exception)
            {

            }
            finally
            {
                IsSearching = false;
                SearchStatusText = "";
            }
        }
    }
}
