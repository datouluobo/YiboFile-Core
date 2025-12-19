using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using OoiMRR.Models.UI;

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

        // Delegate for refreshing the UI after a file operation
        public Action RequestRefresh { get; set; }

        // Delegate to get the current path for background drops
        public Func<string> GetCurrentPath { get; set; }

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
            if (listView == null) return;

            var selectedItems = listView.SelectedItems.Cast<object>().ToList();
            if (selectedItems.Count == 0) return;

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

                    // Allowed effects: Copy | Move
                    DragDropEffects allowedEffects = DragDropEffects.Copy | DragDropEffects.Move;
                    DragDrop.DoDragDrop(listView, dataObject, allowedEffects);
                }
                finally
                {
                    _isDragging = false;
                }
            }
        }

        private void ListView_DragOver(object sender, DragEventArgs e)
        {
            // Determine effect based on keys
            // Ctrl = Copy, Shift = Move, Default = Move (if same drive) or Copy (different drive)

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Check if hovering over a directory item
                var targetItem = GetItemAtLocation(_associatedListView, e.GetPosition(_associatedListView));
                if (targetItem != null && targetItem.Content is FileSystemItem fsItem && fsItem.IsDirectory)
                {
                    // Dropping onto a folder
                    e.Effects = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey
                        ? DragDropEffects.Copy
                        : DragDropEffects.Move;
                    e.Handled = true;
                }
                else
                {
                    // Dropping into the current folder (background)
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private void ListView_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            string[] sources = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (sources == null || sources.Length == 0) return;

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

            // Perform Operation
            bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;

            // If dragging from external -> assume Copy unless Shift is pressed (Move)
            // Or simplified: Just respect Ctrl key. Default without Ctrl is usually Move if same drive, Copy if different.
            // But implementing full Windows logic is complex. 
            // For now: Ctrl -> Copy. No-Ctrl -> Move.
            // UNLESS dropping on background (same folder), where Move is no-op, so maybe default to Copy if source==target?
            // Actually, PerformFileOperation handles the Move-to-same-folder skipping.

            PerformFileOperation(sources, targetPath, isCopy);
        }

        public void PerformFileOperation(string[] sources, string targetFolder, bool isCopy)
        {
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
                        if (Directory.Exists(source))
                        {
                            Directory.Move(source, destPath);
                        }
                        else
                        {
                            File.Move(source, destPath);
                        }
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
    }
}
