using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YiboFile.Services.Config;

namespace YiboFile.Services.Search
{
    public enum HistoryType
    {
        LocalPath,
        Search,
        FullTextSearch,
        Library,
        Tag
    }

    public class HistoryItem
    {
        public HistoryType Type { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }

        [JsonIgnore]
        public string DisplayPath => Content;

        [JsonIgnore]
        public string DisplayType => Type switch
        {
            HistoryType.LocalPath => "位置",
            HistoryType.Search => "搜索",
            HistoryType.FullTextSearch => "全文",
            HistoryType.Library => "库",
            HistoryType.Tag => "标签",
            _ => ""
        };

        [JsonIgnore]
        public string IconKey => Type switch
        {
            HistoryType.LocalPath => "Icon_Folder",
            HistoryType.Search => "Icon_Search",
            HistoryType.FullTextSearch => "Icon_File",
            HistoryType.Library => "Icon_Nav_Library",
            HistoryType.Tag => "Icon_Nav_Tag",
            _ => "Icon_Folder"
        };

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
        // 兼容旧代码的静态访问入口
        public static SearchHistoryService Instance =>
            YiboFile.App.ServiceProvider?.GetService<SearchHistoryService>() ?? new SearchHistoryService(null);

        private List<HistoryItem> _historyItems;
        private readonly string _historyFilePath;
        private const string HISTORY_FILE_NAME = "yibofile_history.json"; 

        // 支持DI注入
        public SearchHistoryService(IConfigPathProvider pathProvider)
        {
            if (pathProvider == null)
            {
                pathProvider = new ConfigPathProvider();
            }
            
            _historyFilePath = pathProvider.HistoryFilePath;

            _historyItems = new List<HistoryItem>();
            LoadHistory();

            // 数据迁移：如果由于文件更名导致加载失败，尝试从旧文件名加载
            if (_historyItems.Count == 0 && _historyFilePath.EndsWith(HISTORY_FILE_NAME))
            {
                string oldPath = Path.Combine(Path.GetDirectoryName(_historyFilePath), "search_history.json");
                if (File.Exists(oldPath))
                {
                    try
                    {
                        string json = File.ReadAllText(oldPath);
                        _historyItems = JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? new List<HistoryItem>();
                        if (_historyItems.Count > 0)
                        {
                            SaveHistory(); // 迁移到新文件
                            // File.Move(oldPath, oldPath + ".bak", true); // 可选：备份或删除旧文件
                        }
                    }
                    catch { }
                }
            }
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

