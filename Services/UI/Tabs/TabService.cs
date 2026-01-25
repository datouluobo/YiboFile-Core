using System;
using System.Collections.Generic;
using System.Linq;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.Services.Core;

namespace YiboFile.Services.Tabs
{
    /// <summary>
    /// 标签页管理服务
    /// 负责标签页的业务逻辑和状态管理
    /// </summary>
    public partial class TabService
    {
        private static readonly List<TabService> _allInstances = new List<TabService>();
        private readonly List<PathTab> _tabs = new List<PathTab>();
        private PathTab _activeTab;
        private AppConfig _config;
        private TabUiContext _ui;
        // Drag fields moved to .DragDrop.cs, but partial class shares them. 
        // We declare them in DragDrop.cs (Wait, C# partial classes share fields? 
        // Yes allowing access, but where are they declared? 
        // If I put them in DragDrop.cs with 'private', they are visible to other partials.
        // So I don't need to redeclare them here.)

        // However, _tabs, _activeTab, _config, _ui are used everywhere. I declare them here.

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

        public int TabCount => _tabs.Count;
        public PathTab ActiveTab => _activeTab;
        public IReadOnlyList<PathTab> Tabs => _tabs;
        private TabWidthCalculator _widthCalculator;

        private void AddTab(PathTab tab)
        {
            _tabs.Add(tab);
            TabAdded?.Invoke(this, tab);
        }

        public void AttachUiContext(TabUiContext context)
        {
            _ui = context;
            if (_ui?.GetConfig != null)
            {
                _config = _ui.GetConfig();
            }
            _widthCalculator = new TabWidthCalculator(_config, GetTabKey, GetPinnedTabWidth);
            InitializeTabsDragDrop();
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

        public void SetActiveTab(PathTab tab)
        {
            if (_activeTab == tab) return;
            _activeTab = tab;
            ActiveTabChanged?.Invoke(this, tab);
        }

        public void RemoveTab(PathTab tab)
        {
            if (_tabs.Contains(tab))
            {
                _tabs.Remove(tab);
                TabRemoved?.Invoke(this, tab);
            }
        }

        private bool CanCloseTab(PathTab tab, bool isLibraryMode)
        {
            return true;
        }

        public void UpdateActiveTabPath(string newPath)
        {
            if (_activeTab != null && _activeTab.Type == TabType.Path)
            {
                _activeTab.Path = newPath;
                UpdateTabTitle(_activeTab, newPath);
            }
        }

        public void UpdateTabTitle(PathTab tab, string newPath)
        {
            if (tab == null) return;
            var newTitle = GetPathDisplayTitle(newPath);
            tab.Title = newTitle;
            if (tab.TitleTextBlock != null)
            {
                tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
            }
            if (tab.TabButton != null)
            {
                tab.TabButton.ToolTip = GetEffectiveTitle(tab);
            }
            TabTitleChanged?.Invoke(this, tab);
        }

        #region 配置应用

        public void ApplyTabOverrides(PathTab tab)
        {
            if (tab == null) return;

            var key = GetTabKey(tab);

            if (_config.TabTitleOverrides != null &&
                _config.TabTitleOverrides.TryGetValue(key, out var overrideTitle) &&
                !string.IsNullOrWhiteSpace(overrideTitle))
            {
                tab.OverrideTitle = overrideTitle;
            }

            if (_config.PinnedTabs != null && _config.PinnedTabs.Contains(key))
            {
                tab.IsPinned = true;
            }
        }

        public void TogglePinTab(PathTab tab)
        {
            if (tab == null) return;

            tab.IsPinned = !tab.IsPinned;
            var key = GetTabKey(tab);

            if (_config.PinnedTabs == null)
                _config.PinnedTabs = new List<string>();

            if (tab.IsPinned)
            {
                if (!_config.PinnedTabs.Contains(key))
                    _config.PinnedTabs.Insert(0, key);
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
                if (_config.TabTitleOverrides != null)
                    _config.TabTitleOverrides.Remove(key);
            }
            else
            {
                tab.OverrideTitle = overrideTitle;
                if (_config.TabTitleOverrides == null)
                    _config.TabTitleOverrides = new Dictionary<string, string>();
                _config.TabTitleOverrides[key] = overrideTitle;
            }

            ConfigurationService.Instance.Set(cfg => cfg.TabTitleOverrides, _config.TabTitleOverrides);
            TabTitleChanged?.Invoke(this, tab);
        }

        public double GetPinnedTabWidth()
        {
            return _config.PinnedTabWidth > 0 ? _config.PinnedTabWidth : 120;
        }

        #endregion
    }
}
