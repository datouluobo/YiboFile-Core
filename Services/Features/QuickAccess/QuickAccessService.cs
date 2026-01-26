using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace YiboFile.Services.QuickAccess
{
    /// <summary>
    /// 快速访问服务
    /// 负责快速访问项和驱动器的加载和管理
    /// </summary>
    public class QuickAccessService
    {
        #region 数据类

        /// <summary>
        /// 驱动器项数据
        /// </summary>
        public class DriveItemData
        {
            public string DisplayName { get; set; }      // 显示名称,如 "C: (系统)"
            public string Path { get; set; }             // 路径,如 "C:\"
            public string ToolTip { get; set; }          // 工具提示
            public long TotalSize { get; set; }          // 总容量(字节)
            public long UsedSize { get; set; }           // 已用容量(字节)
            public double UsagePercentage { get; set; }  // 使用率 0.0-1.0
            public string UsageText { get; set; }        // 使用量文本,如 "125 GB / 500 GB"
            public string IconKey { get; set; } = "Icon_Drive"; // Default to Drive icon
        }

        #endregion

        #region 事件定义

        /// <summary>
        /// 路径导航请求事件
        /// </summary>
        public event EventHandler<string> NavigateRequested;

        /// <summary>
        /// 新标签页创建请求事件
        /// </summary>
        public event EventHandler<string> CreateTabRequested;

        #endregion

        #region 私有字段

        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        #endregion

        #region 构造函数

        public QuickAccessService(System.Windows.Threading.Dispatcher dispatcher = null)
        {
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 加载快速访问列表
        /// </summary>
        /// <summary>
        /// 快速访问项数据
        /// </summary>
        public class QuickAccessItem
        {
            public string DisplayName { get; set; }
            public string Path { get; set; }
            public string IconKey { get; set; }
        }

        /// <summary>
        /// 加载快速访问列表
        /// </summary>
        public void LoadQuickAccess(ListBox quickAccessListBox)
        {
            if (quickAccessListBox == null) return;

            _dispatcher.Invoke(() =>
            {
                var quickAccessPaths = new List<(string Path, string Name, string IconKey)>
                {
                    (Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "桌面", "Icon_Desktop"),
                    (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "文档", "Icon_Document"),
                    (Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "图片", "Icon_Image"),
                    (Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "音乐", "Icon_Music"),
                    (Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "视频", "Icon_Video"),
                    (Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "用户", "Icon_User")
                };

                var accessItems = quickAccessPaths
                    .Where(item => Directory.Exists(item.Path))
                    .Select(item => new QuickAccessItem
                    {
                        DisplayName = item.Name,
                        Path = item.Path,
                        IconKey = item.IconKey
                    })
                    .ToList();

                quickAccessListBox.ItemsSource = accessItems;
                // quickAccessListBox.DisplayMemberPath = "DisplayName"; // Removed, using ItemTemplate in XAML

                // 设置选择事件
                quickAccessListBox.SelectionChanged -= QuickAccessListBox_SelectionChanged;
                quickAccessListBox.SelectionChanged += QuickAccessListBox_SelectionChanged;

                // 设置鼠标中键事件
                quickAccessListBox.PreviewMouseDown -= QuickAccessListBox_PreviewMouseDown;
                quickAccessListBox.PreviewMouseDown += QuickAccessListBox_PreviewMouseDown;
            });
        }

        /// <summary>
        /// 加载驱动器树状列表
        /// </summary>
        public void LoadDriveTree(TreeView treeView, Func<long, string> formatFileSize)
        {
            if (treeView == null) return;

            _dispatcher.Invoke(() =>
            {
                try
                {
                    var items = new System.Collections.ObjectModel.ObservableCollection<YiboFile.Services.Navigation.NavigationItem>();

                    var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                    foreach (var drive in drives)
                    {
                        long usedSize = drive.TotalSize - drive.AvailableFreeSpace;
                        double usagePercentage = drive.TotalSize > 0 ? (double)usedSize / drive.TotalSize : 0;

                        var item = new YiboFile.Services.Navigation.NavigationItem
                        {
                            Header = $"{drive.Name} ({drive.VolumeLabel})",
                            Path = drive.Name,
                            IsDrive = true,
                            IconKey = "Icon_Drive",
                            TotalSize = drive.TotalSize,
                            UsedSize = usedSize,
                            UsagePercentage = usagePercentage,
                            UsageText = $"{formatFileSize(usedSize)} / {formatFileSize(drive.TotalSize)}",
                            ToolTip = $"总空间: {formatFileSize(drive.TotalSize)}\n可用空间: {formatFileSize(drive.AvailableFreeSpace)}",
                            // 响应式显示属性
                            DriveLetter = drive.Name.TrimEnd('\\'),
                            DriveLabel = !string.IsNullOrEmpty(drive.VolumeLabel) ? $"({drive.VolumeLabel})" : "",
                            UsedSizeText = formatFileSize(usedSize),
                            TotalSizeText = formatFileSize(drive.TotalSize)
                        };

                        // 默认添加 dummy child 以显示展开箭头（所有驱动器都可能有子文件夹）
                        item.AddDummyChild();

                        items.Add(item);
                    }

                    treeView.ItemsSource = items;

                    // 事件绑定应该在 XAML 或 View 代码中处理，这里仅绑定数据源
                }
                catch
                {
                    treeView.ItemsSource = null;
                }
            });
        }

        /// <summary>
        /// 加载驱动器列表 (Legacy ListBox support)
        /// </summary>
        public void LoadDrives(ListBox drivesListBox, Func<long, string> formatFileSize)
        {
            if (drivesListBox == null) return;

            _dispatcher.Invoke(() =>
            {
                try
                {
                    var drives = DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
                    var driveItems = drives.Select(drive =>
                    {
                        long usedSize = drive.TotalSize - drive.AvailableFreeSpace;
                        double usagePercentage = drive.TotalSize > 0 ? (double)usedSize / drive.TotalSize : 0;

                        return new DriveItemData
                        {
                            DisplayName = $"{drive.Name} ({drive.VolumeLabel})",
                            Path = drive.Name,
                            ToolTip = $"总空间: {formatFileSize(drive.TotalSize)}\n可用空间: {formatFileSize(drive.AvailableFreeSpace)}",
                            TotalSize = drive.TotalSize,
                            UsedSize = usedSize,
                            UsagePercentage = usagePercentage,
                            UsageText = $"{formatFileSize(usedSize)} / {formatFileSize(drive.TotalSize)}"
                        };
                    }).ToList();

                    drivesListBox.ItemsSource = driveItems;

                    // 设置选择事件
                    drivesListBox.SelectionChanged -= DrivesListBox_SelectionChanged;
                    drivesListBox.SelectionChanged += DrivesListBox_SelectionChanged;

                    // 设置鼠标中键事件（已在LoadDrives中处理）
                    drivesListBox.PreviewMouseDown -= DrivesListBox_PreviewMouseDown;
                    drivesListBox.PreviewMouseDown += DrivesListBox_PreviewMouseDown;
                }
                catch
                {
                    drivesListBox.ItemsSource = null;
                }
            });
        }

        #endregion

        #region 事件处理

        private void QuickAccessListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem == null) return;

            var selectedItem = listBox.SelectedItem;
            var pathProperty = selectedItem.GetType().GetProperty("Path");
            if (pathProperty != null)
            {
                var path = pathProperty.GetValue(selectedItem) as string;
                if (!string.IsNullOrEmpty(path))
                {
                    NavigateRequested?.Invoke(this, path);
                }
            }

            // 清除选择
            listBox.SelectedItem = null;
        }

        private void DrivesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listBox = sender as ListBox;
            if (listBox?.SelectedItem == null) return;

            var selectedItem = listBox.SelectedItem;
            var pathProperty = selectedItem.GetType().GetProperty("Path");
            if (pathProperty != null)
            {
                var path = pathProperty.GetValue(selectedItem) as string;
                if (!string.IsNullOrEmpty(path))
                {
                    NavigateRequested?.Invoke(this, path);
                }
            }

            // 清除选择
            listBox.SelectedItem = null;
        }

        private void QuickAccessListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 处理鼠标中键点击打开新标签页
            if (e.ChangedButton == MouseButton.Middle)
            {
                var listBox = sender as ListBox;
                if (listBox == null) return;

                // 获取点击位置对应的项目
                var hitResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
                if (hitResult == null) return;

                // 向上查找 ListBoxItem
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listBox)
                {
                    if (current is ListBoxItem item && item.DataContext != null)
                    {
                        var pathProperty = item.DataContext.GetType().GetProperty("Path");
                        if (pathProperty != null)
                        {
                            var path = pathProperty.GetValue(item.DataContext) as string;
                            if (!string.IsNullOrEmpty(path))
                            {
                                try
                                {
                                    if (Directory.Exists(path))
                                    {
                                        CreateTabRequested?.Invoke(this, path);
                                        e.Handled = true;
                                        return;
                                    }
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    // MessageBox.Show($"无法访问路径: {path}\n\n{ex.Message}", "权限错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    e.Handled = true;
                                    return;
                                }
                                catch (Exception)
                                {
                                    // MessageBox.Show($"无法打开路径: {path}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    e.Handled = true;
                                    return;
                                }
                            }
                        }
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }

        private void DrivesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 处理鼠标中键点击打开新标签页
            if (e.ChangedButton == MouseButton.Middle)
            {
                var listBox = sender as ListBox;
                if (listBox == null) return;

                // 获取点击位置对应的项目
                var hitResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
                if (hitResult == null) return;

                // 向上查找 ListBoxItem
                DependencyObject current = hitResult.VisualHit;
                while (current != null && current != listBox)
                {
                    if (current is ListBoxItem item && item.DataContext != null)
                    {
                        var pathProperty = item.DataContext.GetType().GetProperty("Path");
                        if (pathProperty != null)
                        {
                            var path = pathProperty.GetValue(item.DataContext) as string;
                            if (!string.IsNullOrEmpty(path))
                            {
                                try
                                {
                                    if (Directory.Exists(path))
                                    {
                                        CreateTabRequested?.Invoke(this, path);
                                        e.Handled = true;
                                        return;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"无法打开驱动器: {path}\n\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                    e.Handled = true;
                                    return;
                                }
                            }
                        }
                        break;
                    }
                    current = VisualTreeHelper.GetParent(current);
                }
            }
        }

        #endregion
    }
}











