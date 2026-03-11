using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Config;
using YiboFile.Services.Features;

namespace YiboFile.ViewModels.Settings
{
    public class SearchSettingsViewModel : BaseViewModel
    {
        private readonly IFullTextSearchService _ftsService;
        private readonly IConfigurationService _configService;

        private bool _isEnableFullTextSearch;
        public bool IsEnableFullTextSearch
        {
            get => _isEnableFullTextSearch;
            set
            {
                if (SetProperty(ref _isEnableFullTextSearch, value))
                {
                    _configService.Update(c => c.IsEnableFullTextSearch = value);
                    if (value && _ftsService != null)
                        _ftsService.StartBackgroundIndexing();
                }
            }
        }

        private bool _autoExpandHistory;
        public bool AutoExpandHistory
        {
            get => _autoExpandHistory;
            set
            {
                if (SetProperty(ref _autoExpandHistory, value))
                    _configService.Update(c => c.AutoExpandHistory = value);
            }
        }

        private int _historyMaxCount;
        private string _historyMaxCountInput;
        public int HistoryMaxCount
        {
            get => _historyMaxCount;
            set
            {
                value = Math.Clamp(value, 0, 10000);
                if (SetProperty(ref _historyMaxCount, value))
                {
                    _historyMaxCountInput = null;
                    OnPropertyChanged(nameof(HistoryMaxCountInput));
                    _configService.Update(c => c.HistoryMaxCount = value);
                }
                else
                {
                    _historyMaxCountInput = null;
                    OnPropertyChanged(nameof(HistoryMaxCountInput));
                }
            }
        }

        public string HistoryMaxCountInput
        {
            get => _historyMaxCountInput ?? _historyMaxCount.ToString();
            set => SetProtectedNumber(ref _historyMaxCountInput, ref _historyMaxCount, value, 0, 10000, v => HistoryMaxCount = v);
        }

        private string _indexLocation;
        public string IndexLocation
        {
            get => _indexLocation;
            set => SetProperty(ref _indexLocation, value);
        }

        private int _indexedFileCount;
        public int IndexedFileCount
        {
            get => _indexedFileCount;
            set => SetProperty(ref _indexedFileCount, value);
        }

        private ObservableCollection<string> _indexScopes;
        public ObservableCollection<string> IndexScopes
        {
            get => _indexScopes;
            set => SetProperty(ref _indexScopes, value);
        }

        private double _indexingProgress;
        public double IndexingProgress
        {
            get => _indexingProgress;
            set => SetProperty(ref _indexingProgress, value);
        }

        private string _indexingStatusText;
        public string IndexingStatusText
        {
            get => _indexingStatusText;
            set => SetProperty(ref _indexingStatusText, value);
        }

        private bool _isIndexing;
        public bool IsIndexing
        {
            get => _isIndexing;
            set => SetProperty(ref _isIndexing, value);
        }

        public ICommand RebuildIndexCommand { get; }
        public ICommand ClearHistoryCommand { get; }

        public SearchSettingsViewModel(IConfigurationService configService)
        {
            _configService = configService;
            _ftsService = App.ServiceProvider.GetService<IFullTextSearchService>();
            RebuildIndexCommand = new RelayCommand(RebuildIndex);
            ClearHistoryCommand = new RelayCommand(ClearHistory);

            LoadFromConfig();
        }

        ~SearchSettingsViewModel()
        {
            if (_ftsService != null)
            {
                _ftsService.ProgressChanged -= OnIndexingProgressChanged;
            }
        }

        public void LoadFromConfig()
        {
            InitializeSearchSettings(_configService.GetSnapshot());
        }

        private void InitializeSearchSettings(AppConfig config)
        {
            _isEnableFullTextSearch = config.IsEnableFullTextSearch;
            _autoExpandHistory = config.AutoExpandHistory;
            _historyMaxCount = config.HistoryMaxCount;

            IndexScopes = new ObservableCollection<string>(config.FullTextIndexPaths ?? new List<string>());

            IndexLocation = config.FullTextIndexDbPath;
            IndexedFileCount = _ftsService?.IndexedFileCount ?? 0;
            IndexingStatusText = "就绪";

            if (_ftsService != null)
            {
                _ftsService.ProgressChanged -= OnIndexingProgressChanged;
                _ftsService.ProgressChanged += OnIndexingProgressChanged;
            }
        }

        private void OnIndexingProgressChanged(object sender, IndexingProgressEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (e.IsCompleted)
                {
                    IndexingStatusText = $"索引完成: {e.TotalFiles} 个文件";
                    IsIndexing = false;
                    IndexingProgress = 100;
                    RefreshIndexStats();
                }
                else
                {
                    IndexingStatusText = $"正在索引: {e.ProcessedFiles}/{e.TotalFiles}";
                    IsIndexing = true;
                    if (e.TotalFiles > 0)
                    {
                        IndexingProgress = (double)e.ProcessedFiles / e.TotalFiles * 100;
                    }
                    else
                    {
                        IndexingProgress = 0;
                    }
                }
            });
        }

        public void RefreshIndexStats()
        {
            Task.Run(() =>
            {
                try
                {
                    int count = _ftsService?.IndexedFileCount ?? 0;
                    string path = _ftsService?.IndexDbPath;
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        IndexedFileCount = count;
                        if (!string.IsNullOrEmpty(path)) IndexLocation = path;
                    });
                }
                catch { }
            });
        }

        public void UpdateIndexScopes(IEnumerable<string> scopes)
        {
            IndexScopes = new ObservableCollection<string>(scopes);
            _configService.Update(c => c.FullTextIndexPaths = scopes.ToList());
        }

        public void UpdateIndexLocation(string newPath)
        {
            _configService.Update(c => c.FullTextIndexDbPath = newPath);
            IndexLocation = newPath;
        }

        public void StartBackgroundIndexing()
        {
            _ftsService?.StartBackgroundIndexing();
        }

        private async void RebuildIndex()
        {
            IsIndexing = true;
            IndexingStatusText = "正在清理...";
            IndexingProgress = 0;

            try
            {
                await Task.Run(async () =>
                {
                    if (_ftsService == null) return;
                    _ftsService.ClearIndex();

                    var config = _configService.GetSnapshot();
                    IEnumerable<string> scanPaths = config.FullTextIndexPaths;

                    if (scanPaths == null || !scanPaths.Any())
                    {
                        var libRepo = App.ServiceProvider.GetRequiredService<YiboFile.Services.Data.Repositories.ILibraryRepository>();
                        var libraries = libRepo.GetAllLibraries();
                        scanPaths = libraries?.SelectMany(l => l.Paths ?? Enumerable.Empty<string>()) ?? Enumerable.Empty<string>();
                    }

                    foreach (var path in scanPaths)
                    {
                        if (System.IO.Directory.Exists(path))
                        {
                            await _ftsService.StartIndexingAsync(path, recursive: true);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                IndexingStatusText = $"重建失败: {ex.Message}";
                IsIndexing = false;
            }
        }

        private void ClearHistory()
        {
            YiboFile.Services.Search.SearchHistoryService.Instance.Clear();
        }
    }
}
