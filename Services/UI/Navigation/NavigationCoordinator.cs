using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using YiboFile.Controls;
using FavoriteType = YiboFile.Favorite;
using YiboFile.Services.Core;

namespace YiboFile.Services.Navigation
{
    /// <summary>
    /// 统一导航协调器
    /// 负责处理所有导航模式的链接打开行为，确保行为一致性
    /// </summary>
    public class NavigationCoordinator
    {
        /// <summary>
        /// 导航来源
        /// </summary>
        public enum NavigationSource
        {
            Drive,          // 驱动器
            QuickAccess,   // 快速访问
            Favorite,       // 收藏夹
            Library,        // 库
            Breadcrumb,     // 面包屑
            AddressBar,     // 地址栏
            FileList        // 文件列表
        }

        /// <summary>
        /// 点击类型
        /// </summary>
        public enum ClickType
        {
            LeftClick,      // 左键点击
            MiddleClick,    // 中键点击
            CtrlLeftClick   // Ctrl+左键点击
        }

        /// <summary>
        /// 路径导航请求事件
        /// </summary>
        public event Action<string, bool> PathNavigateRequested; // path, forceNewTab

        /// <summary>
        /// 库导航请求事件
        /// </summary>
        public event Action<Library, bool> LibraryNavigateRequested; // library, forceNewTab

        /// <summary>
        /// 文件打开请求事件
        /// </summary>
        public event Action<string> FileOpenRequested; // filePath

        /// <summary>
        /// 从鼠标事件判断点击类型
        /// </summary>
        public static ClickType GetClickType(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle)
                return ClickType.MiddleClick;

            if (e.ChangedButton == MouseButton.Left &&
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                return ClickType.CtrlLeftClick;

            return ClickType.LeftClick;
        }

        /// <summary>
        /// 处理路径导航
        /// 判断顺序：
        /// 1. 如果是forceNewTab（中键或Ctrl+左键），直接创建新标签页
        /// 2. 否则查找是否已存在该路径的标签页，如果有则切换
        /// 3. 如果当前标签页是路径类型，更新它
        /// 4. 否则创建新标签页
        /// </summary>
        public void HandlePathNavigation(string path, NavigationSource source, ClickType clickType)
        {
            if (string.IsNullOrEmpty(path))
                return;

            // 验证路径是否存在
            bool isVirtual = ProtocolManager.IsVirtual(path) ||
                           path.StartsWith("content://", StringComparison.OrdinalIgnoreCase) ||
                           path.StartsWith("search://", StringComparison.OrdinalIgnoreCase);

            if (!isVirtual && !Directory.Exists(path) && !File.Exists(path))
            {
                MessageBox.Show($"路径不存在: {path}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 如果是文件，打开文件
            if (File.Exists(path) && !Directory.Exists(path))
            {
                FileOpenRequested?.Invoke(path);
                return;
            }

            // 判断是否需要强制打开新标签页
            bool forceNewTab = clickType == ClickType.MiddleClick || clickType == ClickType.CtrlLeftClick;

            // 触发路径导航请求
            PathNavigateRequested?.Invoke(path, forceNewTab);
        }

        /// <summary>
        /// 处理库导航
        /// 判断顺序：
        /// 1. 如果是forceNewTab（中键或Ctrl+左键），直接创建新标签页
        /// 2. 否则查找是否已存在该库的标签页（按库ID），如果有则切换
        /// 3. 如果当前标签页是库页，用当前页打开
        /// 4. 否则创建新标签页
        /// </summary>
        public void HandleLibraryNavigation(Library library, ClickType clickType)
        {
            if (library == null)
                return;

            // 判断是否需要强制打开新标签页
            bool forceNewTab = clickType == ClickType.MiddleClick || clickType == ClickType.CtrlLeftClick;

            // 触发库导航请求
            LibraryNavigateRequested?.Invoke(library, forceNewTab);
        }

        /// <summary>
        /// 收藏夹路径不存在事件
        /// </summary>
        public event Action<FavoriteType> FavoritePathNotFound; // favorite

        /// <summary>
        /// 处理收藏夹导航（可能是路径或文件）
        /// </summary>
        public void HandleFavoriteNavigation(FavoriteType favorite, ClickType clickType)
        {
            if (favorite == null)
                return;

            if (favorite.IsDirectory && Directory.Exists(favorite.Path))
            {
                HandlePathNavigation(favorite.Path, NavigationSource.Favorite, clickType);
            }
            else if (!favorite.IsDirectory && File.Exists(favorite.Path))
            {
                FileOpenRequested?.Invoke(favorite.Path);
            }
            else
            {
                // 路径不存在，触发事件由MainWindow处理移除逻辑
                FavoritePathNotFound?.Invoke(favorite);
            }
        }
    }
}


