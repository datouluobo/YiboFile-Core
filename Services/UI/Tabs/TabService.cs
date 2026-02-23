using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using YiboFile.Interfaces.Plugins;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.Services.Core;
using YiboFile.ViewModels.Messaging; // For IMessageBus
using YiboFile.ViewModels.Messaging.Messages;
using System.Windows.Input;
using YiboFile.ViewModels;


namespace YiboFile.Services.Tabs
{
    public partial class TabService
    {
        private static readonly List<TabService> _allInstances = new List<TabService>();
        private readonly ObservableCollection<PathTab> _tabs = new ObservableCollection<PathTab>();
        private readonly IMessageBus _messageBus;
        private PathTab _activeTab;
        private AppConfig _config;
        private TabUiContext _ui;

        public Services.Navigation.PaneId Pane { get; set; } = Services.Navigation.PaneId.Main;

        // public event EventHandler<PathTab> ActiveTabChanged; // Replaced by TabActiveChangedMessage
        // public event EventHandler<PathTab> TabPinStateChanged; // Replaced by TabPinStateChangedMessage
        // public event EventHandler<PathTab> TabTitleChanged; // Replaced by TabTitleChangedMessage
        // public event EventHandler<PathTab> TabAdded; // Replaced by TabAddedMessage
        // public event EventHandler<PathTab> TabRemoved; // Replaced by TabRemovedMessage

        public TabService(AppConfig config, IMessageBus messageBus)
        {
            _config = config;
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
            lock (_allInstances) { _allInstances.Add(this); }
        }

        public TabService(IMessageBus messageBus) // For DI without config initially
        {
            _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
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
            _messageBus.Publish(new TabAddedMessage(tab, Pane));
        }

        public string GetEffectiveTitle(PathTab tab)
        {
            if (tab == null) return string.Empty;
            if (!string.IsNullOrEmpty(tab.OverrideTitle)) return tab.OverrideTitle;
            return tab.Title;
        }

        public string GetTabKey(PathTab tab)
        {
            return tab?.Path != null ? NormalizePath(tab.Path) : string.Empty;
        }

        public PathTab FindTabByPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var normalizedTarget = NormalizePath(path);
            return _tabs.FirstOrDefault(t => NormalizePath(t.Path).Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase));
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

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            try
            {
                var trimmed = path.Trim();

                // Handle virtual paths (lib://, tag://, etc.) - Restore original behavior
                if (trimmed.StartsWith("lib://", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("tag://", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("search://", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.ToLowerInvariant();
                }

                // Handle file:// URI scheme
                if (trimmed.StartsWith("file:///", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(8);
                }
                else if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                {
                    trimmed = trimmed.Substring(7);
                }

                // Handle simple drive letter "C:" -> "C:\"
                if (trimmed.Length == 2 && trimmed[1] == ':' && char.IsLetter(trimmed[0]))
                {
                    return trimmed.ToUpperInvariant() + "\\";
                }

                // Unify separators
                trimmed = trimmed.Replace('/', '\\');

                // Trim trailing slash (unless it is a root drive "C:\")
                if (trimmed.Length > 3 && trimmed.EndsWith("\\"))
                {
                    trimmed = trimmed.Substring(0, trimmed.Length - 1);
                }

                // Normalize drive letter casing for root paths "c:\" -> "C:\"
                if (trimmed.Length == 3 && trimmed.EndsWith(":\\") && char.IsLetter(trimmed[0]))
                {
                    return trimmed.ToUpperInvariant();
                }

                // For longer paths, ensure drive letter is uppercase for consistency
                if (trimmed.Length > 3 && trimmed.Length >= 2 && trimmed[1] == ':' && char.IsLetter(trimmed[0]))
                {
                    return char.ToUpperInvariant(trimmed[0]) + trimmed.Substring(1);
                }

                return trimmed;
            }
            catch
            {
                return path?.Trim() ?? string.Empty;
            }
        }

        private bool _isSettingActiveTab = false;
        public void SetActiveTab(PathTab tab)
        {
            if (_activeTab == tab || _isSettingActiveTab) return;
            try
            {
                _isSettingActiveTab = true;

                // Keep the old active tab for reference if needed
                var oldTab = _activeTab;

                // Deactivate all other tabs (or just the old one for performance)
                // Iterating all ensures consistency if state got desynced
                foreach (var t in _tabs)
                {
                    if (t != tab && t.IsActive)
                    {
                        t.IsActive = false;
                    }
                }

                _activeTab = tab;

                if (_activeTab != null)
                {
                    _activeTab.IsActive = true;
                    _activeTab.LastAccessTime = DateTime.Now;
                }

                if (_activeTab != null || oldTab != null)
                {
                    _messageBus.Publish(new YiboFile.ViewModels.Messaging.Messages.TabActiveChangedMessage(_activeTab, Pane));
                }
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
                _messageBus.Publish(new TabRemovedMessage(tab, Pane));
            }
        }

        private bool _isUpdatingPath = false;
        public void UpdateActiveTabPath(string newPath)
        {
            if (_activeTab != null && !_isUpdatingPath)
            {
                // 如果是固定标签页，原则上不应通过普通导航改变其路径，但在内部跳转时保持同步
                try
                {
                    _isUpdatingPath = true;
                    _activeTab.Path = newPath;

                    // [关键修复] 根据新路径自动同步标签页类型，防止类型滞后导致的错误复用
                    if (newPath.StartsWith("tag://", StringComparison.OrdinalIgnoreCase))
                    {
                        _activeTab.Type = TabType.Tag;
                    }
                    else if (newPath.StartsWith("search://", StringComparison.OrdinalIgnoreCase) || newPath.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
                    {
                        _activeTab.Type = TabType.Search;
                    }
                    else if (newPath.StartsWith("lib://", StringComparison.OrdinalIgnoreCase))
                    {
                        _activeTab.Type = TabType.Library;
                    }
                    else
                    {
                        // 普通物理路径
                        _activeTab.Type = TabType.Path;
                    }

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
            tab.Title = newTitle;
            var path = tab.Title; // Actually NewTitle is derived from path, but logic is circular here. 
                                  // Correct logic: CalculateTabDisplayTitle uses newPath.
                                  // But we need old title for message? 
                                  // In typical event usage, we just invoke with the tab which HAS the new title.
                                  // Message definition: (PathTab Tab, string OldTitle, string NewTitle, PaneId Pane)
                                  // We don't easily have OldTitle here unless we capture it, but simple invocation is enough.
                                  // Let's pass null for OldTitle for now or improve later if strict diff needed.

            _messageBus.Publish(new YiboFile.ViewModels.Messaging.Messages.TabTitleChangedMessage(tab, null, newTitle, Pane));
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
            ConfigurationService.Instance.Set(cfg => cfg.PinnedTabs, _config.PinnedTabs);
            _messageBus.Publish(new YiboFile.ViewModels.Messaging.Messages.TabPinStateChangedMessage(tab, Pane));
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
            ConfigurationService.Instance.Set(cfg => cfg.TabTitleOverrides, _config.TabTitleOverrides);
            _messageBus.Publish(new YiboFile.ViewModels.Messaging.Messages.TabTitleChangedMessage(tab, null, overrideTitle, Pane));
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
            try
            {
                // 处理盘符根目录 (如 C:\), GetFileName 返回空
                if (path.Length <= 3 && path.EndsWith(":\\")) return path;

                var fileName = System.IO.Path.GetFileName(path);
                return string.IsNullOrEmpty(fileName) ? path : fileName;
            }
            catch { return path; }
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

        /// <summary>
        /// 查找指定内容类型的已打开标签页。
        /// </summary>
        public PathTab FindTabByContentTypeId(string contentTypeId)
        {
            if (string.IsNullOrEmpty(contentTypeId)) return null;
            return _tabs.FirstOrDefault(t => t.ContentTypeId == contentTypeId);
        }

        /// <summary>
        /// 创建特殊标签页（设置、关于、管理、任务队列等）。
        /// 如果 AllowMultiple=false 且已存在同类型标签，则激活已有标签而非创建新标签。
        /// </summary>
        /// <param name="contentTypeId">内容类型 ID（参见 TabContentTypes）</param>
        /// <param name="registry">TabContentRegistry 实例，用于解析 ITabContent</param>
        /// <param name="activate">是否立即激活，默认 true</param>
        /// <returns>创建或激活的标签页，失败时返回 null</returns>
        public PathTab CreateSpecialTab(string contentTypeId, TabContentRegistry registry, bool activate = true)
        {
            if (string.IsNullOrEmpty(contentTypeId) || registry == null)
                return null;

            // 1. 从 Registry 解析 ITabContent
            var content = registry.Resolve(contentTypeId);
            if (content == null)
            {
                FileLogger.Log($"TabService.CreateSpecialTab: Failed to resolve '{contentTypeId}'");
                return null;
            }

            // 2. AllowMultiple=false 时，查找已存在的同类型标签
            if (!content.AllowMultiple)
            {
                var existing = FindTabByContentTypeId(contentTypeId);
                if (existing != null)
                {
                    if (activate) SetActiveTab(existing);
                    return existing;
                }
            }

            // 3. 创建新标签页
            var tab = new PathTab
            {
                ContentTypeId = contentTypeId,
                Title = content.Title,
                IconKey = content.IconKey,
                Path = $"yibofile://{contentTypeId}",
                CustomContent = content,
            };

            // 设置关闭和选择命令（与普通标签页一致）
            tab.CloseCommand = new RelayCommand(() =>
            {
                content.OnClosed();
                RemoveTab(tab);
                // 如果关闭的是活动标签，切换到上一个标签
                if (_activeTab == null || _activeTab == tab)
                {
                    var next = _tabs.LastOrDefault();
                    if (next != null) SetActiveTab(next);
                }
            });
            tab.SelectCommand = new RelayCommand(() => SetActiveTab(tab));

            AddTab(tab);
            if (activate) SetActiveTab(tab);

            FileLogger.Log($"TabService.CreateSpecialTab: Created tab '{contentTypeId}' with title '{content.Title}'");
            return tab;
        }
    }
}
