using System;
using System.Windows.Input;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Services.Search;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 搜索视图模型（混合架构 - 传统 MVVM）
    /// 职责：存储搜索和过滤器的 UI 状态，直接处理简单逻辑
    /// 复杂业务逻辑（如范围预设）通过 SearchCoordinator 处理
    /// </summary>
    public class SearchViewModel : BaseViewModel
    {
        private readonly IMessageBus _messageBus;
        private string _searchText;
        private bool _searchNames = true;
        private bool _searchNotes = true;
        private bool _searchFolders = true;
        private string _targetPaneId = "Primary";

        // 过滤器状态
        private FileTypeFilter _typeFilter = FileTypeFilter.All;
        private DateRangeFilter _dateFilter = DateRangeFilter.All;
        private SizeRangeFilter _sizeFilter = SizeRangeFilter.All;
        private ImageDimensionFilter _imageSizeFilter = ImageDimensionFilter.All;
        private AudioDurationFilter _durationFilter = AudioDurationFilter.All;
        private SearchMode _searchMode = SearchMode.FileName;
        private PathRangeFilter _pathRange = PathRangeFilter.CurrentDrive;

        public SearchViewModel(IMessageBus messageBus)
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));

            // ✅ 简单命令：直接处理
            SearchCommand = new RelayCommand(ExecuteSearch);
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);

            // ✅ 复杂命令：通过 Coordinator 处理
            SetScopePresetCommand = new RelayCommand<string>(preset =>
                _messageBus.Publish(new RequestSetScopePresetMessage(preset)));
        }

        #region 状态属性

        /// <summary>
        /// 搜索文本
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        /// <summary>
        /// 是否搜索文件名
        /// </summary>
        public bool SearchNames
        {
            get => _searchNames;
            set
            {
                if (SetProperty(ref _searchNames, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 是否搜索备注
        /// </summary>
        public bool SearchNotes
        {
            get => _searchNotes;
            set
            {
                if (SetProperty(ref _searchNotes, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 是否搜索文件夹
        /// </summary>
        public bool SearchFolders
        {
            get => _searchFolders;
            set
            {
                if (SetProperty(ref _searchFolders, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 类型过滤器
        /// </summary>
        public FileTypeFilter TypeFilter
        {
            get => _typeFilter;
            set
            {
                if (SetProperty(ref _typeFilter, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 日期过滤器
        /// </summary>
        public DateRangeFilter DateFilter
        {
            get => _dateFilter;
            set
            {
                if (SetProperty(ref _dateFilter, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 大小过滤器
        /// </summary>
        public SizeRangeFilter SizeFilter
        {
            get => _sizeFilter;
            set
            {
                if (SetProperty(ref _sizeFilter, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 图片尺寸过滤器
        /// </summary>
        public ImageDimensionFilter ImageSizeFilter
        {
            get => _imageSizeFilter;
            set
            {
                if (SetProperty(ref _imageSizeFilter, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 音频时长过滤器
        /// </summary>
        public AudioDurationFilter DurationFilter
        {
            get => _durationFilter;
            set
            {
                if (SetProperty(ref _durationFilter, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 搜索模式
        /// </summary>
        public SearchMode SearchMode
        {
            get => _searchMode;
            set
            {
                if (SetProperty(ref _searchMode, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 路径范围过滤器
        /// </summary>
        public PathRangeFilter PathRange
        {
            get => _pathRange;
            set
            {
                if (SetProperty(ref _pathRange, value))
                {
                    NotifyOptionsChanged();
                }
            }
        }

        /// <summary>
        /// 目标面板 ID
        /// </summary>
        public string TargetPaneId
        {
            get => _targetPaneId;
            set => SetProperty(ref _targetPaneId, value);
        }

        #endregion

        #region 命令

        /// <summary>
        /// 执行搜索命令
        /// </summary>
        public ICommand SearchCommand { get; }

        /// <summary>
        /// 清空搜索命令
        /// </summary>
        public ICommand ClearSearchCommand { get; }

        /// <summary>
        /// 设置搜索范围预设命令（复杂逻辑，由 Coordinator 处理）
        /// </summary>
        public ICommand SetScopePresetCommand { get; }

        #endregion

        #region 公共方法

        /// <summary>
        /// 设置目标面板 ID
        /// </summary>
        public void SetTargetPane(string paneId)
        {
            _targetPaneId = paneId;
        }

        /// <summary>
        /// 重置所有过滤器到默认值
        /// </summary>
        public void ResetFilters()
        {
            _typeFilter = FileTypeFilter.All;
            _dateFilter = DateRangeFilter.All;
            _sizeFilter = SizeRangeFilter.All;
            _imageSizeFilter = ImageDimensionFilter.All;
            _durationFilter = AudioDurationFilter.All;

            // 通知所有属性变更
            OnPropertyChanged(nameof(TypeFilter));
            OnPropertyChanged(nameof(DateFilter));
            OnPropertyChanged(nameof(SizeFilter));
            OnPropertyChanged(nameof(ImageSizeFilter));
            OnPropertyChanged(nameof(DurationFilter));

            // 发布选项变更消息
            NotifyOptionsChanged();
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 执行搜索
        /// </summary>
        private void ExecuteSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return;
            }

            var options = BuildSearchOptions();

            // 发布执行搜索消息，由 SearchModule 处理
            _messageBus.Publish(new ExecuteSearchMessage(
                SearchText,
                SearchNames,
                SearchNotes,
                _targetPaneId,
                options
            ));
        }

        /// <summary>
        /// 通知搜索选项变更
        /// </summary>
        private void NotifyOptionsChanged()
        {
            var options = BuildSearchOptions();
            _messageBus.Publish(new SearchOptionsChangedMessage(options, _targetPaneId));
        }

        /// <summary>
        /// 构建搜索选项
        /// </summary>
        private SearchOptions BuildSearchOptions()
        {
            return new SearchOptions
            {
                Type = _typeFilter,
                DateRange = _dateFilter,
                SizeRange = _sizeFilter,
                ImageSize = _imageSizeFilter,
                Duration = _durationFilter,
                Mode = _searchMode,
                SearchNames = _searchNames,
                SearchNotes = _searchNotes,
                SearchFolders = _searchFolders,
                PathRange = _pathRange
            };
        }

        #endregion
    }
}
