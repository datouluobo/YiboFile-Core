using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;
using YiboFile.Services;
using YiboFile.Services.FileOperations;
using YiboFile.Models.UI;

namespace YiboFile
{
    /// <summary>
    /// MainWindow drag and drop logic (Simplified)
    /// </summary>
    public partial class MainWindow : Window
    {
        private DragDropManager _secondDragDropManager;

        private void InitializeDragDrop()
        {
            try
            {
                // Initialize DragDropManager for main file list
                _dragDropManager = new DragDropManager();
                SetupDragDropManager(_dragDropManager, isPrimary: true);

                // Enable file list drag and drop for main list
                if (FileBrowser?.FilesList != null)
                {
                    _dragDropManager.InitializeFileListDragDrop(FileBrowser.FilesList);
                }

                // Initialize DragDropManager for second file list (dual mode)
                _secondDragDropManager = new DragDropManager();
                SetupDragDropManager(_secondDragDropManager, isPrimary: false);

                if (SecondFileBrowser?.FilesList != null)
                {
                    _secondDragDropManager.InitializeFileListDragDrop(SecondFileBrowser.FilesList);
                }

                // Initialize tab drop handlers
                InitializeTabDragDrop();

                // Initialize library drag drop
                InitializeLibraryDragDrop();
            }
            catch (Exception)
            { }
        }

        private void SetupDragDropManager(DragDropManager manager, bool isPrimary)
        {
            manager.RequestRefresh = () =>
            {
                try
                {
                    // Refresh both lists after operation
                    if (_currentLibrary != null)
                        LoadLibraryFiles(_currentLibrary);
                    else
                        LoadCurrentDirectory();

                    // Also refresh second list if in dual mode
                    if (_isDualListMode && SecondFileBrowser != null)
                    {
                        var secondTab = _secondTabService?.ActiveTab;
                        if (secondTab != null && !string.IsNullOrEmpty(secondTab.Path) && Directory.Exists(secondTab.Path))
                        {
                            SecondFileBrowser_PathChanged(this, secondTab.Path);
                        }
                    }
                }
                catch { }
            };

            manager.GetCurrentPath = () =>
            {
                if (isPrimary)
                {
                    return _currentLibrary == null ? _currentPath : null;
                }
                else
                {
                    var secondTab = _secondTabService?.ActiveTab;
                    return secondTab?.Path;
                }
            };

            manager.UndoService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.FileOperations.Undo.UndoService)) as YiboFile.Services.FileOperations.Undo.UndoService;
            manager.TaskQueueService = App.ServiceProvider?.GetService(typeof(YiboFile.Services.FileOperations.TaskQueue.TaskQueueService)) as YiboFile.Services.FileOperations.TaskQueue.TaskQueueService;
        }

        private void InitializeTabDragDrop()
        {
            // Find tab container panels and enable drop
            try
            {
                // Main tab panel
                if (TabManager?.TabsPanelControl != null)
                {
                    TabManager.TabsPanelControl.AllowDrop = true;
                    TabManager.TabsPanelControl.Drop += TabPanel_Drop;
                    TabManager.TabsPanelControl.DragOver += TabPanel_DragOver;
                }

                // Second tab panel
                if (SecondTabManager?.TabsPanelControl != null)
                {
                    SecondTabManager.TabsPanelControl.AllowDrop = true;
                    SecondTabManager.TabsPanelControl.Drop += TabPanel_Drop;
                    SecondTabManager.TabsPanelControl.DragOver += TabPanel_DragOver;
                }
            }
            catch (Exception)
            { }
        }

        private void TabPanel_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Check if hovering over a specific tab button
                var tabButton = FindTabButtonAtPoint(sender as Panel, e.GetPosition(sender as IInputElement));
                if (tabButton != null)
                {
                    e.Effects = DragDropEffects.Copy | DragDropEffects.Move;

                    // Optional: Highlight tab?
                    // Tabs usually have hover states, DragOver might not trigger hover VSM optionally.
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                }
                e.Handled = true;
            }
        }

        private void TabPanel_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            // Find which tab button was dropped on
            var tabButton = FindTabButtonAtPoint(sender as Panel, e.GetPosition(sender as IInputElement));
            if (tabButton == null) return;

            // Get the tab's path from button Tag (Tag is PathTab object)
            // Note: In TabManagerControl, buttons tags are likely PathTab objects
            if (tabButton.Tag is YiboFile.Services.Tabs.PathTab tab && !string.IsNullOrEmpty(tab.Path) && Directory.Exists(tab.Path))
            {
                // Determine operation type
                bool isCopy = (e.KeyStates & DragDropKeyStates.ControlKey) == DragDropKeyStates.ControlKey;
                // Perform the operation
                _dragDropManager?.PerformFileOperation(files, tab.Path, isCopy);
            }
        }

        private Button FindTabButtonAtPoint(Panel panel, Point point)
        {
            if (panel == null) return null;

            var hitTest = VisualTreeHelper.HitTest(panel, point);
            var element = hitTest?.VisualHit;

            while (element != null && element != panel)
            {
                if (element is Button button && button.Tag is YiboFile.Services.Tabs.PathTab)
                {
                    return button;
                }
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private void InitializeLibraryDragDrop()
        {
            // Placeholder for Library Drag & Drop
            if (LibrariesListBox != null)
            {
                LibrariesListBox.AllowDrop = true;
            }
        }
    }
}



