using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YiboFile.Services.Core;
using YiboFile.Services.Search;
using YiboFile.ViewModels.Messaging;
using YiboFile.ViewModels.Messaging.Messages;
using YiboFile.Models.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace YiboFile.Services.Navigation
{
    /// <summary>
    /// 导航服务
    /// 负责管理导航历史、路径导航等功能
    /// 支持多面板状态管理
    /// </summary>
    public class NavigationService
    {
        #region 私有类

        private class NavigationState
        {
            public List<string> History { get; } = new List<string>();
            public int CurrentIndex { get; set; } = -1;
            public string CurrentPath { get; set; }

            public IEnumerable<string> BackStack
            {
                get
                {
                    if (CurrentIndex <= 0) return Enumerable.Empty<string>();
                    return History.Take(CurrentIndex).Reverse();
                }
            }

            public IEnumerable<string> ForwardStack
            {
                get
                {
                    if (CurrentIndex >= History.Count - 1) return Enumerable.Empty<string>();
                    return History.Skip(CurrentIndex + 1);
                }
            }
        }

        #endregion

        #region 私有字段

        private readonly Dictionary<PaneId, NavigationState> _paneStates;
        private string _lastLeftNavSource;
        private readonly IMessageBus _messageBus;

        #endregion

        #region 公共属性

        /// <summary>
        /// UI 辅助接口
        /// </summary>
        public INavigationUIHelper UIHelper { get; set; }

        /// <summary>
        /// 当前路径 (默认主面板)
        /// </summary>
        public string CurrentPath
        {
            get => GetState(PaneId.Main).CurrentPath;
            set => NavigateTo(PaneId.Main, value);
        }

        /// <summary>
        /// 最后一个左侧导航来源
        /// </summary>
        public string LastLeftNavSource
        {
            get => _lastLeftNavSource;
            set => _lastLeftNavSource = value;
        }

        /// <summary>
        /// 是否可以后退 (默认主面板)
        /// </summary>
        public bool CanNavigateBack => CanNavigateBackFor(PaneId.Main);

        /// <summary>
        /// 是否可以前进 (默认主面板)
        /// </summary>
        public bool CanNavigateForward => CanNavigateForwardFor(PaneId.Main);

        /// <summary>
        /// 是否可以后退（别名，用于兼容）
        /// </summary>
        public bool CanGoBack => CanNavigateBack;

        /// <summary>
        /// 是否可以前进（别名，用于兼容）
        /// </summary>
        public bool CanGoForward => CanNavigateForward;

        /// <summary>
        /// 获取指定面板的当前路径
        /// </summary>
        public string GetCurrentPath(PaneId pane) => GetState(pane).CurrentPath;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化导航服务
        /// </summary>
        /// <param name="initialPath">初始路径</param>
        /// <param name="messageBus">消息总线</param>
        public NavigationService(string initialPath, IMessageBus messageBus = null)
        {
            _paneStates = new Dictionary<PaneId, NavigationState>
            {
                { PaneId.Main, new NavigationState() },
                { PaneId.Second, new NavigationState() }
            };

            var startPath = initialPath ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _lastLeftNavSource = string.Empty;
            _messageBus = messageBus ?? App.ServiceProvider?.GetService<IMessageBus>();

            // Initialize Main Pane
            GetState(PaneId.Main).CurrentPath = startPath;
            AddToHistory(PaneId.Main, startPath);

            // Initialize Second Pane (Same default path)
            GetState(PaneId.Second).CurrentPath = startPath;
            AddToHistory(PaneId.Second, startPath);
        }

        #endregion

        #region 导航方法

        public bool CanNavigateBackFor(PaneId pane)
        {
            var state = GetState(pane);
            return state.CurrentIndex > 0;
        }

        public bool CanNavigateForwardFor(PaneId pane)
        {
            var state = GetState(pane);
            return state.History.Count > 0 && state.CurrentIndex < state.History.Count - 1;
        }

        public IEnumerable<string> GetBackStack(PaneId pane) => GetState(pane).BackStack;
        public IEnumerable<string> GetForwardStack(PaneId pane) => GetState(pane).ForwardStack;

        public void SwitchNavigationMode(string mode)
        {
            // NavigationService 主要负责路径导航，模式切换由 NavigationModeService 处理
            // 但需要调用 UIHelper 来切换导航内容区域的可见性
            if (UIHelper != null)
            {
                UIHelper.SetNavigationContentVisibility(mode);
            }
        }

        /// <summary>
        /// 后退 (默认主面板)
        /// </summary>
        public string NavigateBack() => NavigateBack(PaneId.Main);

        /// <summary>
        /// 后退
        /// </summary>
        public string NavigateBack(PaneId pane)
        {
            if (CanNavigateBackFor(pane))
            {
                var state = GetState(pane);
                state.CurrentIndex--;
                var path = state.History[state.CurrentIndex];
                state.CurrentPath = path;

                PublishNavigationComplete(path, pane, NavigationSource.History);
                return path;
            }
            return null;
        }

        /// <summary>
        /// 前进 (默认主面板)
        /// </summary>
        public string NavigateForward() => NavigateForward(PaneId.Main);

        /// <summary>
        /// 前进
        /// </summary>
        public string NavigateForward(PaneId pane)
        {
            if (CanNavigateForwardFor(pane))
            {
                var state = GetState(pane);
                state.CurrentIndex++;
                var path = state.History[state.CurrentIndex];
                state.CurrentPath = path;

                PublishNavigationComplete(path, pane, NavigationSource.History);
                return path;
            }
            return null;
        }

        /// <summary>
        /// 向上导航 (默认主面板)
        /// </summary>
        public string NavigateUp() => NavigateUp(PaneId.Main);

        /// <summary>
        /// 向上导航
        /// </summary>
        public string NavigateUp(PaneId pane)
        {
            var state = GetState(pane);
            if (string.IsNullOrEmpty(state.CurrentPath))
                return null;

            var currentPath = state.CurrentPath;
            var protocolInfo = ProtocolManager.Parse(currentPath);

            // Only local file system paths and archives support "navigate up"
            if (protocolInfo.Type != ProtocolType.Local && protocolInfo.Type != ProtocolType.Archive)
            {
                return null;
            }

            if (protocolInfo.Type == ProtocolType.Archive)
            {
                try
                {
                    string archiveFile = protocolInfo.TargetPath;
                    string innerPath = protocolInfo.ExtraData;

                    innerPath = innerPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (!string.IsNullOrEmpty(innerPath))
                    {
                        int lastSlash = innerPath.LastIndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                        if (lastSlash >= 0)
                        {
                            string parentInner = innerPath.Substring(0, lastSlash);
                            string newUrl = $"{ProtocolManager.ZipProtocol}{archiveFile}|{parentInner}";
                            NavigateTo(pane, newUrl); // Will publish message
                            return newUrl;
                        }
                        else
                        {
                            string newUrl = $"{ProtocolManager.ZipProtocol}{archiveFile}|";
                            NavigateTo(pane, newUrl);
                            return newUrl;
                        }
                    }

                    if (string.IsNullOrEmpty(innerPath))
                    {
                        string parentDir = Directory.GetParent(archiveFile)?.FullName;
                        if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                        {
                            NavigateTo(pane, parentDir);
                            return parentDir;
                        }
                    }
                }
                catch
                {
                    // Fallback
                }
            }

            try
            {
                var parentPath = Directory.GetParent(currentPath)?.FullName;
                if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
                {
                    NavigateTo(pane, parentPath);
                    return parentPath;
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// 导航到指定路径 (默认主面板)
        /// </summary>
        public void NavigateTo(string path) => NavigateTo(PaneId.Main, path);

        /// <summary>
        /// 导航到指定路径
        /// </summary>
        /// <param name="pane">目标面板</param>
        /// <param name="path">路径</param>
        /// <param name="addToHistory">是否添加到历史</param>
        public void NavigateTo(PaneId pane, string path, bool addToHistory = true)
        {
            if (string.IsNullOrEmpty(path)) return;

            // Allow navigation if it's a directory OR a virtual path (e.g. zip://)
            if (!Directory.Exists(path) && !ProtocolManager.IsVirtual(path))
                return;

            var state = GetState(pane);

            // 始终更新 path，即使相同也可能需要刷新或触发事件? 
            // 保持原逻辑：if changed
            if (state.CurrentPath != path)
            {
                state.CurrentPath = path;
                if (addToHistory) AddToHistory(pane, path);
                PublishNavigationComplete(path, pane, NavigationSource.AddressBar);
            }
        }

        #endregion

        #region 高亮方法

        public void HighlightMatchingLibrary(object library)
        {
            UIHelper?.SetLibrarySelectedItem(library);
        }

        public void HighlightMatchingItems(string path)
        {
            if (UIHelper == null || string.IsNullOrEmpty(path))
                return;

            ClearItemHighlights();

            var drives = UIHelper.GetDrivesListItems()?.Cast<object>().ToList();
            var quickAccess = UIHelper.GetQuickAccessListItems()?.Cast<object>().ToList();
            var favorites = UIHelper.GetFavoritesListItems()?.Cast<object>().ToList();

            if (drives != null)
            {
                foreach (var drive in drives)
                {
                    var drivePath = GetItemPath(drive);
                    if (!string.IsNullOrEmpty(drivePath) && string.Equals(drivePath.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        UIHelper.SetItemHighlight("Drive", drive, true);
                        break;
                    }
                }
            }

            if (quickAccess != null)
            {
                foreach (var item in quickAccess)
                {
                    var itemPath = GetItemPath(item);
                    if (!string.IsNullOrEmpty(itemPath) && string.Equals(itemPath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        UIHelper.SetItemHighlight("QuickAccess", item, true);
                        break;
                    }
                }
            }

            if (favorites != null)
            {
                foreach (var item in favorites)
                {
                    var itemPath = GetItemPath(item);
                    if (!string.IsNullOrEmpty(itemPath) && string.Equals(itemPath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        UIHelper.SetItemHighlight("Favorites", item, true);
                        break;
                    }
                }
            }
        }

        public void ClearItemHighlights()
        {
            UIHelper?.ClearListBoxHighlights("Drive");
            UIHelper?.ClearListBoxHighlights("QuickAccess");
            UIHelper?.ClearListBoxHighlights("Favorites");
            UIHelper?.ClearListBoxHighlights("Library");
        }

        #endregion

        #region 私有方法

        private NavigationState GetState(PaneId pane)
        {
            // Ensure Thread Safety? NavigationService is Singleton.
            // Assuming UI thread usage mainly.
            if (!_paneStates.ContainsKey(pane))
            {
                _paneStates[pane] = new NavigationState();
            }
            return _paneStates[pane];
        }

        private void AddToHistory(PaneId pane, string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            var state = GetState(pane);

            if (state.CurrentIndex >= 0 && state.CurrentIndex < state.History.Count - 1)
            {
                state.History.RemoveRange(state.CurrentIndex + 1, state.History.Count - state.CurrentIndex - 1);
            }

            if (state.History.Count == 0 || state.History[state.History.Count - 1] != path)
            {
                state.History.Add(path);
                state.CurrentIndex = state.History.Count - 1;
            }
            else
            {
                state.CurrentIndex = state.History.Count - 1;
            }
        }

        private void PublishNavigationComplete(string path, PaneId pane, NavigationSource source)
        {
            var state = GetState(pane);

            // 记录到全局历史记录 (地址栏下拉列表)
            RecordGlobalHistory(path);

            _messageBus?.Publish(new NavigationCompleteMessage(
                path,
                pane,
                source,
                "Path", // NavigationMode default
                state.BackStack,
                state.ForwardStack));
        }

        private void RecordGlobalHistory(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                var info = ProtocolManager.Parse(path);
                HistoryType? type = null;
                string content = info.TargetPath;

                switch (info.Type)
                {
                    case ProtocolType.Local:
                        type = HistoryType.LocalPath;
                        break;
                    case ProtocolType.Search:
                        type = HistoryType.Search;
                        break;
                    case ProtocolType.ContentSearch:
                        type = HistoryType.FullTextSearch;
                        break;
                    case ProtocolType.Library:
                        type = HistoryType.Library;
                        break;
                    case ProtocolType.Tag:
                        type = HistoryType.Tag;
                        break;
                }

                if (type.HasValue)
                {
                    SearchHistoryService.Instance.Add(content, type.Value);
                }
            }
            catch { }
        }

        private string GetItemPath(object item)
        {
            if (item == null) return null;
            var pathProperty = item.GetType().GetProperty("Path");
            if (pathProperty != null) return pathProperty.GetValue(item)?.ToString();
            if (item is string str) return str;
            return item.ToString();
        }

        #endregion
    }
}
