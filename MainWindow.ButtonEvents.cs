using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace YiboFile
{
    /// <summary>
    /// MainWindow 新增按钮事件处理
    /// </summary>
    public partial class MainWindow
    {
        #region 新增按钮事件处理

        internal void ImportLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导入库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        internal void ExportLibrary_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("导出库功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        internal void AddFileToLibrary_Click(object sender, RoutedEventArgs e)
        {
            // 获取选中的文件或文件夹
            var selectedItems = FileBrowser?.FilesSelectedItems?.Cast<FileSystemItem>().ToList() ?? new List<FileSystemItem>();
            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请先选择要添加到库的文件或文件夹", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 确定要添加到的库
            Library targetLibrary = null;

            // 如果当前在库模式且选中了库，使用当前库
            if (_currentLibrary != null)
            {
                targetLibrary = _currentLibrary;
            }
            else
            {
                // 让用户选择库
                var libraries = DatabaseManager.GetAllLibraries();
                if (libraries.Count == 0)
                {
                    MessageBox.Show("当前没有可用的库，请先创建一个库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 创建库选择对话框
                var dialog = new Window
                {
                    Title = "选择库",
                    Width = 400,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this
                };

                var listBox = new ListBox
                {
                    DisplayMemberPath = "Name",
                    Margin = new Thickness(10),
                    ItemsSource = libraries
                };

                var okButton = new Button
                {
                    Content = "确定",
                    Width = 80,
                    Height = 30,
                    Margin = new Thickness(0, 0, 10, 0),
                    IsDefault = true
                };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Width = 80,
                    Height = 30,
                    IsCancel = true
                };

                okButton.Click += (s, args) =>
                {
                    if (listBox.SelectedItem is Library selectedLib)
                    {
                        targetLibrary = selectedLib;
                        dialog.DialogResult = true;
                        dialog.Close();
                    }
                    else
                    {
                        MessageBox.Show("请选择一个库", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };

                cancelButton.Click += (s, args) =>
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                };

                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical
                };

                var label = new Label
                {
                    Content = "请选择要添加到的库:",
                    Margin = new Thickness(10, 10, 10, 5)
                };

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(10)
                };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                stackPanel.Children.Add(label);
                stackPanel.Children.Add(listBox);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;

                if (dialog.ShowDialog() != true)
                {
                    return; // 用户取消
                }
            }

            if (targetLibrary == null)
            {
                return;
            }

            // 添加选中的文件/文件夹路径到库
            int successCount = 0;
            int failCount = 0;
            var failedItems = new List<string>();

            foreach (var item in selectedItems)
            {
                try
                {
                    // 对于文件夹，添加文件夹路径
                    // 对于文件，添加文件所在文件夹路径（或直接添加文件路径？）
                    // 根据 Windows 库的行为，应该是添加文件夹路径
                    string pathToAdd = item.IsDirectory ? item.Path : System.IO.Path.GetDirectoryName(item.Path);

                    // 检查路径是否已存在
                    var existingPaths = DatabaseManager.GetLibraryPaths(targetLibrary.Id);
                    if (!existingPaths.Any(p => p.Path.Equals(pathToAdd, StringComparison.OrdinalIgnoreCase)))
                    {
                        DatabaseManager.AddLibraryPath(targetLibrary.Id, pathToAdd);
                        successCount++;
                    }
                    else
                    {
                        // 路径已存在，跳过但不算失败
                        failCount++;
                        failedItems.Add($"{item.Name} (已存在于库中)");
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    failedItems.Add($"{item.Name} ({ex.Message})");
                }
            }

            // 不显示成功提示（减少提示框）
            // 如果有失败项，才显示错误提示
            if (failCount > 0 && successCount == 0)
            {
                var message = $"添加失败:\n{string.Join("\n", failedItems)}";
                MessageBox.Show(message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            // 如果当前在库模式且是当前库，刷新显示
            if (_currentLibrary != null && _currentLibrary.Id == targetLibrary.Id)
            {
                LoadLibraryFiles(_currentLibrary);
            }
        }


        private void WindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            _windowLifecycleHandler?.HandleMinimize();
        }



        internal void WindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            _windowLifecycleHandler?.HandleMaximize();
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            _windowLifecycleHandler?.HandleClose();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _windowLifecycleHandler?.HandleClosing(e);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _windowLifecycleHandler?.HandleSizeChanged(e);
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            _windowLifecycleHandler?.HandleLocationChanged(e);
        }

        internal void ListView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            _windowLifecycleHandler?.HandleListViewSizeChanged(e);
        }

        private void AdjustListViewColumnWidths()
        {
            _windowLifecycleHandler?.HandleListViewSizeChanged(null);
        }
        /// <summary>
        /// 调整列宽以适应窗口大小变化
        /// </summary>
        internal void AdjustColumnWidths()
        {
            _windowLifecycleHandler?.AdjustColumnWidths();
        }

        private void EnsureColumnMinWidths()
        {
            _windowLifecycleHandler?.EnsureColumnMinWidths();
        }

        public void UpdateWindowStateUI()
        {
            _windowLifecycleHandler?.UpdateWindowStateUI();
        }



        // NativeMethods has been moved to WindowLifecycleHandler
        internal void UpdateActionButtonsPosition()
        {
            // TitleActionBar已经自动处理按钮布局，不再需要手动调整位置
        }

        internal void UpdateSeparatorPosition()
        {
            // 更新分隔符位置，使其与列1和列2之间的分割器对齐
            if (TitleBarSeparator == null || ColLeft == null) return;

            try
            {
                // 获取包含分隔符的StackPanel
                var stackPanel = TitleBarSeparator.Parent as StackPanel;
                if (stackPanel == null) return;

                // 计算导航按钮的总宽度
                double navButtonsWidth = 0;
                foreach (var child in stackPanel.Children)
                {
                    if (child == TitleBarSeparator) break; // 遇到分隔符就停止
                    if (child is FrameworkElement fe)
                    {
                        // 使用ActualWidth，如果为0则使用DesiredSize
                        double width = fe.ActualWidth;
                        if (width <= 0)
                        {
                            fe.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                            width = fe.DesiredSize.Width;
                        }
                        navButtonsWidth += width;
                    }
                }

                // 计算分隔符应该的左边距
                // 目标：分隔符右边缘 = ColLeft右边缘
                // 分隔符右边缘 = StackPanel左边距 + 导航按钮宽度 + 分隔符左边距 + 分隔符宽度
                // 所以：分隔符左边距 = ColLeft宽度 - StackPanel左边距 - 导航按钮宽度 - 分隔符宽度

                double stackPanelLeftMargin = 8; // StackPanel的Margin="8,0"

                // 测量分隔符宽度
                TitleBarSeparator.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                double separatorWidth = TitleBarSeparator.ActualWidth > 0 ? TitleBarSeparator.ActualWidth : TitleBarSeparator.DesiredSize.Width;
                if (separatorWidth <= 0) separatorWidth = 1; // 默认宽度

                double targetSeparatorLeftMargin = ColLeft.ActualWidth - stackPanelLeftMargin - navButtonsWidth - separatorWidth;

                // 确保左边距不为负，最小值为0
                targetSeparatorLeftMargin = Math.Max(0, targetSeparatorLeftMargin);

                TitleBarSeparator.Margin = new Thickness(targetSeparatorLeftMargin, 0, 0, 0);
            }
            catch (Exception)
            {
            }
        }

        // 鼠标事件桥接方法 - 已迁移到 MouseEventHandler
        // 顶部标题栏鼠标按下：支持拖动窗口和双击最大化/还原


        // 右上角按钮容器的鼠标事件：非按钮区域也要支持拖动窗口
        private void WindowControlButtonsContainer_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _windowLifecycleHandler?.HandleControlButtonsMouseDown(e, sender);
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        #endregion

    }
}

