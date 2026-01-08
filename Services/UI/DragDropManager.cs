using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Documents;
using OoiMRR.Models.UI;
using OoiMRR.Controls;
using OoiMRR.Services.FileOperations.Undo;
using OoiMRR.Services.UI;

namespace OoiMRR.Services
{
    /// <summary>
    /// Simplified Drag & Drop Manager using standard WPF mechanisms.
    /// handles dragging files from the file list and dropping them onto folders or other supported targets.
    /// </summary>
    public class DragDropManager
    {
        private Point _dragStartPoint;
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
            listView.PreviewMouseMove += ListView_PreviewMouseMove;

            // Drop Target Events (for dropping INTO the list - e.g. from external source or moving to a subfolder)
            listView.AllowDrop = true;
            listView.Drop += ListView_Drop;
            listView.DragOver += ListView_DragOver;
            listView.DragLeave += ListView_DragLeave;
        }

        private void ListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void ListView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            Point currentPosition = e.GetPosition(null);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(currentPosition.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                StartDrag(sender as ListView);
            }
        }

        private void StartDrag(ListView listView)
        {
            System.Diagnostics.Debug.WriteLine("[DragDropManager] StartDrag called");
            if (listView == null)
            {
                System.Diagnostics.Debug.WriteLine("[DragDropManager] StartDrag: listView is null");
                return;
            }

            var selectedItems = listView.SelectedItems.Cast<object>().ToList();
            System.Diagnostics.Debug.WriteLine($"[DragDropManager] StartDrag: {selectedItems.Count} items selected");
            if (selectedItems.Count == 0) return;

            // 如果选中的项目正在重命名，不启动拖放（允许TextBox中的文本选择）
            foreach (var item in selectedItems)
            {
                if (item is FileSystemItem fsItem && fsItem.IsRenaming)
                {
                    System.Diagnostics.Debug.WriteLine("[DragDropManager] StartDrag: Item is renaming, skipping drag");
                    return;
                }
            }

            var filePaths = new List<string>();
            foreach (var item in selectedItems)
            {
                if (item is FileSystemItem fsItem)
                {
                    filePaths.Add(fsItem.Path);
                }
            }

            if (filePaths.Count > 0)
            {
                _isDragging = true;
                try
                {
                    DataObject dataObject = new DataObject(DataFormats.FileDrop, filePaths.ToArray());
                    // Also set text/plain for compatibility
                    dataObject.SetData(DataFormats.Text, string.Join(Environment.NewLine, filePaths));

                    // 允许的操作: 复制 | 移动 | 创建快捷方式
                    DragDropEffects allowedEffects = DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link;
                    DragDrop.DoDragDrop(listView, dataObject, allowedEffects);
                }
                finally
                {
                    _isDragging = false;
                }
            }
        }

        private ListViewItem _lastTargetItem;

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // 修饰键优先级: Alt=快捷方式 > Ctrl=复制 > 默认=移动
                DragDropEffects effect = GetDragDropEffect(e.KeyStates);

                // 检查是否悬停在目录上
                var targetItem = GetItemAtLocation(_associatedListView, e.GetPosition(_associatedListView));

                // 更新高亮状态
                if (targetItem != _lastTargetItem)
                {
                    if (_lastTargetItem != null)
                    {
                        DragAttachedProperties.SetIsDragTarget(_lastTargetItem, false);
                    }
                    _lastTargetItem = targetItem;
                }

                if (targetItem != null && targetItem.Content is FileSystemItem fsItem && fsItem.IsDirectory)
                {
                    // 仅当目标是文件夹时才高亮
                    DragAttachedProperties.SetIsDragTarget(targetItem, true);
                    e.Effects = effect;
                }
                else
                {
                    if (targetItem != null)
                    {
                        DragAttachedProperties.SetIsDragTarget(targetItem, false);
                    }
                    e.Effects = effect;
                }

                // 更新视觉反馈提示
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
            System.Diagnostics.Debug.WriteLine("[DragDropManager] ListView_Drop called");
            RemoveDragFeedback();
            ClearDragTargetHighlight();

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                System.Diagnostics.Debug.WriteLine("[DragDropManager] ListView_Drop: No FileDrop data");
                return;
            }

            string[] sources = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (sources == null || sources.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("[DragDropManager] ListView_Drop: sources is null or empty");
                return;
            }
            System.Diagnostics.Debug.WriteLine($"[DragDropManager] ListView_Drop: {sources.Length} sources");

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

        public void PerformFileOperation(string[] sources, string targetFolder, bool isCopy)
        {
            System.Diagnostics.Debug.WriteLine($"[DragDropManager] PerformFileOperation: isCopy={isCopy}, target={targetFolder}, sources={sources.Length}");
            int successCount = 0;
            List<string> errors = new List<string>();

            try
            {
                foreach (var source in sources)
                {
                    if (!File.Exists(source) && !Directory.Exists(source)) continue;

                    var fileName = Path.GetFileName(source);
                    var destPath = Path.Combine(targetFolder, fileName);

                    bool isSameFolder = string.Equals(Path.GetDirectoryName(source), targetFolder, StringComparison.OrdinalIgnoreCase);

                    // If Moving to same folder, skip
                    if (!isCopy && isSameFolder) continue;

                    // If Copying to same folder, or if destination exists, we need a new name
                    if (File.Exists(destPath) || Directory.Exists(destPath))
                    {
                        if (isCopy)
                        {
                            // Auto-rename for Copy
                            string newName = GetUniqueName(targetFolder, fileName);
                            destPath = Path.Combine(targetFolder, newName);
                        }
                        else
                        {
                            // For Move, if target exists, we usually ask or fail. For now, let's skip to avoid overwrite.
                            // errors.Add($"Skipped '{fileName}' because target already exists.");
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
                    }
                    else // Move
                    {
                        bool isDirectory = Directory.Exists(source);
                        if (isDirectory)
                        {
                            Directory.Move(source, destPath);
                        }
                        else
                        {
                            File.Move(source, destPath);
                        }

                        // 记录撤销操作
                        UndoService?.RecordAction(new MoveUndoAction(source, destPath, isDirectory));
                    }
                    successCount++;
                }

                if (successCount > 0)
                {
                    RequestRefresh?.Invoke();
                }

                // if (errors.Count > 0)
                // {
                //      MessageBox.Show(string.Join("\n", errors), "Operation Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
                // }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Operation failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                MessageBox.Show($"创建快捷方式失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
