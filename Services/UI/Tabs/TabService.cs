using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using YiboFile.Controls;
using YiboFile.Services.Search;
using YiboFile.Services.Config;
using YiboFile;
using System.Text.Json;

namespace YiboFile.Services.Tabs
{
    /// <summary>
    /// 标签页服务的 UI 上下文，用于解耦 MainWindow 状态与 TabService 逻辑
    /// </summary>
    public class TabUiContext
    {
        public FileBrowserControl FileBrowser { get; init; }
        public TabManagerControl TabManager { get; init; }
        public Dispatcher Dispatcher { get; init; }
        public Window OwnerWindow { get; init; }
        public Func<AppConfig> GetConfig { get; init; }
        public Action<AppConfig> SaveConfig { get; init; }
        public Func<Library> GetCurrentLibrary { get; init; }
        public Action<Library> SetCurrentLibrary { get; init; }
        public Func<string> GetCurrentPath { get; init; }
        public Action<string> SetCurrentPath { get; init; }
        public Action<string> SetNavigationCurrentPath { get; init; }
        public Action<Library> LoadLibraryFiles { get; init; }
        public Action<string> NavigateToPathInternal { get; init; }
        public Action UpdateNavigationButtonsState { get; init; }
        public SearchService SearchService { get; init; }
        public Func<SearchCacheService> GetSearchCacheService { get; init; }
        public Func<SearchOptions> GetSearchOptions { get; init; }
        public Func<List<FileSystemItem>> GetCurrentFiles { get; init; }
        public Action<List<FileSystemItem>> SetCurrentFiles { get; init; }
        public Action ClearFilter { get; init; }
        public Func<string, Task> RefreshSearchTab { get; init; }
        public Func<string, object> FindResource { get; init; }

        /// <summary>
        /// 获取当前导航模式（"Path", "Library"）
        /// </summary>
        public Func<string> GetCurrentNavigationMode { get; init; }
    }

    /// <summary>
    /// 标签页管理服务
    /// 负责标签页的业务逻辑和状态管理
    /// </summary>
    public class TabService
    {
        #region 事件定义

        /// <summary>
        /// 标签页已添加事件
        /// </summary>
        public event EventHandler<PathTab> TabAdded;

        /// <summary>
        /// 标签页已移除事件
        /// </summary>
        public event EventHandler<PathTab> TabRemoved;

        /// <summary>
        /// 活动标签页已变更事件
        /// </summary>
        public event EventHandler<PathTab> ActiveTabChanged;

        /// <summary>
        /// 标签页固定状态已变更事件
        /// </summary>
        public event EventHandler<PathTab> TabPinStateChanged;

        /// <summary>
        /// 标签页标题已变更事件
        /// </summary>
        public event EventHandler<PathTab> TabTitleChanged;

        #endregion

        #region 私有字段

        private readonly List<PathTab> _tabs = new List<PathTab>();
        private PathTab _activeTab = null;
        private AppConfig _config;
        private TabUiContext _ui;
        private Point _tabDragStartPoint;
        private PathTab _draggingTab = null;
        private bool _isDragging = false; // 标记是否真的在进行拖拽操作
        private TabWidthCalculator _widthCalculator;

        #endregion

        #region 属性

        /// <summary>
        /// 所有标签页（只读）
        /// </summary>
        public IReadOnlyList<PathTab> Tabs => _tabs.ToList();

        /// <summary>
        /// 当前活动标签页
        /// </summary>
        public PathTab ActiveTab => _activeTab;

        /// <summary>
        /// 标签页数量
        /// </summary>
        public int TabCount => _tabs.Count;

        #endregion

        #region 构造函数

        public TabService(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _widthCalculator = new TabWidthCalculator(
                _config,
                tab => GetEffectiveTitle(tab),
                () => GetPinnedTabWidth()
            );
        }

        #endregion

        #region 配置管理

        /// <summary>
        /// 更新配置
        /// </summary>
        public void UpdateConfig(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _widthCalculator = new TabWidthCalculator(
                _config,
                tab => GetEffectiveTitle(tab),
                () => GetPinnedTabWidth()
            );

            // Trigger tab width recalculation with new config
            UpdateTabWidths();
        }

        #endregion

        #region UI 上下文

        /// <summary>
        /// 注入 UI 上下文，供 TabService 驱动界面元素与宿主状态
        /// </summary>
        public void AttachUiContext(TabUiContext context)
        {
            _ui = context ?? throw new ArgumentNullException(nameof(context));

            // 监听配置变更以实时更新标签页宽度
            ConfigurationService.Instance.SettingChanged -= OnConfigurationChanged; // 防止重复订阅
            ConfigurationService.Instance.SettingChanged += OnConfigurationChanged;
        }

        private void OnConfigurationChanged(object sender, string settingName)
        {
            if (settingName == nameof(AppConfig.PinnedTabWidth) ||
                settingName == nameof(AppConfig.TabWidthMode) ||
                settingName == nameof(AppConfig.TagBoxWidth))
            {
                // 在UI线程更新标签页宽度
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (_ui != null)
                    {
                        // 更新内部配置引用
                        _config = ConfigurationService.Instance.GetSnapshot();
                        UpdateTabWidths();
                    }
                });
            }
        }

        private void EnsureUi()
        {
            if (_ui == null)
            {
                throw new InvalidOperationException("TabUiContext is not attached. Call AttachUiContext before using UI-related methods.");
            }
        }

        #endregion

        #region 标签页查找

        /// <summary>
        /// 根据标识符查找标签页
        /// </summary>
        public PathTab FindTabByIdentifier(TabType type, string identifier)
        {
            switch (type)
            {
                case TabType.Path:
                    return _tabs.FirstOrDefault(t => t.Type == TabType.Path && t.Path == identifier);
                case TabType.Library:
                    return _tabs.FirstOrDefault(t => t.Type == TabType.Library &&
                        (t.Library?.Name == identifier || t.Path == identifier));
                // Tag feature removed - Phase 2
                // case TabType.Tag:
                //     if (int.TryParse(identifier, out int tagId))
                //     {
                //         return _tabs.FirstOrDefault(t => t.Type == TabType.Tag && t.TagId == tagId);
                //     }
                //     return _tabs.FirstOrDefault(t => t.Type == TabType.Tag && t.TagName == identifier);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 根据库ID查找标签页
        /// </summary>
        public PathTab FindTabByLibraryId(int libraryId)
        {
            return _tabs.FirstOrDefault(t => t.Type == TabType.Library && t.Library != null && t.Library.Id == libraryId);
        }

        /// <summary>
        /// 根据标签ID查找标签页
        /// </summary>
        public PathTab FindTabByTagId(int tagId)
        {
            // Tag feature removed - Phase 2
            // return _tabs.FirstOrDefault(t => t.Type == TabType.Tag && t.TagId == tagId);
            return null;
        }

        /// <summary>
        /// 根据路径查找标签页
        /// </summary>
        public PathTab FindTabByPath(string path)
        {
            return _tabs.FirstOrDefault(t => t.Type == TabType.Path && t.Path == path);
        }

        /// <summary>
        /// 智能查找最近访问的标签页
        /// 如果只有一个匹配的标签页，总是返回（唯一匹配）
        /// 如果有多个匹配的标签页，根据配置和时间窗口判断
        /// </summary>
        private PathTab FindRecentTab(Func<PathTab, bool> predicate, TimeSpan timeWindow)
        {
            var matchingTabs = _tabs.Where(predicate).ToList();
            if (matchingTabs.Count == 0) return null;

            // 唯一匹配：总是返回（不管时间窗口）
            if (matchingTabs.Count == 1)
            {
                return matchingTabs[0];
            }

            // 配置选项：从不复用
            var config = _config;
            if (config?.NeverReuseTab == true)
            {
                return null;
            }

            // 配置选项：总是复用（返回第一个）
            if (config?.AlwaysReuseTab == true)
            {
                return matchingTabs[0];
            }

            // 多个匹配的标签页，检查是否有最近访问的
            var now = DateTime.Now;
            var recentTab = matchingTabs.FirstOrDefault(t => now - t.LastAccessTime < timeWindow);
            return recentTab;
        }

        /// <summary>
        /// 查找最近访问的Path标签页（公共方法供MainWindow使用）
        /// </summary>
        public PathTab FindRecentPathTab(string path, TimeSpan? timeWindow = null)
        {
            var window = timeWindow ?? TimeSpan.FromSeconds(_config?.ReuseTabTimeWindow ?? 10);
            return FindRecentTab(t => t.Type == TabType.Path && t.Path == path, window);
        }

        #endregion

        #region 标签页管理

        /// <summary>
        /// 添加标签页
        /// </summary>
        public void AddTab(PathTab tab)
        {
            if (tab == null) return;
            if (_tabs.Contains(tab)) return;

            _tabs.Add(tab);
            ApplyTabOverrides(tab);
            TabAdded?.Invoke(this, tab);
        }

        /// <summary>
        /// 移除标签页
        /// </summary>
        public bool RemoveTab(PathTab tab)
        {
            if (tab == null) return false;

            bool removed = _tabs.Remove(tab);
            if (removed)
            {
                if (tab == _activeTab)
                {
                    _activeTab = null;
                    if (_tabs.Count > 0)
                    {
                        _activeTab = _tabs.First();
                    }
                    ActiveTabChanged?.Invoke(this, _activeTab);
                }
                TabRemoved?.Invoke(this, tab);
            }
            return removed;
        }

        /// <summary>
        /// 设置活动标签页
        /// </summary>
        public void SetActiveTab(PathTab tab)
        {
            if (tab != null && !_tabs.Contains(tab)) return;

            if (_activeTab != tab)
            {
                _activeTab = tab;
                ActiveTabChanged?.Invoke(this, _activeTab);
            }
        }

        /// <summary>
        /// 更新当前活动标签页的路径和标题
        /// </summary>
        public void UpdateActiveTabPath(string newPath)
        {
            if (_activeTab != null && _activeTab.Type == TabType.Path)
            {
                // 如果路径相同也可能需要刷新标题（例如重命名或显示名称不符）
                // if (_activeTab.Path == newPath) return;

                _activeTab.Path = newPath;
                _activeTab.Title = GetPathDisplayTitle(newPath);

                if (_activeTab.TitleTextBlock != null)
                {
                    _activeTab.TitleTextBlock.Text = _activeTab.Title;
                }

                if (_activeTab.TabButton != null)
                {
                    _activeTab.TabButton.ToolTip = newPath;
                }

                if (_activeTab.IconTextBlock != null)
                {
                    _activeTab.IconTextBlock.Text = GetTabTypePrefix(_activeTab);
                }

                TabTitleChanged?.Invoke(this, _activeTab);
            }
        }

        /// <summary>
        /// 判断是否可以关闭标签页
        /// </summary>
        public bool CanCloseTab(PathTab tab, bool isLibraryMode)
        {
            if (tab == null) return false;
            // 在库模式下，如果关闭的是最后一个标签页，不阻止关闭（会重新加载库）
            // 在路径模式下，至少保留一个标签页
            if (!isLibraryMode && _tabs.Count <= 1) return false;
            return true;
        }

        /// <summary>
        /// 获取排序后的标签页列表（固定标签在前，按配置顺序）
        /// </summary>
        public List<PathTab> GetTabsInOrder()
        {
            var pinned = _tabs.Where(t => t.IsPinned).ToList();
            var unpinned = _tabs.Where(t => !t.IsPinned).ToList();
            var ordered = new List<PathTab>();

            if (_config.PinnedTabs != null && _config.PinnedTabs.Count > 0)
            {
                // 按配置中的顺序排列固定标签
                foreach (var key in _config.PinnedTabs)
                {
                    var found = pinned.FirstOrDefault(t => GetTabKey(t) == key);
                    if (found != null) ordered.Add(found);
                }
                // 添加其他固定标签（不在配置中的）
                foreach (var t in pinned)
                {
                    if (!ordered.Contains(t)) ordered.Add(t);
                }
            }
            else
            {
                ordered.AddRange(pinned);
            }

            ordered.AddRange(unpinned);
            return ordered;
        }

        #endregion

        #region 标签页键值和标题

        /// <summary>
        /// 获取标签页的键值（用于配置存储）
        /// </summary>
        public string GetTabKey(PathTab tab)
        {
            if (tab == null) return string.Empty;

            switch (tab.Type)
            {
                case TabType.Path:
                    return "path:" + (tab.Path ?? string.Empty);
                case TabType.Library:
                    return "library:" + (tab.Library?.Id.ToString() ?? "");
                // case TabType.Tag: // Phase 2
                //     return "tag:" + tab.TagId.ToString();
                default:
                    return "unknown:" + (tab.Title ?? "");
            }
        }

        /// <summary>
        /// 获取有效标题（考虑覆盖标题）
        /// </summary>
        public string GetEffectiveTitle(PathTab tab)
        {
            if (tab == null) return string.Empty;
            var title = string.IsNullOrWhiteSpace(tab.OverrideTitle) ? tab.Title : tab.OverrideTitle;

            // Simplify drive root display (e.g. "C: (Windows)" -> "C:")
            if (tab.Type == TabType.Path && !string.IsNullOrEmpty(title) && title.Length > 3)
            {
                // Check if it's a drive root format like "C: (...)"
                if (title[1] == ':' && (title.Contains(" (") || title.Contains(" (")))
                {
                    // Double-check if it starts with drive letter
                    if (char.IsLetter(title[0]))
                    {
                        return title.Substring(0, 2);
                    }
                }
            }
            return title;
        }

        /// <summary>
        /// 获取路径的显示标题（处理驱动器根目录）
        /// </summary>
        public string GetPathDisplayTitle(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            // 规范化路径（移除末尾的反斜杠，但保留驱动器根目录的形式）
            string normalizedPath = path.TrimEnd('\\');
            if (string.IsNullOrEmpty(normalizedPath)) normalizedPath = path;

            // 检查是否是驱动器根目录（如 C:\ 或 F:\）
            string rootPath = Path.GetPathRoot(path);
            if (rootPath == path || rootPath.TrimEnd('\\') == normalizedPath)
            {
                // 是驱动器根目录，尝试获取卷标
                try
                {
                    var driveInfo = new DriveInfo(rootPath);
                    if (driveInfo.IsReady && !string.IsNullOrEmpty(driveInfo.VolumeLabel))
                    {
                        return $"{driveInfo.Name.TrimEnd('\\')} ({driveInfo.VolumeLabel})";
                    }
                    else
                    {
                        return driveInfo.Name.TrimEnd('\\');
                    }
                }
                catch
                {
                    // 如果获取失败，返回路径本身（去掉末尾反斜杠）
                    return rootPath.TrimEnd('\\');
                }
            }

            // 普通路径，使用文件名
            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
            {
                // 如果 GetFileName 返回空，可能路径本身有问题，返回路径
                return path;
            }
            return fileName;
        }

        #endregion

        #region 配置应用

        /// <summary>
        /// 应用标签页配置覆盖（标题覆盖、固定状态）
        /// </summary>
        public void ApplyTabOverrides(PathTab tab)
        {
            if (tab == null) return;

            var key = GetTabKey(tab);

            // 应用标题覆盖
            if (_config.TabTitleOverrides != null &&
                _config.TabTitleOverrides.TryGetValue(key, out var overrideTitle) &&
                !string.IsNullOrWhiteSpace(overrideTitle))
            {
                tab.OverrideTitle = overrideTitle;
            }

            // 应用固定状态
            if (_config.PinnedTabs != null && _config.PinnedTabs.Contains(key))
            {
                tab.IsPinned = true;
            }
        }

        /// <summary>
        /// 切换标签页固定状态
        /// </summary>
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

            // 使用ConfigurationService保存固定标签配置
            ConfigurationService.Instance.Set(cfg => cfg.PinnedTabs, _config.PinnedTabs);
            TabPinStateChanged?.Invoke(this, tab);
        }

        /// <summary>
        /// 设置标签页标题覆盖
        /// </summary>
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

            // 使用ConfigurationService保存标题覆盖配置
            ConfigurationService.Instance.Set(cfg => cfg.TabTitleOverrides, _config.TabTitleOverrides);
            TabTitleChanged?.Invoke(this, tab);
        }

        /// <summary>
        /// 获取固定标签页宽度
        /// </summary>
        public double GetPinnedTabWidth()
        {
            return _config.PinnedTabWidth > 0 ? _config.PinnedTabWidth : 120;
        }

        #endregion

        #region 路径验证

        /// <summary>
        /// 验证路径是否存在且可访问
        /// </summary>
        public bool ValidatePath(string path, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(path))
            {
                errorMessage = "路径不能为空";
                return false;
            }

            // 搜索标签页的路径格式是 "search://keyword" 或 "content://keyword"，不需要验证目录存在性
            if (path.StartsWith("search://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                if (!Directory.Exists(path))
                {
                    errorMessage = $"路径不存在: {path}";
                    return false;
                }
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                errorMessage = $"无法访问路径: {path}\n\n{ex.Message}";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = $"无法访问路径: {path}\n\n{ex.Message}";
                return false;
            }
        }

        #endregion

        #region 库模式标签页管理

        /// <summary>
        /// 获取库模式下的有效路径列表
        /// </summary>
        public List<string> GetValidLibraryPaths(Library library)
        {
            if (library == null || library.Paths == null || library.Paths.Count == 0)
                return new List<string>();

            return library.Paths.Where(p => Directory.Exists(p)).ToList();
        }

        /// <summary>
        /// 获取需要移除的标签页（不属于指定路径列表的路径标签页）
        /// </summary>
        public List<PathTab> GetTabsToRemoveForLibrary(List<string> validPaths)
        {
            return _tabs.Where(tab => tab.Type == TabType.Path && !validPaths.Contains(tab.Path)).ToList();
        }

        /// <summary>
        /// 获取库模式下应该激活的标签页
        /// </summary>
        public PathTab GetTabToActivateForLibrary(List<string> validPaths)
        {
            if (validPaths == null || validPaths.Count == 0) return null;

            // 如果当前活动标签页属于库路径，保持活动
            if (_activeTab != null && _activeTab.Type == TabType.Path && validPaths.Contains(_activeTab.Path))
            {
                return _activeTab;
            }

            // 查找第一个属于库路径的标签页
            var firstTab = _tabs.FirstOrDefault(t => t.Type == TabType.Path && validPaths.Contains(t.Path));
            if (firstTab != null) return firstTab;

            // 如果没有，返回第一个标签页
            return _tabs.FirstOrDefault();
        }

        #endregion

        #region 拖拽排序

        /// <summary>
        /// 更新标签页拖拽后的顺序
        /// </summary>
        public void UpdateTabOrderAfterDrag(PathTab draggedTab, int targetIndex, int pinnedCount)
        {
            if (draggedTab == null || !_tabs.Contains(draggedTab)) return;

            var pinned = _tabs.Where(t => t.IsPinned).ToList();
            var unpinned = _tabs.Where(t => !t.IsPinned).ToList();

            if (draggedTab.IsPinned)
            {
                pinned.Remove(draggedTab);
                targetIndex = Math.Min(targetIndex, pinnedCount);
                pinned.Insert(targetIndex, draggedTab);
                _config.PinnedTabs = pinned.Select(t => GetTabKey(t)).ToList();

                // 使用ConfigurationService保存拖拽后的固定标签顺序
                ConfigurationService.Instance.Set(cfg => cfg.PinnedTabs, _config.PinnedTabs);

                _tabs.Clear();
                _tabs.AddRange(pinned.Concat(unpinned));
            }
            else
            {
                int unTarget = Math.Max(0, targetIndex - pinnedCount);
                int unCurrent = unpinned.IndexOf(draggedTab);
                if (unCurrent == -1) return;
                unpinned.Remove(draggedTab);
                if (unTarget > unpinned.Count) unTarget = unpinned.Count;
                unpinned.Insert(unTarget, draggedTab);
                _tabs.Clear();
                _tabs.AddRange(pinned.Concat(unpinned));
            }
        }

        #endregion

        #region 标签页 UI 操作

        public void InitializeTabsDragDrop()
        {
            EnsureUi();
            try
            {
                var panel = _ui.TabManager?.TabsPanelControl;
                if (panel == null) return;
                panel.AllowDrop = true;
                panel.DragOver -= TabsPanel_DragOver;
                panel.Drop -= TabsPanel_Drop;
                panel.DragOver += TabsPanel_DragOver;
                panel.Drop += TabsPanel_Drop;
            }
            catch { }
        }

        public void CreatePathTab(string path, bool forceNewTab = false, bool skipValidation = false, bool activate = true)
        {
            EnsureUi();
            EnsureUi();
            // 移除 IsLoaded 检查，确保初始化时也能创建标签页
            if (_ui.TabManager?.TabsPanelControl == null) return;

            if (!skipValidation && !ValidatePath(path, out string errorMessage))
            {
                MessageBox.Show(errorMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!forceNewTab)
            {
                var existingTab = FindTabByPath(path);
                if (existingTab != null)
                {
                    if (activate) SwitchToTab(existingTab);
                    return;
                }
            }

            var newTab = new PathTab
            {
                Type = TabType.Path,
                Path = path,
                Title = GetPathDisplayTitle(path)
            };


            CreateTabInternal(newTab, activate);
        }

        /// <summary>
        /// 创建空白标签页(默认打开桌面路径)
        /// </summary>
        public PathTab CreateBlankTab()
        {
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            CreatePathTab(desktopPath, forceNewTab: true);
            return ActiveTab as PathTab;
        }

        public void OpenLibraryTab(Library library, bool forceNewTab = false, bool activate = true)
        {
            EnsureUi();
            if (library == null) return;
            // 1. 强制创建新标签页（中键/Ctrl+左键）
            if (forceNewTab)
            {
                var tab = new PathTab
                {
                    Type = TabType.Library,
                    Path = library.Name,
                    Title = library.Name,
                    Library = library
                };
                CreateTabInternal(tab, activate);
                return;
            }

            // 2. 优先查找：是否已存在该Library的标签页
            var window = TimeSpan.FromSeconds(_config?.ReuseTabTimeWindow ?? 10);
            var recentTab = FindRecentTab(
                t => t.Type == TabType.Library && t.Library?.Id == library.Id,
                window
            );

            if (recentTab != null)
            {
                // 找到了标签页，切换到它
                if (activate) SwitchToTab(recentTab);
                return;
            }

            // 3. 导航行为：在Library模式下且当前是Library标签页 → 更新当前标签页
            var currentMode = _ui?.GetCurrentNavigationMode?.Invoke() ?? "Path";
            if (currentMode == "Library" && _activeTab != null && _activeTab.Type == TabType.Library)
            {
                _activeTab.Library = library;
                _activeTab.Path = library.Name;
                _activeTab.Title = library.Name;
                if (_activeTab.TitleTextBlock != null) _activeTab.TitleTextBlock.Text = library.Name;
                if (_activeTab.TabButton != null) _activeTab.TabButton.ToolTip = library.Name;
                if (activate) SwitchToTab(_activeTab);
                return;
            }

            // 4. 其他情况：创建新标签页
            var newTab = new PathTab
            {
                Type = TabType.Library,
                Path = library.Name,
                Title = library.Name,
                Library = library
            };

            CreateTabInternal(newTab, activate);
        }

        // OpenTagTab removed - Phase 2
        // public void OpenTagTab(YiboFile.Tag tag, bool forceNewTab = false, bool activate = true) { ... }

        public void SwitchToTab(PathTab tab)
        {
            EnsureUi();
            if (tab == null) return;
            var beforeCount = (_ui.FileBrowser?.FilesItemsSource as System.Collections.IList)?.Count ?? 0;
            // 更新最后访问时间
            tab.LastAccessTime = DateTime.Now;

            SetActiveTab(tab);
            UpdateTabStyles();

            // 清空文件列表，防止显示上一个标签页的内容
            // 各个分支的加载方法会重新设置文件列表
            if (_ui.FileBrowser != null)
            {
                _ui.FileBrowser.FilesItemsSource = null;
                _ui.GetCurrentFiles?.Invoke()?.Clear(); // 清空 _currentFiles
            }

            if (tab.Type == TabType.Library)
            {
                if (tab.Library != null)
                {
                    _ui.SetCurrentLibrary?.Invoke(tab.Library);
                    _ui.SetCurrentPath?.Invoke(null);
                    var cfg = _ui.GetConfig?.Invoke();
                    if (cfg != null)
                    {
                        // 使用ConfigurationService保存最后访问的库ID
                        ConfigurationService.Instance.Set(c => c.LastLibraryId, tab.Library.Id);
                    }
                    if (_ui.FileBrowser != null)
                    {
                        _ui.FileBrowser.NavUpEnabled = false;
                        _ui.FileBrowser.IsAddressReadOnly = false;  // 允许在库标签页中进行搜索
                    }
                    _ui.LoadLibraryFiles?.Invoke(tab.Library);
                }
                return;
            }

            // Tag tab logic removed - Phase 2
            // if (tab.Type == TabType.Tag) { ... }

            _ui.SetCurrentLibrary?.Invoke(null);

            if (tab.Path != null && (tab.Path.StartsWith("search://", StringComparison.OrdinalIgnoreCase) ||
                                     tab.Path.StartsWith("content://", StringComparison.OrdinalIgnoreCase)))
            {
                // 从路径提取关键词并规范化（确保即使路径被污染也能正确处理）
                string rawKeyword;
                if (tab.Path.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
                    rawKeyword = tab.Path.Substring("search://".Length);
                else
                    rawKeyword = tab.Path.Substring("content://".Length); // handle content://
                var normalizedKeyword = SearchService.NormalizeKeyword(rawKeyword);
                _ui.SetCurrentPath?.Invoke(null);
                if (_ui.FileBrowser != null)
                {
                    // 使用规范化关键词设置地址栏和面包屑，确保显示一致
                    _ui.FileBrowser.AddressText = normalizedKeyword;
                    _ui.FileBrowser.IsAddressReadOnly = false;
                    _ui.FileBrowser.SetSearchBreadcrumb(normalizedKeyword);
                    _ui.FileBrowser.NavUpEnabled = false;
                }
                // 从缓存恢复搜索结果
                _ = _ui.RefreshSearchTab?.Invoke(tab.Path);
                return;
            }

            try
            {
                if (!Directory.Exists(tab.Path))
                {
                    MessageBox.Show($"路径不存在: {tab.Path}\n\n标签页将被关闭。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    CloseTab(tab);
                    return;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"无法访问路径: {tab.Path}\n\n{ex.Message}\n\n标签页将被关闭。", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                CloseTab(tab);
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法访问路径: {tab.Path}\n\n{ex.Message}\n\n标签页将被关闭。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                CloseTab(tab);
                return;
            }

            _ui.SetCurrentPath?.Invoke(tab.Path);
            _ui.SetNavigationCurrentPath?.Invoke(tab.Path);
            try
            {
                _ui.NavigateToPathInternal?.Invoke(tab.Path);
                if (_ui.FileBrowser != null) _ui.FileBrowser.NavUpEnabled = true;
                _ui.UpdateNavigationButtonsState?.Invoke();
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"无法加载路径: {tab.Path}\n\n{ex.Message}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法加载路径: {tab.Path}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void SetupLibraryTabs(Library library)
        {
            EnsureUi();
            if (library == null || library.Paths == null || library.Paths.Count == 0) return;
            if (_ui.TabManager == null || _ui.TabManager.TabsPanelControl == null) return;

            var validPaths = GetValidLibraryPaths(library);
            if (validPaths.Count == 0) return;

            var tabsToRemove = GetTabsToRemoveForLibrary(validPaths);
            foreach (var tab in tabsToRemove)
            {
                CloseTab(tab);
            }

            foreach (var path in validPaths)
            {
                try
                {
                    var existingTab = FindTabByPath(path);
                    if (existingTab == null)
                    {
                        CreatePathTab(path, activate: false);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"创建库路径标签页失败 {path}: {ex.Message}");
                }
            }

            var tabToActivate = GetTabToActivateForLibrary(validPaths);
            if (tabToActivate != null)
            {
                SwitchToTab(tabToActivate);
            }
        }

        public void ClearTabsInLibraryMode()
        {
            EnsureUi();
            if (_ui.TabManager == null || _ui.TabManager.TabsPanelControl == null) return;

            var tabsToRemove = _tabs.ToList();
            foreach (var tab in tabsToRemove)
            {
                CloseTab(tab);
            }

            if (TabCount == 0)
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktopPath))
                {
                    CreatePathTab(desktopPath);
                }
            }
        }

        public void CloseTab(PathTab tab)
        {
            EnsureUi();
            if (tab == null || tab.TabButton == null) return;
            if (!CanCloseTab(tab, _ui.GetCurrentLibrary?.Invoke() != null)) return;
            if (_ui.TabManager == null || _ui.TabManager.TabsPanelControl == null) return;

            bool wasActive = (tab == _activeTab);

            RemoveTab(tab);

            var container = tab.TabButton.Parent as StackPanel;
            if (container != null)
            {
                container.Children.Clear();
                _ui.TabManager.TabsPanelControl.Children.Remove(container);
                _ui.TabManager.EnsureNewTabButtonLast();
                _ui.TabManager.TabsPanelControl.UpdateLayout();
                _ui.TabManager.TabsBorderControl?.UpdateLayout();
            }

            tab.TabButton = null;
            tab.CloseButton = null;

            // 如果关闭的是活动标签页，切换到剩余的第一个标签页
            if (wasActive)
            {
                var remainingTabs = GetTabsInOrder();
                if (remainingTabs.Count > 0)
                {
                    SwitchToTab(remainingTabs[0]);
                }
                else
                {
                    // 没有剩余标签页，创建默认标签页
                    var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    if (Directory.Exists(desktopPath))
                    {
                        CreatePathTab(desktopPath);
                    }
                }
            }
            else
            {
                UpdateTabStyles();
                UpdateTabWidths(); // Recompress after tab removal
            }
        }

        public void UpdateTabTitle(PathTab tab, string path)
        {
            if (tab == null) return;
            tab.Title = GetPathDisplayTitle(path);
            if (tab.TitleTextBlock != null)
            {
                tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
            }
        }

        public void ApplyPinVisual(PathTab tab)
        {
            if (tab == null || tab.TabButton == null || tab.TitleTextBlock == null) return;
            var effectiveTitle = GetEffectiveTitle(tab);
            if (string.IsNullOrWhiteSpace(effectiveTitle) && !string.IsNullOrWhiteSpace(tab.Path))
            {
                effectiveTitle = GetPathDisplayTitle(tab.Path);
            }
            if (tab.IsPinned)
            {
                tab.TabButton.Width = double.NaN;
                tab.TabButton.MinWidth = GetPinnedTabWidth();
                tab.TitleTextBlock.Text = "📌 " + effectiveTitle;
                tab.TabButton.ToolTip = effectiveTitle;
            }
            else
            {
                // Width is controlled by UpdateTabWidths() for unpinned tabs
                tab.TitleTextBlock.Text = effectiveTitle;
                tab.TabButton.ToolTip = effectiveTitle;
                tab.TabButton.MinWidth = 0;
            }
        }

        public void ReorderTabs()
        {
            EnsureUi();
            if (_ui.TabManager == null || _ui.TabManager.TabsPanelControl == null) return;
            var ordered = GetTabsInOrder();
            _ui.TabManager.TabsPanelControl.Children.Clear();
            foreach (var t in ordered)
            {
                if (t.TabContainer != null)
                {
                    _ui.TabManager.TabsPanelControl.Children.Add(t.TabContainer);
                }
            }
            // 确保+按钮在最后
            _ui.TabManager.EnsureNewTabButtonLast();
            _ui.TabManager.TabsPanelControl.UpdateLayout();
            _ui.TabManager.TabsBorderControl?.UpdateLayout();
        }

        public void RenameDisplayTitle(PathTab tab)
        {
            EnsureUi();
            try
            {
                var dlg = new PathInputDialog("请输入新的显示标题：")
                {
                    Owner = _ui.OwnerWindow,
                    InputText = GetEffectiveTitle(tab)
                };
                if (dlg.ShowDialog() == true)
                {
                    var newTitle = dlg.InputText?.Trim() ?? string.Empty;
                    SetTabOverrideTitle(tab, newTitle);
                    ApplyPinVisual(tab);
                    if (tab.TitleTextBlock != null) tab.TitleTextBlock.Text = GetEffectiveTitle(tab);
                }
            }
            catch { }
        }

        public void UpdateTabStyles()
        {
            EnsureUi();
            var activeTab = _activeTab;
            foreach (var tab in _tabs)
            {
                if (tab.TabButton != null)
                {
                    if (_ui.FindResource != null)
                    {
                        tab.TabButton.Style = (Style)_ui.FindResource(tab == activeTab ? "ActiveTabButtonStyle" : "TabButtonStyle");
                    }

                    if (tab.CloseButton is Border border && border.Child is TextBlock closeButtonText)
                    {
                        if (tab == activeTab)
                        {
                            closeButtonText.Foreground = Brushes.White;
                        }
                        else
                        {
                            closeButtonText.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 根据标签页类型和路径获取类型图标
        /// </summary>
        private string GetTabTypePrefix(PathTab tab)
        {
            if (tab.Type == TabType.Path)
            {
                if (!string.IsNullOrEmpty(tab.Path))
                {
                    if (tab.Path.StartsWith("search://", StringComparison.OrdinalIgnoreCase)) return "🔍";
                    if (tab.Path.StartsWith("content://", StringComparison.OrdinalIgnoreCase)) return "📝";
                    if (tab.Path.StartsWith("lib://")) return "📚";

                }
                return "📁";
            }
            else if (tab.Type == TabType.Library) return "📚";


            return "📁";
        }

        /// <summary>
        /// 根据类型前缀获取 Badge 颜色（图标模式不使用）
        /// </summary>
        private (SolidColorBrush bg, SolidColorBrush fg) GetTabTypeBadgeColors(string prefix)
        {
            // 图标模式下不需要背景色，返回透明
            return (Brushes.Transparent, Brushes.Black);
        }

        /// <summary>
        /// 创建类型标识图标（无背景，纯图标）
        /// </summary>
        private TextBlock CreateTypeIcon(PathTab tab)
        {
            string icon = GetTabTypePrefix(tab);

            var iconText = new TextBlock
            {
                Text = icon,
                FontSize = 14,
                FontFamily = new FontFamily("Segoe UI Emoji, Segoe UI Symbol"),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Opacity = 1.0  // 默认显示
            };

            return iconText;
        }

        private void CreateTabInternal(PathTab tab, bool activate = true)
        {
            EnsureUi();
            if (_ui.TabManager == null || _ui.TabManager.TabsPanelControl == null) return;

            var tabContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 2, 0)
            };

            // 创建类型图标
            var typeIcon = CreateTypeIcon(tab);

            var titleText = new TextBlock
            {
                Text = tab.Title,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Left,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var closeButtonText = new TextBlock
            {
                Text = "×",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI Symbol"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Tag = tab,
                Opacity = 0.0,  // 默认隐藏
                Cursor = Cursors.Hand
            };

            var closeButton = new Border
            {
                Width = 24,
                Height = 24,
                Background = Brushes.Transparent,
                Tag = tab,
                Cursor = Cursors.Hand,
                Child = closeButtonText,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0)
            };

            closeButton.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (s is Border border && border.Tag is PathTab tabToClose)
                {
                    CloseTab(tabToClose);
                }
            };

            closeButton.MouseEnter += (s, e) =>
            {
                if (s is Border border && border.Child is TextBlock textBlock)
                {
                    textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x35, 0x45));
                }
            };

            closeButton.MouseLeave += (s, e) =>
            {
                if (s is Border border && border.Child is TextBlock textBlock)
                {
                    var tabToCheck = border.Tag as PathTab;
                    if (tabToCheck != null && tabToCheck == _activeTab)
                    {
                        textBlock.Foreground = Brushes.White;
                    }
                    else
                    {
                        textBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D));
                    }
                }
            };

            // 创建整合容器：图标和关闭按钮在同一位置
            var iconCloseContainer = new Grid
            {
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            iconCloseContainer.Children.Add(typeIcon);
            iconCloseContainer.Children.Add(closeButton);

            var buttonContent = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(iconCloseContainer, 0);
            Grid.SetColumn(titleText, 1);
            buttonContent.Children.Add(iconCloseContainer);
            buttonContent.Children.Add(titleText);

            var button = new Button
            {
                Content = buttonContent,
                Style = (Style)_ui.FindResource?.Invoke("TabButtonStyle"),
                Tag = tab,
                Margin = new Thickness(0)
            };

            // Allow clicking on the tab button while the container acts as a title bar
            System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button, true);

            button.PreviewMouseLeftButtonDown += (s, e) =>
            {
                // 检查是否点击在关闭按钮上
                if (e.OriginalSource is Border border && border.Tag == tab)
                {
                    // 点击在关闭按钮上，不处理，让关闭按钮自己处理
                    return;
                }
                _tabDragStartPoint = e.GetPosition(null);
                _draggingTab = tab;
                _isDragging = false; // 重置拖拽标志
                                     // 不在这里捕获鼠标，也不设置 e.Handled，让后续事件可以正常触发
                                     // 确保没有鼠标捕获
                if (button.IsMouseCaptured)
                {
                    button.ReleaseMouseCapture();
                }
            };
            // 使用 PreviewMouseMove 来检测拖拽，但只在真正拖拽时才设置 e.Handled
            button.PreviewMouseMove += (s, e) =>
            {
                if (_draggingTab == tab && e.LeftButton == MouseButtonState.Pressed)
                {
                    var pos = e.GetPosition(null);
                    var deltaX = Math.Abs(pos.X - _tabDragStartPoint.X);
                    var deltaY = Math.Abs(pos.Y - _tabDragStartPoint.Y);
                    // 只有移动超过阈值时才处理拖拽并阻止 Click 事件
                    if (deltaX > 4 || deltaY > 4)
                    {
                        // 标记为正在拖拽
                        _isDragging = true;
                        // 确定要拖拽时才捕获鼠标和处理事件
                        button.CaptureMouse();
                        var data = new DataObject();
                        data.SetData("YiboFile_TabKey", GetTabKey(tab));
                        data.SetData("YiboFile_TabPinned", tab.IsPinned);
                        DragDrop.DoDragDrop(button, data, DragDropEffects.Move);
                        if (button.IsMouseCaptured)
                        {
                            button.ReleaseMouseCapture();
                        }
                        // DoDragDrop完成后重置状态
                        _draggingTab = null;
                        _isDragging = false;
                        e.Handled = true; // 拖拽时阻止 Click 事件
                    }
                    // 如果没有移动超过阈值，不设置 e.Handled，让 Click 事件正常触发
                }
            };
            button.PreviewMouseLeftButtonUp += (s, e) =>
            {
                // 检查是否点击在关闭按钮上
                if (e.OriginalSource is Border border && border.Tag == tab)
                {
                    // 点击在关闭按钮上，不处理，让关闭按钮自己处理
                    _draggingTab = null;
                    _isDragging = false;
                    return;
                }

                // 只有在真正进行拖拽时才阻止点击处理
                bool shouldPreventClick = false;
                if (_draggingTab == tab && _isDragging)
                {
                    // 只有在PreviewMouseMove中确认是拖拽操作时才阻止点击
                    shouldPreventClick = true;
                }
                else if (_draggingTab == tab)
                {
                    // 正常点击，直接处理切换
                    var pos = e.GetPosition(null);
                    var deltaX = Math.Abs(pos.X - _tabDragStartPoint.X);
                    var deltaY = Math.Abs(pos.Y - _tabDragStartPoint.Y);

                    // 直接调用SwitchToTab，不再依赖Click事件
                    if (deltaX <= 4 && deltaY <= 4) // 确保是点击而不是拖拽
                    {
                        _ui.TabManager?.RaiseCloseOverlayRequested();
                        SwitchToTab(tab);
                        e.Handled = true; // 标记已处理，避免触发其他事件
                    }
                }

                if (shouldPreventClick)
                {
                    // 拖拽操作，阻止点击处理
                    e.Handled = true;
                }
                // 清除状态
                if (button.IsMouseCaptured)
                {
                    button.ReleaseMouseCapture();
                }
                _draggingTab = null;
                _isDragging = false;
            };

            var cm = new ContextMenu();
            var closeItem = new MenuItem { Header = "关闭标签页" };
            closeItem.Click += (s, e) => CloseTab(tab);
            var closeOthersItem = new MenuItem { Header = "关闭其他标签页" };
            closeOthersItem.Click += (s, e) => CloseOtherTabs(tab);
            var closeAllItem = new MenuItem { Header = "关闭全部标签页" };
            closeAllItem.Click += (s, e) => CloseAllTabs();
            var openInExplorerItem = new MenuItem { Header = "在资源管理器中打开" };
            openInExplorerItem.Click += (s, e) => OpenTabInExplorer(tab);
            var pinItem = new MenuItem { Header = "固定此标签页" };
            pinItem.Click += (s, e) => TogglePinTab(tab);
            var renameItem = new MenuItem { Header = "重命名显示标题" };
            renameItem.Click += (s, e) => RenameDisplayTitle(tab);
            cm.Items.Add(closeItem);
            cm.Items.Add(closeOthersItem);
            cm.Items.Add(closeAllItem);
            cm.Items.Add(new Separator());
            cm.Items.Add(openInExplorerItem);
            cm.Items.Add(new Separator());
            cm.Items.Add(pinItem);
            cm.Items.Add(renameItem);
            cm.Opened += (s, e) =>
            {
                pinItem.Header = tab.IsPinned ? "取消固定此标签页" : "固定此标签页";
                openInExplorerItem.IsEnabled = !string.IsNullOrWhiteSpace(GetTabOpenPath(tab));
            };
            button.ContextMenu = cm;

            button.PreviewMouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Middle)
                {
                    if (s is Button btn && btn.Tag is PathTab tabToClose)
                    {
                        CloseTab(tabToClose);
                        e.Handled = true;
                    }
                }
            };

            if (tab == _activeTab)
            {
                closeButtonText.Foreground = Brushes.White;
            }
            else
            {
                closeButtonText.Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x75, 0x7D));
            }

            var fadeInAnimation = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            var fadeOutAnimation = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            button.MouseEnter += (s, e) =>
            {
                // 图标淡出，关闭按钮淡入
                typeIcon.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };

            button.MouseLeave += (s, e) =>
            {
                var btn = button;
                var mousePos = Mouse.GetPosition(btn);
                if (mousePos.X < 0 || mousePos.Y < 0 || mousePos.X > btn.ActualWidth || mousePos.Y > btn.ActualHeight)
                {
                    // 图标淡入，关闭按钮淡出
                    typeIcon.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
                    closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                }
            };

            closeButton.MouseEnter += (s, e) =>
            {
                // 保持关闭按钮淡入状态
                closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };

            closeButton.MouseLeave += (s, e) =>
            {
                var btn = button;
                var mousePos = Mouse.GetPosition(btn);
                if (mousePos.X < 0 || mousePos.Y < 0 || mousePos.X > btn.ActualWidth || mousePos.Y > btn.ActualHeight)
                {
                    // 图标淡入，关闭按钮淡出
                    typeIcon.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
                    closeButtonText.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
                }
            };

            tabContainer.Children.Add(button);

            tab.CloseButton = closeButton;
            tab.TitleTextBlock = titleText;
            tab.IconTextBlock = typeIcon;
            tab.TabContainer = tabContainer;
            tab.TabButton = button;

            AddTab(tab);

            if (_ui.TabManager?.TabsPanelControl != null)
            {
                _ui.TabManager.TabsPanelControl.Children.Add(tabContainer);
                _ui.TabManager.EnsureNewTabButtonLast();
                // 确保拖拽功能已初始化
                InitializeTabsDragDrop();
            }

            ApplyTabOverrides(tab);
            ApplyPinVisual(tab);
            ReorderTabs();

            // Update tab widths for compression
            _ui.Dispatcher?.InvokeAsync(() =>
            {
                UpdateTabWidths();

                // Scroll to right end if overflowing
                var border = _ui.TabManager?.TabsBorderControl;
                var scrollViewer = FindScrollViewer(border);
                if (scrollViewer != null && scrollViewer.ExtentWidth > scrollViewer.ViewportWidth)
                {
                    scrollViewer.ScrollToRightEnd();
                }
            }, DispatcherPriority.Loaded);

            if (activate) SwitchToTab(tab);
        }

        private void TabsPanel_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("YiboFile_TabKey"))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // Allow Copy if Ctrl is pressed, otherwise Move
            if ((e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey)
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.Move;
            }

            e.Handled = true;
        }

        private void TabsPanel_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent("YiboFile_TabKey")) return;
                var key = e.Data.GetData("YiboFile_TabKey") as string;
                if (string.IsNullOrEmpty(key) || _ui.TabManager?.TabsPanelControl == null) return;

                var panel = _ui.TabManager.TabsPanelControl;
                var mousePos = e.GetPosition(panel);
                var children = panel.Children.OfType<StackPanel>().ToList();
                int childrenCount = children.Count;

                int targetIndex = 0;
                for (int i = 0; i < childrenCount; i++)
                {
                    var child = children[i] as FrameworkElement;
                    if (child == null) continue;
                    var pos = child.TransformToAncestor(panel).Transform(new Point(0, 0));
                    double mid = pos.X + child.ActualWidth / 2;
                    if (mousePos.X > mid) targetIndex = i + 1;
                }

                var tab = _tabs.FirstOrDefault(t => GetTabKey(t) == key);

                // 处理跨面板拖拽 (Inter-pane Drag & Drop)
                if (tab == null)
                {
                    if (_ui.OwnerWindow is MainWindow mainWindow)
                    {
                        // 确定源服务
                        TabService otherService = null;
                        if (this == mainWindow._tabService) otherService = mainWindow._secondTabService;
                        else if (this == mainWindow._secondTabService) otherService = mainWindow._tabService;

                        if (otherService != null)
                        {
                            var otherTab = otherService.Tabs.FirstOrDefault(t => otherService.GetTabKey(t) == key);
                            if (otherTab != null)
                            {
                                // Check for Copy vs Move (Ctrl key)
                                bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;

                                // 1. 如果不是复制模式，则从源服务移除
                                if (!isCopy)
                                {
                                    otherService.RemoveTab(otherTab);
                                }

                                // 2. 在当前服务创建新标签页
                                PathTab newTab = null;
                                if (otherTab.Type == TabType.Library && otherTab.Library != null)
                                {
                                    OpenLibraryTab(otherTab.Library, forceNewTab: true);
                                    newTab = ActiveTab;
                                }
                                else
                                {
                                    CreatePathTab(otherTab.Path, forceNewTab: true);
                                    newTab = ActiveTab;
                                }

                                if (newTab == null) return;

                                // 3. 恢复固定状态
                                bool isPinned = false;
                                if (e.Data.GetDataPresent("YiboFile_TabPinned"))
                                {
                                    isPinned = (bool)e.Data.GetData("YiboFile_TabPinned");
                                }
                                if (isPinned && !newTab.IsPinned)
                                {
                                    TogglePinTab(newTab);
                                }

                                // 4. 调整位置到 Drop 目标位
                                int pinnedCount = _tabs.Count(t => t.IsPinned);
                                if (newTab.IsPinned) targetIndex = Math.Min(targetIndex, pinnedCount); // 固定标签不能超过固定区
                                else targetIndex = Math.Max(targetIndex, pinnedCount); // 非固定标签不能进入固定区

                                UpdateTabOrderAfterDrag(newTab, targetIndex, pinnedCount);
                                ReorderTabs();
                                UpdateTabStyles();
                                return;
                            }
                        }
                    }
                    return;
                }

                // 面板内排序 (Reordering within same pane)
                int pinnedCountLocal = _tabs.Count(t => t.IsPinned);
                if (tab.IsPinned) targetIndex = Math.Min(targetIndex, pinnedCountLocal);
                else targetIndex = Math.Max(targetIndex, pinnedCountLocal);

                int currentIndex = children.IndexOf(tab.TabContainer);
                if (currentIndex == targetIndex) return;

                UpdateTabOrderAfterDrag(tab, targetIndex, pinnedCountLocal);

                ReorderTabs();
                UpdateTabStyles();
            }
            catch { }
        }


        #endregion

        #region 辅助操作

        private string GetTabOpenPath(PathTab tab)
        {
            if (tab == null) return null;
            if (tab.Type == TabType.Path)
            {
                return tab.Path;
            }

            if (tab.Type == TabType.Library && tab.Library != null)
            {
                var paths = GetValidLibraryPaths(tab.Library);
                return paths.FirstOrDefault();
            }

            return null;
        }

        public void OpenTabInExplorer(PathTab tab)
        {
            EnsureUi();
            var path = GetTabOpenPath(tab);
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开资源管理器: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void CloseOtherTabs(PathTab keepTab)
        {
            EnsureUi();
            var toClose = _tabs.Where(t => t != keepTab).ToList();
            foreach (var tab in toClose)
            {
                CloseTab(tab);
            }
        }

        public void CloseAllTabs()
        {
            EnsureUi();
            var toClose = _tabs.ToList();
            foreach (var tab in toClose)
            {
                CloseTab(tab);
            }
            if (TabCount == 0)
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (Directory.Exists(desktopPath))
                {
                    CreatePathTab(desktopPath);
                }
            }
        }

        #endregion

        #region Tab Width Compression

        /// <summary>
        /// Initialize size changed handler for tab compression
        /// </summary>
        public void InitializeTabSizeHandler()
        {
            EnsureUi();
            try
            {
                var border = _ui.TabManager?.TabsBorderControl;
                if (border != null)
                {
                    border.SizeChanged -= TabsBorder_SizeChanged;
                    border.SizeChanged += TabsBorder_SizeChanged;
                }
            }
            catch { }
        }

        private void TabsBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTabWidths();

            // Scroll to right end if overflowing
            var border = _ui.TabManager?.TabsBorderControl;
            var scrollViewer = FindScrollViewer(border);
            if (scrollViewer != null && scrollViewer.ExtentWidth > scrollViewer.ViewportWidth)
            {
                scrollViewer.ScrollToRightEnd();
            }
        }

        private ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer sv) return sv;
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Update tab widths based on available space (compression logic)
        /// </summary>
        private void UpdateTabWidths()
        {
            EnsureUi();
            if (_tabs.Count == 0) return;
            if (_ui.TabManager?.TabsBorderControl == null) return;

            var border = _ui.TabManager.TabsBorderControl;
            _widthCalculator.UpdateTabWidths(border, _tabs);
        }

        #endregion
    }
}






