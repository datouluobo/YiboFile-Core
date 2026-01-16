using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using YiboFile.Services.Core;

namespace YiboFile.Services.Navigation
{
    /// <summary>
    /// 导航服务
    /// 负责管理导航历史、路径导航等功能
    /// </summary>
    public class NavigationService
    {
        #region 私有字段

        private List<string> _navigationHistory;
        private int _currentHistoryIndex;
        private string _currentPath;
        private string _lastLeftNavSource;

        #endregion

        #region 公共属性

        /// <summary>
        /// UI 辅助接口
        /// </summary>
        public INavigationUIHelper UIHelper { get; set; }

        /// <summary>
        /// 当前路径
        /// </summary>
        public string CurrentPath
        {
            get => _currentPath;
            set
            {
                if (_currentPath != value)
                {
                    _currentPath = value;
                    AddToHistory(value);
                }
            }
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
        /// 是否可以后退
        /// </summary>
        public bool CanNavigateBack => _currentHistoryIndex > 0;

        /// <summary>
        /// 是否可以前进
        /// </summary>
        public bool CanNavigateForward => _currentHistoryIndex < _navigationHistory.Count - 1;

        /// <summary>
        /// 是否可以后退（别名，用于兼容）
        /// </summary>
        public bool CanGoBack => CanNavigateBack;

        /// <summary>
        /// 是否可以前进（别名，用于兼容）
        /// </summary>
        public bool CanGoForward => CanNavigateForward;

        #endregion

        #region 事件

        /// <summary>
        /// 导航请求事件
        /// </summary>
        public event EventHandler<string> NavigateRequested;

        #endregion

        #region 构造函数

        /// <summary>
        /// 初始化导航服务
        /// </summary>
        /// <param name="initialPath">初始路径</param>
        public NavigationService(string initialPath)
        {
            _navigationHistory = new List<string>();
            _currentHistoryIndex = -1;
            _currentPath = initialPath ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            _lastLeftNavSource = string.Empty;
            AddToHistory(_currentPath);
        }

        #endregion

        #region 导航方法

        /// <summary>
        /// 切换导航模式（用于兼容 NavigationModeService）
        /// </summary>
        /// <param name="mode">导航模式</param>
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
        /// 后退
        /// </summary>
        /// <returns>导航到的路径</returns>
        public string NavigateBack()
        {
            if (CanNavigateBack)
            {
                _currentHistoryIndex--;
                var path = _navigationHistory[_currentHistoryIndex];
                _currentPath = path;
                NavigateRequested?.Invoke(this, path);
                return path;
            }
            return null;
        }

        /// <summary>
        /// 前进
        /// </summary>
        /// <returns>导航到的路径</returns>
        public string NavigateForward()
        {
            if (CanNavigateForward)
            {
                _currentHistoryIndex++;
                var path = _navigationHistory[_currentHistoryIndex];
                _currentPath = path;
                NavigateRequested?.Invoke(this, path);
                return path;
            }
            return null;
        }

        /// <summary>
        /// 向上导航
        /// </summary>
        /// <returns>父目录路径</returns>
        public string NavigateUp()
        {
            if (string.IsNullOrEmpty(_currentPath))
                return null;

            var protocolInfo = ProtocolManager.Parse(_currentPath);
            if (protocolInfo.Type == ProtocolType.Archive)
            {
                try
                {
                    string archiveFile = protocolInfo.TargetPath;
                    string innerPath = protocolInfo.ExtraData;

                    // If innerPath contains directory separators, move up inside archive
                    // Normalize innerPath to use standard slash for logic if needed, but 7z uses what it uses.
                    // Assuming simple hierarchy.

                    string parentInner = string.Empty;

                    // Trim trailing slashes from innerPath
                    innerPath = innerPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                    if (!string.IsNullOrEmpty(innerPath))
                    {
                        // Has content, try to find parent
                        // If inner path is just a file or folder name, parent is empty (archive root)
                        // If "A/B", parent is "A"

                        int lastSlash = innerPath.LastIndexOfAny(new char[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
                        if (lastSlash >= 0)
                        {
                            parentInner = innerPath.Substring(0, lastSlash);
                            string newUrl = $"{ProtocolManager.ZipProtocol}{archiveFile}|{parentInner}";
                            CurrentPath = newUrl;
                            NavigateRequested?.Invoke(this, newUrl);
                            return newUrl;
                        }
                        else
                        {
                            // At root folder of archive content (e.g. zip://zip|folder), parent is archive root (zip://zip|)
                            string newUrl = $"{ProtocolManager.ZipProtocol}{archiveFile}|";
                            CurrentPath = newUrl;
                            NavigateRequested?.Invoke(this, newUrl);
                            return newUrl;
                        }
                    }

                    // If innerPath is empty or we are at root "zip://zip|", navigate to the archive file's parent folder
                    // i.e. exit archive mode
                    if (string.IsNullOrEmpty(innerPath))
                    {
                        string parentDir = Directory.GetParent(archiveFile)?.FullName;
                        if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                        {
                            CurrentPath = parentDir;
                            NavigateRequested?.Invoke(this, parentDir);
                            return parentDir;
                        }
                    }
                }
                catch
                {
                    // Fallback to standard logic if parsing fails
                }
            }

            try
            {
                var parentPath = Directory.GetParent(_currentPath)?.FullName;
                if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
                {
                    CurrentPath = parentPath;
                    NavigateRequested?.Invoke(this, parentPath);
                    return parentPath;
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }

        /// <summary>
        /// 导航到指定路径
        /// </summary>
        /// <param name="path">目标路径</param>
        public void NavigateTo(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            // Allow navigation if it's a directory OR a virtual path (e.g. zip://)
            if (!Directory.Exists(path) && !ProtocolManager.IsVirtual(path))
                return;

            CurrentPath = path;
            NavigateRequested?.Invoke(this, path);
        }

        #endregion

        #region 高亮方法

        /// <summary>
        /// 高亮匹配的库
        /// </summary>
        /// <param name="library">要高亮的库</param>
        public void HighlightMatchingLibrary(object library)
        {
            UIHelper?.SetLibrarySelectedItem(library);
        }

        /// <summary>
        /// 高亮匹配的项
        /// </summary>
        /// <param name="path">要匹配的路径</param>
        public void HighlightMatchingItems(string path)
        {
            if (UIHelper == null || string.IsNullOrEmpty(path))
                return;

            // 清除所有高亮
            ClearItemHighlights();

            // 根据路径匹配对应的列表项
            var drives = UIHelper.GetDrivesListItems()?.Cast<object>().ToList();
            var quickAccess = UIHelper.GetQuickAccessListItems()?.Cast<object>().ToList();
            var favorites = UIHelper.GetFavoritesListItems()?.Cast<object>().ToList();

            // 检查驱动器 - 使用完全匹配，而不是前缀匹配
            if (drives != null)
            {
                foreach (var drive in drives)
                {
                    var drivePath = GetItemPath(drive);
                    // 修改：只有路径完全匹配驱动器根目录时才高亮（例如 "E:\" 匹配 "E:\\"）
                    // 移除了 StartsWith 以避免子文件夹也高亮父驱动器
                    if (!string.IsNullOrEmpty(drivePath) && string.Equals(drivePath.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    {
                        UIHelper.SetItemHighlight("Drive", drive, true);
                        break;
                    }
                }
            }

            // 检查快速访问
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

            // 检查收藏
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

        /// <summary>
        /// 清除所有项的高亮
        /// </summary>
        public void ClearItemHighlights()
        {
            UIHelper?.ClearListBoxHighlights("Drive");
            UIHelper?.ClearListBoxHighlights("QuickAccess");
            UIHelper?.ClearListBoxHighlights("Favorites");
            UIHelper?.ClearListBoxHighlights("Library");
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 添加到历史记录
        /// </summary>
        private void AddToHistory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            // 如果不在历史记录末尾，删除后面的记录
            if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count - 1)
            {
                _navigationHistory.RemoveRange(_currentHistoryIndex + 1, _navigationHistory.Count - _currentHistoryIndex - 1);
            }

            // 如果与最后一个历史记录不同，添加新记录
            if (_navigationHistory.Count == 0 || _navigationHistory[_navigationHistory.Count - 1] != path)
            {
                _navigationHistory.Add(path);
                _currentHistoryIndex = _navigationHistory.Count - 1;
            }
            else
            {
                _currentHistoryIndex = _navigationHistory.Count - 1;
            }
        }

        /// <summary>
        /// 获取项的路径
        /// </summary>
        private string GetItemPath(object item)
        {
            if (item == null)
                return null;

            // 尝试通过反射获取 Path 属性
            var pathProperty = item.GetType().GetProperty("Path");
            if (pathProperty != null)
            {
                return pathProperty.GetValue(item)?.ToString();
            }

            // 如果是字符串，直接返回
            if (item is string str)
            {
                return str;
            }

            // 尝试 ToString()
            return item.ToString();
        }

        #endregion
    }
}

