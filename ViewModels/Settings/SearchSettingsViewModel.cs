using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Config;
using YiboFile.Services.FullTextSearch;

namespace YiboFile.ViewModels.Settings
{
    public class SearchSettingsViewModel : BaseViewModel
    {
        private bool _isEnableFullTextSearch;
        public bool IsEnableFullTextSearch
        {
            get => _isEnableFullTextSearch;
            set
            {
                if (SetProperty(ref _isEnableFullTextSearch, value))
                {
                    ConfigurationService.Instance.Update(c => c.IsEnableFullTextSearch = value);
                    if (value && FullTextSearchService.Instance != null)
                        FullTextSearchService.Instance.StartBackgroundIndexing();
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
                    ConfigurationService.Instance.Update(c => c.AutoExpandHistory = value);
            }
        }

        private int _historyMaxCount;
        public int HistoryMaxCount
        {
            get => _historyMaxCount;
            set
            {
                if (SetProperty(ref _historyMaxCount, value))
                    ConfigurationService.Instance.Update(c => c.HistoryMaxCount = value);
            }
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

        public SearchSettingsViewModel()
        {
            RebuildIndexCommand = new RelayCommand(RebuildIndex);
            ClearHistoryCommand = new RelayCommand(ClearHistory);

            LoadFromConfig();
        }

        ~SearchSettingsViewModel()
        {
            if (FullTextSearchService.Instance?.IndexingService != null)
            {
                FullTextSearchService.Instance.IndexingService.ProgressChanged -= OnIndexingProgressChanged;
            }
        }

        public void LoadFromConfig()
        {
            InitializeSearchSettings(ConfigurationService.Instance.GetSnapshot());
        }

        private void InitializeSearchSettings(AppConfig config)
        {
            _isEnableFullTextSearch = config.IsEnableFullTextSearch;
            _autoExpandHistory = config.AutoExpandHistory;
            _historyMaxCount = config.HistoryMaxCount;

            IndexScopes = new ObservableCollection<string>(config.FullTextIndexPaths ?? new List<string>());

            IndexLocation = config.FullTextIndexDbPath;
            IndexedFileCount = FullTextSearchService.Instance.IndexedFileCount;
            IndexingStatusText = "就绪";

            if (FullTextSearchService.Instance.IndexingService != null)
            {
                FullTextSearchService.Instance.IndexingService.ProgressChanged -= OnIndexingProgressChanged;
                FullTextSearchService.Instance.IndexingService.ProgressChanged += OnIndexingProgressChanged;
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
                    int count = FullTextSearchService.Instance.IndexedFileCount;
                    string path = FullTextSearchService.Instance.IndexDbPath;
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
            ConfigurationService.Instance.Update(c => c.FullTextIndexPaths = scopes.ToList());
        }

        public void UpdateIndexLocation(string newPath)
        {
            ConfigurationService.Instance.Update(c => c.FullTextIndexDbPath = newPath);
            IndexLocation = newPath;
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
                    FullTextSearchService.Instance.ClearIndex();

                    var config = ConfigurationService.Instance.GetSnapshot();
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
                            await FullTextSearchService.Instance.IndexingService.StartIndexingAsync(path, recursive: true);
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
