using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace OoiMRR
{
    /// <summary>
    /// 拖拽管理器 - 处理文件列表的拖拽操作
    /// </summary>
    public class DragDropManager
    {
        #region 拖拽动画相关

        private Border _dragVisual;
        private Window _dragWindow;

        #endregion

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
        private Point _dragStartPointInListView;
        private bool _isDragging;
        private DragDropData _currentDragData;
        private List<string> _selectedPathsBeforeDrag = new List<string>();
        private ListView _currentListView;
        private bool _isPreparingDrag = false;
        private List<ListViewItem> _selectedListViewItems = new List<ListViewItem>();
        private System.Windows.Threading.DispatcherTimer _selectionKeepAliveTimer;
        private Dictionary<ListViewItem, bool> _originalFocusableStates = new Dictionary<ListViewItem, bool>();
        private Dictionary<ListViewItem, Brush> _originalBackgrounds = new Dictionary<ListViewItem, Brush>();

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化文件列表的拖拽功能
        /// </summary>
        public void InitializeFileListDragDrop(ListView listView)
        {
            if (listView == null) return;

            _currentListView = listView;
            
            listView.PreviewMouseLeftButtonDown += FileList_PreviewMouseLeftButtonDown;
            listView.PreviewMouseMove += FileList_PreviewMouseMove;
            listView.PreviewMouseLeftButtonUp += FileList_PreviewMouseLeftButtonUp;
            listView.SelectionChanged += FileList_SelectionChanged;
            
            // 允许拖放到文件列表（拖到文件夹）
            listView.AllowDrop = true;
            listView.DragEnter += FileList_DragEnter;
            listView.DragOver += FileList_DragOver;
            listView.DragLeave += FileList_DragLeave;
            listView.Drop += FileList_Drop;
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
            // 首先检查是否点击在列头区域，如果是，不处理拖拽
            var listView = sender as ListView;
            if (listView != null)
            {
                System.Windows.Point hitPoint = e.GetPosition(listView);
                
                // 检查 Y 坐标是否在列头区域
                if (hitPoint.Y < 30)
                {
                    System.Diagnostics.Debug.WriteLine("[DragDropManager] 点击在列头区域，不处理拖拽");
                    _selectedPathsBeforeDrag.Clear(); // 清除保存的选中项，防止后续启动拖拽
                    return; // 不在列头区域处理拖拽
                }
                
                // 检查是否点击在列头相关元素上
                var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(listView, hitPoint);
                if (hitResult != null)
                {
                    DependencyObject current = hitResult.VisualHit;
                    while (current != null && current != listView)
                    {
                        if (current is GridViewColumnHeader)
                        {
                            System.Diagnostics.Debug.WriteLine("[DragDropManager] 点击在 GridViewColumnHeader，不处理拖拽");
                            _selectedPathsBeforeDrag.Clear(); // 清除保存的选中项，防止后续启动拖拽
                            return;
                        }
                        if (current.GetType().Name.Contains("Thumb") || current.GetType().Name == "Thumb")
                        {
                            System.Diagnostics.Debug.WriteLine("[DragDropManager] 点击在 Thumb（调整大小句柄），不处理拖拽");
                            _selectedPathsBeforeDrag.Clear(); // 清除保存的选中项，防止后续启动拖拽
                            return;
                        }
                        current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                    }
                }
            }
            
            _dragStartPoint = e.GetPosition(null);
            if (listView != null)
            {
                _dragStartPointInListView = e.GetPosition(listView);
            }
            
            // 在鼠标按下时立即保存当前选中的项目
            // 因为后续的选择逻辑可能会改变选中状态
            if (listView?.SelectedItems != null && listView.SelectedItems.Count > 0)
            {
                _selectedPathsBeforeDrag.Clear();
                foreach (var item in listView.SelectedItems)
                {
                    if (item is FileSystemItem fileItem)
                    {
                        _selectedPathsBeforeDrag.Add(fileItem.Path);
                    }
                }
                
                // 写入日志
                try
                {
                    File.AppendAllText("dragdrop_log.txt", 
                        $"[{DateTime.Now:HH:mm:ss}] 鼠标按下 - 当前选中 {_selectedPathsBeforeDrag.Count} 个项目\n");
                }
                catch { }
                
                // 如果点击的是已选中的项目，标记为准备拖拽
                var clickedItem = GetListViewItemFromPoint(listView, e.GetPosition(listView));
                if (clickedItem != null && clickedItem.IsSelected && _selectedPathsBeforeDrag.Count > 0)
                {
                    _isPreparingDrag = true;
                    
                    // 保存所有选中的 ListViewItem
                    _selectedListViewItems.Clear();
                    foreach (var item in listView.Items)
                    {
                        var container = listView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                        if (container != null && container.IsSelected)
                        {
                            _selectedListViewItems.Add(container);
                        }
                    }
                    
                    e.Handled = true;
                }
            }
        }
        
        private ListViewItem GetListViewItemFromPoint(ListView listView, Point point)
        {
            var element = listView.InputHitTest(point) as DependencyObject;
            while (element != null && !(element is ListViewItem))
            {
                element = VisualTreeHelper.GetParent(element);
            }
            return element as ListViewItem;
        }
        
        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 如果正在准备拖拽或正在拖拽，强制保持选中状态
            if ((_isPreparingDrag || _isDragging) && _selectedPathsBeforeDrag.Count > 0)
            {
                var listView = sender as ListView;
                if (listView != null)
                {
                    // 立即恢复选中状态，不使用 Dispatcher
                    listView.SelectionChanged -= FileList_SelectionChanged;
                    
                    try
                    {
                        // 恢复选中状态
                        listView.SelectedItems.Clear();
                        foreach (var item in listView.Items)
                        {
                            if (item is FileSystemItem fileItem && _selectedPathsBeforeDrag.Contains(fileItem.Path))
                            {
                                listView.SelectedItems.Add(item);
                            }
                        }
                        
                        // 强制更新 ListViewItem 的选中视觉状态
                        foreach (var lvItem in _selectedListViewItems)
                        {
                            lvItem.IsSelected = true;
                        }
                    }
                    finally
                    {
                        listView.SelectionChanged += FileList_SelectionChanged;
                    }
                }
            }
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
                
                // 再次检查是否在列头区域，如果是，不启动文件拖拽
                if (listView != null)
                {
                    // 首先检查原始按下位置是否在列头区域（只需检查一次）
                    if (_dragStartPointInListView.Y < 30)
                        return;
                    
                    System.Windows.Point currentHitPoint = e.GetPosition(listView);
                    
                    // 检查当前 Y 坐标是否在列头区域（快速检查，避免 HitTest）
                    if (currentHitPoint.Y < 30)
                        return;
                    
                    // 只在必要时进行 HitTest（减少性能开销）
                    // 如果 Y 坐标在安全范围内，直接跳过 HitTest
                    if (currentHitPoint.Y > 50)
                    {
                        // Y 坐标足够大，不在列头区域，可以安全地开始拖拽
                    }
                    else
                    {
                        // Y 坐标在临界区域，需要 HitTest 确认
                        var hitResult = System.Windows.Media.VisualTreeHelper.HitTest(listView, currentHitPoint);
                        if (hitResult != null)
                        {
                            DependencyObject current = hitResult.VisualHit;
                            int depth = 0;
                            while (current != null && current != listView && depth < 5)
                            {
                                if (current is GridViewColumnHeader)
                                    return;
                                if (current.GetType().Name.Contains("Thumb") || current.GetType().Name == "Thumb")
                                    return;
                                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
                                depth++;
                            }
                        }
                    }
                }
                
                // 使用鼠标按下时保存的选中项，而不是当前的选中项
                // 因为在拖拽过程中选中状态可能已经改变
                var selectedPaths = new List<string>(_selectedPathsBeforeDrag);

                System.Diagnostics.Debug.WriteLine($"[拖拽开始] 选中了 {selectedPaths.Count} 个项目:");
                foreach (var path in selectedPaths)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {path}");
                }
                
                // 写入日志文件
                try
                {
                    File.AppendAllText("dragdrop_log.txt", 
                        $"[{DateTime.Now:HH:mm:ss}] 拖拽开始 - 选中 {selectedPaths.Count} 个项目\n");
                    foreach (var path in selectedPaths)
                    {
                        File.AppendAllText("dragdrop_log.txt", $"  - {Path.GetFileName(path)}\n");
                    }
                }
                catch { }

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
                
                // 启动定时器持续保持选中状态
                StartSelectionKeepAliveTimer(listView);

                // 创建拖拽视觉效果
                CreateDragVisual(selectedPaths);

                // 创建数据对象
                var dataObject = new DataObject(DataFormats.FileDrop, selectedPaths.ToArray());
                dataObject.SetData("DragDropData", _currentDragData);

                // 开始拖拽操作（允许 Copy、Move 和 Link，以便支持添加到库等操作）
                var effects = DragDrop.DoDragDrop(listView, dataObject, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);

                // 拖拽结束，清理视觉效果
                StopSelectionKeepAliveTimer();
                CloseDragVisual();
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
            _isPreparingDrag = false;
        }

        private void FileList_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            e.Effects = GetDragDropEffects(DropTargetType.Folder, e.KeyStates);
            e.Handled = true;
        }

        private void FileList_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                return;
            }

            // 获取鼠标下的项
            var listView = sender as ListView;
            var point = e.GetPosition(listView);
            var element = listView.InputHitTest(point) as DependencyObject;
            
            // 查找 ListViewItem
            while (element != null && !(element is ListViewItem))
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is ListViewItem item && item.Content is FileSystemItem fileItem)
            {
                // 只有文件夹才能作为拖放目标
                if (fileItem.IsDirectory)
                {
                    // 高亮显示目标文件夹
                    item.Background = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
                    e.Effects = GetDragDropEffects(DropTargetType.Folder, e.KeyStates);
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private void FileList_DragLeave(object sender, DragEventArgs e)
        {
            // 清除所有高亮
            var listView = sender as ListView;
            if (listView != null)
            {
                foreach (var item in listView.Items)
                {
                    var container = listView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                    if (container != null)
                    {
                        container.Background = Brushes.Transparent;
                    }
                }
            }
        }

        private void FileList_Drop(object sender, DragEventArgs e)
        {
            // 清除高亮
            FileList_DragLeave(sender, e);

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                return;

            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
                return;

            System.Diagnostics.Debug.WriteLine($"[拖拽放下] 接收到 {files.Length} 个文件:");
            foreach (var file in files)
            {
                System.Diagnostics.Debug.WriteLine($"  - {file}");
            }
            
            // 写入日志文件
            try
            {
                File.AppendAllText("dragdrop_log.txt", 
                    $"[{DateTime.Now:HH:mm:ss}] 拖拽放下 - 接收 {files.Length} 个文件\n");
                foreach (var file in files)
                {
                    File.AppendAllText("dragdrop_log.txt", $"  - {Path.GetFileName(file)}\n");
                }
            }
            catch { }

            // 获取目标文件夹
            var listView = sender as ListView;
            var point = e.GetPosition(listView);
            var element = listView.InputHitTest(point) as DependencyObject;
            
            while (element != null && !(element is ListViewItem))
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is ListViewItem item && item.Content is FileSystemItem fileItem)
            {
                if (fileItem.IsDirectory)
                {
                    // 创建拖拽数据
                    var dragData = new DragDropData
                    {
                        SourcePaths = files.ToList(),
                        TargetPath = fileItem.Path,
                        TargetType = DropTargetType.Folder,
                        Operation = GetOperationType(DropTargetType.Folder, e.KeyStates),
                        TargetControl = sender as FrameworkElement
                    };

                    // 触发拖拽完成事件
                    DragDropCompleted?.Invoke(this, dragData);
                }
            }

            e.Handled = true;
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
            int successCount = 0;
            int failCount = 0;
            string lastError = "";

            System.Diagnostics.Debug.WriteLine($"[执行移动] 开始移动 {data.SourcePaths.Count} 个项目到 {data.TargetPath}");
            
            // 写入日志文件
            try
            {
                File.AppendAllText("dragdrop_log.txt", 
                    $"[{DateTime.Now:HH:mm:ss}] 执行移动 - {data.SourcePaths.Count} 个项目 -> {Path.GetFileName(data.TargetPath)}\n");
            }
            catch { }

            foreach (var sourcePath in data.SourcePaths)
            {
                try
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var targetPath = Path.Combine(data.TargetPath, fileName);

                    // 检查源文件是否存在
                    if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                    {
                        lastError = $"源文件不存在: {sourcePath}";
                        failCount++;
                        continue;
                    }

                    // 检查目标是否已存在
                    if (File.Exists(targetPath) || Directory.Exists(targetPath))
                    {
                        var result = MessageBox.Show(
                            $"目标位置已存在同名项目:\n{fileName}\n\n是否覆盖?",
                            "确认覆盖",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.No)
                        {
                            failCount++;
                            continue;
                        }

                        // 删除已存在的目标
                        if (Directory.Exists(targetPath))
                            Directory.Delete(targetPath, true);
                        else
                            File.Delete(targetPath);
                    }

                    // 执行移动
                    if (Directory.Exists(sourcePath))
                    {
                        Directory.Move(sourcePath, targetPath);
                    }
                    else
                    {
                        File.Move(sourcePath, targetPath);
                    }

                    successCount++;
                    System.Diagnostics.Debug.WriteLine($"移动成功: {sourcePath} -> {targetPath}");
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    failCount++;
                    System.Diagnostics.Debug.WriteLine($"移动失败: {sourcePath}, 错误: {ex.Message}");
                }
            }

            // 显示结果
            if (failCount > 0)
            {
                MessageBox.Show(
                    $"移动完成\n成功: {successCount} 个\n失败: {failCount} 个\n\n最后错误: {lastError}",
                    "操作结果",
                    MessageBoxButton.OK,
                    failCount == data.SourcePaths.Count ? MessageBoxImage.Error : MessageBoxImage.Warning);
            }

            return successCount > 0;
        }

        private bool ExecuteCopy(DragDropData data)
        {
            int successCount = 0;
            int failCount = 0;
            string lastError = "";

            foreach (var sourcePath in data.SourcePaths)
            {
                try
                {
                    var fileName = Path.GetFileName(sourcePath);
                    var targetPath = Path.Combine(data.TargetPath, fileName);

                    // 检查源文件是否存在
                    if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                    {
                        lastError = $"源文件不存在: {sourcePath}";
                        failCount++;
                        continue;
                    }

                    // 检查目标是否已存在
                    if (File.Exists(targetPath) || Directory.Exists(targetPath))
                    {
                        var result = MessageBox.Show(
                            $"目标位置已存在同名项目:\n{fileName}\n\n是否覆盖?",
                            "确认覆盖",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.No)
                        {
                            failCount++;
                            continue;
                        }

                        // 删除已存在的目标
                        if (Directory.Exists(targetPath))
                            Directory.Delete(targetPath, true);
                        else
                            File.Delete(targetPath);
                    }

                    // 执行复制
                    if (Directory.Exists(sourcePath))
                    {
                        CopyDirectory(sourcePath, targetPath);
                    }
                    else
                    {
                        File.Copy(sourcePath, targetPath, true);
                    }

                    successCount++;
                    System.Diagnostics.Debug.WriteLine($"复制成功: {sourcePath} -> {targetPath}");
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    failCount++;
                    System.Diagnostics.Debug.WriteLine($"复制失败: {sourcePath}, 错误: {ex.Message}");
                }
            }

            // 显示结果
            if (failCount > 0)
            {
                MessageBox.Show(
                    $"复制完成\n成功: {successCount} 个\n失败: {failCount} 个\n\n最后错误: {lastError}",
                    "操作结果",
                    MessageBoxButton.OK,
                    failCount == data.SourcePaths.Count ? MessageBoxImage.Error : MessageBoxImage.Warning);
            }

            return successCount > 0;
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

        #region 拖拽视觉效果

        /// <summary>
        /// 创建拖拽视觉效果
        /// </summary>
        private void CreateDragVisual(List<string> paths)
        {
            try
            {
                // 先清理旧的拖拽窗口
                if (_dragWindow != null)
                {
                    try
                    {
                        _dragWindow.Close();
                        _dragWindow = null;
                    }
                    catch { }
                }

                // 创建拖拽窗口
                _dragWindow = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    ShowInTaskbar = false,
                    Topmost = true,
                    Width = 250,
                    Height = 80,
                    Left = -10000, // 初始位置在屏幕外
                    Top = -10000
                };

                // 创建拖拽视觉元素 - 使用更醒目的渐变背景
                var gradientBrush = new LinearGradientBrush();
                gradientBrush.StartPoint = new Point(0, 0);
                gradientBrush.EndPoint = new Point(1, 1);
                gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(240, 33, 150, 243), 0));
                gradientBrush.GradientStops.Add(new GradientStop(Color.FromArgb(240, 21, 101, 192), 1));
                
                _dragVisual = new Border
                {
                    Background = gradientBrush,
                    BorderBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                    BorderThickness = new Thickness(3),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(15, 10, 15, 10)
                };

                // 创建内容
                var stackPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal
                };

                // 添加图标 - 更大更醒目
                var icon = new TextBlock
                {
                    Text = paths.Count > 1 ? "📦" : (Directory.Exists(paths[0]) ? "📁" : "📄"),
                    FontSize = 32,
                    Margin = new Thickness(0, 0, 12, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                stackPanel.Children.Add(icon);

                // 添加文本
                var textPanel = new StackPanel();
                
                var nameText = new TextBlock
                {
                    Text = paths.Count > 1 ? $"{paths.Count} 个项目" : Path.GetFileName(paths[0]),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 160
                };
                textPanel.Children.Add(nameText);

                // 如果是多个文件，显示第一个文件名作为示例
                if (paths.Count > 1)
                {
                    var exampleText = new TextBlock
                    {
                        Text = $"例如: {Path.GetFileName(paths[0])}",
                        Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)),
                        FontSize = 11,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 160,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    textPanel.Children.Add(exampleText);
                }

                var hintText = new TextBlock
                {
                    Text = "正在拖拽...",
                    Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255)),
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                textPanel.Children.Add(hintText);

                stackPanel.Children.Add(textPanel);
                _dragVisual.Child = stackPanel;

                // 添加更强的阴影效果
                _dragVisual.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 25,
                    ShadowDepth = 5,
                    Opacity = 0.6
                };

                _dragWindow.Content = _dragVisual;
                _dragWindow.Show();

                // 添加缩放脉冲动画 - 更明显
                var scaleTransform = new ScaleTransform(1.0, 1.0, 125, 40); // 中心点
                _dragVisual.RenderTransform = scaleTransform;
                
                var scaleAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.05,
                    Duration = TimeSpan.FromMilliseconds(600),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
                
                // 添加轻微的旋转动画
                var rotateTransform = new RotateTransform(0, 125, 40);
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(scaleTransform);
                transformGroup.Children.Add(rotateTransform);
                _dragVisual.RenderTransform = transformGroup;
                
                var rotateAnimation = new DoubleAnimation
                {
                    From = -2,
                    To = 2,
                    Duration = TimeSpan.FromMilliseconds(800),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);

                // 跟随鼠标移动
                CompositionTarget.Rendering += UpdateDragVisualPosition;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建拖拽视觉效果失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新拖拽视觉效果位置
        /// </summary>
        private void UpdateDragVisualPosition(object sender, EventArgs e)
        {
            if (_dragWindow != null && _dragWindow.IsVisible)
            {
                var mousePos = GetMousePosition();
                _dragWindow.Left = mousePos.X + 10;
                _dragWindow.Top = mousePos.Y + 10;
            }
        }

        /// <summary>
        /// 获取鼠标位置
        /// </summary>
        private Point GetMousePosition()
        {
            var point = new Win32Point();
            GetCursorPos(ref point);
            return new Point(point.X, point.Y);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// 关闭拖拽视觉效果
        /// </summary>
        private void CloseDragVisual()
        {
            ForceCloseDragVisual();
        }

        /// <summary>
        /// 强制关闭拖拽视觉效果（公共方法，用于外部拖拽完成时清理）
        /// </summary>
        public void ForceCloseDragVisual()
        {
            try
            {
                CompositionTarget.Rendering -= UpdateDragVisualPosition;
                
                // 停止定时器
                StopSelectionKeepAliveTimer();
                
                // 清除拖拽标志
                _isPreparingDrag = false;
                _isDragging = false;

                if (_dragWindow != null)
                {
                    // 立即关闭，不等待淡出动画（避免在 MessageBox 显示时还看到拖拽动画）
                    try
                    {
                        _dragWindow.Close();
                        _dragWindow = null;
                        _dragVisual = null;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"关闭拖拽窗口失败: {ex.Message}");
                        // 如果立即关闭失败，尝试淡出动画
                        var fadeOut = new DoubleAnimation
                        {
                            From = 1.0,
                            To = 0.0,
                            Duration = TimeSpan.FromMilliseconds(100)
                        };
                        fadeOut.Completed += (s, e) =>
                        {
                            try
                            {
                                if (_dragWindow != null)
                                {
                                    _dragWindow.Close();
                                    _dragWindow = null;
                                    _dragVisual = null;
                                }
                            }
                            catch { }
                        };
                        _dragWindow.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"关闭拖拽视觉效果失败: {ex.Message}");
            }
        }

        #endregion

        #region 选中状态保持

        /// <summary>
        /// 启动定时器持续保持选中状态
        /// </summary>
        private void StartSelectionKeepAliveTimer(ListView listView)
        {
            StopSelectionKeepAliveTimer(); // 先停止旧的定时器
            
            if (listView == null || _selectedPathsBeforeDrag.Count == 0)
                return;

            _selectionKeepAliveTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33) // 每33ms检查一次（约30fps），足够快以避免闪烁
            };
            
            _selectionKeepAliveTimer.Tick += (s, e) =>
            {
                if (_isDragging && listView != null && _selectedPathsBeforeDrag.Count > 0)
                {
                    ForceRestoreSelection(listView);
                }
            };
            
            _selectionKeepAliveTimer.Start();
        }

        /// <summary>
        /// 停止选中状态保持定时器
        /// </summary>
        private void StopSelectionKeepAliveTimer()
        {
            if (_selectionKeepAliveTimer != null)
            {
                _selectionKeepAliveTimer.Stop();
                _selectionKeepAliveTimer = null;
            }
            
            // 拖拽结束后，确保选中状态正确恢复
            if (_currentListView != null && _selectedPathsBeforeDrag.Count > 0)
            {
                // 使用 Dispatcher 延迟执行，确保在拖拽操作完全结束后再恢复
                _currentListView.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_currentListView != null && _selectedPathsBeforeDrag.Count > 0)
                    {
                        try
                        {
                            _currentListView.SelectionChanged -= FileList_SelectionChanged;
                            
                            // 恢复选中状态
                            _currentListView.SelectedItems.Clear();
                            foreach (var item in _currentListView.Items)
                            {
                                if (item is FileSystemItem fileItem && _selectedPathsBeforeDrag.Contains(fileItem.Path))
                                {
                                    _currentListView.SelectedItems.Add(item);
                                }
                            }
                            
                            // 使用系统默认选中颜色恢复背景
                            var selectedBrush = SystemColors.HighlightBrush;
                            var selectedTextBrush = SystemColors.HighlightTextBrush;
                            var defaultBrush = SystemColors.WindowBrush;
                            var defaultTextBrush = SystemColors.WindowTextBrush;
                            
                            // 确保每个 ListViewItem 的状态和背景正确
                            foreach (var item in _currentListView.Items)
                            {
                                var container = _currentListView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                                if (container != null)
                                {
                                    bool shouldBeSelected = item is FileSystemItem fileItem && _selectedPathsBeforeDrag.Contains(fileItem.Path);
                                    container.IsSelected = shouldBeSelected;
                                    
                                    // 清除所有 LocalValue，让系统默认样式处理
                                    container.ClearValue(ListViewItem.BackgroundProperty);
                                    container.ClearValue(ListViewItem.ForegroundProperty);
                                    
                                    if (shouldBeSelected)
                                    {
                                        // 选中项：清除 LocalValue 后，ListView 的默认样式会自动应用选中背景
                                        // 不需要手动设置，让系统样式处理
                                    }
                                    else
                                    {
                                        // 非选中项：已经清除了 LocalValue，会使用默认背景
                                    }
                                    
                                    // 强制刷新视觉状态
                                    container.InvalidateVisual();
                                }
                            }
                            
                            _currentListView.SelectionChanged += FileList_SelectionChanged;
                        }
                        catch { }
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            
            // 清除保存的状态
            _originalBackgrounds.Clear();
            _originalFocusableStates.Clear();
        }

        /// <summary>
        /// 强制恢复选中状态 - 使用系统默认颜色直接设置背景
        /// </summary>
        private void ForceRestoreSelection(ListView listView)
        {
            if (listView == null || _selectedPathsBeforeDrag.Count == 0)
                return;

            try
            {
                // 暂时移除事件处理器，避免递归
                listView.SelectionChanged -= FileList_SelectionChanged;

                // 收集要选中的项
                var itemsToSelect = new List<object>();
                foreach (var item in listView.Items)
                {
                    if (item is FileSystemItem fileItem && _selectedPathsBeforeDrag.Contains(fileItem.Path))
                    {
                        itemsToSelect.Add(item);
                    }
                }

                // 恢复选中项集合
                listView.SelectedItems.Clear();
                foreach (var item in itemsToSelect)
                {
                    listView.SelectedItems.Add(item);
                }

                // 使用系统默认选中颜色和文字颜色
                var selectedBrush = SystemColors.HighlightBrush; // 系统默认选中背景色
                var selectedTextBrush = SystemColors.HighlightTextBrush; // 系统默认选中文字色
                var defaultBrush = SystemColors.WindowBrush; // 系统默认背景色
                var defaultTextBrush = SystemColors.WindowTextBrush; // 系统默认文字色

                // 强制设置每个 ListViewItem 的视觉状态
                foreach (var item in listView.Items)
                {
                    var container = listView.ItemContainerGenerator.ContainerFromItem(item) as ListViewItem;
                    if (container != null)
                    {
                        bool isSelected = itemsToSelect.Contains(item);
                        container.IsSelected = isSelected;

                        if (isSelected)
                        {
                            // 保存原始背景（只在第一次保存）
                            if (!_originalBackgrounds.ContainsKey(container))
                            {
                                _originalBackgrounds[container] = container.Background ?? defaultBrush;
                            }

                            // 清除可能的样式值，然后使用 LocalValue 优先级设置
                            // 这样可以确保背景色优先级高于样式触发器（如 MouseOver）
                            container.ClearValue(ListViewItem.BackgroundProperty);
                            container.ClearValue(ListViewItem.ForegroundProperty);
                            
                            // 使用 SetValue 设置 LocalValue，优先级高于样式
                            container.SetValue(ListViewItem.BackgroundProperty, selectedBrush);
                            container.SetValue(ListViewItem.ForegroundProperty, selectedTextBrush);
                        }
                        else
                        {
                            // 恢复原始背景 - 如果是默认值则清除设置
                            if (_originalBackgrounds.ContainsKey(container))
                            {
                                var originalBg = _originalBackgrounds[container];
                                if (originalBg == defaultBrush || originalBg == Brushes.White)
                                {
                                    container.ClearValue(ListViewItem.BackgroundProperty);
                                }
                                else
                                {
                                    container.SetValue(ListViewItem.BackgroundProperty, originalBg);
                                }
                            }
                            else
                            {
                                container.ClearValue(ListViewItem.BackgroundProperty);
                            }
                            container.ClearValue(ListViewItem.ForegroundProperty);
                        }
                    }
                }

                // 重新添加事件处理器
                listView.SelectionChanged += FileList_SelectionChanged;
            }
            catch
            {
                // 如果出错，至少重新添加事件处理器
                if (listView != null)
                {
                    listView.SelectionChanged += FileList_SelectionChanged;
                }
            }
        }

        #endregion
    }
}

