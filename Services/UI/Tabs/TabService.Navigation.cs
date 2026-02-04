using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using YiboFile.Models;
using YiboFile.Services.Config;
using YiboFile.Services.Core;
using YiboFile.Services.Search;

namespace YiboFile.Services.Tabs
{
    public partial class TabService
    {
        public bool ValidatePath(string path, out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(path))
            {
                errorMessage = "路径不能为空";
                return false;
            }

            if (ProtocolManager.IsVirtual(path) ||
                path.StartsWith("search://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("content://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                // [FIX] 不要在 UI 线程上进行同步的 Directory.Exists 检查，这在挂载盘或网络路径上极易卡死
                // 验证逻辑应聚焦于格式合法性，实际存在性检查应由异步加载过程处理
                if (string.IsNullOrWhiteSpace(path))
                {
                    errorMessage = "路径不能为空";
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

        public string GetPathDisplayTitle(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            if (ProtocolManager.IsVirtual(path))
            {
                if (path.StartsWith("search://", StringComparison.OrdinalIgnoreCase))
                {
                    return "搜索: " + path.Substring("search://".Length);
                }
                if (path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase))
                {
                    var tagName = path.Substring("tag://".Length);
                    return !string.IsNullOrEmpty(tagName) ? tagName : "标签";
                }
                return path;
            }

            string normalizedPath = path.TrimEnd('\\');
            if (string.IsNullOrEmpty(normalizedPath)) normalizedPath = path;

            string rootPath = Path.GetPathRoot(path);
            if (rootPath == path || rootPath.TrimEnd('\\') == normalizedPath)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(rootPath)) return path;

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
                    return rootPath.TrimEnd('\\');
                }
            }

            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
            {
                return path;
            }
            return fileName;
        }

        public async void SwitchToTab(PathTab tab)
        {
            EnsureUi();
            if (tab == null) return;

            // [SSOT] 只负责更新业务状态，不再通过委托强推 UI 更新
            // UI 同步由 MainWindow 订阅 ActiveTabChanged 实现
            tab.LastAccessTime = DateTime.Now;
            SetActiveTab(tab);
            UpdateTabStyles();

            // 如果路径无效，则在后台检测并关闭标签页
            if (tab.Type == TabType.Path)
            {
                try
                {
                    string p = tab.Path;
                    if (!string.IsNullOrEmpty(p) && !ProtocolManager.IsVirtual(p))
                    {
                        bool exists = await Task.Run(() =>
                        {
                            try { return Directory.Exists(p); }
                            catch { return false; }
                        });

                        if (!exists)
                        {
                            YiboFile.DialogService.Warning($"路径不存在: {tab.Path}\n\n标签页将被关闭。");
                            CloseTab(tab);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    YiboFile.DialogService.Error($"标签页路径检测异常: {ex.Message}");
                    CloseTab(tab);
                    return;
                }
            }
        }

        public List<string> GetValidLibraryPaths(Library library)
        {
            if (library == null || library.Paths == null || library.Paths.Count == 0)
                return new List<string>();

            // [FIX] 避免在 UI 线程直接循环调用 Directory.Exists，这会导致大量同步 IO 阻塞
            // 暂时返回所有路径，依赖后续的异步加载过程来处理不存在的情况
            return library.Paths.ToList();
        }

        public List<PathTab> GetTabsToRemoveForLibrary(List<string> validPaths)
        {
            return _tabs.Where(tab => tab.Type == TabType.Path && !validPaths.Contains(tab.Path)).ToList();
        }

        public PathTab GetTabToActivateForLibrary(List<string> validPaths)
        {
            if (validPaths == null || validPaths.Count == 0) return null;

            if (_activeTab != null && _activeTab.Type == TabType.Path && validPaths.Contains(_activeTab.Path))
            {
                return _activeTab;
            }

            var firstTab = _tabs.FirstOrDefault(t => t.Type == TabType.Path && validPaths.Contains(t.Path));
            if (firstTab != null) return firstTab;

            return _tabs.FirstOrDefault();
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
        }

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
                YiboFile.DialogService.Error($"无法打开资源管理器: {ex.Message}");
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
    }
}
