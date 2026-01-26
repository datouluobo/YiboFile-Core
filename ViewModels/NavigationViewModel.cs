using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using YiboFile.Controls;
using YiboFile.Services;

namespace YiboFile.ViewModels
{
    /// <summary>
    /// 导航管理 ViewModel
    /// 负责管理导航模式切换、导航历史、路径导航等功能
    /// </summary>
    public class NavigationViewModel : BaseViewModel
    {
        private readonly Window _ownerWindow;
        private readonly FileBrowserControl _fileBrowser;
        private readonly Action<string> _onNavigationModeChanged;
        private readonly Action<string> _onPathChanged;

        private NavigationStateManager.NavigationMode _currentMode = NavigationStateManager.NavigationMode.Path;
        private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        private List<string> _navigationHistory = new List<string>();
        private int _currentHistoryIndex = -1;
        private ObservableCollection<string> _quickAccessPaths = new ObservableCollection<string>();
        private ObservableCollection<string> _drivePaths = new ObservableCollection<string>();
        private ObservableCollection<string> _favoritePaths = new ObservableCollection<string>();

        public NavigationStateManager.NavigationMode CurrentMode
        {
            get => _currentMode;
            set => SetProperty(ref _currentMode, value);
        }

        public string CurrentPath
        {
            get => _currentPath;
            set => SetProperty(ref _currentPath, value);
        }

        public ObservableCollection<string> QuickAccessPaths
        {
            get => _quickAccessPaths;
            set => SetProperty(ref _quickAccessPaths, value);
        }

        public ObservableCollection<string> DrivePaths
        {
            get => _drivePaths;
            set => SetProperty(ref _drivePaths, value);
        }

        public ObservableCollection<string> FavoritePaths
        {
            get => _favoritePaths;
            set => SetProperty(ref _favoritePaths, value);
        }

        public bool CanNavigateBack => _currentHistoryIndex > 0;
        public bool CanNavigateForward => _currentHistoryIndex < _navigationHistory.Count - 1;

        public NavigationViewModel(Window ownerWindow, FileBrowserControl fileBrowser, Action<string> onNavigationModeChanged, Action<string> onPathChanged)
        {
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
            _fileBrowser = fileBrowser ?? throw new ArgumentNullException(nameof(fileBrowser));
            _onNavigationModeChanged = onNavigationModeChanged;
            _onPathChanged = onPathChanged;
        }

        /// <summary>
        /// 切换导航模式
        /// </summary>
        public void SwitchNavigationMode(string mode)
        {
            NavigationStateManager.NavigationMode newMode;
            switch (mode)
            {
                case "Path":
                    newMode = NavigationStateManager.NavigationMode.Path;
                    break;
                case "Library":
                    newMode = NavigationStateManager.NavigationMode.Library;
                    break;
                default:
                    newMode = NavigationStateManager.NavigationMode.Path;
                    break;
            }

            CurrentMode = newMode;
            _onNavigationModeChanged?.Invoke(mode);
        }

        /// <summary>
        /// 导航到路径
        /// </summary>
        public void NavigateToPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            // 添加到历史记录
            if (_currentHistoryIndex >= 0 && _currentHistoryIndex < _navigationHistory.Count - 1)
            {
                // 如果不在历史记录末尾，删除后面的记录
                _navigationHistory.RemoveRange(_currentHistoryIndex + 1, _navigationHistory.Count - _currentHistoryIndex - 1);
            }

            if (_navigationHistory.Count == 0 || _navigationHistory[_navigationHistory.Count - 1] != path)
            {
                _navigationHistory.Add(path);
                _currentHistoryIndex = _navigationHistory.Count - 1;
            }
            else
            {
                _currentHistoryIndex = _navigationHistory.Count - 1;
            }

            CurrentPath = path;
            _onPathChanged?.Invoke(path);
        }

        /// <summary>
        /// 后退
        /// </summary>
        public void NavigateBack()
        {
            if (CanNavigateBack)
            {
                _currentHistoryIndex--;
                var path = _navigationHistory[_currentHistoryIndex];
                CurrentPath = path;
                _onPathChanged?.Invoke(path);
            }
        }

        /// <summary>
        /// 前进
        /// </summary>
        public void NavigateForward()
        {
            if (CanNavigateForward)
            {
                _currentHistoryIndex++;
                var path = _navigationHistory[_currentHistoryIndex];
                CurrentPath = path;
                _onPathChanged?.Invoke(path);
            }
        }

        /// <summary>
        /// 向上导航
        /// </summary>
        public void NavigateUp()
        {
            if (string.IsNullOrEmpty(CurrentPath))
                return;

            try
            {
                var parentPath = Directory.GetParent(CurrentPath)?.FullName;
                if (!string.IsNullOrEmpty(parentPath) && Directory.Exists(parentPath))
                {
                    NavigateToPath(parentPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(_ownerWindow, $"无法导航到父目录: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载快速访问路径
        /// </summary>
        public void LoadQuickAccess()
        {
            QuickAccessPaths.Clear();
            var paths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };

            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    QuickAccessPaths.Add(path);
                }
            }
        }

        /// <summary>
        /// 加载驱动器列表
        /// </summary>
        public void LoadDrives()
        {
            DrivePaths.Clear();
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady)
                .Select(d => d.RootDirectory.FullName)
                .ToList();

            foreach (var drive in drives)
            {
                DrivePaths.Add(drive);
            }
        }

        /// <summary>
        /// 加载收藏夹
        /// </summary>
        public void LoadFavorites()
        {
            FavoritePaths.Clear();
            var favorites = DatabaseManager.GetAllFavorites();
            foreach (var favorite in favorites.OrderBy(f => f.SortOrder))
            {
                if (Directory.Exists(favorite.Path) || File.Exists(favorite.Path))
                {
                    FavoritePaths.Add(favorite.Path);
                }
            }
        }
    }
}


