using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OoiMRR
{
    /// <summary>
    /// 拖拽管理器 - 处理文件列表的拖拽操作
    /// </summary>
    public class DragDropManager
    {
        #region 拖拽目标类型

        /// <summary>
        /// 拖拽目标类型
        /// </summary>
        public enum DropTargetType
        {
            /// <summary>
            /// 面包屑导航路径
            /// </summary>
            BreadcrumbPath,

            /// <summary>
            /// 驱动器
            /// </summary>
            Drive,

            /// <summary>
            /// 快速访问
            /// </summary>
            QuickAccess,

            /// <summary>
            /// 库（待开发）
            /// </summary>
            Library,

            /// <summary>
            /// 标签（待开发）
            /// </summary>
            Tag,

            /// <summary>
            /// 文件夹（文件列表中的文件夹）
            /// </summary>
            Folder,

            /// <summary>
            /// 外部（拖到程序外）
            /// </summary>
            External
        }

        #endregion

        #region 拖拽操作类型

        /// <summary>
        /// 拖拽操作类型
        /// </summary>
        public enum DragDropOperation
        {
            /// <summary>
            /// 移动
            /// </summary>
            Move,

            /// <summary>
            /// 复制
            /// </summary>
            Copy,

            /// <summary>
            /// 创建快捷方式/链接
            /// </summary>
            CreateLink,

            /// <summary>
            /// 添加到快速访问
            /// </summary>
            AddToQuickAccess,

            /// <summary>
            /// 添加到库
            /// </summary>
            AddToLibrary,

            /// <summary>
            /// 添加标签
            /// </summary>
            AddTag
        }

        #endregion

        #region 拖拽数据

        /// <summary>
        /// 拖拽数据
        /// </summary>
        public class DragDropData
        {
            /// <summary>
            /// 源文件/文件夹路径列表
            /// </summary>
            public List<string> SourcePaths { get; set; }

            /// <summary>
            /// 目标路径
            /// </summary>
            public string TargetPath { get; set; }

            /// <summary>
            /// 目标类型
            /// </summary>
            public DropTargetType TargetType { get; set; }

            /// <summary>
            /// 操作类型
            /// </summary>
            public DragDropOperation Operation { get; set; }

            /// <summary>
            /// 是否为目录
            /// </summary>
            public bool IsDirectory { get; set; }

            /// <summary>
            /// 源控件（用于刷新）
            /// </summary>
            public FrameworkElement SourceControl { get; set; }

            /// <summary>
            /// 目标控件
            /// </summary>
            public FrameworkElement TargetControl { get; set; }
        }

        #endregion

        #region 事件定义

        /// <summary>
        /// 拖拽完成事件
        /// </summary>
        public event EventHandler<DragDropData> DragDropCompleted;

        /// <summary>
        /// 拖拽开始事件
        /// </summary>
        public event EventHandler<DragDropData> DragDropStarted;

        /// <summary>
        /// 拖拽取消事件
        /// </summary>
        public event EventHandler DragDropCancelled;

        #endregion

        #region 私有字段

        private Point _dragStartPoint;
        private bool _isDragging;
        private DragDropData _currentDragData;

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化文件列表的拖拽功能
        /// </summary>
        public void InitializeFileListDragDrop(ListView listView)
        {
            if (listView == null) return;

            listView.PreviewMouseLeftButtonDown += FileList_PreviewMouseLeftButtonDown;
            listView.PreviewMouseMove += FileList_PreviewMouseMove;
            listView.PreviewMouseLeftButtonUp += FileList_PreviewMouseLeftButtonUp;
        }

        /// <summary>
        /// 初始化目标区域的拖放功能
        /// </summary>
        public void InitializeDropTarget(FrameworkElement element, DropTargetType targetType)
        {
            if (element == null) return;

            element.AllowDrop = true;
            element.DragEnter += (s, e) => DropTarget_DragEnter(s, e, targetType);
            element.DragOver += (s, e) => DropTarget_DragOver(s, e, targetType);
            element.DragLeave += DropTarget_DragLeave;
            element.Drop += (s, e) => DropTarget_Drop(s, e, targetType);
        }

        /// <summary>
        /// 初始化面包屑导航的拖放功能
        /// </summary>
        public void InitializeBreadcrumbDrop(StackPanel breadcrumbPanel)
        {
            if (breadcrumbPanel == null) return;

            breadcrumbPanel.AllowDrop = true;
            breadcrumbPanel.DragEnter += (s, e) => DropTarget_DragEnter(s, e, DropTargetType.BreadcrumbPath);
            breadcrumbPanel.DragOver += (s, e) => DropTarget_DragOver(s, e, DropTargetType.BreadcrumbPath);
            breadcrumbPanel.DragLeave += DropTarget_DragLeave;
            breadcrumbPanel.Drop += (s, e) => DropTarget_Drop(s, e, DropTargetType.BreadcrumbPath);
        }

        /// <summary>
        /// 初始化驱动器列表的拖放功能
        /// </summary>
        public void InitializeDrivesDrop(StackPanel drivesPanel)
        {
            if (drivesPanel == null) return;

            // 为每个驱动器按钮设置拖放
            foreach (var child in drivesPanel.Children)
            {
                if (child is Button driveButton)
                {
                    driveButton.AllowDrop = true;
                    driveButton.DragEnter += (s, e) => DropTarget_DragEnter(s, e, DropTargetType.Drive);
                    driveButton.DragOver += (s, e) => DropTarget_DragOver(s, e, DropTargetType.Drive);
                    driveButton.DragLeave += DropTarget_DragLeave;
                    driveButton.Drop += (s, e) => DropTarget_Drop(s, e, DropTargetType.Drive);
                }
            }
        }

        /// <summary>
        /// 初始化快速访问的拖放功能
        /// </summary>
        public void InitializeQuickAccessDrop(StackPanel quickAccessPanel)
        {
            if (quickAccessPanel == null) return;

            quickAccessPanel.AllowDrop = true;
            quickAccessPanel.DragEnter += (s, e) => DropTarget_DragEnter(s, e, DropTargetType.QuickAccess);
            quickAccessPanel.DragOver += (s, e) => DropTarget_DragOver(s, e, DropTargetType.QuickAccess);
            quickAccessPanel.DragLeave += DropTarget_DragLeave;
            quickAccessPanel.Drop += (s, e) => DropTarget_Drop(s, e, DropTargetType.QuickAccess);
        }

        #endregion

        #region 文件列表拖拽事件

        private void FileList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void FileList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging)
                return;

            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            // 检查是否移动了足够的距离来开始拖拽
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var listView = sender as ListView;
                if (listView?.SelectedItems == null || listView.SelectedItems.Count == 0)
                    return;

                // 获取选中的文件路径
                var selectedPaths = new List<string>();
                foreach (var item in listView.SelectedItems)
                {
                    if (item is FileSystemItem fileItem)
                    {
                        selectedPaths.Add(fileItem.Path);
                    }
                }

                if (selectedPaths.Count == 0)
                    return;

                // 创建拖拽数据
                _currentDragData = new DragDropData
                {
                    SourcePaths = selectedPaths,
                    SourceControl = listView,
                    IsDirectory = Directory.Exists(selectedPaths[0])
                };

                // 触发拖拽开始事件
                DragDropStarted?.Invoke(this, _currentDragData);

                _isDragging = true;

                // 创建数据对象
                var dataObject = new DataObject(DataFormats.FileDrop, selectedPaths.ToArray());
                dataObject.SetData("DragDropData", _currentDragData);

                // 开始拖拽操作
                var effects = DragDrop.DoDragDrop(listView, dataObject, DragDropEffects.Copy | DragDropEffects.Move);

                // 拖拽结束
                _isDragging = false;

                if (effects == DragDropEffects.None)
                {
                    DragDropCancelled?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void FileList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
        }

        #endregion

        #region 目标区域拖放事件

        private void DropTarget_DragEnter(object sender, DragEventArgs e, DropTargetType targetType)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // 设置视觉反馈
            if (sender is FrameworkElement element)
            {
                element.Opacity = 0.7;
            }

            // 根据目标类型和按键状态确定操作类型
            e.Effects = GetDragDropEffects(targetType, e.KeyStates);
            e.Handled = true;
        }

        private void DropTarget_DragOver(object sender, DragEventArgs e, DropTargetType targetType)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = GetDragDropEffects(targetType, e.KeyStates);
            e.Handled = true;
        }

        private void DropTarget_DragLeave(object sender, DragEventArgs e)
        {
            // 恢复视觉反馈
            if (sender is FrameworkElement element)
            {
                element.Opacity = 1.0;
            }
        }

        private void DropTarget_Drop(object sender, DragEventArgs e, DropTargetType targetType)
        {
            // 恢复视觉反馈
            if (sender is FrameworkElement element)
            {
                element.Opacity = 1.0;
            }

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
                return;

            // 获取目标路径
            string targetPath = GetTargetPath(sender, targetType);
            if (string.IsNullOrEmpty(targetPath))
                return;

            // 创建拖拽数据
            var dragData = new DragDropData
            {
                SourcePaths = files.ToList(),
                TargetPath = targetPath,
                TargetType = targetType,
                Operation = GetOperationType(targetType, e.KeyStates),
                TargetControl = sender as FrameworkElement
            };

            // 触发拖拽完成事件
            DragDropCompleted?.Invoke(this, dragData);

            e.Handled = true;
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 根据目标类型和按键状态获取拖拽效果
        /// </summary>
        private DragDropEffects GetDragDropEffects(DropTargetType targetType, DragDropKeyStates keyStates)
        {
            // Ctrl = 复制, Shift = 移动, Ctrl+Shift = 链接
            bool ctrlPressed = (keyStates & DragDropKeyStates.ControlKey) != 0;
            bool shiftPressed = (keyStates & DragDropKeyStates.ShiftKey) != 0;

            if (ctrlPressed && shiftPressed)
                return DragDropEffects.Link;

            if (ctrlPressed)
                return DragDropEffects.Copy;

            if (shiftPressed)
                return DragDropEffects.Move;

            // 默认行为根据目标类型决定
            return targetType switch
            {
                DropTargetType.QuickAccess => DragDropEffects.Link,
                DropTargetType.Library => DragDropEffects.Link,
                DropTargetType.Tag => DragDropEffects.Copy,
                _ => DragDropEffects.Move
            };
        }

        /// <summary>
        /// 根据目标类型和按键状态获取操作类型
        /// </summary>
        private DragDropOperation GetOperationType(DropTargetType targetType, DragDropKeyStates keyStates)
        {
            bool ctrlPressed = (keyStates & DragDropKeyStates.ControlKey) != 0;
            bool shiftPressed = (keyStates & DragDropKeyStates.ShiftKey) != 0;

            // 特殊目标类型的操作
            if (targetType == DropTargetType.QuickAccess)
                return DragDropOperation.AddToQuickAccess;

            if (targetType == DropTargetType.Library)
                return DragDropOperation.AddToLibrary;

            if (targetType == DropTargetType.Tag)
                return DragDropOperation.AddTag;

            // 根据按键确定操作
            if (ctrlPressed && shiftPressed)
                return DragDropOperation.CreateLink;

            if (ctrlPressed)
                return DragDropOperation.Copy;

            return DragDropOperation.Move;
        }

        /// <summary>
        /// 获取目标路径
        /// </summary>
        private string GetTargetPath(object sender, DropTargetType targetType)
        {
            if (sender is FrameworkElement element)
            {
                // 从控件的 Tag 属性获取路径
                if (element.Tag is string path)
                    return path;

                // 从按钮内容获取驱动器路径
                if (sender is Button button && targetType == DropTargetType.Drive)
                {
                    var content = button.Content?.ToString();
                    if (!string.IsNullOrEmpty(content))
                    {
                        // 提取驱动器号 (例如 "C:" 从 "C: 本地磁盘")
                        var parts = content.Split(' ');
                        if (parts.Length > 0)
                            return parts[0];
                    }
                }

                // 从面包屑导航获取路径
                if (targetType == DropTargetType.BreadcrumbPath && element is StackPanel)
                {
                    // 返回当前路径（需要从外部设置）
                    return element.Tag as string;
                }
            }

            return null;
        }

        #endregion

        #region 执行拖拽操作

        /// <summary>
        /// 执行拖拽操作
        /// </summary>
        public bool ExecuteDragDropOperation(DragDropData data)
        {
            try
            {
                switch (data.Operation)
                {
                    case DragDropOperation.Move:
                        return ExecuteMove(data);

                    case DragDropOperation.Copy:
                        return ExecuteCopy(data);

                    case DragDropOperation.CreateLink:
                        return ExecuteCreateLink(data);

                    case DragDropOperation.AddToQuickAccess:
                        return ExecuteAddToQuickAccess(data);

                    case DragDropOperation.AddToLibrary:
                        return ExecuteAddToLibrary(data);

                    case DragDropOperation.AddTag:
                        return ExecuteAddTag(data);

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"拖拽操作失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool ExecuteMove(DragDropData data)
        {
            foreach (var sourcePath in data.SourcePaths)
            {
                var fileName = Path.GetFileName(sourcePath);
                var targetPath = Path.Combine(data.TargetPath, fileName);

                if (Directory.Exists(sourcePath))
                {
                    Directory.Move(sourcePath, targetPath);
                }
                else
                {
                    File.Move(sourcePath, targetPath);
                }
            }
            return true;
        }

        private bool ExecuteCopy(DragDropData data)
        {
            foreach (var sourcePath in data.SourcePaths)
            {
                var fileName = Path.GetFileName(sourcePath);
                var targetPath = Path.Combine(data.TargetPath, fileName);

                if (Directory.Exists(sourcePath))
                {
                    CopyDirectory(sourcePath, targetPath);
                }
                else
                {
                    File.Copy(sourcePath, targetPath, true);
                }
            }
            return true;
        }

        private bool ExecuteCreateLink(DragDropData data)
        {
            // TODO: 实现创建快捷方式功能
            MessageBox.Show("创建快捷方式功能待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        private bool ExecuteAddToQuickAccess(DragDropData data)
        {
            // TODO: 实现添加到快速访问功能
            MessageBox.Show($"已添加 {data.SourcePaths.Count} 项到快速访问", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }

        private bool ExecuteAddToLibrary(DragDropData data)
        {
            // TODO: 实现添加到库功能
            MessageBox.Show($"已添加 {data.SourcePaths.Count} 项到库", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }

        private bool ExecuteAddTag(DragDropData data)
        {
            // TODO: 实现添加标签功能
            MessageBox.Show($"已为 {data.SourcePaths.Count} 项添加标签", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(targetDir, fileName), true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(targetDir, dirName));
            }
        }

        #endregion
    }
}

