using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using YiboFile.Models.UI;
using YiboFile.Controls;
using YiboFile.Services.FileOperations.Undo;
using YiboFile.Services.UI;

namespace YiboFile.Services
{
    /// <summary>
    /// Simplified Drag & Drop Manager using standard WPF mechanisms.
    /// handles dragging files from the file list and dropping them onto folders or other supported targets.
    /// </summary>
    public class DragDropManager
    {
        private Point _dragStartPoint;
        private bool _canInitiateDrag;
        private bool _isDragging;
        private ListView _associatedListView;
        private DragDropFeedbackAdorner _feedbackAdorner;
        private AdornerLayer _adornerLayer;

        // Delegate for refreshing the UI after a file operation
        public Action RequestRefresh { get; set; }

        // Delegate to get the current path for background drops
        public Func<string> GetCurrentPath { get; set; }

        // UndoService for recording undoable operations
        public UndoService UndoService { get; set; }

        // TaskQueueService for showing progress
        public FileOperations.TaskQueue.TaskQueueService TaskQueueService { get; set; }

        public DragDropManager()
        {
        }

        /// <summary>
        /// Initialize drag and drop for the main file list view.
        /// </summary>
        /// <param name="listView">The file list view.</param>
        public void InitializeFileListDragDrop(ListView listView)
        {
            if (listView == null) return;
            _associatedListView = listView;

            // Drag Source Events
            listView.PreviewMouseLeftButtonDown += ListView_PreviewMouseLeftButtonDown;
            listView.PreviewMouseLeftButtonUp += ListView_PreviewMouseLeftButtonUp;
            listView.PreviewMouseMove += ListView_PreviewMouseMove;

            // Drop Target Events (for dropping INTO the list - e.g. from external source or moving to a subfolder)
            listView.AllowDrop = true;
            listView.Drop += ListView_Drop;
            listView.DragOver += ListView_DragOver;
            listView.DragLeave += ListView_DragLeave;
        }

        private void ListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _canInitiateDrag = false;
        }

        private void ListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            if (sender is ListView listView)
            {
                _canInitiateDrag = GetItemAtLocation(listView, e.GetPosition(listView)) != null;
            }
        }

        private void ListView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            // 只有当点击在项目上时才允许拖放，否则可能是框选
            if (!_canInitiateDrag) return;

            Point currentPosition = e.GetPosition(null);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var listView = sender as ListView;
                if (listView != null)
                {
                    StartDrag(listView);
                }
            }
        }

        private void StartDrag(ListView listView)
        {
            if (listView == null)
            {
                return;
            }

            var selectedItems = listView.SelectedItems.Cast<object>().ToList(); if (selectedItems.Count == 0) return;

            // 如果选中的项目正在重命名，不启动拖放（允许TextBox中的文本选择）
            foreach (var item in selectedItems)
            {
                if (item is FileSystemItem fsItem && fsItem.IsRenaming)
                {
                    return;
                }
            }

            var filePaths = new List<string>();
            foreach (var item in selectedItems)
            {
                if (item is FileSystemItem fsItem)
                {
                    // 核心修复：只有物理路径才允许启动文件系统拖拽
                    // 如果是虚拟协议路径，需要先通过 ProtocolManager 解析（逻辑待后续增强）
                    if (string.IsNullOrEmpty(fsItem.Path) ||
                        fsItem.Path.StartsWith("lib://", StringComparison.OrdinalIgnoreCase) ||
                        fsItem.Path.StartsWith("tag://", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (File.Exists(fsItem.Path) || Directory.Exists(fsItem.Path))
                    {
                        filePaths.Add(fsItem.Path);
                    }
                }
            }

            if (filePaths.Count > 0)
            {
                _isDragging = true;

                // [已移除] ShowSourceDragFeedback(listView, filePaths);

                try
                {
                    DataObject dataObject = new DataObject();
                    var collection = new System.Collections.Specialized.StringCollection();
                    collection.AddRange(filePaths.ToArray());
                    dataObject.SetFileDropList(collection);

                    // 同时也设置文本格式，以备某些编辑器需要
                    dataObject.SetText(string.Join(Environment.NewLine, filePaths));

                    DragDropEffects allowedEffects = DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;

                    // 使用线程池或异步包裹可能更安全，但 DoDragDrop 本身是阻塞的。
                    // 这里我们确保在 UI 线程上，且数据对象是纯物理路径。
                    DragDrop.DoDragDrop(listView, dataObject, allowedEffects);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DragDrop failed: {ex.Message}");
                }
                finally
                {
                    // [已移除] RemoveSourceDragFeedback();
                    _isDragging = false;

                    // 关键修复：强制释放鼠标捕获，解决拖拽后UI无法交互的问题
                    if (listView != null && listView.IsMouseCaptured)
                    {
                        listView.ReleaseMouseCapture();
                    }
                    if (Mouse.Captured == listView)
                    {
                        Mouse.Capture(null);
                    }
                }
            }
        }

        private ListViewItem _lastTargetItem;
        private Point _lastDragOverPoint;
        private long _lastDragCheckTicks;
        private bool _isLastTargetDirectory;

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 修饰键优先级: Alt=快捷方式 > Ctrl=复制 > 默认=移动
                DragDropEffects effect = GetDragDropEffect(e.KeyStates);

                Point currentPos = e.GetPosition(_associatedListView);
                long currentTicks = DateTime.Now.Ticks;

                // Throttle HitTesting: Check only if moved > 5 pixels or elapsed > 50ms
                bool shouldHitTest = (_lastTargetItem == null) ||
                                     (Math.Abs(currentPos.X - _lastDragOverPoint.X) > 5 || Math.Abs(currentPos.Y - _lastDragOverPoint.Y) > 5) ||
                                     (currentTicks - _lastDragCheckTicks > 500000); // 50ms

                if (shouldHitTest)
                {
                    _lastDragOverPoint = currentPos;
                    _lastDragCheckTicks = currentTicks;

                    // 检查是否悬停在目录上
                    var targetItem = GetItemAtLocation(_associatedListView, currentPos);

                    // 更新高亮状态
                    if (targetItem != _lastTargetItem)
                    {
                        if (_lastTargetItem != null)
                        {
                            DragAttachedProperties.SetIsDragTarget(_lastTargetItem, false);
                        }
                        _lastTargetItem = targetItem;

                        if (_lastTargetItem != null && _lastTargetItem.Content is FileSystemItem fsItem && fsItem.IsDirectory)
                        {
                            _isLastTargetDirectory = true;
                            DragAttachedProperties.SetIsDragTarget(_lastTargetItem, true);
                        }
                        else
                        {
                            _isLastTargetDirectory = false;
                            if (_lastTargetItem != null) DragAttachedProperties.SetIsDragTarget(_lastTargetItem, false);
                        }
                    }
                }

                // Apply effects based on cached state
                if (_isLastTargetDirectory)
                {
                    e.Effects = effect;
                }
                else
                {
                    // 核心修复：如果不是悬停在目录上，只有当拖放到空白处（此时 _lastTargetItem 为 null）
                    // 才会允许拖放（使用当前文件夹路径）。如果明确悬停在一个普通文件上，禁止拖放。
                    if (_lastTargetItem != null)
                    {
                        e.Effects = DragDropEffects.None;
                    }
                    else
                    {
                        e.Effects = effect;
                    }
                }

                // 更新视觉反馈提示 (Let the adornment logic handle visual throttling if needed, or we throttle here)
                // We update position every frame for smoothness, but text only changes with effect
                UpdateDragFeedback(e, effect);

                e.Handled = true;
            }
            else
            {
                ClearDragTargetHighlight();
                e.Effects = DragDropEffects.None;
                RemoveDragFeedback();
                e.Handled = true;
            }
        }

        private void ClearDragTargetHighlight()
        {
            if (_lastTargetItem != null)
            {
                DragAttachedProperties.SetIsDragTarget(_lastTargetItem, false);
                _lastTargetItem = null;
            }
        }

        private void ListView_DragLeave(object sender, DragEventArgs e)
        {
            RemoveDragFeedback();
            ClearDragTargetHighlight();
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            RemoveDragFeedback();
            ClearDragTargetHighlight();

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string[] sources = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (sources == null || sources.Length == 0)
            {
                return;
            }
            // Determine Target
            string targetPath = null;

            // Hit test for specific folder
            var targetItem = GetItemAtLocation(_associatedListView, e.GetPosition(_associatedListView));
            if (targetItem != null && targetItem.Content is FileSystemItem fsItem && fsItem.IsDirectory)
            {
                targetPath = fsItem.Path;
            }
            else
            {
                // Dropped on whitespace. Use current path if available.
                targetPath = GetCurrentPath?.Invoke();
            }

            if (string.IsNullOrEmpty(targetPath))
            {
                // If can't determine target, cancel.
                return;
            }

            // 确定操作类型: Alt=快捷方式, Ctrl=复制, Shift/默认=移动
            DragDropEffects effect = GetDragDropEffect(e.KeyStates);
            bool isCopy = effect == DragDropEffects.Copy;
            bool isLink = effect == DragDropEffects.Link;

            if (isLink)
            {
                // 创建快捷方式
                CreateShortcuts(sources, targetPath);
            }
            else
            {
                PerformFileOperation(sources, targetPath, isCopy);
            }
        }

        public async void PerformFileOperation(string[] sources, string targetFolder, bool isCopy)
        {
            // Create and enqueue task for progress display
            var task = new FileOperations.TaskQueue.FileOperationTask
            {
                Description = isCopy ? "复制文件" : "移动文件",
                TotalItems = sources.Length,
                Status = FileOperations.TaskQueue.TaskStatus.Running,
                CurrentFile = "准备中..."
            };
            TaskQueueService?.EnqueueTask(task);

            // Run file operations on background thread to prevent UI freeze
            await Task.Run(() =>
            {
                int successCount = 0;
                int processedCount = 0;

                try
                {
                    foreach (var source in sources)
                    {
                        // Check cancellation
                        if (task.Status == FileOperations.TaskQueue.TaskStatus.Canceling)
                        {
                            task.Status = FileOperations.TaskQueue.TaskStatus.Canceled;
                            break;
                        }
                        task.WaitIfPaused();

                        if (!File.Exists(source) && !Directory.Exists(source))
                        {
                            processedCount++;
                            continue;
                        }

                        var fileName = Path.GetFileName(source);
                        task.CurrentFile = fileName;

                        var destPath = Path.Combine(targetFolder, fileName);
                        bool isSameFolder = string.Equals(Path.GetDirectoryName(source), targetFolder, StringComparison.OrdinalIgnoreCase);

                        // If Moving to same folder, skip
                        if (!isCopy && isSameFolder)
                        {
                            processedCount++;
                            task.Progress = (int)((double)processedCount / sources.Length * 100);
                            continue;
                        }

                        // If destination exists, handle conflict
                        if (File.Exists(destPath) || Directory.Exists(destPath))
                        {
                            if (isCopy)
                            {
                                string newName = GetUniqueName(targetFolder, fileName);
                                destPath = Path.Combine(targetFolder, newName);
                            }
                            else
                            {
                                processedCount++;
                                task.Progress = (int)((double)processedCount / sources.Length * 100);
                                continue;
                            }
                        }

                        if (isCopy)
                        {
                            if (Directory.Exists(source))
                            {
                                CopyDirectory(source, destPath);
                            }
                            else
                            {
                                File.Copy(source, destPath, overwrite: false);
                            }
                            UndoService?.RecordAction(new NewFileUndoAction(destPath, Directory.Exists(destPath)));
                        }
                        else // Move
                        {
                            bool isDirectory = Directory.Exists(source);
                            try
                            {
                                if (isDirectory)
                                    Directory.Move(source, destPath);
                                else
                                    File.Move(source, destPath);
                            }
                            catch (IOException)
                            {
                                // Cross-drive move: copy then delete
                                if (isDirectory)
                                {
                                    CopyDirectory(source, destPath);
                                    Directory.Delete(source, true);
                                }
                                else
                                {
                                    File.Copy(source, destPath, true);
                                    File.Delete(source);
                                }
                            }
                            UndoService?.RecordAction(new MoveUndoAction(source, destPath, isDirectory));
                        }

                        successCount++;
                        processedCount++;
                        task.Progress = (int)((double)processedCount / sources.Length * 100);
                    }

                    // Update task status
                    if (task.Status != FileOperations.TaskQueue.TaskStatus.Canceled)
                    {
                        task.Status = FileOperations.TaskQueue.TaskStatus.Completed;
                        task.Progress = 100;
                        task.CurrentFile = "完成";
                    }

                    if (successCount > 0)
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() => RequestRefresh?.Invoke());
                    }

                    // Auto-clear completed task after delay
                    Task.Delay(2000).ContinueWith(_ => TaskQueueService?.ClearCompleted());
                }
                catch (Exception ex)
                {
                    task.Status = FileOperations.TaskQueue.TaskStatus.Failed;
                    task.CurrentFile = ex.Message;
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        YiboFile.DialogService.Error($"操作失败: {ex.Message}"));
                }
            });
        }

        private string GetUniqueName(string folder, string fileName)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            string newName = fileName;
            int counter = 1;

            while (File.Exists(Path.Combine(folder, newName)) || Directory.Exists(Path.Combine(folder, newName)))
            {
                newName = $"{nameWithoutExt} - Copy ({counter}){ext}";
                counter++;
            }
            return newName;
        }

        private void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destinationDir, Path.GetFileName(file));
                File.Copy(file, destFile, false);
            }

            foreach (var subdir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destinationDir, Path.GetFileName(subdir));
                CopyDirectory(subdir, destSubDir);
            }
        }

        private ListViewItem GetItemAtLocation(ListView listView, Point point)
        {
            HitTestResult hitTest = VisualTreeHelper.HitTest(listView, point);
            DependencyObject target = hitTest?.VisualHit;

            while (target != null && target != listView)
            {
                if (target is ListViewItem item)
                {
                    return item;
                }
                target = VisualTreeHelper.GetParent(target);
            }
            return null;
        }

        /// <summary>
        /// 更新拖放视觉反馈
        /// </summary>
        private void UpdateDragFeedback(DragEventArgs e, DragDropEffects effect)
        {
            if (_associatedListView == null) return;

            // 获取或创建装饰者层
            if (_adornerLayer == null)
            {
                _adornerLayer = AdornerLayer.GetAdornerLayer(_associatedListView);
            }

            // 创建装饰者
            if (_feedbackAdorner == null && _adornerLayer != null)
            {
                _feedbackAdorner = new DragDropFeedbackAdorner(_associatedListView);
                _adornerLayer.Add(_feedbackAdorner);
            }

            // 更新提示文本和位置
            if (_feedbackAdorner != null)
            {
                string text = effect switch
                {
                    DragDropEffects.Copy => "复制",
                    DragDropEffects.Link => "创建快捷方式",
                    _ => "移动"
                };

                var position = e.GetPosition(_associatedListView);
                _feedbackAdorner.UpdateFeedback(text, position);
            }
        }

        /// <summary>
        /// 移除拖放视觉反馈
        /// </summary>
        private void RemoveDragFeedback()
        {
            if (_feedbackAdorner != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_feedbackAdorner);
                _feedbackAdorner = null;
            }
        }

        /// <summary>
        /// 根据修饰键确定拖放操作类型
        /// 优先级: Alt=快捷方式 > Ctrl=复制 > Shift/默认=移动
        /// </summary>
        private DragDropEffects GetDragDropEffect(DragDropKeyStates keyStates)
        {
            // Alt = 创建快捷方式
            if ((keyStates & DragDropKeyStates.AltKey) == DragDropKeyStates.AltKey)
            {
                return DragDropEffects.Link;
            }
            // Ctrl = 复制
            if ((keyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey)
            {
                return DragDropEffects.Copy;
            }
            // Shift 或默认 = 移动
            return DragDropEffects.Move;
        }

        /// <summary>
        /// 创建快捷方式
        /// </summary>
        private void CreateShortcuts(string[] sources, string targetFolder)
        {
            int successCount = 0;
            try
            {
                foreach (var source in sources)
                {
                    if (!File.Exists(source) && !Directory.Exists(source)) continue;

                    string fileName = Path.GetFileNameWithoutExtension(source);
                    string shortcutPath = Path.Combine(targetFolder, $"{fileName}.lnk");

                    // 确保唯一名称
                    int counter = 1;
                    while (File.Exists(shortcutPath))
                    {
                        shortcutPath = Path.Combine(targetFolder, $"{fileName} ({counter}).lnk");
                        counter++;
                    }

                    // 使用 IWshRuntimeLibrary 创建快捷方式
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null)
                    {
                        dynamic shell = Activator.CreateInstance(shellType);
                        var shortcut = shell.CreateShortcut(shortcutPath);
                        shortcut.TargetPath = source;
                        shortcut.WorkingDirectory = Path.GetDirectoryName(source);
                        shortcut.Save();
                        successCount++;
                    }
                }

                if (successCount > 0)
                {
                    RequestRefresh?.Invoke();
                }
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"创建快捷方式失败: {ex.Message}");
            }
        }
        private Controls.DragDropFeedbackAdorner _sourceDragAdorner;

        private void ListView_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (_sourceDragAdorner != null)
            {
                // 更新位置
                var pos = Mouse.GetPosition(_associatedListView);
                _sourceDragAdorner.UpdateFeedback(_sourceDragAdorner.Text, pos);

                // 必须禁用默认的光标，否则我们画的 Adorner 会和系统光标重叠或者很难看
                // e.UseDefaultCursors = false; 
                // e.Handled = true;
                // 但是如果我们想要系统的 Copy/Move 指针，就保留默认光标
                // 用户的需求是看到“拖拽的是什么文件”，所以 Adorner 只是作为额外信息跟随
                e.UseDefaultCursors = true;
                e.Handled = false;
            }
        }

        private void ShowSourceDragFeedback(ListView listView, List<string> filePaths)
        {
            RemoveSourceDragFeedback(); // 清理旧的

            try
            {
                _adornerLayer = AdornerLayer.GetAdornerLayer(listView);
                if (_adornerLayer != null)
                {
                    _sourceDragAdorner = new Controls.DragDropFeedbackAdorner(listView);
                    _adornerLayer.Add(_sourceDragAdorner);

                    string text = "";
                    if (filePaths.Count == 1)
                        text = System.IO.Path.GetFileName(filePaths[0]);
                    else if (filePaths.Count > 1)
                        text = $"{System.IO.Path.GetFileName(filePaths[0])} 等 {filePaths.Count} 个项";

                    // 初始位置
                    _sourceDragAdorner.UpdateFeedback(text, Mouse.GetPosition(listView));
                }
            }
            catch { }
        }

        private void RemoveSourceDragFeedback()
        {
            if (_sourceDragAdorner != null && _adornerLayer != null)
            {
                try
                {
                    _adornerLayer.Remove(_sourceDragAdorner);
                }
                catch { }
                _sourceDragAdorner = null;
                // 不要置空 _adornerLayer，因为可能被 DropTarget 逻辑共享
            }
        }
    }
}
