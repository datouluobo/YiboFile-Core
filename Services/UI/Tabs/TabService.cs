using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.Services.Core;
using System.Windows.Input;
using YiboFile.ViewModels;


namespace YiboFile.Services.Tabs
{
    public partial class TabService
    {
        private static readonly List<TabService> _allInstances = new List<TabService>();
        private readonly ObservableCollection<PathTab> _tabs = new ObservableCollection<PathTab>();
        private PathTab _activeTab;
        private AppConfig _config;
        private TabUiContext _ui;

        public event EventHandler<PathTab> TabAdded;
        public event EventHandler<PathTab> TabRemoved;
        public event EventHandler<PathTab> ActiveTabChanged;
        public event EventHandler<PathTab> TabPinStateChanged;
        public event EventHandler<PathTab> TabTitleChanged;

        public TabService() { }

        public TabService(AppConfig config)
        {
            _config = config;
            lock (_allInstances) { _allInstances.Add(this); }
        }

        ~TabService()
        {
            lock (_allInstances) { _allInstances.Remove(this); }
        }

        public void UpdateConfig(AppConfig config)
        {
            _config = config;
        }

        public PathTab ActiveTab => _activeTab;
        public IReadOnlyList<PathTab> Tabs => _tabs;
        public int TabCount => _tabs.Count;
        public ICommand NewTabCommand => new RelayCommand(() => CreateBlankTab());
        public ICommand UpdateTabWidthsCommand => new RelayCommand<double>(width =>
        {
            _widthCalculator?.UpdateTabWidths(width, _tabs);
        });
        private TabWidthCalculator _widthCalculator;

        private void AddTab(PathTab tab)
        {
            _tabs.Add(tab);
            TabAdded?.Invoke(this, tab);
        }

        public string GetEffectiveTitle(PathTab tab)
        {
            if (tab == null) return string.Empty;
            if (!string.IsNullOrEmpty(tab.OverrideTitle)) return tab.OverrideTitle;
            return tab.Title;
        }

        public string GetTabKey(PathTab tab)
        {
            return tab?.Path ?? string.Empty;
        }

        public PathTab FindTabByPath(string path)
        {
            return _tabs.FirstOrDefault(t => string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase));
        }

        public PathTab FindTabByLibraryId(int libraryId)
        {
            return _tabs.FirstOrDefault(t => t.Type == TabType.Library && t.Library?.Id == libraryId);
        }

        public PathTab FindRecentTab(Func<PathTab, bool> predicate, TimeSpan timeWindow)
        {
            return _tabs.Where(predicate).OrderByDescending(t => t.LastAccessTime).FirstOrDefault();
        }

        public List<PathTab> GetTabsInOrder()
        {
            return new List<PathTab>(_tabs);
        }

        private bool _isSettingActiveTab = false;
        public void SetActiveTab(PathTab tab)
        {
            if (_activeTab == tab || _isSettingActiveTab) return;
            try
            {
                _isSettingActiveTab = true;
                _activeTab = tab;
                ActiveTabChanged?.Invoke(this, tab);
            }
            finally
            {
                _isSettingActiveTab = false;
            }
        }

        public void RemoveTab(PathTab tab)
        {
            if (_tabs.Contains(tab))
            {
                _tabs.Remove(tab);
                TabRemoved?.Invoke(this, tab);
            }
        }

        private bool _isUpdatingPath = false;
        public void UpdateActiveTabPath(string newPath)
        {
            if (_activeTab != null && _activeTab.Type == TabType.Path && !_isUpdatingPath)
            {
                try
                {
                    _isUpdatingPath = true;
                    _activeTab.Path = newPath;
                    UpdateTabTitle(_activeTab, newPath);
                }
                finally
                {
                    _isUpdatingPath = false;
                }
            }
        }

        public void UpdateTabTitle(PathTab tab, string newPath)
        {
            if (tab == null) return;
            var newTitle = CalculateTabDisplayTitle(newPath);
            tab.Title = newTitle;
            TabTitleChanged?.Invoke(this, tab);
        }

        public void TogglePinTab(PathTab tab)
        {
            if (tab == null) return;
            tab.IsPinned = !tab.IsPinned;
            var key = GetTabKey(tab);
            if (_config.PinnedTabs == null) _config.PinnedTabs = new List<string>();
            if (tab.IsPinned)
            {
                if (!_config.PinnedTabs.Contains(key)) _config.PinnedTabs.Insert(0, key);
            }
            else
            {
                _config.PinnedTabs.Remove(key);
            }
            ConfigurationService.Instance.Set(cfg => cfg.PinnedTabs, _config.PinnedTabs);
            TabPinStateChanged?.Invoke(this, tab);
        }

        public void SetTabOverrideTitle(PathTab tab, string overrideTitle)
        {
            if (tab == null) return;
            var key = GetTabKey(tab);
            if (string.IsNullOrWhiteSpace(overrideTitle))
            {
                tab.OverrideTitle = null;
                if (_config.TabTitleOverrides != null) _config.TabTitleOverrides.Remove(key);
            }
            else
            {
                tab.OverrideTitle = overrideTitle;
                if (_config.TabTitleOverrides == null) _config.TabTitleOverrides = new Dictionary<string, string>();
                _config.TabTitleOverrides[key] = overrideTitle;
            }
            ConfigurationService.Instance.Set(cfg => cfg.TabTitleOverrides, _config.TabTitleOverrides);
            TabTitleChanged?.Invoke(this, tab);
        }

        public bool CanCloseTab(PathTab tab, bool isLibraryMode) => true;

        public void ApplyTabOverrides(PathTab tab)
        {
            if (tab == null) return;
            var key = GetTabKey(tab);
            if (_config.TabTitleOverrides != null && _config.TabTitleOverrides.TryGetValue(key, out var ot) && !string.IsNullOrWhiteSpace(ot))
            {
                tab.OverrideTitle = ot;
            }
            if (_config.PinnedTabs != null && _config.PinnedTabs.Contains(key)) tab.IsPinned = true;
        }

        public double GetPinnedTabWidth() => _config.PinnedTabWidth > 0 ? _config.PinnedTabWidth : 120;

        public string CalculateTabDisplayTitle(string path)
        {
            if (string.IsNullOrEmpty(path)) return "新标签页";
            try { return System.IO.Path.GetFileName(path) ?? path; } catch { return path; }
        }

        public bool ValidatePath(string path, out string errorMessage)
        {
            errorMessage = string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return true;
            if (path.StartsWith("search://") || path.StartsWith("tag://") || path.StartsWith("lib://") || path.StartsWith("content://")) return true;
            if (!System.IO.Directory.Exists(path) && !System.IO.File.Exists(path))
            {
                errorMessage = "路径不存在";
                return false;
            }
            return true;
        }
    }
}
