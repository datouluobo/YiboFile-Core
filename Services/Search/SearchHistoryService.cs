using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using YiboFile.Services.Config;

namespace YiboFile.Services.Search
{
    public enum HistoryType
    {
        LocalPath,
        Search,
        FullTextSearch
    }

    public class HistoryItem
    {
        public HistoryType Type { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is HistoryItem other)
            {
                return Type == other.Type && Content == other.Content;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Type, Content);
        }
    }

    public class SearchHistoryService
    {
        private static SearchHistoryService _instance;
        public static SearchHistoryService Instance => _instance ??= new SearchHistoryService();

        private List<HistoryItem> _historyItems;
        private readonly string _historyFilePath;
        private const string HISTORY_FILE_NAME = "search_history.json";

        private SearchHistoryService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appFolder = Path.Combine(appData, "YiboFile");
            if (!Directory.Exists(appFolder))
            {
                Directory.CreateDirectory(appFolder);
            }
            _historyFilePath = Path.Combine(appFolder, HISTORY_FILE_NAME);
            _historyItems = new List<HistoryItem>();
            LoadHistory();
        }

        public void Add(string content, HistoryType type)
        {
            if (string.IsNullOrWhiteSpace(content)) return;

            var newItem = new HistoryItem
            {
                Type = type,
                Content = content.Trim(),
                Timestamp = DateTime.Now
            };

            // Remove existing duplicate
            _historyItems.RemoveAll(x => x.Type == type && x.Content.Equals(newItem.Content, StringComparison.OrdinalIgnoreCase));

            // Add to top
            _historyItems.Insert(0, newItem);

            // Trim to limit
            int maxCount = ConfigurationService.Instance.GetSnapshot().HistoryMaxCount;
            if (_historyItems.Count > maxCount)
            {
                _historyItems = _historyItems.Take(maxCount).ToList();
            }

            SaveHistory();
        }

        public List<HistoryItem> GetRecent()
        {
            return _historyItems.ToList(); // Return copy
        }

        public void Clear()
        {
            _historyItems.Clear();
            SaveHistory();
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    string json = File.ReadAllText(_historyFilePath);
                    _historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? new List<HistoryItem>();
                }
            }
            catch (Exception)
            {
                // Ignore load errors, start fresh
                _historyItems = new List<HistoryItem>();
            }
        }

        private void SaveHistory()
        {
            try
            {
                string json = JsonSerializer.Serialize(_historyItems);
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception)
            {
                // Ignore save errors
            }
        }
    }
}

